using System;
using System.Collections.Generic;
using System.IO;
using CircuitCube.Core;
using UnityEngine;
#if CC_UGS
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
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
        public long tenths;     // shape boards: total of their best times
    }

    /// <summary>
    /// Leaderboards on Unity Gaming Services: the weekly challenge (5 boards for its levels and one for their total)
    /// and one board per shape (Expert levels and the total of their best times). The game never writes a score
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
            public string key;      // week id or group id
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

        /// <summary>A win was ranked: the board and the server's answer (for the result card).</summary>
        public static event Action<RankReply> Posted;

        static bool _started, _signingIn, _sending;
        static float _nextTry;
        static PendingFile _pending;

        static string PendingPath => Path.Combine(Application.persistentDataPath, "save", "ranks_pending.json");

        public static void Init()
        {
            if (_started) return;
            _started = true;
            _pending = LoadPending();
        }

        /// <summary>Called every frame: signs in once Play Games has answered, then sends waiting wins.</summary>
        public static void Tick()
        {
#if CC_UGS
            if (!_started || Time.realtimeSinceStartup < _nextTry) return;
            if (!SignedIn) { if (!_signingIn && CloudSave.AuthDone) SignIn(); return; }
            if (!_sending && _pending.runs.Count > 0) SendNext();
#endif
        }

        /// <summary>A weekly challenge level won without hints or undos.</summary>
        public static void SubmitWeekly(string week, int slot, List<int> taps, float seconds) =>
            Queue(new PendingRun { kind = "weekly", key = week, index = slot, taps = new List<int>(taps), tenths = RankScore.Tenths(seconds) });

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

        /// <summary>Changes the name shown on the boards (the service adds a #1234 tag).</summary>
        public static void Rename(string name, Action<bool> done)
        {
#if CC_UGS
            if (!SignedIn || string.IsNullOrWhiteSpace(name)) { done?.Invoke(false); return; }
            RenameAsync(NameFilter.Clean(name), done);
#else
            done?.Invoke(false);
#endif
        }

#if CC_UGS
        static async void SignIn()
        {
            _signingIn = true;
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
                var auth = AuthenticationService.Instance;
                if (!auth.IsSignedIn)
                {
                    string code = await PlayGamesCode();
                    if (!string.IsNullOrEmpty(code)) await auth.SignInWithGooglePlayGamesAsync(code);
                    else await auth.SignInAnonymouslyAsync();
                }
                SignedIn = auth.IsSignedIn;
                PlayerName = await auth.GetPlayerNameAsync() ?? "";
                GameAnalytics.Log("ranks_signin", ("ok", SignedIn));
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
                    page.Top.Add(new BoardRow { Rank = e.Rank + 1, Name = e.PlayerName, Score = e.Score, Me = e.PlayerId == me });
                try
                {
                    var mine = await lb.GetPlayerScoreAsync(board);
                    page.Mine = new BoardRow { Rank = mine.Rank + 1, Name = mine.PlayerName, Score = mine.Score, Me = true };
                }
                catch (Exception) { page.Mine = null; }   // no score on this board yet
                page.Ok = true;
            }
            catch (Exception e) { Debug.LogWarning("[Ranks] board " + board + ": " + e.Message); }
            done?.Invoke(page);
        }

        static async void RenameAsync(string name, Action<bool> done)
        {
            bool ok = false;
            try { PlayerName = await AuthenticationService.Instance.UpdatePlayerNameAsync(name); ok = true; }
            catch (Exception e) { Debug.LogWarning("[Ranks] rename: " + e.Message); }
            done?.Invoke(ok);
        }
#endif
    }

    /// <summary>Player names for the boards: letters, digits and _ only, at most 20, with a short list of words blocked.</summary>
    public static class NameFilter
    {
        // anywhere in the name
        static readonly string[] BlockedParts = { "fuck", "shit", "bitch", "cunt", "nigg", "porn" };
        // only as a whole word (short Vietnamese words also sit inside harmless names)
        static readonly string[] BlockedWords = { "dick", "fag", "sex", "dit", "lon", "cac", "buoi", "dm", "dmm", "vcl", "vl", "dcm", "clm", "cl" };

        public static string Clean(string name)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in (name ?? "").Trim())
            {
                if (sb.Length >= 20) break;
                if (char.IsLetterOrDigit(c) || c == '_') sb.Append(c);
                else if (c == ' ' && sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
            }
            string s = sb.ToString().Trim('_');
            string folded = Fold(s);
            foreach (var w in BlockedParts) if (folded.Contains(w)) return "Player";
            foreach (var part in folded.Split('_'))
                foreach (var w in BlockedWords) if (part == w) return "Player";
            return s.Length == 0 ? "Player" : s;
        }

        /// <summary>Lower case without Vietnamese marks, so "Đ" and "đ" read as "d".</summary>
        static string Fold(string s)
        {
            var d = s.ToLowerInvariant().Replace('đ', 'd').Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder();
            foreach (char c in d)
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(c);
            return sb.ToString();
        }
    }
}
