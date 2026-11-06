using System;
using System.Collections.Generic;

namespace CircuitCube.Core
{
    /// <summary>
    /// Turns a shape id from level data into a board. Preset ids carry their own parameters
    /// (cube-3, tetra-4, ball-25, cross-21, tower-24, ell-42, pyramid-5, bar-4, slab-33);
    /// "voxel-…" ids are free-form shapes stored as shapes/voxel/&lt;id&gt;.json.
    /// </summary>
    public static class ShapeResolver
    {
        /// <param name="readVoxel">Returns the JSON of shapes/voxel/&lt;id&gt;.json, or null.</param>
        public static BoardTopology Build(string id, Func<string, VoxelShapeFile> readVoxel)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (id.StartsWith("voxel-", StringComparison.Ordinal))
            {
                var v = readVoxel?.Invoke(id);
                if (v == null || v.blocks == null || v.blocks.Count < 3) return null;
                return new ShapeTopology(VoxelBlocks(v.blocks));
            }
            int dash = id.LastIndexOf('-');
            if (dash <= 0) return null;
            string type = id.Substring(0, dash), d = id.Substring(dash + 1);
            if (!int.TryParse(d, out int num)) return null;
            int a = d.Length >= 2 ? int.Parse(d.Substring(0, d.Length - 1)) : num, b = d.Length >= 2 ? d[d.Length - 1] - '0' : 0;
            switch (type)
            {
                case "cube": return new ShapeTopology(ShapeLibrary.Cube(num));
                case "tetra": return PolyShapes.Tetrahedron(num);
                case "ball": return new ShapeTopology(ShapeLibrary.Ball(num / 10f));
                case "pyramid": return new ShapeTopology(ShapeLibrary.Pyramid(num));
                case "cross": return d.Length >= 2 ? new ShapeTopology(ShapeLibrary.Cross(a, b)) : null;
                case "tower": return d.Length >= 2 ? new ShapeTopology(ShapeLibrary.Tower(a, b)) : null;
                case "ell": return d.Length >= 2 ? new ShapeTopology(ShapeLibrary.Ell(a, b)) : null;
                case "bar": { var o = new List<Vec3i>(); for (int i = 0; i < num; i++) o.Add(new Vec3i(2 * i, 0, 0)); return new ShapeTopology(ShapeLibrary.Center(o)); }
                case "slab":
                {
                    if (d.Length < 2) return null;
                    var o = new List<Vec3i>(); for (int x = 0; x < a; x++) for (int z = 0; z < b; z++) o.Add(new Vec3i(2 * x, 0, 2 * z));
                    return new ShapeTopology(ShapeLibrary.Center(o));
                }
            }
            return null;
        }

        /// <summary>Unit block positions (flattened) to centred, doubled block centres, as ShapeLibrary builds them.</summary>
        public static List<Vec3i> VoxelBlocks(List<int> flat)
        {
            var o = new List<Vec3i>(flat.Count / 3);
            for (int i = 0; i + 2 < flat.Count; i += 3) o.Add(new Vec3i(2 * flat[i], 2 * flat[i + 1], 2 * flat[i + 2]));
            return ShapeLibrary.Center(o);
        }

        /// <summary>Centred doubled blocks back to small unit positions (for writing a voxel file).</summary>
        public static List<int> FlattenBlocks(List<Vec3i> doubled)
        {
            int mx = int.MaxValue, my = int.MaxValue, mz = int.MaxValue;
            foreach (var v in doubled) { mx = Math.Min(mx, v.X); my = Math.Min(my, v.Y); mz = Math.Min(mz, v.Z); }
            var o = new List<int>(doubled.Count * 3);
            foreach (var v in doubled) { o.Add((v.X - mx) / 2); o.Add((v.Y - my) / 2); o.Add((v.Z - mz) / 2); }
            return o;
        }
    }
}
