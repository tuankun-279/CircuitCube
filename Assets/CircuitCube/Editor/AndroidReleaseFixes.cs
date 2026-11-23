#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CircuitCube.EditorTools
{
    /// <summary>
    /// Two fixes for the Play Console checks on every Android build (found on the 1.4.0 (7) upload):
    /// <list type="bullet">
    /// <item>Native debug symbols (symbol table) go into the app bundle, so Play can read crash and ANR stacks and stops
    /// warning that the bundle "contains native code" without symbols.</item>
    /// <item>The portrait screen feature Unity declares for a portrait-locked game is marked not required. As required it
    /// keeps the game off Chromebooks, Play Games on PC and XR devices ("Required hardware feature declarations are
    /// restricting your app's availability"). The game still runs in portrait only.</item>
    /// </list>
    /// </summary>
    public sealed class AndroidReleaseFixes : IPreprocessBuildWithReport, IPostGenerateGradleAndroidProject
    {
        const string AndroidNs = "http://schemas.android.com/apk/res/android";
        static readonly string[] OptionalFeatures = { "android.hardware.screen.portrait", "android.hardware.screen.landscape" };

        public int callbackOrder => 999;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != UnityEditor.BuildTarget.Android) return;
            if (UserBuildSettings.DebugSymbols.level == Unity.Android.Types.DebugSymbolLevel.None)
                UserBuildSettings.DebugSymbols.level = Unity.Android.Types.DebugSymbolLevel.SymbolTable;
        }

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // path is the unityLibrary module; the launcher module sits next to it
            string library = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            string launcher = Path.GetFullPath(Path.Combine(path, "..", "launcher", "src", "main", "AndroidManifest.xml"));
            if (File.Exists(library)) MakeOptional(library, true);
            if (File.Exists(launcher)) MakeOptional(launcher, false);
        }

        static void MakeOptional(string file, bool addPortrait)
        {
            var doc = new XmlDocument();
            doc.Load(file);
            var root = doc.DocumentElement;
            if (root == null) return;
            bool changed = false;
            foreach (var name in OptionalFeatures)
            {
                bool found = false;
                foreach (XmlNode n in root.SelectNodes("uses-feature"))
                {
                    var e = (XmlElement)n;
                    if (e.GetAttribute("name", AndroidNs) != name) continue;
                    found = true;
                    if (e.GetAttribute("required", AndroidNs) == "false") continue;
                    e.SetAttribute("required", AndroidNs, "false");
                    changed = true;
                }
                // only the portrait feature is added when missing: Unity declares it for a portrait-locked game
                if (!found && addPortrait && name.EndsWith("portrait"))
                {
                    var e = doc.CreateElement("uses-feature");
                    e.SetAttribute("name", AndroidNs, name);
                    e.SetAttribute("required", AndroidNs, "false");
                    root.AppendChild(e);
                    changed = true;
                }
            }
            if (!changed) return;
            doc.Save(file);
            Debug.Log("[CircuitCube] Screen orientation features marked optional in " + file);
        }
    }
}
#endif
