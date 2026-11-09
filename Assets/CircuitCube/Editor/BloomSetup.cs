using CircuitCube.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CircuitCube.EditorTools
{
    /// <summary>
    /// Tools/Circuit Cube/Board/Build Bloom: the bloom profile (Themes/BloomProfile.asset) and a global Volume "Bloom"
    /// in the scene, wired to CircuitCubeApp.BloomVolume. The app turns it on in the dark theme only. Tune the look in
    /// the profile asset; safe to run again (an edited profile keeps its values).
    /// </summary>
    public static class BloomSetup
    {
        const string ProfilePath = "Assets/CircuitCube/Themes/BloomProfile.asset";

        [MenuItem("Tools/Circuit Cube/Board/Build Bloom")]
        public static void Build()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
                var bloom = profile.Add<Bloom>(true);
                // values approved from the 9 Nov demo: neon wires get a soft halo, lamps spill onto their neighbours
                bloom.threshold.Override(0.75f);
                bloom.intensity.Override(1.4f);
                bloom.scatter.Override(0.65f);
                bloom.highQualityFiltering.Override(false);
                bloom.downscale.Override(BloomDownscaleMode.Quarter);   // cheaper on phones
                bloom.maxIterations.Override(5);
                AssetDatabase.AddObjectToAsset(bloom, profile);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != CircuitCubeSetup.ScenePath) scene = EditorSceneManager.OpenScene(CircuitCubeSetup.ScenePath);
            var app = Object.FindFirstObjectByType<CircuitCubeApp>();
            if (app == null) { Debug.LogWarning("[CircuitCube] No CircuitCubeApp in the scene."); return; }
            var volume = app.BloomVolume;
            if (volume == null)
            {
                var go = new GameObject("Bloom");
                volume = go.AddComponent<Volume>();
            }
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = profile;
            volume.enabled = false;   // the app decides at start
            app.BloomVolume = volume;
            EditorUtility.SetDirty(volume);
            EditorUtility.SetDirty(app);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[CircuitCube] Bloom built (dark theme only).");
        }
    }
}
