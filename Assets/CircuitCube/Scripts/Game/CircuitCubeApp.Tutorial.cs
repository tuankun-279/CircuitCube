using System.Collections.Generic;
using CircuitCube.Core;
using CircuitCube.UI;
using UnityEngine;

namespace CircuitCube.Game
{
    /// <summary>
    /// First-time experience (FTUE, approved mockups FT0-FTEnd): a new player is greeted by the mascot, plays six short
    /// tutorial levels on a 3x3 cube (<see cref="TutorialLevels"/>) while the mascot's note explains each idea, and is
    /// then asked "Ready to play?" before Moonbase 1. Every note step names what it points at (tiles, the lamp tray,
    /// Undo, the hint, the HUD line, the result word) and what it waits for (a tap on the note, turning a tile, spinning
    /// the cube, a hint, the win). Later, the first level with a new colour mix gets the same kind of notes.
    /// Progress: CubeSave.TutorialStep holds the tutorial level reached (1-6) and <see cref="FtueDone"/> once done.
    /// </summary>
    public sealed partial class CircuitCubeApp
    {
        public const string MascotName = "Lumi";
        /// <summary>CubeSave.TutorialStep once the FTUE is done (1-3 were the old Moonbase lessons).</summary>
        const int FtueDone = 10;
        /// <summary>The tutorial level that ends on the result card (to show stars and Expert).</summary>
        const int FtueResultLevel = 4;

        enum Wait { Tap, TurnTile, Spin, Hint, Win }
        enum Phase { Play, AfterWin, OnResult }
        enum Mark { None, Tile, Tray, Tag, Undo, Hint, Status, Word }

        struct Target
        {
            public Mark Kind;
            public int Index;   // tile index, or the lamp tray group / tag
            public static Target Tile(int i) => new Target { Kind = Mark.Tile, Index = i };
            public static Target Of(Mark m, int i = 0) => new Target { Kind = m, Index = i };
        }

        sealed class Step
        {
            public string Text;
            public Mascot.Mood Mood;
            public Wait Wait;
            public Phase Phase;
            public int WaitTile = -1;
            public List<Target> Rings = new List<Target>(), Dims = new List<Target>();
            public Target Hand;
            public bool Swipe;
        }

        readonly List<Step> _steps = new List<Step>();
        int _step = -1;
        int _ftue;                  // tutorial level being played (1-6), 0 = none
        Phase _phase;
        bool _stepShown;
        int _lessonMix;             // colour mix the current Mix notes are about (0 = no Mix notes)
        readonly HashSet<int> _turnable = new HashSet<int>();   // tutorial: tiles that may turn

        bool InFtue => _ftue > 0;

        /// <summary>A player who has not done the tutorial and has not won a level yet.</summary>
        static bool NeedsFtue => CubeSave.TutorialStep < FtueDone && TotalWins() == 0;

        static GroupData _ftueGroup;
        /// <summary>The tutorial allows no wasted moves.</summary>
        static GroupData FtueGroup => _ftueGroup ?? (_ftueGroup = new GroupData { wasteAllowed = 0 });

        /// <summary>Lumi's note is on screen and waits for a tap: the play clock stops while the player reads.</summary>
        bool LessonTalking
        {
            get
            {
                var s = CurrentStep;
                return UI.Tutorial != null && UI.Tutorial.gameObject.activeSelf && _stepShown
                       && s != null && s.Text != null && s.Wait == Wait.Tap && s.Phase == _phase;
            }
        }

