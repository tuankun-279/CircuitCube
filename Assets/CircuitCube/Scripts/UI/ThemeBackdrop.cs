using CircuitCube.Game;
using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.UI
{
    /// <summary>
    /// Shows the current theme's backdrop picture on this Image (the space sky behind the board). An AspectRatioFitter
    /// set to Envelope Parent keeps it filling the screen at any phone shape. Without a picture it falls back to the
    /// theme's plain Background colour.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class ThemeBackdrop : MonoBehaviour
    {
        Image _img;
        AspectRatioFitter _fit;

        void OnEnable()
        {
            Themes.Changed += Apply;
            Apply();
        }

        void OnDisable() => Themes.Changed -= Apply;

        public void Apply()
        {
            var th = Themes.Current;
            if (th == null) return;
            if (_img == null) { _img = GetComponent<Image>(); _fit = GetComponent<AspectRatioFitter>(); }
            var pic = th.Backdrop;
            _img.sprite = pic;
            _img.color = pic != null ? Color.white : th.Get(UiRole.Background);
            if (_fit != null && pic != null) _fit.aspectRatio = pic.rect.width / pic.rect.height;
        }
    }
}
