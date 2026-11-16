using CircuitCube.Core;
using UnityEngine;

namespace CircuitCube.Game
{
    /// <summary>
    /// First-time lessons. A pointer shows where to look or act and a bubble says what to do; Skip ends the lesson.
    /// <list type="bullet">
    /// <item>Moonbase 1-3, one lesson each (progress is CubeSave.TutorialStep): level 1 first shows a source and a lamp
    /// (the goal), then "tap this tile", and says so when the first lamp lights; level 2 spin the shape; level 3 use a
    /// free hint. Players who already beat these levels never see them.</item>
    /// <item>Colours: the first level with lamps of two colours points at a lamp of each.</item>
    /// <item>Mix: the first level with a mixed lamp points at its two sources and the lamp ("Red + Blue = Purple");
    /// a later first level with another mix points at one lamp of it.</item>
    /// </list>
    /// The later lessons go by what is on the board, not by level number, so they fit whatever Level Lab publishes;
    /// they are remembered as bits in CubeSave.Tips. Levels started from the debug menu show every lesson again.
    /// </summary>
    public sealed partial class CircuitCubeApp
    {
        // numbers 1-3 are stored in CubeSave.TutorialStep: keep them
        enum Lesson { None, Tap, Spin, Hint, Colours, Mix }
        const int TutorialDone = 3;
        const float StepSeconds = 3.2f;   // how long a "look here" step stays before the next one

        Lesson _lesson;
        float _lessonTime;          // seconds since the lesson (or its current line) began
        float _lessonSpun;          // degrees the player turned the shape during the Spin lesson
        int _lessonTarget = -1;     // tile the pointer shows
        bool _lessonTapped;         // the player turned a tile during the Tap lesson
        bool _lessonWrapUp;         // the lesson is learned; its last line shows for a moment
        string _lessonText;
        int _lessonStep;            // step within a lesson that shows several things in turn
        bool _lessonLitShown;       // the Tap lesson has said "a lamp is lit"
        float _lessonLitTime = -1f; // seconds the "a lamp is lit" line has been up (-1 = not showing)
        int _lessonLitAtStart;      // lamps already lit when the level began (a lamp next to its source)
        int _lessonColorA, _lessonColorB, _lessonMix;   // colours the Colours / Mix lessons talk about
        bool _lessonMixFull;        // the first mix ever: show both sources, then the lamp

        void WireTutorial()
        {
            var tut = UI.Tutorial;
            if (tut == null) return;
            tut.gameObject.SetActive(false);
            tut.Skip.onClick.AddListener(() =>
            {
                GameAnalytics.Log("tutorial_step", ("step", LessonName(_lesson)), ("action", "skipped"));
                if (_lesson == Lesson.Colours) CubeSave.Tips |= CubeSave.TipColours;
                else if (_lesson == Lesson.Mix) MarkMixesSeen();
                else CubeSave.TutorialStep = TutorialDone;
                EndLesson();
            });
        }

        static string LessonName(Lesson l)
        {
            switch (l)
            {
                case Lesson.Tap: return "tap";
                case Lesson.Spin: return "spin";
                case Lesson.Hint: return "hint";
                case Lesson.Colours: return "colours";
                case Lesson.Mix: return "mix";
                default: return "none";
            }
        }

        /// <summary>Pick the lesson for a level that just started (none for most levels).</summary>
        void StartLesson(int group, int number, bool cheat)
        {
            EndLesson();
            if (UI.Tutorial == null) return;
            var lesson = PickLesson(group, number, cheat);
            if (lesson == Lesson.None) return;
            _lesson = lesson;
            _lessonTime = 0f;
            _lessonSpun = 0f;
            _lessonTapped = false;
            _lessonWrapUp = false;
            _lessonTarget = -1;
            _lessonText = null;
            _lessonStep = 0;
            _lessonLitShown = false;
            _lessonLitTime = -1f;
            _lessonLitAtStart = _board != null ? _board.LitLamps : 0;
            if (_lesson <= Lesson.Hint) UI.Hud.Tip.text = "";
            if (_lesson == Lesson.Tap) CubeSave.MarkSeenShape(0);   // the first level teaches by doing, not with a card
            if (_lesson == Lesson.Mix) CubeSave.SeenMix = true;      // the old text card is not needed any more
            GameAnalytics.Log("tutorial_step", ("step", LessonName(_lesson)), ("action", "shown"));
        }

