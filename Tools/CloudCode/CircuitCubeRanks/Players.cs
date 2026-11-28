using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using CircuitCube.Core;
using Studio.Tracking;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;
using Unity.Services.Leaderboards.Model;

namespace CircuitCubeRanks
{
    /// <summary>Answer of the name endpoints (Game/Services/Ranks.cs NameReply has the same fields).</summary>
    public class NameReply
    {
        public bool ok { get; set; }
        public string error { get; set; } = "";
        public string name { get; set; } = "";
        /// <summary>Renames the player may still make for free.</summary>
        public int renamesLeft { get; set; }
        /// <summary>The player chose a name through the 1.5.0 name sheet (or had one reset and chose again).</summary>
        public bool named { get; set; }
        /// <summary>The team reset this player's name after a report: the game shows the default name and asks again.</summary>
        public bool reset { get; set; }
    }

    /// <summary>What the server keeps about a player's name (protected player data "name").</summary>
    public class NameRecord
    {
        public string name { get; set; } = "";
        public bool named { get; set; }
        /// <summary>Free renames in total: 0 for a player who named themself after the tutorial, 1 for a player from before 1.5.0.</summary>
        public int free { get; set; }
        public int renames { get; set; }
        public bool reset { get; set; }
        public string at { get; set; } = "";
    }

    /// <summary>A report of a player name, kept in Cloud Save custom data "name_reports" (players cannot read it, ranks.ac).</summary>
    public class NameReport
    {
        public string id { get; set; } = "";
        public string player { get; set; } = "";
        public string name { get; set; } = "";
        public string board { get; set; } = "";
        public string reporter { get; set; } = "";
        public string at { get; set; } = "";
    }

    /// <summary>What a reporter did today (protected player data "reports"): a daily cap and one report per player.</summary>
    public class ReporterRecord
    {
        public string day { get; set; } = "";
        public List<string> players { get; set; } = new List<string>();
    }

    public class ReportList
    {
        public bool ok { get; set; }
        public string error { get; set; } = "";
        public List<NameReport> reports { get; set; } = new List<NameReport>();
        public string next { get; set; } = "";
    }

    public class TodayReply
    {
        public bool ok { get; set; }
        public string error { get; set; } = "";
        public string day { get; set; } = "";
        public int players { get; set; }
        public int testers { get; set; }
    }

    public partial class Ranks
    {
        const string TestDailyBoard = "daily_players_dev";
        const string ReportsId = "name_reports";
        const int ReportsPerDay = 10;

        static NameRules _names;

        /// <summary>The same name rules the game uses (Resources/NameRules.txt, embedded in the module).</summary>
        static NameRules Names
        {
            get
            {
                if (_names != null) return _names;
                var asm = Assembly.GetExecutingAssembly();
                string res = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("NameRules.txt", StringComparison.Ordinal));
                using (var s = res != null ? asm.GetManifestResourceStream(res) : null)
                using (var r = s != null ? new StreamReader(s) : null)
                    _names = NameRules.Parse(r != null ? r.ReadToEnd() : "");
                return _names;
            }
        }

        static string SaigonDay => DateTime.UtcNow.AddHours(7).ToString("yyyy-MM-dd");
        static string Now => DateTime.UtcNow.ToString("o");

        /// <summary>A call made with a service account (Level Lab sync on the PC), not by a player.</summary>
        static bool Admin(IExecutionContext ctx) => string.IsNullOrEmpty(ctx.PlayerId);

        /// <summary>
        /// The check-in of 1.5.0 and later: once per launch after sign-in. Only Google Play installs (channel "play")
        /// are counted on daily_players; every other channel goes to daily_players_dev, so tests still show up
        /// somewhere without counting as players. Channel and version ride along as score metadata (no personal data).
        /// </summary>
        [CloudCodeFunction("DailyCheckIn")]
        public async Task<RankReply> DailyCheckIn(IExecutionContext ctx, string channel, string version)
        {
            string c = ChannelRules.Normalize(channel);
            string board = ChannelRules.DailyBoard(c, DailyBoard, TestDailyBoard);
            var meta = new Dictionary<string, object> { { "c", c }, { "v", (version ?? "").Length > 16 ? version.Substring(0, 16) : version ?? "" } };
            await _api.Leaderboards.AddLeaderboardPlayerScoreAsync(ctx, ctx.ServiceToken, new Guid(ctx.ProjectId), board, ctx.PlayerId,
                new AddLeaderboardScore(1, meta));
            return await Reply(ctx, board, 1);
        }

        /// <summary>What the player may do with their name now.</summary>
        [CloudCodeFunction("NameState")]
        public async Task<NameReply> NameState(IExecutionContext ctx)
        {
            var rec = await ReadName(ctx, ctx.PlayerId);
            return State(rec);
        }

        static NameReply State(NameRecord rec)
        {
            if (rec == null) return new NameReply { ok = true, renamesLeft = 1 };
            return new NameReply
            {
                ok = true, name = rec.name, named = rec.named, reset = rec.reset,
                renamesLeft = rec.reset ? 1 : Math.Max(0, rec.free - rec.renames)
            };
        }

