# Leaderboards (Unity Gaming Services)

The game never writes a score itself. It sends the taps of a win to the Cloud Code module
`CircuitCubeRanks`, which plays them again with the game's own rules (`Assets/CircuitCube/Scripts/Core`,
compiled into the module) and posts the score with the service token.

## Boards (`Deploy/*.lb`)

| Board | Sort | Score | Reset |
|---|---|---|---|
| `weekly_<group>_1` … `_3` | ascending | moves × 100000 + tenths of a second | every Thursday 00:00 UTC, archived |
| `weekly_<group>` | ascending | sum of the 3 best moves × 100000 + sum of their times | same |
| `shape_<group>` | descending | Expert levels × 10^12 − total best moves × 10^6 − total seconds | never |
| `daily_players` | descending | 1 per player (unique players today) | every day 17:00 UTC = 00:00 Asia/Saigon, archived |

`RankScore` in `Core/Data/Ranking.cs` builds and reads these numbers. A new shape needs new
`shape_<group id>.lb`, `weekly_<group id>.lb` and `weekly_<group id>_1..3.lb` files (24 weekly boards for 6 shapes).
The old `weekly_1..5` and `weekly_total` boards (made by hand on 2026-11-22) are no longer used: delete them in the dashboard.

## Endpoints

- `SubmitWeekly(key = "<week id>/<group id>", index = slot, taps, tenths)`: only while that week is open (manifest
  `challenge.start` + `days`) and the shape has a set (`challenge.groups`). Keeps the best score per shape and slot
  in protected player data `weekly`.
- `SubmitShape(key = group id, index = level serial, taps, tenths)`: only Expert wins. Keeps the best run (moves, then
  time) per level in protected player data `shape_<group>` (a few KB per shape at most).

- `CheckIn()`: no arguments. The game calls it once per launch after sign-in (and again if it stays open past
  midnight Asia/Saigon). Posts 1 for the player on `daily_players` and answers `players` = unique players today.

All answer `RankReply` (ok, error, board, score, rank, players, experts, moves, tenths).
`SubmitWeekly` refuses a `tenths` below the highest one it has seen for that level this week (`clock`, with that value in
`tenths`): the play clock of a weekly level never resets.
Refusals: `clock`, `content`, `week`, `closed`, `args`, `shape`, `level`, `taps`, `tap`, `unsolved`, `fast`, `not_expert`.

## Deploy

1. Unity Cloud Dashboard: project linked to the Unity project; Authentication (Google Play Games provider with the
   Play Console "Game server" credential), Leaderboards and Cloud Code turned on.
2. Install the UGS CLI (`npm i -g ugs`), then `ugs login` and `ugs config set project-id <id>` and
   `ugs config set environment-name production`.
3. Run `deploy.ps1` in this folder (PowerShell). It publishes the module (`CircuitCubeRanks.csproj`, .NET 6 SDK),
   zips it into `Build/CircuitCubeRanks.ccm` and runs `ugs deploy` with it and `Deploy/` (boards, access policy).
   The CLI on its own does not build the `.ccmr` reference outside the Unity editor ("No content deployed").
   First deployed 2026-11-23: module CircuitCubeRanks (SubmitWeekly, SubmitShape), 24 weekly + 6 shape boards,
   policy `PlayersCannotPostScores`.

In the game, the packages `com.unity.services.authentication`, `com.unity.services.leaderboards` and
`com.unity.services.cloudcode` turn the boards on (define `CC_UGS`); without them the game plays as before.

## Players today

`daily_players` counts every player who opened the game today (Asia/Saigon day), within seconds. To see it:
Unity Cloud Dashboard > Leaderboards > `daily_players`: the number of entries is today's unique players. Past days
are kept as archived versions of the board (Archive on). Only players on a build with the check-in and a
network connection are counted; a reinstall without Play Games makes a new anonymous player.
