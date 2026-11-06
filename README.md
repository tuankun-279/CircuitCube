# Circuit Cube

A 3D circuit puzzle for mobile: rotate the tiles on a cube (and other shapes) so every lamp is lit by a source of the right colour.

- **Engine:** Unity 6000.0.76f1 (URP), Android first.
- **Version:** 0.1.1

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
3. Players get it on their next start: the game reads `content/manifest.json` from GitHub
   (`CircuitCubeApp.ContentBaseUrl`) and downloads new or edited levels around their position from the pinned commit.

Unity is only needed to ship a new build: run Tools > Circuit Cube > Data > Copy Published Content into the Build so
the build carries the latest publish for offline players.

## Getting started

1. Open the folder with Unity Hub (Unity 6000.0.76f1).
2. Open `Assets/CircuitCube/Scenes/CircuitCube.unity` and press Play.
3. Debug keys: F1 debug menu, F2 toggle dark/light theme.
