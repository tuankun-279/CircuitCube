using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.UI
{
    /// <summary>
    /// Bottom sheet "Your name on the boards": a name field that takes letters, numbers and _ only (up to
    /// <see cref="MaxLength"/>), the #tag the service adds, a count, the Play Games note, Save and Not now.
    /// </summary>
    public sealed class NameSheet : MonoBehaviour
    {
        public const int MaxLength = 20;

        public RectTransform Panel;
        public TMP_InputField Input;
        public TMP_Text Tag, Count, Note;
        public Button Save, NotNow;

        static readonly string[] Counts = BuildCounts();

        static string[] BuildCounts()
        {
            var a = new string[MaxLength + 1];
            for (int i = 0; i <= MaxLength; i++) a[i] = i + " / " + MaxLength;
            return a;
        }

        void Awake()
        {
            Input.characterLimit = MaxLength;
            Input.onValidateInput = (text, index, c) => char.IsLetterOrDigit(c) || c == '_' ? c : '\0';
            Input.onValueChanged.AddListener(ShowCount);
        }

        public void Open(string name, string tag, string note)
        {
            gameObject.SetActive(true);
            Input.text = name ?? "";
            Tag.text = tag ?? "";
            Note.text = note;
            ShowCount(Input.text);
        }

        void ShowCount(string text)
        {
            int n = Mathf.Clamp(text != null ? text.Length : 0, 0, MaxLength);
            Count.text = Counts[n];
            Save.interactable = n > 0;
        }
    }
}
