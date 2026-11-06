using System.Collections.Generic;
using System.IO;
using CircuitCube.Game;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace CircuitCube.EditorTools
{
    /// <summary>
    /// Packs the art into atlases (Tools/Circuit Cube/Build Atlases):
    /// - Art/Atlases/UI.spriteatlasv2: the UI sprites (Common, Icons, Shapes, Mascot, logo), so screens batch into
    ///   few draw calls. Backgrounds stay separate full-screen images.
    /// - Art/Atlases/Board_Dark.png and Board_Light.png: every image in Art/Board/&lt;theme&gt; and Art/Board/Shared
    ///   (faces, wires, cores, lamp and source parts, hint pin). Each image gets a border copied from its own edge pixels, so wires stay seamless
    ///   across tiles and mipmaps do not bleed. The rectangles are stored on the theme asset by image name, where the
    ///   board renderer looks them up; the single images are not referenced by the game, so builds carry only the atlas.
    /// Run it again after changing any board or UI image.
    /// </summary>
    public static class AtlasBuilder
    {
        const int Pad = 16;
        const string ThemeDir = "Assets/CircuitCube/Themes";

        [MenuItem("Tools/Circuit Cube/Build Atlases")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(ArtPaths.Atlases);
            BuildUiAtlas();
            foreach (var name in new[] { "Dark", "Light" })
            {
                var theme = AssetDatabase.LoadAssetAtPath<GameTheme>(ThemeDir + "/Theme_" + name + ".asset");
                if (theme != null) BuildBoardAtlas(theme, name);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[CircuitCube] Atlases built.");
        }

        // ------------------------------------------------------------------ UI

        public static void BuildUiAtlas()
        {
            string path = ArtPaths.Atlases + "/UI.spriteatlasv2";
            var atlas = new SpriteAtlasAsset();
            var content = new List<Object>();
            foreach (var folder in new[] { "Common", "Icons", "Shapes", "Mascot" })
            {
                var f = AssetDatabase.LoadAssetAtPath<DefaultAsset>(ArtPaths.UI + "/" + folder);
                if (f != null) content.Add(f);
            }
            var logo = ArtPaths.Sprite("logo_mark");
            if (logo != null) content.Add(logo);
            atlas.Add(content.ToArray());
            SpriteAtlasAsset.Save(atlas, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(path);
            importer.packingSettings = new SpriteAtlasPackingSettings
            {
                padding = 4, enableRotation = false, enableTightPacking = false, enableAlphaDilation = true, blockOffset = 1,
            };
            importer.textureSettings = new SpriteAtlasTextureSettings
            {
                readable = false, generateMipMaps = false, sRGB = true, filterMode = FilterMode.Bilinear, anisoLevel = 0,
            };
            var platform = importer.GetPlatformSettings("DefaultTexturePlatform");
            platform.maxTextureSize = 4096;
            platform.textureCompression = TextureImporterCompression.Compressed;
            importer.SetPlatformSettings(platform);
            importer.includeInBuild = true;
            importer.SaveAndReimport();
            var packed = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
            if (packed != null) SpriteAtlasUtility.PackAtlases(new[] { packed }, EditorUserBuildSettings.activeBuildTarget);
        }

        // ------------------------------------------------------------------ board

        struct Item { public string Image; public Texture2D Pixels; public int X, Y; }

        public static void BuildBoardAtlas(GameTheme theme, string name)
        {
            // every image of the theme's board folder, plus the shared ones (hint pin)
            var items = new List<Item>();
            foreach (var folder in new[] { ArtPaths.Board + "/" + name, ArtPaths.Board + "/Shared" })
                foreach (var file in Directory.GetFiles(folder, "*.png"))
                {
                    var px = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    px.LoadImage(File.ReadAllBytes(file));
                    items.Add(new Item { Image = Path.GetFileNameWithoutExtension(file), Pixels = px });
                }
            items.Sort((a, b) => b.Pixels.height.CompareTo(a.Pixels.height));

            // shelf packing, about square
            long area = 0;
            foreach (var it in items) area += (long)(it.Pixels.width + 2 * Pad) * (it.Pixels.height + 2 * Pad);
            int width = Mathf.CeilToInt(Mathf.Sqrt(area * 1.15f) / 4f) * 4;
            int x = 0, y = 0, shelf = 0;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                int w = it.Pixels.width + 2 * Pad, h = it.Pixels.height + 2 * Pad;
                if (x + w > width) { x = 0; y += shelf; shelf = 0; }
                it.X = x; it.Y = y;
                items[i] = it;
                x += w;
                shelf = Mathf.Max(shelf, h);
            }
            int height = Mathf.CeilToInt((y + shelf) / 4f) * 4;

            var pixels = new Color32[width * height];
            var rects = new List<AtlasRect>();
            foreach (var it in items)
            {
                Blit(pixels, width, it.Pixels, it.X + Pad, it.Y + Pad);
                int w = it.Pixels.width, h = it.Pixels.height;
                // half-texel inset: uv 0 and 1 land on the centres of the image's own edge pixels
                var uv = new Vector4((w - 1f) / width, (h - 1f) / height, (it.X + Pad + 0.5f) / width, (it.Y + Pad + 0.5f) / height);
                rects.Add(new AtlasRect { Image = it.Image, Uv = uv });
                Object.DestroyImmediate(it.Pixels);
            }
            var atlas = new Texture2D(width, height, TextureFormat.RGBA32, false);
            atlas.SetPixels32(pixels);
            atlas.Apply();
            string path = ArtPaths.Atlases + "/Board_" + name + ".png";
            File.WriteAllBytes(path, atlas.EncodeToPNG());
            Object.DestroyImmediate(atlas);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Trilinear;
            ti.maxTextureSize = 4096;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.SaveAndReimport();

            theme.BoardAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            theme.BoardAtlasRects = rects.ToArray();
            EditorUtility.SetDirty(theme);
        }

        /// <summary>Copy an image in and repeat its edge pixels into the padding around it.</summary>
        static void Blit(Color32[] atlas, int atlasWidth, Texture2D img, int ox, int oy)
        {
            int w = img.width, h = img.height;
            var src = img.GetPixels32();
            for (int yy = -Pad; yy < h + Pad; yy++)
            {
                int sy = Mathf.Clamp(yy, 0, h - 1);
                int row = (oy + yy) * atlasWidth + ox;
                for (int xx = -Pad; xx < w + Pad; xx++)
                    atlas[row + xx] = src[sy * w + Mathf.Clamp(xx, 0, w - 1)];
            }
        }
    }
}
