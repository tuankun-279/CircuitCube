-- Closed test progress (Firebase Analytics -> BigQuery export), real devices only (see real-devices-daily.sql).
-- One row per tester device since the test started: the days it opened the game. players_sync.py and the closed-test
-- board turn this into testers per day, the run of days with at least 12 testers (Google Play asks for 12 testers
-- opted in for 14 days in a row before production), and D1 / D7.
-- Placeholders: <GCP_PROJECT_ID>, <BIGQUERY_DATASET>, <SINCE> (YYYY-MM-DD, the first day of the closed test).
SELECT
  user_pseudo_id AS tester,
  ARRAY_AGG(DISTINCT FORMAT_DATE('%Y-%m-%d', PARSE_DATE('%Y%m%d', event_date)) ORDER BY FORMAT_DATE('%Y-%m-%d', PARSE_DATE('%Y%m%d', event_date))) AS days,
  MIN(IF(event_name = 'first_open', FORMAT_DATE('%Y-%m-%d', PARSE_DATE('%Y%m%d', event_date)), NULL)) AS first_open
FROM `<GCP_PROJECT_ID>.<BIGQUERY_DATASET>.events_*`
WHERE _TABLE_SUFFIX >= REPLACE('<SINCE>', '-', '')
  AND ((SELECT value.string_value FROM UNNEST(event_params) WHERE key = 'channel') = 'play'
       OR ((SELECT value.string_value FROM UNNEST(event_params) WHERE key = 'channel') IS NULL
           AND app_info.install_source = 'com.android.vending'))
GROUP BY tester;
