using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CircuitCube.EditorTools
{
    /// <summary>
    /// Stops an Android build whose Play Games web client id is empty. Without it the plugin throws when the game asks
    /// for the server sign-in code, so players signed in to Play Games never reach Unity Gaming Services: no
    /// leaderboards, no daily count. 1.4.0 (7) shipped like that. The value is read from the asset as the editor holds
    /// it, because that is what goes into the build, whatever the file on disk says.
    /// </summary>
    public sealed class PlayGamesBuildCheck : IPreprocessBuildWithReport
    {
        const string SettingsAsset = "Assets/GooglePlayGames/Resources/PlayGamesSettings.asset";

        // late, so it sees what earlier build steps wrote
        public int callbackOrder => 10000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(SettingsAsset);
            var id = asset != null ? new SerializedObject(asset).FindProperty("mWebClientId") : null;
            if (id == null || string.IsNullOrEmpty(id.stringValue))
                throw new BuildFailedException("Play Games web client id is empty in " + SettingsAsset
                    + ". Set it in Window > Google Play Games > Setup > Android setup (value in docs/console-setup/unity-dashboard.md).");
        }
    }
}
