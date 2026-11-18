using System;
using System.Collections.Generic;

namespace CircuitCube.Core
{
    /// <summary>
    /// The six hand-made levels of the first-time tutorial, all on the 3x3x3 cube. Each level is written as the tiles
    /// the player sees on the front (f), right (r), top (t) and left (l) faces when the cube is turned the tutorial way:
    /// row 0 is the top row, column 0 the left column, and wire arms are screen directions N, E, S, W.
    /// Tiles not listed are blank (no wire). A tile's start arms say how it is turned when the level begins.
    /// </summary>
    public static class TutorialLevels
    {
        public const int Count = 6;
        public const string ShapeId = "cube-3";

        /// <summary>One tile: face, cell (row * 3 + column), kind ('S' source, 'L' lamp, 'W' wire), colour set, solved and start arms.</summary>
        public struct Spec
        {
            public char Face;
            public int Cell;
            public char Kind;
            public int Color;
            public string Arms, Start;
            public Spec(char face, int cell, char kind, int color, string arms, string start = null)
            {
                Face = face; Cell = cell; Kind = kind; Color = color; Arms = arms; Start = start ?? arms;
            }
        }

        /// <summary>Start arms that mean "one turn from solved", whichever way that leaves the wire.</summary>
        public const string OneTurn = "+1";

        const int R = Circuit.Red, B = Circuit.Blue, Y = Circuit.Yellow, P = Circuit.Red | Circuit.Blue;

        static readonly Spec[][] Levels =
        {
            // 1 · turn one tile to join the source to the lamp
            new[] { new Spec('f', 3, 'S', B, "E"), new Spec('f', 4, 'W', 0, "EW", "NS"), new Spec('f', 5, 'L', B, "W") },
            // 2 · one lamp is lit already; the other is round the side
            new[]
            {
                new Spec('f', 3, 'L', B, "E"), new Spec('f', 4, 'S', B, "EW"), new Spec('f', 5, 'W', 0, "EW", "NS"),
                new Spec('r', 3, 'W', 0, "EW"), new Spec('r', 4, 'W', 0, "EW", "NS"), new Spec('r', 5, 'L', B, "W"),
            },
            // 3 · Undo and the hint (the corner is the hint's tile)
            new[] { new Spec('f', 3, 'S', Y, "E"), new Spec('f', 4, 'W', 0, "EW"), new Spec('f', 5, 'W', 0, "WS", "NE"), new Spec('f', 8, 'L', Y, "N") },
            // 4 · par: two tiles, one turn each
            new[] { new Spec('f', 0, 'S', B, "S"), new Spec('f', 3, 'W', 0, "NS", "EW"), new Spec('f', 6, 'W', 0, "NE", OneTurn), new Spec('f', 7, 'L', B, "W") },
            // 5 · two colours
            new[]
            {
                new Spec('f', 0, 'S', R, "E"), new Spec('f', 1, 'W', 0, "EW"), new Spec('f', 2, 'L', R, "W"),
                new Spec('f', 6, 'S', B, "E"), new Spec('f', 7, 'W', 0, "EW", "NS"), new Spec('f', 8, 'L', B, "W"),
            },
            // 6 · red + blue on one wire make purple
            new[]
            {
                new Spec('f', 0, 'S', R, "S"), new Spec('f', 6, 'S', B, "N"), new Spec('f', 3, 'W', 0, "NES", "NSW"),
                new Spec('f', 4, 'W', 0, "EW"), new Spec('f', 5, 'L', P, "W"),
            },
        };

        /// <summary>The tiles of a level as written (1-based level).</summary>
        public static IReadOnlyList<Spec> SpecOf(int level) => Levels[level - 1];

        static BoardTopology _topo;

        public static BoardTopology Topology => _topo ?? (_topo = ShapeResolver.Build(ShapeId, null));

        /// <summary>Face basis with the camera looking along +z: outward normal, screen right, screen up.</summary>
        static void Basis(char face, out V3 n, out V3 right, out V3 up)
        {
            switch (face)
            {
                case 'r': n = new V3(1, 0, 0); right = new V3(0, 0, 1); up = new V3(0, 1, 0); break;
                case 'l': n = new V3(-1, 0, 0); right = new V3(0, 0, -1); up = new V3(0, 1, 0); break;
                case 't': n = new V3(0, 1, 0); right = new V3(1, 0, 0); up = new V3(0, 0, 1); break;
                default: n = new V3(0, 0, -1); right = new V3(1, 0, 0); up = new V3(0, 1, 0); break;
            }
        }

        static V3 Dir(char arm, V3 right, V3 up) => arm == 'N' ? up : arm == 'S' ? -up : arm == 'E' ? right : -right;

        /// <summary>Index of the tile at <paramref name="cell"/> of <paramref name="face"/>; -1 if the cube has none there.</summary>
        public static int Find(BoardTopology topo, char face, int cell)
        {
            Basis(face, out var n, out var right, out var up);
            int row = cell / 3, col = cell % 3;
            V3 p = right * (-2 + 2 * col) + up * (2 - 2 * row) + n * 3;
            for (int i = 0; i < topo.Count; i++)
            {
                var c = topo.Cells[i];
                if ((c.Center - p).Length < 0.1f && (c.Normal - n).Length < 0.1f) return i;
            }
            return -1;
        }

        /// <summary>Wire mask of the tile for screen arms ("NE" ...), in the tile's own port order.</summary>
        static int Mask(BoardTopology topo, int tile, char face, string arms)
        {
            Basis(face, out _, out var right, out var up);
            var cell = topo.Cells[tile];
            V3 u = cell.Axis, v = cell.Normal.Cross(u);
            V3[] ports = { u, v, -u, -v };
            int m = 0;
            foreach (char a in arms)
            {
                V3 d = Dir(a, right, up);
                for (int k = 0; k < 4; k++) if ((ports[k] - d).Length < 0.1f) m |= 1 << k;
            }
            return m;
        }

        /// <summary>The tutorial level (1-based), with each tile turned as its start arms say.</summary>
        public static CubeLevel Build(int level)
        {
            if (level < 1 || level > Count) throw new ArgumentOutOfRangeException(nameof(level));
            var topo = Topology;
            var tiles = new CubeTile[topo.Count];
            for (int i = 0; i < tiles.Length; i++) tiles[i] = new CubeTile { Kind = TileKind.Wire, Ports = topo.Cells[i].Ports };
            var colors = new List<int>();
            foreach (var s in Levels[level - 1])
            {
                int i = Find(topo, s.Face, s.Cell);
                var t = tiles[i];
                t.Kind = s.Kind == 'S' ? TileKind.Source : s.Kind == 'L' ? TileKind.Lamp : TileKind.Wire;
                t.Color = s.Color;
                t.Sol = Mask(topo, i, s.Face, s.Arms);
                if (s.Kind == 'L' && !colors.Contains(s.Color)) colors.Add(s.Color);
                t.Rot = s.Start == OneTurn ? t.Ports - 1 : StartRot(t, Mask(topo, i, s.Face, s.Start));
            }
            return new CubeLevel { Group = 0, Number = level, Topo = topo, Tiles = tiles, Colors = colors.ToArray(), Par = TileCodec.Par(tiles) };
        }

        /// <summary>Turns from the solved mask to <paramref name="start"/> (0 if no turn gives it).</summary>
        static int StartRot(CubeTile t, int start)
        {
            for (int r = 0; r < t.Ports; r++) if (Circuit.RotN(t.Sol, r, t.Ports) == start) return r;
            return 0;
        }
    }
}
