using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CircuitCube.Core;
using Microsoft.Extensions.DependencyInjection;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;
using Unity.Services.Leaderboards.Model;

namespace CircuitCubeRanks
{
    public class ModuleConfig : ICloudCodeSetup
    {
        public void Setup(ICloudCodeConfig config)
        {
            config.Dependencies.AddSingleton(GameApiClient.Create());
            config.Dependencies.AddSingleton<Content>();
        }
    }

    /// <summary>Answer sent back to the game (Game/Services/Ranks.cs RankReply has the same fields).</summary>
    public class RankReply
    {
        public bool ok { get; set; }
        public string error { get; set; } = "";
        public string board { get; set; } = "";
        public double score { get; set; }
        public int rank { get; set; }
        public int players { get; set; }
        public int experts { get; set; }
        public long moves { get; set; }
        public long tenths { get; set; }
    }

    /// <summary>What the server keeps for a player in protected Cloud Save data (players can read it, not write it).</summary>
    public class WeekRecord
    {
        public string week { get; set; } = "";
        /// <summary>Per shape: best weekly score per slot, index 0 = slot 1; 0 = not won yet.</summary>
        public Dictionary<string, List<double>> sets { get; set; } = new Dictionary<string, List<double>>();
        /// <summary>Per shape: highest play clock seen per slot, in tenths of a second (<see cref="WeeklyClocks"/>).</summary>
        public Dictionary<string, List<int>> clocks { get; set; } = new Dictionary<string, List<int>>();
    }

    /// <summary>The best Expert run of one level: fewest moves, then the shorter time.</summary>
    public class LevelBest
    {
        public int m { get; set; }
        public long t { get; set; }
    }

    public class ShapeRecord
    {
        /// <summary>Best run of each level won as Expert, by serial.</summary>
        public Dictionary<string, LevelBest> levels { get; set; } = new Dictionary<string, LevelBest>();
    }

    /// <summary>
    /// The published level data, read the way the game reads it: manifest.json from the repo (the newest publish),
    /// every other file from the publish's immutable CDN folder.
    /// </summary>
    public class Content
    {
        public const string ManifestBase = "https://raw.githubusercontent.com/tuankun-279/CircuitCube/main/content/";
        static readonly TimeSpan ManifestAge = TimeSpan.FromSeconds(60);
        static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = true };
        static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        ManifestData _manifest;
        DateTime _manifestAt;
        readonly ConcurrentDictionary<string, object> _files = new ConcurrentDictionary<string, object>();

        public async Task<ManifestData> Manifest()
        {
            if (_manifest == null || DateTime.UtcNow - _manifestAt > ManifestAge)
            {
                var m = await Read<ManifestData>(ManifestBase + "manifest.json");
                if (m != null) { _manifest = m; _manifestAt = DateTime.UtcNow; }
            }
            return _manifest;
        }

        string FileBase(ManifestData m) => string.IsNullOrEmpty(m.cdn) ? ManifestBase : m.cdn.TrimEnd('/') + "/";

        /// <summary>A file of the current publish; publishes are immutable, so files are kept once read.</summary>
        public async Task<T> File<T>(ManifestData m, string rel) where T : class
        {
            string url = FileBase(m) + rel;
            if (_files.TryGetValue(url, out var hit)) return hit as T;
            var v = await Read<T>(url);
            if (v != null) _files[url] = v;
            return v;
        }

        static async Task<T> Read<T>(string url) where T : class
        {
            try
            {
                var text = await Http.GetStringAsync(url);
                return JsonSerializer.Deserialize<T>(text, Json);
            }
            catch (Exception) { return null; }
        }

        /// <summary>A level file as a board, with its block shape read when it needs one.</summary>
        public async Task<CubeLevel> Level(ManifestData m, string rel)
        {
            var d = await File<LevelData>(m, rel);
            if (d == null) return null;
            VoxelShapeFile voxel = null;
            if (d.shape != null && d.shape.StartsWith("voxel-", StringComparison.Ordinal))
                voxel = await File<VoxelShapeFile>(m, "shapes/voxel/" + d.shape + ".json");
            return RunCheck.Build(d, _ => voxel);
        }