        void WireTutorial()
        {
            var tut = UI.Tutorial;
            if (tut == null) return;
            tut.Clear();
            tut.gameObject.SetActive(false);
            tut.Skip.onClick.AddListener(SkipNotes);
            tut.TapCatcher.onClick.AddListener(() => { if (CurrentStep != null && CurrentStep.Wait == Wait.Tap) Advance(); });
            tut.WelcomeButton.onClick.AddListener(() => { GameAnalytics.Log("tutorial_step", ("step", "welcome"), ("action", "done")); StartFtueLevel(Mathf.Clamp(CubeSave.TutorialStep, 1, TutorialLevels.Count), false); });
            tut.Go.onClick.AddListener(FinishFtue);
            tut.Replay.onClick.AddListener(() => { GameAnalytics.Log("tutorial_step", ("step", "ready"), ("action", "replay")); StartFtueLevel(1, true); });
        }

        // ---------------------------------------------------------------- flow

        /// <summary>The mascot says hello (first start), then the first tutorial level begins.</summary>
        void ShowWelcome()
        {
            _state = State.Home;
            _ftue = 0;
            _tw.Clear();
            HideAll();
            GameAudio.Music(MusicTrack.Menu);
            if (_board != null) { Destroy(_board.gameObject); _board = null; }
            var tut = UI.Tutorial;
            tut.gameObject.SetActive(true);
            tut.ShowWelcome(Loc.Upper(MascotName), Loc.F("Welcome to Circuit Cube! I'm {0}. Let me show you how to play!", MascotName));
            tut.WelcomeTap.text = Loc.T("Tap to continue");
            GameAnalytics.Log("tutorial_step", ("step", "welcome"), ("action", "shown"));
        }

        /// <summary>Start tutorial level <paramref name="n"/> (1-6); <paramref name="slide"/> brings the board in from the right.</summary>
        void StartFtueLevel(int n, bool slide)
        {
            _ftue = n;
            _weeklySlot = 0;
            SetHelpButtons(true);
            if (CubeSave.TutorialStep < n) CubeSave.TutorialStep = n;
            _cheat = false;
            _tw.Clear();
            _level = TutorialLevels.Build(n);
            CaptureStart();
            _moves = 0;
            _hintsUsed = 0;
            _hintsLeft = HintsPerLevel;
            _undosUsed = 0;
            _undosLeft = UndosPerLevel;
            _undo.Clear();
            _unlockedGroup = -1;
            _levelCheck = "";
            _shownMoves = _shownLit = _shownTotal = _shownHints = _shownUndos = _shownSecond = -1;
            _busy = false;
            _state = State.Playing;
            HideAll();
            GameAudio.Music(MusicTrack.Play);
            SetBoard(_level, true, PlayScreenShare);
            _board.transform.rotation = Quaternion.Euler(-22f, 30f, 0f);   // front, right and top faces, as in the mockups
            _spin = Vector2.zero;
            _turnable.Clear();
            for (int i = 0; i < _level.Tiles.Length; i++) if (Circuit.TapsToSolve(_level.Tiles[i]) > 0) _turnable.Add(i);

            var hud = UI.Hud;
            hud.Show();
            hud.Title.text = Loc.F("TUTORIAL {0} / {1}", n, TutorialLevels.Count);
            hud.Tip.text = "";
            ResetMascot();
            if (hud.Mascot != null) hud.Mascot.Say(Mascot.Mood.Idle, null);
            if (hud.LampTray != null) SetupLampTray();
            UpdateHud();
            UpdateZoomPanel();
            if (UI.Tutorial != null) { UI.Tutorial.Clear(); UI.Tutorial.gameObject.SetActive(false); }
            BuildFtueSteps(n);
            BeginSteps();
            GameAnalytics.Log("tutorial_step", ("step", "level" + n), ("action", "shown"));
            if (slide) SlideBoardIn();
        }

        /// <summary>The tutorial level is won: its last notes, then the next level (or "Ready to play?").</summary>
        void FtueWon()
        {
            _state = State.Won;
            _busy = false;
            _spin = Vector2.zero;
            ResetZoom(true);
            _board.Celebrate();
            GameAudio.Play(Sfx.Win);
            Haptics.Play(Haptics.Kind.Win);
            if (UI.Hud.Mascot != null) UI.Hud.Mascot.Say(Mascot.Mood.Excited, null);
            GameAnalytics.Log("tutorial_step", ("step", "level" + _ftue), ("action", "done"));
            EnterPhase(Phase.AfterWin);
        }