        /// <summary>
        /// Checks a name and records the change. first = the name sheet after the tutorial of a new player (free, and
        /// no free rename after it); otherwise a rename from Settings (one free rename for a player from before 1.5.0,
        /// and a fresh choice after the team reset the name). The game sets the name on the service only after ok.
        /// Refusals: name, reserved, empty, no_renames.
        /// </summary>
        [CloudCodeFunction("ClaimName")]
        public async Task<NameReply> ClaimName(IExecutionContext ctx, string name, bool first)
        {
            var verdict = Names.Check(name, out var clean);
            if (verdict == NameVerdict.Empty) return new NameReply { error = "empty" };
            if (verdict == NameVerdict.Blocked) return new NameReply { error = "name" };
            if (verdict == NameVerdict.Reserved) return new NameReply { error = "reserved" };

            var rec = await ReadName(ctx, ctx.PlayerId);
            if (rec == null) rec = first ? new NameRecord { named = true, free = 0 } : new NameRecord { named = true, free = 1, renames = 1 };
            else if (rec.reset) { rec.reset = false; rec.named = true; }
            else if (rec.renames < rec.free) rec.renames++;
            else { var r = State(rec); r.ok = false; r.error = "no_renames"; return r; }
            rec.name = clean;
            rec.at = Now;
            await WriteProtectedFor(ctx, ctx.PlayerId, "name", Content.Serialize(rec));
            var reply = State(rec);
            reply.ok = true;
            return reply;
        }

        /// <summary>A player reports the name of another row on a board. At most 10 a day, one per reported player.</summary>
        [CloudCodeFunction("ReportName")]
        public async Task<RankReply> ReportName(IExecutionContext ctx, string player, string name, string board)
        {
            if (string.IsNullOrEmpty(player) || player.Length > 64 || player == ctx.PlayerId) return Fail("args");
            var mine = Content.Parse<ReporterRecord>(await ReadProtected(ctx, "reports"));
            if (mine.day != SaigonDay) mine = new ReporterRecord { day = SaigonDay };
            if (mine.players.Contains(player)) return new RankReply { ok = true, board = board ?? "" };   // already reported today
            if (mine.players.Count >= ReportsPerDay) return Fail("limit");
            mine.players.Add(player);
            await WriteProtected(ctx, "reports", Content.Serialize(mine));

            var report = new NameReport
            {
                id = "r" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "_" + ctx.PlayerId.Substring(0, Math.Min(8, ctx.PlayerId.Length)),
                player = player,
                name = Clip(name, 40),
                board = Clip(board, 40),
                reporter = ctx.PlayerId,
                at = Now
            };
            await _api.CloudSaveData.SetCustomItemAsync(ctx, ctx.ServiceToken, ctx.ProjectId, ReportsId, new SetItemBody(report.id, Content.Serialize(report)));
            return new RankReply { ok = true, board = board ?? "" };
        }

        /// <summary>Admin (service account): the name reports, oldest first, a page at a time (after = the last key seen).</summary>
        [CloudCodeFunction("ListReports")]
        public async Task<ReportList> ListReports(IExecutionContext ctx, string after)
        {
            if (!Admin(ctx)) return new ReportList { error = "admin" };
            var list = new ReportList { ok = true };
            var res = await _api.CloudSaveData.GetCustomItemsAsync(ctx, ctx.ServiceToken, ctx.ProjectId, ReportsId, null, string.IsNullOrEmpty(after) ? null : after);
            foreach (var item in res.Data.Results)
            {
                var r = Content.Parse<NameReport>(item.Value);
                if (string.IsNullOrEmpty(r.id)) r.id = item.Key;
                list.reports.Add(r);
                list.next = item.Key;
            }
            return list;
        }

        /// <summary>
        /// Admin (service account): resets a reported player's name. The service name is changed by the player's own
        /// game on its next start (it sees reset in NameState, shows "Player" and asks for a new name).
        /// </summary>
        [CloudCodeFunction("ResetName")]
        public async Task<NameReply> ResetName(IExecutionContext ctx, string player)
        {
            if (!Admin(ctx)) return new NameReply { error = "admin" };
            if (string.IsNullOrEmpty(player)) return new NameReply { error = "args" };
            var rec = await ReadName(ctx, player) ?? new NameRecord { free = 1 };
            rec.reset = true;
            rec.name = NameRules.Fallback;
            rec.at = Now;
            await WriteProtectedFor(ctx, player, "name", Content.Serialize(rec));
            var reply = State(rec);
            reply.ok = true;
            return reply;
        }

        /// <summary>Admin (service account): real players and test devices checked in so far today (Asia/Saigon).</summary>
        [CloudCodeFunction("PlayersToday")]
        public async Task<TodayReply> PlayersToday(IExecutionContext ctx)
        {
            if (!Admin(ctx)) return new TodayReply { error = "admin" };
            return new TodayReply { ok = true, day = SaigonDay, players = await Total(ctx, DailyBoard), testers = await Total(ctx, TestDailyBoard) };
        }

        async Task<int> Total(IExecutionContext ctx, string board)
        {
            try
            {
                var top = await _api.Leaderboards.GetLeaderboardScoresAsync(ctx, ctx.ServiceToken, new Guid(ctx.ProjectId), board, offset: 0, limit: 1);
                return top.Data.Total;
            }
            catch (Exception) { return 0; }
        }

        async Task<NameRecord> ReadName(IExecutionContext ctx, string player)
        {
            try
            {
                var res = await _api.CloudSaveData.GetProtectedItemsAsync(ctx, ctx.ServiceToken, ctx.ProjectId, player, new List<string> { "name" });
                var v = res.Data.Results.FirstOrDefault(i => i.Key == "name")?.Value;
                return v == null ? null : Content.Parse<NameRecord>(v);
            }
            catch (Exception) { return null; }
        }

        async Task WriteProtectedFor(IExecutionContext ctx, string player, string key, string value) =>
            await _api.CloudSaveData.SetProtectedItemAsync(ctx, ctx.ServiceToken, ctx.ProjectId, player, new SetItemBody(key, value));

        static string Clip(string s, int n) => string.IsNullOrEmpty(s) ? "" : s.Length > n ? s.Substring(0, n) : s;
    }
}
