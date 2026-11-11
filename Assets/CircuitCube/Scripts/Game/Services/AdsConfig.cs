using UnityEngine;

namespace CircuitCube.Game
{
    /// <summary>
    /// AdMob rewarded ad units (Resources/AdsConfig.asset). While <see cref="UseTestAds"/> is on, Google's test units are
    /// used whatever is typed here, so a build can never show real ads by accident before the real IDs are set.
    /// The AdMob app ID lives in Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset (Assets > Google Mobile Ads > Settings).
    /// </summary>
    [CreateAssetMenu(menuName = "Circuit Cube/Ads Config", fileName = "AdsConfig")]
    public sealed class AdsConfig : ScriptableObject
    {
        public const string ResourcePath = "AdsConfig";

        // Google's public test units (https://developers.google.com/admob/unity/test-ads)
        public const string TestRewardedAndroid = "ca-app-pub-3940256099942544/5224354917";
        public const string TestRewardedIos = "ca-app-pub-3940256099942544/1712485313";

        [Tooltip("Show Google's test ads. Turn off only after the real ad unit IDs below are filled in.")]
        public bool UseTestAds = true;

        [Header("Android rewarded ad units")]
        public string AndroidHint = "";
        public string AndroidUndo = "";

        [Header("iOS rewarded ad units")]
        public string IosHint = "";
        public string IosUndo = "";

        public string UnitFor(AdPlacement placement)
        {
#if UNITY_IOS
            string real = placement == AdPlacement.Hint ? IosHint : IosUndo;
            string test = TestRewardedIos;
#else
            string real = placement == AdPlacement.Hint ? AndroidHint : AndroidUndo;
            string test = TestRewardedAndroid;
#endif
            return UseTestAds || string.IsNullOrEmpty(real) ? test : real;
        }

        public static AdsConfig Load()
        {
            var c = Resources.Load<AdsConfig>(ResourcePath);
            return c != null ? c : CreateInstance<AdsConfig>();
        }
    }
}
