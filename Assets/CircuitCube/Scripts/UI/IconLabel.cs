using TMPro;
using UnityEngine;

namespace CircuitCube.UI
{
    /// <summary>
    /// A button with an icon before its label (Undo, Restart, Next level, Home...). The label gets the room the button has
    /// after the icon and shrinks to fit it (TMP auto size), and the icon and the label stay centred together, whatever
    /// the language makes the text: Vietnamese words are often longer than the English the prefab was laid out with.
    /// Set up by the editor (Tools/Circuit Cube/Art V2/Fit labels to their language); this only moves the two parts.
    /// </summary>
    [ExecuteAlways]
    public sealed class IconLabel : MonoBehaviour
    {
        public RectTransform Icon;
        public TMP_Text Label;
        [Tooltip("Space between the icon and the text, canvas units.")]
        public float Gap = 16f;
        [Tooltip("Space kept free at both ends of the button, canvas units.")]
        public float Padding = 24f;

        RectTransform _rect;
        bool _busy;

        void OnEnable()
        {
            _rect = (RectTransform)transform;
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            Fit();
        }

        void OnDisable() => TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);

        void OnRectTransformDimensionsChange() { if (isActiveAndEnabled) Fit(); }

        void OnTextChanged(Object changed)
        {
            if (changed == Label) Fit();
        }

        /// <summary>Label box after the icon, then the pair centred on the button.</summary>
        public void Fit()
        {
            if (_busy || Icon == null || Label == null || _rect == null) return;
            _busy = true;
            float width = _rect.rect.width, icon = Icon.rect.width;
            if (width <= 1f) { _busy = false; return; }   // not laid out yet
            // both parts are placed from the button's middle
            Icon.anchorMin = new Vector2(0.5f, Icon.anchorMin.y);
            Icon.anchorMax = new Vector2(0.5f, Icon.anchorMax.y);
            Icon.pivot = new Vector2(0.5f, Icon.pivot.y);
            var lr = (RectTransform)Label.transform;
            float room = Mathf.Max(10f, width - 2f * Padding - icon - Gap);
            lr.anchorMin = new Vector2(0.5f, 0f);
            lr.anchorMax = new Vector2(0.5f, 1f);
            lr.sizeDelta = new Vector2(room, lr.sizeDelta.y);
            Label.ForceMeshUpdate();
            float text = Mathf.Min(room, Label.textBounds.size.x);
            if (text <= 0f || float.IsNaN(text)) text = Mathf.Min(room, Label.preferredWidth);
            float total = icon + Gap + text;
            float left = -total * 0.5f;
            Icon.anchoredPosition = new Vector2(left + icon * 0.5f, Icon.anchoredPosition.y);
            // the text is centred in its box: put the box's middle where the text's middle goes
            lr.anchoredPosition = new Vector2(left + icon + Gap + text * 0.5f, lr.anchoredPosition.y);
            _busy = false;
        }
    }
}
