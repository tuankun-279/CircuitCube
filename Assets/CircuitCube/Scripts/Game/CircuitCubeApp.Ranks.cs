using System;
using System.Collections.Generic;
using CircuitCube.Core;
using CircuitCube.UI;
using UnityEngine;

namespace CircuitCube.Game
{
    /// <summary>
    /// Leaderboard screens (approved mockup ArtRef/mockup/leaderboard): the Ranks pill and the Weekly Challenge card on
    /// Home, the Weekly page, the Leaderboards card (This week / All-time), weekly play with its result card, and the
    /// name sheet. Boards come from <see cref="Ranks"/>; with the debug fill on (F4) they come from
    /// <see cref="RanksDebug"/>. Screens and rows are prefabs (Build 1.5.0 Ranks UI); this only fills them in.
    /// </summary>
    public sealed partial class CircuitCubeApp
    {
        const int BoardRows = 50;
        const float PageFresh = 60f;
        const string WeekSeenKey = "cc_week_seen";

        int _weeklySlot;               // the weekly level in play (1..3), 0 = a normal level
        WeeklyClockEntry _weeklyClock; // its play time over all attempts; _playSeconds continues from it
        float _weeklyClockSaved;       // the time last written to disk
        WeeklyClocks _sampleClocks;    // clocks of the debug sample week (F4), never saved
        const float WeeklyClockSaveEvery = 5f;
        int _weekGroup = -1;           // shape shown on the Weekly page and the weekly boards
        int _boardGroup = -1;          // shape shown on the All-time board
        bool _boardsAllTime;
        int _boardTab = 2;             // 0 = Total, 1..3 = level
        int _boardToken;               // a page that arrives for an older pick is dropped
        int _weekToken;
        int _resultMoves, _resultTenths, _resultWasMoves = -1, _resultWasTenths;
        bool _resultBest;              // the win just made beat an earlier best on this phone
        int _resultOldRank;
        string _mutedHex, _youHex;
        GameTheme _hexTheme;

        readonly Dictionary<string, BoardPage> _pages = new Dictionary<string, BoardPage>();
        readonly Dictionary<string, float> _pageTimes = new Dictionary<string, float>();

        static readonly string[] SlotNames = { "Easy", "Medium", "Hard" };
        static readonly string[] TabNames = { "Total", "L1", "L2", "L3" };

        void WireRanks()
        {
            var h = UI.Home;
            if (h.RanksButton != null) h.RanksButton.onClick.AddListener(() => OpenBoards(true, CubeSave.CurrentGroup));
            if (h.WeeklyButton != null) h.WeeklyButton.onClick.AddListener(() => ShowWeekly(WeekGroupFor(CubeSave.CurrentGroup)));
            Ranks.Posted += OnRankPosted;

            var w = UI.Weekly;
            if (w != null)
            {
                w.BackButton.onClick.AddListener(Back);
                w.BoardButton.onClick.AddListener(() => OpenBoards(false, _weekGroup));
                for (int i = 0; i < w.Rows.Length; i++)
                {
                    int slot = i + 1;
                    w.Rows[i].Play.onClick.AddListener(() => StartWeekly(_weekGroup, slot));
                    w.Rows[i].Button.onClick.AddListener(() => StartWeekly(_weekGroup, slot));
                }
                WireDock(w.Dock, g => { if (WeekOpenFor(g)) ShowWeekly(g); });
            }

            var b = UI.Boards;
            if (b != null)
            {
                b.Close.onClick.AddListener(CloseBoards);
                b.WeekTab.onClick.AddListener(() => { _boardsAllTime = false; FillBoards(); });
                b.AllTab.onClick.AddListener(() => { _boardsAllTime = true; FillBoards(); });
                for (int i = 0; i < b.LevelTabs.Length; i++)
                {
                    int tab = i;
                    b.LevelTabs[i].onClick.AddListener(() => { _boardTab = tab; FillBoards(); });
                }
                if (b.Edit != null) b.Edit.onClick.AddListener(OpenNameSheet);
                WireDock(b.Dock, g =>
                {
                    if (_boardsAllTime) _boardGroup = g;
                    else if (WeekOpenFor(g)) _weekGroup = g;
                    else return;
                    FillBoards();
                });
            }

            var r = UI.WeeklyResult;
            if (r != null)
            {
                r.Next.onClick.AddListener(() =>
                {
                    int next = NextWeeklySlot(_weekGroup);
                    if (next > 0) StartWeekly(_weekGroup, next); else ShowWeekly(_weekGroup);
                });
                r.TryAgain.onClick.AddListener(() => StartWeekly(_weekGroup, _weeklySlot));
                r.AllLevels.onClick.AddListener(() => ShowWeekly(_weekGroup));
                r.RankBox.onClick.AddListener(() => { _boardTab = _weeklySlot; OpenBoards(false, _weekGroup); });
            }

            var n = UI.NameSheet;
            if (n != null)
            {
                n.NotNow.onClick.AddListener(CloseNameSheet);
                n.Save.onClick.AddListener(SaveName);
            }
            WireNames();
        }

        void WireDock(ShapeDock dock, Action<int> pick)
        {
            if (dock == null) return;
            for (int i = 0; i < dock.Items.Length; i++)
            {
                int g = i;
                dock.Items[i].Button.onClick.AddListener(() => pick(g));
            }
        }

        // ---------------- The week ----------------

        string WeekId => RanksDebug.On ? RanksDebug.WeekId : GameData.Challenge.id;
        int WeekCount => RanksDebug.On ? 3 : Mathf.Clamp(GameData.Challenge.count, 1, 3);

        /// <summary>A weekly challenge is open now (or the debug sample is on).</summary>
        bool WeekOpen()
        {
            if (RanksDebug.On) return true;
            if (!GameData.ChallengeWindow(out var opens, out var closes)) return false;
            var now = DateTime.UtcNow;
            return now >= opens && now < closes;
        }

        bool WeekWindow(out DateTime opens, out DateTime closes)
        {
            if (RanksDebug.On) { RanksDebug.Window(out opens, out closes); return true; }
            return GameData.ChallengeWindow(out opens, out closes);
        }

