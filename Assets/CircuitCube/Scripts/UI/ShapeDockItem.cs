using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.UI
{
    /// <summary>One shape in the bottom shape dock: picture, name, a lock badge. Locked shapes are faded and cannot be picked.</summary>
    public sealed class ShapeDockItem : MonoBehaviour
    {
        public Button Button;
        [Tooltip("Teal fill behind the picked shape (Primary), clear otherwise.")]
        public ThemeRole Back;
        public Image Icon;
        [Tooltip("Line icon colour: OnPrimary when picked, TextMuted otherwise.")]
        public ThemeRole IconRole;
        public TMP_Text Name;
        public ThemeRole NameRole;
        public GameObject Lock;
        public CanvasGroup Group;
    }
}
