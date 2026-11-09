using System.Collections.Generic;
using CircuitCube.Game;
using CircuitCube.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.EditorTools
{
    /// <summary>
    /// Tools/Circuit Cube/Audio/Build Audio: import settings for Audio/Sfx and Audio/Music, the SoundBank asset, the
    /// Audio prefab (GameAudio, two music sources, six effect sources) placed in the scene, and a UiClickSound on every
    /// button of the UI prefabs (not Hint and Undo, which have their own sounds). Safe to run again.
    /// Sources: ArtRef/audio/CREDITS.md.
    /// </summary>
    public static class AudioSetup
    {
        const string Root = "Assets/CircuitCube/Audio";
        const string BankPath = Root + "/SoundBank.asset";
        const string PrefabPath = "Assets/CircuitCube/Prefabs/Audio.prefab";

        // file name, volume, pitch jitter, min gap
        static readonly (Sfx id, string file, float volume, float jitter, float gap)[] Effects =
        {
            (Sfx.Rotate, "rotate", 1f, 0.05f, 0.03f),
            (Sfx.Lamp, "lamp", 1f, 0f, 0.05f),
            (Sfx.Win, "win", 1f, 0f, 0.3f),
            (Sfx.Star, "star", 1f, 0f, 0.05f),
            (Sfx.Unlock, "unlock", 1f, 0f, 0.3f),
            (Sfx.Hint, "hint", 1f, 0f, 0.1f),
            (Sfx.Undo, "undo", 1f, 0.04f, 0.03f),
            (Sfx.Blocked, "blocked", 1f, 0f, 0.15f),
            (Sfx.Button, "button", 1f, 0.03f, 0.03f),
            (Sfx.PanelOpen, "panel_open", 1f, 0f, 0.1f),
            (Sfx.PanelClose, "panel_close", 1f, 0f, 0.1f),
        };

        [MenuItem("Tools/Circuit Cube/Audio/Build Audio")]
        public static void Build()
        {
            ImportSettings();
            var bank = BuildBank();
            var prefab = BuildPrefab(bank);
            AddClickSounds();
            PlaceInScene(prefab);
            AssetDatabase.SaveAssets();
            Debug.Log("[CircuitCube] Audio built: " + bank.Sounds.Length + " effects, 2 music tracks.");
        }

        static void ImportSettings()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = (AudioImporter)AssetImporter.GetAtPath(path);
                bool music = path.Contains("/Music/");
                var s = imp.defaultSampleSettings;
                if (music)
                {
                    // long loops: stream from disk, Vorbis, decoded in the background
                    s.loadType = AudioClipLoadType.Streaming;
                    s.compressionFormat = AudioCompressionFormat.Vorbis;
                    s.quality = 0.6f;
                    imp.loadInBackground = true;
                    imp.forceToMono = false;
                }
                else
                {
                    // short effects: kept decoded so they start with no delay
                    s.loadType = AudioClipLoadType.DecompressOnLoad;
                    s.compressionFormat = AudioCompressionFormat.ADPCM;
                    imp.forceToMono = true;
                    imp.loadInBackground = false;
                }
                s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                imp.defaultSampleSettings = s;
                imp.SaveAndReimport();
            }
        }

        static SoundBank BuildBank()
        {
            var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
            if (bank == null) { bank = ScriptableObject.CreateInstance<SoundBank>(); AssetDatabase.CreateAsset(bank, BankPath); }
            var list = new List<SfxEntry>();
            foreach (var e in Effects)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Sfx/" + e.file + ".wav");
                if (clip == null) Debug.LogWarning("[CircuitCube] Missing sound " + e.file);
                var old = bank.Get(e.id);
                list.Add(new SfxEntry { Id = e.id, Clip = clip, Volume = old != null ? old.Volume : e.volume, PitchJitter = e.jitter, MinGap = e.gap });
            }
            bank.Sounds = list.ToArray();
            bank.MenuMusic = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Music/menu_outer_space.ogg");
            bank.PlayMusic = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Music/play_cozy_puzzle.ogg");
            EditorUtility.SetDirty(bank);
            return bank;
        }

        static GameObject BuildPrefab(SoundBank bank)
        {
            var go = new GameObject("Audio");
            var audio = go.AddComponent<GameAudio>();
            audio.Bank = bank;
            AudioSource Source(string name)
            {
                var child = new GameObject(name);
                child.transform.SetParent(go.transform, false);
                var s = child.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                return s;
            }
            audio.MusicA = Source("MusicA");
            audio.MusicB = Source("MusicB");
            audio.MusicA.priority = audio.MusicB.priority = 0;
            audio.Effects = new AudioSource[6];
            for (int i = 0; i < audio.Effects.Length; i++) audio.Effects[i] = Source("Effect" + i);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void AddClickSounds()
        {
            foreach (var path in new[] { "Assets/CircuitCube/Prefabs/UI/UI.prefab", "Assets/CircuitCube/Prefabs/UI/ShapeCard.prefab", "Assets/CircuitCube/Prefabs/UI/LevelNode.prefab" })
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var skip = new HashSet<Button>();
                    var hud = root.GetComponentInChildren<HudScreen>(true);
                    if (hud != null) { skip.Add(hud.HintButton); skip.Add(hud.UndoButton); }
                    foreach (var b in root.GetComponentsInChildren<Button>(true))
                    {
                        var has = b.GetComponent<UiClickSound>();
                        if (skip.Contains(b)) { if (has != null) Object.DestroyImmediate(has, true); continue; }
                        if (has == null) b.gameObject.AddComponent<UiClickSound>();
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        static void PlaceInScene(GameObject prefab)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != CircuitCubeSetup.ScenePath) scene = EditorSceneManager.OpenScene(CircuitCubeSetup.ScenePath);
            var existing = Object.FindFirstObjectByType<GameAudio>(FindObjectsInactive.Include);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            PrefabUtility.InstantiatePrefab(prefab, scene);
            // the scene had no listener, so nothing was heard: it goes on the game camera
            if (Object.FindFirstObjectByType<AudioListener>(FindObjectsInactive.Include) == null)
            {
                var app = Object.FindFirstObjectByType<CircuitCubeApp>();
                var cam = app != null && app.Cam != null ? app.Cam : Camera.main;
                if (cam != null) cam.gameObject.AddComponent<AudioListener>();
                else Debug.LogWarning("[CircuitCube] No camera for the AudioListener.");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
