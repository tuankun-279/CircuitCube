# Leaderboards (Unity Gaming Services)

The game never writes a score itself. It sends the taps of a win to the Cloud Code module
`CircuitCubeRanks`, which plays them again with the game's own rules (`Assets/CircuitCube/Scripts/Core`,
compiled into the module) and posts the score with the service token.

## Boards (`Deploy/*.lb`)

| Board | Sort | Score | Reset |
|---|---|---|---|
| `weekly_1` … `weekly_5` | ascending | moves × 100000 + tenths of a second | every Thursday 00:00 UTC, archived |
| `weekly_total` | ascending | sum of the 5 best moves × 100000 + sum of their times | same |
| `shape_<group>` | descending | Expert levels × 10000000 − total of their best times (tenths) | never |

`RankScore` in `Core/Data/Ranking.cs` builds and reads these numbers. A new shape needs a new
`shape_<group id>.lb` file.

## Endpoints

- `SubmitWeekly(key = week id, index = slot, taps, tenths)`: only while that week is open (manifest
  `challenge.start` + `days`). Keeps the best score per slot in protected player data `weekly`.
- `SubmitShape(key = group id, index = level serial, taps, tenths)`: only Expert wins. Keeps the best time per
  level in protected player data `shape_<group>` (a few KB per shape at most).

Both answer `RankReply` (ok, error, board, score, rank, players, experts, tenths).
Refusals: `content`, `week`, `closed`, `args`, `shape`, `level`, `taps`, `tap`, `unsolved`, `fast`, `not_expert`.

## Deploy

1. Unity Cloud Dashboard: project linked to the Unity project; Authentication (Google Play Games provider with the
   Play Console "Game server" credential), Leaderboards and Cloud Code turned on.
2. Install the UGS CLI (`npm i -g ugs`), then `ugs login` and `ugs config set project-id <id>` and
   `ugs config set environment-name production`.
3. From this folder: `ugs deploy Deploy` (boards, access policy and the module through `CircuitCubeRanks.ccmr`;
   the CLI builds the solution, which needs the .NET 6 SDK).

In the game, the packages `com.unity.services.authentication`, `com.unity.services.leaderboards` and
`com.unity.services.cloudcode` turn the boards on (define `CC_UGS`); without them the game plays as before.
