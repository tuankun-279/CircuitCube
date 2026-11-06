using System.Collections.Generic;
using System.IO;
using CircuitCube.Core;
using UnityEngine;

namespace CircuitCube.Game
{
    /// <summary>
    /// Level data, format v3. Files are read from the download cache first (persistentDataPath/cdn, filled by
    /// <see cref="RemoteContent"/>), then from the copy shipped in the build (Resources/CircuitCubeData).
    /// Groups are addressed by index in manifest order. When no data ships at all, the game falls back to the built-in
    /// generator (<see cref="LevelPlan"/>) so it still runs.
    /// </summary>
    public static class GameData
    {
        public const string ResourceRoot = "CircuitCubeData/";
        public static string CacheRoot => Path.Combine(Application.persistentDataPath, "cdn");

        public static ManifestData Manifest { get; private set; }
        public static bool Legacy { get; private set; }
        public static GameRules Rules => Manifest != null && Manifest.rules != null ? Manifest.rules : new GameRules();

        static readonly List<GroupData> _groups = new List<GroupData>();
        static readonly Dictionary<string, int> _index = new Dictionary<string, int>();
        static readonly Dictionary<string, OrderChunk> _chunks = new Dictionary<string, OrderChunk>();
        static readonly Dictionary<string, VoxelShapeFile> _voxels = new Dictionary<string, VoxelShapeFile>();
        static bool _loaded;

        public static int GroupCount { get { Ensure(); return _groups.Count; } }
        public static GroupData Group(int g) { Ensure(); return _groups[Mathf.Clamp(g, 0, _groups.Count - 1)]; }
        public static string GroupId(int g) => Group(g).id;
        public static int IndexOf(string id) { Ensure(); return id != null && _index.TryGetValue(id, out int i) ? i : -1; }
        public static UnlockRule Unlock(int g) { Ensure(); return Manifest.groups[g].unlock ?? new UnlockRule(); }

        /// <summary>Levels in the group's order (endless groups: the stored ones; play continues past them).</summary>
        public static int LevelCount(int g) => Group(g).levelCount;
        public static int MaxLevel(int g) => Group(g).endless ? 9999 : Mathf.Max(1, LevelCount(g));

        public static void Ensure() { if (!_loaded) Reload(); }

        public static void Reload()
        {
            _loaded = true;
            _groups.Clear(); _index.Clear(); _chunks.Clear(); _voxels.Clear();
            Manifest = ReadJson<ManifestData>("manifest.json");
            Legacy = Manifest == null || Manifest.groups == null || Manifest.groups.Count == 0;
            if (Legacy) { BuildLegacy(); return; }
            foreach (var r in Manifest.groups)
            {
                var g = ReadJson<GroupData>("groups/" + r.id + ".json") ?? new GroupData { id = r.id, name = r.id, enable = false };
                if (g.chunkSize <= 0) g.chunkSize = 100;
                if (!g.enable) continue;                       // hidden groups stay out of the game
                _index[r.id] = _groups.Count;
                _groups.Add(g);
            }
            // keep Manifest.groups aligned with the visible groups
            Manifest.groups.RemoveAll(r => !_index.ContainsKey(r.id));
        }

        static void BuildLegacy()
        {
            Manifest = new ManifestData();
            for (int g = 0; g < LevelPlan.GroupCount; g++)
            {
                var info = LevelPlan.Groups[g];
                string id = LevelPlan.GroupIds[g];
                Manifest.groups.Add(new GroupRef { id = id, unlock = g == 0 ? new UnlockRule() : new UnlockRule { group = LevelPlan.GroupIds[g - 1], cleared = LevelPlan.LevelsPerGroup } });
                _index[id] = g;
                _groups.Add(new GroupData { id = id, name = info.Name, blurb = info.Blurb, icon = id, endless = info.Endless, enable = true, levelCount = LevelPlan.LevelsPerGroup });
            }
        }

        // ---------------- files ----------------

        public static string ReadText(string relPath)
        {
            try
            {
                string p = Path.Combine(CacheRoot, relPath);
                if (File.Exists(p)) return File.ReadAllText(p);
            }
            catch (IOException) { }
            string res = ResourceRoot + (relPath.EndsWith(".json") ? relPath.Substring(0, relPath.Length - 5) : relPath);
            var ta = Resources.Load<TextAsset>(res);
            return ta != null ? ta.text : null;
        }

