using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using CircuitCube.Core;
using CircuitCube.Game;
using UnityEditor;
using UnityEngine;

namespace CircuitCube.EditorTools
{
    /// <summary>
    /// The hand-made difficulty curve for the first 50 levels of each shape, as a Level Lab import zip
    /// (Tools/LevelLab/import/shapes_1-50_curve.zip). Each shape starts on its smallest board with one or two plain
    /// colours, then a mix (players already know mixing from Moonbase 21-30), then three colour groups, then a bigger
    /// board. Every level is the the one of 32 seeds closest to a par target, and is checked to be
    /// solvable and scrambled. Revisions go one above the published ones (content/) so phones download them again.
    /// The zip's manifest carries <see cref="Unlocks"/>: how many levels of the shape before opens the next one.
    /// </summary>
    public static class LevelCurves
    {
        const string Menu = "Tools/Circuit Cube/Data/";
        const string OutZip = "Tools/LevelLab/import/shapes_1-50_curve.zip";
        const int Seeds = 32;

        /// <summary>Levels of the shape before that open each shape (Moonbase 30 = after the mixing lessons).</summary>
        public static readonly Dictionary<string, (string after, int cleared)> Unlocks = new Dictionary<string, (string, int)>
        {
            { "tetra", ("cube", 30) }, { "octa", ("tetra", 25) }, { "sphere", ("octa", 25) },
            { "dodeca", ("sphere", 25) }, { "megamix", ("dodeca", 25) },
        };

        struct Spec
        {
            public int Size; public int[] Colors; public float Scramble; public int Phase;
            public Spec(int phase, int size, float scr, params int[] c) { Phase = phase; Size = size; Scramble = scr; Colors = c; }
        }

        // colour sets: 1 red, 2 blue, 4 yellow, 3 purple (R+B), 5 orange (R+Y), 6 green (B+Y)
        static readonly int[][] Singles = { new[] { 1 }, new[] { 2 }, new[] { 4 }, new[] { 1, 2 }, new[] { 2, 4 }, new[] { 1, 4 } };
        static readonly int[][] OneMix = { new[] { 3 }, new[] { 5 }, new[] { 6 }, new[] { 3, 4 }, new[] { 5, 2 }, new[] { 6, 1 } };
        static readonly int[][] ThreeOneMix = { new[] { 3, 1, 4 }, new[] { 5, 2, 4 }, new[] { 6, 1, 2 }, new[] { 3, 2, 4 }, new[] { 5, 1, 2 }, new[] { 6, 2, 4 } };
        static readonly int[][] ThreeTwoMix = { new[] { 3, 5, 2 }, new[] { 5, 6, 1 }, new[] { 6, 3, 4 }, new[] { 3, 1, 4 }, new[] { 5, 6, 2 }, new[] { 6, 3, 1 } };

        /// <summary>
        /// Board sizes per phase: A 1-5 and B 6-10 (smallest), C 11-20, D 21-30, E 31-40, F 41-49, and level 50.
        /// </summary>
        static readonly Dictionary<string, int[]> Sizes = new Dictionary<string, int[]>
        {
            //                 A  B  C  D  E  F  50
            { "tetra",  new[] { 2, 2, 3, 3, 4, 4, 5 } },     // 16, 36, 64, 100 tiles
            { "octa",   new[] { 2, 2, 2, 3, 3, 3, 4 } },     // 32, 72, 128
            { "sphere", new[] { 1, 1, 1, 2, 2, 2, 3 } },     // 32, 42, 92 (six-sided tiles take more taps)
            { "dodeca", new[] { 2, 2, 2, 2, 2, 2, 3 } },     // 60, 132
        };

        static readonly Dictionary<string, string> ShapePrefix = new Dictionary<string, string>
        {
            { "tetra", "tetra-" }, { "octa", "octa-" }, { "sphere", "geo-" }, { "dodeca", "dodeca-" },
        };

        /// <summary>
        /// Par the curve aims for at each level, on the Moonbase scale: 6 to 15 on the first board, a dip each time
        /// something new comes in (mixes at 21, three groups at 31, the big board at 41) and never a wall.
        /// </summary>
        static int Target(int l)
        {
            if (l <= 10) return 6 + (l - 1);                        // 6..15
            if (l <= 20) return 14 + (l - 11) * 3 / 2;              // 14..27
            if (l <= 30) return 20 + (l - 21) * 3 / 2;              // 20..33
            if (l <= 40) return 28 + (l - 31) * 3 / 2;              // 28..41
            if (l <= 49) return 36 + (l - 41) * 2;                  // 36..52
            return 56;
        }

