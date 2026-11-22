using System;
using System.Collections.Generic;
using System.IO;
using CircuitCube.Core;
using UnityEngine;

namespace CircuitCube.Game
{
    // ---------------- save files, format v3 ----------------
    //   save/profile.json                    PlayerProfile   settings, flags, index of groups {id, unlocked}
    //   save/groups/<id>.json                GroupProgress   cleared position + level in progress
    //   save/groups/<id>/stars-<k>.json      StarChunk       100 digits: score of serials k*100+1 … k*100+100

    [Serializable] public class SaveSettings
    {
        public bool sound = true, music = true, vibration = true;
        public string theme = "system";
        public float soundVolume = 1f, musicVolume = 1f;   // 0..1, the sliders in Settings; 0 = muted
        public string language = "";                        // "en", "vi"; empty = the phone's language
        public int zoomButtons = -1;                        // zoom buttons in a level: -1 on emulators only, 0 off, 1 on
    }
    [Serializable] public class GroupUnlock { public string id; public bool unlocked; }
    [Serializable] public class GroupSeen { public string id; public int levels; }
    [Serializable] public class PlayerProfile
    {
        public int saveVersion = 3;
        public string playerId;
        public SaveSettings settings = new SaveSettings();
        public string currentGroup;
        public List<GroupUnlock> groups = new List<GroupUnlock>();
        public bool seenHowTo, seenMix;
        /// <summary>First-time tutorial steps done: 1 tap a tile, 2 spin the shape, 3 use a hint (3 = finished or skipped).</summary>
        public int tutorial;
        /// <summary>Later lessons seen, as bits: see CubeSave.TipColours and TipMix.</summary>
        public int tips;
        /// <summary>Levels with a mixed colour the player has won (the HUD spells out the mixes for the first few).</summary>
        public int mixWins;
        /// <summary>2 = scores use Expert 7 (before, Expert was 6 and 6 is now three stars with help).</summary>
        public int scoreVersion;
        public List<string> seenShapes = new List<string>();
        public List<GroupSeen> seenLevels = new List<GroupSeen>();   // level count of each group the player last saw
    }
    [Serializable] public class InProgress
    {
        public int serial;                 // 0 = nothing in progress
        public int position;
        public string check = "";          // the level's check when it was saved; another check = the level was edited
        public string rotations = "";      // one digit per tile
        public int moves, hintsUsed, undosUsed;
        public float seconds;              // play time so far
        public List<int> hintedTiles = new List<int>();
        public List<int> taps = new List<int>();   // tiles turned in order (for the leaderboards); older saves have none
    }
    [Serializable] public class GroupProgress
    {
        public string id;
        public int cleared;
        public InProgress inProgress = new InProgress();
    }
    [Serializable] public class StarChunk
    {
        public int chunk;
        public string points = "";
    }

    /// <summary>
    /// Progress on this device. Each group is its own file and scores are split in chunks of 100 serials, so a win
    /// rewrites two small files. The API keeps the names the game already used; groups are indices in manifest order.
    /// The first run after the update copies the old PlayerPrefs progress over.
    /// </summary>
    public static partial class CubeSave
    {
        public const int StarChunkSize = 100;
        const string MigratedKey = "cc.savev3";

        /// <summary>Folder of the save files. Tests point it somewhere else.</summary>
        public static string Root = null;
        static string RootDir => Root ?? Path.Combine(Application.persistentDataPath, "save");

        static PlayerProfile _profile;
        static readonly Dictionary<string, GroupProgress> _groups = new Dictionary<string, GroupProgress>();
        static readonly Dictionary<string, StarChunk> _stars = new Dictionary<string, StarChunk>();

        // ---------------- files ----------------

        static string ProfilePath => Path.Combine(RootDir, "profile.json");
        static string GroupPath(string id) => Path.Combine(RootDir, "groups", id + ".json");
        static string StarPath(string id, int k) => Path.Combine(RootDir, "groups", id, "stars-" + k + ".json");

        public static void WriteAtomic(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string tmp = path + ".tmp", bak = path + ".bak";
            File.WriteAllText(tmp, text);
            if (File.Exists(path)) { File.Copy(path, bak, true); File.Delete(path); }
            File.Move(tmp, path);
        }