        /// <summary>After the last note of a tutorial level: on to the next one, or the end.</summary>
        void FtueNext()
        {
            ClearNotes();
            int next = _ftue + 1;
            if (CubeSave.TutorialStep < next && next <= TutorialLevels.Count) CubeSave.TutorialStep = next;
            if (next > TutorialLevels.Count) { ShowReadyToPlay(); return; }
            SlideBoardOut(() => StartFtueLevel(next, true));
        }

        void ShowReadyToPlay()
        {
            var tut = UI.Tutorial;
            UI.Hud.Hide();
            CloseCards();
            _state = State.Home;
            tut.gameObject.SetActive(true);
            tut.ShowReady();
            tut.ReadyCaption.text = Loc.F("TUTORIAL COMPLETE · {0} / {0}", TutorialLevels.Count);
            tut.ReadyTitle.text = Loc.T("Ready to play?");
            tut.ReadySub.text = Loc.T("Moonbase is waiting for you to light it up!");
            // transition A: the board slides out to the left, the ready screen comes in from the right
            var content = tut.ReadyContent;
            float w = tut.Rect.rect.width;
            var back = Backdrop;
            Vector2 backHome = back != null ? back.anchoredPosition : Vector2.zero;
            var board = _board;
            Vector3 from = board != null ? board.transform.position : Vector3.zero;
            float shift = _fitRadius * 4f;
            _tw.Run(0.42f, p =>
            {
                content.anchoredPosition = new Vector2(w * (1f - p), 0f);
                if (board != null) board.transform.position = from + Vector3.left * shift * p;
                if (back != null) back.anchoredPosition = backHome + new Vector2(-28f * Mathf.Sin(p * Mathf.PI), 0f);
            }, () =>
            {
                content.anchoredPosition = Vector2.zero;
                if (back != null) back.anchoredPosition = backHome;
                if (board != null) { Destroy(board.gameObject); if (_board == board) _board = null; }
            }, content);
            _ftue = 0;
            GameAnalytics.Log("tutorial_step", ("step", "ready"), ("action", "shown"));
        }

        /// <summary>"I'm ready!" (or the tutorial skipped): the tutorial is done, Moonbase 1 starts.</summary>
        void FinishFtue()
        {
            CubeSave.TutorialStep = FtueDone;
            // what the tutorial taught is not taught again
            CubeSave.Tips |= CubeSave.TipColours | CubeSave.TipMix(Circuit.Red | Circuit.Blue);
            CubeSave.SeenMix = true;
            CubeSave.SeenHowTo = true;
            CubeSave.MarkSeenShape(0);
            _ftue = 0;
            ClearNotes();
            if (UI.Tutorial != null) { UI.Tutorial.Clear(); UI.Tutorial.gameObject.SetActive(false); }
            GameAnalytics.Log("tutorial_step", ("step", "ready"), ("action", "done"));
            StartLevel(0, 1);
        }

        /// <summary>"Skip tutorial" on a note: tutorial notes end the whole tutorial, Mix notes end themselves.</summary>
        void SkipNotes()
        {
            if (InFtue)
            {
                GameAnalytics.Log("tutorial_step", ("step", "level" + _ftue), ("action", "skipped"));
                FinishFtue();
                return;
            }
            if (_lessonMix != 0) CubeSave.Tips |= CubeSave.TipMix(_lessonMix);
            GameAnalytics.Log("tutorial_step", ("step", "mix"), ("action", "skipped"));
            ClearNotes();
        }

