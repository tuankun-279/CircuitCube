using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.UI
{
    /// <summary>
    /// The first-time tutorial layer (built by Tools/Circuit Cube/Art V2/Build 1.3.0 FTUE UI, from the approved FTUE
    /// mockups): the mascot's note beside the HUD mascot, a dim with a lit hole round what the note talks about, rings
    /// and a tapping hand on tiles, a swipe arrow, the welcome screen and the "Ready to play?" screen. The game places
    /// and fills the parts; this component only animates them. Taps reach the board except while a note waits for one.
    /// </summary>
    public sealed class TutorialOverlay : MonoBehaviour
    {
        [Header("Dim with a hole")]
        [Tooltip("9-slice frame: dimmed outside, a lit rounded hole with a cyan edge inside (ftue_hole).")]
        public RectTransform Hole;
        [Tooltip("Plain dim quads above, below, left and right of the hole frame.")]
        public RectTransform[] DimSides = new RectTransform[4];
        [Header("Marks")]
        public RectTransform[] Rings = new RectTransform[2];
        public RectTransform Hand;
        public RectTransform Swipe;
        [Header("Note")]
        [Tooltip("Canvas on the HUD mascot: sorted over the dim while a note shows, so the speaker stays lit.")]
        public Canvas Mascot;
        public RectTransform Note;
        public TMP_Text NoteName, NoteText, NextLabel;
        public Button Skip;
        [Tooltip("Covers the screen while a note waits for a tap: any tap moves on.")]
        public Button TapCatcher;
        [Header("Welcome")]
        public GameObject Welcome;
        public RectTransform WelcomeMascot, WelcomeBubble;
        public Image WelcomeFace, WelcomeHalo;
        public RectTransform[] Rays = new RectTransform[12];
        public TMP_Text WelcomeName, WelcomeText, WelcomeTap;
        public Button WelcomeButton;
        [Header("Ready to play")]
        public GameObject Ready;
        public RectTransform ReadyContent, ReadyMascot, ReadyBubble;
        public Image ReadyFace;
        public TMP_Text ReadyCaption, ReadyTitle, ReadySub;
        public Button Go, Replay;

        /// <summary>Hole padding round its target and the dim margin baked into the frame sprite (canvas units).</summary>
        const float Pad = 5f * 2.803f, Margin = 60f * 2.803f;

        RectTransform _rect;
        public RectTransform Rect => _rect != null ? _rect : _rect = (RectTransform)transform;

        Canvas _canvas;
        public Camera UiCamera
        {
            get
            {
                if (_canvas == null) _canvas = GetComponentInParent<Canvas>().rootCanvas;
                return _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            }
        }

        float _t, _noteAt, _welcomeAt, _readyAt;
        Vector2 _handAt, _swipeAt;
        float _swipeDir = 1f;

        /// <summary>A point on the screen in this layer's coordinates.</summary>
        public Vector2 Local(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, screen, UiCamera, out var p);
            return p;
        }

        /// <summary>The rectangle of a UI element in this layer's coordinates.</summary>
        public Rect LocalRect(RectTransform target)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 a = Local(RectTransformUtility.WorldToScreenPoint(UiCamera, corners[0]));
            Vector2 b = Local(RectTransformUtility.WorldToScreenPoint(UiCamera, corners[2]));
            return UnityEngine.Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// <summary>Everything off but the layer itself.</summary>
        public void Clear()
        {
            ClearMarks();
            if (Note != null) Note.gameObject.SetActive(false);
            LiftMascot(false);
            if (TapCatcher != null) TapCatcher.gameObject.SetActive(false);
            if (Welcome != null) Welcome.SetActive(false);
            if (Ready != null) Ready.SetActive(false);
        }

        public void ClearMarks()
        {
            if (Hole != null) Hole.gameObject.SetActive(false);
            foreach (var d in DimSides) if (d != null) d.gameObject.SetActive(false);
            foreach (var r in Rings) if (r != null) r.gameObject.SetActive(false);
            if (Hand != null) Hand.gameObject.SetActive(false);
            if (Swipe != null) Swipe.gameObject.SetActive(false);
        }

        /// <summary>Dim the screen but for <paramref name="r"/> (layer coordinates).</summary>
        public void DimAround(Rect r)
        {
            r = UnityEngine.Rect.MinMaxRect(r.xMin - Pad, r.yMin - Pad, r.xMax + Pad, r.yMax + Pad);
            var frame = UnityEngine.Rect.MinMaxRect(r.xMin - Margin, r.yMin - Margin, r.xMax + Margin, r.yMax + Margin);
            Hole.gameObject.SetActive(true);
            Place(Hole, frame);
            var all = Rect.rect;
            float big = Mathf.Max(all.width, all.height) * 2f;   // the dim reaches past the safe area to the screen edges
            Side(0, UnityEngine.Rect.MinMaxRect(frame.xMin - big, frame.yMax, frame.xMax + big, frame.yMax + big));
            Side(1, UnityEngine.Rect.MinMaxRect(frame.xMin - big, frame.yMin - big, frame.xMax + big, frame.yMin));
            Side(2, UnityEngine.Rect.MinMaxRect(frame.xMin - big, frame.yMin, frame.xMin, frame.yMax));
            Side(3, UnityEngine.Rect.MinMaxRect(frame.xMax, frame.yMin, frame.xMax + big, frame.yMax));
        }

        void Side(int i, Rect r)
        {
            if (i >= DimSides.Length || DimSides[i] == null) return;
            DimSides[i].gameObject.SetActive(true);
            Place(DimSides[i], r);
        }

        static void Place(RectTransform rt, Rect r)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = r.center;
            rt.sizeDelta = r.size;
        }

        public void RingAt(int i, Vector2 local)
        {
            if (i >= Rings.Length || Rings[i] == null) return;
            Rings[i].gameObject.SetActive(true);
            Rings[i].anchoredPosition = local;
        }

        /// <summary>The tapping hand with its fingertip on <paramref name="local"/>.</summary>
        public void HandAt(Vector2 local)
        {
            Hand.gameObject.SetActive(true);
            _handAt = local;
        }

        /// <summary>The swipe arrow; <paramref name="left"/> turns it round (a drag to the left brings the right side to the front).</summary>
        public void ShowSwipe(Vector2 local, bool left)
        {
            Swipe.gameObject.SetActive(true);
            _swipeAt = local;
            _swipeDir = left ? -1f : 1f;
            Swipe.localScale = new Vector3(_swipeDir, 1f, 1f);
        }

        /// <summary>The mascot's note; <paramref name="waitsForTap"/> shows "Next" and lets any tap move on.</summary>
        public void ShowNote(string name, string text, bool waitsForTap)
        {
            bool changed = !Note.gameObject.activeSelf || NoteText.text != text;
            Note.gameObject.SetActive(true);
            NoteName.text = name;
            NoteText.text = text;
            NextLabel.gameObject.SetActive(waitsForTap);
            TapCatcher.gameObject.SetActive(waitsForTap);
            if (changed) _noteAt = _t;
            LiftMascot(true);
        }

        void LiftMascot(bool up)
        {
            if (Mascot == null || Mascot.overrideSorting == up) return;
            Mascot.overrideSorting = up;
            if (up)
            {
                var root = Mascot.transform.parent != null ? Mascot.transform.parent.GetComponentInParent<Canvas>() : null;
                Mascot.sortingOrder = (root != null ? root.rootCanvas.sortingOrder : 0) + 1;
            }
        }

        void OnDisable() => LiftMascot(false);

        public void HideNote()
        {
            LiftMascot(false);
            Note.gameObject.SetActive(false);
            TapCatcher.gameObject.SetActive(false);
        }

        public void ShowWelcome(string name, string text)
        {
            Clear();
            Welcome.SetActive(true);
            WelcomeName.text = name;
            WelcomeText.text = text;
            _welcomeAt = _t;
        }

        public void ShowReady() { Clear(); Ready.SetActive(true); _readyAt = _t; }

        /// <summary>Seconds since the ready screen came in (the game slides it in with the board going out).</summary>
        public float ReadyAge => _t - _readyAt;

        static float OutBack(float t) { const float c = 1.7f; float u = t - 1f; return 1f + (c + 1f) * u * u * u + c * u * u; }

        void Update()
        {
            _t += Time.unscaledDeltaTime;
            float u = 2.803f;   // mockup px to canvas units

            // rings pulse: 1 → 1.18 and back, fading to 0.6 at the widest (1.2 s)
            float pulse = 0.5f - 0.5f * Mathf.Cos(_t * Mathf.PI * 2f / 1.2f);
            foreach (var r in Rings)
            {
                if (r == null || !r.gameObject.activeSelf) continue;
                r.localScale = Vector3.one * (1f + 0.18f * pulse);
                var img = r.GetComponent<Image>();
                if (img != null) { var c = img.color; c.a = 1f - 0.4f * pulse; img.color = c; }
            }
            // the hand taps: rests 14,18 px off, presses onto the target at 45 % (1.2 s)
            if (Hand != null && Hand.gameObject.activeSelf)
            {
                float p = Mathf.Repeat(_t, 1.2f) / 1.2f;
                float k = p < 0.45f ? Mathf.SmoothStep(0f, 1f, p / 0.45f) : Mathf.SmoothStep(1f, 0f, (p - 0.45f) / 0.55f);
                Hand.anchoredPosition = _handAt + new Vector2(14f * u, -18f * u) * (1f - k);
                Hand.localScale = Vector3.one * (1f - 0.15f * k);
            }
            // the swipe arrow slides to the right and fades in and out (1.6 s)
            if (Swipe != null && Swipe.gameObject.activeSelf)
            {
                float p = Mathf.Repeat(_t, 1.6f) / 1.6f;
                Swipe.anchoredPosition = _swipeAt + new Vector2(_swipeDir * Mathf.Lerp(-30f, 30f, Mathf.SmoothStep(0f, 1f, p)) * u, 0f);
                var img = Swipe.GetComponent<Image>();
                if (img != null) { var c = img.color; c.a = p < 0.15f ? p / 0.15f : p > 0.85f ? (1f - p) / 0.15f : 1f; img.color = c; }
            }
            // the note pops out of the mascot's corner
            if (Note != null && Note.gameObject.activeSelf)
            {
                float p = Mathf.Clamp01((_t - _noteAt) / 0.35f);
                Note.localScale = Vector3.one * Mathf.LerpUnclamped(0.4f, 1f, OutBack(p));
            }
            if (Welcome != null && Welcome.activeSelf) TickWelcome(_t - _welcomeAt, u);
            if (Ready != null && Ready.activeSelf) TickReady(_t - _readyAt);
        }

        /// <summary>The mascot bursts out of a spark (0-0.9 s), then its greeting pops up and the hint pulses.</summary>
        void TickWelcome(float t, float u)
        {
            float pop = Mathf.Clamp01(t / 0.6f);
            WelcomeMascot.localScale = Vector3.one * Mathf.LerpUnclamped(0.2f, 1f, OutBack(pop));
            float burst = Mathf.Clamp01(t / 0.9f);
            for (int i = 0; i < Rays.Length; i++)
            {
                var r = Rays[i];
                if (r == null) continue;
                float a = i * 30f * Mathf.Deg2Rad;
                float dist = Mathf.Lerp(70f, 130f, burst) * u;
                r.anchoredPosition = WelcomeMascot.anchoredPosition + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * dist;
                r.localRotation = Quaternion.Euler(0f, 0f, -i * 30f);
                var img = r.GetComponent<Image>();
                if (img != null) { var c = img.color; c.a = 1f - burst; img.color = c; }
            }
            if (WelcomeHalo != null) { var c = WelcomeHalo.color; c.a = Mathf.Clamp01(t / 0.5f); WelcomeHalo.color = c; }
            float b = Mathf.Clamp01((t - 0.75f) / 0.35f);
            WelcomeBubble.localScale = Vector3.one * (t < 0.75f ? 0f : Mathf.LerpUnclamped(0.4f, 1f, OutBack(b)));
            var tap = WelcomeTap.color;
            tap.a = t < 1.3f ? 0f : 0.55f + 0.45f * Mathf.Cos((t - 1.3f) * Mathf.PI * 2f / 1.6f);
            WelcomeTap.color = tap;
            if (WelcomeButton != null) WelcomeButton.interactable = t > 1.1f;   // the greeting is read before a tap goes on
        }

        void TickReady(float t)
        {
            float b = Mathf.Clamp01((t - 0.45f) / 0.35f);
            ReadyBubble.localScale = Vector3.one * (t < 0.45f ? 0f : Mathf.LerpUnclamped(0.4f, 1f, OutBack(b)));
            ReadyMascot.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(t * Mathf.PI * 2f / 2.4f));
        }
    }
}
