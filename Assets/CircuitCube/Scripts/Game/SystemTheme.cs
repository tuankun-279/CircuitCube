using UnityEngine;

namespace CircuitCube.Game
{
    /// <summary>
    /// Reads the phone's light/dark setting for the "System" theme choice. Android reads the night bits of the
    /// current configuration (Android 10+ has a dark theme switch; older phones report "not night", so light).
    /// Other platforms have no setting the game can read without a native plugin and use dark.
    /// </summary>
    public static class SystemTheme
    {
        const int UiModeNightMask = 0x30, UiModeNightYes = 0x20;

        public static bool IsDark()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var resources = activity.Call<AndroidJavaObject>("getResources"))
                using (var config = resources.Call<AndroidJavaObject>("getConfiguration"))
                    return (config.Get<int>("uiMode") & UiModeNightMask) == UiModeNightYes;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[CircuitCube] Could not read the system theme: " + e.Message);
                return true;
            }
#else
            return true;
#endif
        }
    }
}
