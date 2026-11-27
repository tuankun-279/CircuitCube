using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.UI
{
    /// <summary>Three star images; swaps between the full and empty star sprites set on the prefab.</summary>
    public sealed class StarRow : MonoBehaviour
    {
        public Image[] Stars = new Image[3];
        public Sprite Full, Empty;
        [Tooltip("Optional half star. Without it a half star rounds down.")]
        public Sprite Half;

        public void Set(int count)
        {
            for (int i = 0; i < Stars.Length; i++)
                if (Stars[i] != null) Stars[i].sprite = i < count ? Full : Empty;
        }

        /// <summary>
        /// Show a 3–6 point score as stars: 6 = 3, 5 = 2½, 4 = 2, 3 = 1½ (half star only if the sprite is set and
        /// <paramref name="halves"/> is on; otherwise the half is left out: 5 = 2, 3 = 1).
        /// </summary>
        public void SetPoints(int points, bool halves = true)
        {
            points = Mathf.Min(points, 6);   // Expert (7) is three stars where stars are shown
            int whole = Mathf.Max(0, points) / 2;
            bool half = halves && Half != null && points % 2 == 1;
            for (int i = 0; i < Stars.Length; i++)
                if (Stars[i] != null) Stars[i].sprite = i < whole ? Full : i == whole && half ? Half : Empty;
        }
    }
}
