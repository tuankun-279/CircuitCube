using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.UI
{
    /// <summary>
    /// Scales a canvas designed at 1080 x 1920 to any screen (plan, part 4). Phones taller than 16:9 keep the design
    /// width and get extra height; screens wider than 16:9 (tablets, foldables, landscape) keep the design height, and
    /// <see cref="SafeAreaFitter.MaxWidth"/> keeps the UI in a centred column there instead of stretching it.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class ResponsiveScaler : MonoBehaviour
    {
        CanvasScaler _scaler;
        Vector2Int _last;

        void Awake() => _scaler = GetComponent<CanvasScaler>();

        void Update()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            if (size == _last || size.x <= 0 || size.y <= 0) return;
            _last = size;
            var r = _scaler.referenceResolution;
            _scaler.matchWidthOrHeight = (float)size.x / size.y > r.x / r.y + 0.001f ? 1f : 0f;
        }
    }
}
