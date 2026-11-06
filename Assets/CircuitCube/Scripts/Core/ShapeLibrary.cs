using System.Collections.Generic;

namespace CircuitCube.Core
{
    /// <summary>
    /// Block shapes for each level group. Every shape returns block centres in doubled units, centred on the origin,
    /// ready for <see cref="ShapeTopology"/>.
    /// </summary>
    public static class ShapeLibrary
    {
        static Vec3i B(int x, int y, int z) => new Vec3i(2 * x, 2 * y, 2 * z);

        public static List<Vec3i> Cube(int n) => Center(RawCube(n));

        static List<Vec3i> RawCube(int n)
        {
            var o = new List<Vec3i>();
            for (int x = 0; x < n; x++) for (int y = 0; y < n; y++) for (int z = 0; z < n; z++) o.Add(B(x, y, z));
            return o;
        }

        /// <summary>Stepped pyramid: square layers that shrink by one block on every side.</summary>
        public static List<Vec3i> Pyramid(int baseSize) => Center(RawPyramid(baseSize));

        static List<Vec3i> RawPyramid(int baseSize)
        {
            var o = new List<Vec3i>();
            for (int h = 0, s = baseSize; s >= 1; h++, s -= 2)
                for (int x = 0; x < s; x++) for (int z = 0; z < s; z++) o.Add(B(x + h, h, z + h));
            return o;
        }

        /// <summary>Blocks whose centres are within r of the middle: a rounded ball.</summary>
        public static List<Vec3i> Ball(float r) => Center(RawBall(r));

        static List<Vec3i> RawBall(float r)
        {
            var o = new List<Vec3i>();
            int R = (int)r + 1;
            for (int x = -R; x <= R; x++) for (int y = -R; y <= R; y++) for (int z = -R; z <= R; z++)
                if (x * x + y * y + z * z <= r * r) o.Add(B(x, y, z));
            return o;
        }

        /// <summary>Three bars through the middle along X, Y and Z (a 3D plus sign).</summary>
        public static List<Vec3i> Cross(int arm, int thick)
        {
            var set = new HashSet<int>();
            var o = new List<Vec3i>();
            void Add(int x, int y, int z) { var v = B(x, y, z); if (set.Add(v.Key)) o.Add(v); }
            for (int a = -arm; a < arm + thick; a++)
                for (int b = 0; b < thick; b++)
                    for (int c = 0; c < thick; c++) { Add(a, b, c); Add(b, a, c); Add(b, c, a); }
            return Center(o);
        }

        /// <summary>A square tower with a wider base: a stepped column.</summary>
        public static List<Vec3i> Tower(int width, int height)
        {
            var o = new List<Vec3i>();
            for (int x = 0; x < width + 2; x++) for (int z = 0; z < width + 2; z++) o.Add(B(x, 0, z));
            for (int y = 1; y < height; y++)
                for (int x = 1; x <= width; x++) for (int z = 1; z <= width; z++) o.Add(B(x, y, z));
            return Center(o);
        }

        /// <summary>An L of two thick arms.</summary>
        public static List<Vec3i> Ell(int len, int thick)
        {
            var set = new HashSet<int>();
            var o = new List<Vec3i>();
            for (int a = 0; a < len; a++) for (int b = 0; b < thick; b++) for (int c = 0; c < thick; c++)
            {
                var p = B(a, b, c); if (set.Add(p.Key)) o.Add(p);
                var q = B(b, a, c); if (set.Add(q.Key)) o.Add(q);
            }
            return Center(o);
        }

        /// <summary>
        /// A new mix every level: two to four small primitives (cubes, pyramids, balls, bars, slabs) stuck together
        /// face to face. Deterministic for a seed. Blocks that would only touch along an edge get a filler block so the
        /// surface stays a clean single skin.
        /// </summary>
        public static List<Vec3i> Megamix(uint seed, int minTiles, int maxTiles)
        {
            var R = new CubeRng(seed);
            for (int attempt = 0; attempt < 40; attempt++)
            {
                var set = new HashSet<int>();
                var blocks = new List<Vec3i>();
                void AddAll(List<Vec3i> part, Vec3i off)
                {
                    foreach (var p in part) { var q = p + off; if (set.Add(q.Key)) blocks.Add(q); }
                }
                AddAll(Primitive(R), new Vec3i(0, 0, 0));
                int parts = 2 + R.Range(3);
                for (int i = 1; i < parts; i++)
                {
                    var part = Primitive(R);
                    // Put the new part next to a random block, pushed out along a random axis until it no longer
                    // overlaps more than a little.
                    var anchor = blocks[R.Range(blocks.Count)];
                    var dir = ShapeTopology.FaceNormals[R.Range(6)];
                    var off = anchor + dir * 2 - part[R.Range(part.Count)];
                    for (int push = 0; push < 6 && Overlap(set, part, off) > 2; push++) off = off + dir * 2;
                    AddAll(part, off);
                }
                FillEdgeContacts(set, blocks);
                if (!Connected(set, blocks)) continue;
                var centred = Center(blocks);
                int tiles = new ShapeTopology(centred).Count;
                if (tiles >= minTiles && tiles <= maxTiles) return centred;
            }
            return Cube(4);
        }

