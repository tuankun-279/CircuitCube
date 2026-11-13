using System;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Google.Play.AppUpdate;
using Google.Play.Common;
#endif

namespace CircuitCube.Game
{
    /// <summary>
    /// Google Play in-app updates. At start the game asks Play whether a newer version is out:
    /// <list type="bullet">
    /// <item>installed version code below the manifest's <see cref="GameRules.updateBelow"/>: the full-screen
    /// Play update (Immediate); cancelled, it comes back the next time the game returns to the front;</item>
    /// <item>otherwise at most once a day the Play prompt to download in the background (Flexible); once downloaded
    /// the game offers a restart, and installs on its own the next time it goes to the background.</item>
    /// </list>
    /// Works only for a build installed from Google Play (testing tracks too); in the editor and in sideloaded builds
    /// it does nothing. Every failure is quiet: the game carries on.
    /// </summary>
    public static class AppUpdates
    {
        public enum Mode { None, Immediate, Flexible }

        /// <summary>The flexible update is downloaded and waits for a restart.</summary>
        public static bool ReadyToInstall { get; private set; }
        /// <summary>Raised once when a flexible update has finished downloading.</summary>
        public static event Action Downloaded;

        const string LastPromptKey = "cc.updatePromptDay";
        static int _forceBelow;
        static bool _checking, _forcedPending;

        /// <summary>
        /// Which flow to start. <paramref name="installed"/> below <paramref name="forceBelow"/> forces the immediate
        /// one; a flexible prompt is offered once a day. Pure, for the tests.
        /// </summary>
        public static Mode Decide(int installed, int available, int forceBelow, bool immediateAllowed, bool flexibleAllowed, bool promptedToday)
        {
            if (available <= installed) return Mode.None;
            if (forceBelow > 0 && installed < forceBelow && immediateAllowed) return Mode.Immediate;
            if (flexibleAllowed && !promptedToday) return Mode.Flexible;
            return Mode.None;
        }

        static int Today => (int)(DateTime.UtcNow - new DateTime(2026, 1, 1)).TotalDays;

        static bool PromptedToday
        {
            get { try { return PlayerPrefs.GetInt(LastPromptKey, -1) == Today; } catch (Exception) { return false; } }
        }

        /// <summary>Ask Play once at start (and again when the game comes back while a forced update is pending).</summary>
        public static void Check(int forceBelow)
        {
            _forceBelow = forceBelow;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_checking || ReadyToInstall) return;
            _checking = true;
            try { Poll(Manager.GetAppUpdateInfo(), OnInfo); }
            catch (Exception e) { Fail("check", e.Message); }
#endif
        }

        /// <summary>The game is back in front: a forced update that was cancelled asks again.</summary>
        public static void OnResume()
        {
            if (_forcedPending) Check(_forceBelow);
        }

        /// <summary>The game went to the background: a downloaded flexible update installs now, without a prompt.</summary>
        public static void OnPause()
        {
            if (ReadyToInstall) Complete();
        }

        /// <summary>Install the downloaded update; Android restarts the game.</summary>
        public static void Complete()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                GameAnalytics.Log("update_install");
                Manager.CompleteUpdate();
            }
            catch (Exception e) { Fail("install", e.Message); }
#endif
        }

        /// <summary>Installed version code of this build (0 where unknown).</summary>
        public static int InstalledVersionCode
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                try
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var pm = activity.Call<AndroidJavaObject>("getPackageManager"))
                    using (var info = pm.Call<AndroidJavaObject>("getPackageInfo", activity.Call<string>("getPackageName"), 0))
                        return info.Get<int>("versionCode");
                }
                catch (Exception) { return 0; }
#else
                return 0;
#endif
            }
        }

        static void Fail(string step, string why)
        {
            _checking = false;
            Debug.LogWarning("[Update] " + step + " failed: " + why);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static AppUpdateManager _manager;
        static AppUpdateManager Manager => _manager ?? (_manager = new AppUpdateManager());

        // operations are polled from Tick, so nothing here needs a coroutine host
        static PlayAsyncOperation<AppUpdateInfo, AppUpdateErrorCode> _infoOp;
        static Action<PlayAsyncOperation<AppUpdateInfo, AppUpdateErrorCode>> _infoDone;
        static AppUpdateRequest _request;
        static Mode _requestMode;

        static void Poll(PlayAsyncOperation<AppUpdateInfo, AppUpdateErrorCode> op, Action<PlayAsyncOperation<AppUpdateInfo, AppUpdateErrorCode>> done)
        {
            _infoOp = op;
            _infoDone = done;
        }

        static void OnInfo(PlayAsyncOperation<AppUpdateInfo, AppUpdateErrorCode> op)
        {
            _checking = false;
            if (!op.IsSuccessful) { Debug.Log("[Update] no update info: " + op.Error); return; }
            var info = op.GetResult();
            int installed = InstalledVersionCode;
            bool resuming = info.UpdateAvailability == UpdateAvailability.DeveloperTriggeredUpdateInProgress;
            bool available = info.UpdateAvailability == UpdateAvailability.UpdateAvailable || resuming;
            if (!available) { _forcedPending = false; return; }
            var immediate = AppUpdateOptions.ImmediateAppUpdateOptions();
            var flexible = AppUpdateOptions.FlexibleAppUpdateOptions();
            var mode = Decide(installed, Math.Max(info.AvailableVersionCode, installed + 1), _forceBelow,
                info.IsUpdateTypeAllowed(immediate), info.IsUpdateTypeAllowed(flexible), PromptedToday && !resuming);
            if (resuming && mode == Mode.None && info.IsUpdateTypeAllowed(immediate)) mode = Mode.Immediate;   // finish what was started
            if (mode == Mode.None) return;
            _forcedPending = mode == Mode.Immediate;
            if (mode == Mode.Flexible) { try { PlayerPrefs.SetInt(LastPromptKey, Today); PlayerPrefs.Save(); } catch (Exception) { } }
            GameAnalytics.Log("update_prompt", ("mode", mode == Mode.Immediate ? "immediate" : "flexible"),
                ("installed", installed), ("available", info.AvailableVersionCode));
            _requestMode = mode;
            _request = Manager.StartUpdate(info, mode == Mode.Immediate ? immediate : flexible);
        }
#endif

        /// <summary>Follows the open operations. Call every frame; allocates nothing while idle.</summary>
        public static void Tick()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_infoOp != null && _infoOp.IsDone)
            {
                var op = _infoOp; var done = _infoDone;
                _infoOp = null; _infoDone = null;
                try { done(op); } catch (Exception e) { Fail("start", e.Message); }
            }
            if (_request == null) return;
            var status = _request.Status;
            if (status == AppUpdateStatus.Downloaded && _requestMode == Mode.Flexible && !ReadyToInstall)
            {
                ReadyToInstall = true;
                GameAnalytics.Log("update_downloaded");
                Downloaded?.Invoke();
            }
            if (!_request.IsDone) return;
            if (status == AppUpdateStatus.Canceled) GameAnalytics.Log("update_declined", ("mode", _requestMode == Mode.Immediate ? "immediate" : "flexible"));
            else if (status == AppUpdateStatus.Failed) Debug.LogWarning("[Update] update failed: " + _request.Error);
            if (status != AppUpdateStatus.Downloaded) _request = null;
#endif
        }
    }
}
