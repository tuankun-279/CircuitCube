using System.Collections.Generic;
using System.IO;
using CircuitCube.Game;
using UnityEditor;
using UnityEngine;

namespace CircuitCube.EditorTools
{
    /// <summary>
    /// Level data (format v3) in Assets/CircuitCube/Resources/CircuitCubeData. Levels are made by the monthly AI task and
    /// published to content/ (the files the game downloads); these menus only check the data, clear the download cache
    /// and copy the latest publish into the build as its offline fallback.
    /// </summary>
    public static class DataTools
    {
        public const string DataFolder = "Assets/CircuitCube/Resources/CircuitCubeData";
        const string Menu = "Tools/Circuit Cube/Data/";

        [MenuItem(Menu + "Validate")]
        public static void ValidateMenu()
        {
            var problems = DataExport.Validate();
            Log(problems);
            EditorUtility.DisplayDialog("Circuit Cube data", problems.Count == 0 ? "All levels are good." : problems.Count + " problems, see Console.", "OK");
        }

        [MenuItem(Menu + "Clear Download Cache")]
        public static void ClearCache()
        {
            if (Directory.Exists(GameData.CacheRoot)) Directory.Delete(GameData.CacheRoot, true);
            GameData.Reload();
            Debug.Log("[CircuitCube] Download cache cleared.");
        }

        static void Log(List<string> problems)
        {
            if (problems.Count == 0) { Debug.Log("[CircuitCube] Data validate: all good."); return; }
            foreach (var p in problems) Debug.LogWarning("[CircuitCube] " + p);
        }

        /// <summary>The levels the monthly AI task published to the repo (content/ next to Assets, the files the game downloads).</summary>
        public const string ContentFolder = "content";

        [MenuItem(Menu + "Copy Published Content into the Build")]
        public static void CopyContentMenu()
        {
            if (!Directory.Exists(ContentFolder)) { EditorUtility.DisplayDialog("Circuit Cube data", "No content/ folder: pull the repo after the monthly level task has published.", "OK"); return; }
            int n = CopyContent();
            EditorUtility.DisplayDialog("Circuit Cube data", "Copied " + n + " files from content/ into " + DataFolder + ". The build now ships this publish; players still download newer ones.", "OK");
        }

        /// <summary>Replace the data shipped in the build with the repo's published content. Returns the file count.</summary>
        public static int CopyContent()
        {
            var files = new Dictionary<string, string>();
            foreach (var f in Directory.GetFiles(ContentFolder, "*.json", SearchOption.AllDirectories))
                files[f.Substring(ContentFolder.Length + 1).Replace('\\', '/')] = File.ReadAllText(f);
            WriteAll(files, true);
            return files.Count;
        }

        static void WriteAll(Dictionary<string, string> files, bool replace)
        {
            if (replace && Directory.Exists(DataFolder))
            {
                foreach (var f in Directory.GetFiles(DataFolder, "*.json", SearchOption.AllDirectories)) File.Delete(f);
                foreach (var f in Directory.GetFiles(DataFolder, "*.json.meta", SearchOption.AllDirectories)) File.Delete(f);
            }
            foreach (var kv in files)
            {
                string p = Path.Combine(DataFolder, kv.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(p));
                File.WriteAllText(p, kv.Value);
            }
            AssetDatabase.Refresh();
            GameData.Reload();
        }
    }
}
