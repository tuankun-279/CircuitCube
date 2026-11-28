using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.UI
{
    /// <summary>
    /// Bottom sheet "Your name on the boards": a name field that takes letters, numbers and _ only (up to
    /// <see cref="MaxLength"/>), the #tag the service adds, a count, the Play Games note, Save and Not now.
    /// 1.5.0: a line under the count for a refused name, and "By continuing you agree to the Terms" (a link).
    /// </summary>
    public sealed class NameSheet : MonoBehaviour
    {
        public const int MaxLength = 20;

        public RectTransform Panel;
        public TMP_InputField Input;
        public TMP_Text Tag, Count, Note;
        public Button Save, NotNow;
        public TMP_Text Title, Error;
        [Tooltip("The Terms line: opens TERMS.md.")]
        public Button Terms;

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

        public void Open(string name, string tag, string note, string title = null)
        {
            gameObject.SetActive(true);
            if (Title != null && title != null) Title.text = title;
            ShowError(null);
            Input.text = name ?? "";
            Tag.text = tag ?? "";
            Note.text = note;
            ShowCount(Input.text);
        }

        /// <summary>Why the server refused the name; null hides the line.</summary>
        public void ShowError(string text)
        {
            if (Error == null) return;
            Error.gameObject.SetActive(!string.IsNullOrEmpty(text));
            Error.text = text ?? "";
        }

        void ShowCount(string text)
        {
            ShowError(null);
            int n = Mathf.Clamp(text != null ? text.Length : 0, 0, MaxLength);
            Count.text = Counts[n];
            Save.interactable = n > 0;
        }
    }
}