        static List<Spec> CurveFor(string group)
        {
            var s = Sizes[group];
            var list = new List<Spec>();
            // A 1-5: the new shape with plain colours, few tiles turned
            list.Add(new Spec(0, s[0], .25f, 1)); list.Add(new Spec(0, s[0], .30f, 2)); list.Add(new Spec(0, s[0], .35f, 4));
            list.Add(new Spec(0, s[0], .30f, 1, 2)); list.Add(new Spec(0, s[0], .35f, 2, 4));
            // B 6-10: two or three plain colours, then the first mix on this shape
            list.Add(new Spec(1, s[1], .35f, 1, 4)); list.Add(new Spec(1, s[1], .40f, 1, 2, 4)); list.Add(new Spec(1, s[1], .35f, 3));
            list.Add(new Spec(1, s[1], .35f, 5)); list.Add(new Spec(1, s[1], .40f, 6));
            // C 11-20: one mix and a plain colour
            for (int i = 0; i < 10; i++) list.Add(new Spec(2, s[2], .35f + .02f * i, i % 2 == 0 ? OneMix[3 + (i / 2) % 3] : Singles[3 + (i / 2) % 3]));
            // D 21-30: three groups, one mix
            for (int i = 0; i < 10; i++) list.Add(new Spec(3, s[3], .38f + .015f * i, ThreeOneMix[i % ThreeOneMix.Length]));
            // E 31-40: three groups, one or two mixes
            for (int i = 0; i < 10; i++) list.Add(new Spec(4, s[4], .38f + .015f * i, i % 2 == 0 ? ThreeOneMix[(i / 2) % 6] : ThreeTwoMix[(i / 2) % 6]));
            // F 41-49: two mixes, then 50 on the next board
            for (int i = 0; i < 9; i++) list.Add(new Spec(5, s[5], .40f + .01f * i, ThreeTwoMix[i % ThreeTwoMix.Length]));
            list.Add(new Spec(6, s[6], .32f, 3, 5, 2));
            return list;
        }

        [MenuItem(Menu + "Build Shape Curves (levels 1-50)")]
        public static void BuildMenu()
        {
            string report = Build(out int bad);
            Debug.Log("[CircuitCube] Shape curves\n" + report);
            EditorUtility.DisplayDialog("Circuit Cube data", (bad == 0 ? "Wrote " : bad + " PROBLEMS. Wrote ") + OutZip + ".\n\nDetails in the console.", "OK");
        }