        /// <summary>The shape has a set this week and the player has unlocked it.</summary>
        bool WeekOpenFor(int g) => g >= 0 && g < GameData.GroupCount && CubeSave.GroupOpen(g)
                                   && (RanksDebug.On || GameData.Challenge.Has(GameData.GroupId(g)));

        /// <summary><paramref name="g"/> if it is in this week's challenge, else the first shape that is.</summary>
        int WeekGroupFor(int g)
        {
            if (WeekOpenFor(g)) return g;
            for (int i = 0; i < GameData.GroupCount; i++) if (WeekOpenFor(i)) return i;
            return Mathf.Max(0, g);
        }

        string Countdown()
        {
            if (!WeekWindow(out _, out var closes)) return "–";
            return RankText.Countdown(closes - DateTime.UtcNow);
        }

        static string BestKey(string week, string group, int slot) => "cc_wk_" + week + "_" + group + "_" + slot;

        /// <summary>The player's best run of a weekly level on this phone (moves, then tenths of a second).</summary>
        bool WeeklyBest(int g, int slot, out int moves, out int tenths)
        {
            moves = tenths = 0;
            if (RanksDebug.On && g == _weekGroup && slot <= 2 && !PlayerPrefs.HasKey(BestKey(WeekId, GameData.GroupId(g), slot)))
                return RanksDebug.Best(slot, out moves, out tenths);
            string s = PlayerPrefs.GetString(BestKey(WeekId, GameData.GroupId(g), slot), "");
            int comma = s.IndexOf(',');
            return comma > 0 && int.TryParse(s.Substring(0, comma), out moves) && int.TryParse(s.Substring(comma + 1), out tenths);
        }

        int WeeklySolved(int g)
        {
            int n = 0;
            for (int slot = 1; slot <= WeekCount; slot++) if (WeeklyBest(g, slot, out _, out _)) n++;
            return n;
        }

        int NextWeeklySlot(int g)
        {
            for (int slot = 1; slot <= WeekCount; slot++) if (!WeeklyBest(g, slot, out _, out _)) return slot;
            return 0;
        }

        // ---------------- Boards ----------------

        /// <summary>A board page: from the cache when fresh, else loaded (or the debug sample).</summary>
        void Page(string board, int g, Action<BoardPage> done)
        {
            if (RanksDebug.On) { done(RanksDebug.Page(board, g, Mathf.Max(1, GameData.Group(g).levelCount))); return; }
            if (_pages.TryGetValue(board, out var cached) && cached.Ok && Time.realtimeSinceStartup - _pageTimes[board] < PageFresh) { done(cached); return; }
            Ranks.LoadBoard(board, BoardRows, p =>
            {
                if (p != null && p.Ok) { _pages[board] = p; _pageTimes[board] = Time.realtimeSinceStartup; }
                done(p ?? new BoardPage { Board = board });
            });
        }

        bool Cached(string board, out BoardPage page)
        {
            if (RanksDebug.On) { page = RanksDebug.Page(board, _weekGroup, 100); return true; }
            return _pages.TryGetValue(board, out page) && page.Ok;
        }

        void ThemeHex()
        {
            if (_hexTheme == Themes.Current && _mutedHex != null) return;
            _hexTheme = Themes.Current;
            var t = Themes.Current;
            _mutedHex = t != null ? "#" + ColorUtility.ToHtmlStringRGB(t.Get(UiRole.TextMuted)) : "#6F7392";
            _youHex = t != null ? "#" + ColorUtility.ToHtmlStringRGB(t.Get(UiRole.Primary)) : "#8FB8C2";
        }

        string NameWithTag(string full, bool me)
        {
            ThemeHex();
            RankText.SplitTag(full, out var name, out var tag);
            string you = me ? Loc.T("You") + " · " : "";
            return you + name + (tag.Length > 0 ? "<color=" + (me ? _youHex : _mutedHex) + ">" + tag + "</color>" : "");
        }

        static void Role(ThemeRole r, UiRole role)
        {
            if (r == null || r.Role == role) return;
            r.Role = role;
            r.Apply();
        }

        void FillBoardRow(BoardRowView v, BoardRow row, bool allTime, bool pinned)
        {
            int rank = row != null ? row.Rank : 0;
            bool me = pinned || (row != null && row.Me);
            int medal = !pinned && rank >= 1 && rank <= 3 ? rank : 0;
            v.Medal.gameObject.SetActive(medal > 0);
            if (medal > 0) Role(v.Medal, medal == 1 ? UiRole.Star : medal == 2 ? UiRole.Silver : UiRole.Bronze);
            Role(v.RankRole, pinned ? UiRole.Primary : medal > 0 ? UiRole.OnStar : UiRole.TextMuted);
            Role(v.Back, me ? UiRole.Highlight : medal > 0 ? UiRole.Row : UiRole.Transparent);
            v.Frame.SetActive(me);
            v.Rank.text = rank <= 0 ? "–" : pinned ? "#" + rank : rank.ToString();
            string name = row != null ? NameWithTag(row.Name, me) : NameWithTag(Ranks.PlayerName.Length > 0 ? Ranks.PlayerName : RanksDebug.On ? RanksDebug.MyName : Loc.T("Player"), true);
            if (allTime) v.AllName.text = name; else v.WeekName.text = name;
            if (!pinned) KeepRow(v, row, me);
            v.WeekColumns.SetActive(!allTime);
            v.AllColumns.SetActive(allTime);
            if (allTime)
            {
                double s = row != null ? row.Score : 0;
                v.AllExpert.text = row != null ? RankScore.ShapeExperts(s).ToString() : "–";
                v.AllMoves.text = row != null ? RankText.Count(RankScore.ShapeMoves(s)) : "–";
                v.AllTime.text = row != null ? RankText.ShapeTime(RankScore.ShapeSeconds(s)) : "–";
            }
            else
            {
                v.WeekMoves.text = row != null ? RankScore.WeeklyMoves(row.Score).ToString() : "–";
                v.WeekTime.text = row != null ? RankScore.TimeText(RankScore.WeeklyTenths(row.Score)) : _boardTab == 0 ? Loc.T("solve all 3") : Loc.T("not solved");
            }
        }

