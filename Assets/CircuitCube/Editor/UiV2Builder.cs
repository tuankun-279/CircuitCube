using CircuitCube.Game;
using CircuitCube.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.EditorTools
{
    /// <summary>
    /// Builds the V2 screens that the mockup (ArtRef/mockup_v2.png) has and the first UI prefab lacked: splash, star
    /// counters, the current-shape card on Home, the level progress card, the pause / settings card, the HUD bottom
    /// bar with Undo and lamp dots, the result card with mascot and stat tiles, and the new-shape card.
    /// Everything is written into Prefabs/UI/UI.prefab; nothing is created at runtime. Safe to run again.
    /// </summary>
    public static class UiV2Builder
    {
        const string UiPrefab = "Assets/CircuitCube/Prefabs/UI/UI.prefab";
        const string Brand = "#2AA8B8";


        [MenuItem("Tools/Circuit Cube/Art V2/Build UI V2")]
        public static void Build()
        {
            var root = PrefabUtility.LoadPrefabContents(UiPrefab);
            var ui = root.GetComponent<UIRoot>();
            var safe = root.transform.Find("SafeArea");
            var cardClip = CardClip(safe);

            Splash(ui, safe);
            Home(ui, safe);
            Shapes(ui, safe);
            Levels(ui, safe);
            Hud(ui, safe);
            Result(ui, safe);
            Unlock(ui, safe, cardClip);
            Settings(ui, safe, cardClip);

            // cards above every screen, dim right under the cards
            ui.Dim.transform.SetSiblingIndex(safe.childCount - 1);
            foreach (var c in new Component[] { ui.Result, ui.Tip, ui.Unlock, ui.Settings }) c.transform.SetAsLastSibling();

            PrefabUtility.SaveAsPrefabAsset(root, UiPrefab);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
            NewLevelsAndTablets();
            FlyingStars();
            AdsUi();
            VersionUi();
            ZoomUi();
            TutorialUi();
            Debug.Log("[CircuitCube] UI V2 built.");
        }

        /// <summary>
        /// UI for the rewarded ads: a badge with the undos left on the Undo button (a copy of the hint count badge), and a
        /// "Privacy options" link in the settings card for players whose region needs one. Safe to run again.
        /// </summary>
        [MenuItem("Tools/Circuit Cube/Art V2/Build Ads UI")]
        public static void AdsUi()
        {
            var root = PrefabUtility.LoadPrefabContents(UiPrefab);
            try
            {
                var ui = root.GetComponent<UIRoot>();
                var hud = ui.Hud;
                var undo = hud.UndoButton.transform;
                var old = undo.Find("Count");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var count = Object.Instantiate(hud.HintButton.transform.Find("Count").gameObject, undo, false);
                count.name = "Count";
                ((RectTransform)count.transform).anchoredPosition = new Vector2(-14, 14);
                foreach (var g in count.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
                hud.UndoCount = count.GetComponentInChildren<TMP_Text>(true);
                hud.UndoCount.text = "5";

                var st = ui.Settings;
                var panel = st.Panel;
                var link = Fresh(panel, "Privacy", TopC, TopC, TopC, new Vector2(0, -700), new Vector2(760, 64));
                Img(link, null, UiRole.None).color = new Color(1, 1, 1, 0);   // invisible hit area
                var label = Label(link, "Label", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero, "<u>Privacy options</u>", 30, UiRole.TextMuted);
                label.raycastTarget = false;
                st.Privacy = link.gameObject.AddComponent<Button>();
                link.gameObject.AddComponent<UiClickSound>();
                link.gameObject.SetActive(false);

                PrefabUtility.SaveAsPrefabAsset(root, UiPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[CircuitCube] Ads UI built.");
        }

        /// <summary>
        /// App version line at the bottom of the settings card's content (under the theme choice); the Privacy link moves
        /// below it and the card grows to fit. Run after <see cref="AdsUi"/>. Safe to run again.
        /// </summary>
        [MenuItem("Tools/Circuit Cube/Art V2/Build Version UI")]
        public static void VersionUi()
        {
            var root = PrefabUtility.LoadPrefabContents(UiPrefab);
            try
            {
                var st = root.GetComponent<UIRoot>().Settings;
                var panel = st.Panel;
                var line = Fresh(panel, "Version", TopC, TopC, TopC, new Vector2(0, -698), new Vector2(760, 56));
                Img(line, null, UiRole.None).color = new Color(1, 1, 1, 0);   // invisible hit area
                st.VersionLabel = Label(line, "Label", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero, "Version 1.0.0", 26, UiRole.TextMuted);
                st.VersionLabel.raycastTarget = false;
                st.VersionButton = line.gameObject.AddComponent<Button>();
                st.VersionButton.transition = Selectable.Transition.None;   // a plain line of text, not a button to the eye
                st.SettingsHeight = 820f;
                st.PausedHeight = 1100f;
                if (st.Privacy != null) ((RectTransform)st.Privacy.transform).anchoredPosition = new Vector2(0, -760);
                PrefabUtility.SaveAsPrefabAsset(root, UiPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[CircuitCube] Version UI built.");
        }

        /// <summary>
        /// First-time tutorial layer right above the HUD: a pointer (ring and fingertip), a speech bubble at the bottom
        /// with a Skip button. Hidden; only Skip catches touches. Safe to run again.
        /// </summary>
        [MenuItem("Tools/Circuit Cube/Art V2/Build Tutorial UI")]
        public static void TutorialUi()
        {
            var root = PrefabUtility.LoadPrefabContents(UiPrefab);
            try
            {
                var ui = root.GetComponent<UIRoot>();
                var safe = root.transform.Find("SafeArea");
                var layer = Fresh(safe, "Tutorial", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero);
                layer.SetSiblingIndex(ui.Hud.transform.GetSiblingIndex() + 1);   // over the HUD, under the dim and cards
                var tut = layer.gameObject.AddComponent<TutorialOverlay>();

                var pointer = Fresh(layer, "Pointer", Half, Half, Half, Vector2.zero, new Vector2(150, 150));
                tut.Ring = Pic(pointer, "Ring", Half, Half, Half, Vector2.zero, new Vector2(150, 150), S("ui_circle"), UiRole.Accent);
                tut.Dot = Pic(pointer, "Dot", Half, Half, Half, Vector2.zero, new Vector2(64, 64), S("ui_circle"), UiRole.Accent);
                var core = Pic(tut.Dot.transform, "Core", Half, Half, Half, Vector2.zero, new Vector2(28, 28), S("ui_circle"), UiRole.OnPrimary);
                tut.Core = core;
                tut.Pointer = pointer;

                var bubble = Fresh(layer, "Bubble", BottomC, BottomC, new Vector2(0.5f, 0), new Vector2(0, 300), new Vector2(980, 150));
                Img(bubble, S("ui_card"), UiRole.Card, true).raycastTarget = false;
                tut.Text = Label(bubble, "Text", new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(-260, -24),
                    "Tap this tile to turn it", 38, UiRole.Text, TextAnchor.MiddleLeft);
                tut.Text.raycastTarget = false;
                var skip = Fresh(bubble, "Skip", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(180, 90));
                Img(skip, S("ui_button"), UiRole.Secondary, true);
                Label(skip, "Label", Vector2.zero, Vector2.one, Half, Vector2.zero, new Vector2(-12, -8), "Skip", 34, UiRole.OnSecondary).raycastTarget = false;
                tut.Skip = skip.gameObject.AddComponent<Button>();
                skip.gameObject.AddComponent<UiClickSound>();
                tut.Bubble = bubble;

                layer.gameObject.SetActive(false);
                ui.Tutorial = tut;
                PrefabUtility.SaveAsPrefabAsset(root, UiPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[CircuitCube] Tutorial UI built.");
        }

        /// <summary>
        /// "Zoom out" pill under the hint and help buttons, hidden until the player zooms into the board. Safe to run again.
        /// </summary>
        [MenuItem("Tools/Circuit Cube/Art V2/Build Zoom UI")]
        public static void ZoomUi()
        {
            var root = PrefabUtility.LoadPrefabContents(UiPrefab);
            try
            {
                var hud = root.GetComponent<UIRoot>().Hud;
                var b = Fresh(hud.transform, "ZoomReset", TopR, TopR, TopR, new Vector2(-40, -300), new Vector2(240, 84));
                Img(b, S("ui_button"), UiRole.Secondary, true);
                Label(b, "Label", Vector2.zero, Vector2.one, Half, Vector2.zero, new Vector2(-16, -8), "Zoom out", 34, UiRole.OnSecondary).raycastTarget = false;
                hud.ZoomResetButton = b.gameObject.AddComponent<Button>();
                b.gameObject.AddComponent<UiClickSound>();
                b.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, UiPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[CircuitCube] Zoom UI built.");
        }

        /// <summary>
        /// Three star images above every card (hidden): on a win they burst out of the breaking box and fly into the
        /// result card's stars. Safe to run again.
        /// </summary>
        [MenuItem("Tools/Circuit Cube/Art V2/Build Flying Stars")]
        public static void FlyingStars()
        {
            var root = PrefabUtility.LoadPrefabContents(UiPrefab);
            try
            {
                var ui = root.GetComponent<UIRoot>();
                var safe = root.transform.Find("SafeArea");
                var layer = Fresh(safe, "FlyingStars", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero);
                var stars = new RectTransform[3];
                for (int i = 0; i < stars.Length; i++)
                {
                    var img = Pic(layer, "Star" + i, Half, Half, Half, Vector2.zero, new Vector2(120, 120), Icon("icon_star_full"), UiRole.Star);
                    img.gameObject.SetActive(false);
                    stars[i] = img.rectTransform;
                }
                layer.SetAsLastSibling();   // over the dim and the cards
                ui.FlyingStars = stars;
                PrefabUtility.SaveAsPrefabAsset(root, UiPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[CircuitCube] Flying stars built.");
        }

        /// <summary>
        /// "New levels" pill on the shape card, a dot on Home's Shapes button, and the wider level grid on tablets
        /// (WideColumn on the Levels screen, GridColumns on its grid). Safe to run again.
        /// </summary>
        [MenuItem("Tools/Circuit Cube/Art V2/Build New-Level Badges and Tablet Grid")]
        public static void NewLevelsAndTablets()
        {
            const string CardPrefab = "Assets/CircuitCube/Prefabs/UI/ShapeCard.prefab";
            var card = PrefabUtility.LoadPrefabContents(CardPrefab);
            try
            {
                var pill = Fresh(card.transform, "NewBadge", Vector2.one, Vector2.one, Vector2.one, new Vector2(-24, -24), new Vector2(200, 52));
                Img(pill, S("ui_pill"), UiRole.Accent, true).raycastTarget = false;
                Label(pill, "Label", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero, "New levels", 26, UiRole.OnPrimary).raycastTarget = false;
                pill.gameObject.SetActive(false);
                card.GetComponent<ShapeCard>().NewBadge = pill.gameObject;
                PrefabUtility.SaveAsPrefabAsset(card, CardPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(card); }

            var root = PrefabUtility.LoadPrefabContents(UiPrefab);
            try
            {
                var ui = root.GetComponent<UIRoot>();
                var dot = Pic(ui.Home.ShapesButton.transform, "NewDot", Vector2.one, Vector2.one, Half, new Vector2(-26, -26), new Vector2(36, 36), S("ui_circle"), UiRole.Star);
                dot.gameObject.SetActive(false);
                ui.Home.ShapesBadge = dot.gameObject;
                var levels = ui.Levels.gameObject;
                if (levels.GetComponent<WideColumn>() == null) levels.AddComponent<WideColumn>();
                if (ui.Levels.Grid.GetComponent<GridColumns>() == null) ui.Levels.Grid.gameObject.AddComponent<GridColumns>();
                PrefabUtility.SaveAsPrefabAsset(root, UiPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[CircuitCube] New-level badges and tablet grid built.");
        }

        // ------------------------------------------------------------------ screens

        static void Splash(UIRoot ui, Transform safe)
        {
            var s = Fresh(safe, "Splash", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero);
            var screen = s.gameObject.AddComponent<SplashScreen>();
            var back = Pic(s, "Background", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero, null, UiRole.Background);
            ArtV2Setup.Backdrop(back.transform);
            Pic(s, "Logo", C, C, Half, new Vector2(0, 220), new Vector2(420, 420), S("logo_mark"), UiRole.None);
            var word = Label(s, "Wordmark", C, C, Half, new Vector2(0, -60), new Vector2(1000, 120), "CIRCUIT <color=" + Brand + ">CUBE</color>", 92, UiRole.Text);
            word.fontStyle = FontStyles.Bold;
            Label(s, "Tagline", C, C, Half, new Vector2(0, -150), new Vector2(900, 60), "Connect the light", 40, UiRole.TextMuted);
            var track = Pic(s, "Track", BottomC, BottomC, Half, new Vector2(0, 260), new Vector2(460, 16), S("ui_bar"), UiRole.Track, true);
            screen.Fill = Pic(track.transform, "Fill", Vector2.zero, new Vector2(0.4f, 1f), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero, S("ui_bar"), UiRole.Accent, true).rectTransform;
            screen.Status = Label(s, "Status", BottomC, BottomC, Half, new Vector2(0, 200), new Vector2(900, 50), "Loading levels...", 30, UiRole.TextMuted);
            s.gameObject.SetActive(false);
            ui.Splash = screen;
        }

        static void Home(UIRoot ui, Transform safe)
        {
            var h = ui.Home;
            var t = h.transform;
            // top right: star counter and settings
            h.SettingsButton = RoundButton(t, "Settings", TopR, TopR, TopR, new Vector2(-36, -36), new Vector2(120, 120), "icon_settings");
            h.StarsLabel = StarChip(t, new Vector2(-176, -48));
            Off(t, "Stars");
            Off(t, "Shape");     // the group name now sits on the shape card

            // current shape card
            var card = Fresh(t, "GroupCard", BottomC, BottomC, new Vector2(0.5f, 0f), new Vector2(0, 430), new Vector2(920, 180));
            Img(card, S("ui_card"), UiRole.Card, true);
            h.GroupIcon = Pic(card, "Icon", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(130, 130), null, UiRole.None);
            h.GroupIcon.preserveAspect = true;
            h.GroupName = Label(card, "Name", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(190, -30), new Vector2(-220, 64), "Cube", 46, UiRole.Text, TextAnchor.MiddleLeft);
            h.GroupProgress = Label(card, "Progress", new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-34, -30), new Vector2(-220, 64), "Level 1 / 100", 32, UiRole.TextMuted, TextAnchor.MiddleRight);
            var track = Pic(card, "Track", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(190, 42), new Vector2(-224, 16), S("ui_bar"), UiRole.Track, true);
            h.GroupFill = Pic(track.transform, "Fill", Vector2.zero, new Vector2(0.1f, 1f), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero, S("ui_bar"), UiRole.Accent, true).rectTransform;

            // play, then shapes and levels side by side
            Place(t, "Play", BottomC, BottomC, new Vector2(0.5f, 0f), new Vector2(0, 230), new Vector2(920, 170));
            Place(t, "Shapes", BottomC, BottomC, new Vector2(0.5f, 0f), new Vector2(-235, 60), new Vector2(450, 140));
            CenterLabel(t.Find("Shapes"));
            var levels = t.Find("Levels");
            if (levels == null)
            {
                levels = Object.Instantiate(t.Find("Shapes").gameObject, t).transform;
                levels.name = "Levels";
            }
            Place(t, "Levels", BottomC, BottomC, new Vector2(0.5f, 0f), new Vector2(235, 60), new Vector2(450, 140));
            levels.Find("Label").GetComponent<TMP_Text>().text = "Levels";
            CenterLabel(levels);
            h.LevelsButton = levels.GetComponent<Button>();
            h.ShapesButton = t.Find("Shapes").GetComponent<Button>();
        }

        static void Shapes(UIRoot ui, Transform safe)
        {
            ui.Shapes.StarsLabel = StarChip(ui.Shapes.transform, new Vector2(-40, -58));
        }

        static void Levels(UIRoot ui, Transform safe)
        {
            var lv = ui.Levels;
            var t = lv.transform;
            lv.StarsLabel = StarChip(t, new Vector2(-40, -58));
            Off(t, "Subtitle");   // the progress card below the title replaces it
            var card = Fresh(t, "ProgressCard", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -200), new Vector2(-80, 140));
            Img(card, S("ui_card"), UiRole.Card, true);
            Label(card, "Label", new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(0, 1), new Vector2(40, -22), new Vector2(0, 54), "Progress", 32, UiRole.TextMuted, TextAnchor.MiddleLeft);
            lv.ProgressValue = Label(card, "Value", new Vector2(0.4f, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -22), new Vector2(0, 54), "0 / 100", 32, UiRole.TextMuted, TextAnchor.MiddleRight);
            var track = Pic(card, "Track", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 32), new Vector2(-80, 16), S("ui_bar"), UiRole.Track, true);
            lv.ProgressFill = Pic(track.transform, "Fill", Vector2.zero, new Vector2(0.1f, 1f), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero, S("ui_bar"), UiRole.Accent, true).rectTransform;
            // the grid starts under the progress card
            var scroll = (RectTransform)t.Find("Scroll");
            scroll.anchoredPosition = new Vector2(0, -175);
            scroll.sizeDelta = new Vector2(-80, -430);
        }

        static void Hud(UIRoot ui, Transform safe)
        {
            var hud = ui.Hud;
            var t = hud.transform;
            // top: pause on the left, hint and help on the right
            SetIcon(t.Find("Back"), "icon_pause");
            hud.HelpButton = RoundButton(t, "Help", TopR, TopR, TopR, new Vector2(-36, -36), new Vector2(130, 130), "icon_help");
            var oldRestart = t.Find("Restart");
            if (oldRestart != null && oldRestart.GetComponent<Image>().sprite == S("ui_circle")) Object.DestroyImmediate(oldRestart.gameObject);
            Place(t, "Title", TopC, TopC, TopC, new Vector2(-75, -40), new Vector2(560, 76));
            Place(t, "Status", TopC, TopC, TopC, new Vector2(-75, -118), new Vector2(560, 50));

            // bottom bar
            hud.UndoButton = TextButton(t, "Undo", BottomC, new Vector2(-235, 40), new Vector2(450, 130), false, "icon_undo", "Undo", 44);
            hud.RestartButton = TextButton(t, "Restart", BottomC, new Vector2(235, 40), new Vector2(450, 130), false, "icon_restart", "Restart", 44);

            // lamp progress: a dot per lamp and a count
            var dots = Fresh(t, "LampDots", BottomC, BottomC, new Vector2(0.5f, 0), new Vector2(0, 240), new Vector2(900, 34));
            var row = dots.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 14; row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = false;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            hud.DotTemplate = Pic(dots, "Dot", C, C, Half, Vector2.zero, new Vector2(26, 26), S("ui_circle"), UiRole.Track);
            hud.DotTemplate.gameObject.SetActive(false);
            hud.LampDots = dots;
            hud.LampsLabel = Label(t, "LampsLabel", BottomC, BottomC, new Vector2(0.5f, 0), new Vector2(0, 186), new Vector2(900, 48), "0 / 0 lamps lit", 30, UiRole.TextMuted);
            Place(t, "Tip", BottomC, BottomC, new Vector2(0.5f, 0), new Vector2(0, 290), new Vector2(980, 100));
        }

        static void Result(UIRoot ui, Transform safe)
        {
            var r = ui.Result;
            var panel = r.transform.Find("Panel");
            ((RectTransform)panel).sizeDelta = new Vector2(880, 1120);
            var oldBadge = panel.Find("Badge");
            if (oldBadge != null && oldBadge.Find("Title") != null) Object.DestroyImmediate(oldBadge.gameObject);

            r.Art = Pic(panel, "Mascot", TopC, TopC, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(240, 240), S("mascot_win"), UiRole.None);
            r.Art.preserveAspect = true;
            r.Caption = Label(panel, "Caption", TopC, TopC, TopC, new Vector2(0, -140), new Vector2(780, 44), "CUBE  ·  LEVEL 1", 30, UiRole.TextMuted);
            r.Title = Label(panel, "Title", TopC, TopC, TopC, new Vector2(0, -186), new Vector2(780, 84), "Level complete", 62, UiRole.Text);
            Place(panel, "Stars", TopC, TopC, TopC, new Vector2(0, -276), new Vector2(430, 130));

            var badge = Fresh(panel, "Expert", TopC, TopC, TopC, new Vector2(0, -412), new Vector2(230, 58));
            Img(badge, S("ui_pill"), UiRole.Accent, true);
            Label(badge, "Label", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero, "EXPERT", 30, UiRole.OnPrimary).fontStyle = FontStyles.Bold;
            r.Badge = badge.gameObject;

            var names = new[] { "Moves", "Par", "Lamps" };
            r.Stats = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                var tile = Fresh(panel, "Stat" + names[i], TopC, TopC, TopC, new Vector2((i - 1) * 255, -492), new Vector2(235, 150));
                var bg = Img(tile, S("ui_square"), UiRole.Track, true);
                bg.GetComponent<ThemeRole>().Alpha = 0.55f;
                Label(tile, "Label", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(0, 40), names[i], 28, UiRole.TextMuted);
                r.Stats[i] = Label(tile, "Value", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(0, 70), "0", 54, UiRole.Text);
            }
            Place(panel, "Body", TopC, TopC, TopC, new Vector2(0, -664), new Vector2(780, 96));
            r.Body.fontSize = 30;

            Place(panel, "Next", BottomC, BottomC, new Vector2(0.5f, 0), new Vector2(0, 190), new Vector2(760, 150));
            Place(panel, "Replay", new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(60, 50), new Vector2(370, 120));
            var home = panel.Find("Levels") ?? panel.Find("Home");
            home.name = "Home";
            Place(panel, "Home", new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-60, 50), new Vector2(370, 120));
            home.Find("Label").GetComponent<TMP_Text>().text = "Home";
            SetIcon(home, "icon_home");
        }

        static void Unlock(UIRoot ui, Transform safe, AnimationClip clip)
        {
            var c = Fresh(safe, "UnlockCard", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero);
            var card = c.gameObject.AddComponent<Card>();
            card.Anim = Anim(c.gameObject, clip);
            var p = Fresh(c, "Panel", C, C, Half, Vector2.zero, new Vector2(880, 960));
            Img(p, S("ui_card"), UiRole.Card, true);
            card.Caption = Label(p, "Caption", TopC, TopC, TopC, new Vector2(0, -56), new Vector2(780, 44), "NEW SHAPE UNLOCKED", 30, UiRole.Accent);
            card.Caption.fontStyle = FontStyles.Bold;
            var art = Fresh(p, "ArtTile", TopC, TopC, TopC, new Vector2(-80, -130), new Vector2(280, 280));
            var artBg = Img(art, S("ui_square"), UiRole.Track, true);
            artBg.GetComponent<ThemeRole>().Alpha = 0.45f;
            card.Art = Pic(art, "Icon", Vector2.zero, Vector2.one, Half, Vector2.zero, new Vector2(-40, -40), null, UiRole.None);
            card.Art.preserveAspect = true;
            Pic(p, "Mascot", TopC, TopC, TopC, new Vector2(170, -130), new Vector2(170, 170), S("mascot_idle"), UiRole.None).preserveAspect = true;
            card.Title = Label(p, "Title", TopC, TopC, TopC, new Vector2(0, -440), new Vector2(780, 84), "Tetra", 64, UiRole.Text);
            card.Body = Label(p, "Body", TopC, TopC, TopC, new Vector2(0, -530), new Vector2(780, 110), "", 34, UiRole.TextMuted, TextAnchor.UpperCenter);
            card.Primary = TextButton(p, "Play", BottomC, new Vector2(0, 190), new Vector2(760, 150), true, "icon_play", "Play now", 52);
            card.PrimaryLabel = card.Primary.transform.Find("Label").GetComponent<TMP_Text>();
            card.SecondaryA = TextButton(p, "Later", BottomC, new Vector2(0, 50), new Vector2(760, 120), false, null, "Later", 42);
            c.gameObject.SetActive(false);
            ui.Unlock = card;
        }

        static void Settings(UIRoot ui, Transform safe, AnimationClip clip)
        {
            var c = Fresh(safe, "SettingsCard", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero);
            var card = c.gameObject.AddComponent<SettingsCard>();
            card.Anim = Anim(c.gameObject, clip);
            var p = Fresh(c, "Panel", C, C, Half, Vector2.zero, new Vector2(880, card.PausedHeight));
            Img(p, S("ui_card"), UiRole.Card, true);
            card.Panel = p;
            card.Title = Label(p, "Title", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(60, -50), new Vector2(-240, 90), "Paused", 60, UiRole.Text, TextAnchor.MiddleLeft);
            card.Close = RoundButton(p, "Close", TopR, TopR, TopR, new Vector2(-40, -46), new Vector2(100, 100), "icon_close");

            var box = Fresh(p, "Switches", TopC, TopC, TopC, new Vector2(0, -170), new Vector2(760, 330));
            Img(box, S("ui_card"), UiRole.Track, true).GetComponent<ThemeRole>().Alpha = 0.35f;
            card.Sound = SwitchRow(box, "Sound", 0, "icon_sound");
            card.Music = SwitchRow(box, "Music", 1, "icon_music");
            card.Vibration = SwitchRow(box, "Vibration", 2, "icon_vibration");

            Label(p, "ThemeLabel", TopC, TopC, TopC, new Vector2(0, -530), new Vector2(760, 44), "THEME", 28, UiRole.TextMuted, TextAnchor.MiddleLeft);
            var seg = Fresh(p, "Theme", TopC, TopC, TopC, new Vector2(0, -582), new Vector2(760, 100));
            Img(seg, S("ui_pill"), UiRole.Track, true).GetComponent<ThemeRole>().Alpha = 0.55f;
            var modes = new[] { "System", "Light", "Dark" };
            card.ThemeButtons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                var b = Fresh(seg, modes[i], new Vector2(i / 3f, 0), new Vector2((i + 1) / 3f, 1), Half, Vector2.zero, new Vector2(-12, -14));
                Img(b, S("ui_pill"), i == 2 ? UiRole.Card : UiRole.Transparent, true);
                Label(b, "Label", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero, modes[i], 34, i == 2 ? UiRole.Text : UiRole.TextMuted);
                card.ThemeButtons[i] = b.gameObject.AddComponent<Button>();
            }

            var acts = Fresh(p, "PauseActions", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 320));
            card.PauseActions = acts.gameObject;
            card.Resume = TextButton(acts, "Resume", BottomC, new Vector2(0, 180), new Vector2(760, 140), true, "icon_play", "Resume", 50);
            card.Restart = TextButton(acts, "Restart", new Vector2(0, 0), new Vector2(60, 46), new Vector2(370, 116), false, "icon_restart", "Restart", 40);
            card.Home = TextButton(acts, "Home", new Vector2(1, 0), new Vector2(-60, 46), new Vector2(370, 116), false, "icon_home", "Home", 40);
            c.gameObject.SetActive(false);
            ui.Settings = card;
        }

        // ------------------------------------------------------------------ parts

        static SwitchToggle SwitchRow(RectTransform box, string name, int index, string icon)
        {
            var row = Fresh(box, name, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -index * 110), new Vector2(0, 110));
            Pic(row, "Icon", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(34, 0), new Vector2(54, 54), Icon(icon), UiRole.TextMuted);
            Label(row, "Label", new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(110, 0), new Vector2(-300, 0), name, 40, UiRole.Text, TextAnchor.MiddleLeft);
            var track = Fresh(row, "Switch", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-34, 0), new Vector2(130, 72));
            var sw = track.gameObject.AddComponent<SwitchToggle>();
            sw.Track = Img(track, S("ui_pill"), UiRole.Primary, true);
            sw.Button = track.gameObject.AddComponent<Button>();
            var knob = Pic(track, "Knob", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-6, 0), new Vector2(60, 60), S("ui_circle"), UiRole.None);
            knob.color = Color.white;
            sw.Knob = knob.rectTransform;
            if (index < 2) Pic(row, "Divider", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(-60, 2), null, UiRole.Track);
            return sw;
        }

        /// <summary>A pill with a star and the total stars, top right (Shapes and Levels) or left of the settings button.</summary>
        static TMP_Text StarChip(Transform screen, Vector2 pos)
        {
            var chip = Fresh(screen, "StarChip", TopR, TopR, TopR, pos, new Vector2(200, 96));
            Img(chip, S("ui_pill"), UiRole.Chip, true);
            Pic(chip, "Star", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(52, 52), Icon("icon_star_full"), UiRole.Star);
            return Label(chip, "Count", Vector2.zero, Vector2.one, new Vector2(0, 0.5f), new Vector2(86, 0), new Vector2(-100, 0), "0", 40, UiRole.Text, TextAnchor.MiddleLeft);
        }

        static Button RoundButton(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size, string icon)
        {
            var b = Fresh(parent, name, aMin, aMax, pivot, pos, size);
            Img(b, S("ui_circle"), UiRole.Round);
            Pic(b, "Icon", Vector2.zero, Vector2.one, Half, Vector2.zero, size * -0.46f, Icon(icon), UiRole.OnRound);
            return b.gameObject.AddComponent<Button>();
        }

        static Button TextButton(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, bool primary, string icon, string text, int fontSize)
        {
            var b = Fresh(parent, name, anchor, anchor, new Vector2(anchor.x, 0), pos, size);
            Img(b, S("ui_button"), primary ? UiRole.Primary : UiRole.Secondary, true);
            var on = primary ? UiRole.OnPrimary : UiRole.OnSecondary;
            var label = Label(b, "Label", Vector2.zero, Vector2.one, Half, Vector2.zero, new Vector2(-16, -16), text, fontSize, on);
            if (icon != null)
            {
                // icon and text centred together
                float w = label.preferredWidth;
                float iconSize = Mathf.Round(size.y * 0.42f);
                Pic(b, "Icon", C, C, Half, new Vector2(-(w + 16) * 0.5f, 2), new Vector2(iconSize, iconSize), Icon(icon), on);
                ((RectTransform)label.transform).anchoredPosition = new Vector2((iconSize + 16) * 0.5f, 0);
            }
            return b.gameObject.AddComponent<Button>();
        }

        static void CenterLabel(Transform button)
        {
            var icon = button.Find("Icon");
            if (icon != null) icon.gameObject.SetActive(false);
            var label = (RectTransform)button.Find("Label");
            label.anchoredPosition = Vector2.zero;
            label.sizeDelta = new Vector2(-16, -16);
        }

        static void SetIcon(Transform button, string icon)
        {
            var i = button != null ? button.Find("Icon") : null;
            if (i != null) i.GetComponent<Image>().sprite = Icon(icon);
        }

        static AnimationClip CardClip(Transform safe)
        {
            var tip = safe.Find("TipCard");
            var anim = tip != null ? tip.GetComponent<Animation>() : null;
            return anim != null ? anim.GetClip("In") : null;
        }

        static Animation Anim(GameObject go, AnimationClip clip)
        {
            var a = go.AddComponent<Animation>();
            a.playAutomatically = false;
            if (clip != null) { a.AddClip(clip, "In"); a.clip = clip; }
            return a;
        }

        // ------------------------------------------------------------------ helpers

        static readonly Vector2 Half = new Vector2(0.5f, 0.5f), C = Half;
        static readonly Vector2 TopC = new Vector2(0.5f, 1f), TopR = new Vector2(1f, 1f), BottomC = new Vector2(0.5f, 0f);

        static Sprite S(string name) => ArtPaths.Sprite(name);
        static Sprite Icon(string name) => ArtPaths.Icon(name);

        /// <summary>A new child (an old one of the same name is replaced) with the given rect.</summary>
        static RectTransform Fresh(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var old = parent.Find(name);
            int index = old != null ? old.GetSiblingIndex() : -1;
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            if (index >= 0) rt.SetSiblingIndex(index);
            Set(rt, aMin, aMax, pivot, pos, size);
            return rt;
        }

        static void Place(Transform parent, string path, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var t = parent.Find(path) as RectTransform;
            if (t == null) { Debug.LogWarning("[CircuitCube] UI path not found: " + path); return; }
            Set(t, aMin, aMax, pivot, pos, size);
        }

        static void Set(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        static void Off(Transform parent, string path)
        {
            var t = parent.Find(path);
            if (t != null) t.gameObject.SetActive(false);
        }

        static Image Img(RectTransform rt, Sprite sprite, UiRole role, bool sliced = false)
        {
            var img = rt.GetComponent<Image>() ?? rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            if (role != UiRole.None) (rt.GetComponent<ThemeRole>() ?? rt.gameObject.AddComponent<ThemeRole>()).Role = role;
            return img;
        }

        static Image Pic(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size, Sprite sprite, UiRole role, bool sliced = false)
        {
            var rt = Fresh(parent, name, aMin, aMax, pivot, pos, size);
            var img = Img(rt, sprite, role, sliced);
            if (!sliced && sprite != null) img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        static TextMeshProUGUI Label(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size,
            string text, int fontSize, UiRole role, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var rt = Fresh(parent, name, aMin, aMax, pivot, pos, size);
            var t = UiText.On(rt.gameObject, text, fontSize, align);
            if (role != UiRole.None) rt.gameObject.AddComponent<ThemeRole>().Role = role;
            return t;
        }
    }
}