        void SlideBoardIn()
        {
            if (_board == null) return;
            var tr = _board.transform;
            Vector3 home = tr.position, shift = Vector3.right * _fitRadius * 4f;
            var back = Backdrop;
            Vector2 backHome = back != null ? back.anchoredPosition : Vector2.zero;
            _busy = true;
            _tw.Run(0.42f, p =>
            {
                tr.position = home + shift * (1f - p);
                if (back != null) back.anchoredPosition = backHome + new Vector2(-28f * Mathf.Sin(p * Mathf.PI), 0f);
            }, () => { tr.position = home; if (back != null) back.anchoredPosition = backHome; _busy = false; }, tr);
        }

        void SlideBoardOut(System.Action done)
        {
            if (_board == null) { done(); return; }
            var tr = _board.transform;
            Vector3 home = tr.position, shift = Vector3.left * _fitRadius * 4f;
            _busy = true;
            _tw.Run(0.3f, p => tr.position = home + shift * p * p, () => { _busy = false; done(); }, tr);
        }

        // ---------------------------------------------------------------- the notes of each tutorial level

        int T(char face, int cell) => TutorialLevels.Find(_level.Topo, face, cell);

        Step Add(string english, Mascot.Mood mood, Wait wait, Phase phase = Phase.Play)
        {
            var s = new Step { Text = Loc.T(english), Mood = mood, Wait = wait, Phase = phase };
            _steps.Add(s);
            return s;
        }