        void OpenBoards(bool allTime, int g)
        {
            var b = UI.Boards;
            if (b == null) return;
            _boardsAllTime = allTime || !WeekOpen();
            _boardGroup = Mathf.Clamp(g, 0, GameData.GroupCount - 1);
            _weekGroup = WeekGroupFor(g);
            if (_boardTab < 0 || _boardTab > WeekCount) _boardTab = Mathf.Min(2, WeekCount);
            GameAudio.Play(Sfx.PanelOpen);
            UI.Dim.SetActive(true);
            b.Open();
            b.transform.SetAsLastSibling();
            FillBoards();
            GameAnalytics.Log("ranks_open", ("tab", _boardsAllTime ? "all" : "week"));
        }

        void CloseBoards()
        {
            if (UI.Boards == null || !UI.Boards.gameObject.activeSelf) return;
            GameAudio.Play(Sfx.PanelClose);
            UI.Boards.gameObject.SetActive(false);
            if (!UI.Result.gameObject.activeSelf && !UI.Tip.gameObject.activeSelf && !UI.Settings.gameObject.activeSelf
                && (UI.WeeklyResult == null || !UI.WeeklyResult.gameObject.activeSelf)) UI.Dim.SetActive(false);
        }

        void FillBoards()
        {
            var b = UI.Boards;
            bool week = !_boardsAllTime;
            bool weekOpen = WeekOpen();
            Role(b.WeekTabBack, week ? UiRole.Segment : UiRole.Transparent);
            Role(b.AllTabBack, week ? UiRole.Transparent : UiRole.Segment);
            Role(b.WeekTabLabel, week ? UiRole.Text : UiRole.TextMuted);
            Role(b.AllTabLabel, week ? UiRole.TextMuted : UiRole.Text);
            b.WeekRoot.SetActive(week);
            b.AllRoot.SetActive(!week);
            for (int i = 0; i < b.LevelTabs.Length; i++)
            {
                bool on = i == _boardTab;
                b.LevelTabs[i].gameObject.SetActive(i <= WeekCount);
                Role(b.LevelTabBacks[i], on ? UiRole.Highlight : UiRole.Card);
                Role(b.LevelTabLabels[i], on ? UiRole.Text : UiRole.TextMuted);
                b.LevelTabFrames[i].SetActive(on);
            }
            b.Countdown.text = Countdown();
            FillDock(b.Dock, week ? _weekGroup : _boardGroup, !week);

            int g = week ? _weekGroup : _boardGroup;
            string shape = Loc.T(GameData.Group(g).name);
            if (!week)
            {
                b.SummaryTitle.text = Loc.F("YOUR {0}", Loc.Upper(shape));
                b.SummaryLine.text = Loc.T("Loading...");
                b.SummaryRank.text = "–";
                b.SummaryOf.text = "";
            }
            HideRest(b.WeekList, 0);
            HideRest(b.AllList, 0);
            if (week && !weekOpen) { ShowBoardMessage(b, Loc.T("No weekly challenge right now. A new set comes every week.")); FillBoardRow(b.You, null, false, true); return; }
            if (!RanksDebug.On && !Ranks.SignedIn) { ShowBoardMessage(b, Loc.T("Can't reach the boards right now. Your scores are saved and post when you're back online.")); FillBoardRow(b.You, null, false, true); if (!week) b.SummaryLine.text = Loc.T("Offline"); return; }
            ShowBoardMessage(b, Loc.T("Loading..."));
            if (week) FillBoardRow(b.You, null, false, true);

            string gid = GameData.GroupId(g);
            string board = week ? (_boardTab == 0 ? RankScore.WeeklyTotalBoard(gid) : RankScore.WeeklyBoard(gid, _boardTab)) : RankScore.ShapeBoard(gid);
            int token = ++_boardToken;
            _reportBoard = board;
            Page(board, g, page =>
            {
                if (token != _boardToken || UI.Boards == null || !UI.Boards.gameObject.activeSelf) return;
                FillPage(UI.Boards, page, week, g);
            });
        }

