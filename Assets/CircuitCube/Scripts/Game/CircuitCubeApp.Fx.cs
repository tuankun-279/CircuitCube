using CircuitCube.Core;
using CircuitCube.UI;
using UnityEngine;

namespace CircuitCube.Game
{
    /// <summary>
    /// 1.2.0 feel: the win effects (Expert word, comet rain for Expert streaks, constellations for three-star streaks,
    /// a short praise), the lamp tray per colour, the mascot's moods and lines during play, and the sliding screen
    /// changes. The visuals live in <see cref="ResultFx"/>, <see cref="Mascot"/> and <see cref="LampGroup"/>.
    /// </summary>
    public sealed partial class CircuitCubeApp
    {
        enum WinFx { Stars, Expert, Comets, Constellation }

        const string ExpertStreakKey = "cc.expertStreak", StarStreakKey = "cc.starStreak";
        /// <summary>Expert wins in a row that bring the comet rain.</summary>
        public const int CometStreak = 2;
        /// <summary>Three-star (or Expert) wins in a row that bring the constellations.</summary>
        public const int ConstellationStreak = 3;

        /// <summary>Count the streaks for this win and pick its effect. Pure apart from the saved counters.</summary>
        WinFx StreakFor(int points)
        {
            bool expert = LevelScore.IsExpert(points), three = points >= GameData.Rules.parScore;
            int es = expert ? PlayerPrefs.GetInt(ExpertStreakKey, 0) + 1 : 0;
            int ss = three ? PlayerPrefs.GetInt(StarStreakKey, 0) + 1 : 0;
            if (!_cheat) { PlayerPrefs.SetInt(ExpertStreakKey, es); PlayerPrefs.SetInt(StarStreakKey, ss); }
            return PickFx(expert, three, es, ss);
        }

        public static WinFxKind PickFxKind(bool expert, bool three, int expertStreak, int starStreak) => (WinFxKind)(int)PickFx(expert, three, expertStreak, starStreak);

        static WinFx PickFx(bool expert, bool three, int expertStreak, int starStreak)
        {
            if (expert && expertStreak >= CometStreak) return WinFx.Comets;
            if (expert) return WinFx.Expert;
            if (three && starStreak >= ConstellationStreak) return WinFx.Constellation;
            return WinFx.Stars;
        }

        static string[] PraiseFor(WinFx fx, int points)
        {
            switch (fx)
            {
                case WinFx.Comets: return Praise.ExpertStreak;
                case WinFx.Expert: return Praise.Expert;
                case WinFx.Constellation: return Praise.StarStreak;
                default: return points >= 6 ? Praise.ThreeStars : Praise.Encourage;
            }
        }

        /// <summary>A world point of the 3D board as a world point on the UI layer.</summary>
        bool BoardToUi(Vector3 world, RectTransform layer, out Vector3 ui)
        {
            var canvas = layer.GetComponentInParent<Canvas>().rootCanvas;
            var uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 screen = Cam.WorldToScreenPoint(world);
            return RectTransformUtility.ScreenPointToWorldPointInRectangle(layer, screen, uiCam, out ui);
        }

        readonly Vector3[] _lampWorld = new Vector3[32];

        /// <summary>Constellations start at the win (the lamps are still there), before the box breaks.</summary>
        readonly int[] _lampColor = new int[32];

        /// <summary>Three-star streak, from the moment the box breaks (see <see cref="ResultFx.PlayConstellations"/>).</summary>
        void StartConstellations()
        {
            var fx = UI.Fx;
            int n = _board.LampPoints(_lampWorld, _lampColor, Cam.transform.position);
            for (int i = 0; i < n; i++) BoardToUi(_lampWorld[i], fx.Layer, out _lampWorld[i]);
            var row = UI.Result.Stars;
            row.Set(0);
            var slots = new RectTransform[row.Stars.Length];
            for (int i = 0; i < slots.Length; i++) slots[i] = row.Stars[i].rectTransform;
            var panel = (RectTransform)UI.Result.transform.Find("Panel");
            _starsInFlight = 3;
            fx.PlayConstellations(_lampWorld, _lampColor, n, panel, slots, k =>
            {
                _starsInFlight = Mathf.Max(0, _starsInFlight - 1);
                if (!UI.Result.gameObject.activeSelf) return;
                row.Set(k + 1);
                GameAudio.Play(Sfx.Star, 1f + 0.12f * k);
                // the mockup's star pop: from 2.4 times its size, cubic-bezier(.2,1.6,.4,1), 0.35 s
                var pop = row.Stars[k].transform;
                _tw.Run(0.35f, p => pop.localScale = Vector3.one * Mathf.LerpUnclamped(2.4f, 1f, ResultFx.Bezier(0.2f, 1.6f, 0.4f, 1f, p)), () => pop.localScale = Vector3.one, pop);
            }, null);
        }

        /// <summary>One Expert: the letters leave the broken box 0.2 s apart and land in the card.</summary>
        void StartExpertWord()
        {
            var fx = UI.Fx;
            if (!BoardToUi(_board.transform.position, fx.Layer, out var core)) return;
            fx.PlayExpert(core, 0.2f, 1.05f, k => GameAudio.Play(Sfx.Star, 1f + 0.08f * k), null);
        }

        /// <summary>Expert streak: the comets land as the letters once the card is up.</summary>
        void StartComets()
        {
            var fx = UI.Fx;
            var mascot = UI.Result.Art != null ? UI.Result.Art.rectTransform : null;
            fx.PlayComets(mascot, k => { GameAudio.Play(Sfx.Star, 1f + 0.08f * k); Haptics.Play(Haptics.Kind.Lamp); }, null);
        }

        // ---------------------------------------------------------------- lamp tray