        void BuildFtueSteps(int n)
        {
            _steps.Clear();
            _lessonMix = 0;
            Step s;
            switch (n)
            {
                case 1:
                    Add("This is a <b>power source</b>.", Mascot.Mood.Happy, Wait.Tap).Rings.Add(Target.Tile(T('f', 3)));
                    Add("This is a <b>lamp</b>. Connect power to it to light it up!", Mascot.Mood.Idle, Wait.Tap).Rings.Add(Target.Tile(T('f', 5)));
                    s = Add("Tap this tile to <b>turn</b> it.", Mascot.Mood.Think, Wait.TurnTile);
                    s.WaitTile = T('f', 4); s.Dims.Add(Target.Tile(s.WaitTile)); s.Hand = Target.Tile(s.WaitTile);
                    Add("Great! The lamp is on. You win!", Mascot.Mood.Excited, Wait.Tap, Phase.AfterWin).Rings.Add(Target.Tile(T('f', 5)));
                    break;
                case 2:
                    Add("This bar shows <b>how many lamps are lit</b> for each colour. Blue is 1/2!", Mascot.Mood.Idle, Wait.Tap).Dims.Add(Target.Of(Mark.Tray, 0));
                    s = Add("Turn this tile to send power <b>to the side</b>.", Mascot.Mood.Think, Wait.TurnTile);
                    s.WaitTile = T('f', 5); s.Dims.Add(Target.Tile(s.WaitTile)); s.Hand = Target.Tile(s.WaitTile);
                    s = Add("The last lamp is on the side. <b>Drag across the screen</b> to spin the shape.", Mascot.Mood.Happy, Wait.Spin);
                    s.WaitTile = T('r', 4); s.Swipe = true; s.Rings.Add(Target.Tile(T('r', 5)));
                    s = Add("Now you can see it! Tap this tile.", Mascot.Mood.Think, Wait.TurnTile);
                    s.WaitTile = T('r', 4); s.Dims.Add(Target.Tile(s.WaitTile)); s.Hand = Target.Tile(s.WaitTile);
                    Add("Blue 2/2! <b>Light every lamp</b> to win.", Mascot.Mood.Excited, Wait.Tap, Phase.AfterWin).Dims.Add(Target.Of(Mark.Tray, 0));
                    break;
                case 3:
                    s = Add("Turned the wrong one? Press <b>Undo</b> to put it back.", Mascot.Mood.Think, Wait.Tap);
                    s.Dims.Add(Target.Of(Mark.Undo)); s.Hand = Target.Of(Mark.Undo);
                    s = Add("Stuck? Tap the <b>bulb</b> and I'll turn one tile the right way for you.", Mascot.Mood.Idle, Wait.Hint);
                    s.Dims.Add(Target.Of(Mark.Hint)); s.Hand = Target.Of(Mark.Hint);
                    Add("Undo and hints <b>don't count as moves</b>. But using them means no EXPERT!", Mascot.Mood.Excited, Wait.Tap, Phase.AfterWin).Dims.Add(Target.Of(Mark.Status));
                    break;
                case 4:
                    Add("Every move counts. Turn each tile only as far as it needs for <b>3 stars</b>.", Mascot.Mood.Idle, Wait.Tap).Dims.Add(Target.Of(Mark.Status));
                    _steps.Add(new Step { Wait = Wait.Win, Phase = Phase.Play });   // the player solves it alone
                    Add("No wasted moves, no Undo or hints: you're an <b>EXPERT</b>!", Mascot.Mood.Excited, Wait.Tap, Phase.OnResult).Dims.Add(Target.Of(Mark.Word));
                    break;
                case 5:
                    s = Add("A lamp only lights with power of <b>its own colour</b>. Red power lit the Red lamp.", Mascot.Mood.Idle, Wait.Tap);
                    s.Rings.Add(Target.Tile(T('f', 2))); s.Rings.Add(Target.Tile(T('f', 8)));
                    s = Add("The Blue lamp needs Blue power. Turn this tile!", Mascot.Mood.Think, Wait.TurnTile);
                    s.WaitTile = T('f', 7); s.Dims.Add(Target.Tile(s.WaitTile)); s.Hand = Target.Tile(s.WaitTile);
                    s = Add("Each colour has <b>its own counter</b> on the lamp bar.", Mascot.Mood.Excited, Wait.Tap, Phase.AfterWin);
                    s.Dims.Add(Target.Of(Mark.Tray, 0)); s.Dims.Add(Target.Of(Mark.Tray, 1));
                    break;
                case 6:
                    Add("Colours can <b>mix</b>! This is Red power…", Mascot.Mood.Excited, Wait.Tap).Rings.Add(Target.Tile(T('f', 0)));
                    Add("…and this is Blue power.", Mascot.Mood.Idle, Wait.Tap).Rings.Add(Target.Tile(T('f', 6)));
                    s = Add("The <b>Purple</b> lamp needs both. Turn this tile to join them on one wire: Red + Blue = Purple.", Mascot.Mood.Think, Wait.TurnTile);
                    s.WaitTile = T('f', 3); s.Dims.Add(Target.Tile(s.WaitTile)); s.Hand = Target.Tile(s.WaitTile); s.Rings.Add(Target.Tile(T('f', 5)));
                    Add("The mixing recipe is <b>always shown on the lamp bar</b>. Look here if you forget!", Mascot.Mood.Happy, Wait.Tap, Phase.AfterWin).Dims.Add(Target.Of(Mark.Tag, 0));
                    break;
            }
        }

        // ---------------------------------------------------------------- Mix notes in the main game

        /// <summary>The first level with a mix the player has not met: notes on its two sources and its lamp.</summary>
        void StartLesson(int group, int number, bool cheat)
        {
            ClearNotes();
            _steps.Clear();
            _lessonMix = 0;
            if (UI.Tutorial == null) return;
            int tips = cheat ? 0 : CubeSave.Tips;
            bool anyMixSeen = (tips & (CubeSave.TipMix(3) | CubeSave.TipMix(5) | CubeSave.TipMix(6))) != 0;
            foreach (int c in _level.Colors)
            {
                if (!IsMixed(c) || (tips & CubeSave.TipMix(c)) != 0) continue;
                _lessonMix = c;
                break;
            }
            if (_lessonMix == 0) return;
            int a = _lessonMix & -_lessonMix, b = _lessonMix & ~a;
            string mix = CubeColors.EnergyName(_lessonMix), na = CubeColors.EnergyName(a), nb = CubeColors.EnergyName(b);
            if (!anyMixSeen)
            {
                AddText(Loc.F("Colours mix! This is {0} power...", na), Mascot.Mood.Excited).Rings.Add(Target.Tile(FindTile(TileKind.Source, a)));
                AddText(Loc.F("...and this is {0} power", nb), Mascot.Mood.Idle).Rings.Add(Target.Tile(FindTile(TileKind.Source, b)));
            }
            AddText(na + " + " + nb + " = " + mix + ". " + Loc.F("A {0} lamp needs both on the same wire", mix.ToLowerInvariant()), Mascot.Mood.Think)
                .Rings.Add(Target.Tile(FindTile(TileKind.Lamp, _lessonMix)));
            CubeSave.SeenMix = true;
            GameAnalytics.Log("tutorial_step", ("step", "mix"), ("action", "shown"));
            BeginSteps();
        }