        Lesson PickLesson(int group, int number, bool cheat)
        {
            if (group == 0 && number <= 3)
            {
                int done = CubeSave.TutorialStep;
                if (cheat || (done < number && CubeSave.Beaten(0) < number))
                    return number == 1 ? Lesson.Tap : number == 2 ? Lesson.Spin : Lesson.Hint;
            }
            // a mix the player has not been shown yet comes first
            int tips = cheat ? 0 : CubeSave.Tips;
            bool anyMixSeen = (tips & (CubeSave.TipMix(3) | CubeSave.TipMix(5) | CubeSave.TipMix(6))) != 0;
            foreach (int c in _level.Colors)
            {
                if (!IsMixed(c) || (tips & CubeSave.TipMix(c)) != 0) continue;
                _lessonMix = c;
                _lessonMixFull = !anyMixSeen;
                return Lesson.Mix;
            }
            if ((tips & CubeSave.TipColours) == 0)
            {
                _lessonColorA = _lessonColorB = 0;
                foreach (int c in _level.Colors)
                {
                    if (IsMixed(c)) continue;
                    if (_lessonColorA == 0) _lessonColorA = c;
                    else if (c != _lessonColorA) { _lessonColorB = c; break; }
                }
                if (_lessonColorB != 0) return Lesson.Colours;
            }
            return Lesson.None;
        }

        static bool IsMixed(int color) => (color & (color - 1)) != 0;

        void MarkMixesSeen()
        {
            int bits = CubeSave.Tips;
            foreach (int c in _level.Colors) bits |= CubeSave.TipMix(c);
            CubeSave.Tips = bits;
        }

        /// <summary>The current lesson is learned: save it and show its last line briefly.</summary>
        void LessonLearned(string lastLine)
        {
            if (_lesson == Lesson.None || _lessonWrapUp) return;
            GameAnalytics.Log("tutorial_step", ("step", LessonName(_lesson)), ("action", "done"));
            if (_lesson == Lesson.Colours) CubeSave.Tips |= CubeSave.TipColours;
            else if (_lesson == Lesson.Mix) CubeSave.Tips |= CubeSave.TipMix(_lessonMix);
            else CubeSave.TutorialStep = Mathf.Max(CubeSave.TutorialStep, (int)_lesson);
            if (lastLine == null) { EndLesson(); return; }
            _lessonWrapUp = true;
            _lessonTime = 0f;
            SetLessonText(lastLine);
        }

        void EndLesson()
        {
            _lesson = Lesson.None;
            _lessonWrapUp = false;
            if (UI.Tutorial != null) UI.Tutorial.gameObject.SetActive(false);
        }

        void SetLessonText(string text)
        {
            if (_lessonText == text) return;
            _lessonText = text;
            UI.Tutorial.Text.text = text;
        }

        /// <summary>The board turned by this many degrees under the player's finger.</summary>
        void LessonSpun(float degrees)
        {
            if (_lesson != Lesson.Spin || _lessonWrapUp) return;
            _lessonSpun += degrees;
            if (_lessonSpun >= 120f) LessonLearned(Loc.T("Now turn the tiles until every lamp lights up"));
        }

        /// <summary>A tile was turned by the player.</summary>
        void LessonTapped()
        {
            if (_lesson == Lesson.Tap) _lessonTapped = true;
        }

        /// <summary>The level was won while a lesson was on: the lesson counts as learned.</summary>
        void LessonOnWin()
        {
            if (_lesson == Lesson.None) return;
            if (!_lessonWrapUp) LessonLearned(null);
            EndLesson();
        }

