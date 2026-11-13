using CircuitCube.Core;
using UnityEngine;

namespace CircuitCube.Game
{
    /// <summary>
    /// First-time tutorial on Moonbase levels 1-3, one lesson each: tap a tile (level 1), spin the shape (level 2),
    /// use a hint (level 3, free). A pointer shows where to act and a bubble says what to do. Skip ends it for good;
    /// players who already beat these levels never see it. Progress is CubeSave.TutorialStep.
    /// </summary>
    public sealed partial class CircuitCubeApp
    {
        enum Lesson { None, Tap, Spin, Hint }
        const int TutorialDone = 3;

        Lesson _lesson;
        float _lessonTime;          // seconds since the lesson (or its current line) began
        float _lessonSpun;          // degrees the player turned the shape during the Spin lesson
        int _lessonTarget = -1;     // tile the pointer shows
        bool _lessonTapped;         // the player turned a tile during the Tap lesson
        bool _lessonWrapUp;         // the lesson is learned; its last line shows for a moment
        string _lessonText;

        void WireTutorial()
        {
            var tut = UI.Tutorial;
            if (tut == null) return;
            tut.gameObject.SetActive(false);
            tut.Skip.onClick.AddListener(() =>
            {
                GameAnalytics.Log("tutorial_step", ("step", LessonName(_lesson)), ("action", "skipped"));
                CubeSave.TutorialStep = TutorialDone;
                EndLesson();
            });
        }

        static string LessonName(Lesson l) => l == Lesson.Tap ? "tap" : l == Lesson.Spin ? "spin" : l == Lesson.Hint ? "hint" : "none";

        /// <summary>Pick the lesson for a level that just started (none for most levels).</summary>
        void StartLesson(int group, int number, bool cheat)
        {
            EndLesson();
            if (UI.Tutorial == null || group != 0 || number > 3) return;
            int done = CubeSave.TutorialStep;
            if (!cheat && (done >= number || CubeSave.Beaten(0) >= number)) return;
            _lesson = number == 1 ? Lesson.Tap : number == 2 ? Lesson.Spin : Lesson.Hint;
            _lessonTime = 0f;
            _lessonSpun = 0f;
            _lessonTapped = false;
            _lessonWrapUp = false;
            _lessonTarget = -1;
            _lessonText = null;
            UI.Hud.Tip.text = "";
            if (_lesson == Lesson.Tap) CubeSave.MarkSeenShape(0);   // the first level teaches by doing, not with a card
            GameAnalytics.Log("tutorial_step", ("step", LessonName(_lesson)), ("action", "shown"));
        }

        /// <summary>The current lesson is learned: save it and show its last line briefly.</summary>
        void LessonLearned(string lastLine)
        {
            if (_lesson == Lesson.None || _lessonWrapUp) return;
            GameAnalytics.Log("tutorial_step", ("step", LessonName(_lesson)), ("action", "done"));
            CubeSave.TutorialStep = Mathf.Max(CubeSave.TutorialStep, (int)_lesson);
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
            if (_lessonSpun >= 120f) LessonLearned("Now turn the tiles until every lamp lights up");
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
                case Lesson.Tap:
                {
                    int target = TapTarget();
                    if (target >= 0)
                    {
                        if (target != _lessonTarget) { _lessonTarget = target; _lessonTime = 0f; }
                        SetLessonText(_lessonTapped ? "Keep turning tiles until the lamp lights up" : "Tap this tile to turn it");
                        tut.Pointer.gameObject.SetActive(true);
                        tut.ResetPointer();
                        tut.PointAt(Cam.WorldToScreenPoint(_board.CenterWorld(target)));
                        tut.AnimateTap(_lessonTime);
                    }
                    else
                    {
                        // the tiles left to turn are round the back
                        _lessonTarget = -1;
                        SetLessonText("Drag to spin the shape and find the other tiles");
                        tut.Pointer.gameObject.SetActive(true);
                        tut.AnimateDrag(_lessonTime, new Vector2(0f, tut.Rect.rect.height * 0.04f), tut.Rect.rect.width * 0.45f);
                    }
                    break;
                }
                case Lesson.Spin:
                    SetLessonText("Drag to spin the shape and see its other sides");
                    tut.Pointer.gameObject.SetActive(true);
                    tut.AnimateDrag(_lessonTime, new Vector2(0f, tut.Rect.rect.height * 0.04f), tut.Rect.rect.width * 0.45f);
                    break;
                case Lesson.Hint:
                {
                    SetLessonText("Stuck? Tap the bulb for a free hint");
                    var hint = (RectTransform)UI.Hud.HintButton.transform;
                    bool canHint = UI.Hud.HintButton.interactable;
                    tut.Pointer.gameObject.SetActive(canHint);
                    if (canHint)
                    {
                        tut.ResetPointer();
                        tut.PointAt(RectTransformUtility.WorldToScreenPoint(tut.UiCamera, hint.TransformPoint(hint.rect.center)));
                        tut.AnimateTap(_lessonTime, false);   // a ring round the bulb, which stays visible
                    }
                    break;
                }
            }
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

        bool Facing(int tile) => Vector3.Dot(_board.NormalWorld(tile), (Cam.transform.position - _board.CenterWorld(tile)).normalized) > 0.45f;
    }
}
