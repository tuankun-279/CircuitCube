using UnityEngine;

namespace CircuitCube.UI
{
    /// <summary>Keeps its RectTransform inside Screen.safeArea (notches, rounded corners, gesture bar).</summary>
    [ExecuteAlways]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        Rect _last;
        Vector2Int _lastScreen;

        void Update()
        {
            if (!Application.isPlaying) return;
            var sa = Screen.safeArea;
            var scr = new Vector2Int(Screen.width, Screen.height);
            if (sa == _last && scr == _lastScreen) return;
            _last = sa;
            _lastScreen = scr;
            var rt = (RectTransform)transform;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            rt.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            rt.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