        static T Read<T>(string path) where T : class
        {
            foreach (var p in new[] { path, path + ".bak" })
            {
                try { if (File.Exists(p)) { var o = JsonUtility.FromJson<T>(File.ReadAllText(p)); if (o != null) return o; } }
                catch (Exception e) { Debug.LogWarning("[CircuitCube] Save file " + p + " unreadable: " + e.Message); }
            }
            return null;
        }

        static PlayerProfile Profile
        {
            get
            {
                if (_profile != null) return _profile;
                _profile = Read<PlayerProfile>(ProfilePath);
                if (_profile == null)
                {
                    _profile = new PlayerProfile { playerId = Guid.NewGuid().ToString(), scoreVersion = ScoreVersion };
                    if (PlayerPrefs.GetInt(MigratedKey, 0) == 0) MigrateFromPlayerPrefs();
                    PlayerPrefs.SetInt(MigratedKey, 1);
                    PlayerPrefs.Save();
                    SaveProfile();
                }
                if (_profile.scoreVersion < ScoreVersion) UpgradeScores();
                return _profile;
            }
        }

        const int ScoreVersion = 2;

        /// <summary>
        /// Scores saved before 1.2.0 used 6 for an Expert win (par, no hint). 1.2.0 keeps 6 for three stars won with
        /// help and gives Expert its own 7, so old 6s become 7: nobody loses a comet they had.
        /// </summary>
        static void UpgradeScores()
        {
            string dir = Path.Combine(RootDir, "groups");
            if (Directory.Exists(dir))
                foreach (var f in Directory.GetFiles(dir, "stars-*.json", SearchOption.AllDirectories))
                {
                    var c = Read<StarChunk>(f);
                    if (c == null || c.points == null || c.points.IndexOf('6') < 0) continue;
                    c.points = c.points.Replace('6', '7');
                    WriteAtomic(f, JsonUtility.ToJson(c));
                }
            _stars.Clear();
            _profile.scoreVersion = ScoreVersion;
            SaveProfile();
        }

        static void SaveProfile() => WriteAtomic(ProfilePath, JsonUtility.ToJson(_profile, true));

        static GroupProgress Prog(int group)
        {
            string id = GameData.GroupId(group);
            if (_groups.TryGetValue(id, out var g)) return g;
            g = Read<GroupProgress>(GroupPath(id)) ?? new GroupProgress { id = id };
            if (g.inProgress == null) g.inProgress = new InProgress();
            _groups[id] = g;
            return g;
        }

        static void SaveProg(GroupProgress g) => WriteAtomic(GroupPath(g.id), JsonUtility.ToJson(g, true));

        static StarChunk Chunk(string id, int k)
        {
            string key = id + "/" + k;
            if (_stars.TryGetValue(key, out var c)) return c;
            c = Read<StarChunk>(StarPath(id, k)) ?? new StarChunk { chunk = k };
            _stars[key] = c;
            return c;
        }

        /// <summary>Forget everything cached in memory (after files changed on disk, and in tests).</summary>
        public static void Unload() { _profile = null; _groups.Clear(); _stars.Clear(); }

        // ---------------- progress ----------------

        /// <summary>Next level to play in a group (1 when nothing is beaten yet).</summary>
        public static int Unlocked(int group) => Beaten(group) + 1;
        public static int Beaten(int group) { var _ = Profile; return Prog(group).cleared; }

        /// <summary>Opens by the manifest rule; once open, stays open even if the rule changes later.</summary>
        public static bool GroupOpen(int group)
        {
            string id = GameData.GroupId(group);
            var e = Profile.groups.Find(x => x.id == id);
            if (e != null && e.unlocked) return true;
            var rule = GameData.Unlock(group);
            bool open = rule.IsNone;
            if (!open)
            {
                int pre = GameData.IndexOf(rule.group);
                open = pre < 0 || Beaten(pre) >= rule.cleared;
            }
            if (open)
            {
                if (e == null) Profile.groups.Add(e = new GroupUnlock { id = id });
                e.unlocked = true;
                SaveProfile();
            }
            return open;
        }

