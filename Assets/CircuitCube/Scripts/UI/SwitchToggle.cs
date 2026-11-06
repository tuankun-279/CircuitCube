using System;
using CircuitCube.Game;
using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.UI
{
    /// <summary>An on/off switch (prefab part): a track and a knob that slides. Colours come from the theme roles.</summary>
    public sealed class SwitchToggle : MonoBehaviour
    {
        public Button Button;
        public Image Track;
        public RectTransform Knob;
        [Tooltip("Gap between the knob and the end of the track.")]
        public float Inset = 6f;
        public bool IsOn { get; private set; }
        public event Action<bool> Changed;

        void Awake() => Button.onClick.AddListener(() => { Set(!IsOn); Changed?.Invoke(IsOn); });

        public void Set(bool on)
        {
            IsOn = on;
            var r = Track.GetComponent<ThemeRole>();
            if (r != null) { r.Role = on ? UiRole.Primary : UiRole.Track; r.Apply(); }
            float x = on ? 1f : 0f;
            Knob.anchorMin = new Vector2(x, 0.5f);
            Knob.anchorMax = new Vector2(x, 0.5f);
            Knob.pivot = new Vector2(x, 0.5f);
            Knob.anchoredPosition = new Vector2(on ? -Inset : Inset, 0f);
        }
    }
}
