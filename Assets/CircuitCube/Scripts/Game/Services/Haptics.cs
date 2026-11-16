using System;
using UnityEngine;

namespace CircuitCube.Game
{
    /// <summary>
    /// Short vibrations, gated by the Vibration switch in Settings: a tick when a tile turns, a tap when a lamp lights,
    /// a longer buzz on a win and a double tick when a locked tile is pressed. Android uses the system vibrator with
    /// one-shot effects (API 26+ with strength, older phones by length only); elsewhere it does nothing. Any failure turns haptics off for the session.
    /// </summary>
    public static class Haptics
    {
        public enum Kind { Turn, Lamp, Win, Blocked }

        /// <summary>Length (ms) and strength (1-255) of each kind; Blocked plays twice.</summary>
        static readonly long[] Ms = { 12, 28, 90, 14 };
        static readonly int[] Strength = { 60, 140, 255, 90 };

        public static bool Enabled => CubeSave.Settings.vibration;

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject _vibrator;
        static AndroidJavaClass _effect;
        static bool _failed, _ready, _oneShot;
        static readonly object[] OneArg = new object[1];
        static readonly object[] TwoArgs = new object[2];
        static readonly object[] Wave = new object[2];

        static void Ensure()
        {
            if (_ready || _failed) return;
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    _oneShot = version.GetStatic<int>("SDK_INT") >= 26;
                if (_oneShot) _effect = new AndroidJavaClass("android.os.VibrationEffect");
                _ready = _vibrator != null && _vibrator.Call<bool>("hasVibrator");
                if (!_ready) _failed = true;
            }
            catch (Exception e) { Fail(e); }
        }

        static void Fail(Exception e) { _failed = true; Debug.LogWarning("[Haptics] off: " + e.Message); }
#endif

        public static void Play(Kind kind)
        {
            if (!Enabled) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            Ensure();
            if (_failed) return;
            int k = (int)kind;
            try
            {
                if (kind == Kind.Blocked && _oneShot)
                {
                    // two ticks: off, on, off, on
                    Wave[0] = new long[] { 0, Ms[k], 60, Ms[k] };
                    Wave[1] = -1;
                    using (var e = _effect.CallStatic<AndroidJavaObject>("createWaveform", Wave)) { OneArg[0] = e; _vibrator.Call("vibrate", OneArg); }
                }
                else if (_oneShot)
                {
                    TwoArgs[0] = Ms[k];
                    TwoArgs[1] = Strength[k];
                    using (var e = _effect.CallStatic<AndroidJavaObject>("createOneShot", TwoArgs)) { OneArg[0] = e; _vibrator.Call("vibrate", OneArg); }
                }
                else
                {
                    OneArg[0] = Ms[k];
                    _vibrator.Call("vibrate", OneArg);
                }
            }
            catch (Exception e) { Fail(e); }
#elif UNITY_IOS
            if (kind == Kind.Win) Handheld.Vibrate();
#endif
        }

#if UNITY_ANDROID
        // never called: keeps Unity adding the VIBRATE permission to the Android manifest
        static void KeepPermission() => Handheld.Vibrate();
#endif
    }
}
