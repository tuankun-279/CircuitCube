---
name: "circuit-cube-project"
description: "Use when working on the Circuit Cube Unity game (repo tuankun-279/CircuitCube): where things are, menus to run, data format, rendering and the level tools."
---

# Circuit Cube project map

3D circuit puzzle: turn tiles on a shape until every lamp gets exactly its colour (sources mix: R+B=purple, R+Y=orange, B+Y=green).

- Unity 6000.0.76f1, URP, portrait Android first.
- Project on the PC at `D:/Projects/Own_Project/Puzzle_Robot`, repo `github.com/tuankun-279/CircuitCube`, branch main.
- Follow unity-game-dev-rules and english-only-code.

## Code (Assets/CircuitCube/Scripts)

- **Core:** plain C#. Topologies with 3, 4 or 5 sided cells (BoardTopology, ShapeTopology for voxel shapes, PolyTopology for polyhedra), CubeGenerator, LevelPlan (groups and level sizes), CubeLevel (an allocation-free flood fill `Components(useSol, comp, sets, stack)`), Circuit.RotN and TapsToSolve.
- **Game:**
  - CircuitCubeApp: flow Splash → Home → Shapes → Levels → Play → Result / Unlock, plus undo, hints, pause/settings and theme.
  - CubeBoardView: instanced renderer and ray picking.
  - BoardLook, GameTheme (+ Theme.cs: UiRole, BoardSkin image names, AtlasRect), CubeSave (save v3), GameData / RemoteContent (data v3, CDN window X±10), PerfProbe.
- **UI:** screen components only hold references (HomeScreen, HudScreen, Card, SettingsCard, SplashScreen...). ThemeRole, ThemeBackdrop.
- **Prefabs:**
  - `Prefabs/UI/UI.prefab` holds every screen and card.
  - `Prefabs/Board/Tile`, `TriTile` are design sources only; the game never instantiates them.

## Editor menus (Tools/Circuit Cube)

- `Art V2/Apply Art V2`: full rebuild (import rules, themes, prefab styling, UI V2, scene wiring, app icon, board look, atlases).
- `Art V2/Build UI V2`: rebuilds the V2 parts of UI.prefab (splash, cards, HUD bar...).
- `Board/Build Board Look`: run after editing the Tile prefabs or lamp/source/hint/burst clips.
- `Build Atlases`: run after changing any image in Art/Board or Art/UI.
- `Data/...`: export sample data, import/export a data zip, validate.
- F1 opens the debug menu (play any level); F2 toggles dark/light.

## Data (v3)

- Files under `Resources/CircuitCubeData`:
  - manifest.json
  - groups/<id>.json
  - orders/<id>/<k>.json (chunks of 100)
  - levels/<id>/<serial>.json
  - shapes.json and shapes/voxel
- **tiles:** 3 characters per tile:
  - wire mask in base32
  - kind and colour: `.` wire, `r`/`b`/`y` source, `1-7` lamp colour
  - starting rotation
- check = FNV-1a hash.
- **Save v3:** save/profile.json, save/groups/<id>.json, stars chunks.
- **Score:** 6 points at par, -1 per hint, minimum 3; Expert = 6.
- **Level Lab** (web artifact `claude.ai/artifact/WUkC8DCM543afEtakLwwx3`, source `Tools/LevelLab/index.html`) is the only level editor: create, read, update and delete levels, batch-generate, draw fully or partly (shape only, then random fill), import/export zip.

## Look

- Dark and light themes: `Themes/Theme_Dark.asset`, `Theme_Light.asset`.
- Neon only on the board; UI is flat.
- Wire colours:
  - dark: #FF4D6D #3DA9FF #FFE14D #C77DFF #3DFFA2
  - light: #F0435F #2F8FE8 #EFA300 #A15BEB #13B07A
- Teal primary #46B8C6 (dark) / #1E9AAA (light); brand teal #2AA8B8.
- Backdrops: planet-horizon night sky, and pale dawn.
- Font Inter (TMP SDF in Art/Fonts).

## Checks before saying done

1. Run all 71+ edit-mode tests: links point back, every level is solvable, data round trip, board ≤12 draw calls, no GC when drawing or after 100 turns.
2. Screenshot Home, Shapes, Levels, HUD, Pause, Result and Unlock in both themes.
3. Measure with PerfProbe on the biggest shapes (cube 150, tetra 144, sphere 174 tiles).