        /// <summary>Builds the zip; returns one line per level (par, tiles, colours) and the number of problems.</summary>
        public static string Build(out int bad)
        {
            bad = 0;
            var report = new StringBuilder();
            var files = new SortedDictionary<string, string>();
            var catalog = new Dictionary<string, List<string>>();
            string repo = Path.GetDirectoryName(Application.dataPath);
            foreach (var group in new[] { "tetra", "octa", "sphere", "dodeca" })
            {
                var specs = CurveFor(group);
                for (int i = 0; i < specs.Count; i++)
                {
                    int l = i + 1;
                    var sp = specs[i];
                    string shape = ShapePrefix[group] + sp.Size;
                    var topo = ShapeResolver.Build(shape, null);
                    if (topo == null) { bad++; report.AppendLine(group + " " + l + ": no shape " + shape); continue; }
                    if (!catalog.TryGetValue(group == "sphere" ? "geo" : group, out var ids)) catalog[group == "sphere" ? "geo" : group] = ids = new List<string>();
                    if (!ids.Contains(shape)) ids.Add(shape);
                    int minPar = LevelPlan.MinParFor(l, topo.Count);

                    // every seed's par; the pick climbs inside a phase and starts low in a new one
                    var cands = new List<(CubeLevel lv, uint seed)>();
                    for (int k = 0; k < Seeds; k++)
                    {
                        uint seed = (uint)(GroupSalt(group) * 7919 + l * 104729 + k * 6151);
                        var lv = CubeGenerator.Generate(l, topo, sp.Colors, seed, minPar, sp.Scramble);
                        if (Key(lv.Colors) != Key(sp.Colors)) continue;   // the generator may drop a colour that did not fit
                        cands.Add((lv, seed));
                    }
                    if (cands.Count == 0) { bad++; report.AppendLine(group + " " + l + ": no level with colours " + string.Join(",", sp.Colors)); continue; }
                    cands.Sort((a, b) => a.lv.Par.CompareTo(b.lv.Par));
                    // the seed whose par is closest to the curve's target (ties: the easier one)
                    int want = Target(l);
                    var pick = cands[0];
                    foreach (var c in cands) if (System.Math.Abs(c.lv.Par - want) < System.Math.Abs(pick.lv.Par - want)) pick = c;

                    var lvl = pick.lv;
                    bool ok = lvl.IsSolved(true) && !lvl.IsSolved(false);
                    string tiles = TileCodec.Encode(lvl.Tiles);
                    var d = new LevelData
                    {
                        id = group + "-" + l.ToString("D4"), serial = l, group = group, rev = PublishedRev(repo, group, l) + 1, shape = shape,
                        ports = lvl.Tiles[0].Ports, tiles = tiles, par = lvl.Par, check = TileCodec.Check(tiles),
                        difficulty = TileCodec.Difficulty(lvl.Par, lvl.Tiles.Length, lvl.Colors.Length),
                        gen = new GenInfo { ver = 1, seed = pick.seed, colors = new List<int>(lvl.Colors), scramble = sp.Scramble, minPar = minPar }
                    };
                    if (l % 10 == 0) d.tags.Add("milestone");
                    // round-trip through the data reader, as the game will load it
                    var back = GameData.FromData(JsonUtility.FromJson<LevelData>(JsonUtility.ToJson(d)), null);
                    ok &= back != null && back.IsSolved(true) && !back.IsSolved(false) && TileCodec.Par(back.Tiles) == d.par;
                    if (!ok) bad++;
                    files["levels/" + group + "/" + l.ToString("D4") + ".json"] = JsonUtility.ToJson(d, true);
                    report.Append(group).Append(' ').Append(l).Append(" ").Append(shape).Append(" tiles=").Append(lvl.Tiles.Length)
                        .Append(" par=").Append(lvl.Par).Append(" colours=").Append(string.Join("", lvl.Colors)).Append(ok ? "" : "  PROBLEM").Append('\n');
                }
            }

            // manifest: the published one with the new unlock rules (no rules block: a Merge import keeps Level Lab's)
            var man = JsonUtility.FromJson<ManifestData>(File.ReadAllText(Path.Combine(repo, "content/manifest.json")));
            var m = new StringBuilder("{\n  \"schema\": 3,\n  \"groups\": [\n");
            for (int i = 0; i < man.groups.Count; i++)
            {
                var g = man.groups[i];
                string after = g.unlock != null ? g.unlock.group ?? "" : "";
                int cleared = g.unlock != null ? g.unlock.cleared : 0;
                if (Unlocks.TryGetValue(g.id, out var u)) { after = u.after; cleared = u.cleared; }
                m.Append("    { \"id\": \"").Append(g.id).Append("\", \"unlock\": { \"group\": \"").Append(after).Append("\", \"cleared\": ").Append(cleared).Append(" } }")
                 .Append(i < man.groups.Count - 1 ? ",\n" : "\n");
            }
            m.Append("  ]\n}\n");
            files["manifest.json"] = m.ToString();

            // the published shape catalog plus any new size these levels use (tetra-2), so Level Lab and publish know it
            string sj = File.ReadAllText(Path.Combine(repo, "content/shapes.json"));
            foreach (var kv in catalog)
                foreach (var id in kv.Value)
                {
                    if (sj.Contains("\"" + id + "\"")) continue;
                    var at = System.Text.RegularExpressions.Regex.Match(sj, "\"" + kv.Key + "\"\\s*:\\s*\\[");
                    if (!at.Success) { bad++; report.AppendLine("shapes.json has no list " + kv.Key); continue; }
                    int n = int.Parse(id.Substring(id.LastIndexOf('-') + 1));
                    sj = sj.Insert(at.Index + at.Length, "\n    { \"id\": \"" + id + "\", \"n\": " + n + " },");
                }
            files["shapes.json"] = sj;

            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(repo, OutZip)));
            string zipPath = Path.Combine(repo, OutZip);
            if (File.Exists(zipPath)) File.Delete(zipPath);
            using (var fs = File.Create(zipPath))
            using (var z = new ZipArchive(fs, ZipArchiveMode.Create))
                foreach (var kv in files)
                {
                    var e = z.CreateEntry(kv.Key, System.IO.Compression.CompressionLevel.Optimal);
                    using (var w = new StreamWriter(e.Open())) w.Write(kv.Value);
                }
            report.Insert(0, "problems=" + bad + "  zip=" + OutZip + "\n");
            return report.ToString();
        }

        static string Key(int[] c) { var a = (int[])c.Clone(); System.Array.Sort(a); return string.Join(",", a); }

        // string.GetHashCode differs between runtimes; this keeps the seeds the same everywhere
        static uint GroupSalt(string g) { uint h = 2166136261; foreach (char c in g) h = (h ^ c) * 16777619; return h & 0xffff; }

        /// <summary>Revision of a level in the last publish (content/levels), 1 if it was never published.</summary>
        static int PublishedRev(string repo, string group, int serial)
        {
            string p = Path.Combine(repo, "content/levels", group, serial.ToString("D4") + ".json");
            if (!File.Exists(p)) return 1;
            var d = JsonUtility.FromJson<LevelData>(File.ReadAllText(p));
            return d != null && d.rev > 0 ? d.rev : 1;
        }
    }
}
