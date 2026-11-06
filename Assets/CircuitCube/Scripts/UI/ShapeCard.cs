using System;
using CircuitCube.Game;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CircuitCube.UI
{
    /// <summary>One level group in the Shapes list (prefab ShapeCard).</summary>
    public sealed class ShapeCard : MonoBehaviour
    {
        public Image Icon;
        public TMP_Text Title, Blurb, Progress;
        [Tooltip("RectMask2D over the full-width fill bar; its width shows progress.")]
        public RectTransform ProgressMask;
        public GameObject LockedRoot;
        public TMP_Text LockedText;
        public Button Button;
        [Tooltip("Old shape illustrations by index (used when the V2 lists below are empty).")]
        public Sprite[] Icons;
        [Tooltip("Icon id of each entry in IconsDark / IconsLight (cube, tetra, octa, cylinder, dodeca, sphere, megamix, cross).")]
        public string[] IconIds;
        public Sprite[] IconsDark, IconsLight;

        /// <summary>The V2 group icon for a group's icon id, in the current theme.</summary>
        public Sprite IconFor(string id)
        {
            var list = Themes.IsDark ? IconsDark : IconsLight;
            if (IconIds == null || list == null) return null;
            int i = Array.IndexOf(IconIds, id);
            return i >= 0 && i < list.Length ? list[i] : null;
        }

        public void Set(string iconId, string name, string blurb, bool open, int beaten, int total, bool endless, string unlockHint, Action onPick)
        {
            Set(-1, name, blurb, open, beaten, total, endless, unlockHint, onPick);
            var s = IconFor(iconId);
            if (s != null) Icon.sprite = s;
        }

        public void Set(int group, string name, string blurb, bool open, int beaten, int total, bool endless, string unlockHint, Action onPick)
        {
            if (Icons != null && group >= 0 && group < Icons.Length) Icon.sprite = Icons[group];
            Title.text = name;
            Blurb.text = blurb;
            Progress.text = endless && beaten >= total ? beaten + " beaten" : beaten + " / " + total;
            ProgressMask.anchorMax = new Vector2(Mathf.Clamp01(beaten / (float)total), ProgressMask.anchorMax.y);
            LockedRoot.SetActive(!open);
            ProgressMask.parent.gameObject.SetActive(open);
            Progress.gameObject.SetActive(open);
            LockedText.text = unlockHint;
            Icon.color = open ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            Button.interactable = open;
            Button.onClick.RemoveAllListeners();
            if (open) Button.onClick.AddListener(() => onPick());
        }
    }
}
