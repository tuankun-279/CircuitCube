using System.Collections.Generic;
using CircuitCube.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

namespace CircuitCube.EditorTools
{
    public static partial class UiV2Builder
    {
        const string TextIconsTexture = ArtPaths.Fonts + "/text_clock.png";
        const string TextIconsAsset = ArtPaths.Fonts + "/TextIcons.asset";

        /// <summary>
        /// Version 1.4.0 scoring: the lamp tray item of the approved option D (see <see cref="BuildLampGroupPrefab"/>),
        /// the play clock in the HUD line and the result card's Time box in place of Par (a clock sprite inside the
        /// text, from <see cref="TextIcons"/>). Run after <see cref="Ui130Ftue"/>. Safe to run again.
        /// </summary>
        [MenuItem("Tools/Circuit Cube/Art V2/Build 1.4.0 Score UI (tray D, clock)")]
        public static void Ui140Score()
        {
            BuildLampGroupPrefab();
            var icons = TextIcons();
            var root = PrefabUtility.LoadPrefabContents(UiPrefab);
            try
            {
                var ui = root.GetComponent<UIRoot>();
                ui.Hud.Status.spriteAsset = icons;
                ui.Hud.Status.richText = true;
                var stat = ui.Result.transform.Find("Panel/StatPar") ?? FindDeep(ui.Result.transform, "StatPar");
                if (stat != null)
                {
                    var label = stat.Find("Label").GetComponent<TMP_Text>();
                    label.spriteAsset = icons;
                    label.richText = true;
                    label.text = "<sprite name=\"clock\" tint=1> Time";
                }
                else Debug.LogWarning("[CircuitCube] Result card StatPar not found.");
                PrefabUtility.SaveAsPrefabAsset(root, UiPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[CircuitCube] 1.4.0 score UI built.");
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = FindDeep(c, name); if (f != null) return f; }
            return null;
        }

        /// <summary>
        /// The TextMeshPro sprite asset for icons inside text (one sprite, "clock", white so &lt;sprite tint=1&gt; takes
        /// the text colour). Rebuilt from Art/Fonts/text_clock.png each run.
        /// </summary>
        public static TMP_SpriteAsset TextIcons()
        {
            if (AssetImporter.GetAtPath(TextIconsTexture) is TextureImporter ti && (ti.textureType != TextureImporterType.Default || ti.mipmapEnabled || !ti.alphaIsTransparency))
            {
                ti.textureType = TextureImporterType.Default;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TextIconsTexture);
            var asset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(TextIconsAsset);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                AssetDatabase.CreateAsset(asset, TextIconsAsset);
            }
            asset.spriteSheet = tex;
            int w = tex.width, h = tex.height;
            // sized like a capital letter of the text: the glyph box is the art, raised a little off the baseline
            var glyph = new TMP_SpriteGlyph { index = 0, metrics = new GlyphMetrics(w, h, 0, h * 0.86f, w * 1.06f), glyphRect = new GlyphRect(0, 0, w, h), scale = 1f };
            asset.spriteGlyphTable.Clear();
            asset.spriteGlyphTable.Add(glyph);
            asset.spriteCharacterTable.Clear();
            asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xFFFE, glyph) { name = "clock" });
            var mat = asset.material;
            if (mat == null)
            {
                mat = new Material(Shader.Find("TextMeshPro/Sprite")) { name = "TextIcons Material" };
                AssetDatabase.AddObjectToAsset(mat, asset);
                asset.material = mat;
            }
            mat.SetTexture(ShaderUtilities.ID_MainTex, tex);
            asset.UpdateLookupTables();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
