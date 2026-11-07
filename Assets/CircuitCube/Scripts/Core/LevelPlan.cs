using System.Collections.Generic;

namespace CircuitCube.Core
{
    /// <summary>
    /// Level groups. Each group has its own shape family and 100 levels; beating level 100 of a group unlocks the
    /// next one. The last group (Nebula, id megamix) keeps going after 100. Levels are deterministic: group 2 level 37 is the
    /// same board for every player.
    /// </summary>
    public static class LevelPlan
    {
        public const int LevelsPerGroup = 100;
        public const int MixIntroLevel = 6;   // in the Cube group

        public sealed class Group
        {
            public int Index;
            public string Name;
            public string Blurb;
            public bool Endless;
        }

        // Player-facing names follow a space theme; ids (data files, saves, card art) keep the shape names.
        public static readonly Group[] Groups =
        {
            new Group { Index = 0, Name = "Moonbase", Blurb = "Six square faces. Wires wrap over every edge of the base." },
            new Group { Index = 1, Name = "Pyramid", Blurb = "Four triangle faces. One tap turns a tile a third of a turn." },
            new Group { Index = 2, Name = "Star Gem", Blurb = "Eight triangle faces, four meeting at every point." },
            new Group { Index = 3, Name = "Planet", Blurb = "A world of pentagons and hexagons. Six ways out of most tiles." },
            new Group { Index = 4, Name = "Galaxy", Blurb = "Twelve pentagon faces, cut finer and finer, up to 132 tiles." },
            new Group { Index = 5, Name = "Nebula", Blurb = "Stations, towers and shapes nobody has seen. A new one every level, never ends.", Endless = true },
        };

        /// <summary>Group id used by the data files for a generator group (also the card art order).</summary>
        public static readonly string[] GroupIds = { "cube", "tetra", "octa", "sphere", "dodeca", "megamix" };

        /// <summary>Group order of the first releases (before Octa and Dodeca), for moving old PlayerPrefs saves.</summary>
        public static readonly string[] OldGroupIds = { "cube", "tetra", "sphere", "cross", "megamix" };

        static bool Big(int level) => level >= 30 && level % 10 == 0;

        public static int GroupCount => Groups.Length;

        /// <summary>Highest level a group offers (Megamix has no end; the cap only bounds the level list).</summary>
        public static int MaxLevel(int group) => Groups[group].Endless ? 9999 : LevelsPerGroup;

        // ---------------- Cube group (unchanged from the first release, so old progress still matches) ----------------

        struct Step { public int Size; public int[] Colors; public Step(int s, params int[] c) { Size = s; Colors = c; } }

        static readonly Step[] CubeIntro =
        {
            new Step(2, 1), new Step(2, 1), new Step(2, 1, 2), new Step(3, 1), new Step(3, 1, 2), new Step(3, 3),
            new Step(3, 1, 2, 4), new Step(3, 5, 2), new Step(3, 6, 1), new Step(3, 3, 4), new Step(3, 1, 2, 6), new Step(3, 3, 5),
        };

        static int CubeSize(int level)
        {
            if (level <= CubeIntro.Length) return CubeIntro[level - 1].Size;
            if (level >= 30 && level % 10 == 0) return 5;
            return level <= 24 ? 3 : 4;
        }

        // ---------------- Shapes per group ----------------

        /// <summary>Board for a level. Each group starts small for ten levels, then grows; every 10th level is a big one.</summary>
        public static BoardTopology TopologyFor(int group, int level)
        {
            switch (GroupIds[group])
            {
                case "tetra": return PolyShapes.Tetrahedron(TetraSize(level));
                case "octa": return PolyShapes.Octahedron(OctaSize(level));
                case "sphere": return PolyShapes.Goldberg(SphereSize(level));
                case "dodeca": return DodecaSize(level) == 2 ? PolyShapes.Kilominx() : PolyShapes.Megaminx();
                default: return new ShapeTopology(ShapeFor(group, level));
            }
        }

        // Tetrahedron, n x n triangles per face: 36, 64, 100, 144 tiles.
        static int TetraSize(int level) => level <= 10 ? 3 : Big(level) ? 6 : level <= 50 ? 4 : 5;
        // Octahedron, n x n triangles per face: 32, 72, 128 tiles.
        static int OctaSize(int level) => level <= 10 ? 2 : Big(level) || level > 50 ? 4 : 3;
        // Soccer ball 32, then Goldberg balls of 42, 92 and 162 tiles.
        static int SphereSize(int level) => level <= 10 ? 1 : Big(level) ? 4 : level <= 30 ? 2 : 3;
        // Kilominx (60 kites), Megaminx (132) from level 41 and on the big levels.
        static int DodecaSize(int level) => level <= 40 && !Big(level) ? 2 : 3;

        // Nebula (Megamix) is where the unusual shapes live: every third level is a station shape (crosses, towers,
        // L shapes) taking turns, bigger ones as the levels go on; the others are a new mix of blocks.
        static readonly string[] SmallStations = { "cross-11", "cross-21" };
        static readonly string[] MidStations = { "cross-21", "tower-24", "ell-42", "cross-31" };
        static readonly string[] BigStations = { "tower-24", "ell-42", "cross-31", "tower-34", "cross-22" };