        /// <summary>The first hint of the Hint lesson is free: no ad.</summary>
        bool LessonFreeHint => _lesson == Lesson.Hint && !_lessonWrapUp;

        void TickLesson(float dt)
        {
            var tut = UI.Tutorial;
            if (_lesson == Lesson.None || tut == null) return;
            bool show = _state == State.Playing && !_busy && !CardOpen && _board != null;
            if (tut.gameObject.activeSelf != show) tut.gameObject.SetActive(show);
            if (!show) return;
            _lessonTime += dt;

            if (_lessonWrapUp)
            {
                tut.Pointer.gameObject.SetActive(false);
                if (_lessonTime > 3f) EndLesson();
                return;
            }

            switch (_lesson)
            {
                case Lesson.Tap: TickTap(tut, dt); break;
                case Lesson.Spin:
                    SetLessonText(Loc.T("Drag to spin the shape and see its other sides"));
                    tut.Pointer.gameObject.SetActive(true);
                    tut.AnimateDrag(_lessonTime, new Vector2(0f, tut.Rect.rect.height * 0.04f), tut.Rect.rect.width * 0.45f);
                    break;
                case Lesson.Hint:
                {
                    SetLessonText(Loc.T("Stuck? Tap the bulb for a free hint"));
                    var hint = (RectTransform)UI.Hud.HintButton.transform;
                    bool canHint = UI.Hud.HintButton.interactable;
                    tut.Pointer.gameObject.SetActive(canHint);
                    if (canHint) PointAtUi(tut, hint, false);
                    break;
                }
                case Lesson.Colours:
                    if (_lessonStep == 0)
                        LookAt(tut, FindTile(TileKind.Lamp, _lessonColorA), Loc.F("Each lamp needs power of its own colour. This one needs {0}...", CubeColors.EnergyName(_lessonColorA)));
                    else if (_lessonStep == 1)
                        LookAt(tut, FindTile(TileKind.Lamp, _lessonColorB), Loc.F("...and this one needs {0}. Join each lamp to a source of its colour", CubeColors.EnergyName(_lessonColorB)));
                    else { LessonLearned(null); break; }
                    NextStepAfter(StepSeconds + 0.8f);
                    break;
                case Lesson.Mix:
                {
                    int a = _lessonMix & -_lessonMix, b = _lessonMix & ~a;   // the two colours that make the mix
                    string mix = CubeColors.EnergyName(_lessonMix), na = CubeColors.EnergyName(a), nb = CubeColors.EnergyName(b);
                    if (!_lessonMixFull) _lessonStep = Mathf.Max(_lessonStep, 2);
                    if (_lessonStep == 0) LookAt(tut, FindTile(TileKind.Source, a), Loc.F("Colours mix! This is {0} power...", na));
                    else if (_lessonStep == 1) LookAt(tut, FindTile(TileKind.Source, b), Loc.F("...and this is {0} power", nb));
                    else if (_lessonStep == 2)
                        LookAt(tut, FindTile(TileKind.Lamp, _lessonMix), na + " + " + nb + " = " + mix + ". " + Loc.F("A {0} lamp needs both on the same wire", mix.ToLowerInvariant()));
                    else { LessonLearned(null); break; }
                    NextStepAfter(_lessonStep == 2 ? StepSeconds + 1.5f : StepSeconds);
                    break;
                }
            }
        }

        void NextStepAfter(float seconds)
        {
            if (_lessonTime < seconds) return;
            _lessonStep++;
            _lessonTime = 0f;
        }

