using CircuitCube.Game;
using CircuitCube.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.EditorTools
{
    /// <summary>
    /// Adds the "Win as" row to Prefabs/UI/DebugMenu.prefab: six buttons copied from the first shape button, one per
    /// <see cref="CircuitCubeApp.DebugResults"/>, under Reset; the panel grows to fit. Safe to run again.
    /// </summary>
    public static class DebugMenuBuilder
    {
        const string PrefabPath = "Assets/CircuitCube/Prefabs/UI/DebugMenu.prefab";
        const float Extra = 300f;

        [MenuItem("Tools/Circuit Cube/Debug/Build Win Buttons")]
        public static void Build()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var menu = root.GetComponent<DebugMenu>();
                var panel = (RectTransform)menu.Panel.transform;
                var old = panel.Find("WinRow");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                else panel.sizeDelta += new Vector2(0f, Extra);   // first run only

                var row = new GameObject("WinRow", typeof(RectTransform)).GetComponent<RectTransform>();
                row.SetParent(panel, false);
                row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
                row.pivot = new Vector2(0f, 1f);
                row.anchoredPosition = new Vector2(0f, -1010f);
                row.sizeDelta = new Vector2(panel.sizeDelta.x, Extra);

                var labelSrc = panel.Find("LevelLabel");
                var label = Object.Instantiate(labelSrc.gameObject, row).GetComponent<RectTransform>();
                label.name = "Label";
                label.anchoredPosition = new Vector2(60f, -10f);
                label.GetComponent<TMP_Text>().text = "Win as (in play or the level above)";

                var template = panel.Find("Shape0");
                var names = CircuitCubeApp.DebugResults;
                var buttons = new Button[names.Length];
                for (int i = 0; i < names.Length; i++)
                {
                    var b = Object.Instantiate(template.gameObject, row).GetComponent<RectTransform>();
                    b.name = "Win" + i;
                    b.anchoredPosition = new Vector2(60f + (i % 3) * 280f, -70f - (i / 3) * 115f);
                    var sel = b.Find("Selected");
                    if (sel != null) Object.DestroyImmediate(sel.gameObject);
                    var tx = b.Find("Label").GetComponent<TMP_Text>();
                    tx.text = names[i];
                    tx.enableAutoSizing = true; tx.fontSizeMin = 22; tx.fontSizeMax = tx.fontSize;
                    var btn = b.GetComponent<Button>();
                    btn.onClick.RemoveAllListeners();
                    buttons[i] = btn;
                }
                menu.WinButtons = buttons;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Debug.Log("[CircuitCube] Debug menu win buttons built.");
        }
    }
}
