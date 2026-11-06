# Circuit Cube

Turn the wire tiles on the surface of a 3D shape so every lamp gets exactly its colour.
Red, blue and yellow power mix when they share a wire (red + blue = purple, red + yellow = orange, blue + yellow = green).

## Rule: no UI in code

Everything the player sees is an asset you edit in the Unity editor:

| What | Where |
|---|---|
| Screens (Home, Shapes, Levels, HUD) and the pop-up cards | `Prefabs/UI/UI.prefab` |
| One level cell, one shape-group card, one colour chip | `Prefabs/UI/LevelNode.prefab`, `ShapeCard.prefab`, `LegendChip.prefab` |
| Background behind the shape | `Prefabs/UI/Background.prefab` |
| A board tile: base, 5 wire shapes, lamp, source, hint pin, burst | `Prefabs/Board/Tile.prefab` (square), `TriTile.prefab` (triangle) |
| Debug menu (F1, editor and development builds only) | `Prefabs/UI/DebugMenu.prefab` |
| Animations (lamp on / lit / off, source idle, hint pop, burst, screen and card in) | `Animations/*.anim` (legacy Animation components on the prefabs) |
| Materials for the tile layers | `Art/Materials` |
| Art made with ChatGPT | `Art/UI` (sprites), `Art/Board` (tile textures) |
| Scene with camera, event system, background, UI and the game object | `Scenes/CircuitCube.unity` |

Code only fills in text, picks which wire image a tile shows, turns it, tints layers with energy colours and plays the
clips. The tile turn is the one animation in code, because its angle depends on the move.

## Shape groups

Five groups of 100 levels. Beating level 100 of a group opens the next one; Megamix keeps going after 100.

| Group | Shapes |
|---|---|
| Cube | 2x2 to 5x5 cubes (the first release's levels) |
| Tetra | regular tetrahedron, every face cut into n x n triangle tiles (36 to 144 tiles); one tap turns a tile a third of a turn |
| Sphere | balls of blocks, radius 1.5 to 3 |
| Cross | 3D plus signs, towers, L shapes |
| Megamix | a new mix of small cubes, pyramids, balls, bars and slabs every level |

In each group the first 10 levels are small and lightly scrambled, then boards grow and colours go up to four;
every 10th level from 30 is a big board. The first level of a new group shows an introduction card.

Tiles can have 3, 4 or 5 sides (`BoardTopology`). Block shapes (`ShapeLibrary` + `ShapeTopology`) link surface tiles
over convex edges and into concave corners; polygon shapes (`PolyTopology`, `PolyShapes.Tetrahedron`) link tiles that
share a side. Progress is saved per group (`CubeSave`); the Cube group reads the old keys.

## Editor menu

Tools > Circuit Cube: Open Game Scene, Reset Saved Progress, Unlock All Shapes (testing).

## Rendering

The project's default renderer is the URP Universal Renderer (`Assets/Settings/UniversalRenderer.asset`) so shapes
with hollows draw with a depth buffer. Tile bases are opaque; the other layers are transparent with a render-queue
order set in their materials.

## Tests

Window > General > Test Runner > EditMode > CircuitCube.Tests: links point back on every shape, all 500 group levels
are solvable and start scrambled, same level gives the same board, colour mixing, rotation.

## Not done yet

Sound, haptics, a settings screen, accounts and cloud save, daily puzzle, shop and ads.
