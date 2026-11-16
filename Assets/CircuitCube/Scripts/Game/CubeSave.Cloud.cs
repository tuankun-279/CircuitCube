using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CircuitCube.Game
{
    [Serializable] public class CloudGroup
    {
        public string id;
        public int cleared;
        public bool unlocked;
        public List<StarChunk> stars = new List<StarChunk>();
    }

    /// <summary>All progress in one record for the cloud save (settings and levels in progress stay on the device).</summary>
    [Serializable] public class CloudProgress
    {
        public int version = 1;
        public long savedAt;   // unix seconds
        public int tutorial, tips, mixWins;
        public bool seenHowTo, seenMix;
        public List<string> seenShapes = new List<string>();
        public List<CloudGroup> groups = new List<CloudGroup>();

        /// <summary>How far the record goes: levels beaten plus stars, to pick the better of two records.</summary>
        public int Weight()
        {
            int w = 0;
            foreach (var g in groups)
            {
                w += g.cleared * 10;
                foreach (var c in g.stars) foreach (char ch in c.points) w += ch - '0';
            }
            return w;
        }
    }

    public static partial class CubeSave
    {
        /// <summary>Raised after cloud progress was merged in, so screens can redraw.</summary>
        public static event Action Merged;

        static IEnumerable<string> KnownGroupIds()
        {
            var ids = new HashSet<string>();
            foreach (var g in Profile.groups) ids.Add(g.id);
            for (int i = 0; i < GameData.GroupCount; i++) ids.Add(GameData.GroupId(i));
            string dir = Path.Combine(RootDir, "groups");
            if (Directory.Exists(dir))
                foreach (var f in Directory.GetFiles(dir, "*.json")) ids.Add(Path.GetFileNameWithoutExtension(f));
            return ids;
        }

        static GroupProgress ProgById(string id)
        {
            if (_groups.TryGetValue(id, out var g)) return g;
            g = Read<GroupProgress>(GroupPath(id)) ?? new GroupProgress { id = id };
            if (g.inProgress == null) g.inProgress = new InProgress();
            _groups[id] = g;
            return g;
        }

        static List<int> StarChunksOnDisk(string id)
        {
            var ks = new List<int>();
            string dir = Path.Combine(RootDir, "groups", id);
            if (!Directory.Exists(dir)) return ks;
            foreach (var f in Directory.GetFiles(dir, "stars-*.json"))
            {
                string n = Path.GetFileNameWithoutExtension(f).Substring(6);
                if (int.TryParse(n, out int k)) ks.Add(k);
            }
            return ks;
        }

        /// <summary>This device's progress as a cloud record.</summary>
        public static CloudProgress ExportCloud()
        {
            var p = Profile;
            var r = new CloudProgress
            {
                savedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                tutorial = p.tutorial, tips = p.tips, mixWins = p.mixWins, seenHowTo = p.seenHowTo, seenMix = p.seenMix,
                seenShapes = new List<string>(p.seenShapes)
            };
            foreach (var id in KnownGroupIds())
            {
                var g = ProgById(id);
                var e = p.groups.Find(x => x.id == id);
                var cg = new CloudGroup { id = id, cleared = g.cleared, unlocked = e != null && e.unlocked };
                foreach (int k in StarChunksOnDisk(id))
                {
                    var c = Chunk(id, k);
                    if (!string.IsNullOrEmpty(c.points)) cg.stars.Add(new StarChunk { chunk = k, points = c.points });
                }
                if (cg.cleared > 0 || cg.unlocked || cg.stars.Count > 0) r.groups.Add(cg);
            }
            return r;
        }

        /// <summary>Best of two score strings, digit by digit. Pure, for the tests.</summary>
        public static string MaxPoints(string a, string b)
        {
            a = a ?? ""; b = b ?? "";
            int n = Math.Max(a.Length, b.Length);
            var o = new char[n];
            for (int i = 0; i < n; i++)
            {
                char x = i < a.Length ? a[i] : '0', y = i < b.Length ? b[i] : '0';
                o[i] = x > y ? x : y;
            }
            return new string(o);
        }

        /// <summary>
        /// Merge a cloud record into this device: every group keeps the furthest level beaten and every level its best
        /// score; lessons seen and shapes opened add up. Nothing is ever lost on either side. Returns whether anything
        /// on the device changed.
        /// </summary>
        public static bool MergeCloud(CloudProgress cloud)
        {
            if (cloud == null) return false;
            var p = Profile;
            bool changed = false, profileChanged = false;
            if (cloud.tutorial > p.tutorial) { p.tutorial = cloud.tutorial; profileChanged = true; }
            if ((p.tips | cloud.tips) != p.tips) { p.tips |= cloud.tips; profileChanged = true; }
            if (cloud.mixWins > p.mixWins) { p.mixWins = cloud.mixWins; profileChanged = true; }
            if (cloud.seenHowTo && !p.seenHowTo) { p.seenHowTo = true; profileChanged = true; }
            if (cloud.seenMix && !p.seenMix) { p.seenMix = true; profileChanged = true; }
            foreach (var s in cloud.seenShapes) if (!p.seenShapes.Contains(s)) { p.seenShapes.Add(s); profileChanged = true; }
            foreach (var cg in cloud.groups)
            {
                if (string.IsNullOrEmpty(cg.id)) continue;
                if (cg.unlocked)
                {
                    var e = p.groups.Find(x => x.id == cg.id);
                    if (e == null) p.groups.Add(e = new GroupUnlock { id = cg.id });
                    if (!e.unlocked) { e.unlocked = true; profileChanged = true; }
                }
                var g = ProgById(cg.id);
                if (cg.cleared > g.cleared) { g.cleared = cg.cleared; SaveProg(g); changed = true; }
                foreach (var cs in cg.stars)
                {
                    var c = Chunk(cg.id, cs.chunk);
                    string best = MaxPoints(c.points, cs.points);
                    if (best == c.points) continue;
                    c.points = best;
                    WriteAtomic(StarPath(cg.id, cs.chunk), JsonUtility.ToJson(c));
                    changed = true;
                }
            }
            if (profileChanged) SaveProfile();
            changed |= profileChanged;
            if (changed) Merged?.Invoke();
            return changed;
        }
    }
}
