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
| `Assets/CircuitCube/Art/V2` | Current art set (dark and light board skins, UI, icons) |
| `Assets/CircuitCube/Themes` | `Theme_Dark` / `Theme_Light` assets |
| `Assets/CircuitCube/Tests` | Edit-mode tests |
| `Tools/LevelLab/index.html` | Web level editor (CRUD for level data, batch generation, import/export zip) |
| `ArtRef` | Art references, mockup and the sprite cutting scripts (`tools/cut_v2.py`) |

See `Assets/CircuitCube/README.md` for the data format and editor menus.

## Getting started

1. Open the folder with Unity Hub (Unity 6000.0.76f1).
2. Open `Assets/CircuitCube/Scenes/CircuitCube.unity` and press Play.
3. Debug keys: F1 debug menu, F2 toggle dark/light theme.
