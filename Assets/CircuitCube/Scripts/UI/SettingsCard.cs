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
        [Tooltip("Zoom buttons in a level (on by itself on emulators).")]
        public SwitchToggle ZoomButtons;
        [Tooltip("Volume sliders (0..1) for effects and music; when set they replace the Sound and Music switches.")]
        public Slider SoundVolume, MusicVolume;
        [Tooltip("Language buttons in order: English, Vietnamese.")]
        public Button[] LanguageButtons = new Button[0];
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
        [Tooltip("1.5.0: the name on the boards, the renames left and the Rename button.")]
        public TMP_Text NameValue, RenameNote;
        public Button Rename;
        [Tooltip("1.5.0: links to TERMS.md and to the delete section of PRIVACY.md.")]
        public Button Terms, DeleteData;

        const int TapsForDeviceCode = 7;
        int _versionTaps;

        static readonly string[] Modes = { "system", "light", "dark" };

        /// <summary>Raised when the player turns the zoom buttons on or off.</summary>
        public event Action ZoomButtonsChanged;

        /// <summary>Raised with the theme mode the player picked.</summary>
        public event Action<string> ThemePicked;

        void Awake()
        {
            if (Sound != null) Sound.Changed += on => { CubeSave.Settings.sound = on; CubeSave.SaveSettings(); };
            if (Music != null) Music.Changed += on => { CubeSave.Settings.music = on; CubeSave.SaveSettings(); };
            if (SoundVolume != null)
            {
                SoundVolume.onValueChanged.AddListener(v => { CubeSave.Settings.soundVolume = v; CubeSave.Settings.sound = v > 0.001f; });
                AddRelease(SoundVolume, () => { CubeSave.SaveSettings(); GameAudio.Play(Sfx.Button); });   // a sample at the new level
            }
            if (MusicVolume != null)
            {
                MusicVolume.onValueChanged.AddListener(v => { CubeSave.Settings.musicVolume = v; CubeSave.Settings.music = v > 0.001f; });
                AddRelease(MusicVolume, CubeSave.SaveSettings);
            }
            for (int i = 0; i < LanguageButtons.Length; i++)
            {
                string lang = Loc.Languages[i];
                LanguageButtons[i].onClick.AddListener(() => { Loc.Set(lang); ShowLanguage(); });
            }
            Vibration.Changed += on => { CubeSave.Settings.vibration = on; CubeSave.SaveSettings(); };
            if (ZoomButtons != null) ZoomButtons.Changed += on => { CubeSave.Settings.zoomButtons = on ? 1 : 0; CubeSave.SaveSettings(); ZoomButtonsChanged?.Invoke(); };
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
            return code > 0 ? Loc.F("Version {0} ({1})", Application.version, code) : Loc.F("Version {0}", Application.version);
        }

        public void Open(bool paused)
        {
            gameObject.SetActive(true);
            Title.text = Loc.T(paused ? "Paused" : "Settings");
            PauseActions.SetActive(paused);
            bool privacy = Privacy != null && RewardedAds.PrivacyOptionsRequired;
            if (Privacy != null) Privacy.gameObject.SetActive(privacy);
            if (Panel != null) Panel.sizeDelta = new Vector2(Panel.sizeDelta.x, (paused ? PausedHeight : SettingsHeight) + (privacy ? PrivacyHeight : 0f));
            var s = CubeSave.Settings;
            if (Sound != null) Sound.Set(s.sound);
            if (Music != null) Music.Set(s.music);
            if (SoundVolume != null) SoundVolume.SetValueWithoutNotify(s.sound ? s.soundVolume : 0f);
            if (MusicVolume != null) MusicVolume.SetValueWithoutNotify(s.music ? s.musicVolume : 0f);
            ShowLanguage();
            Vibration.Set(s.vibration);
            if (ZoomButtons != null) ZoomButtons.Set(Emulator.ZoomButtonsOn);
            ShowTheme(s.theme);
            _versionTaps = 0;
            if (VersionLabel != null) VersionLabel.text = VersionText();
            if (Anim != null && Anim.GetClip("In") != null) { Anim.Stop(); Anim.Play("In"); }
        }

        /// <summary>Saves when the finger leaves a slider, not on every step of the drag.</summary>
        static void AddRelease(Slider slider, Action done)
        {
            var trigger = slider.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (trigger == null) trigger = slider.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var entry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp };
            entry.callback.AddListener(_ => done());
            trigger.triggers.Add(entry);
        }

        void ShowLanguage() => Highlight(LanguageButtons, Array.IndexOf(Loc.Languages, Loc.Current));

        void ShowTheme(string mode) => Highlight(ThemeButtons, Mathf.Max(0, Array.IndexOf(Modes, mode)));

        static void Highlight(Button[] buttons, int pick)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                bool on = i == pick;
                var bg = buttons[i].GetComponent<ThemeRole>();
                if (bg != null) { bg.Role = on ? UiRole.Card : UiRole.Transparent; bg.Apply(); }
                var label = buttons[i].GetComponentInChildren<TMP_Text>(true)?.GetComponent<ThemeRole>();
                if (label != null) { label.Role = on ? UiRole.Text : UiRole.TextMuted; label.Apply(); }
            }
        }
    }
}
