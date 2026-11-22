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
        const string BoardRowPrefab = "Assets/CircuitCube/Prefabs/UI/BoardRow.prefab";

        /// <summary>Canvas units per pixel of the approved leaderboard mockup (a 390 px wide phone on the 1080 canvas).</summary>
        const float K = 2.77f;

        /// <summary>
        /// Version 1.5.0 leaderboards, laid out from the approved mockup (ArtRef/mockup/leaderboard, 1 px = 2.77 units):
        /// the Ranks pill and the Weekly Challenge card on Home, the Weekly page, the Leaderboards card (This week /
        /// All-time) with the BoardRow item prefab, the weekly result card and the name sheet. Adds the new theme roles.
        /// Run after <see cref="Ui140Score"/>. Safe to run again.
        /// </summary>
        [MenuItem("Tools/Circuit Cube/Art V2/Build 1.5.0 Ranks UI (leaderboards)")]
        public static void Ui150Ranks()
        {
            BuildRanks();
            FitLabels();
        }

        static void BuildRanks()
        {
            RankThemeRoles();
            BuildBoardRowPrefab();
            var root = PrefabUtility.LoadPrefabContents(UiPrefab);
            try
            {
                var ui = root.GetComponent<UIRoot>();
                var safe = root.transform.Find("SafeArea");
                var clip = CardClip(safe);
                HomeRanks(ui);
                ui.Weekly = WeeklyPage(ui, safe);
                ui.Boards = BoardsCard(safe, clip);
                ui.WeeklyResult = WeeklyResultCard(safe, clip);
                ui.NameSheet = NameSheetCard(safe);
                // screens under the dim, the cards above it
                ui.Weekly.transform.SetSiblingIndex(ui.Levels.transform.GetSiblingIndex() + 1);
                foreach (var c in new Component[] { ui.Boards, ui.WeeklyResult, ui.NameSheet }) c.transform.SetSiblingIndex(ui.Dim.transform.GetSiblingIndex() + 1);
                foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
                    if (t.GetComponent<LocText>() == null && IsRanksUi(t.transform, ui)) t.gameObject.AddComponent<LocText>();
                PrefabUtility.SaveAsPrefabAsset(root, UiPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[CircuitCube] 1.5.0 ranks UI built.");
        }

        static bool IsRanksUi(Transform t, UIRoot ui) =>
            t.IsChildOf(ui.Weekly.transform) || t.IsChildOf(ui.Boards.transform) || t.IsChildOf(ui.WeeklyResult.transform)
            || t.IsChildOf(ui.NameSheet.transform) || t.IsChildOf(ui.Home.RanksButton.transform) || t.IsChildOf(ui.Home.WeeklyButton.transform);

        /// <summary>Colours of the new roles, dark (from the mockup) and light.</summary>
        static void RankThemeRoles()
        {
            var roles = new[] { UiRole.Row, UiRole.Highlight, UiRole.Silver, UiRole.Bronze, UiRole.Warning, UiRole.OnWarning, UiRole.Segment, UiRole.OnStar };
            string[] dark = { "191D35", "1F3A47", "C9CEDD", "D98E5A", "2A2213", "F3D9A0", "2A2E48", "0A0C18" };
            string[] light = { "F1F3F9", "DCF1F4", "A3ABBE", "C9773F", "FFF1D6", "7A5400", "FFFFFF", "18203A" };
            foreach (var (path, hexes) in new[] { ("Assets/CircuitCube/Themes/Theme_Dark.asset", dark), ("Assets/CircuitCube/Themes/Theme_Light.asset", light) })
            {
                var theme = AssetDatabase.LoadAssetAtPath<GameTheme>(path);
                if (theme == null) continue;
                for (int i = 0; i < roles.Length; i++)
                {
                    ColorUtility.TryParseHtmlString("#" + hexes[i], out var c);
                    int at = theme.Ui.FindIndex(r => r.Role == roles[i]);
                    if (at >= 0) theme.Ui[at] = new RoleColor(roles[i], c); else theme.Ui.Add(new RoleColor(roles[i], c));
                }
                EditorUtility.SetDirty(theme);
            }
        }

        // ------------------------------------------------------------------ Home

        static void HomeRanks(UIRoot ui)
        {
            var h = ui.Home;
            var t = h.transform;
            // top bar: stars, Ranks, settings (right to left: settings at -36, Ranks next to it, then the stars)
            var pill = Fresh(t, "Ranks", TopR, TopR, TopR, new Vector2(-176, -48), new Vector2(280, 96));
            Img(pill, S("ui_pill"), UiRole.Star, true).GetComponent<ThemeRole>().Alpha = 0.28f;
            var inner = Pic(pill, "Fill", Vector2.zero, Vector2.one, Half, Vector2.zero, new Vector2(-8, -8), S("ui_pill"), UiRole.Chip, true);
            inner.raycastTarget = false;
            // icon and text centred together (IconLabel, added by FitLabels)
            Pic(pill, "Icon", C, C, Half, new Vector2(-70, 0), new Vector2(58, 58), Icon("icon_trophy"), UiRole.Star);
            var label = Label(pill, "Label", Vector2.zero, Vector2.one, Half, new Vector2(37, 0), new Vector2(-16, 0), "Ranks", 42, UiRole.Text);
            Heavy(label);
            h.RanksButton = pill.gameObject.AddComponent<Button>();
            pill.gameObject.AddComponent<UiClickSound>();
            Place(t, "StarChip", TopR, TopR, TopR, new Vector2(-476, -48), new Vector2(200, 96));
            // the logo moves under the top bar, which now reaches past the middle of the screen
            Place(t, "Logo", TopC, TopC, TopC, new Vector2(0, -170), new Vector2(900, 330));

            // weekly card above the shape card: 920 wide like it, 20 px corners, teal border
            var card = Fresh(t, "WeeklyCard", BottomC, BottomC, new Vector2(0.5f, 0), new Vector2(0, 646), new Vector2(920, 276));
            Round(card, S("ui_card"), UiRole.Primary, 55, 276);
            Round(Fresh(card, "Fill", Vector2.zero, Vector2.one, Half, Vector2.zero, new Vector2(-8, -8)), S("ui_card"), UiRole.Card, 52, 268).raycastTarget = false;
            var box = Fresh(card, "IconBox", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(44, 0), new Vector2(144, 144));
            Round(box, S("ui_card"), UiRole.Highlight, 44, 144).raycastTarget = false;
            Pic(box, "Icon", Half, Half, Half, Vector2.zero, new Vector2(80, 80), Icon("icon_trophy"), UiRole.Primary);
            float x = 44 + 144 + 39;
            var head = Fresh(card, "Head", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(x, -60), new Vector2(-x - 110, 60));
            var row = head.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 22; row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = true; row.childControlHeight = false; row.childForceExpandWidth = false; row.childForceExpandHeight = false;
            var title = Label(head, "Title", Half, Half, Half, Vector2.zero, new Vector2(500, 60), "Weekly Challenge", 47, UiRole.Text, TextAnchor.MiddleLeft);
            Heavy(title);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            h.WeeklyNew = Badge(head, "New", "NEW", 30, new Vector2(100, 46)).gameObject;
            h.WeeklyLine = Label(card, "Line", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(x, -132), new Vector2(-x - 110, 46),
                "Moonbase 2 / 3 solved · ends in 2d 14h", 36, UiRole.TextMuted, TextAnchor.MiddleLeft);
            var steps = Fresh(card, "Steps", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(x, -196), new Vector2(-x - 110, 17));
            var sr = steps.gameObject.AddComponent<HorizontalLayoutGroup>();
            sr.spacing = 11; sr.childControlWidth = sr.childControlHeight = true; sr.childForceExpandWidth = sr.childForceExpandHeight = true;
            h.WeeklySteps = new ThemeRole[3];
            for (int i = 0; i < 3; i++)
            {
                var bar = Pic(steps, "Step" + i, Half, Half, Half, Vector2.zero, new Vector2(100, 17), S("ui_pill"), i < 2 ? UiRole.Primary : UiRole.Track, true);
                bar.pixelsPerUnitMultiplier = PillScale(17);
                h.WeeklySteps[i] = bar.GetComponent<ThemeRole>();
            }
            Chevron(card, -44);
            h.WeeklyButton = card.gameObject.AddComponent<Button>();
            card.gameObject.AddComponent<UiClickSound>();
            card.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ Weekly page

        static WeeklyScreen WeeklyPage(UIRoot ui, Transform safe)
        {
            var s = Fresh(safe, "Weekly", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero);
            var screen = s.gameObject.AddComponent<WeeklyScreen>();
            s.gameObject.AddComponent<CanvasGroup>();
            var shade = Pic(s, "Shade", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero, S("ui_vignette"), UiRole.Dim);
            shade.preserveAspect = false;
            shade.raycastTarget = true;   // the board behind does not take taps
            const float pad = 55f;
            screen.BackButton = RoundButton(s, "Back", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(pad, -pad), new Vector2(122, 122), "icon_chevron_left");
            s.Find("Back").gameObject.AddComponent<UiClickSound>();
            ((RectTransform)s.Find("Back/Icon")).sizeDelta = new Vector2(-30, -30);   // the chevron sprite has room around it
            float tx = pad + 122 + 33;
            var title = Label(s, "Title", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(tx, -48), new Vector2(-tx - pad, 80), "Weekly Challenge", 66, UiRole.Text, TextAnchor.MiddleLeft);
            Heavy(title);
            screen.Subtitle = Label(s, "Subtitle", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(tx, -132), new Vector2(-tx - pad, 92),
                "Week of Dec 3 · new set every Thursday 07:00", 36, UiRole.TextMuted, TextAnchor.UpperLeft);

            // three stat tiles
            var tiles = Fresh(s, "Tiles", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -277), new Vector2(-2 * pad, 175));
            var tl = tiles.gameObject.AddComponent<HorizontalLayoutGroup>();
            tl.spacing = 22; tl.childControlWidth = tl.childControlHeight = true; tl.childForceExpandWidth = tl.childForceExpandHeight = true;
            screen.EndsIn = StatTile(tiles, "EndsIn", "Ends in", "2d 14h");
            screen.Solved = StatTile(tiles, "Solved", "Solved", "2 / 3");
            screen.TotalRank = StatTile(tiles, "TotalRank", "Total rank", "–");
            screen.TotalRankRole = screen.TotalRank.GetComponent<ThemeRole>();
            screen.TotalRankRole.Role = UiRole.TextMuted;

            // rules
            var rules = Fresh(s, "Rules", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -496), new Vector2(-2 * pad, 202));
            Round(rules, S("ui_card"), UiRole.Row, 39, 202).raycastTarget = false;
            Pic(rules, "Icon", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(36, 0), new Vector2(66, 66), Icon("icon_info"), UiRole.Primary);
            Label(rules, "Text", Vector2.zero, Vector2.one, new Vector2(0, 0.5f), new Vector2(124, 0), new Vector2(-124 - 39, -40),
                "No hints, no undo. Fewest moves wins, then fastest time. Replay as often as you like; your best run counts.", 36, UiRole.Text, TextAnchor.MiddleLeft)
                .GetComponent<ThemeRole>().Alpha = 0.85f;

            // the three levels
            screen.Rows = new WeeklyRowView[3];
            string[] diff = { "Easy", "Medium", "Hard" };
            for (int i = 0; i < 3; i++)
                screen.Rows[i] = WeeklyRow(s, i, -742 - i * (185 + 28), diff[i], pad);

            // dock, the Leaderboard button and the footnote over it
            screen.Dock = Dock(s, "Dock", 0f);
            var board = TextButton(s, "Leaderboard", BottomC, new Vector2(0, 296), new Vector2(1080 - 2 * pad, 144), false, "icon_trophy", "Leaderboard", 50);
            Round((RectTransform)board.transform, S("ui_card"), UiRole.Chip, 55, 144);
            board.transform.Find("Icon").GetComponent<ThemeRole>().Role = UiRole.Star;
            var bl = board.transform.Find("Label").GetComponent<TMP_Text>();
            bl.fontStyle = FontStyles.Bold;
            bl.GetComponent<ThemeRole>().Role = UiRole.Text;
            board.gameObject.AddComponent<UiClickSound>();
            screen.BoardButton = board;
            screen.Footnote = Label(s, "Footnote", BottomC, BottomC, new Vector2(0.5f, 0), new Vector2(0, 470), new Vector2(1080 - 2 * pad, 100),
                "Solve all 3 to get on the Moonbase total board. Locked shapes join the challenge once you unlock them.", 36, UiRole.TextMuted);
            s.gameObject.SetActive(false);
            return screen;
        }

        static TMP_Text StatTile(Transform parent, string name, string label, string value)
        {
            var tile = Fresh(parent, name, Half, Half, Half, Vector2.zero, new Vector2(300, 175));
            Round(tile, S("ui_card"), UiRole.Card, 44, 175).raycastTarget = false;
            Label(tile, "Label", new Vector2(0, 1), new Vector2(1, 1), TopC, new Vector2(0, -30), new Vector2(-20, 44), label, 33, UiRole.TextMuted);
            var v = Label(tile, "Value", new Vector2(0, 1), new Vector2(1, 1), TopC, new Vector2(0, -78), new Vector2(-20, 64), value, 50, UiRole.Text);
            Heavy(v);
            return v;
        }

        static WeeklyRowView WeeklyRow(Transform s, int i, float top, string diff, float pad)
        {
            const float h = 185f;
            var r = Fresh(s, "Level" + (i + 1), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, top), new Vector2(-2 * pad, h));
            var v = r.gameObject.AddComponent<WeeklyRowView>();
            var frame = Fresh(r, "Frame", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero);
            Round(frame, S("ui_card"), UiRole.Primary, 50, h).raycastTarget = false;
            v.Frame = frame.gameObject;
            var fill = Fresh(r, "Fill", Vector2.zero, Vector2.one, Half, Vector2.zero, new Vector2(-8, -8));
            Round(fill, S("ui_card"), UiRole.Card, 46, h - 8);
            v.Button = r.gameObject.AddComponent<Button>();
            v.Button.targetGraphic = fill.GetComponent<Image>();
            r.gameObject.AddComponent<UiClickSound>();
            var badge = Fresh(r, "Badge", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(43, 0), new Vector2(111, 111));
            Round(badge, S("ui_card"), UiRole.Highlight, 33, 111).raycastTarget = false;
            v.Badge = badge.GetComponent<ThemeRole>();
            v.Number = Label(badge, "Number", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero, (i + 1).ToString(), 47, UiRole.Primary);
            Heavy(v.Number);
            v.NumberRole = v.Number.GetComponent<ThemeRole>();
            const float x = 43 + 111 + 33;
            var head = Fresh(r, "Head", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(x, -40), new Vector2(-x - 280, 53));
            var hl = head.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 22; hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = true; hl.childControlHeight = false; hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            v.Shape = Label(head, "Shape", Half, Half, Half, Vector2.zero, new Vector2(300, 53), "Moonbase", 44, UiRole.Text, TextAnchor.MiddleLeft);
            v.Shape.fontStyle = FontStyles.Bold;
            v.Shape.textWrappingMode = TextWrappingModes.NoWrap;
            v.Level = Label(head, "Level", Half, Half, Half, Vector2.zero, new Vector2(160, 53), diff, 33, UiRole.TextMuted, TextAnchor.MiddleLeft);
            v.Level.textWrappingMode = TextWrappingModes.NoWrap;
            v.Detail = Label(r, "Detail", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(x, -101), new Vector2(-x - 280, 44), "14 moves · 0:21.4", 36, UiRole.TextMuted, TextAnchor.MiddleLeft);
            v.DetailRole = v.Detail.GetComponent<ThemeRole>();
            var rank = Fresh(r, "Rank", new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-43, 0), new Vector2(260, 0));
            v.RankRoot = rank.gameObject;
            v.Rank = Label(rank, "Rank", new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -45), new Vector2(0, 53), "#8", 44, UiRole.Star, TextAnchor.MiddleRight);
            Heavy(v.Rank);
            v.Of = Label(rank, "Of", new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -104), new Vector2(0, 36), "of 1,412", 30, UiRole.TextMuted, TextAnchor.MiddleRight);
            var play = Fresh(r, "Play", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-43, 0), new Vector2(222, 111));
            Round(play, S("ui_card"), UiRole.Primary, 39, 111);
            var pl = Label(play, "Label", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero, "Play", 42, UiRole.OnPrimary);
            Heavy(pl);
            v.Play = play.gameObject.AddComponent<Button>();
            play.gameObject.AddComponent<UiClickSound>();
            play.gameObject.SetActive(false);
            frame.gameObject.SetActive(false);
            return v;
        }

        /// <summary>The bottom shape dock: six columns of shape picture and name, a lock badge, over a dark strip with a top line.</summary>
        static ShapeDock Dock(Transform parent, string name, float bottom, bool roundedBottom = false)
        {
            const float h = 238f;
            var d = Fresh(parent, name, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, bottom), new Vector2(0, h));
            var dock = d.gameObject.AddComponent<ShapeDock>();
            if (roundedBottom)
            {
                // the card's bottom corners are round: a round strip, squared off at the top by a plain one
                Round(Fresh(d, "Back", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero), S("ui_card"), UiRole.Background, 72, h).raycastTarget = false;
                Pic(d, "BackTop", new Vector2(0, 1), Vector2.one, TopC, Vector2.zero, new Vector2(0, h * 0.5f), null, UiRole.Background).color = Color.white;
            }
            else Img(Fresh(d, "Back", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero), null, UiRole.Background).raycastTarget = true;
            Pic(d, "Line", new Vector2(0, 1), Vector2.one, TopC, Vector2.zero, new Vector2(0, 3), null, UiRole.Track);
            var grid = Fresh(d, "Items", Vector2.zero, Vector2.one, Half, new Vector2(0, 5), new Vector2(-30, -66));
            var hl = grid.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 6; hl.childControlWidth = hl.childControlHeight = true; hl.childForceExpandWidth = hl.childForceExpandHeight = true;
            dock.Items = new ShapeDockItem[6];
            dock.IconIds = new[] { "cube", "tetra", "octa", "sphere", "dodeca", "megamix" };
            dock.Icons = new Sprite[dock.IconIds.Length];
            for (int i = 0; i < dock.IconIds.Length; i++) dock.Icons[i] = Icon("icon_dock_" + dock.IconIds[i]);
            string[] ids = { "cube", "tetra", "octa", "sphere", "dodeca", "megamix" };
            string[] names = { "Moonbase", "Pyramid", "Star Gem", "Planet", "Galaxy", "Nebula" };
            for (int i = 0; i < 6; i++)
            {
                var it = Fresh(grid, "Shape" + i, Half, Half, Half, Vector2.zero, new Vector2(160, 172));
                var item = it.gameObject.AddComponent<ShapeDockItem>();
                dock.Items[i] = item;
                item.Group = it.gameObject.AddComponent<CanvasGroup>();
                var back = Round(it, S("ui_card"), i == 0 ? UiRole.Primary : UiRole.Transparent, 39, 160);
                item.Back = back.GetComponent<ThemeRole>();
                item.Icon = Pic(it, "Icon", TopC, TopC, Half, new Vector2(0, -66), new Vector2(80, 80), Icon("icon_dock_" + ids[i]), i == 0 ? UiRole.OnPrimary : UiRole.TextMuted);
                item.IconRole = item.Icon.GetComponent<ThemeRole>();
                item.Name = Label(it, "Name", new Vector2(0, 1), new Vector2(1, 1), TopC, new Vector2(0, -122), new Vector2(-6, 36), names[i], 29, i == 0 ? UiRole.OnPrimary : UiRole.TextMuted);
                Heavy(item.Name);
                item.Name.textWrappingMode = TextWrappingModes.NoWrap;
                item.Name.enableAutoSizing = false;   // FitLabels leaves it: 25 ExtraBold fits "Moonbase" in the column
                item.Name.fontSize = 25;
                item.Name.overflowMode = TextOverflowModes.Overflow;
                item.NameRole = item.Name.GetComponent<ThemeRole>();
                var lk = Pic(it, "Lock", TopR, TopR, TopR, new Vector2(-14, -11), new Vector2(46, 46), S("ui_circle"), UiRole.Track);
                Pic(lk.transform, "Icon", Vector2.zero, Vector2.one, Half, Vector2.zero, new Vector2(-18, -18), Icon("icon_lock"), UiRole.TextMuted);
                item.Lock = lk.gameObject;
                item.Lock.SetActive(i > 0);
                item.Button = it.gameObject.AddComponent<Button>();
                item.Button.targetGraphic = back;
                var colors = item.Button.colors;
                colors.disabledColor = Color.white;   // the CanvasGroup fades a locked shape
                item.Button.colors = colors;
                it.gameObject.AddComponent<UiClickSound>();
            }
            return dock;
        }

        // ------------------------------------------------------------------ Leaderboards card

        static LeaderboardCard BoardsCard(Transform safe, AnimationClip clip)
        {
            var c = Fresh(safe, "BoardsCard", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero);
            var card = c.gameObject.AddComponent<LeaderboardCard>();
            card.Anim = Anim(c.gameObject, clip);
            // the card fills the screen under a 44 px top margin, 12 px at the sides and the bottom
            var p = Fresh(c, "Panel", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero);
            p.offsetMin = new Vector2(33, 33); p.offsetMax = new Vector2(-33, -122);
            Round(p, S("ui_card"), UiRole.Track, 78, 1000);
            Round(Fresh(p, "Fill", Vector2.zero, Vector2.one, Half, Vector2.zero, new Vector2(-6, -6)), S("ui_card"), UiRole.Card, 76, 1000);
            const float pad = 44f;

            // title row
            Pic(p, "Trophy", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(pad, -111), new Vector2(66, 66), Icon("icon_trophy"), UiRole.Star);
            var title = Label(p, "Title", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(pad + 66 + 28, -111), new Vector2(-(pad + 66 + 28) - 330, 80), "Leaderboards", 61, UiRole.Text, TextAnchor.MiddleLeft);
            Heavy(title);
            card.Close = RoundButton(p, "Close", TopR, TopR, new Vector2(1, 0.5f), new Vector2(-pad, -111), new Vector2(122, 122), "icon_close");
            p.Find("Close").GetComponent<ThemeRole>().Role = UiRole.Row;
            card.Edit = RoundButton(p, "Edit", TopR, TopR, new Vector2(1, 0.5f), new Vector2(-pad - 122 - 22, -111), new Vector2(122, 122), "icon_edit");
            p.Find("Edit").GetComponent<ThemeRole>().Role = UiRole.Row;
            p.Find("Close").gameObject.AddComponent<UiClickSound>();
            p.Find("Edit").gameObject.AddComponent<UiClickSound>();

            // This week | All-time
            var seg = Fresh(p, "Switch", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -205), new Vector2(-2 * pad, 133));
            Round(seg, S("ui_card"), UiRole.Background, 44, 133).raycastTarget = false;
            card.WeekTab = SegHalf(seg, "ThisWeek", 0, "This week", true, out card.WeekTabBack, out card.WeekTabLabel);
            card.AllTab = SegHalf(seg, "AllTime", 1, "All-time", false, out card.AllTabBack, out card.AllTabLabel);

            const float dockH = 238f, youH = 144f;
            float listBottomWeek = dockH + 33 + youH + 17, listBottomAll = dockH + 33 + 84 + 17;

            // ---- This week
            var week = Fresh(p, "Week", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero);
            card.WeekRoot = week.gameObject;
            var tabs = Fresh(week, "Levels", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(pad, -371), new Vector2(-2 * pad - 222, 94));
            var tl = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            tl.spacing = 11; tl.childControlWidth = tl.childControlHeight = true; tl.childForceExpandWidth = tl.childForceExpandHeight = true;
            string[] names = { "Total", "L1", "L2", "L3" };
            card.LevelTabs = new Button[4];
            card.LevelTabBacks = new ThemeRole[4];
            card.LevelTabLabels = new ThemeRole[4];
            card.LevelTabFrames = new GameObject[4];
            for (int i = 0; i < 4; i++)
            {
                bool on = i == 2;
                var tb = Fresh(tabs, names[i], Half, Half, Half, Vector2.zero, new Vector2(170, 94));
                var edge = Round(tb, S("ui_card"), UiRole.Track, 28, 94);
                var fr = Fresh(tb, "Frame", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero);
                Round(fr, S("ui_card"), UiRole.Primary, 28, 94).raycastTarget = false;
                fr.gameObject.SetActive(on);
                card.LevelTabFrames[i] = fr.gameObject;
                var back = Fresh(tb, "Back", Vector2.zero, Vector2.one, Half, Vector2.zero, new Vector2(-8, -8));
                Round(back, S("ui_card"), on ? UiRole.Highlight : UiRole.Card, 25, 86).raycastTarget = false;
                card.LevelTabBacks[i] = back.GetComponent<ThemeRole>();
                var lb = Label(tb, "Label", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero, names[i], 36, on ? UiRole.Text : UiRole.TextMuted);
                Heavy(lb);
                card.LevelTabLabels[i] = lb.GetComponent<ThemeRole>();
                card.LevelTabs[i] = tb.gameObject.AddComponent<Button>();
                card.LevelTabs[i].targetGraphic = edge;
                tb.gameObject.AddComponent<UiClickSound>();
            }
            var cd = Fresh(week, "Countdown", TopR, TopR, TopR, new Vector2(-pad, -371), new Vector2(200, 94));
            Pic(cd, "Icon", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(38, 38), Icon("icon_timer"), UiRole.TextMuted);
            card.Countdown = Label(cd, "Text", Vector2.zero, Vector2.one, new Vector2(1, 0.5f), Vector2.zero, new Vector2(-54, 0), "2d 14h", 33, UiRole.TextMuted, TextAnchor.MiddleRight);
            card.Countdown.fontStyle = FontStyles.Bold;
            Columns(week, -498, false, pad);
            var ws = RowList(week, "List", -545, listBottomWeek, pad);
            card.WeekScroll = ws; card.WeekList = ws.content;
            var you = Fresh(week, "You", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, dockH + 33), new Vector2(-2 * pad, youH));
            card.You = BoardRow(you, youH, true);

            // ---- All-time
            var all = Fresh(p, "All", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero);
            card.AllRoot = all.gameObject;
            var sum = Fresh(all, "Summary", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -371), new Vector2(-2 * pad, 222));
            Round(sum, S("ui_card"), UiRole.Row, 44, 222).raycastTarget = false;
            card.SummaryTitle = Label(sum, "Title", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(39, -30), new Vector2(-39 - 230, 36), "YOUR MOONBASE", 30, UiRole.TextMuted, TextAnchor.MiddleLeft);
            card.SummaryTitle.fontStyle = FontStyles.Bold;
            card.SummaryTitle.characterSpacing = 4;
            card.SummaryLine = Label(sum, "Line", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(39, -72), new Vector2(-39 - 230, 120),
                "96 Expert <size=72%>of 100 · 2,332 moves · 45:38</size>", 50, UiRole.Text, TextAnchor.UpperLeft);
            Heavy(card.SummaryLine);
            card.SummaryRank = Label(sum, "Rank", TopR, TopR, TopR, new Vector2(-39, -40), new Vector2(200, 66), "#5", 55, UiRole.Primary, TextAnchor.MiddleRight);
            Heavy(card.SummaryRank);
            card.SummaryOf = Label(sum, "Of", TopR, TopR, TopR, new Vector2(-39, -106), new Vector2(200, 40), "of 3,207", 30, UiRole.TextMuted, TextAnchor.MiddleRight);
            Columns(all, -626, true, pad);
            var asr = RowList(all, "List", -673, listBottomAll, pad);
            card.AllScroll = asr; card.AllList = asr.content;
            card.Footnote = Label(all, "Footnote", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, dockH + 33), new Vector2(-2 * pad, 84),
                "Most Expert levels, then fewest moves, then time. Never resets.", 30, UiRole.TextMuted);
            all.gameObject.SetActive(false);

            card.Message = Label(p, "Message", new Vector2(0, 0.5f), new Vector2(1, 0.5f), Half, new Vector2(0, -60), new Vector2(-4 * pad, 200), "Loading...", 40, UiRole.TextMuted);
            card.Message.gameObject.SetActive(false);
            card.RowPrefab = AssetDatabase.LoadAssetAtPath<BoardRowView>(BoardRowPrefab);
            card.Dock = Dock(p, "Dock", 3f, true);
            ((RectTransform)card.Dock.transform).offsetMin = new Vector2(6, 6);
            ((RectTransform)card.Dock.transform).offsetMax = new Vector2(-6, 6 + dockH);   // inside the card's 3 px border
            c.gameObject.SetActive(false);
            return card;
        }

        static Button SegHalf(RectTransform seg, string name, int i, string text, bool on, out ThemeRole back, out ThemeRole label)
        {
            var b = Fresh(seg, name, new Vector2(i * 0.5f, 0), new Vector2((i + 1) * 0.5f, 1), Half, new Vector2(i == 0 ? 3 : -3, 0), new Vector2(-17, -22));
            var img = Round(b, S("ui_card"), on ? UiRole.Segment : UiRole.Transparent, 33, 111);
            back = img.GetComponent<ThemeRole>();
            var l = Label(b, "Label", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero, text, 42, on ? UiRole.Text : UiRole.TextMuted);
            Heavy(l);
            label = l.GetComponent<ThemeRole>();
            var button = b.gameObject.AddComponent<Button>();
            b.gameObject.AddComponent<UiClickSound>();
            return button;
        }

        /// <summary>Column heads over the rows. Positions match <see cref="BoardRow"/>.</summary>
        static void Columns(Transform parent, float top, bool allTime, float pad)
        {
            var h = Fresh(parent, "Columns", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, top), new Vector2(-2 * pad, 32));
            HeadText(h, "#", 28, 0, 89, false);
            HeadText(h, "PLAYER", allTime ? 139 : 145, 0, 400, false);
            if (allTime)
            {
                HeadText(h, "EXPERT", 0, -350, 139, true);
                HeadText(h, "MOVES", 0, -189, 139, true);
                HeadText(h, "TIME", 0, -28, 139, true);
            }
            else
            {
                HeadText(h, "MOVES", 0, -222, 144, true);
                HeadText(h, "TIME", 0, -28, 166, true);
            }
        }

        static void HeadText(Transform h, string text, float left, float right, float width, bool fromRight)
        {
            var t = fromRight
                ? Label(h, text, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(right, 0), new Vector2(width, 0), text, 30, UiRole.TextMuted, TextAnchor.MiddleRight)
                : Label(h, text, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(left, 0), new Vector2(width, 0), text, 30, UiRole.TextMuted, TextAnchor.MiddleLeft);
            t.fontStyle = FontStyles.Bold;
            t.characterSpacing = 4;
            t.textWrappingMode = TextWrappingModes.NoWrap;
        }

        /// <summary>A vertical scroll list of rows (6 px apart) between <paramref name="top"/> and <paramref name="bottom"/> of the card.</summary>
        static ScrollRect RowList(Transform parent, string name, float top, float bottom, float pad)
        {
            var v = Fresh(parent, name, Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero);
            v.offsetMin = new Vector2(pad, bottom); v.offsetMax = new Vector2(-pad, top);
            v.gameObject.AddComponent<RectMask2D>();
            var hit = v.gameObject.AddComponent<Image>();
            hit.color = new Color(1, 1, 1, 0);   // drags anywhere in the list scroll it
            var content = Fresh(v, "Rows", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 0));
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 17; vl.childControlWidth = true; vl.childControlHeight = true; vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sr = v.gameObject.AddComponent<ScrollRect>();
            sr.content = content; sr.viewport = v;
            sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 30f;
            return sr;
        }

        static void BuildBoardRowPrefab()
        {
            var go = new GameObject("BoardRow", typeof(RectTransform));
            try
            {
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = new Vector2(970, 122);
                var le = go.AddComponent<LayoutElement>();
                le.preferredHeight = le.minHeight = 122;
                BoardRow(rt, 122, false);
                foreach (var t in go.GetComponentsInChildren<TMP_Text>(true)) t.raycastTarget = false;
                PrefabUtility.SaveAsPrefabAsset(go, BoardRowPrefab);
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>
        /// A leaderboard row on <paramref name="rt"/>: 10 px padding, a 32 px rank column with a 26 px medal disc, the
        /// name, and the score columns of each tab (mockup sizes).
        /// </summary>
        static BoardRowView BoardRow(RectTransform rt, float h, bool pinned)
        {
            var v = rt.gameObject.AddComponent<BoardRowView>();
            float r = pinned ? 39 : 33;
            var frame = Fresh(rt, "Frame", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero);
            Round(frame, S("ui_card"), UiRole.Primary, r, h).raycastTarget = false;
            frame.gameObject.SetActive(pinned);
            v.Frame = frame.gameObject;
            var back = Fresh(rt, "Back", Vector2.zero, Vector2.one, Half, Vector2.zero, new Vector2(-8, -8));
            Round(back, S("ui_card"), pinned ? UiRole.Highlight : UiRole.Row, r - 4, h - 8).raycastTarget = false;
            v.Back = back.GetComponent<ThemeRole>();
            v.Medal = Pic(rt, "Medal", new Vector2(0, 0.5f), new Vector2(0, 0.5f), Half, new Vector2(64, 0), new Vector2(72, 72), S("ui_circle"), UiRole.Star).GetComponent<ThemeRole>();
            v.Medal.gameObject.SetActive(!pinned);
            v.Rank = Label(rt, "Rank", new Vector2(0, 0.5f), new Vector2(0, 0.5f), Half, new Vector2(64, 0), new Vector2(110, 60), pinned ? "#19" : "1", pinned ? 39 : 36, pinned ? UiRole.Primary : UiRole.OnStar);
            Heavy(v.Rank);
            v.Rank.textWrappingMode = TextWrappingModes.NoWrap;
            v.RankRole = v.Rank.GetComponent<ThemeRole>();

            var week = Fresh(rt, "Week", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero);
            v.WeekColumns = week.gameObject;
            v.WeekName = RowName(week, 145, 394, pinned);
            v.WeekMoves = RowValue(week, "Moves", -222, 144, 44, true, 1f);
            v.WeekTime = RowValue(week, "Time", -28, 166, 36, false, 0.8f);
            var all = Fresh(rt, "All", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero);
            v.AllColumns = all.gameObject;
            v.AllName = RowName(all, 139, 511, pinned);
            v.AllExpert = RowValue(all, "Expert", -350, 139, 44, true, 1f);
            v.AllMoves = RowValue(all, "Moves", -189, 139, 36, true, 1f);
            v.AllTime = RowValue(all, "Time", -28, 139, 33, false, 0.8f);
            all.gameObject.SetActive(false);
            return v;
        }

        static TMP_Text RowName(Transform parent, float left, float right, bool pinned)
        {
            var t = Label(parent, "Name", Vector2.zero, Vector2.one, new Vector2(0, 0.5f), new Vector2(left, 0), new Vector2(-left - right, 0),
                pinned ? "You · Tuan_Pro#4821" : "LinhNeon#0193", 42, UiRole.Text, TextAnchor.MiddleLeft);
            t.fontStyle = FontStyles.Bold;
            // the pinned row is taller: "You · name#tag" may take two lines there, as in the mockup
            t.textWrappingMode = pinned ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            if (pinned) t.lineSpacing = -12;
            t.rectTransform.offsetMin = new Vector2(left, 0); t.rectTransform.offsetMax = new Vector2(-right, 0);
            return t;
        }

        static TMP_Text RowValue(Transform parent, string name, float right, float width, int size, bool bold, float alpha)
        {
            var t = Label(parent, name, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(right, 0), new Vector2(width, 0), "21", size, UiRole.Text, TextAnchor.MiddleRight);
            if (bold) Heavy(t);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.GetComponent<ThemeRole>().Alpha = alpha;
            return t;
        }

        // ------------------------------------------------------------------ weekly result card

        static WeeklyResultCard WeeklyResultCard(Transform safe, AnimationClip clip)
        {
            var c = Fresh(safe, "WeeklyResultCard", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero);
            var card = c.gameObject.AddComponent<WeeklyResultCard>();
            card.Anim = Anim(c.gameObject, clip);
            const float w = 970f, inner = 860f;
            var p = Fresh(c, "Panel", C, C, Half, new Vector2(0, 228), new Vector2(w, 1349));
            Round(p, S("ui_card"), UiRole.Card, 78, 1349);
            card.Caption = Label(p, "Caption", TopC, TopC, TopC, new Vector2(0, -78), new Vector2(inner, 47), "WEEKLY · MOONBASE · LEVEL 2", 39, UiRole.TextMuted);
            card.Caption.fontStyle = FontStyles.Bold;
            card.Caption.characterSpacing = 4;
            card.Title = Label(p, "Title", TopC, TopC, TopC, new Vector2(0, -164), new Vector2(inner, 108), "Solved!", 89, UiRole.Text);
            Heavy(card.Title);
            var best = Badge(p, "NewBest", "NEW BEST", 36, new Vector2(300, 72));
            Set(best, TopC, TopC, TopC, new Vector2(0, -311), new Vector2(300, 72));
            card.NewBest = best.gameObject;

            card.MovesValue = ResultTile(p, "Moves", -1, "Moves", "27", "was 31", out card.MovesWas, inner);
            card.TimeValue = ResultTile(p, "Time", 1, "Time", "1:12.9", "was 1:40.2", out card.TimeWas, inner);

            // rank box (online) or the offline note in its place
            var box = Fresh(p, "RankBox", TopC, TopC, TopC, new Vector2(0, -705), new Vector2(inner, 194));
            Round(box, S("ui_card"), UiRole.Primary, 50, 194);
            Round(Fresh(box, "Fill", Vector2.zero, Vector2.one, Half, Vector2.zero, new Vector2(-8, -8)), S("ui_card"), UiRole.Highlight, 46, 186).raycastTarget = false;
            Pic(box, "Icon", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(44, 0), new Vector2(78, 78), Icon("icon_trophy"), UiRole.Star);
            card.RankLine = Label(box, "Rank", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0), new Vector2(160, -2), new Vector2(-160 - 100, 66),
                "#19 <size=70%>of 902 players</size>", 55, UiRole.Text, TextAnchor.MiddleLeft);
            Heavy(card.RankLine);
            card.RankUp = Label(box, "Up", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 1), new Vector2(160, -6), new Vector2(-160 - 100, 44), "Up 14 places", 36, UiRole.Primary, TextAnchor.MiddleLeft);
            card.RankUp.fontStyle = FontStyles.Bold;
            Chevron(box, -44);
            card.RankBox = box.gameObject.AddComponent<Button>();
            box.gameObject.AddComponent<UiClickSound>();

            var off = Fresh(p, "Offline", TopC, TopC, TopC, new Vector2(0, -705), new Vector2(inner, 194));
            Round(off, S("ui_card"), UiRole.Warning, 39, 194).raycastTarget = false;
            Pic(off, "Icon", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(39, 0), new Vector2(66, 66), Icon("icon_offline"), UiRole.OnWarning);
            card.OfflineText = Label(off, "Text", Vector2.zero, Vector2.one, new Vector2(0, 0.5f), new Vector2(133, 0), new Vector2(-133 - 39, -30),
                "Saved. Your score posts when you're back online.", 39, UiRole.OnWarning, TextAnchor.MiddleLeft);
            card.Offline = off.gameObject;
            off.gameObject.SetActive(false);

            card.Next = TextButton(p, "Next", TopC, new Vector2(0, -949 - 161), new Vector2(inner, 161), true, "icon_arrow", "Level 3", 55);
            Round((RectTransform)card.Next.transform, S("ui_card"), UiRole.Primary, 55, 161);
            card.NextLabel = card.Next.transform.Find("Label").GetComponent<TMP_Text>();
            card.NextLabel.fontStyle = FontStyles.Bold;
            float half = (inner - 28) / 2f;
            card.TryAgain = TextButton(p, "TryAgain", TopC, new Vector2(-(half + 28) / 2f, -1149 - 139), new Vector2(half, 139), false, null, "Try again", 47);
            card.AllLevels = TextButton(p, "AllLevels", TopC, new Vector2((half + 28) / 2f, -1149 - 139), new Vector2(half, 139), false, null, "All levels", 47);
            foreach (var b in new[] { card.TryAgain, card.AllLevels })
            {
                Round((RectTransform)b.transform, S("ui_card"), UiRole.Row, 50, 139);
                var l = b.transform.Find("Label").GetComponent<TMP_Text>();
                l.fontStyle = FontStyles.Bold;
                l.GetComponent<ThemeRole>().Role = UiRole.Text;
            }
            foreach (var b in new[] { card.Next, card.TryAgain, card.AllLevels }) b.gameObject.AddComponent<UiClickSound>();
            c.gameObject.SetActive(false);
            return card;
        }

        static TMP_Text ResultTile(RectTransform p, string name, int side, string label, string value, string was, out TMP_Text wasText, float inner)
        {
            float w = (inner - 22) / 2f;
            var tile = Fresh(p, name, TopC, TopC, TopC, new Vector2(side * (w + 22) / 2f, -422), new Vector2(w, 244));
            Round(tile, S("ui_card"), UiRole.Row, 44, 244).raycastTarget = false;
            Label(tile, "Label", new Vector2(0, 1), new Vector2(1, 1), TopC, new Vector2(0, -33), new Vector2(-20, 44), label, 36, UiRole.TextMuted);
            var v = Label(tile, "Value", new Vector2(0, 1), new Vector2(1, 1), TopC, new Vector2(0, -80), new Vector2(-20, 90), value, 72, UiRole.Text);
            Heavy(v);
            wasText = Label(tile, "Was", new Vector2(0, 1), new Vector2(1, 1), TopC, new Vector2(0, -172), new Vector2(-20, 38), was, 30, UiRole.Primary);
            wasText.fontStyle = FontStyles.Bold;
            return v;
        }

        // ------------------------------------------------------------------ name sheet

        static NameSheet NameSheetCard(Transform safe)
        {
            var c = Fresh(safe, "NameSheet", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero);
            var sheet = c.gameObject.AddComponent<NameSheet>();
            const float h = 1390f, pad = 66f;
            // reaches 80 below the screen so only the top corners are round
            var p = Fresh(c, "Panel", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, -80), new Vector2(0, h + 80));
            Round(p, S("ui_card"), UiRole.Card, 78, h);
            sheet.Panel = p;
            var t = Fresh(p, "Content", new Vector2(0, 1), Vector2.one, TopC, Vector2.zero, new Vector2(0, h));
            Pic(t, "Handle", TopC, TopC, TopC, new Vector2(0, -28), new Vector2(122, 14), S("ui_pill"), UiRole.Track, true).pixelsPerUnitMultiplier = PillScale(14);
            var title = Label(t, "Title", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(pad, -97), new Vector2(-2 * pad, 80), "Your name on the boards", 66, UiRole.Text, TextAnchor.MiddleLeft);
            Heavy(title);
            Label(t, "Body", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(pad, -222), new Vector2(-2 * pad, 170),
                "Shown next to your scores. Letters, numbers and _ only, up to 20. A #tag is added so names stay unique.", 39, UiRole.TextMuted, TextAnchor.UpperLeft);
            var nl = Label(t, "NameLabel", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(pad, -435), new Vector2(-2 * pad, 44), "Name", 36, UiRole.Text, TextAnchor.MiddleLeft);
            nl.fontStyle = FontStyles.Bold;
            nl.GetComponent<ThemeRole>().Alpha = 0.85f;

            var field = Fresh(t, "Field", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -501), new Vector2(-2 * pad, 150));
            Round(field, S("ui_card"), UiRole.Primary, 44, 150);
            Round(Fresh(field, "Fill", Vector2.zero, Vector2.one, Half, Vector2.zero, new Vector2(-8, -8)), S("ui_card"), UiRole.Background, 41, 142).raycastTarget = false;
            var area = Fresh(field, "Text Area", Vector2.zero, Vector2.one, Half, new Vector2(-70, 0), new Vector2(-44 - 44 - 140, -20));
            area.gameObject.AddComponent<RectMask2D>();
            var ph = Label(area, "Placeholder", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero, "Your name", 50, UiRole.TextMuted, TextAnchor.MiddleLeft);
            ph.fontStyle = FontStyles.Bold;
            ph.textWrappingMode = TextWrappingModes.NoWrap;
            var tx = Label(area, "Text", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero, "", 50, UiRole.Text, TextAnchor.MiddleLeft);
            tx.fontStyle = FontStyles.Bold;
            tx.textWrappingMode = TextWrappingModes.NoWrap;
            var input = field.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area;
            input.textComponent = tx;
            input.placeholder = ph;
            input.fontAsset = tx.font;
            input.pointSize = 50;
            input.characterLimit = NameSheet.MaxLength;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.targetGraphic = field.GetComponent<Image>();
            input.caretColor = Color.white;
            input.customCaretColor = false;
            input.caretWidth = 4;
            sheet.Input = input;
            sheet.Tag = Label(field, "Tag", new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-44, 0), new Vector2(160, 0), "#4821", 44, UiRole.TextMuted, TextAnchor.MiddleRight);
            sheet.Count = Label(t, "Count", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(pad, -673), new Vector2(-2 * pad, 42), "8 / 20", 33, UiRole.TextMuted, TextAnchor.MiddleLeft);

            var note = Fresh(t, "Note", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -759), new Vector2(-2 * pad, 166));
            Round(note, S("ui_card"), UiRole.Row, 39, 166).raycastTarget = false;
            Pic(note, "Icon", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(39, 0), new Vector2(55, 55), Icon("icon_check"), UiRole.Primary);
            sheet.Note = Label(note, "Text", Vector2.zero, Vector2.one, new Vector2(0, 0.5f), new Vector2(127, 0), new Vector2(-127 - 39, -30),
                "Signed in with Google Play Games. Your rank follows you to a new phone.", 36, UiRole.Text, TextAnchor.MiddleLeft);
            sheet.Note.GetComponent<ThemeRole>().Alpha = 0.85f;

            var save = Fresh(t, "Save", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -970), new Vector2(-2 * pad, 155));
            Round(save, S("ui_card"), UiRole.Primary, 55, 155);
            Label(save, "Label", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero, "Save", 53, UiRole.OnPrimary).fontStyle = FontStyles.Bold;
            sheet.Save = save.gameObject.AddComponent<Button>();
            save.gameObject.AddComponent<UiClickSound>();
            var later = Fresh(t, "NotNow", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -1169), new Vector2(-2 * pad, 133));
            Img(later, null, UiRole.None).color = new Color(1, 1, 1, 0);
            Label(later, "Label", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero, "Not now", 44, UiRole.TextMuted).fontStyle = FontStyles.Bold;
            sheet.NotNow = later.gameObject.AddComponent<Button>();
            later.gameObject.AddComponent<UiClickSound>();
            c.gameObject.SetActive(false);
            return sheet;
        }

        // ------------------------------------------------------------------ parts

        /// <summary>
        /// A rounded box from ui_card (27 px corners at full scale): the multiplier gives <paramref name="radius"/>
        /// canvas units, never so small that the corners overlap on a side <paramref name="minSide"/> long.
        /// </summary>
        static Image Round(RectTransform rt, Sprite sprite, UiRole role, float radius, float minSide)
        {
            var img = Img(rt, sprite, role, true);
            float m = 27f / Mathf.Max(1f, radius);
            if (sprite != null) m = Mathf.Max(m, sprite.border.x * 2f / Mathf.Max(1f, minSide));
            img.pixelsPerUnitMultiplier = m;
            return img;
        }

        /// <summary>A "&gt;" on the right of a card that opens something.</summary>
        static void Chevron(Transform parent, float right)
        {
            Pic(parent, "Chevron", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(right, 0), new Vector2(70, 70), Icon("icon_chevron_right"), UiRole.TextMuted);
        }

        const string ExtraBoldTtf = ArtPaths.Fonts + "/Inter-ExtraBold.ttf";
        const string ExtraBoldAsset = ArtPaths.Fonts + "/Inter-ExtraBold SDF.asset";

        /// <summary>Text the mockup sets in weight 800: Inter ExtraBold, no faux bold.</summary>
        static void Heavy(TMP_Text t)
        {
            var f = ExtraBold();
            if (f != null) { t.font = f; t.fontStyle = FontStyles.Normal; }
            else t.fontStyle = FontStyles.Bold;
        }

        static TMP_FontAsset _extraBold;

        /// <summary>
        /// The Inter ExtraBold SDF font asset, made once from Art/Fonts/Inter-ExtraBold.ttf with the settings of
        /// Inter-SemiBold SDF (dynamic, same atlas size, padding and render mode, same fallbacks for Vietnamese).
        /// </summary>
        static TMP_FontAsset ExtraBold()
        {
            if (_extraBold != null) return _extraBold;
            _extraBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ExtraBoldAsset);
            if (_extraBold != null) return _extraBold;
            var font = AssetDatabase.LoadAssetAtPath<Font>(ExtraBoldTtf);
            var like = UiText.SemiBold;
            if (font == null || like == null) return null;
            var fi = like.faceInfo;
            var fa = TMP_FontAsset.CreateFontAsset(font, fi.pointSize > 0 ? Mathf.RoundToInt(fi.pointSize) : 90, like.atlasPadding, like.atlasRenderMode,
                like.atlasWidth, like.atlasHeight, AtlasPopulationMode.Dynamic, like.isMultiAtlasTexturesEnabled);
            fa.name = "Inter-ExtraBold SDF";
            AssetDatabase.CreateAsset(fa, ExtraBoldAsset);
            fa.material.name = fa.name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            foreach (var tex in fa.atlasTextures)
            {
                tex.name = fa.name + " Atlas";
                AssetDatabase.AddObjectToAsset(tex, fa);
            }
            fa.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>(like.fallbackFontAssetTable ?? new System.Collections.Generic.List<TMP_FontAsset>());
            fa.TryAddCharacters(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~·–…");
            // like the SemiBold asset: glyphs added while playing are dropped from the build, the source font fills them in again
            var so = new SerializedObject(fa);
            var clear = so.FindProperty("m_ClearDynamicDataOnBuild");
            if (clear != null) { clear.boolValue = new SerializedObject(like).FindProperty("m_ClearDynamicDataOnBuild").boolValue; so.ApplyModifiedPropertiesWithoutUndo(); }
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            _extraBold = fa;
            return fa;
        }

        /// <summary>A gold pill with dark bold text (NEW, NEW BEST).</summary>
        static RectTransform Badge(Transform parent, string name, string text, int size, Vector2 box)
        {
            var b = Fresh(parent, name, Half, Half, Half, Vector2.zero, box);
            Round(b, S("ui_card"), UiRole.Star, box.y * 0.5f, box.y).raycastTarget = false;
            var le = b.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = box.x; le.preferredHeight = box.y;
            var l = Label(b, "Label", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero, text, size, UiRole.OnStar);
            Heavy(l);
            l.characterSpacing = 4;
            return b;
        }
    }
}
