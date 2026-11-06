using System.Collections.Generic;

namespace CircuitCube.Core
{
    /// <summary>
    /// The tiles on the outer surface of a shape built from unit blocks (voxels), and how they connect, including
    /// over convex edges and into concave corners. A cube is the simplest case; pyramids, balls and mixes use the same
    /// rules.
    ///
    /// Coordinates are doubled so every block centre C has all-even or all-odd components and a block spans C +/- 1.
    /// A surface tile of block C with outward normal n has its centre at P = C + n. Direction k of a tile (bit k of its
    /// wire mask) is Dirs[k]: 0 = +U, 1 = +V, 2 = -U, 3 = -V, where U, V span the tile and N = U x V.
    /// </summary>
    public sealed class ShapeTopology : BoardTopology
    {
        sealed class Sq
        {
            public Vec3i P, N, U, V;
            public Vec3i[] Dirs;
        }

        public static readonly Vec3i[] FaceNormals =
        {
            new Vec3i(1, 0, 0), new Vec3i(-1, 0, 0),
            new Vec3i(0, 1, 0), new Vec3i(0, -1, 0),
            new Vec3i(0, 0, 1), new Vec3i(0, 0, -1),
        };


        public static Vec3i UFor(Vec3i n) => n.X != 0 ? new Vec3i(0, 1, 0) : new Vec3i(1, 0, 0);

        /// <param name="blocks">Block centres in doubled units, already centred around the origin.</param>
        public ShapeTopology(IEnumerable<Vec3i> blocks)
        {
            var solid = new HashSet<int>();
            var list = new List<Vec3i>();
            foreach (var b in blocks) if (solid.Add(b.Key)) list.Add(b);
            // A stable order keeps level generation deterministic.
            list.Sort((a, b) => a.Key.CompareTo(b.Key));

            float r2 = 0f;
            var map = new Dictionary<long, int>();
            var sq = new List<Sq>();
            foreach (var c in list)
            {
                for (int f = 0; f < 6; f++)
                {
                    Vec3i n = FaceNormals[f];
                    if (solid.Contains((c + n * 2).Key)) continue;
                    Vec3i u = UFor(n), v = n.Cross(u);
                    Vec3i p = c + n;
                    map[TileKey(p, n)] = sq.Count;
                    sq.Add(new Sq { P = p, N = n, U = u, V = v, Dirs = new[] { u, v, -u, -v } });
                    V3 P = V3.From(p), U = V3.From(u), W = V3.From(v);
                    Cells.Add(new Cell
                    {
                        Ports = 4, Center = P, Normal = V3.From(n), Axis = U, Face = f, Shape = CellShape.Square,
                        Corners = new[] { P + U - W, P + U + W, P - U + W, P - U - W },
                    });
                }
                float d = (System.Math.Abs(c.X) + 1) * (System.Math.Abs(c.X) + 1)
                        + (System.Math.Abs(c.Y) + 1) * (System.Math.Abs(c.Y) + 1)
                        + (System.Math.Abs(c.Z) + 1) * (System.Math.Abs(c.Z) + 1);
                if (d > r2) r2 = d;
            }
            Radius = (float)System.Math.Sqrt(r2);

            Links = new Link[sq.Count][];
            for (int i = 0; i < sq.Count; i++)
            {
                Sq cell = sq[i];
                Vec3i c = cell.P - cell.N;   // the block this tile belongs to
                Links[i] = new Link[4];
                for (int k = 0; k < 4; k++)
                {
                    Vec3i d = cell.Dirs[k];
                    Vec3i np, nn, back;
                    if (solid.Contains((c + d * 2 + cell.N * 2).Key))
                    {
                        // Concave corner: the wire climbs onto the side of the block in front of us.
                        np = cell.P + d + cell.N; nn = -d; back = -cell.N;
                    }
                    else if (solid.Contains((c + d * 2).Key))
                    {
                        // Flat: the next block's tile on the same plane.
                        np = cell.P + d * 2; nn = cell.N; back = -d;
                    }
                    else
                    {
                        // Convex edge: wraps onto our own block's side.
                        np = cell.P + d - cell.N; nn = d; back = cell.N;
                    }
                    int j = map[TileKey(np, nn)];
                    int bk = System.Array.IndexOf(sq[j].Dirs, back);
                    Links[i][k] = new Link { Cell = j, Back = bk };
                }
            }
        }

        static long TileKey(Vec3i p, Vec3i n)
        {
            int f = n.X > 0 ? 0 : n.X < 0 ? 1 : n.Y > 0 ? 2 : n.Y < 0 ? 3 : n.Z > 0 ? 4 : 5;
            return (long)p.Key * 8 + f;
        }
    }
}
