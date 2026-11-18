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
        [Header("1.2.0")]
        [Tooltip("Lamps per colour, above Undo/Restart; holds one LampGroup per colour.")]
        public RectTransform LampTray;
        public LampGroup GroupPrefab;
        [Tooltip("Top-right mascot with moods and short lines.")]
        public Mascot Mascot;
        [Header("1.3.0 zoom buttons (emulators)")]
        [Tooltip("Pill on the right edge with zoom in, zoom out and fit; shown on emulators or when Settings turns it on.")]
        public GameObject ZoomPanel;
        public Button ZoomIn, ZoomOut, ZoomFit;
        public TMP_Text ZoomLevel;
        [Tooltip("One-time tip beside the pill.")]
        public GameObject ZoomTip;
    }
}
