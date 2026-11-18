using CircuitCube.Game;
using CircuitCube.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.EditorTools
{
    public static partial class UiV2Builder
    {
        /// <summary>Mockup px (390 px wide phone) to canvas units (1080 wide).</summary>
        const float U = 2.77f;
        /// <summary>Sprites rendered from the mockups carry 1.35 texels per canvas unit.</summary>
        const float Texel = 1.35f;

        static readonly Color NoteBg = new Color32(0xe9, 0xf7, 0xfa, 255), NoteInk = new Color32(0x0b, 0x1c, 0x22, 255),
            NoteTeal = new Color32(0x2a, 0x8f, 0x9c, 255), NoteNext = new Color32(0x1E, 0x9A, 0xAA, 255), NoteMuted = new Color32(0x6b, 0x87, 0x90, 255),
            ReadySub = new Color32(0x4c, 0x68, 0x70, 255);

        /// <summary>
        /// Version 1.3.0 first-time experience, laid out from the approved FTUE mockups (canvas boards FT0-FTEnd and
        /// FTZoom): the tutorial layer (note beside the HUD mascot, dim with a lit hole, rings, tapping hand, swipe
        /// arrow), the welcome and "Ready to play?" screens, the zoom pill for emulators with its first-time tip, the
        /// Zoom buttons switch in Settings and the Vietnamese Expert word (XUẤT SẮC). Run after <see cref="Ui120Fx"/>.
        /// Safe to run again.
        /// </summary>
        [MenuItem("Tools/Circuit Cube/Art V2/Build 1.3.0 FTUE UI")]
        public static void Ui130Ftue()
        {
            var root = PrefabUtility.LoadPrefabContents(UiPrefab);
            try
            {
                var ui = root.GetComponent<UIRoot>();
                var safe = root.transform.Find("SafeArea");
                BuildTutorialLayer(ui, safe);
                BuildZoomPill(ui);
                BuildZoomSwitch(ui);
                BuildVietnameseWord(ui);
                PrefabUtility.SaveAsPrefabAsset(root, UiPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[CircuitCube] 1.3.0 FTUE UI built.");
        }

        static Image Exact(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size, string sprite, bool sliced = false, float mul = Texel)
        {
            var img = Pic(parent, name, aMin, aMax, pivot, pos, size, S(sprite), UiRole.None, sliced);
            img.preserveAspect = false;
            if (sliced) img.pixelsPerUnitMultiplier = mul;
            var light = S(sprite + "_light");
            if (light != null)
            {
                var ts = img.gameObject.AddComponent<ThemeSprite>();
                ts.Dark = img.sprite; ts.Light = light;
            }
            return img;
        }

        static TextMeshProUGUI Ink(Transform parent, string name, string text, float px, Color color, bool bold, TextAnchor align = TextAnchor.UpperLeft)
        {
            var t = Label(parent, name, Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero, text, Mathf.RoundToInt(px * U), UiRole.None, align);
            t.color = color;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.raycastTarget = false;
            t.richText = true;
            return t;
        }

        static void BuildTutorialLayer(UIRoot ui, Transform safe)
        {
            var layer = Fresh(safe, "Tutorial", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero);
            layer.SetSiblingIndex(ui.Hud.transform.GetSiblingIndex() + 1);   // over the HUD, under the dim and the cards
            var tut = layer.gameObject.AddComponent<TutorialOverlay>();

            // dim: four plain quads round a 9-slice frame with the lit hole (mockup: #000 at 73 %, 2 px cyan edge, 14 px corners)
            tut.DimSides = new RectTransform[4];
            string[] sides = { "DimTop", "DimBottom", "DimLeft", "DimRight" };
            for (int i = 0; i < 4; i++)
            {
                var d = Pic(layer, sides[i], Half, Half, Half, Vector2.zero, new Vector2(10, 10), null, UiRole.None);
                d.color = new Color(0f, 0f, 0f, 0.733f);
                tut.DimSides[i] = Hidden(d).rectTransform;
            }
            tut.Hole = Hidden(Exact(layer, "Hole", Half, Half, Half, Vector2.zero, new Vector2(500, 500), "ftue_hole", true)).rectTransform;

            // marks: ring 62 px (+24 px glow), hand 44 x 52 px (+6) with its fingertip at the pivot, swipe arrow 170 x 40 px (+5)
            tut.Rings = new RectTransform[2];
            for (int i = 0; i < 2; i++) tut.Rings[i] = Hidden(Exact(layer, "Ring" + i, Half, Half, Half, Vector2.zero, new Vector2(110, 110) * U, "ftue_ring")).rectTransform;
            var hand = Exact(layer, "Hand", Half, Half, new Vector2(16f / 56f, 1f - 14f / 64f), Vector2.zero, new Vector2(56, 64) * U, "ftue_hand");
            tut.Hand = Hidden(hand).rectTransform;
            tut.Swipe = Hidden(Exact(layer, "Swipe", Half, Half, Half, Vector2.zero, new Vector2(180, 50) * U, "ftue_swipe")).rectTransform;

            // any tap moves a waiting note on
            var catcher = Fresh(layer, "TapCatcher", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero);
            Img(catcher, null, UiRole.None).color = new Color(1, 1, 1, 0);
            tut.TapCatcher = catcher.gameObject.AddComponent<Button>();
            tut.TapCatcher.transition = Selectable.Transition.None;
            catcher.gameObject.SetActive(false);

            // the note: 220 px wide, top-right corner 4 px towards the mascot, 8 px left of it, tops level
            var hudMascot = ui.Hud.Mascot != null ? (RectTransform)ui.Hud.Mascot.transform : null;
            Vector2 mPos = hudMascot != null ? hudMascot.anchoredPosition : new Vector2(-36, -186);
            float mW = hudMascot != null ? hudMascot.sizeDelta.x : 150f;
            var note = Fresh(layer, "Note", TopR, TopR, TopR, new Vector2(mPos.x - mW - 8 * U, mPos.y), new Vector2(220 * U, 100));
            var bg = Fresh(note, "Bg", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero);
            bg.offsetMin = new Vector2(-36 * U, -44 * U); bg.offsetMax = new Vector2(36 * U, 28 * U);   // the sprite's shadow margins
            var bgImg = Img(bg, S("ftue_note"), UiRole.None, true);
            bgImg.pixelsPerUnitMultiplier = Texel;
            bgImg.raycastTarget = false;
            bg.SetAsFirstSibling();
            var col = note.gameObject.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset(Mathf.RoundToInt(13 * U), Mathf.RoundToInt(13 * U), Mathf.RoundToInt(11 * U), Mathf.RoundToInt(9 * U));
            col.childControlWidth = true; col.childControlHeight = true; col.childForceExpandWidth = true; col.childForceExpandHeight = false;
            col.spacing = 3 * U;
            var fit = note.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            bg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            tut.NoteName = Ink(note, "Name", "LUMI", 10, NoteTeal, true);
            tut.NoteName.characterSpacing = 8;
            tut.NoteText = Ink(note, "Text", "This is a <b>power source</b>.", 14, NoteInk, true);
            tut.NoteText.lineSpacing = 10;
            var nav = Fresh(note, "Nav", Vector2.zero, Vector2.one, Half, Vector2.zero, new Vector2(0, 15 * U));
            nav.gameObject.AddComponent<LayoutElement>().minHeight = 15 * U + 5 * U;
            var skip = Ink(nav, "Skip", "Skip tutorial", 11, NoteMuted, true, TextAnchor.LowerLeft);
            skip.raycastTarget = true;
            skip.gameObject.AddComponent<LocText>();
            tut.Skip = skip.gameObject.AddComponent<Button>();
            tut.Skip.targetGraphic = skip;
            skip.gameObject.AddComponent<UiClickSound>();
            tut.NextLabel = Ink(nav, "Next", "Tap to continue ›", 11, NoteNext, true, TextAnchor.LowerRight);
            tut.NextLabel.gameObject.AddComponent<LocText>();
            tut.Note = note;
            note.gameObject.SetActive(false);

            // the HUD mascot speaks the notes: it may come up over the dim (a canvas of its own, sorted on while a note shows)
            if (ui.Hud.Mascot != null)
            {
                var mc = ui.Hud.Mascot.GetComponent<Canvas>();
                if (mc == null) mc = ui.Hud.Mascot.gameObject.AddComponent<Canvas>();
                mc.overrideSorting = false;
                tut.Mascot = mc;
            }
            BuildWelcome(tut, layer);
            BuildReady(tut, layer);
            layer.gameObject.SetActive(false);
            ui.Tutorial = tut;
        }

        /// <summary>FT0: the mascot bursts out of a spark under the title and says hello; a tap goes on.</summary>
        static void BuildWelcome(TutorialOverlay tut, RectTransform layer)
        {
            var w = Fresh(layer, "Welcome", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero);
            Img(w, null, UiRole.None).color = new Color(1, 1, 1, 0);
            tut.WelcomeButton = w.gameObject.AddComponent<Button>();
            tut.WelcomeButton.transition = Selectable.Transition.None;
            // mockup (844 px tall, centre at 422): title top 120, mascot 160 px centred at 350, bubble top 462, hint top 700
            Exact(w, "Logo", TopC, TopC, TopC, new Vector2(0, -105 * U), new Vector2(300, 80) * U, "ftue_logo");
            var halo = Exact(w, "Halo", Half, Half, Half, new Vector2(0, 72 * U), new Vector2(280, 280) * U, "ftue_halo");
            tut.WelcomeHalo = halo;
            tut.Rays = new RectTransform[12];
            for (int i = 0; i < 12; i++) tut.Rays[i] = Exact(w, "Ray" + i, Half, Half, Half, Vector2.zero, new Vector2(8, 48) * U, "ftue_ray").rectTransform;
            var m = Exact(w, "Mascot", Half, Half, Half, new Vector2(0, 72 * U), new Vector2(280, 280) * U, "ftue_frame");
            tut.WelcomeMascot = m.rectTransform;
            tut.WelcomeFace = Pic(m.transform, "Face", Half, Half, Half, Vector2.zero, new Vector2(124, 112) * (104f / 124f) * U, S("face_happy"), UiRole.None);
            var b = Fresh(w, "Bubble", TopC, TopC, TopC, new Vector2(0, -40 * U), new Vector2(318 * U, 112 * U));
            ((RectTransform)b).anchorMin = ((RectTransform)b).anchorMax = Half;
            Bubble(b, out var tail);
            tut.WelcomeBubble = b;
            var inner = Fresh(b, "Inner", Vector2.zero, Vector2.one, Half, Vector2.zero, new Vector2(-36 * U, -32 * U));
            tut.WelcomeName = Ink(inner, "Name", "LUMI", 10, NoteTeal, true);
            ((RectTransform)tut.WelcomeName.transform).offsetMin = new Vector2(0, 0);
            tut.WelcomeName.characterSpacing = 8;
            tut.WelcomeText = Ink(inner, "Text", "Welcome to Circuit Cube! I'm Lumi. Let me show you how to play!", 16, NoteInk, true);
            var tr = (RectTransform)tut.WelcomeText.transform;
            tr.offsetMax = new Vector2(0, -14 * U);
            tut.WelcomeTap = Label(w, "Tap", BottomC, BottomC, new Vector2(0.5f, 0), new Vector2(0, 128 * U), new Vector2(380 * U, 20 * U), "Tap to continue", Mathf.RoundToInt(13 * U), UiRole.Accent);
            tut.WelcomeTap.fontStyle = FontStyles.Bold;
            tut.WelcomeTap.raycastTarget = false;
            tut.WelcomeTap.gameObject.AddComponent<LocText>();
            tut.Welcome = w.gameObject;
            w.gameObject.SetActive(false);
        }

        /// <summary>A light speech bubble (ftue_bubble, 20 px corners) with its tail on the top edge, pointing up.</summary>
        static void Bubble(RectTransform b, out Image tail)
        {
            var bg = Fresh(b, "Bg", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero);
            bg.offsetMin = new Vector2(-40 * U, -50 * U); bg.offsetMax = new Vector2(40 * U, 30 * U);
            var img = Img(bg, S("ftue_bubble"), UiRole.None, true);
            img.pixelsPerUnitMultiplier = Texel;
            img.raycastTarget = false;
            tail = Exact(b, "Tail", TopC, TopC, new Vector2(0.5f, 0), new Vector2(0, -2 * U), new Vector2(24, 14) * U, "ftue_tail");
        }

        /// <summary>FTEnd: six lit dashes, the mascot with star eyes, "Ready to play?", I'm ready! and Replay tutorial.</summary>
        static void BuildReady(TutorialOverlay tut, RectTransform layer)
        {
            var r = Fresh(layer, "Ready", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero);
            Img(r, null, UiRole.None).color = new Color(1, 1, 1, 0);   // keeps taps off the board behind
            var c = Fresh(r, "Content", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero);
            tut.ReadyContent = c;
            // mockup: dashes top 170 (22 x 6 px, 6 px apart), caption top 186, mascot 160 px top 250, bubble top 440,
            // button 274 x 56 px top 600, the replay line top 672; centre at 422
            for (int i = 0; i < 6; i++)
                Exact(c, "Dash" + i, Half, Half, Half, new Vector2((i - 2.5f) * 28 * U, (422 - 173) * U), new Vector2(46, 30) * U, "ftue_dash");
            tut.ReadyCaption = Label(c, "Caption", Half, Half, Half, new Vector2(0, (422 - 193) * U), new Vector2(380 * U, 16 * U), "TUTORIAL COMPLETE · 6 / 6", Mathf.RoundToInt(11 * U), UiRole.TextMuted);
            tut.ReadyCaption.fontStyle = FontStyles.Bold;
            tut.ReadyCaption.characterSpacing = 8;
            var m = Exact(c, "Mascot", Half, Half, Half, new Vector2(0, (422 - 330) * U), new Vector2(280, 280) * U, "ftue_frame");
            tut.ReadyMascot = m.rectTransform;
            tut.ReadyFace = Pic(m.transform, "Face", Half, Half, Half, Vector2.zero, new Vector2(124, 112) * (104f / 124f) * U, S("face_excited"), UiRole.None);
            var b = Fresh(c, "Bubble", Half, Half, TopC, new Vector2(0, (422 - 440) * U), new Vector2(300 * U, 86 * U));
            Bubble(b, out _);
            tut.ReadyBubble = b;
            tut.ReadyTitle = Ink(b, "Title", "Ready to play?", 25, NoteInk, true, TextAnchor.UpperCenter);
            var tt = (RectTransform)tut.ReadyTitle.transform;
            tt.offsetMin = new Vector2(0, 0); tt.offsetMax = new Vector2(0, -16 * U);
            tut.ReadySub = Ink(b, "Sub", "Moonbase is waiting for you to light it up!", 13, ReadySub, true, TextAnchor.LowerCenter);
            var st = (RectTransform)tut.ReadySub.transform;
            st.offsetMin = new Vector2(16 * U, 16 * U); st.offsetMax = new Vector2(-16 * U, 0);
            tut.ReadySub.enableAutoSizing = true;
            tut.ReadySub.fontSizeMax = tut.ReadySub.fontSize;
            tut.ReadySub.fontSizeMin = Mathf.RoundToInt(10 * U);
            var go = Fresh(c, "Go", Half, Half, TopC, new Vector2(0, (422 - 600) * U), new Vector2(274, 56) * U);
            Img(go, S("ui_button"), UiRole.Primary, true);
            var gl = Label(go, "Label", Vector2.zero, Vector2.one, Half, new Vector2(-14 * U, 0), Vector2.zero, "I'm ready!", Mathf.RoundToInt(19 * U), UiRole.OnPrimary);
            gl.fontStyle = FontStyles.Bold;
            gl.gameObject.AddComponent<LocText>();
            Pic(go, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Half, new Vector2(gl.preferredWidth * 0.5f + 8 * U, 0), new Vector2(22, 22) * U, Icon("icon_arrow"), UiRole.OnPrimary);
            tut.Go = go.gameObject.AddComponent<Button>();
            go.gameObject.AddComponent<UiClickSound>();
            var later = Fresh(c, "Replay", Half, Half, TopC, new Vector2(0, (422 - 668) * U), new Vector2(300 * U, 24 * U));
            Img(later, null, UiRole.None).color = new Color(1, 1, 1, 0);
            var ll = Label(later, "Label", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero, "Replay tutorial", Mathf.RoundToInt(13 * U), UiRole.TextMuted);
            ll.fontStyle = FontStyles.Bold;
            ll.raycastTarget = false;
            ll.gameObject.AddComponent<LocText>();
            tut.Replay = later.gameObject.AddComponent<Button>();
            later.gameObject.AddComponent<UiClickSound>();
            tut.Ready = r.gameObject;
            r.gameObject.SetActive(false);
        }

        /// <summary>FTZoom: a pill on the right edge (48 px, top 300 px) with +, the zoom level, − and fit, plus a one-time tip.</summary>
        static void BuildZoomPill(UIRoot ui)
        {
            var hud = ui.Hud;
            var pill = Fresh(hud.transform, "ZoomPanel", TopR, TopR, TopR, new Vector2(-12 * U, -300 * U), new Vector2(48 * U, 150 * U));
            Img(pill, S("ui_pill"), UiRole.Chip, true).pixelsPerUnitMultiplier = PillScale(48 * U);
            var col = pill.gameObject.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset(0, 0, Mathf.RoundToInt(6 * U), Mathf.RoundToInt(6 * U));
            col.spacing = 4 * U;
            col.childAlignment = TextAnchor.UpperCenter;
            col.childControlWidth = false; col.childControlHeight = false; col.childForceExpandWidth = false; col.childForceExpandHeight = false;
            hud.ZoomIn = ZoomButton(pill, "In", "icon_plus", false);
            hud.ZoomLevel = Label(pill, "Level", Half, Half, Half, Vector2.zero, new Vector2(46 * U, 12 * U), "1.0x", Mathf.RoundToInt(10 * U), UiRole.TextMuted);
            hud.ZoomLevel.fontStyle = FontStyles.Bold;
            hud.ZoomLevel.raycastTarget = false;
            hud.ZoomOut = ZoomButton(pill, "Out", "icon_minus", false);
            hud.ZoomFit = ZoomButton(pill, "Fit", "icon_fit", true);
            hud.ZoomPanel = pill.gameObject;
            pill.gameObject.SetActive(false);

            // tip: 150 px wide, its right edge 68 px from the screen's, top 318 px
            var tip = Fresh(hud.transform, "ZoomTip", TopR, TopR, TopR, new Vector2(-68 * U, -318 * U), new Vector2(150 * U, 60 * U));
            var bg = Fresh(tip, "Bg", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero);
            float k = 16f / 10f;   // the note sprite's 16 px corners drawn as the tip's 10 px ones
            bg.offsetMin = new Vector2(-36 * U / k, -44 * U / k); bg.offsetMax = new Vector2(36 * U / k, 28 * U / k);
            var img = Img(bg, S("ftue_note"), UiRole.None, true);
            img.pixelsPerUnitMultiplier = Texel * k;
            img.raycastTarget = false;
            var t = Ink(tip, "Text", "On an emulator: press + / − or scroll the mouse wheel to zoom", 11.5f, NoteInk, true, TextAnchor.MiddleLeft);
            var tr = (RectTransform)t.transform;
            tr.offsetMin = new Vector2(10 * U, 7 * U); tr.offsetMax = new Vector2(-10 * U, -7 * U);
            t.gameObject.AddComponent<LocText>();
            hud.ZoomTip = tip.gameObject;
            tip.gameObject.SetActive(false);
        }

        static Button ZoomButton(RectTransform pill, string name, string icon, bool filled)
        {
            var b = Fresh(pill, name, Half, Half, Half, Vector2.zero, new Vector2(38, 38) * U);
            var img = Img(b, S("ui_circle"), filled ? UiRole.Primary : UiRole.Transparent);
            Pic(b, "Icon", Half, Half, Half, Vector2.zero, new Vector2(20, 20) * U, Icon(icon), filled ? UiRole.OnPrimary : UiRole.Text);
            var button = b.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            b.gameObject.AddComponent<UiClickSound>();
            return button;
        }

        /// <summary>A fourth switch under Vibration; everything below it in the card moves down one row.</summary>
        static void BuildZoomSwitch(UIRoot ui)
        {
            var st = ui.Settings;
            var panel = st.Panel;
            var box = (RectTransform)panel.Find("Switches");
            box.sizeDelta = new Vector2(box.sizeDelta.x, 440);
            var vib = box.Find("Vibration");
            if (vib != null && vib.Find("Divider") == null)
                Pic(vib, "Divider", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(-60, 2), null, UiRole.Track);
            st.ZoomButtons = SwitchRow(box, "Zoom buttons", 3, "icon_fit");
            var label = box.Find("Zoom buttons/Label");
            if (label != null && label.GetComponent<LocText>() == null) label.gameObject.AddComponent<LocText>();
            void Y(string path, float y) { var t = panel.Find(path) as RectTransform; if (t != null) t.anchoredPosition = new Vector2(t.anchoredPosition.x, y); }
            Y("ThemeLabel", -640); Y("Theme", -692); Y("LanguageLabel", -810); Y("Language", -862); Y("Version", -978);
            if (st.Privacy != null) ((RectTransform)st.Privacy.transform).anchoredPosition = new Vector2(0, -1040);
            st.SettingsHeight = 1100f;
            st.PausedHeight = 1380f;
        }

        /// <summary>XUẤT SẮC in the result card (7 letters, slots from the mockup) and its flying letters in the effect layer.</summary>
        static void BuildVietnameseWord(UIRoot ui)
        {
            // letter centres measured from the mockup word (ArtRef/tools/render_mockup_word_vi.py), canvas units
            float[] xs = { -266.8f, -181.1f, -93.2f, -10.4f, 97.7f, 183.0f, 266.8f };
            var fx = ui.Fx;
            var panel = (RectTransform)ui.Result.transform.Find("Panel");
            var w = Fresh(panel, "ExpertWordVi", TopC, TopC, Half, new Vector2(0, -348), new Vector2(700, 160));
            var letters = new Image[7];
            for (int i = 0; i < 7; i++)
            {
                letters[i] = Exact(w, "L" + i, Half, Half, Half, new Vector2(xs[i], 0), LetterSize, "expert_vi_" + i);
                letters[i].preserveAspect = true;
            }
            var mask = Fresh(w, "ShineMask", Half, Half, Half, new Vector2(0, -5), new Vector2(650, 157));
            mask.gameObject.AddComponent<RectMask2D>();
            var shine = Exact(mask, "Shine", Half, Half, Half, new Vector2(-400, 0), new Vector2(168, 157), "fx_word_shine");
            w.gameObject.SetActive(false);
            fx.WordVi = w;
            fx.WordLettersVi = letters;
            fx.ShineVi = shine.rectTransform;

            var layer = fx.Layer;
            fx.FlyLettersVi = new Image[7];
            fx.HotLettersVi = new Image[7];
            for (int i = 0; i < 7; i++)
            {
                fx.FlyLettersVi[i] = Hidden(Exact(layer, "FlyVi" + i, Half, Half, Half, Vector2.zero, LetterSize, "expert_vi_" + i));
                fx.HotLettersVi[i] = Exact(fx.FlyLettersVi[i].transform, "Hot", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero, "expert_vi_" + i + "_hot");
            }
            // one more ping for the seventh letter
            if (fx.Pings.Length < 7)
            {
                var pings = new Image[7];
                for (int i = 0; i < fx.Pings.Length; i++) pings[i] = fx.Pings[i];
                pings[6] = Hidden(Exact(layer, "Ping6", Half, Half, Half, Vector2.zero, new Vector2(179, 202), "fx_ping"));
                fx.Pings = pings;
            }
            foreach (var g in layer.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
        }
    }
}
