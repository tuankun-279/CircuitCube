using System;
using UnityEngine;
#if CC_CRASHLYTICS && !UNITY_EDITOR
using Firebase;
using Firebase.Crashlytics;
using Firebase.Extensions;
#endif

namespace CircuitCube.Game
{
    /// <summary>
    /// Firebase Crashlytics: crashes and uncaught exceptions from players' phones show up in the Firebase console
    /// (Crashlytics), with the shape, level and app version as keys. Built only when the Crashlytics package is in the
    /// project (define CC_CRASHLYTICS from the package); in the editor and without the package it does nothing.
    /// </summary>
    public static class CrashReports
    {
        static bool _started, _ready;

        public static void Init()
        {
            if (_started) return;
            _started = true;
#if CC_CRASHLYTICS && !UNITY_EDITOR
            try
            {
                FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted || task.Result != DependencyStatus.Available) { Debug.LogWarning("[Crashlytics] off: " + (task.IsFaulted ? task.Exception?.Message : task.Result.ToString())); return; }
                    try
                    {
                        Crashlytics.ReportUncaughtExceptionsAsFatal = true;
                        Crashlytics.SetCustomKey("content", GameData.Manifest != null ? GameData.Manifest.contentVersion.ToString() : "0");
                        _ready = true;
                    }
                    catch (Exception e) { Debug.LogWarning("[Crashlytics] off: " + e.Message); }
                });
            }
            catch (Exception e) { Debug.LogWarning("[Crashlytics] off: " + e.Message); }
#endif
        }

        /// <summary>Where the player is, sent with any crash that follows.</summary>
        public static void SetLevel(string shape, int level)
        {
#if CC_CRASHLYTICS && !UNITY_EDITOR
            if (!_ready) return;
            try { Crashlytics.SetCustomKey("shape", shape); Crashlytics.SetCustomKey("level", level.ToString()); }
            catch (Exception) { }
#endif
        }

        /// <summary>A caught error worth seeing in the console (not a crash).</summary>
        public static void Report(Exception e)
        {
#if CC_CRASHLYTICS && !UNITY_EDITOR
            if (!_ready || e == null) return;
            try { Crashlytics.LogException(e); } catch (Exception) { }
#endif
        }
    }
}
