using System;
using CircuitCube.Game;
using UnityEngine;
using UnityEngine.UI;

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
        public Text Title;
        public Button Close;
        public SwitchToggle Sound, Music, Vibration;
        [Tooltip("Theme buttons in order: system, light, dark.")]
        public Button[] ThemeButtons = new Button[3];
        public GameObject PauseActions;
        public Button Resume, Restart, Home;

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
        }

        public void Open(bool paused)
        {
            gameObject.SetActive(true);
            Title.text = paused ? "Paused" : "Settings";
            PauseActions.SetActive(paused);
            if (Panel != null) Panel.sizeDelta = new Vector2(Panel.sizeDelta.x, paused ? PausedHeight : SettingsHeight);
            var s = CubeSave.Settings;
            Sound.Set(s.sound);
            Music.Set(s.music);
            Vibration.Set(s.vibration);
            ShowTheme(s.theme);
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
                var label = ThemeButtons[i].GetComponentInChildren<Text>(true)?.GetComponent<ThemeRole>();
                if (label != null) { label.Role = on ? UiRole.Text : UiRole.TextMuted; label.Apply(); }
            }
        }
    }
}
