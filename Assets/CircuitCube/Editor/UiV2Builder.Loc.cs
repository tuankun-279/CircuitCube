using System.Collections.Generic;
using CircuitCube.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.EditorTools
{
    public static partial class UiV2Builder
    {
        static readonly string[] ItemPrefabs =
        {
            "Assets/CircuitCube/Prefabs/UI/LampGroup.prefab",
            "Assets/CircuitCube/Prefabs/UI/LevelNode.prefab",
            "Assets/CircuitCube/Prefabs/UI/ShapeCard.prefab",
            "Assets/CircuitCube/Prefabs/UI/LegendChip.prefab",
            "Assets/CircuitCube/Prefabs/UI/BoardRow.prefab",
        };

        /// <summary>
        /// Every label fits its box in every language: buttons with an icon keep the icon and the text centred together
        /// (<see cref="IconLabel"/>), and one-line labels shrink (TMP auto size, down to 70 %) instead of running past
        /// their box or over their neighbours. Vietnamese is often a third longer than the English the UI was laid out
        /// with. Run after <see cref="Ui130Ftue"/>. Safe to run again.
        /// </summary>
        [MenuItem("Tools/Circuit Cube/Art V2/Fit labels to their language")]
        public static void FitLabels()
        {
            int pairs = 0, sized = 0;
            var root = PrefabUtility.LoadPrefabContents(UiPrefab);
            try
            {
                FitIn(root.transform, ref pairs, ref sized);
                PrefabUtility.SaveAsPrefabAsset(root, UiPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            foreach (var path in ItemPrefabs)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                var item = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    FitIn(item.transform, ref pairs, ref sized);
                    PrefabUtility.SaveAsPrefabAsset(item, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(item); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[CircuitCube] Labels fitted: " + pairs + " icon buttons, " + sized + " one-line labels.");
        }

        static void FitIn(Transform root, ref int pairs, ref int sized)
        {
            // icon + text buttons
            var done = new HashSet<TMP_Text>();
            foreach (var b in root.GetComponentsInChildren<Button>(true))
            {
                var icon = b.transform.Find("Icon") as RectTransform;
                var label = b.transform.Find("Label");
                var text = label != null ? label.GetComponent<TMP_Text>() : null;
                if (icon == null || text == null || !icon.gameObject.activeSelf) continue;
                // only an icon beside the text (not a round icon button, where the icon fills the button)
                if (icon.anchorMin != icon.anchorMax) continue;
                var il = b.GetComponent<IconLabel>();
                if (il == null) il = b.gameObject.AddComponent<IconLabel>();
                il.Icon = icon; il.Label = text;
                il.Gap = 16f;
                il.Padding = Mathf.Max(16f, ((RectTransform)b.transform).rect.height * 0.18f);
                Shrinkable(text);
                done.Add(text);
                pairs++;
            }
            // one-line labels in boxes of their own: buttons, chips, tiles, rows, headings
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (done.Contains(t)) continue;
                var rt = (RectTransform)t.transform;
                float h = rt.rect.height;
                bool oneLine = h > 0f && h < t.fontSize * 2.1f;
                if (!oneLine || t.enableAutoSizing) continue;
                Shrinkable(t);
                sized++;
            }
        }

        static void Shrinkable(TMP_Text t)
        {
            if (!t.enableAutoSizing)
            {
                t.fontSizeMax = t.fontSize;
                t.fontSizeMin = Mathf.Max(10f, Mathf.Round(t.fontSize * 0.7f));
            }
            t.enableAutoSizing = true;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
        }
    }
}
