using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.UI
{
    public sealed class HomeScreen : UIScreen
    {
        public Button PlayButton, ShapesButton, LevelsButton, SettingsButton;
        public Text PlayLabel, GroupLabel, StarsLabel;
        [Header("Current shape card")]
        public Image GroupIcon;
        public Text GroupName, GroupProgress;
        public RectTransform GroupFill;     // anchorMax.x shows progress
    }
}
