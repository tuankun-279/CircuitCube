using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CircuitCube.UI
{
    public sealed class HudScreen : UIScreen
    {
        [Tooltip("Top-left button; opens the pause card.")]
        public Button BackButton;
        public Button RestartButton, HintButton, HelpButton, UndoButton;
        public TMP_Text Title, Status, Tip, HintCount;
        public RectTransform Legend;
        public LegendChip ChipPrefab;
        [Header("Lamp progress")]
        public RectTransform LampDots;
        [Tooltip("Inactive template dot inside LampDots; copies are made for each lamp.")]
        public Image DotTemplate;
        public TMP_Text LampsLabel;
    }
}
