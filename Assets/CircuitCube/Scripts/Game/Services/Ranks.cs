using System;
using System.Collections.Generic;
using System.IO;
using CircuitCube.Core;
using Studio.Tracking;
using UnityEngine;
#if CC_UGS
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Leaderboards;
#endif
#if CC_UGS && CC_PLAYGAMES && UNITY_ANDROID && !UNITY_EDITOR
using GooglePlayGames;
#endif

namespace CircuitCube.Game
{
    /// <summary>One row of a leaderboard as the game shows it.</summary>
    public class BoardRow
    {
        public int Rank;        // 1 = first
        public string Name;
        public string PlayerId; // for reporting a name
        public double Score;
        public bool Me;
    }

    /// <summary>The top of a leaderboard and the player's own row (null when they have no score there).</summary>
    public class BoardPage
    {
        public bool Ok;
        public string Board;
        public int Players;
        public List<BoardRow> Top = new List<BoardRow>();
        public BoardRow Mine;
    }

    /// <summary>What the server answered for a posted win.</summary>
    [Serializable] public class RankReply
    {
        public bool ok;
        public string error = "";
        public string board = "";
        public double score;
        public int rank;        // 1 = first, 0 = unknown
        public int players;
        public int experts;     // shape boards: Expert levels counted now
        public long moves;      // shape boards: total moves of their best runs
        public long tenths;     // shape boards: total of their best times
    }

    /// <summary>What the server answered about the player's name (Cloud Code ClaimName / NameState).</summary>
    [Serializable] public class NameReply
    {
        public bool ok;
        public string error = "";   // name, reserved, empty, no_renames, offline
        public string name = "";
        public int renamesLeft;
        public bool named;
        public bool reset;
    }

    /// <summary>
    /// Leaderboards on Unity Gaming Services: the weekly challenge (per shape, a board for each level of its set and one
    /// for the set total)
    /// and one board per shape (Expert levels, then the total moves and total time of their best runs). The game never writes a score
    /// itself: it sends the taps of a win to the Cloud Code module CircuitCubeRanks (Tools/CloudCode), which plays
    /// them again and posts the score. Wins made offline wait in save/ranks_pending.json and are sent later.
    /// Signs in with the Play Games account once <see cref="CloudSave"/> has, otherwise anonymously.
    /// Built only with the com.unity.services.cloudcode, leaderboards and authentication packages (define CC_UGS);
    /// otherwise every call does nothing and the boards are simply not shown.
    /// </summary>
    public static class Ranks
    {
        public const string Module = "CircuitCubeRanks";
        const float RetrySeconds = 30f;

        [Serializable] class PendingRun
        {
            public string kind;     // "weekly" or "shape"
            public string key;      // "<week id>/<group id>" or group id
            public int index;       // weekly slot or level serial
            public List<int> taps = new List<int>();
            public int tenths;
        }

        [Serializable] class PendingFile { public List<PendingRun> runs = new List<PendingRun>(); }

        public static bool Available
        {
            get
            {
#if CC_UGS
                return true;
#else
                return false;
#endif
            }
        }

        public static bool SignedIn { get; private set; }
        public static string PlayerName { get; private set; } = "";

        /// <summary>The services environment this build signs in to: production only for Google Play installs.</summary>
        public static string Environment => BuildChannel.ServicesEnvironment;

        /// <summary>The name state from the server once signed in (null before, or offline).</summary>
        public static NameReply Name { get; private set; }

        /// <summary>Raised when <see cref="Name"/> arrives or changes.</summary>
        public static event Action NameChanged;

        /// <summary>A win was ranked: the board and the server's answer (for the result card).</summary>
        public static event Action<RankReply> Posted;

        static bool _started, _signingIn, _sending, _checkingIn;
        static DateTime _checkedDay;   // Asia/Saigon date of the last check-in
        static float _nextTry;
        static PendingFile _pending;

