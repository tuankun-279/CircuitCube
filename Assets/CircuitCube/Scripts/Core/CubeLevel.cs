namespace CircuitCube.Core
{
    public enum TileKind { Wire, Source, Lamp }

    public sealed class CubeTile
    {
        public int Sol;        // wire mask in the solved rotation (bit k = port k)
        public int Ports = 4;  // sides of the tile: 4 square, 3 triangle, 5 pentagon
        public TileKind Kind;
        public int Color;      // source: one of 1, 2, 4. lamp: the colour set it needs (1..7)
        public int Rot;        // steps of 360/Ports applied from Sol (port k towards port k+1)
        public bool Locked;    // set by a hint; cannot be rotated any more

        public int Current => Circuit.RotN(Sol, Rot, Ports);
        public CubeTile Clone() => (CubeTile)MemberwiseClone();
    }

    public static class Circuit
    {
        // Energy colours are bit sets: 1 red, 2 blue, 4 yellow. 3 purple, 5 orange, 6 green, 7 white.
        public const int Red = 1, Blue = 2, Yellow = 4;

        /// <summary>One step of turning for a tile with `ports` sides: the wire on port k moves to port k+1.</summary>
        public static int RotCW(int m, int ports = 4) => ((m << 1) & ((1 << ports) - 1)) | (m >> (ports - 1));

        public static int RotN(int m, int t, int ports = 4)
        {
            t = ((t % ports) + ports) % ports;
            while (t-- > 0) m = RotCW(m, ports);
            return m;
        }

        public static int Bits(int m) { int c = 0; while (m != 0) { c += m & 1; m >>= 1; } return c; }
        public static int Pop3(int v) => (v & 1) + ((v >> 1) & 1) + ((v >> 2) & 1);

        /// <summary>Taps still needed to bring the tile back to a solved orientation.</summary>
        public static int TapsToSolve(CubeTile t)
        {
            for (int k = 0; k < t.Ports; k++)
                if (RotN(t.Sol, t.Rot + k, t.Ports) == t.Sol) return k;
            return 0;
        }
    }

    public sealed class CubeLevel
    {
        public int Group;
        public int Number;
        public BoardTopology Topo;
        public CubeTile[] Tiles;
        public int[] Colors;   // lamp colour sets used in this level
        public int Par;        // quarter turns needed from the scrambled start

        /// <summary>
        /// Flood fill over connected wires. comp[i] = group id of tile i; sets[g] = OR of the source colours in group g.
        /// A link counts only when both tiles point at each other.
        /// </summary>
        public void Components(bool useSol, int[] comp, out int[] sets)
        {
            int n = Tiles.Length;
            for (int i = 0; i < n; i++) comp[i] = -1;
            var list = new System.Collections.Generic.List<int>();
            var stack = new System.Collections.Generic.Stack<int>();
            for (int c = 0; c < n; c++)
            {
                if (comp[c] >= 0) continue;
                int id = list.Count, set = 0;
                comp[c] = id;
                stack.Push(c);
                while (stack.Count > 0)
                {
                    int x = stack.Pop();
                    CubeTile tx = Tiles[x];
                    if (tx.Kind == TileKind.Source) set |= tx.Color;
                    int mx = useSol ? tx.Sol : tx.Current;
                    for (int k = 0; k < tx.Ports; k++)
                    {
                        if (((mx >> k) & 1) == 0) continue;
                        var link = Topo.Links[x][k];
                        int j = link.Cell;
                        if (comp[j] >= 0) continue;
                        int mj = useSol ? Tiles[j].Sol : Tiles[j].Current;
                        if (((mj >> link.Back) & 1) == 0) continue;
                        comp[j] = id;
                        stack.Push(j);
                    }
                }
                list.Add(set);
            }
            sets = list.ToArray();
        }

        public bool IsSolved(bool useSol)
        {
            var comp = new int[Tiles.Length];
            Components(useSol, comp, out int[] sets);
            for (int i = 0; i < Tiles.Length; i++)
                if (Tiles[i].Kind == TileKind.Lamp && sets[comp[i]] != Tiles[i].Color) return false;
            return true;
        }

        public CubeLevel Clone()
        {
            var c = (CubeLevel)MemberwiseClone();
            c.Tiles = new CubeTile[Tiles.Length];
            for (int i = 0; i < Tiles.Length; i++) c.Tiles[i] = Tiles[i].Clone();
            return c;
        }
    }
}
