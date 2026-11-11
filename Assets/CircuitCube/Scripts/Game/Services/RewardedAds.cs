using System;
using System.Collections.Generic;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace CircuitCube.Game
{
    public enum AdPlacement { Hint, Undo }

    /// <summary>
    /// AdMob rewarded ads in front of a hint or an undo, with Google's consent form (UMP) first.
    /// One ad per placement is kept loaded so a tap shows it at once. When no ad can be shown (offline, nothing to
    /// fill, consent not given, the SDK failed) the player gets the reward anyway: the game never gets stuck on an ad.
    /// Closing the ad before it finishes gives nothing.
    /// </summary>
    public static class RewardedAds
    {
        /// <summary>Debug menu (F3): grant at once without an ad.</summary>
        public static bool SkipAds;
        /// <summary>An ad is on screen.</summary>
        public static bool Showing { get; private set; }

        static readonly AdPlacement[] Placements = { AdPlacement.Hint, AdPlacement.Undo };
        static readonly Dictionary<AdPlacement, RewardedAd> Loaded = new Dictionary<AdPlacement, RewardedAd>();
        static readonly Dictionary<AdPlacement, bool> Loading = new Dictionary<AdPlacement, bool>();
        static readonly Dictionary<AdPlacement, float> RetryAt = new Dictionary<AdPlacement, float>();
        static readonly Dictionary<AdPlacement, int> Failures = new Dictionary<AdPlacement, int>();
        static AdsConfig _config;
        static bool _initStarted, _sdkStarted, _sdkReady, _consentChecked;

        /// <summary>The player's region needs a way to change the consent choice later (Settings shows the button).</summary>
        public static bool PrivacyOptionsRequired
        {
            get
            {
                if (!_consentChecked) return false;
                try { return ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required; }
                catch (Exception) { return false; }
            }
        }

        /// <summary>Ask for consent if needed, then start the SDK and load one ad per placement. Safe to call again.</summary>
        public static void Init()
        {
            if (_initStarted) return;
            _initStarted = true;
            _config = AdsConfig.Load();
            try
            {
                // runs the queued callbacks below in Update; MobileAds.Initialize would create it too, but the consent
                // callbacks come before that
                MobileAdsEventExecutor.Initialize();
                // a returning player who already answered can get ads while the consent info refreshes
                if (ConsentInformation.CanRequestAds()) StartSdk();
                var request = new ConsentRequestParameters { TagForUnderAgeOfConsent = false };
                ConsentInformation.Update(request, updateError => MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    if (updateError != null)
                    {
                        Debug.LogWarning("[Ads] consent update failed: " + updateError.Message);
                        _consentChecked = true;
                        StartSdk();
                        return;
                    }
                    ConsentForm.LoadAndShowConsentFormIfRequired(formError => MobileAdsEventExecutor.ExecuteInUpdate(() =>
                    {
                        if (formError != null) Debug.LogWarning("[Ads] consent form failed: " + formError.Message);
                        _consentChecked = true;
                        StartSdk();
                    }));
                }));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ads] init failed, hints and undos stay free: " + e.Message);
            }
        }

        /// <summary>Settings > Privacy options: the form where the player changes the consent choice.</summary>
        public static void ShowPrivacyOptions(Action done = null)
        {
            try
            {
                ConsentForm.ShowPrivacyOptionsForm(error => MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    if (error != null) Debug.LogWarning("[Ads] privacy options failed: " + error.Message);
                    StartSdk();
                    done?.Invoke();
                }));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ads] privacy options failed: " + e.Message);
                done?.Invoke();
            }
        }

        static void StartSdk()
        {
            if (_sdkStarted) return;
            bool canRequest;
            try { canRequest = ConsentInformation.CanRequestAds(); }
            catch (Exception) { canRequest = false; }
            if (!canRequest) return;
            _sdkStarted = true;
            MobileAds.Initialize(status => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                if (status == null) { Debug.LogWarning("[Ads] SDK failed to start"); _sdkStarted = false; return; }
                _sdkReady = true;
                foreach (var p in Placements) Load(p);
            }));
        }

        /// <summary>Retries failed loads with a growing wait (5 s, 10 s … up to 2 min). Call every frame.</summary>
        public static void Tick()
        {
            if (!_sdkReady) return;
            float now = Time.realtimeSinceStartup;
            foreach (var p in Placements)
                if (RetryAt.TryGetValue(p, out float at) && at > 0f && now >= at)
                {
                    RetryAt[p] = 0f;
                    Load(p);
                }
        }

        static void Load(AdPlacement p)
        {
            if (!_sdkReady || _config == null) return;
            if (Loading.TryGetValue(p, out bool busy) && busy) return;
            if (Loaded.TryGetValue(p, out var have) && have != null && have.CanShowAd()) return;
            Loading[p] = true;
            // ad callbacks can arrive on another thread: everything below runs in the next Update
            RewardedAd.Load(_config.UnitFor(p), new AdRequest(), (ad, error) => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                Loading[p] = false;
                if (error != null || ad == null)
                {
                    int n = Failures.TryGetValue(p, out int f) ? f + 1 : 1;
                    Failures[p] = n;
                    RetryAt[p] = Time.realtimeSinceStartup + Mathf.Min(120f, 5f * Mathf.Pow(2f, n - 1));
                    Debug.LogWarning("[Ads] " + p + " failed to load: " + error);
                    return;
                }
                Failures[p] = 0;
                Loaded[p] = ad;
            }));
        }

        /// <summary>
        /// Show the rewarded ad for <paramref name="placement"/>. <paramref name="granted"/> runs when it was watched to
        /// the end, or straight away when no ad can be shown; <paramref name="declined"/> runs when the player closed it early.
        /// </summary>
        public static void Show(AdPlacement placement, Action granted, Action declined)
        {
            string name = placement == AdPlacement.Hint ? "hint" : "undo";
            Loaded.TryGetValue(placement, out var ad);
            if (SkipAds || ad == null || !ad.CanShowAd())
            {
                GameAnalytics.Log("ad_unavailable", ("placement", name), ("reason", SkipAds ? "skipped" : _sdkReady ? "not_loaded" : "no_sdk"));
                Load(placement);
                granted?.Invoke();
                return;
            }
            Loaded[placement] = null;
            bool earned = false, finished = false;
            void Finish(bool failedToShow)
            {
                if (finished) return;
                finished = true;
                Showing = false;
                AudioListener.pause = false;
                ad.Destroy();
                Load(placement);
                if (earned || failedToShow)
                {
                    if (earned) GameAnalytics.Log("ad_rewarded", ("placement", name));
                    else GameAnalytics.Log("ad_unavailable", ("placement", name), ("reason", "show_failed"));
                    granted?.Invoke();
                }
                else
                {
                    GameAnalytics.Log("ad_declined", ("placement", name));
                    declined?.Invoke();
                }
            }
            ad.OnAdFullScreenContentClosed += () => MobileAdsEventExecutor.ExecuteInUpdate(() => Finish(false));
            ad.OnAdFullScreenContentFailed += error => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                Debug.LogWarning("[Ads] " + placement + " failed to show: " + error);
                Finish(true);
            });
            Showing = true;
            AudioListener.pause = true;   // the ad has its own sound
            ad.Show(reward => earned = true);   // the reward comes before the close, both queued in order
        }
    }
}
