using System.Collections.Generic;
using CircuitCube.Core;
using UnityEngine;

namespace CircuitCube.Game
{
    /// <summary>
    /// Builds level data files (format v3) from the built-in generator: the first N levels of every generator group.
    /// Used by the editor menu Tools/Circuit Cube/Data and by the tests. Returns relative path → JSON text.
    /// </summary>
    public static class DataExport
    {
        public static Dictionary<string, string> FromGenerator(int perGroup)
        {
            var files = new Dictionary<string, string>();
            var manifest = new ManifestData { schema = 3, minApp = "0.1.0", shapesVersion = 1 };
            var catalog = new ShapeCatalogFile { version = 1 };
            var seenShapes = new HashSet<string>();

            for (int g = 0; g < LevelPlan.GroupCount; g++)
            {
                var info = LevelPlan.Groups[g];
                string id = LevelPlan.GroupIds[g];
                manifest.groups.Add(new GroupRef { id = id, unlock = g == 0 ? new UnlockRule() : new UnlockRule { group = LevelPlan.GroupIds[g - 1], cleared = perGroup } });
                var gd = new GroupData { id = id, name = info.Name, blurb = info.Blurb, icon = id, endless = info.Endless, enable = true, levelCount = perGroup, chunkSize = 100 };
                var order = new List<int>();
                for (int l = 1; l <= perGroup; l++)
                {
                    var lv = LevelPlan.Build(g, l);
                    string shape = LevelPlan.ShapeIdFor(g, l);
                    if (shape.StartsWith("voxel-"))
                    {
                        var vox = new VoxelShapeFile { id = shape, blocks = ShapeResolver.FlattenBlocks(LevelPlan.ShapeFor(g, l)) };
                        files["shapes/voxel/" + shape + ".json"] = JsonUtility.ToJson(vox);
                    }
                    else if (seenShapes.Add(shape)) AddToCatalog(catalog, shape);
                    string tiles = TileCodec.Encode(lv.Tiles);
                    var d = new LevelData
                    {
                        id = id + "-" + l.ToString("D4"), serial = l, group = id, rev = 1, shape = shape, ports = lv.Tiles[0].Ports,
                        tiles = tiles, par = lv.Par, check = TileCodec.Check(tiles),
                        difficulty = TileCodec.Difficulty(lv.Par, lv.Tiles.Length, lv.Colors.Length),
                        gen = new GenInfo
                        {
                            ver = 1, seed = LevelPlan.SeedFor(g, l), colors = new List<int>(lv.Colors),
                            scramble = (float)System.Math.Round(LevelPlan.ScrambleFor(g, l, lv.Tiles.Length), 3),
                            minPar = LevelPlan.MinParFor(l, lv.Tiles.Length)
                        }
                    };
                    if (l % 10 == 0) d.tags.Add("milestone");
                    if (g == 0 && l <= 3) d.tags.Add("tutorial");
                    files[GameData.LevelPath(id, l)] = JsonUtility.ToJson(d, true);
                    order.Add(l);
                }
                for (int k = 0; k * 100 < order.Count; k++)
                {
                    var c = new OrderChunk { group = id, chunk = k, version = 1, levels = order.GetRange(k * 100, Mathf.Min(100, order.Count - k * 100)) };
                    files[GameData.ChunkPath(id, k)] = JsonUtility.ToJson(c);
                    gd.chunks.Add(new ChunkInfo { index = k, version = 1, count = c.levels.Count });
                }
                files["groups/" + id + ".json"] = JsonUtility.ToJson(gd, true);
            }
            files["manifest.json"] = JsonUtility.ToJson(manifest, true);
            files["shapes.json"] = JsonUtility.ToJson(catalog, true);
            return files;
        }

        static void AddToCatalog(ShapeCatalogFile c, string id)
        {
            int dash = id.LastIndexOf('-');
            string type = id.Substring(0, dash), d = id.Substring(dash + 1);
            int n = int.Parse(d), a = d.Length >= 2 ? int.Parse(d.Substring(0, d.Length - 1)) : n, b = d[d.Length - 1] - '0';
            switch (type)
            {
                case "cube": c.cube.Add(new NShapeDef { id = id, n = n }); break;
                case "tetra": c.tetra.Add(new NShapeDef { id = id, n = n }); break;
                case "octa": c.octa.Add(new NShapeDef { id = id, n = n }); break;
                case "dodeca": c.dodeca.Add(new NShapeDef { id = id, n = n }); break;
                case "geo": c.geo.Add(new NShapeDef { id = id, n = n }); break;
                case "ball": c.ball.Add(new BallShapeDef { id = id, r = n / 10f }); break;
                case "cross": c.cross.Add(new CrossShapeDef { id = id, arm = a, thick = b }); break;
                case "tower": c.tower.Add(new TowerShapeDef { id = id, w = a, h = b }); break;
                case "ell": c.ell.Add(new EllShapeDef { id = id, len = a, thick = b }); break;
            }
        }

        /// <summary>Problems found in the data that ships with the game (empty list = all good).</summary>
        public static List<string> Validate()
        {
            var problems = new List<string>();
            GameData.Reload();
            if (GameData.Legacy) { problems.Add("No data in Resources/CircuitCubeData: the game uses the built-in generator."); return problems; }
            for (int g = 0; g < GameData.GroupCount; g++)
            {
                var gd = GameData.Group(g);
                int total = 0;
                foreach (var ch in gd.chunks) total += ch.count;
                if (total != gd.levelCount) problems.Add(gd.id + ": chunks hold " + total + " levels, levelCount says " + gd.levelCount);
                var seen = new HashSet<int>();
                for (int p = 1; p <= gd.levelCount; p++)
                {
                    int serial = GameData.SerialAt(g, p);
                    if (serial <= 0) { problems.Add(gd.id + " position " + p + ": no serial"); continue; }
                    if (!seen.Add(serial)) problems.Add(gd.id + " position " + p + ": serial " + serial + " used twice");
                    var lv = GameData.Build(g, p);
                    if (lv == null) { problems.Add(gd.id + " position " + p + " (serial " + serial + "): level missing or does not fit its shape"); continue; }
                    if (!lv.IsSolved(true)) problems.Add(gd.id + " position " + p + ": the solution does not light every lamp");
                    if (lv.IsSolved(false)) problems.Add(gd.id + " position " + p + ": already solved at the start");
                    if (TileCodec.Par(lv.Tiles) != lv.Par) problems.Add(gd.id + " position " + p + ": par " + lv.Par + " but the start needs " + TileCodec.Par(lv.Tiles) + " taps");
                }
            }
            return problems;
        }
    }
}
