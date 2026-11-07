using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using CircuitCube.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace CircuitCube.Game
{
    /// <summary>
    /// Keeps the download cache in step with the CDN: manifest, group files, the order chunks around the player's
    /// position, and only the levels from X−windowBack to X+windowAhead in each open group. Anything that fails keeps
    /// the files already on the device; the game never waits on the network.
    /// </summary>
    public static class RemoteContent
    {
        public static bool Busy { get; private set; }

        /// <summary>
        /// The published content needs a newer game (manifest minApp above Application.version). The device keeps
        /// the content it has and the game asks the player to update.
        /// </summary>
        public static bool NeedsUpdate { get; private set; }

        /// <summary>Compares dotted versions ("1.2.10" &gt; "1.2.9"); missing parts count as 0, other text is ignored.</summary>
        public static int CompareVersions(string a, string b)
        {
            var x = (a ?? "").Split('.');
            var y = (b ?? "").Split('.');
            for (int i = 0; i < Mathf.Max(x.Length, y.Length); i++)
            {
                int p = i < x.Length ? Lead(x[i]) : 0, q = i < y.Length ? Lead(y[i]) : 0;
                if (p != q) return p < q ? -1 : 1;
            }
            return 0;
        }

        static int Lead(string part)
        {
            int n = 0;
            foreach (char c in part) { if (c < '0' || c > '9') break; n = n * 10 + (c - '0'); }
            return n;
        }

        /// <param name="baseUrl">
        /// Where manifest.json is read (the repo's content folder on raw.githubusercontent.com, so a new publish shows within
        /// minutes). The manifest's <c>cdn</c> then names the immutable copy of that publish (a content-v&lt;n&gt; tag on
        /// jsDelivr) for groups/, orders/, levels/ and shapes/.
        /// </param>
        /// <param name="clearedOf">Positions cleared per group index (from the save).</param>
        public static IEnumerator Sync(string baseUrl, Func<int, int> clearedOf, Action done)
        {
            if (string.IsNullOrEmpty(baseUrl) || Busy) { done?.Invoke(); yield break; }
            Busy = true;
            baseUrl = baseUrl.TrimEnd('/') + "/";
            string root = GameData.CacheRoot;

            // the new manifest lands next to the old one and replaces it only when this game can play it
            bool ok = false;
            yield return Fetch(baseUrl, "manifest.json", root, r => ok = r, "manifest.new.json");
            ManifestData m = null;
            string staged = Path.Combine(root, "manifest.new.json");
            if (ok)
            {
                try { m = JsonUtility.FromJson<ManifestData>(File.ReadAllText(staged)); } catch (Exception) { m = null; }
                NeedsUpdate = m != null && CompareVersions(m.minApp, Application.version) > 0;
                if (m == null || NeedsUpdate)
                {
                    if (NeedsUpdate) Debug.Log("[CircuitCube] Content needs app " + m.minApp + ", this is " + Application.version + ": keeping the old content.");
                    try { File.Delete(staged); } catch (IOException) { }
                    ok = false;
                }
                else ok = Replace(staged, Path.Combine(root, "manifest.json"));
            }
            if (ok)
            {
                if (m != null && !string.IsNullOrEmpty(m.cdn)) baseUrl = m.cdn.TrimEnd('/') + "/";
                foreach (var g in m.groups) yield return Fetch(baseUrl, "groups/" + g.id + ".json", root, null);
                GameData.Reload();

                var rules = GameData.Rules;
                for (int gi = 0; gi < GameData.GroupCount; gi++)
                {
                    var gd = GameData.Group(gi);
                    if (gd.levelCount == 0) continue;
                    int x = Mathf.Clamp(clearedOf(gi) + 1, 1, gd.levelCount);
                    int from = Mathf.Max(1, x - rules.windowBack), to = Mathf.Min(gd.levelCount, x + rules.windowAhead);

                    // order chunks covering the window: download when the cached version differs
                    for (int k = (from - 1) / gd.chunkSize; k <= (to - 1) / gd.chunkSize; k++)
                    {
                        var info = gd.chunks.Find(c => c.index == k);
                        string rel = GameData.ChunkPath(gd.id, k);
                        var cached = ReadCached<OrderChunk>(root, rel);
                        if (info != null && (cached == null || cached.version != info.version)) yield return Fetch(baseUrl, rel, root, null);
                    }
                    GameData.Reload();

                    var keep = new HashSet<string>();
                    for (int p = from; p <= to; p++)
                    {
                        int serial = GameData.SerialAt(gi, p);
                        if (serial <= 0) continue;
                        string rel = GameData.LevelPath(gd.id, serial);
                        keep.Add(Path.GetFileName(rel));
                        // fetch when missing, or when the order chunk says the level was edited since the copy we have
                        int want = GameData.RevAt(gi, p);
                        var have = GameData.Has(rel) ? GameData.ReadJson<LevelData>(rel) : null;
                        if (have == null || (want > 0 && have.rev != want)) yield return Fetch(baseUrl, rel, root, null);
                        var lv = ReadCached<LevelData>(root, rel);
                        if (lv != null && lv.shape != null && lv.shape.StartsWith("voxel-"))
                        {
                            string sr = "shapes/voxel/" + lv.shape + ".json";
                            if (!GameData.Has(sr)) yield return Fetch(baseUrl, sr, root, null);
                        }
                    }
                    Prune(Path.Combine(root, "levels", gd.id), keep);
                }
            }
            Busy = false;
            done?.Invoke();
        }

        static T ReadCached<T>(string root, string rel) where T : class
        {
            string p = Path.Combine(root, rel);
            if (!File.Exists(p)) return null;
            try { return JsonUtility.FromJson<T>(File.ReadAllText(p)); } catch (Exception) { return null; }
        }

        static void Prune(string dir, HashSet<string> keep)
        {
            if (!Directory.Exists(dir)) return;
            foreach (var f in Directory.GetFiles(dir, "*.json"))
                if (!keep.Contains(Path.GetFileName(f))) { try { File.Delete(f); } catch (IOException) { } }
        }

        static bool Replace(string from, string to)
        {
            try
            {
                if (File.Exists(to)) File.Delete(to);
                File.Move(from, to);
                return true;
            }
            catch (IOException) { return false; }
        }

        /// <param name="saveAs">Where to keep the file under <paramref name="root"/> (default: the same relative path).</param>
        static IEnumerator Fetch(string baseUrl, string rel, string root, Action<bool> result, string saveAs = null)
        {
            using (var req = UnityWebRequest.Get(baseUrl + rel))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
                bool ok = req.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(req.downloadHandler.text);
                if (ok)
                {
                    string path = Path.Combine(root, saveAs ?? rel);
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        string tmp = path + ".tmp";
                        File.WriteAllText(tmp, req.downloadHandler.text);
                        if (File.Exists(path)) File.Delete(path);
                        File.Move(tmp, path);
                    }
                    catch (IOException) { ok = false; }
                }
                result?.Invoke(ok);
            }
        }
    }
}
