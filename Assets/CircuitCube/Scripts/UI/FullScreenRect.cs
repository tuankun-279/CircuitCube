using UnityEngine;

namespace CircuitCube.UI
{
    /// <summary>
    /// Stretches an element over the whole screen even when its parent is a narrower column (a dim overlay or a
    /// full-screen card background inside the safe-area column on a tablet).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class FullScreenRect : MonoBehaviour
    {
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
            // the canvas rect in the parent's space, so the element covers it exactly
            var c = (RectTransform)canvas.transform;
            var corners = new Vector3[4];
            c.GetWorldCorners(corners);
            Vector2 min = parent.InverseTransformPoint(corners[0]), max = parent.InverseTransformPoint(corners[2]);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = max - min;
            rt.anchoredPosition = (min + max) * 0.5f - parent.rect.center;
        }
    }
}