        /// <summary>Score 3–6 of the level at a position (0 = not won yet).</summary>
        public static int Points(int group, int position)
        {
            int serial = GameData.SerialAt(group, position);
            if (serial <= 0) return 0;
            var c = Chunk(GameData.GroupId(group), (serial - 1) / StarChunkSize);
            int i = (serial - 1) % StarChunkSize;
            return i < c.points.Length ? c.points[i] - '0' : 0;
        }

        /// <summary>Stars of every level won, counted in halves (a 5-point win is 2½ stars).</summary>
        public static int TotalStarHalves
        {
            get
            {
                int sum = 0;
                for (int g = 0; g < GameData.GroupCount; g++)
                {
                    string dir = Path.Combine(RootDir, "groups", GameData.GroupId(g));
                    if (!Directory.Exists(dir)) continue;
                    foreach (var f in Directory.GetFiles(dir, "stars-*.json"))
                    {
                        if (!int.TryParse(Path.GetFileNameWithoutExtension(f).Substring(6), out int k)) continue;
                        foreach (char ch in Chunk(GameData.GroupId(g), k).points) sum += LevelScore.HalfStars(ch - '0');
                    }
                }
                return sum;
            }
        }

        public static void Complete(int group, int position, int points)
        {
            var _ = Profile;
            var g = Prog(group);
            int serial = GameData.SerialAt(group, position);
            if (serial > 0)
            {
                int k = (serial - 1) / StarChunkSize, i = (serial - 1) % StarChunkSize;
                var c = Chunk(g.id, k);
                if (c.points.Length <= i) c.points = c.points.PadRight(i + 1, '0');
                if (points > c.points[i] - '0')
                {
                    var arr = c.points.ToCharArray(); arr[i] = (char)('0' + Mathf.Clamp(points, 0, 9)); c.points = new string(arr);
                    WriteAtomic(StarPath(g.id, k), JsonUtility.ToJson(c));
                }
            }
            if (position > g.cleared) g.cleared = position;
            if (g.inProgress.serial == serial || g.inProgress.position == position) g.inProgress = new InProgress();
            SaveProg(g);
        }

        // ---------------- level in progress ----------------

        public static InProgress GetInProgress(int group) => Prog(group).inProgress;

        public static void SetInProgress(int group, InProgress ip)
        {
            var g = Prog(group);
            g.inProgress = ip ?? new InProgress();
            SaveProg(g);
        }

        // ---------------- profile ----------------

        /// <summary>The group the Play button continues: the last one played, if it is open.</summary>
        public static int CurrentGroup
        {
            get
            {
                int g = GameData.IndexOf(Profile.currentGroup);
                return g >= 0 && GroupOpen(g) ? g : 0;
            }
            set { Profile.currentGroup = GameData.GroupId(value); SaveProfile(); }
        }

        public static SaveSettings Settings => Profile.settings;
        public static void SaveSettings() => SaveProfile();

        public static bool SeenShape(int group) => group == 0 || Profile.seenShapes.Contains(GameData.GroupId(group));
        public static void MarkSeenShape(int group)
        {
            string id = GameData.GroupId(group);
            if (!Profile.seenShapes.Contains(id)) { Profile.seenShapes.Add(id); SaveProfile(); }
        }

        /// <summary>
        /// The group has more levels than when the player last opened its level list (new levels were published).
        /// A group seen for the first time records its count and shows no badge.
        /// </summary>
        public static bool HasNewLevels(int group)
        {
            if (!GroupOpen(group)) return false;
            string id = GameData.GroupId(group);
            int count = GameData.Group(group).levelCount;
            var seen = SeenLevels.Find(s => s.id == id);
            if (seen == null) { SeenLevels.Add(new GroupSeen { id = id, levels = count }); SaveProfile(); return false; }
            // an endless group only counts as new when the player has reached its end
            if (GameData.Group(group).endless && Beaten(group) < seen.levels) return false;
            return count > seen.levels;
        }

        static List<GroupSeen> SeenLevels => Profile.seenLevels ?? (Profile.seenLevels = new List<GroupSeen>());

        public static void MarkLevelsSeen(int group)
        {
            string id = GameData.GroupId(group);
            int count = GameData.Group(group).levelCount;
            var seen = SeenLevels.Find(s => s.id == id);
            if (seen == null) SeenLevels.Add(seen = new GroupSeen { id = id });
            if (seen.levels == count) return;
            seen.levels = count;
            SaveProfile();
        }

