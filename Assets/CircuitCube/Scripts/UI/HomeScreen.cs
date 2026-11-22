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
        [Header("Leaderboards")]
        [Tooltip("\"Ranks\" pill in the top bar: opens the Leaderboards card on All-time.")]
        public Button RanksButton;
        [Tooltip("Weekly Challenge card above the shape card: opens the Weekly page.")]
        public Button WeeklyButton;
        public GameObject WeeklyNew;
        public TMP_Text WeeklyLine;
        [Tooltip("One bar per level of the week: Primary when solved, Track when not.")]
        public ThemeRole[] WeeklySteps = new ThemeRole[0];
    }
}