        Step AddText(string text, Mascot.Mood mood)
        {
            var s = new Step { Text = text, Mood = mood, Wait = Wait.Tap, Phase = Phase.Play };
            _steps.Add(s);
            return s;
        }

        static bool IsMixed(int color) => (color & (color - 1)) != 0;

        // ---------------------------------------------------------------- step engine

        Step CurrentStep => _step >= 0 && _step < _steps.Count ? _steps[_step] : null;

        void BeginSteps()
        {
            _phase = Phase.Play;
            _step = 0;
            OnStepChanged();
        }

        /// <summary>A tapped note: on to the next step.</summary>
        void Advance()
        {
            var s = CurrentStep;
            if (s == null || s.Phase != _phase) return;
            GameAudio.Play(Sfx.Button);
            NextShownStep();
        }

        void NextShownStep()
        {
            _step++;
            OnStepChanged();
        }

        /// <summary>
        /// The level moved to a new phase (won, result card up): skip what the player went past and show the phase's
        /// first note.
        /// </summary>
        void EnterPhase(Phase phase)
        {
            _phase = phase;
            if (_step < 0) _step = 0;
            while (_step < _steps.Count && _steps[_step].Phase < phase) _step++;
            OnStepChanged();
        }

        void OnStepChanged()
        {
            _stepShown = false;
            var s = CurrentStep;
            if (s != null)
            {
                if (s.Phase != _phase || s.Text == null) { HideNotes(); return; }   // waits for the win or the card
                if (UI.Hud.Mascot != null) UI.Hud.Mascot.Say(s.Mood, null);
                return;
            }
            // no notes left
            ClearNotes(false);
            if (InFtue && _phase == Phase.AfterWin) FtueNext();
            else if (!InFtue && _lessonMix != 0)
            {
                CubeSave.Tips |= CubeSave.TipMix(_lessonMix);
                GameAnalytics.Log("tutorial_step", ("step", "mix"), ("action", "done"));
                _lessonMix = 0;
            }
        }

        void HideNotes()
        {
            var tut = UI.Tutorial;
            if (tut == null) return;
            tut.Clear();
            tut.gameObject.SetActive(false);
        }

        void ClearNotes(bool forget = true)
        {
            if (forget) { _steps.Clear(); _step = -1; }
            var tut = UI.Tutorial;
            if (tut == null) return;
            tut.Clear();
            tut.gameObject.SetActive(false);
        }

        /// <summary>A tile turned by the player: a "turn this tile" step is done once the tile sits right.</summary>
        void LessonTapped(int index)
        {
            var s = CurrentStep;
            if (s == null || s.Wait != Wait.TurnTile || s.WaitTile != index) return;
            if (Circuit.TapsToSolve(_level.Tiles[index]) == 0) NextShownStep();
        }

        void LessonHinted()
        {
            var s = CurrentStep;
            if (s != null && s.Wait == Wait.Hint) NextShownStep();
        }

        /// <summary>The board turned under the player's finger (the Spin step watches the cube itself).</summary>
        void LessonSpun(float degrees) { }

