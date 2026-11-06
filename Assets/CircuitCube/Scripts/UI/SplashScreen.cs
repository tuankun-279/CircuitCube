using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.UI
{
    /// <summary>The first screen: logo, tag line and a loading bar while the level data is read.</summary>
    public sealed class SplashScreen : UIScreen
    {
        public RectTransform Fill;      // anchorMax.x shows progress
        public Text Status;

        public void SetProgress(float p) => Fill.anchorMax = new Vector2(Mathf.Clamp01(p), Fill.anchorMax.y);
    }
}
