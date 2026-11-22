using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.UI
{
    /// <summary>One level of the weekly set on the Weekly Challenge page: number badge, shape and difficulty, the best run, and the rank or a Play button.</summary>
    public sealed class WeeklyRowView : MonoBehaviour
    {
        [Tooltip("The whole row: plays the level (a solved level can be played again for a better run).")]
        public Button Button;
        [Tooltip("Teal outline on the next level to play.")]
        public GameObject Frame;
        [Tooltip("Number badge: Highlight when solved, Secondary when not.")]
        public ThemeRole Badge;
        public TMP_Text Number;
        public ThemeRole NumberRole;
        public TMP_Text Shape, Level, Detail;
        public ThemeRole DetailRole;
        public GameObject RankRoot;
        public TMP_Text Rank, Of;
        public Button Play;
    }
}
