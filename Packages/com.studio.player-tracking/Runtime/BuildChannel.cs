using System;
using UnityEngine;

namespace Studio.Tracking
{
    /// <summary>
    /// The channel of the running build (<see cref="ChannelRules"/>): editor, dev, testlab, sideload or play.
    /// Read once and kept. On Android the installer comes from the PackageManager and test-lab devices from the
    /// system setting "firebase.test.lab"; other platforms report sideload for a release build.
    /// </summary>
    public static class BuildChannel
    {
        static string _current, _installer;

        /// <summary>The channel name, for analytics and the servers.</summary>
        public static string Current
        {
            get
            {
                if (_current == null)
                    _current = ChannelRules.Pick(Application.isEditor, Debug.isDebugBuild, IsTestLab(), Installer);
                return _current;
            }
        }

        /// <summary>The package that installed the app ("" when unknown or not on Android).</summary>
        public static string Installer
        {
            get
            {
                if (_installer == null) _installer = ReadInstaller() ?? "";
                return _installer;
            }
        }

        /// <summary>A real player: installed by Google Play, not a test device, not a development build.</summary>
        public static bool CountsAsPlayer => ChannelRules.CountsAsPlayer(Current);

        /// <summary>The Unity Gaming Services environment for this build.</summary>
        public static string ServicesEnvironment => ChannelRules.Environment(Current);

        /// <summary>For tests: force a channel (null goes back to the detected one).</summary>
        public static void Override(string channel) => _current = channel;

        static string ReadInstaller()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var pm = activity.Call<AndroidJavaObject>("getPackageManager"))
                {
                    string package = activity.Call<string>("getPackageName");
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    {
                        if (version.GetStatic<int>("SDK_INT") >= 30)
                            using (var info = pm.Call<AndroidJavaObject>("getInstallSourceInfo", package))
                                return info.Call<string>("getInstallingPackageName");
                    }
                    return pm.Call<string>("getInstallerPackageName", package);
                }
            }
            catch (Exception e) { Debug.LogWarning("[BuildChannel] installer unknown: " + e.Message); }
#endif
            return null;
        }

        static bool IsTestLab()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var resolver = activity.Call<AndroidJavaObject>("getContentResolver"))
                using (var system = new AndroidJavaClass("android.provider.Settings$System"))
                    return system.CallStatic<string>("getString", resolver, "firebase.test.lab") == "true";
            }
            catch (Exception e) { Debug.LogWarning("[BuildChannel] test lab check: " + e.Message); }
#endif
            return false;
        }
    }
}