        /// <summary>A tutorial tap may turn only the tiles the lesson is about (and only the one it points at, if any).</summary>
        bool FtueAllowsTap(int index)
        {
            if (!InFtue) return true;
            var s = CurrentStep;
            if (s != null && s.Phase == Phase.Play && s.Text != null)
                return s.Wait == Wait.TurnTile && s.WaitTile == index && Circuit.TapsToSolve(_level.Tiles[index]) > 0;
            return _turnable.Contains(index);
        }

        /// <summary>Hints and undos are free in the tutorial; the hint step only takes the hint.</summary>
        bool FtueFreeHelp => InFtue;

        /// <summary>The result card is up in tutorial level 4: its note shows over the card.</summary>
        void FtueOnResult()
        {
            if (InFtue) EnterPhase(Phase.OnResult);
        }

        void TickLesson(float dt)
        {
            var tut = UI.Tutorial;
            var s = CurrentStep;
            if (tut == null || s == null || s.Text == null || s.Phase != _phase || _board == null) return;
            bool onResult = s.Phase == Phase.OnResult;
            bool show = onResult ? UI.Result.gameObject.activeSelf
                : (_state == State.Playing || (_state == State.Won && s.Phase == Phase.AfterWin)) && !CardOpen && !_busy;
            if (!show)
            {
                if (tut.gameObject.activeSelf && !tut.Ready.activeSelf && !tut.Welcome.activeSelf) tut.gameObject.SetActive(false);
                return;
            }
            if (!tut.gameObject.activeSelf) tut.gameObject.SetActive(true);
            if (onResult) tut.transform.SetAsLastSibling();
            if (s.Wait == Wait.Spin && Facing(s.WaitTile, 0.55f)) { NextShownStep(); return; }

            if (!_stepShown)
            {
                _stepShown = true;
                tut.ShowNote(Loc.Upper(MascotName), s.Text, s.Wait == Wait.Tap);
                tut.NextLabel.text = Loc.T("Tap to continue ›");
            }
            // marks follow the cube as it turns
            tut.ClearMarks();
            bool any = false;
            Rect hole = default;
            foreach (var d in s.Dims)
            {
                if (!TargetRect(tut, d, out var r)) continue;
                hole = any ? Rect.MinMaxRect(Mathf.Min(hole.xMin, r.xMin), Mathf.Min(hole.yMin, r.yMin), Mathf.Max(hole.xMax, r.xMax), Mathf.Max(hole.yMax, r.yMax)) : r;
                any = true;
            }
            if (any) tut.DimAround(hole);
            for (int i = 0; i < s.Rings.Count; i++)
                if (TargetRect(tut, s.Rings[i], out var r) && (s.Rings[i].Kind != Mark.Tile || Facing(s.Rings[i].Index, 0.12f))) tut.RingAt(i, r.center);
            if (s.Hand.Kind != Mark.None && TargetRect(tut, s.Hand, out var h) && (s.Hand.Kind != Mark.Tile || Facing(s.Hand.Index, 0.12f))) tut.HandAt(h.center);
            if (s.Swipe) tut.ShowSwipe(new Vector2(0f, -tut.Rect.rect.height * 0.2f), SwipeLeft(s.WaitTile));
        }

        /// <summary>Which way to drag to bring a tile round to the front: towards the side it is on, seen from the camera.</summary>
        bool SwipeLeft(int tile)
        {
            if (tile < 0) return true;
            float x = Cam.transform.InverseTransformDirection(_board.NormalWorld(tile)).x;
            return x > 0f;   // a tile facing right comes round with a drag to the left
        }