        public static string Serialize<T>(T v) => JsonSerializer.Serialize(v);
        public static T Parse<T>(object v) where T : class, new()
        {
            if (v == null) return new T();
            try { return JsonSerializer.Deserialize<T>(v is string s ? s : v.ToString()) ?? new T(); }
            catch (Exception) { return new T(); }
        }
    }

    /// <summary>
    /// Endpoints the game calls with CallModuleEndpointAsync("CircuitCubeRanks", ...). Players cannot write scores
    /// themselves (Deploy/ranks.ac denies it); only these functions post, with the service token.
    /// </summary>
    public class Ranks
    {
        const int MaxTaps = 5000;
        const string DailyBoard = "daily_players";
        readonly IGameApiClient _api;
        readonly Content _content;

        public Ranks(IGameApiClient api, Content content) { _api = api; _content = content; }

        /// <summary>A weekly challenge level won without hints or undos. key = "&lt;week id&gt;/&lt;group id&gt;", index = slot 1..count.</summary>
        [CloudCodeFunction("SubmitWeekly")]
        public async Task<RankReply> SubmitWeekly(IExecutionContext ctx, string key, int index, List<int> taps, int tenths)
        {
            var m = await _content.Manifest();
            if (m == null) return Fail("content");
            var w = m.challenge;
            int cut = key == null ? -1 : key.IndexOf('/');
            if (w == null || w.IsNone || cut <= 0 || key.Substring(0, cut) != w.id) return Fail("week");
            string group = key.Substring(cut + 1);
            if (!w.Has(group)) return Fail("shape");
            if (!DateTime.TryParse(w.start, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var opens)) return Fail("week");
            var now = DateTime.UtcNow;
            if (now < opens || now >= opens.AddDays(Math.Max(1, w.days))) return Fail("closed");
            if (index < 1 || index > w.count || taps == null || taps.Count > MaxTaps) return Fail("args");

            var level = await _content.Level(m, ChallengeWeek.LevelPath(w.id, group, index));
            var run = RunCheck.Replay(level, taps, tenths, m.rules);
            if (!run.Valid) return Fail(run.Error);

            string recKey = "weekly";
            var rec = Content.Parse<WeekRecord>(await ReadProtected(ctx, recKey));
            if (rec.week != w.id || rec.sets == null) rec = new WeekRecord { week = w.id };
            if (!rec.sets.TryGetValue(group, out var best) || best == null) rec.sets[group] = best = new List<double>();
            while (best.Count < w.count) best.Add(0);
            string board = RankScore.WeeklyBoard(group, index);
            // the clock of a level never goes back, whatever happened to the app's data
            if (rec.clocks == null) rec.clocks = new Dictionary<string, List<int>>();
            if (!rec.clocks.TryGetValue(group, out var clocks) || clocks == null) rec.clocks[group] = clocks = new List<int>();
            while (clocks.Count < w.count) clocks.Add(0);
            tenths = WeeklyClocks.Counted(clocks[index - 1], tenths);
            bool later = tenths > clocks[index - 1];
            clocks[index - 1] = tenths;

            double score = RankScore.Weekly(run.Moves, tenths);
            bool better = best[index - 1] <= 0 || score < best[index - 1];
            if (better) best[index - 1] = score;
            if (better || later) await WriteProtected(ctx, recKey, Content.Serialize(rec));

            if (better) await Post(ctx, board, score);
            // the set total: all levels won; sum of their best moves, then of their best times
            if (better && best.Take(w.count).All(s => s > 0))
            {
                int moves = best.Take(w.count).Sum(s => RankScore.WeeklyMoves(s));
                int time = best.Take(w.count).Sum(s => RankScore.WeeklyTenths(s));
                await Post(ctx, RankScore.WeeklyTotalBoard(group), RankScore.Weekly(moves, Math.Min(time, RankScore.MaxTenths)));
            }
            var reply = await Reply(ctx, board, best[index - 1]);
            reply.tenths = tenths;   // the time that counted; the game's clock goes on from it
            return reply;
        }

        /// <summary>A shape level won as Expert. key = group id, index = level serial.</summary>
        [CloudCodeFunction("SubmitShape")]
        public async Task<RankReply> SubmitShape(IExecutionContext ctx, string key, int index, List<int> taps, int tenths)
        {
            var m = await _content.Manifest();
            if (m == null) return Fail("content");
            if (string.IsNullOrEmpty(key) || m.groups.All(g => g.id != key)) return Fail("shape");
            if (index <= 0 || taps == null || taps.Count > MaxTaps) return Fail("args");
            var group = await _content.File<GroupData>(m, "groups/" + key + ".json");
            var level = await _content.Level(m, "levels/" + key + "/" + index.ToString("D4") + ".json");
            var run = RunCheck.Replay(level, taps, tenths, m.rules, group);
            if (!run.Valid) return Fail(run.Error);
            if (!run.Expert) return Fail("not_expert");

            string recKey = "shape_" + key;
            var rec = Content.Parse<ShapeRecord>(await ReadProtected(ctx, recKey));
            string serial = index.ToString(CultureInfo.InvariantCulture);
            bool better = !rec.levels.TryGetValue(serial, out var had) || run.Moves < had.m || (run.Moves == had.m && tenths < had.t);
            if (better) rec.levels[serial] = new LevelBest { m = run.Moves, t = tenths };
            int experts = rec.levels.Count;
            long moves = rec.levels.Values.Sum(l => (long)l.m);
            long total = rec.levels.Values.Sum(l => l.t);
            string board = RankScore.ShapeBoard(key);
            double score = RankScore.Shape(experts, moves, total);
            if (better)
            {
                await WriteProtected(ctx, recKey, Content.Serialize(rec));
                await Post(ctx, board, score);
            }
            var reply = await Reply(ctx, board, score);
            reply.experts = experts;
            reply.moves = moves;
            reply.tenths = total;
            return reply;
        }

        /// <summary>
        /// Counts the player once on today's board of unique players (Deploy/daily_players.lb, reset every day at
        /// 00:00 Asia/Saigon). Called once per app launch after sign-in. Answers how many players are on it so far.
        /// </summary>
        [CloudCodeFunction("CheckIn")]
        public async Task<RankReply> CheckIn(IExecutionContext ctx)
        {
            await Post(ctx, DailyBoard, 1);
            return await Reply(ctx, DailyBoard, 1);
        }

        static RankReply Fail(string error) => new RankReply { ok = false, error = error };

        async Task Post(IExecutionContext ctx, string board, double score) =>
            await _api.Leaderboards.AddLeaderboardPlayerScoreAsync(ctx, ctx.ServiceToken, new Guid(ctx.ProjectId), board, ctx.PlayerId,
                new AddLeaderboardScore(score));

        async Task<RankReply> Reply(IExecutionContext ctx, string board, double score)
        {
            var r = new RankReply { ok = true, board = board, score = score };
            try
            {
                var mine = await _api.Leaderboards.GetLeaderboardPlayerScoreAsync(ctx, ctx.ServiceToken, new Guid(ctx.ProjectId), board, ctx.PlayerId);
                r.rank = mine.Data.Rank + 1;
                var top = await _api.Leaderboards.GetLeaderboardScoresAsync(ctx, ctx.ServiceToken, new Guid(ctx.ProjectId), board, offset: 0, limit: 1);
                r.players = top.Data.Total;
            }
            catch (Exception) { r.rank = 0; }
            return r;
        }

        async Task<object> ReadProtected(IExecutionContext ctx, string key)
        {
            try
            {
                var res = await _api.CloudSaveData.GetProtectedItemsAsync(ctx, ctx.ServiceToken, ctx.ProjectId, ctx.PlayerId, new List<string> { key });
                return res.Data.Results.FirstOrDefault(i => i.Key == key)?.Value;
            }
            catch (Exception) { return null; }
        }

        async Task WriteProtected(IExecutionContext ctx, string key, string value) =>
            await _api.CloudSaveData.SetProtectedItemAsync(ctx, ctx.ServiceToken, ctx.ProjectId, ctx.PlayerId, new SetItemBody(key, value));
    }
}
