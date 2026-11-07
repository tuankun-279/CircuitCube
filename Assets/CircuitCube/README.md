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

Code only fills in text, places the wire arms of a tile, turns them, tints layers with energy colours and plays the
clips. The tile turn is the one animation in code, because it depends on the move.

## Shape groups

Six groups of 100 levels. Beating level 100 of a group opens the next one; Megamix keeps going after 100.
Players see space names (ids stay the shape names): Moonbase (cube), Pyramid (tetra), Star Gem (octa), Planet
(sphere), Galaxy (dodeca), Nebula (megamix). Names and blurbs are edited in Level Lab.

| Group | Shapes |
|---|---|
| Cube | 2x2 to 5x5 cubes (the first release's levels) |
| Tetra | regular tetrahedron, every face cut into n x n triangle tiles (36 to 144 tiles); one tap turns a tile a third of a turn |
| Octa | regular octahedron, n x n triangles per face (32, 72, 128 tiles) |
| Sphere | soccer ball (32 tiles), then Goldberg balls of 42, 92 and 162 tiles: 12 pentagons, the rest hexagons |
| Dodeca | dodecahedron cut like a Kilominx (60 kites), from level 41 and on big levels like a Megaminx (132 tiles) |
| Megamix | the place for unusual shapes: every third level a station (3D plus sign, tower or L shape, bigger as levels go on), the others a new mix of small cubes, pyramids, balls, bars and slabs |

In each group the first 10 levels are small and lightly scrambled, then boards grow and colours go up to four;
every 10th level from 30 is a big board. The first level of a new group shows an introduction card.

Tiles can have 3 to 6 sides (`BoardTopology`); each tile keeps its corners and its outline kind (`CellShape`: square,
triangle, pentagon, hexagon, kite, Megaminx corner and edge). Block shapes (`ShapeLibrary` + `ShapeTopology`) link
surface tiles over convex edges and into concave corners; polygon shapes (`PolyTopology`, `PolyShapes`) link tiles
that share a side. Shape ids in the level data: `tetra-n`, `octa-n`, `dodeca-2|3`, `geo-1..4` (see `PolyShapes`).
Wire masks are written in base64 (`TileCodec`), so hexagon tiles fit; older base32 data reads the same. Progress is saved per group (`CubeSave`); the Cube group reads the old keys.

## Editor menu

Tools > Circuit Cube: Open Game Scene, Reset Saved Progress, Unlock All Shapes (testing).

## Rendering

The project's default renderer is the URP Universal Renderer (`Assets/Settings/UniversalRenderer.asset`) so shapes
with hollows draw with a depth buffer.

The board has no GameObject per tile. `CubeBoardView` draws:
- the tile faces of the whole board as one mesh (`Shaders/BoardFace.shader`, shaded by the face normal); each tile is a
  fan of triangles mapped onto the face image of its outline, corner to corner, so uneven tiles still fit their image;
- the wires as arms: a strip from the tile centre to the middle of each side the wire leaves by, and a hub under them.
  A turn slides every arm to the next side (angle and length eased), so kites and Goldberg hexagons turn cleanly;
- lamps, sources, hint pins and bursts as instanced quads (`Graphics.RenderMeshInstanced`, `Shaders/BoardSprite.shader`).
Every board image of a theme is packed into one atlas (`Art/Atlases/Board_Dark.png`, `Board_Light.png`), so each
layer is one draw call: at most 12 for the biggest shapes, and nothing allocated per frame. Taps are picked with a ray
against the tile polygons, without colliders.

The pentagon, hexagon, kite and Megaminx faces and the arm and hub images are made from the ChatGPT square tile and
wire by `ArtRef/tools/board_shapes.py` (the square's bevel measured from each side, redrawn on the new outlines). The
old whole-tile wire images are in `ArtRef/legacy_v2`.

Art layout (also in `Editor/ArtPaths.cs`): `Art/Board/{Dark,Light,Shared}` tile images, `Art/UI/{Common,Icons,
Shapes/{Dark,Light},Brand,Mascot,Backgrounds}` UI sprites, `Art/Fonts` Inter and its TextMeshPro font assets,
`Art/Atlases` generated atlases, `Art/Materials/Board` preview materials for the Tile prefabs (not used in game).
Import settings come from `Editor/UIAssetImporter.cs`. After changing a board or UI image run Tools > Circuit Cube >
Build Atlases (UI sprite atlas `Art/Atlases/UI.spriteatlasv2` and the two board atlases; the themes keep the image
rectangles by file name). All UI text is TextMeshPro with Inter SDF (dynamic, ASCII pre-filled).

Tiles are still designed on `Prefabs/Board/Tile.prefab` and `TriTile.prefab` (layers, sizes, animation clips). After
changing them run Tools > Circuit Cube > Board > Build Board Look, which writes `Themes/BoardLook.asset` (offsets,
sizes and the clips as curves) and links it to the scene. Textures and colours come from the theme skins.

`PerfProbe.Measure(frames)` (development) averages draw calls, batches, SetPass calls and GC bytes per frame.

## Tests

Window > General > Test Runner > EditMode > CircuitCube.Tests: links point back on every shape, all 500 group levels
are solvable and start scrambled, same level gives the same board, colour mixing, rotation. `BoardPerfTests`: the
biggest cube, tetra, sphere and megamix boards stay within 30 draw calls, and drawing and 100 tile turns allocate
no garbage.

## Not done yet

Sound, haptics, a settings screen, accounts and cloud save, daily puzzle, shop and ads.