        readonly int[] _litBy = new int[8], _totBy = new int[8];

        void SetupLampTray()
        {
            var hud = UI.Hud;
            if (hud.LampTray == null) return;
            UpdateLampTray(true);
        }

        void UpdateLampTray(bool force = false)
        {
            var hud = UI.Hud;
            if (hud.LampTray == null || _board == null) return;
            _board.LampCounts(_litBy, _totBy);
            int n = 0;
            for (int c = 1; c < 8; c++)
            {
                if (_totBy[c] == 0) continue;
                var g = Slot(hud.LampTray, hud.GroupPrefab, ref n);
                if (force) g.Forget();
                bool mixed = IsMixed(c);
                int a = c & -c, b = c & ~a;
                g.Set(CubeColors.Energy(c), CubeColors.EnergyName(c), _litBy[c], _totBy[c],
                    mixed ? CubeColors.Energy(a) : Color.clear, mixed ? CubeColors.Energy(b) : Color.clear, mixed);
            }
            HideRest(hud.LampTray, n);
        }

        // ---------------------------------------------------------------- mascot

        float _lastProgress, _lastLine;
        int _litForLine;
        bool _saidNearly, _saidOverPar;
        const float StuckSeconds = 25f, LineGap = 14f;

        /// <summary>A level starts: the mascot's timers start over (and the next menu comes in without a slide).</summary>
        void ResetMascot()
        {
            _menuShown = null;
            _lastProgress = _lastLine = Clock.Now;
            _litForLine = 0;
            _saidNearly = _saidOverPar = false;
        }

        /// <summary>A lamp just lit: hop, and now and then a line.</summary>
        void MascotOnLamp(int color)
        {
            var m = UI.Hud.Mascot;
            if (m == null) return;
            _lastProgress = Clock.Now;
            _board.LampCounts(_litBy, _totBy);
            int left = _board.TotalLamps - _board.LitLamps;
            string line = null;
            var mood = Mascot.Mood.Happy;
            if (left > 0 && left <= 2 && !_saidNearly) { _saidNearly = true; mood = Mascot.Mood.Excited; line = Praise.Pick(Praise.NearlyDone); }
            else if (color > 0 && color < 8 && _totBy[color] > 1 && _litBy[color] == _totBy[color]) line = Loc.F("{0} done!", CubeColors.EnergyName(color));
            else if (_board.LitLamps == 1 || ++_litForLine >= 2) { _litForLine = 0; line = Praise.Pick(Praise.Lit); }
            if (line != null) { _lastLine = Clock.Now; _litForLine = 0; }
            m.Say(mood, line);
        }

        /// <summary>Every frame in a level: a calm tip when nothing has lit for a while or the moves run far past par.</summary>
        void TickMascot()
        {
            var m = UI.Hud.Mascot;
            if (m == null || _state != State.Playing || CardOpen || _level == null) return;
            float now = Clock.Now;
            if (now - _lastLine < LineGap) return;
            int par = LevelScore.ExpertLimit(_level.Par, GameData.Rules, GameData.Group(_group));
            bool stuck = now - _lastProgress > StuckSeconds;
            bool far = !_saidOverPar && _moves > par + Mathf.Max(6, par / 3);
            if (!stuck && !far) return;
            if (far) _saidOverPar = true;
            _lastLine = _lastProgress = now;
            m.Say(Mascot.Mood.Think, Praise.Pick(Praise.Cheer));
        }

        // ---------------------------------------------------------------- screen changes (slide with parallax)

        UIScreen _menuShown;
        static int Depth(UIScreen s, UIRoot ui) => s == ui.Home ? 0 : s == ui.Shapes ? 1 : s == ui.Levels ? 2 : -1;

        /// <summary>Slide from the menu screen shown before to <paramref name="to"/>: deeper screens come in from the right.</summary>
        void SlideIn(UIScreen to)
        {
            var from = _menuShown;
            _menuShown = to;
            if (from == null || from == to || !from.gameObject.scene.IsValid()) return;
            int a = Depth(from, UI), b = Depth(to, UI);
            if (a < 0 || b < 0) return;
            float dir = b > a ? 1f : -1f;
            var fr = (RectTransform)from.transform; var tr = (RectTransform)to.transform;
            var parent = (RectTransform)tr.parent;
            float w = parent.rect.width;
            var fcg = from.GetComponent<CanvasGroup>();
            var back = Backdrop;
            Vector2 backHome = back != null ? back.anchoredPosition : Vector2.zero;
            from.gameObject.SetActive(true);
            _tw.Run(0.42f, p =>
            {
                fr.anchoredPosition = new Vector2(-dir * w * 0.35f * p, 0f);
                if (fcg != null) fcg.alpha = 1f - p;
                tr.anchoredPosition = new Vector2(dir * w * (1f - p), 0f);
                if (back != null) back.anchoredPosition = backHome + new Vector2(-dir * 28f * Mathf.Sin(p * Mathf.PI), 0f);
            }, () =>
            {
                fr.anchoredPosition = Vector2.zero;
                if (fcg != null) fcg.alpha = 1f;
                if (_menuShown != from) from.gameObject.SetActive(false);
                tr.anchoredPosition = Vector2.zero;
                if (back != null) back.anchoredPosition = backHome;
            }, to);
        }

        RectTransform _backdrop;
        RectTransform Backdrop
        {
            get
            {
                if (_backdrop == null) { var b = FindFirstObjectByType<ThemeBackdrop>(FindObjectsInactive.Exclude); if (b != null) _backdrop = (RectTransform)b.transform; }
                return _backdrop;
            }
        }
    }

    public enum WinFxKind { Stars, Expert, Comets, Constellation }
}