        /// <summary>
        /// Level 1: show a source and a lamp first (the goal), then point at tiles to turn; when the first lamp
        /// lights, say so and show the lamp count in the HUD for a moment.
        /// </summary>
        void TickTap(CircuitCube.UI.TutorialOverlay tut, float dt)
        {
            if (_lessonStep == 0 && !_lessonTapped)
            {
                LookAt(tut, FindTile(TileKind.Source, 0), Loc.T("This is a power source"));
                NextStepAfter(StepSeconds - 0.6f);
                return;
            }
            if (_lessonStep == 1 && !_lessonTapped)
            {
                LookAt(tut, FindTile(TileKind.Lamp, 0), Loc.T("Turn the tiles so the wires carry its power to every lamp"));
                NextStepAfter(StepSeconds + 0.4f);
                return;
            }
            if (!_lessonLitShown && _lessonTapped && _board.LitLamps > _lessonLitAtStart)
            {
                _lessonLitShown = true;
                _lessonLitTime = 0f;
            }
            if (_lessonLitTime >= 0f)
            {
                _lessonLitTime += dt;
                SetLessonText(Loc.T("A lamp is lit! Light every lamp to finish the level"));
                tut.Pointer.gameObject.SetActive(true);
                PointAtUi(tut, UI.Hud.LampDots, false);
                if (_lessonLitTime > StepSeconds) _lessonLitTime = -1f;
                return;
            }

            int target = TapTarget();
            if (target >= 0)
            {
                if (target != _lessonTarget) { _lessonTarget = target; _lessonTime = 0f; }
                SetLessonText(Loc.T(_lessonTapped ? "Keep turning tiles until every lamp lights up" : "Tap this tile to turn it"));
                tut.Pointer.gameObject.SetActive(true);
                tut.ResetPointer();
                tut.PointAt(Cam.WorldToScreenPoint(_board.CenterWorld(target)));
                tut.AnimateTap(_lessonTime);
            }
            else
            {
                // the tiles left to turn are round the back
                _lessonTarget = -1;
                SetLessonText(Loc.T("Drag to spin the shape and find the other tiles"));
                tut.Pointer.gameObject.SetActive(true);
                tut.AnimateDrag(_lessonTime, new Vector2(0f, tut.Rect.rect.height * 0.04f), tut.Rect.rect.width * 0.45f);
            }
        }

        /// <summary>Ring round a tile with a line of text; text only when the tile is round the back.</summary>
        void LookAt(CircuitCube.UI.TutorialOverlay tut, int tile, string text)
        {
            bool seen = tile >= 0 && Facing(tile, 0.12f);   // a tile on a slanted face still counts as seen
            SetLessonText(text);
            tut.Pointer.gameObject.SetActive(seen);
            if (!seen) return;
            tut.ResetPointer();
            tut.PointAt(Cam.WorldToScreenPoint(_board.CenterWorld(tile)));
            tut.AnimateTap(_lessonTime, false);   // a ring that keeps the tile visible
        }

        void PointAtUi(CircuitCube.UI.TutorialOverlay tut, RectTransform target, bool fingertip)
        {
            tut.ResetPointer();
            tut.PointAt(RectTransformUtility.WorldToScreenPoint(tut.UiCamera, target.TransformPoint(target.rect.center)));
            tut.AnimateTap(_lessonTime, fingertip);
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

        /// <summary>A tile still to turn that faces the camera, nearest the middle of the screen; -1 if none.</summary>
        int TapTarget()
        {
            if (_lessonTarget >= 0 && Facing(_lessonTarget) && Circuit.TapsToSolve(_level.Tiles[_lessonTarget]) > 0) return _lessonTarget;
            int best = -1;
            float bestD = float.MaxValue;
            Vector2 mid = new Vector2(Screen.width, Screen.height) * 0.5f;
            for (int i = 0; i < _level.Tiles.Length; i++)
            {
                var t = _level.Tiles[i];
                if (t.Locked || Circuit.TapsToSolve(t) == 0 || !Facing(i)) continue;
                float d = ((Vector2)Cam.WorldToScreenPoint(_board.CenterWorld(i)) - mid).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        bool Facing(int tile, float min = 0.45f) => Vector3.Dot(_board.NormalWorld(tile), (Cam.transform.position - _board.CenterWorld(tile)).normalized) > min;

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
