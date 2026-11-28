using TMPro;
using UnityEngine;

namespace CircuitCube.UI
{
    /// <summary>
    /// One row of a leaderboard (prefab Prefabs/UI/BoardRow.prefab, also the pinned "You" row): a medal disc or plain
    /// rank, the player name with its grey #tag, and the score columns. "This week" rows show moves and time; "All-time"
    /// rows show Expert levels, moves and time (two column sets with their own name box, one is hidden). Filled by the game.
    /// </summary>
    public sealed class BoardRowView : MonoBehaviour
    {
        [Tooltip("Row fill: Row for the top three, Highlight for the player, Transparent for the rest.")]
        public ThemeRole Back;
        [Tooltip("Teal outline, shown on the player's row.")]
        public GameObject Frame;
        [Tooltip("Disc behind the rank of the top three (Star, Silver, Bronze).")]
        public ThemeRole Medal;
        public TMP_Text Rank;
        [Tooltip("Rank colour: OnStar on a medal, TextMuted without, Primary on the pinned row.")]
        public ThemeRole RankRole;
        [Tooltip("This week columns.")]
        public GameObject WeekColumns;
        public TMP_Text WeekName, WeekMoves, WeekTime;
        [Tooltip("All-time columns.")]
        public GameObject AllColumns;
        public TMP_Text AllName, AllExpert, AllMoves, AllTime;
        [Tooltip("Long press on the row: report the name (not on the player's own row).")]
        public LongPress Press;
    }
}
