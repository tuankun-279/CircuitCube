"""Players sync: real-device numbers and name reports for a game's dashboard (Level Lab "Players" tab).

Reads, never writes game data except a requested name reset:
  today     players checked in today: UGS Cloud Code endpoint PlayersToday (daily_players / daily_players_dev)
  reports   name reports kept by the Cloud Code module (ListReports)
  reset ID  reset one player's name (ResetName); the player's game shows the default name and asks again
  daily     real devices per day, new players, D1 / D7 from the Firebase BigQuery export (Sql/real-devices-daily.sql)
  closed    closed-test progress from BigQuery (Sql/closed-test.sql): testers per day, run of days with the goal
  all       today + reports + daily + closed as one JSON document (what the sync task writes to the dashboard)

Output is JSON on stdout. Settings come from a JSON file (--config, default players_sync.local.json next to the
game's copy of this script, kept out of git):
  {"ugsProject": "...", "ugsEnvironmentId": "...", "module": "...", "gcpProject": "...", "dataset": "...",
   "since": "YYYY-MM-DD", "goal": 12}
Secrets come from the environment only, never from files in git:
  UGS_KEY_ID, UGS_SECRET_KEY            a UGS service account key (Cloud Code calls)
  GOOGLE_APPLICATION_CREDENTIALS        a Google service account JSON with BigQuery Data Viewer + Job User
Needs: Python 3.10+, `pip install google-cloud-bigquery` for daily / closed.
"""
import argparse
import base64
import datetime as dt
import json
import os
import pathlib
import sys
import urllib.request

HERE = pathlib.Path(__file__).resolve().parent
SQL = HERE.parent / "Sql"
SAIGON = dt.timezone(dt.timedelta(hours=7))


def load_config(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def http_json(url, body, headers):
    data = json.dumps(body).encode("utf-8")
    req = urllib.request.Request(url, data=data, headers={"Content-Type": "application/json", **headers}, method="POST")
    with urllib.request.urlopen(req, timeout=30) as r:
        return json.loads(r.read().decode("utf-8") or "{}")


def ugs_token(cfg):
    key, secret = os.environ.get("UGS_KEY_ID"), os.environ.get("UGS_SECRET_KEY")
    if not key or not secret:
        raise SystemExit("Set UGS_KEY_ID and UGS_SECRET_KEY (a UGS service account key) first.")
    basic = base64.b64encode(f"{key}:{secret}".encode()).decode()
    url = (f"https://services.api.unity.com/auth/v1/token-exchange?projectId={cfg['ugsProject']}"
           f"&environmentId={cfg['ugsEnvironmentId']}")
    return http_json(url, {}, {"Authorization": "Basic " + basic})["accessToken"]


def cloud_code(cfg, token, function, params=None):
    url = f"https://cloud-code.services.api.unity.com/v1/projects/{cfg['ugsProject']}/modules/{cfg['module']}/{function}"
    reply = http_json(url, {"params": params or {}}, {"Authorization": "Bearer " + token})
    return reply.get("output", reply)


def all_reports(cfg, token):
    reports, after = [], ""
    for _ in range(50):   # 50 pages at most
        page = cloud_code(cfg, token, "ListReports", {"after": after})
        if not page.get("ok"):
            raise SystemExit("ListReports refused: " + str(page.get("error")))
        reports += page.get("reports", [])
        if not page.get("next") or page.get("next") == after or not page.get("reports"):
            break
        after = page["next"]
    return reports


def bigquery(cfg, file, **values):
    try:
        from google.cloud import bigquery as bq
    except ImportError:
        raise SystemExit("pip install google-cloud-bigquery")
    sql = (SQL / file).read_text(encoding="utf-8")
    values.update(GCP_PROJECT_ID=cfg["gcpProject"], BIGQUERY_DATASET=cfg["dataset"])
    for k, v in values.items():
        sql = sql.replace(f"<{k}>", str(v))
    client = bq.Client(project=cfg["gcpProject"])
    return [dict(row.items()) for row in client.query(sql).result()]


def daily(cfg, days):
    rows = bigquery(cfg, "real-devices-daily.sql", DAYS=int(days))
    return [{"day": r["day"], "players": r["players"], "newPlayers": r["new_players"], "d1": r["d1"], "d7": r["d7"]} for r in rows]


def closed(cfg):
    """Testers per day, the run of days (ending yesterday or today) with at least `goal` testers, D1 / D7."""
    since, goal = cfg.get("since") or (dt.datetime.now(SAIGON).date() - dt.timedelta(days=30)).isoformat(), int(cfg.get("goal", 12))
    rows = bigquery(cfg, "closed-test.sql", SINCE=since)
    per_day, back1, back7, fresh = {}, 0, 0, 0
    today = dt.datetime.now(SAIGON).date()
    for r in rows:
        days = set(r["days"] or [])
        for d in days:
            per_day[d] = per_day.get(d, 0) + 1
        first = r.get("first_open")
        if first:
            f = dt.date.fromisoformat(first)
            if f + dt.timedelta(days=7) < today:
                fresh += 1
                back1 += (f + dt.timedelta(days=1)).isoformat() in days
                back7 += (f + dt.timedelta(days=7)).isoformat() in days
    run, d = 0, today if per_day.get(today.isoformat(), 0) >= goal else today - dt.timedelta(days=1)
    while per_day.get(d.isoformat(), 0) >= goal:
        run += 1
        d -= dt.timedelta(days=1)
    return {
        "day": today.isoformat(), "since": since, "goal": goal, "days": len(per_day),
        "testersToday": per_day.get(today.isoformat(), 0), "streakDays": run,
        "streak": sum(1 for r in rows if len(r["days"] or []) >= 14),
        "d1": round(100 * back1 / fresh, 1) if fresh else None,
        "d7": round(100 * back7 / fresh, 1) if fresh else None,
        "perDay": dict(sorted(per_day.items())),
    }


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("command", choices=["today", "reports", "reset", "daily", "closed", "all"])
    ap.add_argument("player", nargs="?", help="player id for reset")
    ap.add_argument("--config", default="players_sync.local.json")
    ap.add_argument("--days", type=int, default=60)
    a = ap.parse_args()
    cfg = load_config(a.config)
    now = dt.datetime.now(dt.timezone.utc).isoformat()
    out = {}
    if a.command in ("today", "reports", "reset", "all"):
        token = ugs_token(cfg)
        if a.command == "reset":
            if not a.player:
                raise SystemExit("reset needs a player id")
            out = cloud_code(cfg, token, "ResetName", {"player": a.player})
        if a.command in ("today", "all"):
            out["today"] = dict(cloud_code(cfg, token, "PlayersToday"), at=now)
        if a.command in ("reports", "all"):
            out["reports"] = all_reports(cfg, token)
    if a.command in ("daily", "all"):
        out["daily"] = daily(cfg, a.days)
    if a.command in ("closed", "all"):
        out["closed"] = dict(closed(cfg), at=now)
    json.dump(out, sys.stdout, ensure_ascii=False, indent=1, default=str)
    print()


if __name__ == "__main__":
    main()
