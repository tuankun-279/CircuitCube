# Unity Cloud Dashboard (Unity Gaming Services)

Dashboard: https://cloud.unity.com. Environment: `production` (add `development` for tests if wanted).

## 1. Project and link (once)

1. Create the project "CircuitCube" (or pick the existing one).
2. Unity Editor: Edit > Project Settings > Services: sign in, **Link** to that project. Note the project id in
   [README.md](README.md).
3. Package Manager: add `com.unity.services.authentication`, `com.unity.services.leaderboards`,
   `com.unity.services.cloudcode` (they bring `com.unity.services.core`). Their presence turns on `CC_UGS`.
   Commit `Packages/manifest.json` and `packages-lock.json`.

## 2. Player Authentication

- Identity providers > **Google Play Games**: client id and secret of the Play Console "Game server" credential
  ([play-console.md](play-console.md)). The owner types the secret.
- Anonymous sign-in stays on (players without Play Games).
- Player names: on (the game sets them through `Ranks.Rename`, cleaned by `NameFilter`).

## 3. Leaderboards, Cloud Code, Cloud Save, access control

All of it is in files under `Tools/CloudCode/Deploy` and goes up in one command (see `Tools/CloudCode/README.md`):

```
npm i -g ugs
ugs login                      # owner signs in, or a service account key kept on the PC only
ugs config set project-id <project id>
ugs config set environment-name production
ugs deploy Tools/CloudCode/Deploy
```

Service account for `ugs login` (Unity Cloud > Administration > Service Accounts > New, e.g. `circuitcube-deploy`),
Manage project roles on the game's project:
- Admin: Project Resource Policy Editor, Player Resource Policy Editor, Unity Environments Viewer.
- LiveOps: Cloud Code Editor, Leaderboards Admin, Cloud Save Editor.
- DevOps: none. Then Add key; the owner runs `ugs login` > Service Account and pastes the key on the PC (never in git
  or chat). Delete the key when deploys are done; make a new one next time.

- 24 weekly boards, per shape `weekly_<group>_1..3` and the set total `weekly_<group>`, reset Thursdays 00:00 UTC;
  the first `Start` in the files is 2026-12-03. 6 shape boards `shape_<group>`.
- After the first deploy, delete the 6 old hand-made boards `weekly_1..5` and `weekly_total` (unused since the
  weekly challenge moved to one set per shape).
- Module `CircuitCubeRanks` (needs the .NET 6 SDK on the machine that deploys).
- `ranks.ac`: players cannot post scores; only the module does, with the service token.
- Cloud Save needs no setup; the module writes protected player data `weekly` and `shape_<group>`.

## State (2026-11-22)

- Leaderboards on; the 12 boards were created by hand in the dashboard with the same settings as the `.lb` files
  (weekly: ascending, best score, weekly reset Thursday 00:00 UTC from 2026-12-03, archive on; shape: descending,
  latest score, no reset). A later `ugs deploy` updates them in place.
- Access Control project policy: statement `deny-write-leaderboards` (Deny, Write, Leaderboards, everything).
- Play Console: Game server credential "Circuit Cube" with the Web application OAuth client
  `6185798786-ggj1m5g0cl6vr8kosijnl3k46mmsbgal.apps.googleusercontent.com`. Play Games project published
  2026-11-22 (first publish: it also needed description, category Puzzle, icon and feature graphic from
  `StoreListing/`). Before that only testers could sign in with Play Games.
- Player Authentication: Google Play Games identity provider enabled with that client id (secret typed by the owner).
- 2026-11-23: deployed from the PC with `Tools/CloudCode/deploy.ps1` (service account login): module
  `CircuitCubeRanks` (SubmitWeekly, SubmitShape), the 24 weekly and 6 shape boards from the `.lb` files, policy
  `PlayersCannotPostScores`. The old hand-made boards `weekly_1..5` and `weekly_total` are still there (unused).
- Game: UGS packages installed in 1.4.0 (authentication 3.9.0, leaderboards 2.4.0, cloudcode 2.10.4), so `CC_UGS` is on.

## 4. Check after deploying

- Leaderboards page lists the 12 boards with the right sort and reset.
- Cloud Code > Modules shows `CircuitCubeRanks` with `SubmitWeekly` and `SubmitShape`.
- Play one Expert level on a test phone: the shape board gets a row; Cloud Code logs show no error.
- Free tier and prices: https://unity.com/products/gaming-services/pricing (check before the player count grows).
