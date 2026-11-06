using System;
using CircuitCube.Game;
using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.UI
{
    /// <summary>One cell of the level grid (prefab LevelNode). Sprites for each state are set on the prefab.</summary>
    public sealed class LevelNode : MonoBehaviour
    {
        public Image Background;
        public Text Number;
        public StarRow Stars;
        public GameObject LockIcon;
        public Button Button;
        public Sprite OpenSprite, DoneSprite, LockedSprite;
        public Color OpenText = new Color(0.12f, 0.14f, 0.25f), LockedText = new Color(0.6f, 0.64f, 0.71f);

        public void Set(int level, bool open, int stars, Action onPick)
        {
            Number.text = level.ToString();
            Number.color = open ? OpenText : LockedText;
            Background.sprite = !open ? LockedSprite : stars > 0 ? DoneSprite : OpenSprite;
            var th = Themes.Current;
            if (th != null)
            {
                // the next level to play is highlighted, like the mockup's level 12
                bool current = open && stars == 0;
                Background.color = th.Get(!open ? UiRole.NodeLocked : current ? UiRole.NodeCurrent : UiRole.NodeOpen);
                Number.color = th.Get(!open ? UiRole.TextMuted : current ? UiRole.OnPrimary : UiRole.OnNode);
            }
            LockIcon.SetActive(!open);
            Stars.gameObject.SetActive(open);
            Stars.Set(stars);
            Button.interactable = open;
            Button.onClick.RemoveAllListeners();
            if (open) Button.onClick.AddListener(() => onPick());
        }
    }
}