        public static bool SeenMix { get => Profile.seenMix; set { Profile.seenMix = value; SaveProfile(); } }
        public static bool SeenHowTo { get => Profile.seenHowTo; set { Profile.seenHowTo = value; SaveProfile(); } }
        public static int TutorialStep { get => Profile.tutorial; set { Profile.tutorial = value; SaveProfile(); } }
        public static int Tips { get => Profile.tips; set { Profile.tips = value; SaveProfile(); } }
        public static int MixWins { get => Profile.mixWins; set { Profile.mixWins = value; SaveProfile(); } }
        /// <summary>Bit in <see cref="Tips"/>: the "each lamp needs its own colour" lesson.</summary>
        public const int TipColours = 1;
        /// <summary>Bit in <see cref="Tips"/> for the lesson on one mixed colour (3 purple, 5 orange, 6 green).</summary>
        public static int TipMix(int color) => color == 3 ? 2 : color == 5 ? 4 : color == 6 ? 8 : 0;
        /// <summary>Bit in <see cref="Tips"/>: the tip beside the zoom buttons was shown.</summary>
        public const int TipZoom = 16;

        /// <summary>Test helper: mark every level of a group up to <paramref name="level"/> as beaten.</summary>
        public static void UnlockUpTo(int group, int level)
        {
            var _ = Profile;
            var g = Prog(group);
            if (level > g.cleared) { g.cleared = level; SaveProg(g); }
        }

        public static void ResetAll()
        {
            Unload();
            try { if (Directory.Exists(RootDir)) Directory.Delete(RootDir, true); } catch (IOException) { }
            PlayerPrefs.SetInt(MigratedKey, 1);   // do not pull the old progress back in
            PlayerPrefs.Save();
        }

        // ---------------- migration from the PlayerPrefs save (before 0.1.1) ----------------

        static void MigrateFromPlayerPrefs()
        {
            string Unl(int g) => g == 0 ? "cc.unlocked" : "cc.g" + g + ".unlocked";
            string Sk(int g, int l) => g == 0 ? "cc.stars." + l : "cc.g" + g + ".stars." + l;
            bool any = false;
            for (int lg = 0; lg < LevelPlan.OldGroupIds.Length; lg++)
            {
                int unlocked = PlayerPrefs.GetInt(Unl(lg), 0);
                if (unlocked <= 1) continue;
                int g = GameData.IndexOf(LevelPlan.OldGroupIds[lg]);
                if (g < 0) continue;
                any = true;
                var p = Prog(g);
                p.cleared = Mathf.Max(p.cleared, Mathf.Min(unlocked - 1, GameData.MaxLevel(g)));
                for (int l = 1; l < unlocked; l++)
                {
                    int s = PlayerPrefs.GetInt(Sk(lg, l), 0);
                    if (s <= 0) continue;
                    int serial = GameData.SerialAt(g, l);
                    if (serial <= 0) continue;
                    int k = (serial - 1) / StarChunkSize, i = (serial - 1) % StarChunkSize;
                    var c = Chunk(p.id, k);
                    if (c.points.Length <= i) c.points = c.points.PadRight(i + 1, '0');
                    var arr = c.points.ToCharArray(); arr[i] = (char)('0' + (s >= 3 ? 6 : s == 2 ? 4 : 3)); c.points = new string(arr);
                }
                SaveProg(p);
                foreach (var kv in _stars) if (kv.Key.StartsWith(p.id + "/")) WriteAtomic(StarPath(p.id, kv.Value.chunk), JsonUtility.ToJson(kv.Value));
                if (PlayerPrefs.GetInt("cc.g" + lg + ".seen", 0) == 1) _profile.seenShapes.Add(p.id);
            }
            _profile.seenMix = PlayerPrefs.GetInt("cc.seenMix", 0) == 1;
            _profile.seenHowTo = PlayerPrefs.GetInt("cc.seenHowTo", 0) == 1;
            int cg = PlayerPrefs.GetInt("cc.group", 0);
            if (cg >= 0 && cg < LevelPlan.OldGroupIds.Length) _profile.currentGroup = LevelPlan.OldGroupIds[cg];
            if (any) Debug.Log("[CircuitCube] Progress moved from the old save to save v3.");
        }
    }
}
