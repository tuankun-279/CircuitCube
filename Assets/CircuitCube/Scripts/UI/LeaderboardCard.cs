using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.UI
{
    /// <summary>
    /// The Leaderboards pop-up: "This week" (Total / L1 / L2 / L3 boards of a shape's weekly set, a countdown and the
    /// pinned "You" row) and "All-time" (the shape boards, with the player's summary on top). Each half has its own
    /// list; rows are <see cref="RowPrefab"/> copies kept under the list and reused.
    /// </summary>
    public sealed class LeaderboardCard : MonoBehaviour
    {
        public Animation Anim;          // clip "In"
        public Button Close, Edit;
        [Header("This week | All-time")]
        public Button WeekTab, AllTab;
        public ThemeRole WeekTabBack, AllTabBack, WeekTabLabel, AllTabLabel;
        [Header("This week")]
        public GameObject WeekRoot;
        public Button[] LevelTabs = new Button[0];
        [Tooltip("Tab fill: Highlight on the picked level, Card otherwise.")]
        public ThemeRole[] LevelTabBacks = new ThemeRole[0];
        public ThemeRole[] LevelTabLabels = new ThemeRole[0];
        public GameObject[] LevelTabFrames = new GameObject[0];
        public TMP_Text Countdown;
        public ScrollRect WeekScroll;
        public RectTransform WeekList;
        public BoardRowView You;
        [Header("All-time")]
        public GameObject AllRoot;
        public TMP_Text SummaryTitle, SummaryLine, SummaryRank, SummaryOf;
        public ScrollRect AllScroll;
        public RectTransform AllList;
        public TMP_Text Footnote;
        [Header("Rows")]
        public BoardRowView RowPrefab;
        [Tooltip("Loading, offline or empty board.")]
        public TMP_Text Message;
        public ShapeDock Dock;

        public void Open()
        {
            gameObject.SetActive(true);
            if (Anim != null && Anim.GetClip("In") != null) { Anim.Stop(); Anim.Play("In"); }
        }
    }
}
