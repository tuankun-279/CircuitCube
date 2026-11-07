using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CircuitCube.UI
{
    public sealed class HomeScreen : UIScreen
    {
        public Button PlayButton, ShapesButton, LevelsButton, SettingsButton;
        public TMP_Text PlayLabel, GroupLabel, StarsLabel;
        [Header("Current shape card")]
        public Image GroupIcon;
        public TMP_Text GroupName, GroupProgress;
        public RectTransform GroupFill;     // anchorMax.x shows progress
        [Tooltip("Dot on the Shapes button: some open group has new levels.")]
        public GameObject ShapesBadge;
    }
}
