using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using CircuitCube.Game;
using UnityEditor;
using UnityEngine;

namespace CircuitCube.EditorTools
{
    /// <summary>
    /// Level data (format v3) in Assets/CircuitCube/Resources/CircuitCubeData. The zip layout is the CDN layout, so a
    /// zip from the web config tool imports as-is and an exported zip uploads as-is.
    /// </summary>
    public static class DataTools
    {
        public const string DataFolder = "Assets/CircuitCube/Resources/CircuitCubeData";
        const string Menu = "Tools/Circuit Cube/Data/";

        [MenuItem(Menu + "Export Sample Data (10 per group)")]
        public static void ExportSampleMenu()
        {
            if (Directory.Exists(DataFolder) && !EditorUtility.DisplayDialog("Circuit Cube data",
                    "Replace the data in " + DataFolder + " with 10 generated levels per group?", "Replace", "Cancel")) return;
            int n = ExportSample(10);
            EditorUtility.DisplayDialog("Circuit Cube data", "Wrote " + n + " files to " + DataFolder + ".", "OK");
        }

        /// <summary>Writes the generator's first N levels of every group as data files. Returns the file count.</summary>
        public static int ExportSample(int perGroup)
        {
            var files = DataExport.FromGenerator(perGroup);
            WriteAll(files, true);
            return files.Count;
        }

        [MenuItem(Menu + "Import Data Zip…")]
        public static void ImportZipMenu()
        {
            string path = EditorUtility.OpenFilePanel("Import level data zip", "", "zip");
            if (string.IsNullOrEmpty(path)) return;
            int n = ImportZip(path, true);
            var problems = DataExport.Validate();
            EditorUtility.DisplayDialog("Circuit Cube data", "Imported " + n + " files.\n" +
                (problems.Count == 0 ? "Validate: all good." : "Validate found " + problems.Count + " problems (see Console)."), "OK");
            Log(problems);
        }

        /// <summary>Copies every .json inside the zip (manifest.json may sit in a sub-folder) into the data folder.</summary>
        public static int ImportZip(string zipPath, bool replace)
        {
            var files = new Dictionary<string, string>();
            using (var zip = ZipFile.OpenRead(zipPath))
            {
                string prefix = null;
                foreach (var e in zip.Entries)
                    if (e.Name == "manifest.json") { prefix = e.FullName.Substring(0, e.FullName.Length - e.Name.Length); break; }
                if (prefix == null) throw new IOException("The zip has no manifest.json");
                foreach (var e in zip.Entries)
                {
                    if (!e.FullName.StartsWith(prefix) || !e.Name.EndsWith(".json")) continue;
                    string rel = e.FullName.Substring(prefix.Length).Replace('\\', '/');
                    if (rel.Contains("..")) continue;
                    using (var r = new StreamReader(e.Open())) files[rel] = r.ReadToEnd();
                }
            }
            WriteAll(files, replace);
            return files.Count;
        }

        [MenuItem(Menu + "Export Data Zip…")]
        public static void ExportZipMenu()
        {
            string path = EditorUtility.SaveFilePanel("Export level data zip", "", "circuitcube-data.zip", "zip");
            if (string.IsNullOrEmpty(path)) return;
            int n = ExportZip(path);
            EditorUtility.RevealInFinder(path);
            Debug.Log("[CircuitCube] Exported " + n + " data files to " + path);
        }

        public static int ExportZip(string zipPath)
        {
            if (!Directory.Exists(DataFolder)) throw new IOException("No data in " + DataFolder);
            if (File.Exists(zipPath)) File.Delete(zipPath);
            int n = 0;
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                foreach (var f in Directory.GetFiles(DataFolder, "*.json", SearchOption.AllDirectories))
                {
                    string rel = f.Substring(DataFolder.Length + 1).Replace('\\', '/');
                    zip.CreateEntryFromFile(f, rel);
                    n++;
                }
            return n;
        }

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
