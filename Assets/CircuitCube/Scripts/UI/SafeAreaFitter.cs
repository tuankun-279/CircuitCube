using UnityEngine;

namespace CircuitCube.UI
{
    /// <summary>
    /// Keeps its RectTransform inside Screen.safeArea (notches, rounded corners, gesture bar) and, on screens wider
    /// than the design, no wider than <see cref="MaxWidth"/> canvas units, centred.
    /// </summary>
    [ExecuteAlways]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        [Tooltip("Widest the UI column may get, in canvas units (0 = no limit). The design is 1080 wide.")]
        public float MaxWidth = 1080f;
        Rect _last;
        Vector2Int _lastScreen;
        float _lastScale;

        void Update()
        {
            if (!Application.isPlaying) return;
            var sa = Screen.safeArea;
            var scr = new Vector2Int(Screen.width, Screen.height);
            var canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
            if (sa == _last && scr == _lastScreen && Mathf.Approximately(scale, _lastScale)) return;
            _last = sa;
            _lastScreen = scr;
            _lastScale = scale;
            var rt = (RectTransform)transform;
            if (Screen.width <= 0 || Screen.height <= 0 || scale <= 0f) return;
            float xMin = sa.xMin, xMax = sa.xMax;
            if (MaxWidth > 0f && (xMax - xMin) / scale > MaxWidth)
            {
                float pad = ((xMax - xMin) - MaxWidth * scale) * 0.5f;
                xMin += pad;
                xMax -= pad;
            }
            rt.anchorMin = new Vector2(xMin / Screen.width, sa.yMin / Screen.height);
            rt.anchorMax = new Vector2(xMax / Screen.width, sa.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
