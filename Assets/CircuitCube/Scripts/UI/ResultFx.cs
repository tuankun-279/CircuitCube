using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CircuitCube.UI
{
    /// <summary>
    /// The win effects over the result card (prefab part of UI.prefab, built by Tools/Circuit Cube/Art V2/Build 1.2.0 FX):
    /// <list type="bullet">
    /// <item>Expert: a burst where the box breaks, then the letters E-X-P-E-R-T fly out one by one and curve into the
    /// card, where the neon word replaces the stars;</item>
    /// <item>Expert streak: a rain of comets, six of them landing as the letters with a light shake each, while a
    /// beam runs along the mascot's lines and turns them gold from the bottom up;</item>
    /// <item>three-star streak: light rises from the lamps into three small constellations that drop into the star
    /// slots.</item>
    /// </list>
    /// Every sprite is set in the prefab; this only moves, scales and fades them. Runs on unscaled time.
    /// </summary>
    public sealed class ResultFx : MonoBehaviour
    {
        [Header("Layers")]
        [Tooltip("Full-screen layer above the cards that holds every flying piece.")]
        public RectTransform Layer;
        [Tooltip("Moved a few units for the light shake when a comet lands (the result card and the board view).")]
        public RectTransform ShakeRoot;

        [Header("Expert word")]
        [Tooltip("The six letters flying out of the box.")]
        public Image[] FlyLetters = new Image[6];
        [Tooltip("White-hot copy of each flying letter (child of it), faded out as the letter cools.")]
        public Image[] HotLetters = new Image[6];
        [Tooltip("Thin ring that pings out of each letter as it lands.")]
        public Image[] Pings = new Image[6];
        [Tooltip("The neon word in the result card, in place of the star row.")]
        public RectTransform Word;
        public Image[] WordLetters = new Image[6];
        [Tooltip("Light streak swept across the word (inside a RectMask2D the size of the word).")]
        public RectTransform Shine;

        [Header("Burst")]
        public Image Core, Ring;
        public Image[] Sparks = new Image[0];

        [Header("Comets")]
        [Tooltip("Comet sprites pointing right; the first six land as the letters, the rest only rain.")]
        public Image[] Comets = new Image[0];
        [Tooltip("Gold mascot laid over the card's mascot: body, gold frame and gold face, filled from the bottom up as the beams run.")]
        public Image GoldBody, GoldFrame, GoldFace;
        [Tooltip("Two glow dots that run up both sides of the mascot frame, then over its face.")]
        public Image[] Beams = new Image[2];

        [Header("Constellations")]
        public Image[] Dots = new Image[0];
        public Image[] Lines = new Image[0];

        [Header("Praise")]
        public TMP_Text Praise;

        public bool Busy { get; private set; }
        Coroutine _shineLoop;

        static float T => Clock.Now;

        void OnDisable() { Busy = false; _shineLoop = null; }

        public void HideAll()
        {
            StopAllCoroutines();
            _shineLoop = null;
            Busy = false;
            foreach (var g in Each()) if (g != null) g.gameObject.SetActive(false);
            if (Word != null) Word.gameObject.SetActive(false);
            if (Praise != null) Praise.gameObject.SetActive(false);
            if (Shine != null) Shine.gameObject.SetActive(false);
            if (ShakeRoot != null) ShakeRoot.anchoredPosition = Vector2.zero;
        }

        System.Collections.Generic.IEnumerable<Graphic> Each()
        {
            foreach (var x in FlyLetters) yield return x;
            foreach (var x in Pings) yield return x;
            foreach (var x in WordLetters) yield return x;
            yield return Core; yield return Ring;
            foreach (var x in Sparks) yield return x;
            foreach (var x in Comets) yield return x;
            yield return GoldBody; yield return GoldFrame; yield return GoldFace;
            foreach (var x in Beams) yield return x;
            foreach (var x in Dots) yield return x;
            foreach (var x in Lines) yield return x;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>A world point (on the layer's plane) as the layer's local position.</summary>
        Vector2 Local(Vector3 world) => Layer.InverseTransformPoint(world);

        static float Smooth(float p) => p * p * (3f - 2f * p);

        /// <summary>CSS cubic-bezier(x1, y1, x2, y2) easing, so the motion matches the approved web mockup.</summary>
        static float Bezier(float x1, float y1, float x2, float y2, float x)
        {
            if (x <= 0f) return 0f;
            if (x >= 1f) return 1f;
            float t = x;
            for (int i = 0; i < 8; i++)
            {
                float u = 1f - t;
                float bx = 3f * u * u * t * x1 + 3f * u * t * t * x2 + t * t * t - x;
                float dx = 3f * u * u * x1 + 6f * u * t * (x2 - x1) + 3f * t * t * (1f - x2);
                if (Mathf.Abs(bx) < 1e-5f || Mathf.Abs(dx) < 1e-6f) break;
                t = Mathf.Clamp01(t - bx / dx);
            }
            float v = 1f - t;
            return 3f * v * v * t * y1 + 3f * v * t * t * y2 + t * t * t;
        }

        static float EaseOut(float p) => Bezier(0f, 0f, 0.58f, 1f, p);
        static float OutBack(float t) { const float c = 1.7f; float u = t - 1f; return 1f + (c + 1f) * u * u * u + c * u * u; }

        static IEnumerator Wait(float s) { float end = T + s; while (T < end) yield return null; }

        static void SetAlpha(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }

        IEnumerator Fade(Graphic g, float from, float to, float dur)
        {
            float t0 = T;
            while (true)
            {
                float p = Mathf.Clamp01((T - t0) / dur);
                SetAlpha(g, Mathf.Lerp(from, to, p));
                if (p >= 1f) yield break;
                yield return null;
            }
        }

        // ------------------------------------------------------------------ expert word

        /// <summary>The word in the card: letters hidden until they land (or shown at once).</summary>
        public void PrepareWord(bool shown)
        {
            Word.gameObject.SetActive(true);
            foreach (var l in WordLetters) { l.gameObject.SetActive(shown); l.transform.localScale = Vector3.one; SetAlpha(l, 1f); }
            Shine.gameObject.SetActive(false);
        }

        void LandLetter(int i, Action<int> landed)
        {
            var l = WordLetters[i];
            l.gameObject.SetActive(true);
            l.transform.localScale = Vector3.one;
            StartCoroutine(Flicker(l));
            if (i < Pings.Length && Pings[i] != null) StartCoroutine(Ping(Pings[i], l.rectTransform));
            landed?.Invoke(i);
        }

        /// <summary>Neon tube catching: 0.3, 1, 0.5, then full, in 0.1 s steps.</summary>
        IEnumerator Flicker(Image l)
        {
            float t0 = T;
            while (true)
            {
                float t = T - t0;
                SetAlpha(l, t < 0.1f ? 0.3f : t < 0.2f ? 1f : t < 0.3f ? 0.5f : 1f);
                if (t >= 0.3f) yield break;
                yield return null;
            }
        }

        IEnumerator Ping(Image ring, RectTransform at)
        {
            var rt = ring.rectTransform;
            ring.gameObject.SetActive(true);
            rt.anchoredPosition = Local(at.position);
            float t0 = T;
            while (true)
            {
                float p = Mathf.Clamp01((T - t0) / 0.5f), e = EaseOut(p);
                rt.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.8f, e);
                SetAlpha(ring, Mathf.Lerp(0.9f, 0f, e));
                if (p >= 1f) break;
                yield return null;
            }
            ring.gameObject.SetActive(false);
        }

        /// <summary>A comet lands as a letter: in from 2.2 times its size (cubic-bezier(.2,1.6,.4,1), 0.35 s).</summary>
        void LandPop(int i, Action<int> landed)
        {
            var l = WordLetters[i];
            l.gameObject.SetActive(true);
            SetAlpha(l, 1f);
            StartCoroutine(Pop(l.transform, 2.2f, 0.35f));
            landed?.Invoke(i);
        }

        IEnumerator Pop(Transform t, float from, float dur)
        {
            float t0 = T;
            while (true)
            {
                float p = Mathf.Clamp01((T - t0) / dur);
                t.localScale = Vector3.one * Mathf.LerpUnclamped(from, 1f, Bezier(0.2f, 1.6f, 0.4f, 1f, p));
                if (p >= 1f) yield break;
                yield return null;
            }
        }

        /// <summary>Sweep the light over the word now, then every few seconds while the card is up.</summary>
        public void StartShine()
        {
            if (_shineLoop != null) StopCoroutine(_shineLoop);
            _shineLoop = StartCoroutine(ShineLoop());
        }

        IEnumerator ShineLoop()
        {
            var mask = (RectTransform)Shine.parent;
            float w = mask.rect.width, sw = Shine.rect.width;
            Shine.gameObject.SetActive(true);
            float from = -w * 0.5f - sw * 0.5f, to = w * 0.7f + sw * 0.5f;   // mockup: left -50px to 120%
            while (true)
            {
                float t0 = T;
                while (T - t0 < 2.6f)
                {
                    float p = Mathf.Clamp01((T - t0) / 1.17f);
                    Shine.anchoredPosition = new Vector2(Mathf.Lerp(from, to, Bezier(0.42f, 0f, 0.58f, 1f, p)), 0f);
                    yield return null;
                }
            }
        }

        // ------------------------------------------------------------------ 1) one Expert

        /// <summary>
        /// The box broke at <paramref name="core"/> (world point): a burst there, then the letters leave one by one
        /// <paramref name="gap"/> seconds apart and curve into the word slots, which follow the card while it slides in.
        /// </summary>
        public void PlayExpert(Vector3 core, float gap, float flight, Action<int> landed, Action done)
        {
            HideAll();
            Busy = true;
            PrepareWord(false);
            StartCoroutine(Burst(Local(core)));
            StartCoroutine(Letters(Local(core), gap, flight, landed, done));
        }

        IEnumerator Burst(Vector2 at)
        {
            Core.gameObject.SetActive(true); Ring.gameObject.SetActive(true);
            var cr = Core.rectTransform; var rr = Ring.rectTransform;
            cr.anchoredPosition = at; rr.anchoredPosition = at;
            SetAlpha(Ring, 0f);
            var dirs = new Vector2[Sparks.Length];
            for (int i = 0; i < Sparks.Length; i++)
            {
                float a = i * Mathf.PI * 2f / Sparks.Length;
                dirs[i] = new Vector2(Mathf.Cos(a), -Mathf.Sin(a)) * MockupPx(120f);
                Sparks[i].gameObject.SetActive(true);
                Sparks[i].rectTransform.anchoredPosition = at;
                SetAlpha(Sparks[i], 0f);
            }
            float t0 = T;
            while (true)
            {
                float t = T - t0;
                // core: cubic-bezier(.2,.8,.3,1) per keyframe segment, 0 -> 35 % grows to 2.4, then to 3.4 while fading
                float pc = Mathf.Clamp01(t / 0.42f);
                if (pc < 0.35f) { float e = Bezier(0.2f, 0.8f, 0.3f, 1f, pc / 0.35f); cr.localScale = Vector3.one * Mathf.Lerp(0.2f, 2.4f, e); SetAlpha(Core, 1f); }
                else { float e = Bezier(0.2f, 0.8f, 0.3f, 1f, (pc - 0.35f) / 0.65f); cr.localScale = Vector3.one * Mathf.Lerp(2.4f, 3.4f, e); SetAlpha(Core, 1f - e); }
                // ring and dots start 0.02 s later, ease-out over 0.45 s
                float pr = Mathf.Clamp01((t - 0.02f) / 0.45f), er = EaseOut(pr);
                bool started = t >= 0.02f;
                rr.localScale = Vector3.one * Mathf.Lerp(0.3f, 6f, er);
                SetAlpha(Ring, started ? 1f - er : 0f);
                for (int i = 0; i < Sparks.Length; i++)
                {
                    var s = Sparks[i].rectTransform;
                    s.anchoredPosition = at + dirs[i] * er;
                    s.localScale = Vector3.one * Mathf.Lerp(1f, 0.4f, er);
                    SetAlpha(Sparks[i], started ? 1f - er : 0f);
                }
                if (pc >= 1f && pr >= 1f) break;
                yield return null;
            }
            Core.gameObject.SetActive(false); Ring.gameObject.SetActive(false);
            foreach (var s in Sparks) s.gameObject.SetActive(false);
        }

        /// <summary>A length in the 390 px wide mockup phone, in this layer's units (card 314 px = 880 units).</summary>
        static float MockupPx(float px) => px * (880f / 314f);

        IEnumerator Letters(Vector2 core, float gap, float flight, Action<int> landed, Action done)
        {
            int n = FlyLetters.Length;
            int left = n;
            yield return Wait(0.05f);
            StartCoroutine(ShineAt(1.85f));
            for (int i = 0; i < n; i++)
            {
                int k = i;
                StartCoroutine(FlyLetter(k, core, flight, () => { LandLetter(k, landed); left--; }));
                if (i < n - 1) yield return Wait(gap);
            }
            while (left > 0) yield return null;
            Busy = false;
            done?.Invoke();
        }

        IEnumerator ShineAt(float delay) { yield return Wait(delay); StartShine(); }

        // each letter rides out with a piece of the box (mockup angles, CSS degrees, y down)
        static readonly float[] LetterAngles = { -150f, -110f, -70f, -30f, 200f, -10f };

        /// <summary>
        /// The mockup's flight, key for key: from a point 40 px off the blast, 28 % of the time out 115 px
        /// (cubic-bezier(.1,.8,.3,1), scale .5 to 1.3, turn -90 to 8 degrees, white-hot), then into the slot
        /// (cubic-bezier(.5,0,.3,1.35), a small overshoot, scale 1.3 to 1, turn 8 to 0, cooling down).
        /// </summary>
        IEnumerator FlyLetter(int i, Vector2 core, float flight, Action arrive)
        {
            var img = FlyLetters[i];
            var hot = i < HotLetters.Length ? HotLetters[i] : null;
            var rt = img.rectTransform;
            img.gameObject.SetActive(true);
            if (hot != null) hot.gameObject.SetActive(true);
            SetAlpha(img, 1f);
            float a = LetterAngles[i % LetterAngles.Length] * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(a), -Mathf.Sin(a));
            Vector2 start = core + dir * MockupPx(40f), burst = start + dir * MockupPx(115f);
            var slot = WordLetters[i].rectTransform;
            float t0 = T;
            while (true)
            {
                float p = Mathf.Clamp01((T - t0) / flight);
                Vector2 to = Local(slot.position);   // the slot rides up with the card
                float scale, rot, heat;
                if (p < 0.28f)
                {
                    float e = Bezier(0.1f, 0.8f, 0.3f, 1f, p / 0.28f);
                    rt.anchoredPosition = Vector2.LerpUnclamped(start, burst, e);
                    scale = Mathf.LerpUnclamped(0.5f, 1.3f, e); rot = Mathf.LerpUnclamped(-90f, 8f, e); heat = Mathf.LerpUnclamped(1f, 0.5f, e);
                }
                else
                {
                    float e = Bezier(0.5f, 0f, 0.3f, 1.35f, (p - 0.28f) / 0.72f);
                    rt.anchoredPosition = Vector2.LerpUnclamped(burst, to, e);
                    scale = Mathf.LerpUnclamped(1.3f, 1f, e); rot = Mathf.LerpUnclamped(8f, 0f, e); heat = Mathf.Clamp01(Mathf.LerpUnclamped(0.5f, 0f, e));
                }
                rt.localScale = Vector3.one * scale;
                rt.localRotation = Quaternion.Euler(0f, 0f, -rot);   // CSS turns clockwise
                if (hot != null) SetAlpha(hot, heat);
                if (p >= 1f) break;
                yield return null;
            }
            img.gameObject.SetActive(false);
            arrive();
        }

        // ------------------------------------------------------------------ 2) Expert streak: comet rain

        /// <summary>
        /// Comets rain over the card; the first six land on the word slots one by one (a light shake each) while the
        /// beam turns the mascot gold. <paramref name="mascot"/> is the card's mascot frame.
        /// </summary>
        public void PlayComets(RectTransform mascot, Action<int> landed, Action done)
        {
            HideAll();
            Busy = true;
            PrepareWord(false);
            StartCoroutine(Comets_(mascot, landed, done));
        }

        IEnumerator Comets_(RectTransform mascot, Action<int> landed, Action done)
        {
            var rect = Layer.rect;
            int big = Mathf.Min(6, Comets.Length), small = Comets.Length - big;
            for (int k = 0; k < small; k++)
            {
                var c = Comets[big + k];
                float x0 = Mathf.Lerp(rect.xMin + 80f, rect.xMax + 340f, ((k * 37) % 11) / 10f);
                var from = new Vector2(x0, rect.yMax + 60f);
                var to = from + new Vector2(-560f, -1150f);
                StartCoroutine(Streak(c, from, to, 0.2f + k * 0.22f, 0.8f, 0.45f, null));
            }
            int left = big;
            for (int i = 0; i < big; i++)
            {
                int k = i;
                var slot = WordLetters[k].rectTransform;
                Vector2 to = Local(slot.position);
                var from = to + new Vector2(420f, rect.yMax - to.y + 120f);
                StartCoroutine(Streak(Comets[k], from, to, 0.5f + k * 0.3f, 0.45f, 1f, () =>
                {
                    LandPop(k, landed);
                    StartCoroutine(Shake(4f, 0.22f));
                    left--;
                }));
            }
            StartCoroutine(GoldTrace(mascot, 0.6f));
            while (left > 0) yield return null;
            yield return Wait(0.2f);
            StartShine();
            Busy = false;
            done?.Invoke();
        }

        IEnumerator Streak(Image c, Vector2 from, Vector2 to, float delay, float dur, float scale, Action hit)
        {
            yield return Wait(delay);
            var rt = c.rectTransform;
            c.gameObject.SetActive(true);
            SetAlpha(c, 1f);
            Vector2 d = to - from;
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            rt.localScale = Vector3.one * scale;
            float t0 = T;
            while (true)
            {
                float p = Mathf.Clamp01((T - t0) / dur);
                float e = hit != null ? p * p : p;          // landing comets speed up into the hit
                rt.anchoredPosition = Vector2.Lerp(from, to, e);
                if (p >= 1f) break;
                yield return null;
            }
            c.gameObject.SetActive(false);
            hit?.Invoke();
        }

        IEnumerator Shake(float amount, float dur)
        {
            if (ShakeRoot == null) yield break;
            float t0 = T;
            while (T - t0 < dur)
            {
                float k = 1f - (T - t0) / dur;
                ShakeRoot.anchoredPosition = new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * amount * k;
                yield return null;
            }
            ShakeRoot.anchoredPosition = Vector2.zero;
        }

        /// <summary>Two beams run up the sides of the mascot frame and over its face, filling it gold behind them.</summary>
        IEnumerator GoldTrace(RectTransform mascot, float delay)
        {
            yield return Wait(delay);
            if (mascot == null || GoldBody == null) yield break;
            var br = GoldBody.rectTransform;
            br.position = mascot.position;
            float w = mascot.rect.width * mascot.lossyScale.x / Layer.lossyScale.x, h = mascot.rect.height * mascot.lossyScale.y / Layer.lossyScale.y;
            br.sizeDelta = new Vector2(w, h);
            var parts = new[] { GoldBody, GoldFrame, GoldFace };
            foreach (var g in parts)
            {
                g.gameObject.SetActive(true);
                g.type = Image.Type.Filled; g.fillMethod = Image.FillMethod.Vertical; g.fillOrigin = 0; g.fillAmount = 0f;
            }
            foreach (var b in Beams) b.gameObject.SetActive(true);
            Vector2 c = Local(mascot.position);
            float t0 = T, dur = 1.9f;
            while (true)
            {
                float p = Mathf.Clamp01((T - t0) / dur);
                // the beams climb the frame (0..0.75) while gold fills up behind them, then sweep the face once
                float pf = Mathf.Clamp01(p / 0.75f), pc = Mathf.Clamp01((p - 0.75f) / 0.25f);
                float fill = Mathf.Clamp01(pf * 1.02f);
                foreach (var g in parts) g.fillAmount = fill;
                for (int s = 0; s < Beams.Length && s < 2; s++)
                {
                    float side = s == 0 ? -1f : 1f;
                    Vector2 at = p < 0.75f
                        ? PerimeterUp(pf, side, w, h)
                        : new Vector2(side * w * Mathf.Lerp(0.22f, 0.05f, pc), Mathf.Lerp(h * 0.12f, -h * 0.14f, pc));
                    Beams[s].rectTransform.anchoredPosition = c + at;
                    SetAlpha(Beams[s], p < 0.95f ? 1f : (1f - p) / 0.05f);
                }
                if (p >= 1f) break;
                yield return null;
            }
            foreach (var b in Beams) b.gameObject.SetActive(false);
        }

        /// <summary>A point on one half of a rounded square's outline, from the bottom middle (0) to the top middle (1).</summary>
        static Vector2 PerimeterUp(float t, float side, float w, float h)
        {
            float hw = w * 0.5f, hh = h * 0.5f;
            if (t < 0.2f) return new Vector2(side * Mathf.Lerp(0f, hw, t / 0.2f), -hh);
            if (t < 0.8f) return new Vector2(side * hw, Mathf.Lerp(-hh, hh, (t - 0.2f) / 0.6f));
            return new Vector2(side * Mathf.Lerp(hw, 0f, (t - 0.8f) / 0.2f), hh);
        }

        // ------------------------------------------------------------------ 3) three-star streak: constellations

        /// <summary>
        /// Light rises from <paramref name="lamps"/> (world points) into three small five-point stars above the board,
        /// lines join them, then each star drops into its slot in <paramref name="slots"/>; <paramref name="landed"/> is
        /// called with its index.
        /// </summary>
        public void PlayConstellations(Vector3[] lamps, int lampCount, Vector3 boardCenter, RectTransform[] slots, float dropAt, Action<int> landed, Action done)
        {
            HideAll();
            Busy = true;
            StartCoroutine(Const_(lamps, lampCount, Local(boardCenter), slots, dropAt, landed, done));
        }

        IEnumerator Const_(Vector3[] lamps, int lampCount, Vector2 board, RectTransform[] slots, float dropAt, Action<int> landed, Action done)
        {
            float t0 = T;
            var centers = new[] { board + new Vector2(-260f, 470f), board + new Vector2(0f, 560f), board + new Vector2(260f, 470f) };
            const float r = 95f;
            int perStar = 5, stars = Mathf.Min(3, Mathf.Min(Dots.Length / perStar, Lines.Length / perStar));
            for (int s = 0; s < stars; s++)
            {
                var pts = new Vector2[perStar];
                for (int k = 0; k < perStar; k++)
                {
                    float a = (-90f + 72f * k) * Mathf.Deg2Rad;
                    pts[k] = centers[s] + new Vector2(Mathf.Cos(a), -Mathf.Sin(a)) * r;
                }
                for (int k = 0; k < perStar; k++)
                {
                    var dot = Dots[s * perStar + k];
                    Vector2 from = lampCount > 0 ? Local(lamps[(s * perStar + k) % lampCount]) : board;
                    StartCoroutine(Rise(dot, from, pts[k], 0.15f + s * 0.25f + k * 0.05f, 0.9f));
                }
                // star drawn in one stroke: 0 2 4 1 3 0
                int[] order = { 0, 2, 4, 1, 3, 0 };
                for (int k = 0; k < perStar; k++)
                    StartCoroutine(DrawLine(Lines[s * perStar + k], pts[order[k]], pts[order[k + 1]], 1.0f + s * 0.25f + k * 0.12f, 0.18f));
            }
            while (T - t0 < dropAt) yield return null;
            int left = stars;
            for (int s = 0; s < stars; s++)
            {
                int k = s;
                StartCoroutine(Drop(s, perStar, centers[s], slots[s], 0.55f, () => { landed?.Invoke(k); left--; }));
                yield return Wait(0.2f);
            }
            while (left > 0) yield return null;
            Busy = false;
            done?.Invoke();
        }

        IEnumerator Rise(Image dot, Vector2 from, Vector2 to, float delay, float dur)
        {
            yield return Wait(delay);
            var rt = dot.rectTransform;
            dot.gameObject.SetActive(true);
            SetAlpha(dot, 1f);
            float t0 = T;
            while (true)
            {
                float p = Mathf.Clamp01((T - t0) / dur);
                float e = 1f - (1f - p) * (1f - p) * (1f - p);
                rt.anchoredPosition = Vector2.Lerp(from, to, e);
                rt.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, e);
                if (p >= 1f) yield break;
                yield return null;
            }
        }

        IEnumerator DrawLine(Image line, Vector2 a, Vector2 b, float delay, float dur)
        {
            yield return Wait(delay);
            var rt = line.rectTransform;
            line.gameObject.SetActive(true);
            SetAlpha(line, 1f);
            Vector2 d = b - a;
            float len = d.magnitude;
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = a;
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            float t0 = T;
            while (true)
            {
                float p = Mathf.Clamp01((T - t0) / dur);
                rt.sizeDelta = new Vector2(len * p, rt.sizeDelta.y);
                if (p >= 1f) yield break;
                yield return null;
            }
        }

        /// <summary>One constellation shrinks and flies into its star slot.</summary>
        IEnumerator Drop(int s, int perStar, Vector2 center, RectTransform slot, float dur, Action arrive)
        {
            var dots = new RectTransform[perStar];
            var lines = new RectTransform[perStar];
            var dotsFrom = new Vector2[perStar];
            var linesFrom = new Vector2[perStar];
            var linesSize = new Vector2[perStar];
            for (int k = 0; k < perStar; k++)
            {
                dots[k] = Dots[s * perStar + k].rectTransform; dotsFrom[k] = dots[k].anchoredPosition;
                lines[k] = Lines[s * perStar + k].rectTransform; linesFrom[k] = lines[k].anchoredPosition; linesSize[k] = lines[k].sizeDelta;
            }
            float t0 = T;
            while (true)
            {
                float p = Mathf.Clamp01((T - t0) / dur);
                float e = Smooth(p);
                Vector2 to = Local(slot.position);
                float k2 = Mathf.Lerp(1f, 0.15f, e);
                for (int k = 0; k < perStar; k++)
                {
                    dots[k].anchoredPosition = Vector2.Lerp(center, to, e) + (dotsFrom[k] - center) * k2;
                    lines[k].anchoredPosition = Vector2.Lerp(center, to, e) + (linesFrom[k] - center) * k2;
                    lines[k].sizeDelta = new Vector2(linesSize[k].x * k2, linesSize[k].y);
                    SetAlpha(Dots[s * perStar + k], 1f - e * 0.8f);
                    SetAlpha(Lines[s * perStar + k], 1f - e);
                }
                if (p >= 1f) break;
                yield return null;
            }
            for (int k = 0; k < perStar; k++) { Dots[s * perStar + k].gameObject.SetActive(false); Lines[s * perStar + k].gameObject.SetActive(false); }
            arrive();
        }

        // ------------------------------------------------------------------ praise

        public void ShowPraise(string text, float delay)
        {
            if (Praise == null) return;
            StartCoroutine(Praise_(text, delay));
        }

        IEnumerator Praise_(string text, float delay)
        {
            yield return Wait(delay);
            Praise.text = text;
            Praise.gameObject.SetActive(true);
            var t = Praise.transform;
            float t0 = T;
            while (true)
            {
                float p = Mathf.Clamp01((T - t0) / 0.45f);
                t.localScale = Vector3.one * Mathf.LerpUnclamped(0.4f, 1f, OutBack(p));
                t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-6f, 0f, p));
                SetAlpha(Praise, Mathf.Clamp01(p * 3f));
                if (p >= 1f) yield break;
                yield return null;
            }
        }
    }
}
