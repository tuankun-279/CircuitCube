using System.Collections.Generic;
using CircuitCube.Core;
using CircuitCube.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CircuitCube.Game
{
    /// <summary>
    /// Game flow: Home → Shapes → Levels → Play → Result. The scene (Scenes/CircuitCube.unity) holds the camera, the
    /// event system, the background and the UI prefab; this component only drives them. No UI is created or drawn
    /// here: screens, cards, the Tile prefab and their animations are all assets set up in the editor.
    /// </summary>
    public sealed partial class CircuitCubeApp : MonoBehaviour
    {
        enum State { Home, Shapes, Levels, Playing, Won }

        public static int HintsPerLevel => GameData.Rules.hintsPerLevel;
        public static int UndosPerLevel => GameData.Rules.undosPerLevel;

        [Header("Scene")]
        public Camera Cam;
        public UIRoot UI;
        [Header("Board")]
        [Tooltip("Themes/BoardLook.asset, built from the Tile prefabs (Tools/Circuit Cube/Board/Build Board Look).")]
        public BoardLook Look;

        [Header("Look")]
        [Tooltip("Global bloom volume (scene object Bloom, built by Tools/Circuit Cube/Board/Build Bloom). On only in the dark theme: on light backgrounds it washes the board out.")]
        public UnityEngine.Rendering.Volume BloomVolume;
        [Tooltip("Lowest quality level (Edit > Project Settings > Quality, 0 = first) that gets bloom; lower levels keep the sprite glow only.")]
        public int MinBloomQuality = 2;
        [Tooltip("After the box starts to break, how long until the result card slides in (seconds).")]
        public float ResultDelay = 0.3f;
        [Tooltip("Themes/Theme_Dark.asset")] public GameTheme DarkTheme;
        [Tooltip("Themes/Theme_Light.asset")] public GameTheme LightTheme;

        [Header("Level data")]
        [Tooltip("CDN root with manifest.json, groups/, orders/, levels/. Empty = only the data shipped in the build.")]
        public string ContentBaseUrl = "";

        [Header("Camera framing")]
        [Range(0.3f, 1f)] public float PlayScreenShare = 0.72f;
        [Range(0.3f, 1f)] public float HomeScreenShare = 0.55f;

        [Header("Zoom")]
        [Tooltip("How far a pinch (or the mouse wheel) can zoom into the board, against the framed view.")]
        [Range(1.5f, 5f)] public float MaxZoom = 3f;
        [Tooltip("Two taps on empty space within this many seconds zoom back out.")]
        public float DoubleTapTime = 0.32f;

        readonly Tweener _tw = new Tweener();
        State _state;
        int _group;

        CubeLevel _level;
        CubeBoardView _board;
        int _moves, _hintsLeft, _hintsUsed, _undosLeft, _undosUsed;
        float _levelStartTime;
        bool _busy, _cheat;
        System.Action _tipClosed;
        readonly Stack<int> _undo = new Stack<int>();   // tiles the player turned, newest first
        int _unlockedGroup = -1;                       // a shape the last win unlocked, offered after the result card

        // Input
        bool _pressed, _dragging, _overUi;
        Vector2 _pressPos, _lastPos;
        Vector2 _spin;
        float _dragThreshold;
        readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        PointerEventData _ped;

        // Zoom: a narrower field of view plus a sideways camera shift (pan), both on top of the framed view
        float _baseFov, _zoom = 1f;
        Vector2 _pan;                 // camera offset from the framed position, world units in the camera plane
        Vector3 _camFramed;           // camera position FitCamera chose
        bool _pinching, _zoomLogged, _rightPanning;
        float _pinchDist, _lastEmptyTap = -1f;
        Vector2 _pinchMid, _lastEmptyTapPos, _rightLast;

        void Awake()
        {
            Application.targetFrameRate = 60;
            _dragThreshold = Mathf.Max(10f, Screen.dpi > 0 ? Screen.dpi * 0.06f : 10f);
            if (Cam == null) Cam = Camera.main;
            _baseFov = Cam.fieldOfView;
            ApplyTheme(CubeSave.Settings.theme);

            var home = UI.Home;
            home.PlayButton.onClick.AddListener(() => StartLevel(CubeSave.CurrentGroup, CubeSave.Unlocked(CubeSave.CurrentGroup)));
            home.ShapesButton.onClick.AddListener(ShowShapes);
            home.LevelsButton.onClick.AddListener(() => ShowLevels(CubeSave.CurrentGroup));
            home.SettingsButton.onClick.AddListener(() => OpenSettings(false));
            UI.Shapes.BackButton.onClick.AddListener(Back);
            UI.Levels.BackButton.onClick.AddListener(Back);
            var hud = UI.Hud;
            hud.BackButton.onClick.AddListener(() => { if (_state == State.Playing && !_busy && !CardOpen) OpenSettings(true); });
            hud.RestartButton.onClick.AddListener(() =>
            {
                if (_state != State.Playing || _busy || CardOpen) return;
                GameAnalytics.Log("level_restart", ("shape", GameData.Group(_group).id), ("level", _level.Number), ("moves", _moves));
                StartLevel(_group, _level.Number);
            });
            hud.HintButton.onClick.AddListener(UseHint);
            hud.UndoButton.onClick.AddListener(Undo);
            hud.HelpButton.onClick.AddListener(() => { if (_state == State.Playing && !CardOpen) ShowHowTo(); });
            if (hud.ZoomResetButton != null) hud.ZoomResetButton.onClick.AddListener(() => ResetZoom(true));

            var r = UI.Result;
            r.Primary.onClick.AddListener(NextLevel);
            r.SecondaryA.onClick.AddListener(() => StartLevel(_group, _level.Number));
            r.SecondaryB.onClick.AddListener(ShowHome);
            UI.Tip.Primary.onClick.AddListener(CloseTip);

            var u = UI.Unlock;
            u.Primary.onClick.AddListener(() =>
            {
                int g = _unlockedGroup;
                _unlockedGroup = -1;
                CubeSave.MarkSeenShape(g);   // the card already introduced it
                StartLevel(g, 1);
            });
            u.SecondaryA.onClick.AddListener(() => { _unlockedGroup = -1; NextLevel(); });

            var st = UI.Settings;
            st.Close.onClick.AddListener(CloseSettings);
            st.Resume.onClick.AddListener(CloseSettings);
            st.Restart.onClick.AddListener(() => { CloseSettings(); StartLevel(_group, _level.Number); });
            st.Home.onClick.AddListener(() => { CloseSettings(); SaveInProgress(); ShowHome(); });
            st.ThemePicked += SetTheme;
            if (st.Privacy != null) st.Privacy.onClick.AddListener(() => RewardedAds.ShowPrivacyOptions());
            WireTutorial();
        }

        void Start()
        {
            GameAnalytics.Init();
            RewardedAds.Init();
            HideAll();
            UI.Splash.Show();
            UI.Splash.SetProgress(0f);
            UI.Splash.Status.text = "Loading levels...  ·  v" + Application.version;
            GameData.Reload();
            // the data loads in one go; the bar just gives the logo a moment on screen
            _tw.Run(0.9f, p => UI.Splash.SetProgress(Tweener.Ease(p)), () => { if (UI.Splash.gameObject.activeSelf) ShowHome(); });
            if (!string.IsNullOrEmpty(ContentBaseUrl))
                StartCoroutine(RemoteContent.Sync(ContentBaseUrl, CubeSave.Beaten, () =>
                {
                    if (_state == State.Playing || _state == State.Won) return;
                    DebugRefresh();
                    if (RemoteContent.NeedsUpdate && !CardOpen) ShowUpdateTip();
                }));
        }

        void OnApplicationPause(bool paused) { if (paused) SaveInProgress(); }

        // the player may switch the phone between light and dark while the game is in the background
        void OnApplicationFocus(bool focus)
        {
            if (focus && CubeSave.Settings != null && CubeSave.Settings.theme == "system" && Themes.Current != null && Themes.IsDark != SystemTheme.IsDark())
                SetTheme("system");
        }
        void OnApplicationQuit() => SaveInProgress();

        /// <summary>Remember the board of the level being played, so it continues where it stopped.</summary>
        void SaveInProgress()
        {
            if (_state != State.Playing || _level == null || _cheat) return;
            int serial = GameData.SerialAt(_group, _level.Number);
            if (serial <= 0) return;
            var ip = new InProgress { serial = serial, position = _level.Number, moves = _moves, hintsUsed = _hintsUsed, undosUsed = _undosUsed };
            var sb = new System.Text.StringBuilder(_level.Tiles.Length);
            for (int i = 0; i < _level.Tiles.Length; i++)
            {
                var t = _level.Tiles[i];
                sb.Append((char)('0' + ((t.Rot % t.Ports) + t.Ports) % t.Ports));
                if (t.Locked) ip.hintedTiles.Add(i);
            }
            ip.rotations = sb.ToString();
            CubeSave.SetInProgress(_group, ip);
        }

        void RestoreInProgress()
        {
            int serial = GameData.SerialAt(_group, _level.Number);
            var ip = CubeSave.GetInProgress(_group);
            if (serial <= 0 || ip == null || ip.serial != serial || ip.rotations == null || ip.rotations.Length != _level.Tiles.Length) return;
            for (int i = 0; i < _level.Tiles.Length; i++) _level.Tiles[i].Rot = ip.rotations[i] - '0';
            foreach (int i in ip.hintedTiles) if (i >= 0 && i < _level.Tiles.Length) _level.Tiles[i].Locked = true;
            _moves = ip.moves;
            _hintsUsed = ip.hintsUsed;
            _hintsLeft = Mathf.Max(0, HintsPerLevel - _hintsUsed);
            _undosUsed = ip.undosUsed;
            _undosLeft = Mathf.Max(0, UndosPerLevel - _undosUsed);
        }

        // ---------------- Screens ----------------

        void HideAll()
        {
            UI.Splash.Hide(); UI.Home.Hide(); UI.Shapes.Hide(); UI.Levels.Hide(); UI.Hud.Hide();
            CloseCards();
        }

        void CloseCards()
        {
            UI.Result.gameObject.SetActive(false);
            UI.Tip.gameObject.SetActive(false);
            UI.Unlock.gameObject.SetActive(false);
            UI.Settings.gameObject.SetActive(false);
            UI.Dim.SetActive(false);
            if (UI.FlyingStars != null) foreach (var st in UI.FlyingStars) if (st != null) st.gameObject.SetActive(false);
            _starsInFlight = 0;
        }

        bool CardOpen => UI.Result.gameObject.activeSelf || UI.Tip.gameObject.activeSelf
                         || UI.Unlock.gameObject.activeSelf || UI.Settings.gameObject.activeSelf;

        void OpenSettings(bool paused)
        {
            GameAudio.Play(Sfx.PanelOpen);
            UI.Dim.SetActive(true);
            UI.Settings.Open(paused);
        }

        void CloseSettings()
        {
            if (UI.Settings.gameObject.activeSelf) GameAudio.Play(Sfx.PanelClose);
            UI.Settings.gameObject.SetActive(false);
            if (!UI.Result.gameObject.activeSelf && !UI.Tip.gameObject.activeSelf) UI.Dim.SetActive(false);
        }

        /// <summary>The shape icon of a group in the current theme (from the ShapeCard prefab's icon lists).</summary>
        Sprite GroupIcon(int g)
        {
            var info = GameData.Group(g);
            return UI.Shapes.CardPrefab.IconFor(string.IsNullOrEmpty(info.icon) ? info.id : info.icon);
        }

        void ShowHome()
        {
            _state = State.Home;
            _busy = false;
            GameAudio.Music(MusicTrack.Menu);
            _tw.Clear();
            HideAll();
            int g = CubeSave.CurrentGroup;
            // A solved decorative shape of the current group spins behind the title. It is not a real level.
            int lg = Mathf.Max(0, System.Array.IndexOf(LevelPlan.GroupIds, GameData.GroupId(g)));
            var topo = LevelPlan.TopologyFor(lg, lg == 0 ? 4 : 1);
            var demo = CubeGenerator.Generate(0, topo, new[] { 1, 2, 4 }, 424242u, 0);
            foreach (var t in demo.Tiles) t.Rot = 0;
            SetBoard(demo, false, HomeScreenShare);
            _board.transform.rotation = StartRotation(topo, -24f, 35f);
            _spin = new Vector2(14f, 0f);

            var h = UI.Home;
            h.Show();
            var info = GameData.Group(g);
            int total = Mathf.Max(1, info.levelCount);
            int next = CubeSave.Unlocked(g);
            h.PlayLabel.text = "Play";
            h.GroupLabel.text = info.name;
            h.StarsLabel.text = CubeSave.TotalStars.ToString();
            h.GroupName.text = info.name;
            h.GroupProgress.text = info.endless ? "Level " + next : "Level " + Mathf.Min(next, total) + " / " + total;
            h.GroupFill.anchorMax = new Vector2(info.endless ? 1f : Mathf.Clamp01(CubeSave.Beaten(g) / (float)total), h.GroupFill.anchorMax.y);
            var icon = GroupIcon(g);
            if (icon != null) h.GroupIcon.sprite = icon;
            bool anyNew = false;
            for (int i = 0; i < GameData.GroupCount; i++) anyNew |= CubeSave.HasNewLevels(i);
            if (h.ShapesBadge != null) h.ShapesBadge.SetActive(anyNew);
        }

        void ShowShapes()
        {
            _state = State.Shapes;
            HideAll();
            GameAudio.Music(MusicTrack.Menu);
            var s = UI.Shapes;
            s.Show();
            s.StarsLabel.text = CubeSave.TotalStars.ToString();
            int next = 0;
            for (int g = 0; g < GameData.GroupCount; g++)
            {
                var info = GameData.Group(g);
                int group = g;
                bool open = CubeSave.GroupOpen(g);
                var rule = GameData.Unlock(g);
                int pre = GameData.IndexOf(rule.group);
                string hint = rule.IsNone || pre < 0 ? "" : "Beat level " + rule.cleared + " of " + GameData.Group(pre).name;
                int total = Mathf.Max(1, info.levelCount);
                var card = Slot(s.List, s.CardPrefab, ref next);
                string iconId = string.IsNullOrEmpty(info.icon) ? info.id : info.icon;
                if (card.IconFor(iconId) != null) card.Set(iconId, info.name, info.blurb, open, info.endless ? CubeSave.Beaten(g) : Mathf.Min(CubeSave.Beaten(g), total),
                    total, info.endless, hint, () => ShowLevels(group));
                else card.Set(IconIndex(g), info.name, info.blurb, open, info.endless ? CubeSave.Beaten(g) : Mathf.Min(CubeSave.Beaten(g), total),
                    total, info.endless, hint, () => ShowLevels(group));
                card.SetNew(CubeSave.HasNewLevels(g));
            }
            HideRest(s.List, next);
        }

        /// <summary>
        /// The next child of a list to fill, reusing the ones made for an earlier screen before making a new copy, so
        /// opening a screen again does not rebuild it. <paramref name="skip"/> is a template child to leave alone.
        /// </summary>
        static T Slot<T>(Transform parent, T prefab, ref int next, Transform skip = null) where T : Component
        {
            while (next < parent.childCount)
            {
                var c = parent.GetChild(next++);
                if (c == skip) continue;
                c.gameObject.SetActive(true);
                return c.GetComponent<T>();
            }
            var made = Instantiate(prefab, parent);
            made.gameObject.SetActive(true);
            next = parent.childCount;
            return made;
        }

        /// <summary>Hide the list children left over after <paramref name="next"/>.</summary>
        static void HideRest(Transform parent, int next, Transform skip = null)
        {
            for (int i = next; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c != skip) c.gameObject.SetActive(false);
            }
        }

        /// <summary>Card art is in generator group order; data groups pick theirs by id.</summary>
        static int IconIndex(int g)
        {
            int i = System.Array.IndexOf(LevelPlan.GroupIds, GameData.Group(g).icon);
            if (i < 0) i = System.Array.IndexOf(LevelPlan.GroupIds, GameData.GroupId(g));
            return Mathf.Max(0, i);
        }

        void ShowLevels(int group)
        {
            _state = State.Levels;
            _group = group;
            HideAll();
            GameAudio.Music(MusicTrack.Menu);
            CubeSave.MarkLevelsSeen(group);
            var lv = UI.Levels;
            lv.Show();
            var info = GameData.Group(group);
            lv.Title.text = info.name;
            int unlocked = CubeSave.Unlocked(group);
            int total = Mathf.Max(1, info.levelCount);
            lv.Subtitle.text = info.blurb;   // hidden in the V2 layout; kept for older prefabs
            lv.StarsLabel.text = CubeSave.TotalStars.ToString();
            int beaten = CubeSave.Beaten(group), groupStars = 0;
            for (int l = 1; l <= beaten; l++) groupStars += CubeSave.Stars(group, l);
            lv.ProgressValue.text = (info.endless ? beaten.ToString() : Mathf.Min(beaten, total) + " / " + total) + "  ·  " + groupStars + (groupStars == 1 ? " star" : " stars");
            lv.ProgressFill.anchorMax = new Vector2(info.endless ? 1f : Mathf.Clamp01(beaten / (float)total), lv.ProgressFill.anchorMax.y);
            int next = 0;
            int count = info.endless ? Mathf.Max(total, ((unlocked + 11) / 4 + 1) * 4) : total;
            for (int l = 1; l <= count; l++)
            {
                int level = l;
                var node = Slot(lv.Grid, lv.NodePrefab, ref next);
                node.Set(l, l <= unlocked, CubeSave.Stars(group, l), () => StartLevel(group, level), l == unlocked);
            }
            HideRest(lv.Grid, next);
            // Scroll so the next level to play is in view.
            Canvas.ForceUpdateCanvases();
            if (lv.Scroll != null)
                lv.Scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01((unlocked - 8) / (float)Mathf.Max(1, count - 8));
        }

        void Back()
        {
            if (UI.Settings.gameObject.activeSelf) { CloseSettings(); return; }
            if (UI.Tip.gameObject.activeSelf) { CloseTip(); return; }
            switch (_state)
            {
                case State.Playing: if (!_busy) OpenSettings(true); break;
                case State.Won: ShowLevels(_group); break;
                case State.Levels: ShowShapes(); break;
                case State.Shapes: ShowHome(); break;
                default: Application.Quit(); break;
            }
        }

        void NextLevel()
        {
            if (_unlockedGroup >= 0)
            {
                // offer the new shape first; "Later" comes back here with the offer cleared
                UI.Result.gameObject.SetActive(false);
                var u = UI.Unlock;
                var ng = GameData.Group(_unlockedGroup);
                u.Caption.text = "NEW SHAPE UNLOCKED";
                u.Title.text = ng.name;
                u.Body.text = ng.blurb;
                var icon = GroupIcon(_unlockedGroup);
                if (icon != null) u.Art.sprite = icon;
                UI.Dim.SetActive(true);
                GameAudio.Play(Sfx.Unlock);
                u.Open();
                return;
            }
            var info = GameData.Group(_group);
            if (!info.endless && _level.Number >= info.levelCount)
            {
                for (int g = 0; g < GameData.GroupCount; g++)
                    if (g != _group && CubeSave.GroupOpen(g) && CubeSave.Beaten(g) < GameData.LevelCount(g)) { StartLevel(g, CubeSave.Unlocked(g)); return; }
                ShowShapes();
                return;
            }
            StartLevel(_group, _level.Number + 1);
        }

        void StartLevel(int group, int number) => StartLevel(group, number, false);

        /// <param name="cheat">Debug menu: play any level of any group without unlocking it first.</param>
        void StartLevel(int group, int number, bool cheat)
        {
            int max = GameData.MaxLevel(group);
            if (cheat) number = Mathf.Clamp(number, 1, max);
            else
            {
                if (!CubeSave.GroupOpen(group)) group = 0;
                number = Mathf.Clamp(Mathf.Min(number, CubeSave.Unlocked(group)), 1, max);
            }
            _group = group;
            CubeSave.CurrentGroup = group;
            _tw.Clear();
            var level = GameData.Build(group, number);
            if (level == null)
            {
                // Past the levels downloaded to this device and no connection to fetch more.
                ShowLevels(group);
                if (RemoteContent.NeedsUpdate) ShowUpdateTip();
                else ShowTip("More levels need the internet",
                    "Levels arrive ten at a time. Connect to the internet and open the game again to unlock the next ones.", "OK", null);
                return;
            }
            _cheat = cheat;
            _level = level;
            _moves = 0;
            _hintsUsed = 0;
            _hintsLeft = HintsPerLevel;
            _undosUsed = 0;
            _undosLeft = UndosPerLevel;
            _undo.Clear();
            _unlockedGroup = -1;
            _shownMoves = _shownPar = _shownLit = _shownTotal = _shownHints = _shownUndos = -1;
            if (!cheat) RestoreInProgress();
            _levelStartTime = Time.realtimeSinceStartup;
            _zoomLogged = false;
            GameAnalytics.Log("level_start", ("shape", GameData.Group(group).id), ("level", number), ("resumed", _moves > 0));
            _busy = false;
            _state = State.Playing;
            HideAll();
            GameAudio.Music(MusicTrack.Play);
            SetBoard(_level, true, PlayScreenShare);
            _board.transform.rotation = StartRotation(_level.Topo, -28f, 38f);
            _spin = Vector2.zero;

            var hud = UI.Hud;
            hud.Show();
            hud.Title.text = GameData.Group(group).name.ToUpperInvariant() + "  " + number;
            hud.Tip.text = (group == 0 && number <= 3) || !CubeSave.SeenHowTo ? "Drag to turn the shape  ·  Tap a tile to rotate it" : "";
            int chips = 0;
            var seen = new HashSet<int>();
            foreach (int c in _level.Colors)
            {
                if (!seen.Add(c)) continue;
                Slot(hud.Legend, hud.ChipPrefab, ref chips).Set(CubeColors.Energy(c), CubeColors.EnergyName(c));
            }
            HideRest(hud.Legend, chips);
            // one dot per lamp, copied from the template dot in the prefab
            var template = hud.DotTemplate.transform;
            hud.DotTemplate.gameObject.SetActive(false);
            int dots = 0;
            for (int i = 0; i < _board.TotalLamps; i++) Slot(hud.LampDots, hud.DotTemplate, ref dots, template);
            HideRest(hud.LampDots, dots, template);
            UpdateHud();
            StartLesson(group, number, cheat);

            if (!CubeSave.SeenShape(group))
            {
                var info = GameData.Group(group);
                ShowTip("New shape: " + info.name, info.blurb + "\n\nThe rules are the same: turn tiles until every lamp gets exactly its colour.", "Let's go",
                    () => CubeSave.MarkSeenShape(group));
            }
            else if (GameData.GroupId(group) == "cube" && number == LevelPlan.MixIntroLevel && !CubeSave.SeenMix)
            {
                ShowTip("Colours mix!",
                    "When two power sources share one wire, their colours combine.\n\nRed + Blue = Purple\nRed + Yellow = Orange\nBlue + Yellow = Green\n\nA lamp lights only when it gets exactly its colour.",
                    "Got it", () => CubeSave.SeenMix = true);
            }
        }

        void ShowUpdateTip() => ShowTip("Update to play more",
            "New levels are out, and they need the latest version of Circuit Cube. Update the game from the store to play them.", "OK", null);

        void ShowTip(string title, string body, string button, System.Action onClose)
        {
            var t = UI.Tip;
            t.Title.text = title;
            t.Body.text = body;
            t.PrimaryLabel.text = button;
            _tipClosed = onClose;
            UI.Dim.SetActive(true);
            GameAudio.Play(Sfx.PanelOpen);
            t.Open();
        }

        void CloseTip()
        {
            if (UI.Tip.gameObject.activeSelf) GameAudio.Play(Sfx.PanelClose);
            UI.Tip.gameObject.SetActive(false);
            if (!UI.Result.gameObject.activeSelf) UI.Dim.SetActive(false);
            var cb = _tipClosed; _tipClosed = null;
            cb?.Invoke();
        }

        /// <summary>A first view that shows three sides. A tetrahedron is turned so one corner points at the camera.</summary>
        static Quaternion StartRotation(BoardTopology topo, float pitch, float yaw)
        {
            if (topo.Count > 0 && topo.Cells[0].Ports == 3)
                return Quaternion.Euler(-12f, 0f, 0f) * Quaternion.FromToRotation(new Vector3(1f, 1f, 1f).normalized, Vector3.back);
            return Quaternion.Euler(pitch, yaw, 0f);
        }

        void SetBoard(CubeLevel level, bool interactive, float share)
        {
            if (_board != null) Destroy(_board.gameObject);
            var go = new GameObject("Board");
            _board = go.AddComponent<CubeBoardView>();
            _board.Build(level, Look, Cam, interactive);
            FitCamera(_board.Radius, share, interactive);
        }

        float _fitRadius, _fitShare;
        bool _fitPlaying;
        Vector2Int _fitScreen;

        /// <summary>Frame the board again when the screen changes (rotation, a foldable opening, the editor's Game view).</summary>
        void RefitOnResize()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            if (size == _fitScreen || _board == null || _fitRadius <= 0f) return;
            FitCamera(_fitRadius, _fitShare, _fitPlaying);
        }

        void FitCamera(float radius, float share, bool playing)
        {
            _fitRadius = radius; _fitShare = share; _fitPlaying = playing;
            _fitScreen = new Vector2Int(Screen.width, Screen.height);
            // Keep the whole shape in view however it is turned, with room for the HUD above and below.
            float r = radius + (playing ? 0.2f : 0f);
            float vHalf = _baseFov * 0.5f * Mathf.Deg2Rad;
            float hHalf = Mathf.Atan(Mathf.Tan(vHalf) * Mathf.Max(0.3f, Cam.aspect));
            float fit = Mathf.Min(Mathf.Atan(Mathf.Tan(vHalf) * share), hHalf * 0.96f);
            float dist = r / Mathf.Sin(fit);
            float lift = playing ? 0.03f : 0.01f;
            _camFramed = new Vector3(0, lift * dist, -dist);
            Cam.transform.rotation = Quaternion.identity;
            _tw.Stop(Cam);
            _zoom = 1f;
            _pan = Vector2.zero;
            ApplyZoom();
        }

        // ---------------- Zoom ----------------

        /// <summary>Field of view and camera shift from <see cref="_zoom"/> and <see cref="_pan"/>.</summary>
        void ApplyZoom()
        {
            Cam.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(_baseFov * 0.5f * Mathf.Deg2Rad) / _zoom) * Mathf.Rad2Deg;
            // the zoomed view may move at most as far as the board reaches past the narrower frame
            float reach = _fitRadius * (1f - 1f / _zoom) * 1.15f;
            _pan = Vector2.ClampMagnitude(_pan, reach);
            Cam.transform.position = _camFramed + new Vector3(_pan.x, _pan.y, 0f);
            var reset = UI.Hud.ZoomResetButton;
            if (reset != null && reset.gameObject.activeSelf != (_zoom > 1.02f)) reset.gameObject.SetActive(_zoom > 1.02f);
            if (!_zoomLogged && _zoom > 1.25f && _state == State.Playing && _level != null)
            {
                _zoomLogged = true;
                GameAnalytics.Log("zoom_used", ("shape", GameData.Group(_group).id), ("level", _level.Number));
            }
        }

        /// <summary>World units per screen pixel in the plane through the board's centre.</summary>
        float WorldPerPixel => 2f * -_camFramed.z * Mathf.Tan(Cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, Screen.height);

        /// <summary>Zoom to <paramref name="zoom"/> keeping the board point under <paramref name="screen"/> where it is.</summary>
        void ZoomAt(Vector2 screen, float zoom)
        {
            zoom = Mathf.Clamp(zoom, 1f, MaxZoom);
            if (Mathf.Approximately(zoom, _zoom)) return;
            _tw.Stop(Cam);
            Vector2 fromCentre = screen - new Vector2(Screen.width, Screen.height) * 0.5f;
            Vector2 before = fromCentre * WorldPerPixel;
            _zoom = zoom;
            Cam.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(_baseFov * 0.5f * Mathf.Deg2Rad) / _zoom) * Mathf.Rad2Deg;
            Vector2 after = fromCentre * WorldPerPixel;
            _pan += before - after;
            ApplyZoom();
        }

        /// <summary>Move the zoomed view with the fingers (the board follows them).</summary>
        void PanPixels(Vector2 delta)
        {
            if (_zoom <= 1.001f) return;
            _pan -= delta * WorldPerPixel;
            ApplyZoom();
        }

        /// <summary>Back to the framed view: at once, or with a short ease.</summary>
        void ResetZoom(bool animate)
        {
            _tw.Stop(Cam);
            if (!animate || _zoom <= 1.001f) { _zoom = 1f; _pan = Vector2.zero; ApplyZoom(); return; }
            float z0 = _zoom;
            Vector2 p0 = _pan;
            _tw.Run(0.25f, p =>
            {
                _zoom = Mathf.Lerp(z0, 1f, p);     // p arrives eased
                _pan = Vector2.Lerp(p0, Vector2.zero, p);
                ApplyZoom();
            }, null, Cam);
        }

        // ---------------- Play ----------------

        int _shownMoves = -1, _shownPar = -1, _shownLit = -1, _shownTotal = -1, _shownHints = -1, _shownUndos = -1;

        /// <summary>Numbers as text without allocating for the values a level actually shows.</summary>
        static readonly string[] Numbers = BuildNumbers(1000);
        static string[] BuildNumbers(int n) { var a = new string[n]; for (int i = 0; i < n; i++) a[i] = i.ToString(); return a; }
        static string Num(int v) => v >= 0 && v < Numbers.Length ? Numbers[v] : v.ToString();

        /// <summary>Only texts whose numbers changed are rebuilt, so a tap costs at most two short strings.</summary>
        void UpdateHud()
        {
            var hud = UI.Hud;
            int target = LevelScore.ExpertLimit(_level.Par, GameData.Rules, GameData.Group(_group));   // the par players see includes the slack
            if (_moves != _shownMoves || target != _shownPar)
            {
                _shownMoves = _moves;
                _shownPar = target;
                hud.Status.text = string.Concat("MOVES ", Num(_moves), "  ·  PAR ", Num(target));
            }
            if (_hintsLeft != _shownHints)
            {
                _shownHints = _hintsLeft;
                hud.HintCount.text = Num(_hintsLeft);
            }
            if (_undosLeft != _shownUndos)
            {
                _shownUndos = _undosLeft;
                if (hud.UndoCount != null) hud.UndoCount.text = Num(_undosLeft);
            }
            hud.HintButton.interactable = _hintsLeft > 0;
            hud.UndoButton.interactable = _undosLeft > 0 && _undo.Count > 0;
            int lit = _board.LitLamps, total = _board.TotalLamps;
            if (lit == _shownLit && total == _shownTotal) return;
            _shownLit = lit;
            _shownTotal = total;
            hud.LampsLabel.text = string.Concat(Num(lit), " / ", Num(total), " lamps lit");
            int n = 0;
            for (int i = 0; i < hud.LampDots.childCount; i++)
            {
                var dot = hud.LampDots.GetChild(i);
                if (dot == hud.DotTemplate.transform || !dot.gameObject.activeSelf) continue;
                var role = dot.GetComponent<ThemeRole>();
                if (role != null) { role.Role = n < lit ? UiRole.Accent : UiRole.Track; role.Apply(); }
                n++;
            }
        }

        void TapTile(int index)
        {
            if (_state != State.Playing || _busy || CardOpen) return;
            var tile = _level.Tiles[index];
            if (tile.Locked) { GameAudio.Play(Sfx.Blocked); return; }   // a hint fixed this tile
            GameAudio.Play(Sfx.Rotate);
            tile.Rot++;
            _moves++;
            _undo.Push(index);
            LessonTapped();
            if (_moves == 4 && !CubeSave.SeenHowTo) { CubeSave.SeenHowTo = true; if (!(_group == 0 && _level.Number <= 3)) UI.Hud.Tip.text = ""; }
            _board.AnimateRotation(index, 1);
            bool solved = _board.Refresh();
            if (!solved && _board.NewlyLitColor != 0) GameAudio.Lamp(_board.NewlyLitColor);
            UpdateHud();
            if (solved) Win();
        }

        /// <summary>
        /// Turn the last tile the player turned back one step and take the move back. Each undo costs a rewarded ad
        /// and a level allows <see cref="UndosPerLevel"/> of them.
        /// </summary>
        void Undo()
        {
            if (_state != State.Playing || _busy || CardOpen || _undosLeft <= 0 || !CanUndo()) return;
            WithAd(AdPlacement.Undo, DoUndo);
        }

        /// <summary>There is a turn to take back (tiles a hint fixed since are skipped).</summary>
        bool CanUndo()
        {
            foreach (int index in _undo) if (!_level.Tiles[index].Locked) return true;
            return false;
        }

        void DoUndo()
        {
            if (_state != State.Playing || _undosLeft <= 0) return;
            while (_undo.Count > 0)
            {
                int index = _undo.Pop();
                var tile = _level.Tiles[index];
                if (tile.Locked) continue;              // a hint fixed this tile since
                tile.Rot += tile.Ports - 1;             // one step back, kept non-negative
                _moves = Mathf.Max(0, _moves - 1);
                _undosLeft--;
                _undosUsed++;
                GameAudio.Play(Sfx.Undo);
                _board.AnimateRotation(index, -1);
                _board.Refresh();
                GameAnalytics.Log("undo_used", ("shape", GameData.Group(_group).id), ("level", _level.Number), ("left", _undosLeft));
                break;
            }
            UpdateHud();
        }

        /// <summary>
        /// Show the rewarded ad for <paramref name="placement"/>, then run <paramref name="reward"/>. The board takes no
        /// input while the ad is up; closing the ad early gives nothing, and no ad to show gives the reward for free.
        /// </summary>
        void WithAd(AdPlacement placement, System.Action reward)
        {
            _busy = true;
            RewardedAds.Show(placement,
                () => { _busy = false; reward(); },
                () => { _busy = false; });
        }

        void ShowHowTo() => ShowTip("How to play",
            "Drag to turn the shape. Tap a tile to rotate it.\n\nConnect every lamp to a power source of its colour. "
            + "When two sources share a wire their colours mix: Red + Blue = Purple, Red + Yellow = Orange, Blue + Yellow = Green.\n\n"
            + "Solve it in the fewest moves (par) for three stars.", "Got it", null);

        /// <summary>Fix one wrong tile. Each hint costs a rewarded ad; a level allows <see cref="HintsPerLevel"/> of them.</summary>
        void UseHint()
        {
            if (_state != State.Playing || _busy || _hintsLeft <= 0 || CardOpen || !HasHintTile()) return;
            if (LessonFreeHint)
            {
                DoHint();
                LessonLearned("A hint turns one tile the right way. Each level has " + HintsPerLevel);
                return;
            }
            WithAd(AdPlacement.Hint, DoHint);
        }

        bool HasHintTile()
        {
            foreach (var t in _level.Tiles) if (!t.Locked && Circuit.TapsToSolve(t) > 0) return true;
            return false;
        }

        void DoHint()
        {
            if (_state != State.Playing || _hintsLeft <= 0) return;
            var candidates = new List<int>();
            var visibleFirst = new List<int>();
            Vector3 toCam = (Cam.transform.position - _board.transform.position).normalized;
            for (int i = 0; i < _level.Tiles.Length; i++)
            {
                var t = _level.Tiles[i];
                if (t.Locked || Circuit.TapsToSolve(t) == 0) continue;
                candidates.Add(i);
                if (Vector3.Dot(_board.NormalWorld(i), toCam) > 0.2f) visibleFirst.Add(i);
            }
            var pool = visibleFirst.Count > 0 ? visibleFirst : candidates;
            if (pool.Count == 0) return;
            int pick = pool[Random.Range(0, pool.Count)];
            var tile = _level.Tiles[pick];
            int turns = Circuit.TapsToSolve(tile);
            tile.Rot += turns;
            tile.Locked = true;
            _hintsLeft--;
            _hintsUsed++;
            GameAudio.Play(Sfx.Hint);
            GameAnalytics.Log("hint_used", ("shape", GameData.Group(_group).id), ("level", _level.Number), ("left", _hintsLeft));
            _board.AnimateRotation(pick, turns, 0.3f);
            bool solved = _board.Refresh();
            if (!solved && _board.NewlyLitColor != 0) GameAudio.Lamp(_board.NewlyLitColor);
            UpdateHud();
            if (solved) Win();
        }

        /// <summary>6 at par, at most 5 over par, minus one per hint, never below 3 (rules come from the manifest).</summary>
        int PointsFor() => LevelScore.Points(_moves, _level.Par, _hintsUsed, GameData.Rules, GameData.Group(_group));

        void Win()
        {
            _busy = true;
            _state = State.Won;
            _spin = Vector2.zero;
            LessonOnWin();
            ResetZoom(true);   // the box breaks open in the framed view, so the stars fly from its centre
            int points = PointsFor();
            var wasOpen = new bool[GameData.GroupCount];
            for (int g = 0; g < wasOpen.Length; g++) wasOpen[g] = CubeSave.GroupOpen(g);
            if (!_cheat) CubeSave.Complete(_group, _level.Number, points);
            int newGroup = -1;
            for (int g = 0; g < wasOpen.Length && newGroup < 0; g++) if (!wasOpen[g] && CubeSave.GroupOpen(g)) newGroup = g;
            bool unlocksNext = newGroup >= 0;
            _unlockedGroup = newGroup;
            GameAnalytics.Log("level_complete", ("shape", GameData.Group(_group).id), ("level", _level.Number), ("moves", _moves),
                ("par", LevelScore.ExpertLimit(_level.Par, GameData.Rules, GameData.Group(_group))), ("stars", points / 2),
                ("hints", _hintsUsed), ("undos", _undosUsed), ("seconds", Mathf.RoundToInt(Time.realtimeSinceStartup - _levelStartTime)));
            if (unlocksNext) GameAnalytics.Log("shape_unlocked", ("shape", GameData.Group(newGroup).id));
            _board.Celebrate();
            GameAudio.Play(Sfx.Win);
            GameAudio.Duck(3f);
            // the solved box winds up with a half turn, then breaks open and the result card comes up through the pieces
            var tr = _board.transform;
            Quaternion from = tr.rotation;
            var board = _board;
            _tw.Run(0.55f, p => tr.rotation = Quaternion.AngleAxis(200f * p * p, Vector3.up) * from, () =>
            {
                board.Shatter();
                LaunchStars(points / 2);
#if UNITY_ANDROID || UNITY_IOS
                if (CubeSave.Settings.vibration) Handheld.Vibrate();
#endif
            }, tr);
            _tw.Run(0.55f + ResultDelay, p => { }, () =>
            {
                _busy = false;
                var r = UI.Result;
                r.Caption.text = GameData.Group(_group).name.ToUpperInvariant() + "  ·  LEVEL " + _level.Number;
                r.Title.text = "Level complete";
                r.Stats[0].text = _moves.ToString();
                r.Stats[1].text = LevelScore.ExpertLimit(_level.Par, GameData.Rules, GameData.Group(_group)).ToString();
                r.Stats[2].text = _board.LitLamps + "/" + _board.TotalLamps;
                r.Badge.SetActive(points >= GameData.Rules.parScore);
                // the stars say how well it went; the points and hint rule stay behind the scenes
                r.Body.text = unlocksNext ? "New shape unlocked: " + GameData.Group(newGroup).name + "!" : "";
                // stars already in the air fill the row as they land; without them the row fills at once
                if (_starsInFlight == 0) r.Stars.SetPoints(points);
                r.PrimaryLabel.text = "Next level";
                UI.Dim.SetActive(true);
                r.Open();
                if (_starsInFlight == 0)
                    for (int i = 0; i < points / 2; i++) GameAudio.Play(Sfx.Star, 1f + 0.12f * i, 0.3f + 0.25f * i);
            }, UI.Result);   // its own owner: a second run on the board's transform would cancel the wind-up
        }

        // ---------------- Stars flying from the box to the result card ----------------

        [Header("Win stars")]
        [Tooltip("Seconds from the box breaking to a star landing in its slot.")]
        public float StarFlightTime = 1.05f;
        [Tooltip("Seconds between one star leaving and the next.")]
        public float StarStagger = 0.12f;
        [Tooltip("How far the stars burst out of the box before they turn towards the card (canvas units).")]
        public float StarBurst = 320f;
        [Tooltip("Size of a star at the top of its flight, against its size in the slot.")]
        public float StarPeakScale = 1.3f;

        int _starsInFlight;

        static float EaseOutBack(float t) { float c = 1.7f, u = t - 1f; return 1f + (c + 1f) * u * u * u + c * u * u; }

        /// <summary>
        /// The earned stars burst out of the breaking box and, in one unbroken curve, fly into their slots
        /// on the result card, which comes up while they are in the air. Each slot fills as its star lands.
        /// </summary>
        void LaunchStars(int count)
        {
            var fs = UI.FlyingStars;
            var row = UI.Result.Stars;
            _starsInFlight = 0;
            row.Set(0);
            if (fs == null || fs.Length == 0 || fs[0] == null || _board == null) return;
            count = Mathf.Min(count, Mathf.Min(fs.Length, row.Stars.Length));
            if (count <= 0) return;
            var parent = (RectTransform)fs[0].parent;
            var canvas = parent.GetComponentInParent<Canvas>().rootCanvas;
            var uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 screen = Cam.WorldToScreenPoint(_board.transform.position);
            if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, screen, uiCam, out var core)) return;
            float unit = parent.lossyScale.x;
            _starsInFlight = count;
            for (int i = 0; i < count; i++)
            {
                int k = i;
                var st = fs[k];
                var slot = row.Stars[k].rectTransform;
                // fan the burst out sideways and up so the stars never cross each other on the way
                float x = count == 1 ? 0f : -1f + 2f * k / (count - 1);
                Vector3 burst = new Vector3(x * 0.9f, 0.55f + 0.25f * (1f - Mathf.Abs(x)), 0f).normalized * StarBurst * unit;
                float spin = (x >= 0f ? -1f : 1f) * 360f;
                st.gameObject.SetActive(false);
                _tw.Run(StarStagger * k, p => { }, () =>
                {
                    st.gameObject.SetActive(true);
                    st.position = core;
                    st.localScale = Vector3.zero;
                    _tw.Run(StarFlightTime, p =>
                    {
                        // one cubic curve: out of the box along the burst, then down into the slot from above
                        Vector3 to = slot.position;    // follows the card while it is still sliding in
                        Vector3 c1 = core + burst * 1.6f;
                        Vector3 c2 = to + Vector3.up * (140f * unit);
                        float s = p * p * (3f - 2f * p) * 0.6f + p * 0.4f;    // gentle in and out, never stops
                        float q = 1f - s;
                        st.position = q * q * q * core + 3f * q * q * s * c1 + 3f * q * s * s * c2 + s * s * s * to;
                        // pop out big, then settle to the slot's own size as it arrives
                        float slotScale = slot.rect.width * slot.lossyScale.x / Mathf.Max(1f, st.rect.width * st.parent.lossyScale.x);
                        float grow = EaseOutBack(Mathf.Clamp01(p / 0.3f));
                        float settle = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((p - 0.45f) / 0.55f));
                        st.localScale = Vector3.one * Mathf.LerpUnclamped(0f, Mathf.Lerp(StarPeakScale * slotScale, slotScale, settle), grow);
                        st.localRotation = Quaternion.Euler(0f, 0f, spin * (1f - Mathf.SmoothStep(0f, 1f, p)));
                    }, () =>
                    {
                        st.gameObject.SetActive(false);
                        _starsInFlight = Mathf.Max(0, _starsInFlight - 1);
                        if (!UI.Result.gameObject.activeSelf) return;
                        row.Set(k + 1);
                        GameAudio.Play(Sfx.Star, 1f + 0.12f * k);
                        var pop = slot.transform;
                        _tw.Run(0.3f, p => pop.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(p * Mathf.PI) * (1f - p * 0.5f)), () => pop.localScale = Vector3.one, pop);
                    }, st);
                }, row.Stars[k]);
            }
        }

        // ---------------- Loop and input ----------------

        void Update()
        {
            RefitOnResize();
            float dt = Time.unscaledDeltaTime;
            RewardedAds.Tick();
            _tw.Tick(dt);
            HandlePointer(dt);
            TickLesson(dt);

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Back();

            if (_board != null && !_pressed && _spin.sqrMagnitude > 0.01f && !_busy)
            {
                Rotate(_spin.x * dt, _spin.y * dt);
                if (_state != State.Home) _spin *= Mathf.Exp(-4f * dt);
            }
        }

        void Rotate(float aroundUp, float aroundRight)
        {
            var tr = _board.transform;
            tr.rotation = Quaternion.AngleAxis(aroundUp, Cam.transform.up) * Quaternion.AngleAxis(aroundRight, Cam.transform.right) * tr.rotation;
        }

        bool CanZoom => _state == State.Playing && _board != null && !_busy && !CardOpen;

        /// <summary>
        /// Two fingers: pinch to zoom towards the point between them, move them together to pan. Until every finger
        /// is up again a pinch never turns into a spin or a tap. Mouse: wheel zooms at the pointer, right button pans.
        /// </summary>
        bool HandleZoom()
        {
            var ts = Touchscreen.current;
            int n = 0;
            Vector2 a = default, b = default;
            if (ts != null)
            {
                var touches = ts.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    if (!touches[i].press.isPressed) continue;
                    if (n == 0) a = touches[i].position.ReadValue();
                    else if (n == 1) b = touches[i].position.ReadValue();
                    n++;
                }
            }
            if (n >= 2 && CanZoom)
            {
                float d = (a - b).magnitude;
                Vector2 mid = (a + b) * 0.5f;
                if (!_pinching)
                {
                    _pinching = true;
                    _spin = Vector2.zero;
                    _pressed = true;
                    _dragging = true;   // the finger that went down first is no tap any more
                    _overUi = false;
                }
                else
                {
                    if (_pinchDist > 1f) ZoomAt(mid, _zoom * d / _pinchDist);
                    PanPixels(mid - _pinchMid);
                }
                _pinchDist = d;
                _pinchMid = mid;
                return true;
            }
            if (_pinching)
            {
                if (n == 0) { _pinching = false; _pressed = false; }
                return true;
            }

            var mouse = Mouse.current;
            if (mouse == null || ts != null && ts.primaryTouch.press.isPressed) return false;
            Vector2 mp = mouse.position.ReadValue();
            float wheel = mouse.scroll.ReadValue().y;
            if (wheel != 0f && CanZoom && !PointerOverUi(mp)) ZoomAt(mp, _zoom * Mathf.Pow(1.0015f, Mathf.Clamp(wheel, -480f, 480f)));
            if (mouse.rightButton.isPressed && CanZoom)
            {
                if (_rightPanning) PanPixels(mp - _rightLast);
                _rightPanning = true;
                _rightLast = mp;
            }
            else _rightPanning = false;
            return false;
        }

        void HandlePointer(float dt)
        {
            if (HandleZoom()) return;
            bool down = false;
            Vector2 pos = default;
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.isPressed) { down = true; pos = ts.primaryTouch.position.ReadValue(); }
            else if (Mouse.current != null && Mouse.current.leftButton.isPressed) { down = true; pos = Mouse.current.position.ReadValue(); }

            if (down && !_pressed)
            {
                _pressed = true;
                _dragging = false;
                _pressPos = _lastPos = pos;
                _overUi = PointerOverUi(pos);
                if (!_overUi && _state == State.Playing) _spin = Vector2.zero;
            }
            else if (down && _pressed)
            {
                if (_overUi || _board == null || _state != State.Playing || _busy) { _lastPos = pos; return; }
                if (!_dragging && (pos - _pressPos).magnitude > _dragThreshold) _dragging = true;
                if (_dragging)
                {
                    Vector2 d = pos - _lastPos;
                    float degPerPixel = 220f / Mathf.Max(400f, Screen.height) / Mathf.Sqrt(_zoom);   // finer turns when zoomed in
                    float ax = -d.x * degPerPixel, ay = d.y * degPerPixel;
                    Rotate(ax, ay);
                    LessonSpun(Mathf.Abs(ax) + Mathf.Abs(ay));
                    if (dt > 0f) _spin = Vector2.Lerp(_spin, new Vector2(ax, ay) / dt, 0.5f);
                }
                _lastPos = pos;
            }
            else if (!down && _pressed)
            {
                _pressed = false;
                if (_overUi) return;
                if (!_dragging) { _spin = Vector2.zero; TryTap(_lastPos); }
                else if (_spin.magnitude > 900f) _spin = _spin.normalized * 900f;
            }
        }

        void TryTap(Vector2 screenPos)
        {
            if (_state != State.Playing || _board == null) return;
            int index = _board.Pick(Cam.ScreenPointToRay(screenPos));
            if (index >= 0) { _lastEmptyTap = -1f; TapTile(index); return; }
            // two taps next to the board zoom back out
            float now = Time.unscaledTime;
            if (_zoom > 1.02f && _lastEmptyTap >= 0f && now - _lastEmptyTap < DoubleTapTime && (screenPos - _lastEmptyTapPos).magnitude < _dragThreshold * 4f)
            {
                _lastEmptyTap = -1f;
                if (CanZoom) ResetZoom(true);
                return;
            }
            _lastEmptyTap = now;
            _lastEmptyTapPos = screenPos;
        }

        bool PointerOverUi(Vector2 pos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (_ped == null) _ped = new PointerEventData(es);
            _ped.position = pos;
            _uiHits.Clear();
            es.RaycastAll(_ped, _uiHits);
            return _uiHits.Count > 0;
        }

        // ---------------- Test hooks (used by the editor play-test) ----------------

        /// <summary>"dark", "light" or "system" (the phone's own light/dark setting, see <see cref="SystemTheme"/>).</summary>
        public void ApplyTheme(string mode)
        {
            bool light = mode == "light" || (mode == "system" && !SystemTheme.IsDark());
            var t = light ? LightTheme : DarkTheme;
            if (t == null) t = DarkTheme != null ? DarkTheme : LightTheme;
            if (t == null) return;
            Themes.Set(t);
            if (Cam != null) Cam.backgroundColor = t.CameraBackground;
            // bloom: dark theme on medium quality and up; off, the camera skips post-processing altogether
            bool bloom = t.Dark && BloomVolume != null && QualitySettings.GetQualityLevel() >= MinBloomQuality;
            if (BloomVolume != null) BloomVolume.enabled = bloom;
            var camData = Cam != null ? Cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() : null;
            if (camData != null) camData.renderPostProcessing = bloom;
            if (_board != null) _board.ApplyTheme();
        }

        public void SetTheme(string mode)
        {
            CubeSave.Settings.theme = mode;
            CubeSave.SaveSettings();
            ApplyTheme(mode);
            if (_state == State.Shapes) ShowShapes();
            else if (_state == State.Levels) ShowLevels(_group);
            else if (_state == State.Home)
            {
                var icon = GroupIcon(CubeSave.CurrentGroup);   // shape icons differ per theme
                if (icon != null) UI.Home.GroupIcon.sprite = icon;
            }
        }

        public string ThemeMode => CubeSave.Settings.theme;

        public void DebugStart(int group, int level) => StartLevel(group, level, true);
        public void DebugHome() => ShowHome();
        /// <summary>Redraw the open menu screen after a cheat changed progress.</summary>
        public void DebugRefresh()
        {
            if (_state == State.Home) ShowHome();
            else if (_state == State.Shapes) ShowShapes();
            else if (_state == State.Levels) ShowLevels(_group);
        }
        public void DebugShowShapes() => ShowShapes();
        public void DebugShowLevels(int group) => ShowLevels(group);
        public void DebugWin() { if (_state == State.Playing) Win(); }
    }
}
