using CircuitCube.Game;
using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.UI
{
    /// <summary>
    /// Colours this Image or Text from the current theme by its role (set on the prefab). Sprites stay on the prefab;
    /// the white V2 UI sprites take their colour from here, so one set of art serves dark and light mode.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public sealed class ThemeRole : MonoBehaviour
    {
        public UiRole Role;
        [Range(0f, 1f)] public float Alpha = 1f;

        Graphic _g;

        void OnEnable()
        {
            Themes.Changed += Apply;
            Apply();
        }

        void OnDisable() => Themes.Changed -= Apply;

        public void Apply()
        {
            if (Themes.Current == null || Role == UiRole.None) return;
            if (_g == null) _g = GetComponent<Graphic>();
            var c = Themes.Current.Get(Role);
            c.a *= Alpha;
            _g.color = c;
        }
    }
}
