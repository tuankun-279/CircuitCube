# Leaderboards (Unity Gaming Services)

The game never writes a score itself. It sends the taps of a win to the Cloud Code module
`CircuitCubeRanks`, which plays them again with the game's own rules (`Assets/CircuitCube/Scripts/Core`,
compiled into the module) and posts the score with the service token.

## Boards (`Deploy/*.lb`)

| Board | Sort | Score | Reset |
|---|---|---|---|
| `weekly_<group>_1` … `_3` | ascending | moves × 100000 + tenths of a second | every Wednesday 23:50 UTC (Thursday 06:50 Vietnam, before the 07:00 week start), archived; wins of the old week close at 23:45 UTC |
| `weekly_<group>` | ascending | sum of the 3 best moves × 100000 + sum of their times | same |
| `shape_<group>` | descending | Expert levels × 10^12 − total best moves × 10^6 − total seconds | never |
| `daily_players` | descending | 1 per player (unique Google Play players today) | every day 17:00 UTC = 00:00 Asia/Saigon, archived |
| `daily_players_dev` | descending | 1 per test device (unique test devices today) | same as `daily_players`, archived |

`RankScore` in `Core/Data/Ranking.cs` builds and reads these numbers. A new shape needs new
`shape_<group id>.lb`, `weekly_<group id>.lb` and `weekly_<group id>_1..3.lb` files (24 weekly boards for 6 shapes).
The old `weekly_1..5` and `weekly_total` boards (made by hand on 2026-11-22) are no longer used: delete them in the dashboard.

## Environments

There are two UGS environments. No ids are listed here.

- `development`: the Unity Editor, development builds, Firebase Test Lab and Play pre-launch robots, and APKs
  installed from the PC.
- `production`: Google Play installs only.

The game picks the environment in `Assets/CircuitCube/Scripts/Game/Services/Ranks.cs` (`Ranks.Environment`). It comes
from `BuildChannel` in `Packages/com.studio.player-tracking`. The Editor and development builds never use production.

## Endpoints

- `SubmitWeekly(key = "<week id>/<group id>", index = slot, taps, tenths)`: only while that week is open (manifest
  `challenge.start` + `days`) and the shape has a set (`challenge.groups`). Keeps the best score per shape and slot
  in protected player data `weekly`.
- `SubmitShape(key = group id, index = level serial, taps, tenths)`: only Expert wins. Keeps the best run (moves, then
  time) per level in protected player data `shape_<group>` (a few KB per shape at most).

- `CheckIn()`: no arguments. Kept for builds before 1.5.0, which send no channel. They were all store installs, so
  they are counted on `daily_players`.
- `DailyCheckIn(channel, version)`: 1.5.0 and later. The game calls it once per launch after sign-in (and again if it
  stays open past midnight Asia/Saigon). `channel` is `editor`, `dev`, `testlab`, `sideload` or `play`. Only `play` is
  counted on `daily_players`. Every other channel goes to `daily_players_dev`. Channel and version are stored as score
  metadata (no personal data).
- `NameState()`: answers `renamesLeft`, `named` and `reset`.
- `ClaimName(name, first)`: checks the name with the same rules file as the game
  (`Assets/CircuitCube/Resources/NameRules.txt`, embedded in the module).
  - `first` = the name sheet after the tutorial of a new player. It is free, and there is no free rename after it.
  - Otherwise it is a rename from Settings: one free rename for players from before 1.5.0, and a new choice after a
    reset.
  - Refusals: `name`, `reserved`, `empty`, `no_renames`.
  - The game sets the service name only after `ok`. The record is kept in protected player data `name`.
- `ReportName(player, name, board)`: a player reports the name in another row. At most 10 a day, and one per reported
  player a day (protected player data `reports`). Reports go to Cloud Save custom data `name_reports`.
- Admin endpoints, only for service-account calls (no player id):
  - `ListReports(after)`
  - `ResetName(player)`
  - `PlayersToday()`: entries on `daily_players` and `daily_players_dev` today.

All answer `RankReply` (ok, error, board, score, rank, players, experts, moves, tenths), except the name endpoints,
which have their own replies.
`SubmitWeekly` scores a win with the highest `tenths` it has seen for that level this week, if that is above the one sent
(the play clock of a weekly level never resets), and answers with the time that counted in `tenths`.
Refusals: `content`, `week`, `closed`, `args`, `shape`, `level`, `taps`, `tap`, `unsolved`, `fast`, `not_expert`.

## Deploy

1. Unity Cloud Dashboard: project linked to the Unity project; Authentication (Google Play Games provider with the
   Play Console "Game server" credential), Leaderboards and Cloud Code turned on.
2. Install the UGS CLI (`npm i -g ugs`), then `ugs login` and `ugs config set project-id <id>`.
3. Run `deploy.ps1 -Env development|production` in this folder (PowerShell). `-Env` is mandatory. It publishes the
   module (`CircuitCubeRanks.csproj`, .NET 6 SDK), zips it into `Build/CircuitCubeRanks.ccm` and runs `ugs deploy` with
   it and `Deploy/` (boards, access policy) in that environment.
   The CLI on its own does not build the `.ccmr` reference outside the Unity editor ("No content deployed").
4. Deploy to `development` first and test. Deploy to `production` only after the owner confirms.

UGS refuses a board whose reset start is in the past. So `deploy.ps1` skips a board that already exists in the
environment and has a reset start in the past. A board that is new to the environment gets its `Start` moved forward by
whole schedule periods (a day, or a week for weekly boards).

State:
- First deployed 2026-11-23: module CircuitCubeRanks (SubmitWeekly, SubmitShape), 24 weekly + 6 shape boards,
  policy `PlayersCannotPostScores`.
- 2026-11-28: `development` got the 1.5.0 module (with the new endpoints), all boards and both policy statements.
- `production` still runs the 1.4.x module until the owner confirms the 1.5.0 deploy.

The access policy `ranks.ac` has two statements: `PlayersCannotPostScores` and `PlayersCannotReadNameReports` (players
cannot read or write custom data `name_reports`).

In the game, the packages `com.unity.services.authentication`, `com.unity.services.leaderboards` and
`com.unity.services.cloudcode` turn the boards on (define `CC_UGS`); without them the game plays as before.

## Players today

`daily_players` counts the Google Play installs that opened the game today (Asia/Saigon day), within seconds. That is
1.5.0 and later with channel `play`, plus older builds. Test devices are on `daily_players_dev`. To see them:
Unity Cloud Dashboard > Leaderboards > `daily_players` (or `daily_players_dev`), in the right environment: the number
of entries is today's unique players. Past days are kept as archived versions of the board (Archive on). Only players
on a build with the check-in and a network connection are counted; a reinstall without Play Games makes a new
anonymous player.

The Level Lab Players tab shows both boards. It is written by the players sync
(`Packages/com.studio.player-tracking/Tools~/players_sync.py`).
