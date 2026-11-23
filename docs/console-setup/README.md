# Console setup (Play Console, Unity Cloud, Firebase)

Step lists for every outside console the game depends on, and templates for the files that come back into the
repo. Use them when setting a service up for the first time, adding a shape, or checking why a service is off.
**No secrets go in this folder or anywhere in git**: client secrets, service-account keys and passwords are typed
by the owner in the console or kept on the PC.

| Console | Page | Used for |
|---|---|---|
| Google Play Console | [play-console.md](play-console.md) | Play Games sign-in, cloud save, the "Game server" credential for UGS, Data safety |
| Unity Cloud Dashboard | [unity-dashboard.md](unity-dashboard.md) | Authentication, Leaderboards, Cloud Code, Cloud Save, access control |
| Firebase | [firebase.md](firebase.md) | Analytics, Crashlytics |

## Known values (not secret)

| What | Value | Where it lives |
|---|---|---|
| Android package | `com.zyerkun.circuitcube` | Player Settings, `Assets/google-services.json` |
| Play Games app id | `6185798786` | `Assets/GooglePlayGames/Resources/PlayGamesSettings.asset` (`mAppId`) |
| Play Games web client id | `6185798786-ggj1m5g0cl6vr8kosijnl3k46mmsbgal.apps.googleusercontent.com` (OAuth client "Circuit Cube", Web application, Google Cloud project circuit-cube; Game server credential made 2026-11-22) | same asset, `mWebClientId` (Window > Google Play Games > Setup) |
| Firebase project | `circuit-cube` | `Assets/google-services.json` |
| UGS project | `Puzzle_Robot`, id `1040eab6-9bcb-4d53-af4c-fc3205412b87`, org `18967060165293`, env `production` | `ProjectSettings/ProjectSettings.asset` (`cloudProjectId`) |
| Content manifest | `https://raw.githubusercontent.com/tuankun-279/CircuitCube/main/content/manifest.json` | scene field `ContentBaseUrl`, `Tools/CloudCode/CircuitCubeRanks/Ranks.cs` |

Update this table whenever a value changes.

## Templates

- [templates/weekly-board.lb](templates/weekly-board.lb): a weekly challenge board (ascending, keep best, Thursday reset).
- [templates/shape-board.lb](templates/shape-board.lb): a shape board (descending, never reset). Copy to
  `Tools/CloudCode/Deploy/shape_<group id>.lb` when a shape is added.
- [templates/access-policy.ac](templates/access-policy.ac): players may not post scores themselves.
- [templates/new-shape-checklist.md](templates/new-shape-checklist.md): everything to touch when a shape is added.
