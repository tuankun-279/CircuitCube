using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.UI
{
    public sealed class LevelsScreen : UIScreen
    {
        public Button BackButton;
        public Text Title, Subtitle;
        public RectTransform Grid;
        public ScrollRect Scroll;
        public LevelNode NodePrefab;
    }
}
