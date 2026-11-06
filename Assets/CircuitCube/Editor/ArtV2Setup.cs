using System.Collections.Generic;
using System.IO;
using CircuitCube.Game;
using CircuitCube.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.EditorTools
{
    /// <summary>
    /// Puts the V2 art (Assets/CircuitCube/Art/V2, cut from the ChatGPT sheets in ArtRef) into the game:
    /// import settings, per-theme board materials, the two Theme assets, a white core layer on every wire of the tile
    /// prefabs, V2 sprites and theme roles on the UI prefabs, the theme fields of the scene's app, and the app icon.
    /// Safe to run again.
    /// </summary>
    public static class ArtV2Setup
    {
        const string V2 = "Assets/CircuitCube/Art/V2";
        const string OldMats = "Assets/CircuitCube/Art/Materials";
        const string ThemeDir = "Assets/CircuitCube/Themes";
        const string Prefabs = "Assets/CircuitCube/Prefabs";

        static readonly string[] SquareWires = { "wire_end", "wire_straight", "wire_corner", "wire_tee", "wire_cross" };
        static readonly string[] TriWires = { "tri_wire_end", "tri_wire_bend", "tri_wire_fork" };
        static readonly string[] IconIds = { "cube", "tetra", "octa", "cylinder", "dodeca", "sphere", "megamix", "cross" };
        static readonly string[] IconFiles = { "shape_cube", "shape_tetra", "shape_octa", "shape_cylinder", "shape_dodeca", "shape_sphere", "shape_megamix", "shape_megamix" };
        static readonly string[] NineSlice = { "ui_button", "ui_card", "ui_pill", "ui_square", "ui_bar", "ui_card_shadow" };

        [MenuItem("Tools/Circuit Cube/Art V2/Apply Art V2")]
        public static void ApplyAll()
        {
            ImportSettings();
            var dark = BuildTheme(true);
            var light = BuildTheme(false);
            AddCoreLayers(dark);
            NodeLayers();
            StyleUI();
            UiV2Builder.Build();
            WireScene(dark, light);
            AppIcon();
            AssetDatabase.SaveAssets();
            Debug.Log("[CircuitCube] Art V2 applied. F2 in play mode switches dark / light.");
        }

        // ------------------------------------------------------------------ textures
        public static void ImportSettings()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { V2 }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                if (ti == null) continue;
                bool ui = path.Contains("/UI/");
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.sRGBTexture = true;
                if (ui)
                {
                    ti.textureType = TextureImporterType.Sprite;
                    ti.spriteImportMode = SpriteImportMode.Single;
                    ti.mipmapEnabled = false;
                    ti.maxTextureSize = path.Contains("/Backgrounds/") ? 2048 : Path.GetFileNameWithoutExtension(path) == "app_icon" ? 1024 : 512;
                    string name = Path.GetFileNameWithoutExtension(path);
                    if (System.Array.IndexOf(NineSlice, name) >= 0)
                    {
                        float b = Border(path);
                        ti.spriteBorder = new Vector4(b, b, b, b);
                    }
                }
                else
                {
                    ti.textureType = TextureImporterType.Default;
                    ti.mipmapEnabled = true;
                    ti.maxTextureSize = 512;
                }
                ti.SaveAndReimport();
            }
        }

        /// <summary>Corner radius of a rounded shape: where its top edge becomes opaque, plus a little.</summary>
        static float Border(string path)
        {
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(path));
            int w = tex.width, h = tex.height;
            int y = h - 1 - Mathf.Max(1, h / 64);
            int first = 0;
            for (int x = 0; x < w / 2; x++) if (tex.GetPixel(x, y).a > 0.5f) { first = x; break; }
            // the radius ends where the top edge is straight: walk inwards from the first opaque pixel on the top row
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

        // ------------------------------------------------------------------ theme assets
        static Material Mat(string folder, string name, string template, string texPath)
        {
            string dir = V2 + "/Materials/" + folder;
            Directory.CreateDirectory(dir);
            string path = dir + "/M2_" + name + ".mat";
            var tmpl = AssetDatabase.LoadAssetAtPath<Material>(OldMats + "/" + template + ".mat");
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = tmpl != null ? new Material(tmpl) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            else if (tmpl != null) m.CopyPropertiesFromMaterial(tmpl);
            if (tex != null) { m.SetTexture("_BaseMap", tex); m.mainTexture = tex; }
            m.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
        static Color Hex(string h, float a) { var c = Hex(h); c.a = a; return c; }

        public static GameTheme BuildTheme(bool dark)
        {
            Directory.CreateDirectory(ThemeDir);
            string folder = dark ? "Dark" : "Light";
            string path = ThemeDir + "/Theme_" + folder + ".asset";
            var t = AssetDatabase.LoadAssetAtPath<GameTheme>(path);
            if (t == null) { t = ScriptableObject.CreateInstance<GameTheme>(); AssetDatabase.CreateAsset(t, path); }
            t.Dark = dark;
            string b = V2 + "/Board/" + folder + "/";

            BoardSkin Skin(string baseName, string baseTmpl, string[] wires, string wireTmpl)
            {
                var s = new BoardSkin();
                s.Base = Mat(folder, baseName, baseTmpl, b + baseName + ".png");
                s.Wires = new Material[wires.Length];
                s.WireCores = new Material[wires.Length];
                for (int i = 0; i < wires.Length; i++)
                {
                    s.Wires[i] = Mat(folder, wires[i], wireTmpl, b + wires[i] + ".png");
                    s.WireCores[i] = Mat(folder, wires[i] + "_core", wireTmpl, b + wires[i] + "_core.png");
                }
                s.LampHalo = Mat(folder, "glow_halo", "M_lamp_glow", b + "glow_halo.png");
                s.LampRing = Mat(folder, "lamp_ring", "M_lamp_socket", b + "lamp_ring.png");
                s.LampOff = Mat(folder, "lamp_core_off", "M_lamp_bulb_off", b + "lamp_core_off.png");
                s.LampOn = Mat(folder, "lamp_core_on", "M_lamp_bulb_on", b + "lamp_core_on.png");
                s.SourceHalo = Mat(folder, "source_halo", "M_source_ring", b + "glow_halo.png");
                s.SourceOutline = Mat(folder, "source_outline", "M_source_disc", b + "source_outline.png");
                s.SourceCenter = Mat(folder, "source_center", "M_source_mark", b + "source_center.png");
                return s;
            }
            t.Square = Skin("board_tile", "M_board_tile", SquareWires, "M_wire_end");
            t.Triangle = Skin("board_tri", "M_board_tri", TriWires, "M_tri_wire_end");

            if (dark)
            {
                t.CameraBackground = Hex("0A0C14");
                t.Backdrop = S("Backgrounds/bg_dark");
                t.ShadeDark = new Color(0.70f, 0.73f, 0.84f); t.ShadeLight = Color.white;
                t.Red = Hex("FF4D6D"); t.Blue = Hex("3DA9FF"); t.Yellow = Hex("FFE14D"); t.Purple = Hex("C77DFF");
                t.Orange = Hex("FF9A3D"); t.Green = Hex("3DFFA2"); t.White = Hex("F4F7FF"); t.WireOff = Hex("3A4360");
                t.TintLampCore = false; t.HaloAlpha = 0.45f;
                t.Ui = new List<RoleColor>
                {
                    new RoleColor(UiRole.Background, Hex("0A0C14")), new RoleColor(UiRole.Card, Hex("141826")),
                    new RoleColor(UiRole.Primary, Hex("46B8C6")), new RoleColor(UiRole.OnPrimary, Hex("071418")),
                    new RoleColor(UiRole.Secondary, Hex("1B2133")), new RoleColor(UiRole.OnSecondary, Hex("E3E8F5")),
                    new RoleColor(UiRole.Round, Hex("141826")), new RoleColor(UiRole.OnRound, Hex("E3E8F5")),
                    new RoleColor(UiRole.Text, Hex("EEF1F7")), new RoleColor(UiRole.TextMuted, Hex("8A93AD")),
                    new RoleColor(UiRole.Dim, Hex("0A0C14", 0.72f)), new RoleColor(UiRole.Overlay, Hex("000000", 0.6f)),
                    new RoleColor(UiRole.Track, Hex("262E46")), new RoleColor(UiRole.Accent, Hex("46B8C6")),
                    new RoleColor(UiRole.Star, Hex("FFC83D")), new RoleColor(UiRole.Chip, Hex("141826")),
                    new RoleColor(UiRole.NodeOpen, Hex("141826")), new RoleColor(UiRole.NodeCurrent, Hex("46B8C6")),
                    new RoleColor(UiRole.NodeLocked, Hex("0F1320")), new RoleColor(UiRole.OnNode, Hex("E3E8F5")),
                };
            }
            else
            {
                t.CameraBackground = Hex("EEF1F7");
                t.Backdrop = S("Backgrounds/bg_light");
                t.ShadeDark = new Color(0.86f, 0.88f, 0.93f); t.ShadeLight = Color.white;
                t.Red = Hex("F0435F"); t.Blue = Hex("2F8FE8"); t.Yellow = Hex("EFA300"); t.Purple = Hex("A15BEB");
                t.Orange = Hex("F07A1A"); t.Green = Hex("13B07A"); t.White = Hex("9AA3B8"); t.WireOff = Hex("C3CAD8");
                t.TintLampCore = true; t.HaloAlpha = 0.22f;
                t.Ui = new List<RoleColor>
                {
                    new RoleColor(UiRole.Background, Hex("EEF1F7")), new RoleColor(UiRole.Card, Hex("FFFFFF")),
                    new RoleColor(UiRole.Primary, Hex("1E9AAA")), new RoleColor(UiRole.OnPrimary, Hex("FFFFFF")),
                    new RoleColor(UiRole.Secondary, Hex("FFFFFF")), new RoleColor(UiRole.OnSecondary, Hex("18203A")),
                    new RoleColor(UiRole.Round, Hex("FFFFFF")), new RoleColor(UiRole.OnRound, Hex("18203A")),
                    new RoleColor(UiRole.Text, Hex("18203A")), new RoleColor(UiRole.TextMuted, Hex("626C88")),
                    new RoleColor(UiRole.Dim, Hex("EEF1F7", 0.78f)), new RoleColor(UiRole.Overlay, Hex("18203A", 0.45f)),
                    new RoleColor(UiRole.Track, Hex("D9DEEA")), new RoleColor(UiRole.Accent, Hex("1E9AAA")),
                    new RoleColor(UiRole.Star, Hex("F5B300")), new RoleColor(UiRole.Chip, Hex("FFFFFF")),
                    new RoleColor(UiRole.NodeOpen, Hex("FFFFFF")), new RoleColor(UiRole.NodeCurrent, Hex("1E9AAA")),
                    new RoleColor(UiRole.NodeLocked, Hex("E4E8F0")), new RoleColor(UiRole.OnNode, Hex("18203A")),
                };
            }
            EditorUtility.SetDirty(t);
            return t;
        }

        // ------------------------------------------------------------------ board prefabs
        public static void AddCoreLayers(GameTheme dark)
        {
            foreach (var name in new[] { "Tile", "TriTile" })
            {
                string path = Prefabs + "/Board/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                var bt = root.GetComponent<BoardTile>();
                var skin = dark.SkinFor(bt.Ports);
                var cores = new Renderer[bt.Wires.Length];
                for (int i = 0; i < bt.Wires.Length; i++)
                {
                    var w = bt.Wires[i];
                    if (w == null) continue;
                    var existing = w.transform.Find("Core");
                    GameObject go;
                    if (existing != null) go = existing.gameObject;
                    else
                    {
                        go = new GameObject("Core");
                        go.transform.SetParent(w.transform, false);
                        go.AddComponent<MeshFilter>().sharedMesh = w.GetComponent<MeshFilter>().sharedMesh;
                        var mr = go.AddComponent<MeshRenderer>();
                        var src = (MeshRenderer)w;
                        mr.shadowCastingMode = src.shadowCastingMode;
                        mr.receiveShadows = false;
                    }
                    go.transform.localPosition = new Vector3(0, 0, 0.002f);
                    go.transform.localRotation = Quaternion.identity;
                    go.transform.localScale = Vector3.one;
                    cores[i] = go.GetComponent<Renderer>();
                    if (i < skin.WireCores.Length) cores[i].sharedMaterial = skin.WireCores[i];
                }
                bt.WireCores = cores;
                var mark = root.transform.Find("Source/Mark");
                if (mark != null) bt.SourceMark = mark.GetComponent<Renderer>();
                // default look in the editor = dark theme
                bt.ApplySkin(dark);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ------------------------------------------------------------------ sources and lamps
        // The V2 node sprites are cut on one shared scale (ring = 60% of the sprite), so every layer of a lamp, and every
        // layer of a source, sits on a quad of the same size; only the halo is larger. The clips are rebuilt to match.
        const float LampSize = 1.25f, SourceSize = 1.2f, HaloSize = 1.9f;

        public static void NodeLayers()
        {
            foreach (var name in new[] { "Tile", "TriTile" })
            {
                string path = Prefabs + "/Board/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                float k = name == "Tile" ? 1f : 0.752f;          // triangle tiles are smaller
                Size(root, "Lamp/Socket", LampSize * k);
                Size(root, "Lamp/BulbOff", LampSize * k);
                Size(root, "Lamp/BulbOn", LampSize * k);
                Size(root, "Lamp/Glow", HaloSize * k);
                Size(root, "Source/Ring", HaloSize * k * 0.9f);
                Size(root, "Source/Disc", SourceSize * k);
                Size(root, "Source/Mark", SourceSize * k);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);

                string pre = name == "Tile" ? "" : "Tri";
                float L = LampSize * k, H = HaloSize * k, S = SourceSize * k;
                // light up: the core pops in, the ring gives a small kick, the halo blooms and settles
                Clip(pre + "LampOn", false,
                    ("BulbOn", new[] { 0f, 0.12f, 0.28f }, new[] { 0.35f * L, 1.15f * L, L }),
                    ("Socket", new[] { 0f, 0.1f, 0.26f }, new[] { L, 1.12f * L, L }),
                    ("Glow", new[] { 0f, 0.16f, 0.45f }, new[] { 0.2f * H, 1.25f * H, H }));
                // while lit: the halo breathes slowly
                Clip(pre + "LampLit", true,
                    ("Glow", new[] { 0f, 0.8f, 1.6f }, new[] { H, 1.1f * H, H }));
                // going dark: the dark core settles back in
                Clip(pre + "LampOff", false,
                    ("BulbOff", new[] { 0f, 0.16f }, new[] { 0.8f * L, L }));
                // shockwave: a ring of the lamp's colour expands past the tile edge, then hides itself
                Clip(pre + "Burst", false,
                    ("", new[] { 0f, 0.3f }, new[] { 0.9f * L, 2.3f * L }));
                HideAt(pre + "Burst", 0.3f);
                // source idle: outline and centre breathe together
                Clip(pre + "SourceIdle", true,
                    ("Disc", new[] { 0f, 1.5f, 3f, 4.5f, 6f }, new[] { S, 1.06f * S, S, 1.06f * S, S }),
                    ("Mark", new[] { 0f, 1.5f, 3f, 4.5f, 6f }, new[] { S, 1.06f * S, S, 1.06f * S, S }));
            }
        }

        /// <summary>Deactivate the clip's own object at the given time, so a one-shot effect cleans up after itself.</summary>
        static void HideAt(string clipName, float time)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/CircuitCube/Animations/" + clipName + ".anim");
            if (clip == null) return;
            var curve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(time, 0f));
            AnimationUtility.SetKeyRightTangentMode(curve, 0, AnimationUtility.TangentMode.Constant);
            AnimationUtility.SetKeyLeftTangentMode(curve, 1, AnimationUtility.TangentMode.Constant);
            clip.SetCurve("", typeof(GameObject), "m_IsActive", curve);
            EditorUtility.SetDirty(clip);
        }

        static void Size(GameObject root, string path, float size)
        {
            var t = root.transform.Find(path);
            if (t == null) { Debug.LogWarning("[CircuitCube] Tile layer not found: " + path); return; }
            var s = t.localScale;
            t.localScale = new Vector3(Mathf.Sign(s.x) * size, Mathf.Sign(s.y) * size, s.z);
        }

        /// <summary>Replace the scale curves of the named layers in a clip (x keeps the layer's mirrored sign).</summary>
        static void Clip(string clipName, bool loop, params (string path, float[] times, float[] sizes)[] layers)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/CircuitCube/Animations/" + clipName + ".anim");
            if (clip == null) { Debug.LogWarning("[CircuitCube] Clip not found: " + clipName); return; }
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
                if (b.propertyName.StartsWith("m_LocalScale") || b.propertyName.StartsWith("m_LocalRotation")
                    || b.propertyName.StartsWith("localEulerAngles") || b.propertyName == "m_IsActive")
                    if (!(clipName.EndsWith("SourceIdle") && !b.propertyName.StartsWith("m_LocalScale")))
                        AnimationUtility.SetEditorCurve(clip, b, null);
            foreach (var (path, times, sizes) in layers)
            {
                var kx = new Keyframe[times.Length];
                var ky = new Keyframe[times.Length];
                var kz = new Keyframe[times.Length];
                for (int i = 0; i < times.Length; i++)
                {
                    kx[i] = new Keyframe(times[i], -sizes[i]);
                    ky[i] = new Keyframe(times[i], sizes[i]);
                    kz[i] = new Keyframe(times[i], 1f);
                }
                var cx = new AnimationCurve(kx); var cy = new AnimationCurve(ky); var cz = new AnimationCurve(kz);
                for (int i = 0; i < times.Length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(cx, i, AnimationUtility.TangentMode.ClampedAuto);
                    AnimationUtility.SetKeyRightTangentMode(cx, i, AnimationUtility.TangentMode.ClampedAuto);
                    AnimationUtility.SetKeyLeftTangentMode(cy, i, AnimationUtility.TangentMode.ClampedAuto);
                    AnimationUtility.SetKeyRightTangentMode(cy, i, AnimationUtility.TangentMode.ClampedAuto);
                }
                clip.SetCurve(path, typeof(Transform), "m_LocalScale.x", cx);
                clip.SetCurve(path, typeof(Transform), "m_LocalScale.y", cy);
                clip.SetCurve(path, typeof(Transform), "m_LocalScale.z", cz);
            }
            clip.wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever;
            EditorUtility.SetDirty(clip);
        }

        // ------------------------------------------------------------------ UI prefabs
        static Sprite S(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(V2 + "/UI/" + name + ".png");
        static Sprite Icon(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(V2 + "/UI/Icons/" + name + ".png");

        static void Role(Component c, UiRole role)
        {
            if (c == null) return;
            var r = c.GetComponent<ThemeRole>() ?? c.gameObject.AddComponent<ThemeRole>();
            r.Role = role;
        }

        static void Img(Transform root, string path, Sprite sprite, UiRole role, bool sliced = false, bool keepSprite = false)
        {
            var t = path == "" ? root : root.Find(path);
            if (t == null) { Debug.LogWarning("[CircuitCube] UI path not found: " + path); return; }
            var img = t.GetComponent<Image>();
            if (img == null) return;
            if (!keepSprite) img.sprite = sprite;
            if (sprite != null) img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            if (!sliced && sprite != null) img.preserveAspect = true;
            Role(img, role);
        }

        static void Txt(Transform root, string path, UiRole role)
        {
            var t = root.Find(path);
            if (t == null) { Debug.LogWarning("[CircuitCube] UI path not found: " + path); return; }
            var txt = t.GetComponent<Text>();
            if (txt != null) Role(txt, role);
        }

        static void Button(Transform root, string path, bool primary, string icon)
        {
            Img(root, path, S("ui_button"), primary ? UiRole.Primary : UiRole.Secondary, true);
            var on = primary ? UiRole.OnPrimary : UiRole.OnSecondary;
            if (icon != null && root.Find(path + "/Icon") != null) Img(root, path + "/Icon", Icon(icon), on);
            if (root.Find(path + "/Label") != null) Txt(root, path + "/Label", on);
        }

        static void Round(Transform root, string path, string icon)
        {
            Img(root, path, S("ui_circle"), UiRole.Round);
            Img(root, path + "/Icon", Icon(icon), UiRole.OnRound);
        }

        static void Stars(GameObject root)
        {
            foreach (var row in root.GetComponentsInChildren<StarRow>(true))
            {
                row.Full = Icon("icon_star_full");
                row.Empty = Icon("icon_star_empty");
                foreach (var s in row.Stars) if (s != null) Role(s, UiRole.Star);
            }
        }

        static void Edit(string name, System.Action<GameObject> edit)
        {
            string path = Prefabs + "/UI/" + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            edit(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        public static void StyleUI()
        {
            Edit("UI", go =>
            {
                var r = go.transform.Find("SafeArea");
                // Home
                Img(r, "Home/Logo", S("logo_mark"), UiRole.None);
                Wordmark(r.Find("Home/Logo"), r.Find("Home/Shape")?.GetComponent<Text>());
                // the V2 mark is square and the lettering sits under it: smaller mark, group name below the lettering
                var logoRt = (RectTransform)r.Find("Home/Logo");
                logoRt.sizeDelta = new Vector2(logoRt.sizeDelta.x, 330f);
                var shapeRt = (RectTransform)r.Find("Home/Shape");
                shapeRt.anchoredPosition = new Vector2(shapeRt.anchoredPosition.x, -545f);
                Txt(r, "Home/Shape", UiRole.TextMuted);
                Button(r, "Home/Play", true, "icon_play");
                Button(r, "Home/Shapes", false, "icon_levels");
                Txt(r, "Home/Stars", UiRole.TextMuted);
                // Shapes / Levels
                foreach (var screen in new[] { "Shapes", "Levels" })
                {
                    Img(r, screen + "/Shade", null, UiRole.Dim);
                    Round(r, screen + "/Back", "icon_back");
                    Txt(r, screen + "/Title", UiRole.Text);
                }
                Txt(r, "Levels/Subtitle", UiRole.TextMuted);
                // HUD
                Round(r, "Hud/Back", "icon_back");
                Button(r, "Hud/Restart", false, "icon_restart");
                Round(r, "Hud/Hint", "icon_hint");
                Img(r, "Hud/Hint/Count", S("ui_circle"), UiRole.Accent);
                Txt(r, "Hud/Hint/Count/Value", UiRole.OnPrimary);
                Txt(r, "Hud/Title", UiRole.Text);
                Txt(r, "Hud/Status", UiRole.TextMuted);
                Txt(r, "Hud/Tip", UiRole.TextMuted);
                // pop-ups
                Img(r, "Dim", null, UiRole.Overlay);
                Img(r, "ResultCard/Panel", S("ui_card"), UiRole.Card, true);
                Txt(r, "ResultCard/Panel/Body", UiRole.TextMuted);
                Button(r, "ResultCard/Panel/Next", true, "icon_next");
                Button(r, "ResultCard/Panel/Replay", false, "icon_restart");
                Button(r, "ResultCard/Panel/" + (r.Find("ResultCard/Panel/Home") != null ? "Home" : "Levels"), false,
                    r.Find("ResultCard/Panel/Home") != null ? "icon_home" : "icon_levels");
                Img(r, "TipCard/Panel", S("ui_card"), UiRole.Card, true);
                Txt(r, "TipCard/Panel/Title", UiRole.Text);
                Txt(r, "TipCard/Panel/Body", UiRole.TextMuted);
                Button(r, "TipCard/Panel/Ok", true, null);
                Stars(go);
            });
            Edit("Background", go => Backdrop(go.transform.Find("Image")));
            Edit("LevelNode", go =>
            {
                var t = go.transform;
                var node = go.GetComponent<LevelNode>();
                var sq = S("ui_square");
                node.OpenSprite = node.DoneSprite = node.LockedSprite = sq;
                var img = go.GetComponent<Image>();
                img.sprite = sq; img.type = Image.Type.Sliced;
                Img(t, "Lock", Icon("icon_lock"), UiRole.TextMuted);
                Stars(go);
            });
            Edit("ShapeCard", go =>
            {
                var t = go.transform;
                Img(t, "", S("ui_card"), UiRole.Card, true);
                Txt(t, "Title", UiRole.Text);
                Txt(t, "Blurb", UiRole.TextMuted);
                Txt(t, "Progress", UiRole.TextMuted);
                Img(t, "Track", S("ui_bar"), UiRole.Track, true);
                Img(t, "Track/FillMask/Fill", S("ui_bar"), UiRole.Accent, true);
                Img(t, "Locked/LockIcon", Icon("icon_lock"), UiRole.TextMuted);
                Txt(t, "Locked/Hint", UiRole.TextMuted);
                var card = go.GetComponent<ShapeCard>();
                card.IconIds = IconIds;
                card.IconsDark = new Sprite[IconFiles.Length];
                card.IconsLight = new Sprite[IconFiles.Length];
                for (int i = 0; i < IconFiles.Length; i++)
                {
                    card.IconsDark[i] = S("Dark/" + IconFiles[i]);
                    card.IconsLight[i] = S("Light/" + IconFiles[i]);
                }
                var icon = t.Find("Icon")?.GetComponent<Image>();
                if (icon != null) { icon.sprite = card.IconsDark[0]; icon.preserveAspect = true; }
            });
            Edit("LegendChip", go =>
            {
                var t = go.transform;
                Img(t, "", S("ui_pill"), UiRole.Chip, true);
                var dot = t.Find("Dot")?.GetComponent<Image>();
                if (dot != null) { dot.sprite = S("ui_circle"); dot.preserveAspect = true; }
                Txt(t, "Label", UiRole.Text);
            });
        }

        /// <summary>"CIRCUIT CUBE" under the logo mark (the V2 mark has no lettering; the plan's font goes here later).</summary>
        /// <summary>The space picture of the current theme behind everything, cropped to fill the screen.</summary>
        public static void Backdrop(Transform t)
        {
            if (t == null) return;
            var role = t.GetComponent<ThemeRole>();
            if (role != null) Object.DestroyImmediate(role, true);
            if (t.GetComponent<ThemeBackdrop>() == null) t.gameObject.AddComponent<ThemeBackdrop>();
            var fit = t.GetComponent<AspectRatioFitter>() ?? t.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            var img = t.GetComponent<Image>();
            img.preserveAspect = false;
            img.raycastTarget = false;
        }

        static void Wordmark(Transform logo, Text style)
        {
            if (logo == null) return;
            var t = logo.Find("Wordmark");
            Text txt;
            if (t == null)
            {
                var go = new GameObject("Wordmark", typeof(RectTransform), typeof(Text));
                go.transform.SetParent(logo, false);
                t = go.transform;
            }
            txt = t.GetComponent<Text>();
            if (style != null) txt.font = style.font;
            txt.fontStyle = FontStyle.Bold;
            txt.supportRichText = true;
            txt.text = "CIRCUIT <color=#2AA8B8>CUBE</color>";
            txt.alignment = TextAnchor.UpperCenter;
            txt.resizeTextForBestFit = true;
            txt.resizeTextMinSize = 20;
            txt.resizeTextMaxSize = 96;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            var rt = (RectTransform)t;
            rt.anchorMin = new Vector2(-0.6f, 0f);
            rt.anchorMax = new Vector2(1.6f, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -12);
            rt.sizeDelta = new Vector2(0, 96);
            Role(txt, UiRole.Text);
        }

        // ------------------------------------------------------------------ scene and player settings
        static void WireScene(GameTheme dark, GameTheme light)
        {
            var scenePath = "Assets/CircuitCube/Scenes/CircuitCube.unity";
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != scenePath) scene = EditorSceneManager.OpenScene(scenePath);
            var app = Object.FindFirstObjectByType<CircuitCubeApp>();
            if (app == null) { Debug.LogWarning("[CircuitCube] No CircuitCubeApp in " + scenePath); return; }
            app.DarkTheme = dark;
            app.LightTheme = light;
            EditorUtility.SetDirty(app);
            var cam = app.Cam != null ? app.Cam : Camera.main;
            if (cam != null) { cam.backgroundColor = dark.CameraBackground; EditorUtility.SetDirty(cam); }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void AppIcon()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(V2 + "/UI/app_icon.png");
            if (icon != null) PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });
        }
    }
}
