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
        /// <summary>
        /// Version 1.5.0 player names: the name sheet gets a line for a refused name and the Terms line; Settings gets
        /// the name row (name, renames left, Rename) and the Terms and "Delete my data" links; a new Report sheet opens
        /// from a long press on a leaderboard row (BoardRow prefab gets the LongPress). Run after
        /// <see cref="Ui150Ranks"/>. Safe to run again.
        /// </summary>
        [MenuItem("Tools/Circuit Cube/Art V2/Build 1.5.0 Names UI (name, report, terms)")]
        public static void Ui150Names()
        {
            var root = PrefabUtility.LoadPrefabContents(UiPrefab);
            try
            {
                var ui = root.GetComponent<UIRoot>();
                var safe = root.transform.Find("SafeArea");
                NameSheetExtras(ui.NameSheet);
                SettingsNames(ui.Settings);
                ui.Report = ReportSheetCard(safe);
                ui.Report.transform.SetSiblingIndex(ui.NameSheet.transform.GetSiblingIndex() + 1);
                foreach (var t in new Component[] { ui.NameSheet, ui.Settings, ui.Report })
                    foreach (var text in t.GetComponentsInChildren<TMP_Text>(true))
                        if (text.GetComponent<LocText>() == null) text.gameObject.AddComponent<LocText>();
                PrefabUtility.SaveAsPrefabAsset(root, UiPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            BoardRowLongPress();
            AssetDatabase.SaveAssets();
            FitLabels();
            Debug.Log("[CircuitCube] 1.5.0 names UI built.");
        }

        static void NameSheetExtras(NameSheet sheet)
        {
            const float h = 1420f, pad = 66f;
            var p = sheet.Panel;
            p.sizeDelta = new Vector2(p.sizeDelta.x, h + 80);
            var t = (RectTransform)p.Find("Content");
            t.sizeDelta = new Vector2(t.sizeDelta.x, h);
            sheet.Title = t.Find("Title").GetComponent<TMP_Text>();
            // tags have 4 or 5 digits: one line, shrinking a little when long
            sheet.Tag.textWrappingMode = TextWrappingModes.NoWrap;
            sheet.Tag.enableAutoSizing = true;
            sheet.Tag.fontSizeMin = 32;
            sheet.Tag.fontSizeMax = sheet.Tag.fontSize;
            sheet.Tag.rectTransform.sizeDelta = new Vector2(190, sheet.Tag.rectTransform.sizeDelta.y);
            sheet.Error = Label(t, "Error", new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-pad, -673), new Vector2(-2 * pad - 180, 42),
                "This name isn't allowed. Try another.", 33, UiRole.OnWarning, TextAnchor.MiddleRight);
            sheet.Error.fontStyle = FontStyles.Bold;
            sheet.Error.textWrappingMode = TextWrappingModes.NoWrap;
            sheet.Error.gameObject.SetActive(false);
            Place(t, "NotNow", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -1150), new Vector2(-2 * pad, 120));
            var terms = Fresh(t, "Terms", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -1285), new Vector2(-2 * pad, 80));
            Img(terms, null, UiRole.None).color = new Color(1, 1, 1, 0);
            var line = Label(terms, "Label", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero,
                "By continuing you agree to the <u>Terms</u>", 33, UiRole.TextMuted);
            line.raycastTarget = false;
            sheet.Terms = terms.gameObject.AddComponent<Button>();
            sheet.Terms.transition = Selectable.Transition.None;
        }

        static void SettingsNames(SettingsCard st)
        {
            var panel = st.Panel;
            var row = Fresh(panel, "NameRow", TopC, TopC, TopC, new Vector2(0, -990), new Vector2(760, 124));
            Round(row, S("ui_card"), UiRole.Row, 36, 124).raycastTarget = false;
            st.NameValue = Label(row, "Name", Vector2.zero, Vector2.one, new Vector2(0, 0.5f), new Vector2(36, 18), new Vector2(-36 - 260, 56),
                "Tuan_Pro#4821", 40, UiRole.Text, TextAnchor.MiddleLeft);
            st.NameValue.rectTransform.anchorMin = new Vector2(0, 0.5f); st.NameValue.rectTransform.anchorMax = new Vector2(1, 0.5f);
            st.NameValue.fontStyle = FontStyles.Bold;
            st.NameValue.textWrappingMode = TextWrappingModes.NoWrap;
            st.NameValue.overflowMode = TextOverflowModes.Ellipsis;
            st.RenameNote = Label(row, "Note", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0.5f), new Vector2(36, -28), new Vector2(-36 - 260, 40),
                "1 free rename left", 28, UiRole.TextMuted, TextAnchor.MiddleLeft);
            st.RenameNote.textWrappingMode = TextWrappingModes.NoWrap;
            var rename = Fresh(row, "Rename", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-22, 0), new Vector2(230, 84));
            Round(rename, S("ui_card"), UiRole.Primary, 42, 84);
            Label(rename, "Label", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero, "Rename", 34, UiRole.OnPrimary).fontStyle = FontStyles.Bold;
            st.Rename = rename.gameObject.AddComponent<Button>();
            rename.gameObject.AddComponent<UiClickSound>();

            var links = Fresh(panel, "Links", TopC, TopC, TopC, new Vector2(0, -1132), new Vector2(760, 64));
            st.Terms = LinkButton(links, "Terms", 0, "Terms");
            st.DeleteData = LinkButton(links, "DeleteData", 1, "Delete my data");

            Place(panel, "Version", TopC, TopC, TopC, new Vector2(0, -1210), new Vector2(760, 56));
            if (st.Privacy != null) ((RectTransform)st.Privacy.transform).anchoredPosition = new Vector2(0, -1280);
            st.SettingsHeight = 1330f;
            st.PausedHeight = 1610f;
        }

        static Button LinkButton(RectTransform parent, string name, int side, string text)
        {
            var b = Fresh(parent, name, new Vector2(side * 0.5f, 0), new Vector2(side * 0.5f + 0.5f, 1), Half, Vector2.zero, Vector2.zero);
            Img(b, null, UiRole.None).color = new Color(1, 1, 1, 0);
            var l = Label(b, "Label", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero, text, 30, UiRole.Primary);
            l.fontStyle = FontStyles.Bold | FontStyles.Underline;
            l.raycastTarget = false;
            var button = b.gameObject.AddComponent<Button>();
            b.gameObject.AddComponent<UiClickSound>();
            return button;
        }

        static ReportSheet ReportSheetCard(Transform safe)
        {
            var c = Fresh(safe, "ReportSheet", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero);
            var sheet = c.gameObject.AddComponent<ReportSheet>();
            const float h = 800f, pad = 66f;
            var p = Fresh(c, "Panel", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, -80), new Vector2(0, h + 80));
            Round(p, S("ui_card"), UiRole.Card, 78, h);
            sheet.Panel = p;
            var t = Fresh(p, "Content", new Vector2(0, 1), Vector2.one, TopC, Vector2.zero, new Vector2(0, h));
            Pic(t, "Handle", TopC, TopC, TopC, new Vector2(0, -28), new Vector2(122, 14), S("ui_pill"), UiRole.Track, true).pixelsPerUnitMultiplier = PillScale(14);
            sheet.Title = Label(t, "Title", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(pad, -97), new Vector2(-2 * pad, 80), "Report this name?", 60, UiRole.Text, TextAnchor.MiddleLeft);
            Heavy(sheet.Title);
            sheet.PlayerName = Label(t, "Name", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(pad, -200), new Vector2(-2 * pad, 70), "LinhNeon#0193", 48, UiRole.Primary, TextAnchor.MiddleLeft);
            sheet.PlayerName.fontStyle = FontStyles.Bold;
            sheet.PlayerName.textWrappingMode = TextWrappingModes.NoWrap;
            sheet.PlayerName.overflowMode = TextOverflowModes.Ellipsis;
            sheet.Body = Label(t, "Body", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(pad, -290), new Vector2(-2 * pad, 150),
                "Our team checks every report. A name that breaks the Terms is reset.", 38, UiRole.TextMuted, TextAnchor.UpperLeft);
            var report = Fresh(t, "Report", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -470), new Vector2(-2 * pad, 150));
            Round(report, S("ui_card"), UiRole.Primary, 55, 150);
            Label(report, "Label", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero, "Report", 50, UiRole.OnPrimary).fontStyle = FontStyles.Bold;
            sheet.Report = report.gameObject.AddComponent<Button>();
            report.gameObject.AddComponent<UiClickSound>();
            var cancel = Fresh(t, "Cancel", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -640), new Vector2(-2 * pad, 120));
            Img(cancel, null, UiRole.None).color = new Color(1, 1, 1, 0);
            Label(cancel, "Label", Vector2.zero, Vector2.one, Half, Vector2.zero, Vector2.zero, "Cancel", 44, UiRole.TextMuted).fontStyle = FontStyles.Bold;
            sheet.Cancel = cancel.gameObject.AddComponent<Button>();
            cancel.gameObject.AddComponent<UiClickSound>();
            c.gameObject.SetActive(false);
            return sheet;
        }

        static void BoardRowLongPress()
        {
            var row = PrefabUtility.LoadPrefabContents(BoardRowPrefab);
            try
            {
                var v = row.GetComponent<BoardRowView>();
                var hit = row.GetComponent<Image>() ?? row.AddComponent<Image>();
                hit.sprite = null;
                hit.color = new Color(1, 1, 1, 0);   // invisible hit area for the press
                hit.raycastTarget = true;
                v.Press = row.GetComponent<LongPress>() ?? row.AddComponent<LongPress>();
                PrefabUtility.SaveAsPrefabAsset(row, BoardRowPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(row); }
        }
    }
}
