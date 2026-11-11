using System;
using System.Collections.Generic;
using Firebase;
using Firebase.Analytics;
using Firebase.Extensions;
using UnityEngine;

namespace CircuitCube.Game
{
    /// <summary>
    /// Firebase Analytics events. Events logged before Firebase is up wait in a short queue. Without a Firebase
    /// config (google-services.json) or in the editor, events only go to the console when <see cref="EchoToConsole"/>
    /// is on, and the game carries on as normal. Consent: Analytics reads the choice the player made in Google's
    /// consent form (UMP writes it in the IAB TCF format that the Analytics SDK reads by itself).
    /// </summary>
    public static class GameAnalytics
    {
        /// <summary>Print every event to the console (editor and development builds).</summary>
        public static bool EchoToConsole = Application.isEditor;

        const int MaxQueued = 50;
        static readonly List<(string name, Parameter[] args)> Queue = new List<(string, Parameter[])>();
        static bool _initStarted, _ready, _failed;

        public static void Init()
        {
            if (_initStarted || Application.isEditor) return;   // the editor has no mobile Analytics backend
            _initStarted = true;
            try
            {
                FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted || task.Result != DependencyStatus.Available)
                    {
                        Fail("dependencies " + (task.IsFaulted ? task.Exception?.Message : task.Result.ToString()));
                        return;
                    }
                    try
                    {
                        FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
                        _ready = true;
                        foreach (var e in Queue) FirebaseAnalytics.LogEvent(e.name, e.args);
                        Queue.Clear();
                    }
                    catch (Exception e) { Fail(e.Message); }
                });
            }
            catch (Exception e) { Fail(e.Message); }
        }

        static void Fail(string why)
        {
            _failed = true;
            Queue.Clear();
            Debug.LogWarning("[Analytics] off: " + why);
        }

        /// <summary>Log an event. Values may be string, int, long, float, double or bool.</summary>
        public static void Log(string name, params (string key, object value)[] args)
        {
            if (EchoToConsole) Debug.Log("[Analytics] " + name + Describe(args));
            if (_failed || Application.isEditor) return;
            var ps = new Parameter[args.Length];
            for (int i = 0; i < args.Length; i++) ps[i] = ToParameter(args[i].key, args[i].value);
            if (!_ready)
            {
                if (Queue.Count < MaxQueued) Queue.Add((name, ps));
                return;
            }
            try { FirebaseAnalytics.LogEvent(name, ps); }
            catch (Exception e) { Fail(e.Message); }
        }

        static Parameter ToParameter(string key, object value)
        {
            switch (value)
            {
                case int i: return new Parameter(key, i);
                case long l: return new Parameter(key, l);
                case float f: return new Parameter(key, f);
                case double d: return new Parameter(key, d);
                case bool b: return new Parameter(key, b ? 1L : 0L);
                default: return new Parameter(key, value != null ? value.ToString() : "");
            }
        }

        static string Describe((string key, object value)[] args)
        {
            if (args.Length == 0) return "";
            var sb = new System.Text.StringBuilder(" {");
            for (int i = 0; i < args.Length; i++) sb.Append(i > 0 ? ", " : "").Append(args[i].key).Append('=').Append(args[i].value);
            return sb.Append('}').ToString();
        }
    }
}
