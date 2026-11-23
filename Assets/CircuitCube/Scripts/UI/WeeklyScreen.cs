using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.UI
{
    /// <summary>The Weekly Challenge page: header, three stat tiles, the rules, the three levels of the week, and the shape dock.</summary>
    public sealed class WeeklyScreen : UIScreen
    {
        public Button BackButton;
        public TMP_Text Subtitle;
        public TMP_Text EndsIn, Solved, TotalRank;
        public ThemeRole TotalRankRole;
        public WeeklyRowView[] Rows = new WeeklyRowView[0];
        public TMP_Text Footnote;
        public Button BoardButton;
        public ShapeDock Dock;
    }
}
