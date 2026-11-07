using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.UI
{
    /// <summary>
    /// Fits as many columns of a fixed-column grid as its width allows (between <see cref="Min"/> and
    /// <see cref="Max"/>), keeping the cell size: four level tiles on a phone, five or six on a tablet.
    /// </summary>
    [RequireComponent(typeof(GridLayoutGroup))]
    public sealed class GridColumns : MonoBehaviour
    {
        public int Min = 4, Max = 6;
        float _lastWidth = -1f;

        void OnEnable() => Fit();
        void OnRectTransformDimensionsChange() => Fit();

        void Fit()
        {
            var grid = GetComponent<GridLayoutGroup>();
            if (grid == null) return;
            float width = ((RectTransform)transform).rect.width - grid.padding.horizontal;
            if (width <= 0f || Mathf.Approximately(width, _lastWidth)) return;
            _lastWidth = width;
            int n = Mathf.Clamp(Mathf.FloorToInt((width + grid.spacing.x) / (grid.cellSize.x + grid.spacing.x)), Min, Max);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            if (grid.constraintCount != n) grid.constraintCount = n;
        }
    }
}
