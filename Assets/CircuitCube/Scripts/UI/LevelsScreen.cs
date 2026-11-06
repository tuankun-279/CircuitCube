using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.UI
{
    public sealed class LevelsScreen : UIScreen
    {
        public Button BackButton;
        public Text Title, Subtitle, StarsLabel;
        public RectTransform Grid;
        public ScrollRect Scroll;
        public LevelNode NodePrefab;
        [Header("Progress card")]
        public Text ProgressValue;
        public RectTransform ProgressFill;  // anchorMax.x shows progress
    }
}