        /// <summary>Where a step target is, in the tutorial layer's coordinates.</summary>
        bool TargetRect(TutorialOverlay tut, Target t, out Rect r)
        {
            r = default;
            RectTransform ui = null;
            var hud = UI.Hud;
            switch (t.Kind)
            {
                case Mark.Tile:
                {
                    if (t.Index < 0) return false;
                    var sr = _board.ScreenRect(t.Index, Cam);
                    Vector2 a = tut.Local(sr.min), b = tut.Local(sr.max);
                    r = Rect.MinMaxRect(a.x, a.y, b.x, b.y);
                    return true;
                }
                case Mark.Tray: ui = TrayGroup(t.Index); break;
                case Mark.Tag:
                {
                    var g = TrayGroup(t.Index);
                    var lg = g != null ? g.GetComponent<LampGroup>() : null;
                    ui = lg != null && lg.Formula != null ? (RectTransform)lg.Formula.transform : null;
                    break;
                }
                case Mark.Undo: ui = (RectTransform)hud.UndoButton.transform; break;
                case Mark.Hint: ui = (RectTransform)hud.HintButton.transform; break;
                case Mark.Status: ui = (RectTransform)hud.Status.transform; break;
                case Mark.Word:
                    ui = UI.Fx != null && UI.Fx.Word != null && UI.Fx.Word.gameObject.activeInHierarchy ? UI.Fx.Word : (RectTransform)UI.Result.Stars.transform;
                    break;
            }
            if (ui == null || !ui.gameObject.activeInHierarchy) return false;
            r = tut.LocalRect(ui);
            if (t.Kind == Mark.Status)
            {
                // the HUD line is a wide box: light just its words
                float w = Mathf.Min(r.width, hud.Status.preferredWidth * ui.lossyScale.x / tut.Rect.lossyScale.x);
                r = new Rect(r.center.x - w * 0.5f, r.yMin, w, r.height);
            }
            if (t.Kind == Mark.Word)
            {
                // the word's own box is wide for its glow: light the letters
                r = new Rect(r.center.x - r.width * 0.42f, r.center.y - r.height * 0.22f, r.width * 0.84f, r.height * 0.44f);
            }
            return true;
        }

        RectTransform TrayGroup(int i)
        {
            var tray = UI.Hud.LampTray;
            if (tray == null) return null;
            int n = 0;
            for (int k = 0; k < tray.childCount; k++)
            {
                var c = tray.GetChild(k);
                if (!c.gameObject.activeSelf) continue;
                if (n++ == i) return (RectTransform)c;
            }
            return null;
        }

        /// <summary>
        /// A source or lamp of the given colour (0 = any), preferring one that faces the camera nearest the middle of
        /// the screen; -1 if the level has none.
        /// </summary>
        int FindTile(TileKind kind, int color)
        {
            int best = -1;
            float bestScore = float.MaxValue;
            Vector2 mid = new Vector2(Screen.width, Screen.height) * 0.5f;
            for (int i = 0; i < _level.Tiles.Length; i++)
            {
                var t = _level.Tiles[i];
                if (t.Kind != kind || (color != 0 && t.Color != color)) continue;
                float score = ((Vector2)Cam.WorldToScreenPoint(_board.CenterWorld(i)) - mid).sqrMagnitude + (Facing(i) ? 0f : Facing(i, 0.12f) ? 1e8f : 1e9f);
                if (score < bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        bool Facing(int tile, float min = 0.45f) => tile >= 0 && Vector3.Dot(_board.NormalWorld(tile), (Cam.transform.position - _board.CenterWorld(tile)).normalized) > min;

        /// <summary>The mixes in this level spelled out for the HUD tip line ("Purple = Red + Blue"), or null.</summary>
        static string MixFormula(int[] colors)
        {
            string s = null;
            foreach (int c in colors)
            {
                if (!IsMixed(c)) continue;
                int a = c & -c, b = c & ~a;
                string f = CubeColors.EnergyName(c) + " = " + CubeColors.EnergyName(a) + " + " + CubeColors.EnergyName(b);
                if (s == null) s = f;
                else if (!s.Contains(f)) s += "  ·  " + f;
            }
            return s;
        }
    }
}