        public static bool Has(string relPath) => ReadText(relPath) != null;

        public static T ReadJson<T>(string relPath) where T : class
        {
            string txt = ReadText(relPath);
            if (string.IsNullOrEmpty(txt)) return null;
            try { return JsonUtility.FromJson<T>(txt); }
            catch (System.ArgumentException e) { Debug.LogWarning("[CircuitCube] Bad data file " + relPath + ": " + e.Message); return null; }
        }

        public static string LevelPath(string group, int serial) => "levels/" + group + "/" + serial.ToString("D4") + ".json";
        public static string ChunkPath(string group, int k) => "orders/" + group + "/" + k + ".json";

        // ---------------- order ----------------

        public static OrderChunk Chunk(int g, int k)
        {
            string id = GroupId(g), key = id + "/" + k;
            if (_chunks.TryGetValue(key, out var c)) return c;
            c = ReadJson<OrderChunk>(ChunkPath(id, k));
            if (c != null) _chunks[key] = c;
            return c;
        }

        /// <summary>Serial of the level at a 1-based position, 0 if unknown. Positions past an endless group's data map to themselves.</summary>
        public static int SerialAt(int g, int position)
        {
            if (Legacy || position < 1) return position;
            var gd = Group(g);
            if (position > gd.levelCount) return gd.endless ? position : 0;
            int k = (position - 1) / gd.chunkSize;
            var c = Chunk(g, k);
            int i = (position - 1) % gd.chunkSize;
            return c != null && i < c.levels.Count ? c.levels[i] : 0;
        }

        /// <summary>Revision the order chunk lists for the level at a position, 0 if it lists none.</summary>
        public static int RevAt(int g, int position)
        {
            if (Legacy || position < 1) return 0;
            var gd = Group(g);
            if (position > gd.levelCount) return 0;
            var c = Chunk(g, (position - 1) / gd.chunkSize);
            int i = (position - 1) % gd.chunkSize;
            return c != null && c.revs != null && i < c.revs.Count ? c.revs[i] : 0;
        }

        // ---------------- levels ----------------

        static VoxelShapeFile Voxel(string id)
        {
            if (_voxels.TryGetValue(id, out var v)) return v;
            v = ReadJson<VoxelShapeFile>("shapes/voxel/" + id + ".json");
            if (v != null) _voxels[id] = v;
            return v;
        }

        public static LevelData LevelDataAt(int g, int position)
        {
            int serial = SerialAt(g, position);
            return serial > 0 ? ReadJson<LevelData>(LevelPath(GroupId(g), serial)) : null;
        }

        /// <summary>
        /// The level at a position, ready to play, or null when its data is not on this device yet (offline past the
        /// download window). Endless groups continue with generated levels after their stored ones.
        /// </summary>
        public static CubeLevel Build(int g, int position)
        {
            Ensure();
            int legacyGroup = System.Array.IndexOf(LevelPlan.GroupIds, GroupId(g));
            if (Legacy || (Group(g).endless && position > LevelCount(g) && legacyGroup >= 0))
                return Number(LevelPlan.Build(legacyGroup >= 0 ? legacyGroup : g, position), g, position);

            var d = LevelDataAt(g, position);
            if (d == null) return null;
            var lv = FromData(d, Voxel);
            return lv == null ? null : Number(lv, g, position);
        }

        static CubeLevel Number(CubeLevel lv, int g, int position) { lv.Group = g; lv.Number = position; return lv; }

        public static CubeLevel FromData(LevelData d, System.Func<string, VoxelShapeFile> voxels)
        {
            var topo = ShapeResolver.Build(d.shape, voxels);
            if (topo == null) { Debug.LogWarning("[CircuitCube] Unknown shape " + d.shape + " in " + d.id); return null; }
            var tiles = TileCodec.Decode(d.tiles, topo);
            if (tiles == null) { Debug.LogWarning("[CircuitCube] Tiles of " + d.id + " do not fit shape " + d.shape); return null; }
            if (d.locked != null) foreach (int i in d.locked) if (i >= 0 && i < tiles.Length) tiles[i].Locked = true;
            return new CubeLevel { Topo = topo, Tiles = tiles, Par = d.par, Colors = TileCodec.LampColors(tiles) };
        }
    }
}
