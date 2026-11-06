using System.IO;
using UnityEditor;
using UnityEngine;

namespace CircuitCube.EditorTools
{
    /// <summary>
    /// Import settings for the ChatGPT art (folders in <see cref="ArtPaths"/>). Only import settings: no UI is built here.
    /// - Art/UI: sprites without mipmaps, uncompressed (the UI atlas compresses them once). The rounded shapes in UI/Common get 9-slice borders from their corner
    ///   radius; backgrounds may be 2048 px, the app icon 1024, everything else 512.
    /// - Art/Board: textures for the board layers (mipmapped, clamped, alpha kept, 512 px).
    /// Applied once per file (marked in the importer's user data); after that changes made in the inspector or Sprite
    /// Editor are kept. <see cref="Reapply"/> clears the mark and imports again.
    /// </summary>
    public sealed class UIAssetImporter : AssetPostprocessor
    {
        const string Marker = "circuitcube-art";
        /// <summary>UI/Common shapes that stretch with 9-slice borders.</summary>
        public static readonly string[] NineSlice = { "ui_button", "ui_card", "ui_pill", "ui_square", "ui_bar", "ui_card_shadow" };

        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            bool ui = path.StartsWith(ArtPaths.UI + "/");
            bool board = path.StartsWith(ArtPaths.Board + "/");
            if (!ui && !board) return;
            if (!(assetImporter is TextureImporter ti)) return;
            if (ti.userData != null && ti.userData.Contains(Marker)) return;

            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.sRGBTexture = true;
            string name = Path.GetFileNameWithoutExtension(path);
            if (board)
            {
                ti.textureType = TextureImporterType.Default;
                ti.mipmapEnabled = true;
                ti.maxTextureSize = 512;
            }
            else
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled = false;
                ti.maxTextureSize = path.Contains("/Backgrounds/") ? 2048 : name == "app_icon" ? 1024 : 512;
                // sprites are packed into the UI atlas, which is compressed once; the full-screen backgrounds are not
                ti.textureCompression = path.Contains("/Backgrounds/") ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed;
                if (System.Array.IndexOf(NineSlice, name) >= 0)
                {
                    float b = Border(path);
                    if (b <= 0f) return;   // leave it unmarked so the next import tries again
                    ti.spriteBorder = new Vector4(b, b, b, b);
                    var settings = new TextureImporterSettings();
                    ti.ReadTextureSettings(settings);
                    settings.spriteMeshType = SpriteMeshType.FullRect;
                    ti.SetTextureSettings(settings);
                }
            }
            ti.userData = Marker;
        }

        /// <summary>Import every art texture again with the rules above.</summary>
        public static void Reapply()
        {
            foreach (var root in new[] { ArtPaths.UI, ArtPaths.Board })
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { root }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
                    ti.userData = "";
                    ti.SaveAndReimport();
                }
        }

        /// <summary>Corner radius of a rounded shape: where its top edge becomes straight, plus a little.</summary>
        public static float Border(string path)
        {
            if (!File.Exists(path)) return 0f;
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(path));
            int w = tex.width, h = tex.height;
            int y = h - 1 - Mathf.Max(1, h / 64);
            int first = 0;
            for (int x = 0; x < w / 2; x++) if (tex.GetPixel(x, y).a > 0.5f) { first = x; break; }
            // walk down from the top row until the left edge is straight
            int r = first;
            for (int yy = h - 1; yy > h / 2; yy--)
            {
                int fx = 0;
                for (int x = 0; x < w / 2; x++) if (tex.GetPixel(x, yy).a > 0.5f) { fx = x; break; }
                if (fx <= 1) { r = Mathf.Max(r, h - 1 - yy); break; }
            }
            Object.DestroyImmediate(tex);
            float max = Mathf.Min(w, h) * 0.5f - 1f;
            return Mathf.Min(max, r + 4f);
        }
    }
}
