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
        [Tooltip("Undos left in this level, on a badge at the corner of the Undo button.")]
        public TMP_Text UndoCount;
        [Tooltip("Shown only while the board is zoomed in; zooms back out.")]
        public Button ZoomResetButton;
        public RectTransform Legend;
        public LegendChip ChipPrefab;
        [Header("Lamp progress")]
        public RectTransform LampDots;
        [Tooltip("Inactive template dot inside LampDots; copies are made for each lamp.")]
        public Image DotTemplate;
        public TMP_Text LampsLabel;
    }
}
