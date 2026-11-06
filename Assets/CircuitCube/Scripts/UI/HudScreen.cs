using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.UI
{
    public sealed class HudScreen : UIScreen
    {
        public Button BackButton, RestartButton, HintButton;
        public Text Title, Status, Tip, HintCount;
        public RectTransform Legend;
        public LegendChip ChipPrefab;
    }
}
