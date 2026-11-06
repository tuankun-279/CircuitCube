using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CircuitCube.UI
{
    public sealed class LevelsScreen : UIScreen
    {
        public Button BackButton;
        public TMP_Text Title, Subtitle, StarsLabel;
        public RectTransform Grid;
        public ScrollRect Scroll;
        public LevelNode NodePrefab;
        [Header("Progress card")]
        public TMP_Text ProgressValue;
        public RectTransform ProgressFill;  // anchorMax.x shows progress
    }
}
