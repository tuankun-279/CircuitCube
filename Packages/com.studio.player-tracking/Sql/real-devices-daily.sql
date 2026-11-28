-- Real devices per day (Firebase Analytics -> BigQuery export), Google Play installs only.
-- One row per day (Asia/Saigon): players = distinct devices that sent any event, new_players = first_open that day,
-- d1 / d7 = share (%) of that day's new players who came back 1 / 7 days later (null while the day is too recent).
-- Real device = event parameter channel = 'play' (builds with the studio tracking package, which add it to every
-- event), or, for older builds that send no channel, an install from Google Play (app_info.install_source).
-- Editor, development, test-lab and sideloaded builds are left out.
-- Placeholders: <GCP_PROJECT_ID>, <BIGQUERY_DATASET>, <DAYS> (players_sync.py fills them in).
DECLARE days INT64 DEFAULT <DAYS>;
WITH ev AS (
  SELECT
    user_pseudo_id AS player,
    PARSE_DATE('%Y%m%d', event_date) AS day,
    event_name,
    (SELECT value.string_value FROM UNNEST(event_params) WHERE key = 'channel') AS channel,
    app_info.install_source AS source
  FROM `<GCP_PROJECT_ID>.<BIGQUERY_DATASET>.events_*`
  WHERE _TABLE_SUFFIX >= FORMAT_DATE('%Y%m%d', DATE_SUB(CURRENT_DATE('Asia/Saigon'), INTERVAL days + 7 DAY))
), real AS (
  SELECT player, day, event_name FROM ev
  WHERE channel = 'play' OR (channel IS NULL AND source = 'com.android.vending')
), active AS (
  SELECT DISTINCT player, day FROM real
), fresh AS (
  SELECT player, MIN(day) AS first_day FROM real WHERE event_name = 'first_open' GROUP BY player
), per_day AS (
  SELECT day, COUNT(DISTINCT player) AS players FROM active GROUP BY day
), cohort AS (
  SELECT
    f.first_day AS day,
    COUNT(DISTINCT f.player) AS new_players,
    COUNT(DISTINCT IF(a.day = DATE_ADD(f.first_day, INTERVAL 1 DAY), f.player, NULL)) AS back1,
    COUNT(DISTINCT IF(a.day = DATE_ADD(f.first_day, INTERVAL 7 DAY), f.player, NULL)) AS back7
  FROM fresh f LEFT JOIN active a ON a.player = f.player
  GROUP BY day
)
SELECT
  FORMAT_DATE('%Y-%m-%d', p.day) AS day,
  p.players,
  IFNULL(c.new_players, 0) AS new_players,
  IF(DATE_ADD(p.day, INTERVAL 1 DAY) >= CURRENT_DATE('Asia/Saigon') OR IFNULL(c.new_players, 0) = 0, NULL,
     ROUND(100 * c.back1 / c.new_players, 1)) AS d1,
  IF(DATE_ADD(p.day, INTERVAL 7 DAY) >= CURRENT_DATE('Asia/Saigon') OR IFNULL(c.new_players, 0) = 0, NULL,
     ROUND(100 * c.back7 / c.new_players, 1)) AS d7
FROM per_day p LEFT JOIN cohort c ON c.day = p.day
WHERE p.day >= DATE_SUB(CURRENT_DATE('Asia/Saigon'), INTERVAL days DAY)
ORDER BY day;
