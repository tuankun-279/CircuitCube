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

- 6 weekly boards (`weekly_1..5`, `weekly_total`) reset Thursdays 00:00 UTC; the first `Start` in the files is
  2026-12-03. 6 shape boards `shape_<group>`.
- Module `CircuitCubeRanks` (needs the .NET 6 SDK on the machine that deploys).
- `ranks.ac`: players cannot post scores; only the module does, with the service token.
- Cloud Save needs no setup; the module writes protected player data `weekly` and `shape_<group>`.

## State (2026-11-22)

- Leaderboards on; the 12 boards were created by hand in the dashboard with the same settings as the `.lb` files
  (weekly: ascending, best score, weekly reset Thursday 00:00 UTC from 2026-12-03, archive on; shape: descending,
  latest score, no reset). A later `ugs deploy` updates them in place.
- Access Control project policy: statement `deny-write-leaderboards` (Deny, Write, Leaderboards, everything).
- Not yet: Google Play Games identity provider, Cloud Code module.

## 4. Check after deploying

- Leaderboards page lists the 12 boards with the right sort and reset.
- Cloud Code > Modules shows `CircuitCubeRanks` with `SubmitWeekly` and `SubmitShape`.
- Play one Expert level on a test phone: the shape board gets a row; Cloud Code logs show no error.
- Free tier and prices: https://unity.com/products/gaming-services/pricing (check before the player count grows).
