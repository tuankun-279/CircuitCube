-- Which shapes players play the most (Firebase Analytics -> BigQuery, dataset analytics_563777735).
-- One row per shape for the last 30 days: players, levels started and won, minutes played, share of all play
-- and how often players picked the shape on the Shapes screen (shape_open from 1.4.2).
-- Use it to decide which shape gets more levels in the next monthly batch.
DECLARE days INT64 DEFAULT 30;
WITH ev AS (
  SELECT
    user_pseudo_id AS player,
    event_name,
    (SELECT value.string_value FROM UNNEST(event_params) WHERE key = 'shape') AS shape,
    (SELECT COALESCE(value.int_value, CAST(value.double_value AS INT64)) FROM UNNEST(event_params) WHERE key = 'seconds') AS seconds,
    (SELECT value.string_value FROM UNNEST(event_params) WHERE key = 'from') AS src
  FROM `circuit-cube.analytics_563777735.events_*`
  WHERE _TABLE_SUFFIX >= FORMAT_DATE('%Y%m%d', DATE_SUB(CURRENT_DATE('Asia/Saigon'), INTERVAL days DAY))
    AND event_name IN ('level_start', 'level_complete', 'level_quit', 'shape_open', 'weekly_complete')
), per AS (
  SELECT
    shape,
    COUNT(DISTINCT IF(event_name = 'level_start', player, NULL)) AS players,
    COUNTIF(event_name = 'level_start') AS starts,
    COUNTIF(event_name = 'level_complete') AS wins,
    ROUND(SUM(IF(event_name IN ('level_complete', 'level_quit'), seconds, 0)) / 60, 1) AS minutes,
    COUNTIF(event_name = 'shape_open' AND src = 'shapes') AS picked,
    COUNTIF(event_name = 'weekly_complete') AS weekly_wins
  FROM ev
  WHERE shape IS NOT NULL
  GROUP BY shape
)
SELECT
  shape, players, starts, wins,
  ROUND(SAFE_DIVIDE(wins, starts) * 100, 1) AS win_rate_pct,
  minutes,
  ROUND(SAFE_DIVIDE(minutes, SUM(minutes) OVER ()) * 100, 1) AS share_of_play_pct,
  ROUND(SAFE_DIVIDE(minutes, players), 1) AS minutes_per_player,
  picked, weekly_wins
FROM per
ORDER BY minutes DESC;

-- Players by main shape (user property main_shape from 1.4.2), latest value per player:
-- SELECT main_shape, COUNT(*) AS players FROM (
--   SELECT user_pseudo_id,
--     ARRAY_AGG((SELECT value.string_value FROM UNNEST(user_properties) WHERE key = 'main_shape') IGNORE NULLS
--       ORDER BY event_timestamp DESC LIMIT 1)[SAFE_OFFSET(0)] AS main_shape
--   FROM `circuit-cube.analytics_563777735.events_*`
--   WHERE _TABLE_SUFFIX >= FORMAT_DATE('%Y%m%d', DATE_SUB(CURRENT_DATE('Asia/Saigon'), INTERVAL 30 DAY))
--   GROUP BY user_pseudo_id)
-- GROUP BY main_shape ORDER BY players DESC;