        static string PendingPath => Path.Combine(Application.persistentDataPath, "save", "ranks_pending.json");

        public static void Init()
        {
            if (_started) return;
            _started = true;
            _pending = LoadPending();
        }

        /// <summary>Called every frame: signs in once Play Games has answered, checks in for today, then sends waiting wins.</summary>
        public static void Tick()
        {
#if CC_UGS
            if (!_started || Time.realtimeSinceStartup < _nextTry) return;
            if (!SignedIn) { if (!_signingIn && CloudSave.AuthDone) SignIn(); return; }
            if (!_checkingIn && _checkedDay != SaigonToday) CheckIn();
            if (!_sending && _pending.runs.Count > 0) SendNext();
#endif
        }

        /// <summary>A level of a shape's weekly challenge set won without hints or undos.</summary>
        public static void SubmitWeekly(string week, string group, int slot, List<int> taps, float seconds) =>
            Queue(new PendingRun { kind = "weekly", key = RankScore.WeeklyKey(week, group), index = slot, taps = new List<int>(taps), tenths = RankScore.Tenths(seconds) });

        /// <summary>A level of a shape won as Expert (no hint, no undo).</summary>
        public static void SubmitShape(string group, int serial, List<int> taps, float seconds) =>
            Queue(new PendingRun { kind = "shape", key = group, index = serial, taps = new List<int>(taps), tenths = RankScore.Tenths(seconds) });

        static void Queue(PendingRun run)
        {
            if (!Available || run.taps.Count == 0) return;
            if (_pending == null) _pending = LoadPending();
            _pending.runs.Add(run);
            SavePending();
            _nextTry = 0f;
        }

        static PendingFile LoadPending()
        {
            try { if (File.Exists(PendingPath)) return JsonUtility.FromJson<PendingFile>(File.ReadAllText(PendingPath)) ?? new PendingFile(); }
            catch (Exception e) { Debug.LogWarning("[Ranks] pending unreadable: " + e.Message); }
            return new PendingFile();
        }

        static void SavePending()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PendingPath));
                File.WriteAllText(PendingPath, JsonUtility.ToJson(_pending));
            }
            catch (Exception e) { Debug.LogWarning("[Ranks] pending not saved: " + e.Message); }
        }

        /// <summary>The top <paramref name="limit"/> rows of a board and the player's own row.</summary>
        public static void LoadBoard(string board, int limit, Action<BoardPage> done)
        {
#if CC_UGS
            if (!SignedIn) { done?.Invoke(new BoardPage { Board = board }); return; }
            LoadBoardAsync(board, limit, done);
#else
            done?.Invoke(new BoardPage { Board = board });
#endif
        }

        /// <summary>
        /// Changes the name shown on the boards (the service adds a #1234 tag). The server checks the name and the
        /// renames left first (Cloud Code ClaimName); first = the name sheet right after the tutorial.
        /// </summary>
        public static void Rename(string name, bool first, Action<NameReply> done)
        {
#if CC_UGS
            if (!SignedIn) { done?.Invoke(new NameReply { error = "offline" }); return; }
            RenameAsync(name, first, done);
#else
            done?.Invoke(new NameReply { error = "offline" });
#endif
        }

        /// <summary>Reports another player's name on a board; the team reads reports in Level Lab (Players).</summary>
        public static void ReportName(string playerId, string name, string board, Action<bool> done)
        {
#if CC_UGS
            if (!SignedIn || string.IsNullOrEmpty(playerId)) { done?.Invoke(false); return; }
            ReportAsync(playerId, name, board, done);
#else
            done?.Invoke(false);
#endif
        }

        /// <summary>The player's own id on the services ("" before sign-in).</summary>
        public static string PlayerId
        {
            get
            {
#if CC_UGS
                return SignedIn ? AuthenticationService.Instance.PlayerId ?? "" : "";
#else
                return "";
#endif
            }
        }