        static List<Vec3i> Primitive(CubeRng R)
        {
            // Raw (uncentred, all-even) coordinates, so parts snap onto the same block grid.
            switch (R.Range(6))
            {
                case 0: return RawCube(2);
                case 1: return RawCube(3);
                case 2: return RawPyramid(3);
                case 3: return RawBall(1.5f);
                case 4: { var o = new List<Vec3i>(); int len = 3 + R.Range(3); for (int i = 0; i < len; i++) o.Add(B(i, 0, 0)); return o; }
                default: { var o = new List<Vec3i>(); for (int x = 0; x < 3; x++) for (int z = 0; z < 3; z++) o.Add(B(x, 0, z)); return o; }
            }
        }

        static int Overlap(HashSet<int> set, List<Vec3i> part, Vec3i off)
        {
            int n = 0;
            foreach (var p in part) if (set.Contains((p + off).Key)) n++;
            return n;
        }

        /// <summary>Two blocks meeting only along an edge make a pinched surface. Fill one of the two gap blocks.</summary>
        static void FillEdgeContacts(HashSet<int> set, List<Vec3i> blocks)
        {
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int i = 0; i < blocks.Count; i++)
                {
                    var c = blocks[i];
                    for (int a = 0; a < 6; a++)
                    for (int b = 0; b < 6; b++)
                    {
                        var da = ShapeTopology.FaceNormals[a];
                        var db = ShapeTopology.FaceNormals[b];
                        if (da.Dot(db) != 0) continue;
                        var diag = c + da * 2 + db * 2;
                        if (!set.Contains(diag.Key)) continue;
                        var g1 = c + da * 2;
                        var g2 = c + db * 2;
                        if (set.Contains(g1.Key) || set.Contains(g2.Key)) continue;
                        set.Add(g1.Key); blocks.Add(g1); changed = true;
                    }
                }
            }
        }

        static bool Connected(HashSet<int> set, List<Vec3i> blocks)
        {
            if (blocks.Count == 0) return false;
            var seen = new HashSet<int> { blocks[0].Key };
            var stack = new Stack<Vec3i>();
            stack.Push(blocks[0]);
            while (stack.Count > 0)
            {
                var c = stack.Pop();
                foreach (var n in ShapeTopology.FaceNormals)
                {
                    var q = c + n * 2;
                    if (set.Contains(q.Key) && seen.Add(q.Key)) stack.Push(q);
                }
            }
            return seen.Count == blocks.Count;
        }

        /// <summary>Moves blocks so the bounding box is centred on the origin (shifts stay even, so parity is kept).</summary>
        public static List<Vec3i> Center(List<Vec3i> blocks)
        {
            int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue, maxZ = int.MinValue;
            foreach (var b in blocks)
            {
                if (b.X < minX) minX = b.X; if (b.X > maxX) maxX = b.X;
                if (b.Y < minY) minY = b.Y; if (b.Y > maxY) maxY = b.Y;
                if (b.Z < minZ) minZ = b.Z; if (b.Z > maxZ) maxZ = b.Z;
            }
            var off = new Vec3i(-Half(minX + maxX), -Half(minY + maxY), -Half(minZ + maxZ));
            var o = new List<Vec3i>(blocks.Count);
            foreach (var b in blocks) o.Add(b + off);
            return o;
        }

        // Block coordinates are all even, so min + max is even; the shift keeps every coordinate on the same parity
        // grid. When the extent is an odd number of blocks the result is even, otherwise odd (like the old cube).
        static int Half(int v) => v >= 0 ? v / 2 : -((-v) / 2);
    }
}
