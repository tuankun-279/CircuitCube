using System;
using CircuitCube.Game;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CircuitCube.UI
{
    /// <summary>
    /// The settings card (prefab part of UI.prefab). In a level it is the pause card with Resume, Restart and Home;
    /// on the home screen it only shows the switches and the theme choice.
    /// </summary>
    public sealed class SettingsCard : MonoBehaviour
    {
        public Animation Anim;          // clip "In"
        public RectTransform Panel;
        [Tooltip("Panel height with and without the pause buttons.")]
        public float PausedHeight = 1040f, SettingsHeight = 760f;
        public TMP_Text Title;
        public Button Close;
        public SwitchToggle Sound, Music, Vibration;
        [Tooltip("Theme buttons in order: system, light, dark.")]
        public Button[] ThemeButtons = new Button[3];
        public GameObject PauseActions;
        public Button Resume, Restart, Home;
        [Tooltip("Opens Google's privacy options form. Shown only where the law asks for it.")]
        public Button Privacy;
        [Tooltip("Extra panel height while the Privacy button shows.")]
        public float PrivacyHeight = 80f;
        [Tooltip("App version line. Seven taps show this device's code for the ad tester list and copy it.")]
        public Button VersionButton;
        public TMP_Text VersionLabel;

        const int TapsForDeviceCode = 7;
        int _versionTaps;

        static readonly string[] Modes = { "system", "light", "dark" };

        /// <summary>Raised with the theme mode the player picked.</summary>
        public event Action<string> ThemePicked;

        void Awake()
        {
            Sound.Changed += on => { CubeSave.Settings.sound = on; CubeSave.SaveSettings(); };
            Music.Changed += on => { CubeSave.Settings.music = on; CubeSave.SaveSettings(); };
            Vibration.Changed += on => { CubeSave.Settings.vibration = on; CubeSave.SaveSettings(); };
            for (int i = 0; i < ThemeButtons.Length; i++)
            {
                string mode = Modes[i];
                ThemeButtons[i].onClick.AddListener(() => { ThemePicked?.Invoke(mode); ShowTheme(mode); });
            }
            if (VersionButton != null) VersionButton.onClick.AddListener(VersionTapped);
        }

        void VersionTapped()
        {
            if (VersionLabel == null || ++_versionTaps < TapsForDeviceCode) return;
            string code = AdsGate.DeviceCode;
            GUIUtility.systemCopyBuffer = code;
            VersionLabel.text = "Device " + code + " \u00b7 ads " + (AdsGate.On ? "on" : "off") + " \u00b7 copied";
        }

        static string VersionText()
        {
            int code = AppUpdates.InstalledVersionCode;
            return code > 0 ? "Version " + Application.version + " (" + code + ")" : "Version " + Application.version;
        }

        public void Open(bool paused)
        {
            gameObject.SetActive(true);
            Title.text = paused ? "Paused" : "Settings";
            PauseActions.SetActive(paused);
            bool privacy = Privacy != null && RewardedAds.PrivacyOptionsRequired;
            if (Privacy != null) Privacy.gameObject.SetActive(privacy);
            if (Panel != null) Panel.sizeDelta = new Vector2(Panel.sizeDelta.x, (paused ? PausedHeight : SettingsHeight) + (privacy ? PrivacyHeight : 0f));
            var s = CubeSave.Settings;
            Sound.Set(s.sound);
            Music.Set(s.music);
            Vibration.Set(s.vibration);
            ShowTheme(s.theme);
            _versionTaps = 0;
            if (VersionLabel != null) VersionLabel.text = VersionText();
            if (Anim != null && Anim.GetClip("In") != null) { Anim.Stop(); Anim.Play("In"); }
        }

        void ShowTheme(string mode)
        {
            int pick = Mathf.Max(0, Array.IndexOf(Modes, mode));
            for (int i = 0; i < ThemeButtons.Length; i++)
            {
                bool on = i == pick;
                var bg = ThemeButtons[i].GetComponent<ThemeRole>();
                if (bg != null) { bg.Role = on ? UiRole.Card : UiRole.Transparent; bg.Apply(); }
                var label = ThemeButtons[i].GetComponentInChildren<TMP_Text>(true)?.GetComponent<ThemeRole>();
                if (label != null) { label.Role = on ? UiRole.Text : UiRole.TextMuted; label.Apply(); }
            }
        }
    }
}