#if CC_UGS
        static async void SignIn()
        {
            _signingIn = true;
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    // only Google Play installs use production; the Editor and every test build use development
                    var options = new InitializationOptions();
                    options.SetEnvironmentName(Environment);
                    await UnityServices.InitializeAsync(options);
                }
                var auth = AuthenticationService.Instance;
                if (!auth.IsSignedIn)
                {
                    string code = await PlayGamesCode();
                    bool done = false;
                    if (!string.IsNullOrEmpty(code))
                    {
                        try { await auth.SignInWithGooglePlayGamesAsync(code); done = true; }
                        catch (Exception e) { Debug.LogWarning("[Ranks] Play Games sign-in refused, anonymous instead: " + e.Message); }
                    }
                    if (!done) await auth.SignInAnonymouslyAsync();
                }
                SignedIn = auth.IsSignedIn;
                PlayerName = await auth.GetPlayerNameAsync() ?? "";
                GameAnalytics.Log("ranks_signin", ("ok", SignedIn), ("env", Environment));
                if (SignedIn) LoadNameState();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ranks] sign-in: " + e.Message);
                _nextTry = Time.realtimeSinceStartup + RetrySeconds;
            }
            _signingIn = false;
        }

        /// <summary>A one-time code from Play Games for the server sign-in; null without Play Games.</summary>
        static Task<string> PlayGamesCode()
        {
            var tcs = new TaskCompletionSource<string>();
#if CC_PLAYGAMES && UNITY_ANDROID && !UNITY_EDITOR
            if (CloudSave.SignedIn) PlayGamesPlatform.Instance.RequestServerSideAccess(false, code => tcs.TrySetResult(code));
            else tcs.TrySetResult(null);
#else
            tcs.TrySetResult(null);
#endif
            return tcs.Task;
        }

        /// <summary>Today's date in Asia/Saigon (UTC+7, no daylight saving): the day of the daily_players board.</summary>
        static DateTime SaigonToday => DateTime.UtcNow.AddHours(7).Date;

        /// <summary>Counts this player once on today's unique-players board (daily_players): at launch, and again
        /// if the game is still open after midnight.</summary>
        static async void CheckIn()
        {
            _checkingIn = true;
            var day = SaigonToday;
            try
            {
                var args = new Dictionary<string, object> { { "channel", BuildChannel.Current }, { "version", Application.version } };
                var reply = await CloudCodeService.Instance.CallModuleEndpointAsync<RankReply>(Module, "DailyCheckIn", args);
                _checkedDay = day;
                if (reply != null) Debug.Log("[Ranks] players today: " + reply.players);
            }
            catch (Exception e)
            {
                _nextTry = Time.realtimeSinceStartup + RetrySeconds;
                Debug.LogWarning("[Ranks] check-in: " + e.Message);
            }
            _checkingIn = false;
        }

        static async void SendNext()
        {
            _sending = true;
            var run = _pending.runs[0];
            bool drop = false;
            try
            {
                var args = new Dictionary<string, object> { { "key", run.key }, { "index", run.index }, { "taps", run.taps }, { "tenths", run.tenths } };
                string fn = run.kind == "weekly" ? "SubmitWeekly" : "SubmitShape";
                var reply = await CloudCodeService.Instance.CallModuleEndpointAsync<RankReply>(Module, fn, args);
                drop = true;   // ranked or refused: either way it is not sent again
                if (reply != null && !reply.ok) Debug.LogWarning("[Ranks] " + fn + " refused: " + reply.error);
                if (reply != null) Posted?.Invoke(reply);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ranks] send: " + e.Message);
                _nextTry = Time.realtimeSinceStartup + RetrySeconds;
            }
            if (drop && _pending.runs.Count > 0 && _pending.runs[0] == run) { _pending.runs.RemoveAt(0); SavePending(); }
            _sending = false;
        }

        static async void LoadBoardAsync(string board, int limit, Action<BoardPage> done)
        {
            var page = new BoardPage { Board = board };
            try
            {
                var lb = LeaderboardsService.Instance;
                var top = await lb.GetScoresAsync(board, new Unity.Services.Leaderboards.GetScoresOptions { Limit = limit });
                page.Players = top.Total;
                string me = AuthenticationService.Instance.PlayerId;
                foreach (var e in top.Results)
                    page.Top.Add(new BoardRow { Rank = e.Rank + 1, Name = e.PlayerName, PlayerId = e.PlayerId, Score = e.Score, Me = e.PlayerId == me });
                try
                {
                    var mine = await lb.GetPlayerScoreAsync(board);
                    page.Mine = new BoardRow { Rank = mine.Rank + 1, Name = mine.PlayerName, PlayerId = mine.PlayerId, Score = mine.Score, Me = true };
                }
                catch (Exception) { page.Mine = null; }   // no score on this board yet
                page.Ok = true;
            }
            catch (Exception e) { Debug.LogWarning("[Ranks] board " + board + ": " + e.Message); }
            done?.Invoke(page);
        }

        static async void RenameAsync(string name, bool first, Action<NameReply> done)
        {
            NameReply reply;
            try
            {
                var args = new Dictionary<string, object> { { "name", name ?? "" }, { "first", first } };
                reply = await CloudCodeService.Instance.CallModuleEndpointAsync<NameReply>(Module, "ClaimName", args) ?? new NameReply { error = "offline" };
                if (reply.ok)
                {
                    PlayerName = await AuthenticationService.Instance.UpdatePlayerNameAsync(reply.name);
                    Name = reply;
                    NameChanged?.Invoke();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ranks] rename: " + e.Message);
                reply = new NameReply { error = "offline" };
            }
            done?.Invoke(reply);
        }

        /// <summary>Reads what the player may do with their name. After a reset by the team, the game shows the
        /// default name at once and asks for a new one (<see cref="NameChanged"/>).</summary>
        static async void LoadNameState()
        {
            try
            {
                var reply = await CloudCodeService.Instance.CallModuleEndpointAsync<NameReply>(Module, "NameState", new Dictionary<string, object>());
                if (reply == null || !reply.ok) return;
                if (reply.reset && !PlayerName.StartsWith(NameRules.Fallback + "#", StringComparison.Ordinal))
                    PlayerName = await AuthenticationService.Instance.UpdatePlayerNameAsync(NameRules.Fallback);
                Name = reply;
                NameChanged?.Invoke();
            }
            catch (Exception e) { Debug.LogWarning("[Ranks] name state: " + e.Message); }
        }

        static async void ReportAsync(string playerId, string name, string board, Action<bool> done)
        {
            bool ok = false;
            try
            {
                var args = new Dictionary<string, object> { { "player", playerId }, { "name", name ?? "" }, { "board", board ?? "" } };
                var reply = await CloudCodeService.Instance.CallModuleEndpointAsync<RankReply>(Module, "ReportName", args);
                ok = reply != null && reply.ok;
                if (reply != null && !reply.ok) Debug.LogWarning("[Ranks] report refused: " + reply.error);
            }
            catch (Exception e) { Debug.LogWarning("[Ranks] report: " + e.Message); }
            done?.Invoke(ok);
        }
#endif
    }

    /// <summary>The name rules of the game (Resources/NameRules.txt, the same file the server checks with).</summary>
    public static class NameFilter
    {
        static NameRules _rules;

        public static NameRules Rules
        {
            get
            {
                if (_rules == null)
                {
                    var file = Resources.Load<TextAsset>("NameRules");
                    if (file == null) Debug.LogWarning("[Names] Resources/NameRules.txt is missing: names are not checked");
                    _rules = NameRules.Parse(file != null ? file.text : "");
                }
                return _rules;
            }
        }

        public static string Clean(string name) => Rules.Clean(name);
        public static NameVerdict Check(string name, out string clean) => Rules.Check(name, out clean);
    }
}