        void FillPage(LeaderboardCard b, BoardPage page, bool week, int g)
        {
            if (!page.Ok) { ShowBoardMessage(b, Loc.T("Can't reach the boards right now. Your scores are saved and post when you're back online.")); return; }
            int next = 0;
            var list = week ? b.WeekList : b.AllList;
            foreach (var row in page.Top)
            {
                var v = Slot(list, b.RowPrefab, ref next);
                FillBoardRow(v, row, !week, false);
            }
            HideRest(list, next);
            ShowBoardMessage(b, page.Top.Count > 0 ? null : week ? Loc.T("No scores yet. Solve this level to be the first!") : Loc.T("No Expert levels on this board yet."));
            if (week) FillBoardRow(b.You, page.Mine, false, true);
            else
            {
                var mine = page.Mine;
                int levels = Mathf.Max(1, GameData.Group(g).levelCount);
                ThemeHex();
                if (mine != null)
                {
                    double s = mine.Score;
                    b.SummaryLine.text = Loc.F("{0} Expert", RankScore.ShapeExperts(s)) + " <size=72%><color=" + _mutedHex + ">"
                        + Loc.F("of {0} · {1} moves · {2}", levels, RankText.Count(RankScore.ShapeMoves(s)), RankText.ShapeTime(RankScore.ShapeSeconds(s))) + "</color></size>";
                    b.SummaryRank.text = "#" + mine.Rank;
                    b.SummaryOf.text = Loc.F("of {0}", RankText.Count(page.Players));
                }
                else
                {
                    b.SummaryLine.text = Loc.T("No Expert levels yet");
                    b.SummaryRank.text = "–";
                    b.SummaryOf.text = page.Players > 0 ? Loc.F("of {0}", RankText.Count(page.Players)) : "";
                }
            }
            var scroll = week ? b.WeekScroll : b.AllScroll;
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        static void ShowBoardMessage(LeaderboardCard b, string text)
        {
            b.Message.gameObject.SetActive(!string.IsNullOrEmpty(text));
            if (!string.IsNullOrEmpty(text)) b.Message.text = text;
        }

        /// <summary>The shape dock: every group (up to the dock's size), the picked one filled; locked ones faded in the weekly views.</summary>
        void FillDock(ShapeDock dock, int picked, bool allOpen)
        {
            if (dock == null) return;
            for (int i = 0; i < dock.Items.Length; i++)
            {
                var it = dock.Items[i];
                bool has = i < GameData.GroupCount;
                it.gameObject.SetActive(has);
                if (!has) continue;
                var info = GameData.Group(i);
                bool open = allOpen || WeekOpenFor(i);
                bool on = i == picked;
                // the white line icon tinted like the name; a shape without one shows its picture untinted
                var line = dock.IconFor(string.IsNullOrEmpty(info.icon) ? info.id : info.icon);
                if (line != null)
                {
                    it.Icon.sprite = line;
                    Role(it.IconRole, on ? UiRole.OnPrimary : UiRole.TextMuted);
                }
                else
                {
                    var picture = GroupIcon(i);
                    if (picture != null) it.Icon.sprite = picture;
                    Role(it.IconRole, UiRole.None);
                    it.Icon.color = Color.white;
                }
                it.Name.text = Loc.T(info.name);
                Role(it.Back, on ? UiRole.Primary : UiRole.Transparent);
                Role(it.NameRole, on ? UiRole.OnPrimary : UiRole.TextMuted);
                it.Lock.SetActive(!open);
                it.Group.alpha = open ? 1f : 0.45f;
                it.Button.interactable = open;
            }
        }

        // ---------------- Home ----------------

        void FillHomeRanks()
        {
            var h = UI.Home;
            if (h.RanksButton != null) h.RanksButton.gameObject.SetActive(Ranks.Available || RanksDebug.On);
            if (h.WeeklyButton == null) return;
            bool show = WeekOpen() && WeekOpenFor(WeekGroupFor(CubeSave.CurrentGroup));
            h.WeeklyButton.gameObject.SetActive(show);
            if (!show) return;
            int g = WeekGroupFor(CubeSave.CurrentGroup);
            _weekGroup = g;
            int solved = WeeklySolved(g);
            h.WeeklyLine.text = Loc.F("{0} {1} / {2} solved", Loc.T(GameData.Group(g).name), solved, WeekCount) + " · " + Loc.F("ends in {0}", Countdown());
            for (int i = 0; i < h.WeeklySteps.Length; i++)
            {
                h.WeeklySteps[i].gameObject.SetActive(i < WeekCount);
                Role(h.WeeklySteps[i], WeeklyBest(g, i + 1, out _, out _) ? UiRole.Primary : UiRole.Track);
            }
            if (h.WeeklyNew != null) h.WeeklyNew.SetActive(PlayerPrefs.GetString(WeekSeenKey, "") != WeekId);
        }

        // ---------------- Weekly page ----------------

        void ShowWeekly(int g)
        {
            var w = UI.Weekly;
            if (w == null) return;
            _weeklySlot = 0;
            _weekGroup = WeekGroupFor(g);
            _state = State.Weekly;
            _busy = false;
            HideAll();
            GameAudio.Music(MusicTrack.Menu);
            if (_board != null) _board.gameObject.SetActive(false);   // the page is opaque in the mockup; Home builds a new board
            w.Show();
            SlideIn(w);
            PlayerPrefs.SetString(WeekSeenKey, WeekId);
            FillWeekly();
        }

        void FillWeekly()
        {
            var w = UI.Weekly;
            int g = _weekGroup;
            string gid = GameData.GroupId(g), shape = Loc.T(GameData.Group(g).name);
            if (WeekWindow(out var opens, out _))
            {
                var local = opens.ToLocalTime();
                string day = Loc.Vietnamese ? local.Day + "/" + local.Month : local.ToString("MMM d", System.Globalization.CultureInfo.InvariantCulture);
                w.Subtitle.text = Loc.F("Week of {0} · new set every {1} {2}", day, Loc.T(local.DayOfWeek.ToString()), local.ToString("HH:mm"));
            }
            else w.Subtitle.text = Loc.T("No weekly challenge right now");
            w.EndsIn.text = Countdown();
            int count = WeekCount, solved = WeeklySolved(g);
            w.Solved.text = solved + " / " + count;
            w.Footnote.text = Loc.F("Solve all {0} to get on the {1} total board. Locked shapes join the challenge once you unlock them.", count, shape);
            FillDock(w.Dock, g, false);
            FillWeeklyRows(g);

            // ranks of each level and of the total, as they come in
            bool online = RanksDebug.On || Ranks.SignedIn;
            int token = ++_weekToken;
            if (!online) return;
            for (int slot = 0; slot <= count; slot++)
            {
                string board = slot == 0 ? RankScore.WeeklyTotalBoard(gid) : RankScore.WeeklyBoard(gid, slot);
                Page(board, g, _ => { if (token == _weekToken && _state == State.Weekly) FillWeeklyRows(g); });
            }
        }

        void FillWeeklyRows(int g)
        {
            var w = UI.Weekly;
            string gid = GameData.GroupId(g), shape = Loc.T(GameData.Group(g).name);
            int next = NextWeeklySlot(g);
            for (int i = 0; i < w.Rows.Length; i++)
            {
                var r = w.Rows[i];
                int slot = i + 1;
                r.gameObject.SetActive(slot <= WeekCount);
                if (slot > WeekCount) continue;
                bool solved = WeeklyBest(g, slot, out int moves, out int tenths);
                r.Number.text = slot.ToString();
                r.Shape.text = shape;
                r.Level.text = Loc.T(SlotNames[i]);
                Role(r.Badge, solved ? UiRole.Highlight : UiRole.Row);
                Role(r.NumberRole, solved ? UiRole.Primary : UiRole.Text);
                r.Frame.SetActive(slot == next);
                r.Detail.text = solved ? Loc.F("{0} moves", moves) + " · " + RankScore.TimeText(tenths) : Loc.T("Not solved yet");
                r.RankRoot.SetActive(solved);
                r.Play.gameObject.SetActive(!solved);
                if (solved)
                {
                    if (Cached(RankScore.WeeklyBoard(gid, slot), out var page) && page.Mine != null)
                    {
                        r.Rank.text = "#" + page.Mine.Rank;
                        r.Of.text = Loc.F("of {0}", RankText.Count(page.Players));
                    }
                    else { r.Rank.text = "–"; r.Of.text = Ranks.SignedIn || RanksDebug.On ? "" : Loc.T("offline"); }
                }
            }
            bool total = Cached(RankScore.WeeklyTotalBoard(gid), out var tp) && tp.Mine != null && WeeklySolved(g) >= WeekCount;
            w.TotalRank.text = total ? "#" + tp.Mine.Rank : "–";
            Role(w.TotalRankRole, total ? UiRole.Star : UiRole.TextMuted);
        }

        // ---------------- Weekly play ----------------

        string HudTitle() => _weeklySlot > 0
            ? Loc.T("WEEKLY") + "  ·  " + Loc.Upper(Loc.T(GameData.Group(_group).name)) + "  " + _weeklySlot
            : Loc.Upper(Loc.T(GameData.Group(_group).name)) + "  " + _level.Number;

        /// <summary>Play a level of the week: no hints, no undo, the clock and the moves are the score.</summary>
        void StartWeekly(int g, int slot)
        {
            if (slot < 1 || !WeekOpenFor(g)) return;
            var level = GameData.ChallengeLevel(GameData.GroupId(g), slot);
            if (level == null && RanksDebug.On) { level = GameData.Build(g, slot); if (level != null) level.Number = slot; }
            if (level == null)
            {
                ShowTip(Loc.T("This week's levels are not here yet"),
                    Loc.T("Connect to the internet and open the game again to download them."), Loc.T("OK"), null);
                return;
            }
            QuitAttempt();   // a normal level left for the weekly one ends its attempt; weekly runs never open one (no level_* events)
            _tw.Clear();
            _ftue = 0;
            _group = g;
            _weekGroup = g;
            _weeklySlot = slot;
            _cheat = false;
            _level = level;
            CaptureStart();
            OpenWeeklyClock(g, slot);
            _moves = 0;
            _hintsUsed = _undosUsed = 0;
            _hintsLeft = _undosLeft = 0;
            _undo.Clear();
            _unlockedGroup = -1;
            _shownMoves = _shownLit = _shownTotal = _shownHints = _shownUndos = _shownSecond = -1;
            _levelCheck = "";
            _zoomLogged = false;
            GameAnalytics.Log("weekly_start", ("shape", GameData.GroupId(g)), ("slot", slot));
            _busy = false;
            _state = State.Playing;
            HideAll();
            GameAudio.Music(MusicTrack.Play);
            SetBoard(_level, true, PlayScreenShare);
            _board.transform.rotation = StartRotation(_level.Topo, -28f, 38f);
            _spin = Vector2.zero;
            var hud = UI.Hud;
            hud.Show();
            hud.Title.text = HudTitle();
            hud.Tip.text = Loc.T("No hints, no undo. Fewest moves wins, then fastest time. The clock never resets.");
            SetHelpButtons(false);
            ResetMascot();
            if (hud.Mascot != null) hud.Mascot.Say(Mascot.Mood.Idle, null);
            if (hud.LampTray != null) SetupLampTray();
            UpdateHud();
            UpdateZoomPanel();
        }

        /// <summary>The clock of the level goes on from its earlier attempts (<see cref="WeeklyClocks"/>).</summary>
        void OpenWeeklyClock(int g, int slot)
        {
            SaveWeeklyClock();   // the attempt before, which may be of another level
            if (RanksDebug.On && _sampleClocks == null) _sampleClocks = new WeeklyClocks();
            var clocks = RanksDebug.On ? _sampleClocks : CubeSave.WeeklyClocks;
            bool pruned = clocks.KeepOnly(WeekId);
            _weeklyClock = clocks.Get(WeekId, GameData.GroupId(g), slot);
            _playSeconds = _weeklyClockSaved = _weeklyClock.seconds;
            if (pruned && !RanksDebug.On) CubeSave.SaveWeeklyClocks();
        }

        /// <summary>Every frame of a weekly level: keeps the clock, and writes it to disk every few seconds.</summary>
        void TickWeeklyClock()
        {
            WeeklyClocks.Raise(_weeklyClock, _playSeconds);
            if (_playSeconds - _weeklyClockSaved >= WeeklyClockSaveEvery) SaveWeeklyClock();
        }

        /// <summary>Write the clock of the weekly level in play, if it moved since the last time.</summary>
        void SaveWeeklyClock()
        {
            if (_weeklyClock == null || _weeklyClock.seconds <= _weeklyClockSaved) return;
            _weeklyClockSaved = _weeklyClock.seconds;
            if (_sampleClocks == null || !_sampleClocks.entries.Contains(_weeklyClock)) CubeSave.SaveWeeklyClocks();
        }

        /// <summary>The server counts a later clock for a level (data cleared, or another phone): go on from that one.</summary>
        void AdoptWeeklyClock(RankReply reply)
        {
            for (int g = 0; g < GameData.GroupCount; g++)
                for (int slot = 1; slot <= WeekCount; slot++)
                {
                    string gid = GameData.GroupId(g);
                    if (reply.board != RankScore.WeeklyBoard(gid, slot)) continue;
                    var entry = CubeSave.WeeklyClocks.Get(GameData.Challenge.id, gid, slot);
                    if (reply.tenths <= RankScore.Tenths(entry.seconds)) return;   // the usual case: our own time came back
                    WeeklyClocks.Raise(entry, reply.tenths / 10f);
                    if (entry == _weeklyClock)
                    {
                        _weeklyClockSaved = entry.seconds;
                        if (_weeklySlot > 0) _playSeconds = Mathf.Max(_playSeconds, entry.seconds);
                        if (_weeklySlot > 0 && _state == State.Won) RaiseResult(gid, slot, (int)reply.tenths);
                    }
                    CubeSave.SaveWeeklyClocks();
                    return;
                }
        }

        /// <summary>The win just made counts with a later time: correct its card and, when it is the best run, the saved best.</summary>
        void RaiseResult(string gid, int slot, int tenths)
        {
            string key = BestKey(WeekId, gid, slot), mine = _resultMoves + "," + _resultTenths;
            bool wasBest = PlayerPrefs.GetString(key, "") == mine;
            _resultTenths = tenths;
            if (!wasBest) return;
            bool still = _resultWasMoves < 0 || RankScore.Weekly(_resultMoves, tenths) < RankScore.Weekly(_resultWasMoves, _resultWasTenths);
            PlayerPrefs.SetString(key, still ? _resultMoves + "," + tenths : _resultWasMoves + "," + _resultWasTenths);
            _resultBest = still && _resultWasMoves >= 0;
        }

        /// <summary>The time that counts, on the open win card.</summary>
        void ShowCountedTime()
        {
            UI.WeeklyResult.TimeValue.text = RankScore.TimeText(_resultTenths);
            UI.WeeklyResult.NewBest.SetActive(_resultBest);
        }

        void SetHelpButtons(bool on)
        {
            UI.Hud.HintButton.gameObject.SetActive(on);
            UI.Hud.UndoButton.gameObject.SetActive(on);
        }

        void WeeklyWin()
        {
            _busy = true;
            _state = State.Won;
            _spin = Vector2.zero;
            ClearNotes();
            ResetZoom(true);
            int g = _group, slot = _weeklySlot;
            string gid = GameData.GroupId(g);
            WeeklyClocks.Raise(_weeklyClock, _playSeconds);
            SaveWeeklyClock();
            int tenths = RankScore.Tenths(_playSeconds);
            bool had = WeeklyBest(g, slot, out int oldMoves, out int oldTenths);
            bool better = !had || RankScore.Weekly(_moves, tenths) < RankScore.Weekly(oldMoves, oldTenths);
            if (better) PlayerPrefs.SetString(BestKey(WeekId, gid, slot), _moves + "," + tenths);
            _resultMoves = _moves; _resultTenths = tenths;
            _resultWasMoves = had ? oldMoves : -1; _resultWasTenths = oldTenths;
            _resultBest = better && had;
            _resultOldRank = Cached(RankScore.WeeklyBoard(gid, slot), out var page) && page.Mine != null ? page.Mine.Rank : 0;
            _pages.Remove(RankScore.WeeklyBoard(gid, slot));
            _pages.Remove(RankScore.WeeklyTotalBoard(gid));
            if (!RanksDebug.On && _tapsWhole && _taps.Count == _moves && !string.IsNullOrEmpty(WeekId)) Ranks.SubmitWeekly(WeekId, gid, slot, _taps, _playSeconds);
            GameAnalytics.Log("weekly_complete", ("shape", gid), ("slot", slot), ("moves", _moves), ("seconds", Mathf.RoundToInt(_playSeconds)), ("best", better));
            _board.Celebrate();
            GameAudio.Play(Sfx.Win);
            GameAudio.Duck(3f);
            var tr = _board.transform;
            Quaternion from = tr.rotation;
            var board = _board;
            _tw.Run(0.55f, p => tr.rotation = Quaternion.AngleAxis(200f * p * p, Vector3.up) * from, () =>
            {
                board.Shatter();
                ExpertBurst();
                Haptics.Play(Haptics.Kind.Win);
            }, tr);
            _tw.Run(0.55f + ResultDelay, p => { }, () =>
            {
                _busy = false;
                ShowWeeklyResult(RanksDebug.On || Ranks.SignedIn, RanksDebug.On ? 19 : 0, RanksDebug.On ? 902 : 0, RanksDebug.On ? 14 : 0);
            }, UI.WeeklyResult);
        }

        /// <summary>The weekly win card. <paramref name="rank"/> 0 = not ranked yet (filled in when the server answers).</summary>
        void ShowWeeklyResult(bool online, int rank, int players, int up)
        {
            var r = UI.WeeklyResult;
            if (r == null) return;
            string shape = Loc.Upper(Loc.T(GameData.Group(_group).name));
            r.Caption.text = Loc.T("WEEKLY") + " · " + shape + " · " + Loc.F("LEVEL {0}", _weeklySlot);
            r.Title.text = Loc.T("Solved!");
            r.NewBest.SetActive(_resultBest);
            r.MovesValue.text = _resultMoves.ToString();
            r.TimeValue.text = RankScore.TimeText(_resultTenths);
            r.MovesWas.gameObject.SetActive(_resultWasMoves >= 0);
            r.TimeWas.gameObject.SetActive(_resultWasMoves >= 0);
            if (_resultWasMoves >= 0)
            {
                r.MovesWas.text = Loc.F("was {0}", _resultWasMoves);
                r.TimeWas.text = Loc.F("was {0}", RankScore.TimeText(_resultWasTenths));
            }
            r.RankBox.gameObject.SetActive(online);
            r.Offline.SetActive(!online);
            if (!online) r.OfflineText.text = Loc.T("Saved. Your score posts when you're back online.");
            else FillRankBox(rank, players, up);
            int next = NextWeeklySlot(_group);
            r.NextLabel.text = next > 0 ? Loc.F("Level {0}", next) : Loc.T("All levels");
            UI.Dim.SetActive(true);
            r.Open();
            r.transform.SetAsLastSibling();
        }

        void FillRankBox(int rank, int players, int up)
        {
            var r = UI.WeeklyResult;
            ThemeHex();
            if (rank <= 0)
            {
                r.RankLine.text = Loc.T("Posting your score...");
                r.RankUp.text = "";
                return;
            }
            r.RankLine.text = "#" + rank + " <size=70%><color=" + _mutedHex + ">" + Loc.F("of {0} players", RankText.Count(players)) + "</color></size>";
            r.RankUp.text = up > 0 ? Loc.F("Up {0} places", up) : up < 0 ? "" : Loc.T("Your rank is kept");
        }

        void OnRankPosted(RankReply reply)
        {
            if (this == null || reply == null) return;
            if (reply.ok) AdoptWeeklyClock(reply);
            if (!reply.ok || UI.WeeklyResult == null || !UI.WeeklyResult.gameObject.activeSelf) return;
            if (reply.board != RankScore.WeeklyBoard(GameData.GroupId(_group), _weeklySlot)) return;
            ShowCountedTime();
            int up = _resultOldRank > 0 && reply.rank > 0 ? _resultOldRank - reply.rank : 0;
            FillRankBox(reply.rank, reply.players, up);
        }

        // ---------------- Name ----------------

        public const string TermsUrl = "https://github.com/tuankun-279/CircuitCube/blob/main/TERMS.md";
        public const string DeleteDataUrl = "https://github.com/tuankun-279/CircuitCube/blob/main/PRIVACY.md#delete-your-data";

        bool _nameFirst;          // the sheet after the tutorial (free name, first = true on the server)
        Action _afterName;        // what comes after the sheet (the first level after the tutorial)
        bool _nameSending;
        BoardRow _reportRow;
        string _reportBoard = "";
        readonly Dictionary<BoardRowView, BoardRow> _rowData = new Dictionary<BoardRowView, BoardRow>();

        void WireNames()
        {
            var n = UI.NameSheet;
            if (n != null && n.Terms != null) n.Terms.onClick.AddListener(() => Application.OpenURL(TermsUrl));
            var st = UI.Settings;
            if (st.Rename != null) st.Rename.onClick.AddListener(() => OpenNameSheet(false));
            if (st.Terms != null) st.Terms.onClick.AddListener(() => Application.OpenURL(TermsUrl));
            if (st.DeleteData != null) st.DeleteData.onClick.AddListener(() => Application.OpenURL(DeleteDataUrl));
            var r = UI.Report;
            if (r != null)
            {
                r.Cancel.onClick.AddListener(CloseReport);
                r.Report.onClick.AddListener(SendReport);
            }
            Ranks.NameChanged += OnNameState;
            // players from before 1.5.0 already have a name: they are not asked after a tutorial they finished long ago
            if (CubeSave.NameAsk == 0 && CubeSave.TutorialStep >= FtueDone) CubeSave.NameAsk = 2;
        }

        /// <summary>After the tutorial of a new player: opens the name sheet once and runs <paramref name="then"/> when it closes.</summary>
        bool AskNameAfterTutorial(Action then)
        {
            if (UI.NameSheet == null || CubeSave.NameAsk != 0 || !Ranks.Available) return false;
            if (CubeSave.Beaten(0) > 0) { CubeSave.NameAsk = 2; return false; }   // a player from before 1.5.0 replaying the tutorial
            CubeSave.NameAsk = 1;
            _afterName = then;
            OpenNameSheet(true);
            GameAnalytics.Log("name_sheet", ("from", "tutorial"));
            return true;
        }

        void OpenNameSheet() => OpenNameSheet(false);

        void OpenNameSheet(bool first)
        {
            var n = UI.NameSheet;
            if (n == null) return;
            if (!first && !RanksDebug.On && Ranks.Name != null && Ranks.Name.renamesLeft <= 0)
            {
                ShowTip(Loc.T("No renames left"), Loc.T("You have used your free rename."), Loc.T("OK"), null);
                return;
            }
            _nameFirst = first;
            string full = Ranks.PlayerName.Length > 0 ? Ranks.PlayerName : RanksDebug.On ? RanksDebug.MyName : "";
            RankText.SplitTag(full, out var name, out var tag);
            if (first || name == NameRules.Fallback) name = "";
            string note = CloudSave.SignedIn || RanksDebug.On
                ? Loc.T("Signed in with Google Play Games. Your rank follows you to a new phone.")
                : Loc.T("Not signed in to Google Play Games: your name and rank stay on this phone.");
            GameAudio.Play(Sfx.PanelOpen);
            UI.Dim.SetActive(true);
            n.Open(name, tag, note, Loc.T(first ? "Your name on the boards" : "Rename"));
            n.transform.SetAsLastSibling();
        }

        void CloseNameSheet()
        {
            if (UI.NameSheet == null || !UI.NameSheet.gameObject.activeSelf) return;
            UI.NameSheet.gameObject.SetActive(false);
            if ((UI.Boards == null || !UI.Boards.gameObject.activeSelf) && !UI.Settings.gameObject.activeSelf) UI.Dim.SetActive(false);
            var then = _afterName; _afterName = null;
            then?.Invoke();
        }

        void SaveName()
        {
            var n = UI.NameSheet;
            var verdict = NameFilter.Check(n.Input.text, out var clean);
            if (verdict != NameVerdict.Ok) { n.ShowError(NameError(verdict == NameVerdict.Reserved ? "reserved" : verdict == NameVerdict.Empty ? "empty" : "name")); return; }
            if (RanksDebug.On) { CloseNameSheet(); return; }
            if (!Ranks.SignedIn)
            {
                if (_nameFirst)
                {
                    // offline right after the tutorial: the name goes up as soon as the boards are reachable
                    CubeSave.PendingName = clean;
                    CloseNameSheet();
                    return;
                }
                n.ShowError(NameError("offline"));
                return;
            }
            if (_nameSending) return;
            _nameSending = true;
            n.Save.interactable = false;
            bool first = _nameFirst;
            Ranks.Rename(clean, first, reply =>
            {
                _nameSending = false;
                if (this == null) return;
                if (n != null) n.Save.interactable = true;
                GameAnalytics.Log("name_set", ("first", first), ("ok", reply.ok), ("error", reply.error ?? ""));
                if (!reply.ok) { n.ShowError(NameError(reply.error)); return; }
                _pages.Clear();
                CloseNameSheet();
                if (UI.Boards != null && UI.Boards.gameObject.activeSelf) FillBoards();
                if (UI.Settings.gameObject.activeSelf) FillSettingsName();
            });
        }

        static string NameError(string code)
        {
            switch (code)
            {
                case "reserved": return Loc.T("That name looks like the game's team. Try another.");
                case "empty": return Loc.T("Type a name first.");
                case "no_renames": return Loc.T("You have used your free rename.");
                case "offline": return Loc.T("Can't reach the boards right now. Try again when you're back online.");
                default: return Loc.T("This name isn't allowed. Try another.");
            }
        }

        /// <summary>Sends a name typed offline after the tutorial once the boards are reachable.</summary>
        void TickNames()
        {
            if (_nameSending || !Ranks.SignedIn || Ranks.Name == null) return;
            string pending = CubeSave.PendingName;
            if (pending.Length == 0) return;
            _nameSending = true;
            Ranks.Rename(pending, true, reply =>
            {
                _nameSending = false;
                // sent, or refused for good: either way it is not sent again (offline keeps it for later)
                if (reply.ok || reply.error != "offline") CubeSave.PendingName = "";
            });
        }

        /// <summary>The server's answer about the name arrived: a reset by the team asks for a new name.</summary>
        void OnNameState()
        {
            if (this == null) return;
            if (UI.Settings.gameObject.activeSelf) FillSettingsName();
            var s = Ranks.Name;
            if (s != null && s.reset && _state == State.Home && !CardOpen && UI.NameSheet != null)
            {
                ShowTip(Loc.T("Choose a new name"), Loc.T("Your name was reset after a report. Pick a new one for the boards."), Loc.T("OK"), () => OpenNameSheet(false));
            }
        }

        void FillSettingsName()
        {
            var st = UI.Settings;
            if (st.NameValue == null) return;
            bool on = Ranks.Available && (Ranks.SignedIn || RanksDebug.On);
            string full = Ranks.PlayerName.Length > 0 ? Ranks.PlayerName : RanksDebug.On ? RanksDebug.MyName : Loc.T("Player");
            RankText.SplitTag(full, out var name, out var tag);
            st.NameValue.text = name + (tag.Length > 0 ? " <color=" + MutedHex() + ">" + tag + "</color>" : "");
            int left = RanksDebug.On ? 1 : Ranks.Name != null ? Ranks.Name.renamesLeft : 0;
            st.RenameNote.text = !on ? Loc.T("Offline")
                : Ranks.Name == null && !RanksDebug.On ? Loc.T("Loading...")
                : left == 1 ? Loc.T("1 free rename left") : left > 1 ? Loc.F("{0} free renames left", left) : Loc.T("No renames left");
            st.Rename.interactable = on && (RanksDebug.On || (Ranks.Name != null && left > 0));
        }

        string MutedHex()
        {
            ThemeHex();
            return _mutedHex;
        }

        // ---------------- Report a name ----------------

        /// <summary>Hooks the long press of a board row once and keeps the row it shows now.</summary>
        void KeepRow(BoardRowView v, BoardRow row, bool me)
        {
            if (v.Press == null) return;
            if (!_rowData.ContainsKey(v))
                v.Press.Pressed += () => { if (_rowData.TryGetValue(v, out var r) && r != null) OpenReport(r); };
            _rowData[v] = me || row == null || string.IsNullOrEmpty(row.PlayerId) ? null : row;
        }

        void OpenReport(BoardRow row)
        {
            var r = UI.Report;
            if (r == null || row == null) return;
            _reportRow = row;
            r.PlayerName.text = row.Name ?? "";
            Haptics.Play(Haptics.Kind.Turn);
            GameAudio.Play(Sfx.PanelOpen);
            r.gameObject.SetActive(true);
            r.transform.SetAsLastSibling();
        }

        void CloseReport()
        {
            if (UI.Report == null || !UI.Report.gameObject.activeSelf) return;
            UI.Report.gameObject.SetActive(false);
            _reportRow = null;
        }

        void SendReport()
        {
            var row = _reportRow;
            CloseReport();
            if (row == null) return;
            GameAnalytics.Log("name_report", ("board", _reportBoard));
            if (RanksDebug.On) { ShowTip(Loc.T("Thanks for the report"), Loc.T("Our team will check this name."), Loc.T("OK"), null); return; }
            Ranks.ReportName(row.PlayerId, row.Name, _reportBoard, ok =>
            {
                if (this == null) return;
                if (ok) ShowTip(Loc.T("Thanks for the report"), Loc.T("Our team will check this name."), Loc.T("OK"), null);
                else ShowTip(Loc.T("Report not sent"), Loc.T("Can't reach the boards right now. Try again when you're back online."), Loc.T("OK"), null);
            });
        }

        // ---------------- Debug ----------------

        /// <summary>Debug fill on or off (F4): sample boards and a sample week for the leaderboard screens.</summary>
        public void DebugRanks(bool on)
        {
            RanksDebug.On = on;
            _pages.Clear();
            if (_state == State.Home) ShowHome();
            else if (_state == State.Weekly) ShowWeekly(_weekGroup);
            if (UI.Boards != null && UI.Boards.gameObject.activeSelf) FillBoards();
        }

        /// <summary>Open a leaderboard screen for a screenshot: 0 Home, 1 Weekly, 2 This week, 3 All-time, 4 result, 5 offline result, 6 name.</summary>
        public void DebugShowRanks(int screen)
        {
            if (!RanksDebug.On) DebugRanks(true);
            switch (screen)
            {
                case 0: ShowHome(); break;
                case 1: ShowWeekly(0); break;
                case 2: ShowHome(); _boardTab = 2; OpenBoards(false, 0); break;
                case 3: ShowHome(); OpenBoards(true, 0); break;
                case 4:
                case 5:
                    ShowWeekly(0);
                    _group = 0; _weeklySlot = 2;
                    _resultMoves = 27; _resultTenths = 729; _resultWasMoves = 31; _resultWasTenths = 1002; _resultBest = true;
                    ShowWeeklyResult(screen == 4, 19, 902, 14);
                    break;
                case 6: ShowHome(); OpenBoards(true, 0); OpenNameSheet(); break;
            }
        }
    }
}
