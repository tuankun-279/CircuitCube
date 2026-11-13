using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.UI
{
    /// <summary>
    /// First-time tutorial layer over the HUD: a pulsing pointer that shows where to tap or which way to drag, a speech
    /// bubble and a Skip button. Built in the editor (Tools/Circuit Cube/Art V2/Build Tutorial UI); the game only moves
    /// the pointer, fills the text and toggles the parts. Only Skip catches touches, so taps reach the board.
    /// </summary>
    public sealed class TutorialOverlay : MonoBehaviour
    {
        [Tooltip("Pointer group, anchored at the centre of this layer.")]
        public RectTransform Pointer;
        [Tooltip("Ring around the pointer that grows and fades out.")]
        public Image Ring;
        [Tooltip("Solid dot in the middle: the fingertip.")]
        public Image Dot;
        [Tooltip("Light centre of the fingertip.")]
        public Image Core;
        public RectTransform Bubble;
        public TMP_Text Text;
        public Button Skip;

        RectTransform _rect;
        public RectTransform Rect => _rect != null ? _rect : _rect = (RectTransform)transform;

        /// <summary>The camera this layer's canvas draws with (null for an overlay canvas).</summary>
        public Camera UiCamera
        {
            get
            {
                if (_canvas == null) _canvas = GetComponentInParent<Canvas>().rootCanvas;
                return _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            }
        }
        Canvas _canvas;

        /// <summary>Place the pointer over a screen point.</summary>
        public void PointAt(Vector2 screen)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, screen, UiCamera, out var local))
                Pointer.anchoredPosition = local;
        }

        /// <summary>
        /// Pulse for a tap (time in seconds since the step began): the ring grows and fades, the dot presses down.
        /// </summary>
        public void AnimateTap(float t, bool fingertip = true)
        {
            TipAlpha(fingertip ? 1f : 0f);
            float p = Mathf.Repeat(t, 1.2f) / 1.2f;
            float ring = Mathf.Clamp01(p / 0.75f);
            Ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.5f, ring);
            var c = Ring.color; c.a = 0.85f * (1f - ring); Ring.color = c;
            float press = p < 0.15f ? p / 0.15f : p < 0.3f ? 1f - (p - 0.15f) / 0.15f : 0f;
            Dot.rectTransform.localScale = Vector3.one * (1f - 0.22f * press);
        }

        /// <summary>A swipe from left to right across <paramref name="centre"/>, repeating.</summary>
        public void AnimateDrag(float t, Vector2 centre, float width)
        {
            float p = Mathf.Repeat(t, 1.6f) / 1.6f;
            float move = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((p - 0.1f) / 0.6f));
            Pointer.anchoredPosition = centre + new Vector2(Mathf.Lerp(-width * 0.5f, width * 0.5f, move), 0f);
            float fade = p < 0.1f ? p / 0.1f : p > 0.8f ? 1f - (p - 0.8f) / 0.2f : 1f;
            TipAlpha(fade);
            var c = Ring.color; c.a = 0.5f * fade; Ring.color = c;
            Ring.rectTransform.localScale = Vector3.one;
            Dot.rectTransform.localScale = Vector3.one * (p > 0.1f && p < 0.7f ? 0.85f : 1f);
        }

        /// <summary>Back to a still, fully visible pointer.</summary>
        public void ResetPointer()
        {
            TipAlpha(1f);
            Dot.rectTransform.localScale = Vector3.one;
        }

        void TipAlpha(float a)
        {
            var d = Dot.color; d.a = a; Dot.color = d;
            if (Core != null) { var c = Core.color; c.a = a; Core.color = c; }
        }
    }
}
