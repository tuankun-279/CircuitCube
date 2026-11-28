using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.UI
{
    /// <summary>
    /// Bottom sheet "Report name" (1.5.0), opened by a long press on a leaderboard row: the reported name, a short
    /// note on what happens next, Report and Cancel. Filled by the game.
    /// </summary>
    public sealed class ReportSheet : MonoBehaviour
    {
        public RectTransform Panel;
        public TMP_Text Title, PlayerName, Body;
        public Button Report, Cancel;
    }
}
