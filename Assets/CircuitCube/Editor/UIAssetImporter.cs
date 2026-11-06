using UnityEditor;
using UnityEngine;

namespace CircuitCube.EditorTools
{
    /// <summary>
    /// Import settings for the ChatGPT art. Only import settings: no UI is built here.
    /// - Art/UI: sprites. ui_btn_, ui_panel_, ui_hud_, ui_level_node_ get 9-slice borders of 24% of the shorter side at
    ///   200 pixels per unit; ui_chip and ui_bar_ (pills) get borders of half their height.
    /// - Art/Board: textures for the 3D tile layers (mipmapped, clamped, alpha kept).
    /// Applied once per file; after that your own changes in the inspector or Sprite Editor are kept.
    /// </summary>
    public sealed class UIAssetImporter : AssetPostprocessor
    {
        const string Marker = "circuitcube-ui";
        static readonly string[] Sliced = { "ui_btn_", "ui_panel_", "ui_hud_", "ui_level_node_" };
        static readonly string[] Pills = { "ui_chip", "ui_bar_" };

        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            bool ui = path.Contains("/CircuitCube/Art/UI/");
            bool board = path.Contains("/CircuitCube/Art/Board/");
            if (!ui && !board) return;
            if (!(assetImporter is TextureImporter importer)) return;
            if (importer.userData != null && importer.userData.Contains(Marker)) return;

            if (board)
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = true;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 1024;
                importer.userData = Marker;
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 2048;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spritePixelsPerUnit = 100;

            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            float share = 0f;
            foreach (var p in Sliced) if (file.StartsWith(p)) share = 0.24f;
            foreach (var p in Pills) if (file.StartsWith(p)) share = 0.49f;
            if (share > 0f)
            {
                importer.GetSourceTextureWidthAndHeight(out int w, out int h);
                if (w <= 0 || h <= 0) ReadPngSize(assetPath, out w, out h);
                if (w <= 0 || h <= 0) return;   // leave it unmarked so the next reimport tries again
                float b = Mathf.Round(Mathf.Min(w, h) * share);
                importer.spriteBorder = new Vector4(b, b, b, b);
                importer.spritePixelsPerUnit = 200;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
            }
            importer.userData = Marker;
        }

        /// <summary>Width and height from a PNG header (big-endian bytes 16-23). Zero if the file is not a PNG.</summary>
        static void ReadPngSize(string path, out int w, out int h)
        {
            w = h = 0;
            try
            {
                using (var fs = System.IO.File.OpenRead(path))
                {
                    var buf = new byte[24];
                    if (fs.Read(buf, 0, 24) < 24 || buf[1] != (byte)'P' || buf[2] != (byte)'N' || buf[3] != (byte)'G') return;
                    w = (buf[16] << 24) | (buf[17] << 16) | (buf[18] << 8) | buf[19];
                    h = (buf[20] << 24) | (buf[21] << 16) | (buf[22] << 8) | buf[23];
                }
            }
            catch (System.IO.IOException) { }
        }
    }
}
