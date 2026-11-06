using CircuitCube.Core;
using CircuitCube.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CircuitCube.EditorTools
{
    /// <summary>Editor shortcuts. The scene, prefabs and animations are assets; edit them directly in the editor.</summary>
    public static class CircuitCubeSetup
    {
        public const string ScenePath = "Assets/CircuitCube/Scenes/CircuitCube.unity";

        [MenuItem("Tools/Circuit Cube/Open Game Scene")]
        public static void OpenScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Tools/Circuit Cube/Reset Saved Progress")]
        public static void ResetProgress()
        {
            CubeSave.ResetAll();
            Debug.Log("[CircuitCube] Saved progress cleared.");
        }

        [MenuItem("Tools/Circuit Cube/Unlock All Shapes (testing)")]
        public static void UnlockAll()
        {
            GameData.Reload();
            for (int g = 0; g < GameData.GroupCount; g++) if (!GameData.Group(g).endless) CubeSave.UnlockUpTo(g, GameData.LevelCount(g));
            for (int g = 0; g < GameData.GroupCount; g++) CubeSave.GroupOpen(g);
            Debug.Log("[CircuitCube] Every shape group is open (every level of each group marked beaten).");
        }
    }
}
