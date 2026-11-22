# Leaderboard logic

Status: draft for review (2026-11-22). The board UI is approved: see the design canvas
https://claude.ai/artifact/BudjQEtz8hkYqohpBmw1Do (screens 1-6). This page is the rules behind it.

## Goals

- Players compete on **fewest moves, then fastest time**, on puzzles that are the same for everyone.
- Long-term progress per shape is rewarded without storing a score per level per player.
- No score is trusted from the phone: the server replays every win (see "Anti-cheat").

## Two kinds of boards

| Board | What ranks | Sort | Resets | Who can enter |
|---|---|---|---|---|
| Weekly challenge | Moves, then time, on the week's challenge levels | ascending | every Thursday 00:00 UTC (07:00 Vietnam) | anyone who has the shape unlocked |
| Shape (lifetime) | Number of Expert levels in the shape, then total moves of their best runs, then total time | descending | never | anyone |

There are **no per-level boards for normal levels** (Tuan, 2026-11-22: keep player data small).

## Weekly challenge: split by shape (decided 2026-11-22, 3 levels per shape)

Each Thursday the content publish adds a **challenge set per shape**: `N` = **3** levels (Tuan, 2026-11-22) for each of the 6 shapes
(cube Moonbase, tetra Pyramid, octa Star Gem, sphere Planet, dodeca Galaxy, megamix Nebula), ordered easy to hard.

- A player sees the sets of the shapes they have unlocked; a locked shape shows "Unlock Pyramid to join".
- Rules while playing a challenge level: no hints, no undo (restart is allowed; only the winning run is sent).
- Boards per shape:
  - `weekly_<group>_<slot>`: one per level, best run counts (score = moves x 100000 + tenths of a second).
  - `weekly_<group>`: the set total, only after all `N` levels are solved: sum of best moves, then sum of best times.
- Board count: 6 x (N + 1). N = 3 gives 24 boards, N = 5 gives 36 (UGS has no practical limit at this size).
- Weekly boards are archived on reset, so last week's top players can be shown ("Last week's winners").

Why by shape and not one mixed set:
- Shapes play differently (triangle tiles turn in thirds, planets have six exits). A mixed set favours players who happen
  to be strong on one shape and locks out new players who only have Moonbase.
- Each shape board stays fair: everyone on it solved the same kind of puzzle.
- Cost: players are split over up to 6 boards, so each board is thinner while the player base is small. If a board has
  very few players, the UI can still show it; nothing breaks.

### What similar games do

- Microsoft Solitaire Collection: Daily Challenges have one challenge per game (Klondike, Spider, FreeCell, ...), so
  challenges are split by game mode; its multi-day Events mix games and difficulties. Players see their progress
  next to players worldwide when logged in.
  (https://en.wikipedia.org/wiki/Microsoft_Solitaire_Collection)
- Microsoft Sudoku: 3 Daily Challenges a day spread over its modes (Classic, Irregular, Ice Breaker), with badges and
  personal best times. (https://apps.apple.com/app/id1462494195)
- Flow Free: Daily Puzzles every day; its Time Trial mode is split by board size and duration (20 variants).
  (https://apps.apple.com/us/app/id526641427)
- Infinity Loop (rotate-tile puzzle like ours): Daily Challenges with trophies and Game Center leaderboards.
  (https://apps.apple.com/us/app/id977028266)

Pattern: daily or weekly challenges are kept **per mode / per board type** when modes play differently; mixed sets are
used for special events. That supports splitting the weekly challenge by shape.

## Shape boards (lifetime)

- A level counts once it is won as **Expert**: no hint, no undo, at most `minTaps + wasteAllowed` moves (default 3).
- Order (Tuan, 2026-11-22): **most Expert levels, then fewest total moves, then shortest total time**. Totals are
  over the player's best run of each Expert level (best = fewest moves, then shorter time).
- Score = experts x 10^12 - total moves x 10^6 - total whole seconds. Caps: 9,000 Expert levels, 999,999 moves,
  999,999 seconds (about 11.5 days); the largest score stays below 2^53, so the double is exact.
- Replaying an Expert level in fewer moves, or as many moves but faster, improves the score.
- Expert wins from before the update do not count (the server never saw their taps). Open question for Tuan.

## Data kept per player (protected Cloud Save, written only by the server)

- `weekly`: `{ week, sets: { <group>: [best score per slot] } }`. Replaced when the week changes.
- `shape_<group>`: `{ levels: { <serial>: { m: moves, t: tenths } } }`, only Expert levels. About 20 bytes per Expert level.

## Flow of one win

1. The game records the taps (tile index per tap) and the play time of the run.
2. On a win that qualifies (challenge level, or Expert on a shape level), it queues the run in
   `save/ranks_pending.json` and sends it when online.
3. Cloud Code `CircuitCubeRanks` fetches the level from the published content, replays the taps with the game's Core
   code, checks the board ends solved, the taps are legal and the time is believable (at least 0.15 s per tap).
4. If the run beats the stored best, the server updates the player record and posts to the board(s), then replies
   with the rank and player count for the win card.

## Anti-cheat

- Clients cannot write to Leaderboards (Access Control deny statement); only Cloud Code posts with the service token.
- Replays use the published level file, never level data from the client.
- Time is still self-reported; the minimum-time check stops the obvious fakes.

## Implementation (done in PR #3, 2026-11-22)

- Manifest `challenge`: `{ id, start, days, groups: [<group>...], count: 3 }`; level files at
  `challenge/<week>/<group>/<slot>.json`.
- `SubmitWeekly(key = "<week>/<group>", index = slot, ...)`; boards `weekly_<group>_<slot>` and `weekly_<group>`.
- Still to do: replace the 6 weekly boards in the Unity dashboard (`weekly_1..5`, `weekly_total`) with the new ids
  (`Tools/CloudCode/Deploy/*.lb`, then `ugs deploy`).
- Still to do: the monthly level batch generates `N` extra challenge levels per shape per week (see the monthly levels task).
- UI (design updated): the Weekly screen gets a shape picker (same chips as the shape board) above the level list.
