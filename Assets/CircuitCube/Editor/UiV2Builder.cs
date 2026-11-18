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
    public static partial class UiV2Builder
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
            LevelGridV3();
            ZoomUi();
            TutorialUi();
            Ui120();
            Ui120Fx();
            Ui130Ftue();
            Debug.Log("[CircuitCube] UI V2 built.");
        }

        /// <summary>
        /// Version 1.2.0 settings: volume sliders for effects and music in place of their switches (pill track, filled
        /// pill, round knob), a language choice (English / Tiếng Việt) under the theme, and a <see cref="LocText"/> on
        /// every text of the UI prefab so fixed labels follow the language. Run after <see cref="VersionUi"/>.
        /// Safe to run again.
        /// </summary>
        [MenuItem("Tools/Circuit Cube/Art V2/Build 1.2.0 UI (volume, language)")]
        public static void Ui120()
        {
            var root = PrefabUtility.LoadPrefabContents(UiPrefab);
            try
            {
                var st = root.GetComponent<UIRoot>().Settings;
                var panel = st.Panel;
                var box = panel.Find("Switches");
                st.SoundVolume = VolumeSlider(box.Find("Sound"));
                st.MusicVolume = VolumeSlider(box.Find("Music"));

                // language: label and a two-way segment like the theme one, below it
                Label(panel, "LanguageLabel", TopC, TopC, TopC, new Vector2(0, -700), new Vector2(760, 44), "LANGUAGE", 28, UiRole.TextMuted, TextAnchor.MiddleLeft);
                var seg = Fresh(panel, "Language", TopC, TopC, TopC, new Vector2(0, -752), new Vector2(760, 100));
                Img(seg, S("ui_pill"), UiRole.Track, true).GetComponent<ThemeRole>().Alpha = 0.55f;
                var names = new[] { "English", "Tiếng Việt" };
                st.LanguageButtons = new Button[2];
                for (int i = 0; i < 2; i++)
                {
                    var b = Fresh(seg, i == 0 ? "English" : "Vietnamese", new Vector2(i / 2f, 0), new Vector2((i + 1) / 2f, 1), Half, Vector2.zero, new Vector2(-12, -14));
                    Img(b, S("ui_pill"), i == 0 ? UiRole.Card : UiRole.Transparent, true);
                    Label(b, "Label", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero, names[i], 34, i == 0 ? UiRole.Text : UiRole.TextMuted);
                    st.LanguageButtons[i] = b.gameObject.AddComponent<Button>();
                    b.gameObject.AddComponent<UiClickSound>();
                }
                var version = panel.Find("Version") as RectTransform;
                if (version != null) version.anchoredPosition = new Vector2(0, -868);
                if (st.Privacy != null) ((RectTransform)st.Privacy.transform).anchoredPosition = new Vector2(0, -930);
                st.SettingsHeight = 990f;
                st.PausedHeight = 1270f;

                // fixed texts follow the language; the language names stay as they are
                int added = 0;
                foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (t.transform.parent != null && t.transform.parent.parent == seg) continue;
                    if (t.GetComponent<LocText>() == null) { t.gameObject.AddComponent<LocText>(); added++; }
                }
                PrefabUtility.SaveAsPrefabAsset(root, UiPrefab);
                Debug.Log("[CircuitCube] 1.2.0 UI built; LocText added to " + added + " texts.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Pixels-per-unit multiplier that makes the pill's 9-slice ends fit a bar this tall (round ends, no pinch).</summary>
        static float PillScale(float height)
        {
            var sp = S("ui_pill");
            float border = sp != null ? Mathf.Max(sp.border.x, sp.border.z) : height / 2f;
            return Mathf.Max(1f, border * 2f / height);
        }

        const string LampGroupPrefab = "Assets/CircuitCube/Prefabs/UI/LampGroup.prefab";

        /// <summary>
        /// Version 1.2.0 play and win feel, all in UI.prefab (plus the LampGroup item prefab): the win effect layer
        /// (flying EXPERT letters, burst, comets, gold mascot overlay, constellation dots and lines), the neon word and
        /// the praise line in the result card, the lamp tray above Undo/Restart in place of the colour chips and lamp
        /// dots, and the mascot with its speech bubble under the top-right buttons. Safe to run again.
        /// </summary>
        [MenuItem("Tools/Circuit Cube/Art V2/Build 1.2.0 FX (win effects, lamp tray, mascot)")]
        public static void Ui120Fx()
        {
            BuildLampGroupPrefab();
            var root = PrefabUtility.LoadPrefabContents(UiPrefab);
            try
            {
                var ui = root.GetComponent<UIRoot>();
                var safe = root.transform.Find("SafeArea");
                string[] word = { "expert_E", "expert_X", "expert_P", "expert_E", "expert_R", "expert_T" };

                // ---- result card: neon word where the stars are, praise above the card
                var r = ui.Result;
                var panel = (RectTransform)r.transform.Find("Panel");
                if (r.GetComponent<CanvasGroup>() == null) r.gameObject.AddComponent<CanvasGroup>();   // the constellations fade the card in
                // laid out from the approved mockup (390 px phone, card 314 px wide = 880 units, so 1 px = 2.803 units):
                // letter boxes 36 x 44 px, 40 px apart, centred 124 px below the card top; sprites carry 90 px of glow
                var w = Fresh(panel, "ExpertWord", TopC, TopC, Half, new Vector2(0, -348), new Vector2(700, 160));
                var letters = new Image[6];
                for (int i = 0; i < 6; i++)
                    letters[i] = Pic(w, "L" + i, Half, Half, Half, new Vector2((i - 2.5f) * 112.1f, 0), LetterSize, S(word[i]), UiRole.None);
                // shine strip: 232 x 56 px, 2 px above the letter boxes
                var mask = Fresh(w, "ShineMask", Half, Half, Half, new Vector2(0, -5), new Vector2(650, 157));
                mask.gameObject.AddComponent<RectMask2D>();
                var shine = Pic(mask, "Shine", Half, Half, Half, new Vector2(-400, 0), new Vector2(168, 157), S("fx_word_shine"), UiRole.None);
                shine.preserveAspect = false;
                w.gameObject.SetActive(false);
                var praise = Label(panel, "Praise", TopC, TopC, new Vector2(0.5f, 0), new Vector2(0, 146), new Vector2(940, 110), "Brilliant!", 84, UiRole.None);
                praise.fontStyle = FontStyles.Bold | FontStyles.Italic;
                praise.color = new Color(1f, 0.878f, 0.541f);
                praise.raycastTarget = false;
                praise.gameObject.SetActive(false);

                // ---- effect layer over everything
                var layer = Fresh(safe, "Fx", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero);
                layer.SetAsLastSibling();
                var fx = layer.gameObject.AddComponent<ResultFx>();
                fx.Layer = layer;
                fx.ShakeRoot = panel;
                fx.Word = w; fx.WordLetters = letters; fx.Shine = shine.rectTransform; fx.Praise = praise;
                fx.FlyLetters = new Image[6];
                fx.HotLetters = new Image[6];
                fx.Pings = new Image[6];
                for (int i = 0; i < 6; i++)
                {
                    fx.FlyLetters[i] = Hidden(Pic(layer, "Fly" + i, Half, Half, Half, Vector2.zero, LetterSize, S(word[i]), UiRole.None));
                    fx.HotLetters[i] = Pic(fx.FlyLetters[i].transform, "Hot", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero, S(word[i] + "_hot"), UiRole.None);
                    fx.Pings[i] = Hidden(Pic(layer, "Ping" + i, Half, Half, Half, Vector2.zero, new Vector2(179, 202), S("fx_ping"), UiRole.None));
                }
                // the mockup's burst: core 36 px (+110 px glow each side), ring 40 px (+34), 6 px dots (+20)
                fx.Core = Hidden(Pic(layer, "Core", Half, Half, Half, Vector2.zero, new Vector2(718, 718), S("fx_boom_core"), UiRole.None));
                fx.Ring = Hidden(Pic(layer, "Ring", Half, Half, Half, Vector2.zero, new Vector2(303, 303), S("fx_boom_ring"), UiRole.None));
                fx.Sparks = new Image[12];
                for (int i = 0; i < 12; i++) fx.Sparks[i] = Hidden(Pic(layer, "Spark" + i, Half, Half, Half, Vector2.zero, new Vector2(129, 129), S("fx_boom_dot"), UiRole.None));
                fx.Comets = new Image[15];
                for (int i = 0; i < 15; i++)
                {
                    var c = Pic(layer, "Comet" + i, Half, Half, Half, Vector2.zero, new Vector2(330, 156), S("fx_comet"), UiRole.None);
                    c.rectTransform.pivot = new Vector2(0.9f, 0.5f);   // the head leads
                    fx.Comets[i] = Hidden(c);
                }
                var goldBody = Hidden(Pic(layer, "GoldBody", Half, Half, Half, Vector2.zero, new Vector2(240, 240), S("mascot_body"), UiRole.None));
                fx.GoldFrame = Hidden(Pic(goldBody.transform, "GoldFrame", Half, Half, Half, Vector2.zero, new Vector2(246, 246), S("fx_frame_gold"), UiRole.None));
                fx.GoldFace = Hidden(Pic(goldBody.transform, "GoldFace", Half, Half, Half, Vector2.zero, new Vector2(200, 180), S("face_excited_gold"), UiRole.None));
                fx.GoldBody = goldBody;
                fx.Beams = new[] { Hidden(Pic(layer, "Beam0", Half, Half, Half, Vector2.zero, new Vector2(80, 80), S("fx_glow"), UiRole.None)),
                                   Hidden(Pic(layer, "Beam1", Half, Half, Half, Vector2.zero, new Vector2(80, 80), S("fx_glow"), UiRole.None)) };
                // constellations, sized from the mockup: lamp light 12 px (+30 px glow), 8 px dots (+24), 1.6 px strokes (+12)
                fx.LampGlows = new Image[15];
                for (int i = 0; i < 15; i++)
                {
                    fx.LampGlows[i] = Hidden(Pic(layer, "LampGlow" + i, Half, Half, Half, Vector2.zero, new Vector2(202, 202), S("fx_lamp_halo"), UiRole.None));
                    Pic(fx.LampGlows[i].transform, "Core", Half, Half, Half, Vector2.zero, new Vector2(56, 56), S("fx_lamp_core"), UiRole.None);
                }
                fx.Dots = new Image[15];
                fx.Lines = new Image[15];
                for (int i = 0; i < 15; i++)
                {
                    fx.Lines[i] = Hidden(Pic(layer, "Line" + i, Half, Half, new Vector2(0, 0.5f), Vector2.zero, new Vector2(10, 73), S("fx_star_line"), UiRole.None));
                    fx.Lines[i].preserveAspect = false;
                }
                for (int i = 0; i < 15; i++) fx.Dots[i] = Hidden(Pic(layer, "Dot" + i, Half, Half, Half, Vector2.zero, new Vector2(157, 157), S("fx_star_dot"), UiRole.None));
                foreach (var g in layer.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
                ui.Fx = fx;
                // glow sprites are baked for the card colour: the light theme swaps in the "_light" copies
                foreach (var img in root.GetComponentsInChildren<Image>(true))
                {
                    if (img.sprite == null || !(img.transform.IsChildOf(layer) || img.transform.IsChildOf(w))) continue;
                    var light = S(img.sprite.name + "_light");
                    if (light == null) continue;
                    var ts = img.GetComponent<ThemeSprite>() ?? img.gameObject.AddComponent<ThemeSprite>();
                    ts.Dark = img.sprite; ts.Light = light;
                }

                // ---- HUD: lamp tray instead of the chips and the dots, mascot under the top-right buttons
                var hud = ui.Hud;
                var t = hud.transform;
                if (hud.Legend != null) hud.Legend.gameObject.SetActive(false);
                if (hud.LampDots != null) hud.LampDots.gameObject.SetActive(false);
                if (hud.LampsLabel != null) hud.LampsLabel.gameObject.SetActive(false);
                var tray = Fresh(t, "LampTray", BottomC, BottomC, new Vector2(0.5f, 0), new Vector2(0, 196), new Vector2(1024, 108));
                var row = tray.gameObject.AddComponent<HorizontalLayoutGroup>();
                row.spacing = 6 * 2.77f; row.childAlignment = TextAnchor.LowerCenter;   // mockup: 6 px gaps
                // up to three colours keep their 300 width; more share the tray (see LampGroup.Fit)
                row.childControlWidth = true; row.childControlHeight = false;
                row.childForceExpandWidth = false; row.childForceExpandHeight = false;
                hud.LampTray = tray;
                hud.GroupPrefab = AssetDatabase.LoadAssetAtPath<LampGroup>(LampGroupPrefab);
                Place(t, "Tip", BottomC, BottomC, new Vector2(0.5f, 0), new Vector2(0, 410), new Vector2(980, 100));
                var tutorial = safe.Find("Tutorial");
                if (tutorial != null) Place(tutorial, "Bubble", BottomC, BottomC, new Vector2(0.5f, 0), new Vector2(0, 400), new Vector2(980, 150));

                var m = Fresh(t, "Mascot", TopR, TopR, TopR, new Vector2(-36, -186), new Vector2(150, 150));
                var mascot = m.gameObject.AddComponent<Mascot>();
                var body = Fresh(m, "Body", Half, Half, Half, Vector2.zero, new Vector2(150, 150));
                Img(body, S("mascot_body"), UiRole.None).raycastTarget = false;
                mascot.Body = body;
                mascot.Face = Pic(body, "Face", Half, Half, Half, new Vector2(0, -2), new Vector2(124, 112), S("face_idle"), UiRole.None);
                mascot.Idle = S("face_idle"); mascot.Blink = S("face_blink"); mascot.Happy = S("face_happy");
                mascot.Think = S("face_think"); mascot.Excited = S("face_excited");
                mascot.Drop = Hidden(Pic(body, "Drop", Half, Half, Half, new Vector2(54, 26), new Vector2(30, 32), S("sweat_drop"), UiRole.None));
                mascot.Sparks = new Image[5];
                for (int i = 0; i < 5; i++) mascot.Sparks[i] = Hidden(Pic(body, "Spark" + i, Half, Half, Half, Vector2.zero, new Vector2(34, 34), S("fx_sparkle"), UiRole.None));
                var bubble = Fresh(m, "Bubble", new Vector2(0, 1), new Vector2(0, 1), new Vector2(1, 1), new Vector2(-16, -8), new Vector2(560, 120));
                var bimg = Img(bubble, S("ui_bubble"), UiRole.Text, true);
                bimg.raycastTarget = false;
                mascot.Bubble = bubble;
                mascot.BubbleText = Label(bubble, "Text", Vector2.zero, Vector2.one, Half, new Vector2(-8, 0), new Vector2(-64, -26), "Almost there!", 34, UiRole.Card);
                mascot.BubbleText.fontStyle = FontStyles.Bold;
                mascot.BubbleText.enableAutoSizing = true; mascot.BubbleText.fontSizeMin = 24; mascot.BubbleText.fontSizeMax = 34;
                mascot.BubbleText.raycastTarget = false;
                bubble.gameObject.SetActive(false);
                hud.Mascot = mascot;

                foreach (var tx in root.GetComponentsInChildren<TMP_Text>(true))
                    if (tx.GetComponent<LocText>() == null && tx.transform.IsChildOf(t)) tx.gameObject.AddComponent<LocText>();
                PrefabUtility.SaveAsPrefabAsset(root, UiPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[CircuitCube] 1.2.0 FX built.");
        }

        static Image Hidden(Image i) { i.gameObject.SetActive(false); return i; }

        /// <summary>A neon letter sprite: the mockup's 36 x 44 px box plus 90 px of glow on each side, in canvas units.</summary>
        static readonly Vector2 LetterSize = new Vector2(605, 628);

        /// <summary>
        /// The lamp tray item, option A of the approved mockup (390 px phone, 1 px = 2.77 units): a 118 px card with
        /// 7 px padding, a 7 px bar split into one cell per lamp (2 px gaps, 2 px corners, 6 px glow on lit cells) or one
        /// continuous bar past 12 lamps (8 px glow), the name and count in 11 px bold under it with a tick once done,
        /// and the formula tag (16 px high, 3 px above the card) for a mixed colour.
        /// </summary>
        static void BuildLampGroupPrefab()
        {
            const float k = 2.77f;
            var go = new GameObject("LampGroup", typeof(RectTransform));
            try
            {
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = new Vector2(118 * k, 108);
                var g = go.AddComponent<LampGroup>();
                g.Group = go.AddComponent<CanvasGroup>();
                Img(rt, S("ui_card"), UiRole.Card, true).GetComponent<ThemeRole>().Alpha = 0.93f;
                Pic(rt, "Border", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero, S("ui_frame"), UiRole.Track, true);
                var le = go.AddComponent<LayoutElement>();
                le.minWidth = 120; le.preferredWidth = 118 * k; le.flexibleWidth = 0;

                float pad = 7 * k, barH = 7 * k;
                // cells
                var segs = Fresh(rt, "Cells", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -pad), new Vector2(-2 * pad, barH));
                var row = segs.gameObject.AddComponent<HorizontalLayoutGroup>();
                row.spacing = 2 * k; row.childAlignment = TextAnchor.MiddleCenter;
                row.childControlWidth = row.childControlHeight = true;
                row.childForceExpandWidth = row.childForceExpandHeight = true;
                g.SegRow = segs.gameObject;
                g.Segs = new Image[12];
                g.SegGlows = new Image[12];
                for (int i = 0; i < 12; i++)
                {
                    var c = Pic(segs, "Cell" + i, Half, Half, Half, Vector2.zero, new Vector2(20, barH), S("fx_seg"), UiRole.None, true);
                    c.pixelsPerUnitMultiplier = 1.35f;   // 2 px corners
                    var gl = Pic(c.transform, "Glow", Vector2.zero, Vector2.one, Half, Vector2.zero, new Vector2(12 * k, 12 * k), S("fx_seg_glow"), UiRole.None, true);
                    gl.pixelsPerUnitMultiplier = 1.35f;
                    g.Segs[i] = c; g.SegGlows[i] = gl;
                }
                // continuous bar
                var bar = Fresh(rt, "Bar", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -pad), new Vector2(-2 * pad, barH));
                g.Bar = bar.gameObject;
                g.BarTrack = Img(bar, S("ui_pill"), UiRole.None, true);
                // a round-ended pill the bar's height (mockup: 4 px corners on a 7 px bar)
                float pillMul = S("ui_pill").border.x / (barH * 0.5f);
                g.BarTrack.pixelsPerUnitMultiplier = pillMul;
                var fill = Fresh(bar, "Fill", Vector2.zero, new Vector2(0.5f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
                g.BarFill = fill;
                g.BarFillImage = Img(fill, S("ui_pill"), UiRole.None, true);
                g.BarFillImage.pixelsPerUnitMultiplier = pillMul;
                g.BarGlow = Pic(fill, "Glow", Vector2.zero, Vector2.one, Half, Vector2.zero, new Vector2(16 * k, 16 * k), S("fx_bar_glow"), UiRole.None, true);
                g.BarGlow.pixelsPerUnitMultiplier = 1.35f;
                bar.gameObject.SetActive(false);

                // name and count, a tick in front once the colour is done
                var nameRow = Fresh(rt, "Name", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, pad - 4), new Vector2(-12, 40));
                var nr = nameRow.gameObject.AddComponent<HorizontalLayoutGroup>();
                nr.spacing = 4 * k; nr.childAlignment = TextAnchor.MiddleCenter;
                nr.childControlWidth = true; nr.childControlHeight = true;
                nr.childForceExpandWidth = false; nr.childForceExpandHeight = false;
                g.Check = Pic(nameRow, "Tick", Half, Half, Half, Vector2.zero, new Vector2(13 * k, 13 * k), Icon("icon_check"), UiRole.None);
                var tle = g.Check.gameObject.AddComponent<LayoutElement>();
                tle.preferredWidth = tle.minWidth = 13 * k; tle.preferredHeight = tle.minHeight = 13 * k;
                g.Check.gameObject.SetActive(false);
                g.Label = Label(nameRow, "Label", Half, Half, Half, Vector2.zero, new Vector2(200, 40), "Yellow 3/4", 30, UiRole.Text);
                g.Label.fontStyle = FontStyles.Bold;
                g.Label.enableAutoSizing = false;
                g.Label.textWrappingMode = TextWrappingModes.NoWrap;

                // formula tag above the card
                var f = Fresh(rt, "Formula", TopC, TopC, new Vector2(0.5f, 0), new Vector2(0, 3 * k), new Vector2(164, 16 * k));
                var tag = Img(f, S("fx_tag"), UiRole.None, true);
                tag.pixelsPerUnitMultiplier = 1.35f;
                var fr = Fresh(f, "Row", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero);
                var frow = fr.gameObject.AddComponent<HorizontalLayoutGroup>();
                frow.spacing = 2 * k; frow.childAlignment = TextAnchor.MiddleCenter;
                frow.childControlWidth = frow.childControlHeight = false;
                frow.childForceExpandWidth = frow.childForceExpandHeight = false;
                float dot = 8 * k;
                g.FormulaA = Pic(fr, "A", Half, Half, Half, Vector2.zero, new Vector2(dot, dot), S("ui_circle"), UiRole.None);
                Pic(fr, "Plus", Half, Half, Half, Vector2.zero, new Vector2(7 * k, 7 * k), Icon("icon_plus"), UiRole.TextMuted);
                g.FormulaB = Pic(fr, "B", Half, Half, Half, Vector2.zero, new Vector2(dot, dot), S("ui_circle"), UiRole.None);
                Pic(fr, "Arrow", Half, Half, Half, Vector2.zero, new Vector2(8 * k, 7 * k), Icon("icon_arrow"), UiRole.TextMuted);
                g.FormulaResult = Pic(fr, "Result", Half, Half, Half, Vector2.zero, new Vector2(dot, dot), S("ui_circle"), UiRole.None);
                g.Formula = f.gameObject;

                // light theme copies of the glow and tag sprites
                foreach (var img in go.GetComponentsInChildren<Image>(true))
                {
                    if (img.sprite == null) continue;
                    var light = S(img.sprite.name + "_light");
                    if (light == null) continue;
                    var ts = img.GetComponent<ThemeSprite>() ?? img.gameObject.AddComponent<ThemeSprite>();
                    ts.Dark = img.sprite; ts.Light = light;
                }
                foreach (var gr in go.GetComponentsInChildren<Graphic>(true)) gr.raycastTarget = false;
                PrefabUtility.SaveAsPrefabAsset(go, LampGroupPrefab);
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>A 0..1 slider on the right of a settings row; the row's switch is hidden (kept for older saves).</summary>
        static Slider VolumeSlider(Transform row)
        {
            var sw = row.Find("Switch");
            if (sw != null) sw.gameObject.SetActive(false);
            var label = row.Find("Label") as RectTransform;
            if (label != null) label.sizeDelta = new Vector2(-560, 0);
            var area = Fresh(row, "Volume", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-40, 0), new Vector2(-440, 72));   // 320 wide, clear of the label
            var hit = Img(area, null, UiRole.None);
            hit.color = new Color(1, 1, 1, 0);   // the whole strip takes the drag
            var bg = Pic(area, "Track", new Vector2(0, 0.5f), new Vector2(1, 0.5f), Half, Vector2.zero, new Vector2(0, 22), S("ui_pill"), UiRole.Track, true);
            bg.pixelsPerUnitMultiplier = PillScale(22);
            var fillArea = Fresh(area, "Fill Area", new Vector2(0, 0.5f), new Vector2(1, 0.5f), Half, new Vector2(-11, 0), new Vector2(-22, 22));
            var fill = Pic(fillArea, "Fill", Vector2.zero, new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(22, 0), S("ui_pill"), UiRole.Primary, true);
            fill.pixelsPerUnitMultiplier = PillScale(22);
            var handleArea = Fresh(area, "Handle Slide Area", Vector2.zero, Vector2.one, Half, Vector2.zero, new Vector2(-56, 0));
            var knob = Pic(handleArea, "Handle", new Vector2(0, 0.5f), new Vector2(0, 0.5f), Half, Vector2.zero, new Vector2(56, 56), S("ui_circle"), UiRole.None);
            knob.color = Color.white;
            var slider = area.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = knob.rectTransform;
            slider.targetGraphic = knob;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            var colors = slider.colors;
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            slider.colors = colors;
            return slider;
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
                var link = Fresh(panel, "Privacy", TopC, TopC, TopC, new Vector2(0, -760), new Vector2(760, 64));   // under the version line (VersionUi)
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
        /// V3 art (ArtRef/tools/cut_v3.py): level cells get a frame (locked levels are an outline, Expert levels a gold
        /// outline) and the Expert badge in place of the stars; the dim behind cards and the screen shades use the
        /// vignette; the settings dividers use the fading line. Adds the NodeFrame colour to the themes if missing.
        /// Safe to run again.
        /// </summary>
        [MenuItem("Tools/Circuit Cube/Art V2/Build Level Grid V3")]
        public static void LevelGridV3()
        {
            foreach (var (path, hex) in new[] { ("Assets/CircuitCube/Themes/Theme_Dark.asset", "2E3650"), ("Assets/CircuitCube/Themes/Theme_Light.asset", "BCC4D6") })
            {
                var theme = AssetDatabase.LoadAssetAtPath<GameTheme>(path);
                if (theme == null || theme.Ui.Exists(r => r.Role == UiRole.NodeFrame)) continue;
                ColorUtility.TryParseHtmlString("#" + hex, out var c);
                theme.Ui.Add(new RoleColor(UiRole.NodeFrame, c));
                EditorUtility.SetDirty(theme);
            }

            const string NodePrefab = "Assets/CircuitCube/Prefabs/UI/LevelNode.prefab";
            var node = PrefabUtility.LoadPrefabContents(NodePrefab);
            try
            {
                var ln = node.GetComponent<LevelNode>();
                var frame = Fresh(node.transform, "Frame", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero);
                frame.SetSiblingIndex(0);   // over the cell's background, under the number
                ln.Frame = Img(frame, S("ui_frame"), UiRole.None, true);
                ln.Frame.raycastTarget = false;
                ln.Frame.pixelsPerUnitMultiplier = 0.72f;   // corners as round as the cell's own (ui_square is drawn larger)
                // where the star row sits (bottom centre), a little larger than one star
                ln.Expert = Pic(node.transform, "Expert", BottomC, BottomC, BottomC, new Vector2(0, 20), new Vector2(96, 96), Icon("icon_expert_comet3_a"), UiRole.None);
                ln.Expert.gameObject.SetActive(false);
                frame.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(node, NodePrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(node); }

            var root = PrefabUtility.LoadPrefabContents(UiPrefab);
            try
            {
                var safe = root.transform.Find("SafeArea");
                foreach (var path in new[] { "Dim", "Levels/Shade", "Shapes/Shade" })
                {
                    var img = safe.Find(path)?.GetComponent<Image>();
                    if (img == null) { Debug.LogWarning("[CircuitCube] UI path not found: " + path); continue; }
                    img.sprite = S("ui_vignette");
                    img.type = Image.Type.Simple;
                    img.preserveAspect = false;
                }
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != "Divider") continue;
                    var img = t.GetComponent<Image>();
                    if (img == null) continue;
                    img.sprite = S("ui_divider");
                    img.type = Image.Type.Simple;
                    var rt = (RectTransform)t;
                    rt.sizeDelta = new Vector2(rt.sizeDelta.x, 4);
                }
                PrefabUtility.SaveAsPrefabAsset(root, UiPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[CircuitCube] Level grid V3 built.");
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
