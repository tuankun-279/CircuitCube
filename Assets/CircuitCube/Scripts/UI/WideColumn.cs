using UnityEngine;

namespace CircuitCube.UI
{
    /// <summary>
    /// Lets a screen grow wider than the UI column (<see cref="SafeAreaFitter.MaxWidth"/>) on wide screens, up to
    /// <see cref="MaxWidth"/> canvas units and never past the screen, centred on the column. The Levels screen uses
    /// it so a tablet shows more level tiles per row. The element must stretch over its parent.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class WideColumn : MonoBehaviour
    {
        public float MaxWidth = 1600f;
        Vector2 _lastParent;
        Vector2Int _lastScreen;

        void LateUpdate()
        {
            var rt = (RectTransform)transform;
            var parent = rt.parent as RectTransform;
            var canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (parent == null || canvas == null) return;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (parent.rect.size == _lastParent && screen == _lastScreen) return;
            _lastParent = parent.rect.size;
            _lastScreen = screen;
            // the canvas width in the parent's space
            var corners = new Vector3[4];
            ((RectTransform)canvas.transform).GetWorldCorners(corners);
            float canvasWidth = parent.InverseTransformPoint(corners[2]).x - parent.InverseTransformPoint(corners[0]).x;
            float width = Mathf.Max(parent.rect.width, Mathf.Min(MaxWidth, canvasWidth));
            float grow = (width - parent.rect.width) * 0.5f;
            rt.anchorMin = new Vector2(0f, rt.anchorMin.y);
            rt.anchorMax = new Vector2(1f, rt.anchorMax.y);
            rt.offsetMin = new Vector2(-grow, rt.offsetMin.y);
            rt.offsetMax = new Vector2(grow, rt.offsetMax.y);
        }
    }
}