        static string StationFor(int level)
        {
            if (level % 3 != 0) return null;
            if (Big(level)) return "cross-22";
            var list = level <= 20 ? SmallStations : level <= 50 ? MidStations : BigStations;
            return list[(level / 3) % list.Length];
        }

        /// <summary>Block shape for the groups built from blocks (Cube, Nebula).</summary>
        public static List<Vec3i> ShapeFor(int group, int level)
        {
            bool big = Big(level);
            switch (GroupIds[group])
            {
                case "cube": return ShapeLibrary.Cube(CubeSize(level));
                default:
                {
                    string station = StationFor(level);
                    if (station != null)
                    {
                        string d = station.Substring(station.IndexOf('-') + 1);
                        int a = d[0] - '0', b = d[1] - '0';
                        return station.StartsWith("tower") ? ShapeLibrary.Tower(a, b) : station.StartsWith("ell") ? ShapeLibrary.Ell(a, b) : ShapeLibrary.Cross(a, b);
                    }
                    int min = level <= 10 ? 50 : 70, max = level <= 10 ? 90 : big ? 180 : 140;
                    return ShapeLibrary.Megamix(SeedFor(group, level) ^ 0x2545f491u, min, max);
                }
            }
        }

        /// <summary>
        /// Shape id of a level in the v3 data format (see ShapeResolver). Nebula's mixed levels get their own voxel shape
        /// id; the blocks come from <see cref="ShapeFor"/>.
        /// </summary>
        public static string ShapeIdFor(int group, int level)
        {
            switch (GroupIds[group])
            {
                case "cube": return "cube-" + CubeSize(level);
                case "tetra": return "tetra-" + TetraSize(level);
                case "octa": return "octa-" + OctaSize(level);
                case "sphere": return "geo-" + SphereSize(level);
                case "dodeca": return "dodeca-" + DodecaSize(level);
                default: return StationFor(level) ?? "voxel-mega-" + level.ToString("D4");
            }
        }

        // ---------------- Colours ----------------

        static readonly int[] Singles = { 1, 2, 4 };
        static readonly int[] Mixes = { 3, 5, 6 };

        public static int[] ColorsFor(int group, int level)
        {
            if (group == 0 && level <= CubeIntro.Length) return CubeIntro[level - 1].Colors;
            var R = new CubeRng(SeedFor(group, level) ^ 0x9e3779b9u);
            int count, mixes;
            if (group > 0 && level <= 3) { count = level == 1 ? 2 : 3; mixes = 1; }
            else if (group > 0 && level <= 10) { count = 3; mixes = 1 + R.Range(2); }
            else { count = level <= 30 ? 3 : 4; mixes = 1 + R.Range(level <= 30 ? 2 : 3); }
            var groups = new List<int>();
            var s = new List<int>(Singles); R.Shuffle(s);
            var m = new List<int>(Mixes); R.Shuffle(m);
            for (int i = 0; i < mixes && i < m.Count; i++) groups.Add(m[i]);
            for (int i = 0; groups.Count < count && i < s.Count; i++) groups.Add(s[i]);
            return groups.ToArray();
        }

        // ---------------- Seeds and difficulty ----------------

        /// <summary>The Cube group keeps the first release's seeds.</summary>
        public static uint SeedFor(int group, int level) =>
            group == 0 ? (uint)(level * 7919 + 17) : (uint)((group * 100003 + level) * 7919 + 17);

        public static int MinParFor(int level, int tiles) => System.Math.Min(tiles / 7 + 6, 5 + level);

        /// <summary>
        /// Share of tiles turned away from the solution. Bigger boards scramble a smaller share so a level stays around
        /// 30-110 taps. The first ten levels of a group ramp up from easy.
        /// </summary>
        public static float ScrambleFor(int group, int level, int tiles)
        {
            if (group == 0 && level <= CubeIntro.Length) return 0.3f + 0.03f * (level - 1);
            float s = tiles <= 60 ? 0.6f : tiles <= 100 ? 0.45f : 0.32f;
            if (group > 0 && level <= 10) s *= 0.6f + 0.04f * level;
            // a hexagon tile takes up to five taps to come back, so the balls turn fewer tiles
            if (GroupIds[group] == "sphere") s *= 0.7f;
            return s;
        }

        static readonly Dictionary<long, CubeLevel> Cache = new Dictionary<long, CubeLevel>();

        /// <summary>Returns a fresh copy the caller may modify.</summary>
        public static CubeLevel Build(int group, int level)
        {
            if (level < 1) level = 1;
            long key = (long)group * 100000 + level;
            if (!Cache.TryGetValue(key, out var lv))
            {
                var topo = TopologyFor(group, level);
                lv = CubeGenerator.Generate(level, topo, ColorsFor(group, level), SeedFor(group, level),
                    MinParFor(level, topo.Count), ScrambleFor(group, level, topo.Count));
                lv.Group = group;
                Cache[key] = lv;
            }
            return lv.Clone();
        }
    }
}
