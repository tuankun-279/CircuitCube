using UnityEngine;

namespace CircuitCube.Game
{
    /// <summary>
    /// Whether the game runs on a PC Android emulator (LDPlayer, BlueStacks, MuMu, Nox, the SDK emulator...), where a
    /// mouse stands in for fingers and a pinch is hard to do. Read once from the Android build properties.
    /// </summary>
    public static class Emulator
    {
        static int _detected = -1;

        public static bool Detected
        {
            get
            {
                if (_detected < 0) _detected = Detect() ? 1 : 0;
                return _detected == 1;
            }
        }

        /// <summary>Zoom buttons in a level: the Settings switch, or on by itself on an emulator.</summary>
        public static bool ZoomButtonsOn
        {
            get
            {
                int s = CubeSave.Settings.zoomButtons;
                return s < 0 ? Detected : s == 1;
            }
        }

        static bool Detect()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var build = new AndroidJavaClass("android.os.Build"))
                {
                    string fingerprint = build.GetStatic<string>("FINGERPRINT") ?? "";
                    string model = build.GetStatic<string>("MODEL") ?? "";
                    string brand = build.GetStatic<string>("BRAND") ?? "";
                    string hardware = build.GetStatic<string>("HARDWARE") ?? "";
                    string product = build.GetStatic<string>("PRODUCT") ?? "";
                    string manufacturer = build.GetStatic<string>("MANUFACTURER") ?? "";
                    string all = (fingerprint + "|" + model + "|" + brand + "|" + hardware + "|" + product + "|" + manufacturer).ToLowerInvariant();
                    foreach (var mark in new[] { "generic", "emulator", "sdk_gphone", "goldfish", "ranchu", "vbox", "ttvm", "nox", "bluestacks", "ldplayer", "mumu", "genymotion" })
                        if (all.Contains(mark)) return true;
                    // phones with an x86 processor are all but gone: such a device is a PC emulator
                    string[] abis = build.GetStatic<string[]>("SUPPORTED_ABIS");
                    if (abis != null && abis.Length > 0 && abis[0].StartsWith("x86")) return true;
                }
            }
            catch (System.Exception) { }
#endif
            return false;
        }
    }
}
