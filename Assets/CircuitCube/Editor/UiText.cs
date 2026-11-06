using TMPro;
using UnityEditor;
using UnityEngine;

namespace CircuitCube.EditorTools
{
    /// <summary>Text for the editor UI builders: TextMeshPro with the Inter font assets in Art/Fonts.</summary>
    public static class UiText
    {
        public static TMP_FontAsset SemiBold => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ArtPaths.Fonts + "/Inter-SemiBold SDF.asset");
        public static TMP_FontAsset Regular => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ArtPaths.Fonts + "/Inter-Regular SDF.asset");

        /// <summary>The TextMeshPro alignment for a legacy text anchor.</summary>
        public static TextAlignmentOptions Align(TextAnchor a)
        {
            switch (a)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Center;
            }
        }

        /// <summary>A TextMeshPro text on <paramref name="go"/> (reused if there is one), wrapping, overflowing downwards.</summary>
        public static TextMeshProUGUI On(GameObject go, string text, float size, TextAnchor align)
        {
            var t = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
            t.font = SemiBold;
            t.text = text;
            t.fontSize = size;
            t.alignment = Align(align);
            t.richText = true;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.margin = Vector4.zero;
            t.raycastTarget = false;
            return t;
        }
    }
}
