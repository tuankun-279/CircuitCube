using CircuitCube.Game;
using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.UI
{
    /// <summary>
    /// Swaps an image between two sprites with the theme: glow sprites are baked for the card colour they sit on,
    /// so the light theme needs its own copy.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class ThemeSprite : MonoBehaviour
    {
        public Sprite Dark, Light;

        void OnEnable()
        {
            Themes.Changed += Apply;
            Apply();
        }

        void OnDisable() => Themes.Changed -= Apply;

        public void Apply()
        {
            var s = Themes.IsDark ? Dark : Light;
            if (s != null) GetComponent<Image>().sprite = s;
        }
    }
}
