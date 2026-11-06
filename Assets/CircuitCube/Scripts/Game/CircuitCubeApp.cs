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
    public sealed class CircuitCubeApp : MonoBehaviour
    {
        enum State { Home, Shapes, Levels, Playing, Won }

        public static int HintsPerLevel => GameData.Rules.hintsPerLevel;

        [Header("Scene")]
        public Camera Cam;
        public UIRoot UI;
        [Header("Prefabs")]
        public BoardTile TilePrefab;
        public BoardTile TriTilePrefab;

        [Header("Look")]
        [Tooltip("Themes/Theme_Dark.asset")] public GameTheme DarkTheme;
        [Tooltip("Themes/Theme_Light.asset")] public GameTheme LightTheme;

        [Header("Level data")]
        [Tooltip("CDN root with manifest.json, groups/, orders/, levels/. Empty = only the data shipped in the build.")]
        public string ContentBaseUrl = "";

        [Header("Camera framing")]
        [Range(0.3f, 1f)] public float PlayScreenShare = 0.72f;
        [Range(0.3f, 1f)] public float HomeScreenShare = 0.55f;

        readonly Tweener _tw = new Tweener();
        State _state;
        int _group;

        CubeLevel _level;
        CubeBoardView _board;
        int _moves, _hintsLeft, _hintsUsed;
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

        void Awake()
        {
            Application.targetFrameRate = 60;
            _dragThreshold = Mathf.Max(10f, Screen.dpi > 0 ? Screen.dpi * 0.06f : 10f);
            if (Cam == null) Cam = Camera.main;
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
            hud.RestartButton.onClick.AddListener(() => { if (_state == State.Playing && !_busy && !CardOpen) StartLevel(_group, _level.Number); });
            hud.HintButton.onClick.AddListener(UseHint);
            hud.UndoButton.onClick.AddListener(Undo);
            hud.HelpButton.onClick.AddListener(() => { if (_state == State.Playing && !CardOpen) ShowHowTo(); });

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
        }

        void Start()
        {
            HideAll();
            UI.Splash.Show();
            UI.Splash.SetProgress(0f);
            UI.Splash.Status.text = "Loading levels...  ·  v" + Application.version;
            GameData.Reload();
            // the data loads in one go; the bar just gives the logo a moment on screen
            _tw.Run(0.9f, p => UI.Splash.SetProgress(Tweener.Ease(p)), () => { if (UI.Splash.gameObject.activeSelf) ShowHome(); });
            if (!string.IsNullOrEmpty(ContentBaseUrl))
                StartCoroutine(RemoteContent.Sync(ContentBaseUrl, CubeSave.Beaten, () => { if (_state != State.Playing && _state != State.Won) DebugRefresh(); }));
        }

        void OnApplicationPause(bool paused) { if (paused) SaveInProgress(); }
        void OnApplicationQuit() => SaveInProgress();

        /// <summary>Remember the board of the level being played, so it continues where it stopped.</summary>
        void SaveInProgress()
        {
            if (_state != State.Playing || _level == null || _cheat) return;
            int serial = GameData.SerialAt(_group, _level.Number);
            if (serial <= 0) return;
            var ip = new InProgress { serial = serial, position = _level.Number, moves = _moves, hintsUsed = _hintsUsed };
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
        }

        bool CardOpen => UI.Result.gameObject.activeSelf || UI.Tip.gameObject.activeSelf
                         || UI.Unlock.gameObject.activeSelf || UI.Settings.gameObject.activeSelf;

        void OpenSettings(bool paused)
        {
            UI.Dim.SetActive(true);
            UI.Settings.Open(paused);
        }

        void CloseSettings()
        {
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
        }

        void ShowShapes()
        {
            _state = State.Shapes;
            HideAll();
            var s = UI.Shapes;
            s.Show();
            s.StarsLabel.text = CubeSave.TotalStars.ToString();
            for (int i = s.List.childCount - 1; i >= 0; i--) Destroy(s.List.GetChild(i).gameObject);
            for (int g = 0; g < GameData.GroupCount; g++)
            {
                var info = GameData.Group(g);
                int group = g;
                bool open = CubeSave.GroupOpen(g);
                var rule = GameData.Unlock(g);
                int pre = GameData.IndexOf(rule.group);
                string hint = rule.IsNone || pre < 0 ? "" : "Beat level " + rule.cleared + " of " + GameData.Group(pre).name;
                int total = Mathf.Max(1, info.levelCount);
                var card = Instantiate(s.CardPrefab, s.List);
                string iconId = string.IsNullOrEmpty(info.icon) ? info.id : info.icon;
                if (card.IconFor(iconId) != null) card.Set(iconId, info.name, info.blurb, open, info.endless ? CubeSave.Beaten(g) : Mathf.Min(CubeSave.Beaten(g), total),
                    total, info.endless, hint, () => ShowLevels(group));
                else card.Set(IconIndex(g), info.name, info.blurb, open, info.endless ? CubeSave.Beaten(g) : Mathf.Min(CubeSave.Beaten(g), total),
                    total, info.endless, hint, () => ShowLevels(group));
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
            for (int i = lv.Grid.childCount - 1; i >= 0; i--) Destroy(lv.Grid.GetChild(i).gameObject);
            int count = info.endless ? Mathf.Max(total, ((unlocked + 11) / 4 + 1) * 4) : total;
            for (int l = 1; l <= count; l++)
            {
                int level = l;
                var node = Instantiate(lv.NodePrefab, lv.Grid);
                node.Set(l, l <= unlocked, CubeSave.Stars(group, l), () => StartLevel(group, level), l == unlocked);
            }
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
                ShowTip("More levels need the internet",
                    "Levels arrive ten at a time. Connect to the internet and open the game again to unlock the next ones.", "OK", null);
                return;
            }
            _cheat = cheat;
            _level = level;
            _moves = 0;
            _hintsUsed = 0;
            _hintsLeft = HintsPerLevel;
            _undo.Clear();
            _unlockedGroup = -1;
            if (!cheat) RestoreInProgress();
            _busy = false;
            _state = State.Playing;
            HideAll();
            SetBoard(_level, true, PlayScreenShare);
            _board.transform.rotation = StartRotation(_level.Topo, -28f, 38f);
            _spin = Vector2.zero;

            var hud = UI.Hud;
            hud.Show();
            hud.Title.text = GameData.Group(group).name.ToUpperInvariant() + "  " + number;
            hud.Tip.text = (group == 0 && number <= 3) || !CubeSave.SeenHowTo ? "Drag to turn the shape  ·  Tap a tile to rotate it" : "";
            for (int i = hud.Legend.childCount - 1; i >= 0; i--) Destroy(hud.Legend.GetChild(i).gameObject);
            var seen = new HashSet<int>();
            foreach (int c in _level.Colors)
            {
                if (!seen.Add(c)) continue;
                Instantiate(hud.ChipPrefab, hud.Legend).Set(CubeColors.Energy(c), CubeColors.EnergyName(c));
            }
            // one dot per lamp, copied from the template dot in the prefab
            for (int i = hud.LampDots.childCount - 1; i >= 0; i--)
                if (hud.LampDots.GetChild(i) != hud.DotTemplate.transform) Destroy(hud.LampDots.GetChild(i).gameObject);
            hud.DotTemplate.gameObject.SetActive(false);
            for (int i = 0; i < _board.TotalLamps; i++)
            {
                var dot = Instantiate(hud.DotTemplate, hud.LampDots);
                dot.gameObject.SetActive(true);
            }
            UpdateHud();

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

        void ShowTip(string title, string body, string button, System.Action onClose)
        {
            var t = UI.Tip;
            t.Title.text = title;
            t.Body.text = body;
            t.PrimaryLabel.text = button;
            _tipClosed = onClose;
            UI.Dim.SetActive(true);
            t.Open();
        }

        void CloseTip()
        {
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
            _board.Build(level, new[] { null, null, null, TriTilePrefab, TilePrefab, null }, Cam, _tw, interactive);
            FitCamera(_board.Radius, share, interactive);
        }

        void FitCamera(float radius, float share, bool playing)
        {
            // Keep the whole shape in view however it is turned, with room for the HUD above and below.
            float r = radius + (playing ? 0.2f : 0f);
            float vHalf = Cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float hHalf = Mathf.Atan(Mathf.Tan(vHalf) * Mathf.Max(0.3f, Cam.aspect));
            float fit = Mathf.Min(Mathf.Atan(Mathf.Tan(vHalf) * share), hHalf * 0.96f);
            float dist = r / Mathf.Sin(fit);
            float lift = playing ? 0.03f : 0.01f;
            Cam.transform.position = new Vector3(0, lift * dist, -dist);
            Cam.transform.rotation = Quaternion.identity;
        }

        // ---------------- Play ----------------

        void UpdateHud()
        {
            var hud = UI.Hud;
            hud.Status.text = "MOVES " + _moves + "  ·  PAR " + _level.Par;
            hud.HintCount.text = _hintsLeft.ToString();
            hud.HintButton.interactable = _hintsLeft > 0;
            hud.UndoButton.interactable = _undo.Count > 0;
            hud.LampsLabel.text = _board.LitLamps + " / " + _board.TotalLamps + " lamps lit";
            int n = 0;
            for (int i = 0; i < hud.LampDots.childCount; i++)
            {
                var dot = hud.LampDots.GetChild(i);
                if (dot == hud.DotTemplate.transform || !dot.gameObject.activeSelf) continue;
                var role = dot.GetComponent<ThemeRole>();
                if (role != null) { role.Role = n < _board.LitLamps ? UiRole.Accent : UiRole.Track; role.Apply(); }
                n++;
            }
        }

        void TapTile(int index)
        {
            if (_state != State.Playing || _busy || CardOpen) return;
            var tile = _level.Tiles[index];
            if (tile.Locked) return;
            tile.Rot++;
            _moves++;
            _undo.Push(index);
            if (_moves == 4 && !CubeSave.SeenHowTo) { CubeSave.SeenHowTo = true; if (!(_group == 0 && _level.Number <= 3)) UI.Hud.Tip.text = ""; }
            _board.AnimateRotation(index, 1);
            bool solved = _board.Refresh();
            UpdateHud();
            if (solved) Win();
        }

        /// <summary>Turn the last tile the player turned back one step. Takes the move back too.</summary>
        void Undo()
        {
            if (_state != State.Playing || _busy || CardOpen) return;
            while (_undo.Count > 0)
            {
                int index = _undo.Pop();
                var tile = _level.Tiles[index];
                if (tile.Locked) continue;              // a hint fixed this tile since
                tile.Rot += tile.Ports - 1;             // one step back, kept non-negative
                _moves = Mathf.Max(0, _moves - 1);
                _board.AnimateRotation(index, -1);
                _board.Refresh();
                break;
            }
            UpdateHud();
        }

        void ShowHowTo() => ShowTip("How to play",
            "Drag to turn the shape. Tap a tile to rotate it.\n\nConnect every lamp to a power source of its colour. "
            + "When two sources share a wire their colours mix: Red + Blue = Purple, Red + Yellow = Orange, Blue + Yellow = Green.\n\n"
            + "Solve it in the fewest moves (par) for three stars.", "Got it", null);

        void UseHint()
        {
            if (_state != State.Playing || _busy || _hintsLeft <= 0 || CardOpen) return;
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
            _board.AnimateRotation(pick, turns, 0.3f);
            bool solved = _board.Refresh();
            UpdateHud();
            if (solved) Win();
        }

        /// <summary>6 at par, at most 5 over par, minus one per hint, never below 3 (rules come from the manifest).</summary>
        int PointsFor() => LevelScore.Points(_moves, _level.Par, _hintsUsed, GameData.Rules);

        void Win()
        {
            _busy = true;
            _state = State.Won;
            _spin = Vector2.zero;
            int points = PointsFor();
            var wasOpen = new bool[GameData.GroupCount];
            for (int g = 0; g < wasOpen.Length; g++) wasOpen[g] = CubeSave.GroupOpen(g);
            if (!_cheat) CubeSave.Complete(_group, _level.Number, points);
            int newGroup = -1;
            for (int g = 0; g < wasOpen.Length && newGroup < 0; g++) if (!wasOpen[g] && CubeSave.GroupOpen(g)) newGroup = g;
            bool unlocksNext = newGroup >= 0;
            _unlockedGroup = newGroup;
            _board.Celebrate();
#if UNITY_ANDROID || UNITY_IOS
            if (CubeSave.Settings.vibration) Handheld.Vibrate();
#endif
            var tr = _board.transform;
            Quaternion from = tr.rotation;
            _tw.Run(1.1f, p => tr.rotation = Quaternion.AngleAxis(360f * p, Vector3.up) * from, () =>
            {
                _busy = false;
                var r = UI.Result;
                r.Caption.text = GameData.Group(_group).name.ToUpperInvariant() + "  ·  LEVEL " + _level.Number;
                r.Title.text = "Level complete";
                r.Stats[0].text = _moves.ToString();
                r.Stats[1].text = _level.Par.ToString();
                r.Stats[2].text = _board.LitLamps + "/" + _board.TotalLamps;
                r.Badge.SetActive(points >= GameData.Rules.parScore);
                string line = "Score " + points + " / " + GameData.Rules.parScore;
                if (_hintsUsed > 0) line += "  ·  " + _hintsUsed + (_hintsUsed == 1 ? " hint" : " hints") + " (−" + GameData.Rules.hintPenalty + " each)";
                if (unlocksNext) line += "\nNew shape unlocked: " + GameData.Group(newGroup).name + "!";
                r.Body.text = line;
                r.Stars.SetPoints(points);
                r.PrimaryLabel.text = "Next level";
                UI.Dim.SetActive(true);
                r.Open();
            }, tr);
        }

        // ---------------- Loop and input ----------------

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _tw.Tick(dt);
            HandlePointer(dt);

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

        void HandlePointer(float dt)
        {
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
                    float degPerPixel = 220f / Mathf.Max(400f, Screen.height);
                    float ax = -d.x * degPerPixel, ay = d.y * degPerPixel;
                    Rotate(ax, ay);
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
            var ray = Cam.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out var hit, 500f))
            {
                var t = hit.collider.GetComponent<BoardTile>();
                if (t != null) TapTile(_board.TileAt(hit.point, t.Index));
            }
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

        /// <summary>"dark", "light" or "system" (system follows dark until the OS theme is read on device).</summary>
        public void ApplyTheme(string mode)
        {
            var t = mode == "light" ? LightTheme : DarkTheme;
            if (t == null) t = DarkTheme != null ? DarkTheme : LightTheme;
            if (t == null) return;
            Themes.Set(t);
            if (Cam != null) Cam.backgroundColor = t.CameraBackground;
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
