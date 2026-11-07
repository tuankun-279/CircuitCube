using System;
using System.Reflection;
using CircuitCube.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.EditorTools
{
    /// <summary>
    /// The screens the game must look right on (plan, part 4), as Game view sizes:
    /// Tools/Circuit Cube/Screens/Add Game View Sizes adds them to the Game view's size menu (current platform group),
    /// <see cref="Select"/> switches the Game view to one, for checking each screen by hand or from a script.
    /// Unity has no public API for Game view sizes, so this goes through the editor's internal GameViewSizes types.
    /// </summary>
    public static class ScreenSizes
    {
        public struct Size
        {
            public string Name;
            public int W, H;
            public Size(string name, int w, int h) { Name = name; W = w; H = h; }
        }

        public static readonly Size[] All =
        {
            // Android phones, portrait
            new Size("CC Android 720x1280 16:9 (low end)", 720, 1280),
            new Size("CC Android 720x1600 20:9", 720, 1600),
            new Size("CC Android 1080x1920 16:9 (design)", 1080, 1920),
            new Size("CC Android 1080x2340 19.5:9", 1080, 2340),
            new Size("CC Android 1080x2400 20:9", 1080, 2400),
            new Size("CC Android 1440x3200 20:9", 1440, 3200),
            // iPhone, portrait
            new Size("CC iPhone SE 750x1334", 750, 1334),
            new Size("CC iPhone 12-14 1170x2532", 1170, 2532),
            new Size("CC iPhone 15-16 1179x2556", 1179, 2556),
            new Size("CC iPhone Pro Max 1290x2796", 1290, 2796),
            // tablets and foldables
            new Size("CC Tablet 10in 1200x1920 16:10", 1200, 1920),
            new Size("CC iPad 1536x2048 4:3", 1536, 2048),
            new Size("CC iPad Air 1640x2360", 1640, 2360),
            new Size("CC iPad Pro 12.9 2048x2732", 2048, 2732),
            new Size("CC Foldable open 1812x2176", 1812, 2176),
            // no landscape: the game is locked to portrait (Player Settings)
        };

        static readonly Type SizesType = Type.GetType("UnityEditor.GameViewSizes,UnityEditor");
        static readonly Type SizeType = Type.GetType("UnityEditor.GameViewSize,UnityEditor");
        static readonly Type SizeKind = Type.GetType("UnityEditor.GameViewSizeType,UnityEditor");
        static readonly Type GameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");

        static object Group()
        {
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(SizesType);
            var instance = singleton.GetProperty("instance").GetValue(null);
            return SizesType.GetProperty("currentGroup").GetValue(instance);
        }

        static int IndexOf(object group, Size s)
        {
            var total = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
            var get = group.GetType().GetMethod("GetGameViewSize");
            for (int i = 0; i < total; i++)
            {
                var size = get.Invoke(group, new object[] { i });
                if ((string)SizeType.GetProperty("baseText").GetValue(size) == s.Name) return i;
            }
            return -1;
        }

        [MenuItem("Tools/Circuit Cube/Screens/Add Game View Sizes")]
        public static int AddAll()
        {
            var group = Group();
            int added = 0;
            var ctor = SizeType.GetConstructor(new[] { SizeKind, typeof(int), typeof(int), typeof(string) });
            var fixedRes = Enum.Parse(SizeKind, "FixedResolution");
            foreach (var s in All)
            {
                if (IndexOf(group, s) >= 0) continue;
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { ctor.Invoke(new object[] { fixedRes, s.W, s.H, s.Name }) });
                added++;
            }
            if (added > 0) SizesType.GetMethod("SaveToHDD")?.Invoke(typeof(ScriptableSingleton<>).MakeGenericType(SizesType).GetProperty("instance").GetValue(null), null);
            Debug.Log("[CircuitCube] Game view sizes: " + added + " added, " + (All.Length - added) + " already there.");
            return added;
        }

        /// <summary>
        /// Layout rules for these screens, set on the prefabs (no layout code at runtime): every canvas scales by width
        /// on tall phones and by height on wider screens (<see cref="ResponsiveScaler"/>), the UI column stays at most
        /// 1080 units wide and centred (<see cref="SafeAreaFitter.MaxWidth"/>), and the dim overlay still covers the
        /// whole screen (<see cref="FullScreenRect"/>).
        /// </summary>
        [MenuItem("Tools/Circuit Cube/Screens/Apply Layout Rules")]
        public static void ApplyLayoutRules()
        {
            foreach (var name in new[] { "UI", "Background", "DebugMenu" })
            {
                string path = "Assets/CircuitCube/Prefabs/UI/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var scaler in root.GetComponentsInChildren<CanvasScaler>(true))
                        if (scaler.GetComponent<ResponsiveScaler>() == null) scaler.gameObject.AddComponent<ResponsiveScaler>();
                    foreach (var safe in root.GetComponentsInChildren<SafeAreaFitter>(true))
                    {
                        safe.MaxWidth = 1080f;
                        var dim = safe.transform.Find("Dim");
                        if (dim != null && dim.GetComponent<FullScreenRect>() == null) dim.gameObject.AddComponent<FullScreenRect>();
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Debug.Log("[CircuitCube] Layout rules applied to the UI, Background and DebugMenu prefabs.");
        }

        /// <summary>Switch the Game view to one of <see cref="All"/>; returns false when the size is missing.</summary>
        public static bool Select(int index)
        {
            var group = Group();
            int i = IndexOf(group, All[index]);
            if (i < 0) { AddAll(); i = IndexOf(group, All[index]); }
            if (i < 0) return false;
            var view = EditorWindow.GetWindow(GameViewType, false, null, false);
            var pick = GameViewType.GetMethod("SizeSelectionCallback", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (pick != null) pick.Invoke(view, new object[] { i, null });
            else GameViewType.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(view, i);
            view.Repaint();
            return true;
        }
    }
}
