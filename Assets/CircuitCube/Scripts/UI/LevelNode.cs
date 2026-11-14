using System;
using CircuitCube.Game;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CircuitCube.UI
{
    /// <summary>
    /// One cell of the level grid (prefab LevelNode). Sprites for each state are set on the prefab.
    /// A locked level is an empty frame with its number and a lock; an Expert level (full score) shows the Expert
    /// badge in a gold frame instead of its three stars.
    /// </summary>
    public sealed class LevelNode : MonoBehaviour
    {
        public Image Background;
        public TMP_Text Number;
        public StarRow Stars;
        public GameObject LockIcon;
        public Button Button;
        public Sprite OpenSprite, DoneSprite, LockedSprite;
        [Tooltip("Outline over the cell: shown on locked levels (frame colour) and Expert levels (star colour).")]
        public Image Frame;
        [Tooltip("Badge shown in place of the stars on a level won with the full score.")]
        public Image Expert;

        /// <summary>Score that counts as Expert (three full stars).</summary>
        public const int ExpertPoints = 6;

        public void Set(int level, bool open, int points, Action onPick) => Set(level, open, points, onPick, open && points == 0);

        /// <param name="points">The level's best score, 3-6 (0 = not won): shown as stars, with half stars.</param>
        /// <param name="current">The next level to play; highlighted like the mockup's level 12.</param>
        public void Set(int level, bool open, int points, Action onPick, bool current)
        {
            bool expert = open && !current && points >= ExpertPoints && Expert != null;
            Number.text = level.ToString();
            Background.sprite = !open ? LockedSprite : points > 0 ? DoneSprite : OpenSprite;
            var th = Themes.Current;
            if (th != null)
            {
                Background.color = th.Get(!open ? UiRole.Transparent : current ? UiRole.NodeCurrent : UiRole.NodeOpen);
                Number.color = th.Get(!open ? UiRole.TextMuted : current ? UiRole.OnPrimary : UiRole.OnNode);
                if (Frame != null) Frame.color = th.Get(expert ? UiRole.Star : UiRole.NodeFrame);
                if (Expert != null) Expert.color = th.Get(UiRole.Star);
            }
            if (Frame != null) Frame.gameObject.SetActive(!open || expert);
            if (Expert != null) Expert.gameObject.SetActive(expert);
            LockIcon.SetActive(!open);
            Stars.gameObject.SetActive(open && !expert);
            Stars.SetPoints(points);
            Button.interactable = open;
            Button.onClick.RemoveAllListeners();
            if (open) Button.onClick.AddListener(() => onPick());
        }
    }
}
