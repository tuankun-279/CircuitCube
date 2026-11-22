# Circuit Cube

A 3D circuit puzzle for mobile: rotate the tiles on a cube (and other shapes) so every lamp is lit by a source of the right colour.

- **Engine:** Unity 6000.0.76f1 (URP), Android first.
- **Version:** 1.3.0 (version code 6)

## Layout

| Path | What |
| --- | --- |
| `Assets/CircuitCube/Scripts` | Game code (`Core` = puzzle logic, `Game` = board, data, save, theme, `UI`) |
| `Assets/CircuitCube/Editor` | Editor setup tools (`Tools/Circuit Cube/...` menus) |
| `Assets/CircuitCube/Resources/CircuitCubeData` | Level data v3 (manifest, groups, levels, shapes) |
| `Assets/CircuitCube/Art` | Art: `Board/{Dark,Light,Shared}`, `UI/{Common,Icons,Shapes,Brand,Mascot,Backgrounds}`, `Fonts`, `Atlases`, `Materials/Board` (layout in `Editor/ArtPaths.cs`) |
| `Assets/CircuitCube/Themes` | `Theme_Dark` / `Theme_Light` assets |
| `Assets/CircuitCube/Tests` | Edit-mode tests |
| `Tools/LevelLab/index.html` | Level Lab, the web level editor (published as a claude.ai artifact): shapes, boards, play test, batch generation, groups and rules, **Publish to game** |
| `Tools/LevelLab/poly.js`, `publish.py` | The polygon shapes Level Lab shares with the game, and the script that turns its database into `content/` |
| `content/` | Levels as the game downloads them (written only by publishing from Level Lab) |
| `ArtRef` | Art references, mockup and the sprite cutting scripts (`tools/cut_v2.py`) |

See `Assets/CircuitCube/README.md` for the data format and editor menus.

## Making levels without Unity

1. Open Level Lab (claude.ai artifact `WUkC8DCM543afEtakLwwx3`). Design or batch-generate levels, reorder them, edit
   groups and rules; everything saves in the page's database.
2. Press **Publish to game**. It starts the scheduled task "Circuit Cube: publish Level Lab levels", which checks
   every level (`Tools/LevelLab/publish.py`), commits `content/` to main, then pins that commit in
   `content/manifest.json` (`cdn` = the commit on jsDelivr). The status line in Level Lab shows the result.
3. Players get it without restarting: the game reads `content/manifest.json` from GitHub
   (`CircuitCubeApp.ContentBaseUrl`) at start (the splash waits up to 3.5 s for it), every 3 minutes and when it
   comes back to the front, then downloads new or edited levels around their position from the pinned commit. A
   player in a level finishes it first and then sees a "New levels are here" card.

Unity is only needed to ship a new build: run Tools > Circuit Cube > Data > Copy Published Content into the Build so
the build carries the latest publish for offline players.

## Services (1.2.0)

Each one is built only when its package is in `Packages/manifest.json` (version defines in `CircuitCube.asmdef`);
without it the game runs as before.

| Service | Package | Code | Set up outside Unity |
| --- | --- | --- | --- |
| Crash reports | `com.google.firebase.crashlytics` (CC_CRASHLYTICS) | `Game/Services/CrashReports.cs` | Firebase console > Crashlytics: enable |
| Rating card | `com.google.play.review` (CC_REVIEW) | `Game/Services/AppReview.cs` | none (works from a Play install) |
| Cloud save | `com.google.play.games` (CC_PLAYGAMES) | `Game/Services/CloudSave.cs`, `CubeSave.Cloud.cs` | Play Console > Play Games Services: create, add the Android credential (SHA-1 of the app signing key), Saved Games on; paste the resources into Window > Google Play Games > Setup > Android setup |
| Vietnamese | built in | `Game/Loc.cs`, `Game/LocVi.cs`, `UI/LocText.cs` | none |

## Getting started

1. Open the folder with Unity Hub (Unity 6000.0.76f1).
2. Open `Assets/CircuitCube/Scenes/CircuitCube.unity` and press Play.
3. Debug keys: F1 debug menu, F2 toggle dark/light theme.
