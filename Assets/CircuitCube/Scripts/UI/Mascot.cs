using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CircuitCube.UI
{
    /// <summary>
    /// The mascot in the play screen's top-right corner (prefab part of the HUD). It floats and blinks while you play,
    /// hops when a lamp lights, tilts and sweats when you are stuck, wiggles with star eyes near the end, and now and
    /// then says a short line in the bubble to its left. Sprites come from the prefab; all motion is code-driven
    /// transforms. Allocation free per frame.
    /// </summary>
    public sealed class Mascot : MonoBehaviour
    {
        public enum Mood { Idle, Happy, Think, Excited }

        public RectTransform Body;
        public Image Face;
        public Sprite Idle, Blink, Happy, Think, Excited;
        public Image Drop;
        [Tooltip("Small sparkles thrown out on a happy hop.")]
        public Image[] Sparks = new Image[0];
        public RectTransform Bubble;
        public TMP_Text BubbleText;
        [Tooltip("Seconds a line stays up.")]
        public float LineSeconds = 3.2f;
        [Tooltip("Seconds a mood lasts before the mascot goes back to idle.")]
        public float MoodSeconds = 2.4f;

        Mood _mood;
        float _moodStart, _lineStart = -100f, _nextBlink;
        bool _blinking;
        Vector2 _home;
        static readonly Vector2[] SparkDirs =
        {
            new Vector2(-0.8f, 0.7f), new Vector2(0.8f, 0.8f), new Vector2(-1f, 0.05f), new Vector2(1f, 0f), new Vector2(0f, 1f),
        };

        void Awake()
        {
            if (Body != null) _home = Body.anchoredPosition;
            Reset_();
        }

        void OnEnable() => Reset_();

        void Reset_()
        {
            _mood = Mood.Idle;
            _lineStart = -100f;
            if (Bubble != null) Bubble.gameObject.SetActive(false);
            if (Drop != null) Drop.gameObject.SetActive(false);
            foreach (var s in Sparks) if (s != null) s.gameObject.SetActive(false);
            if (Face != null) Face.sprite = Idle;
            _nextBlink = Clock.Now + 3f;
        }

        /// <summary>Change mood; a non-empty <paramref name="line"/> shows in the bubble.</summary>
        public void Say(Mood mood, string line)
        {
            _mood = mood;
            _moodStart = Clock.Now;
            if (Face != null) Face.sprite = SpriteFor(mood);
            if (Drop != null) Drop.gameObject.SetActive(mood == Mood.Think);
            bool sparks = mood == Mood.Happy || mood == Mood.Excited;
            for (int i = 0; i < Sparks.Length; i++) if (Sparks[i] != null) Sparks[i].gameObject.SetActive(sparks);
            if (!string.IsNullOrEmpty(line) && Bubble != null)
            {
                BubbleText.text = line;
                Bubble.gameObject.SetActive(true);
                _lineStart = Clock.Now;
            }
        }

        public bool Talking => Bubble != null && Bubble.gameObject.activeSelf;

        Sprite SpriteFor(Mood m) => m == Mood.Happy ? Happy : m == Mood.Think ? Think : m == Mood.Excited ? Excited : Idle;

        void Update()
        {
            if (Body == null) return;
            float now = Clock.Now, t = now - _moodStart;
            if (_mood != Mood.Idle && t > MoodSeconds)
            {
                _mood = Mood.Idle;
                if (Face != null) Face.sprite = Idle;
                if (Drop != null) Drop.gameObject.SetActive(false);
            }

            // body motion per mood
            Vector2 pos = _home;
            float rot = 0f, sx = 1f, sy = 1f;
            switch (_mood)
            {
                case Mood.Happy:
                {
                    float p = Mathf.Clamp01(t / 0.5f);
                    float hop = Mathf.Sin(p * Mathf.PI);
                    pos.y += 22f * hop;
                    sx = 1f + 0.08f * hop; sy = 1f - 0.06f * hop;
                    break;
                }
                case Mood.Think:
                    rot = -7f * Mathf.Sin(t * Mathf.PI / 0.6f) ;
                    break;
                case Mood.Excited:
                {
                    float w = t < 1.05f ? Mathf.Sin(t * Mathf.PI * 2f / 0.35f) : 0f;
                    rot = 8f * w; sx = sy = 1f + 0.06f * Mathf.Abs(w);
                    break;
                }
                default:
                    pos.y += 6f * Mathf.Sin(now * Mathf.PI * 2f / 2.4f);
                    break;
            }
            Body.anchoredPosition = pos;
            Body.localRotation = Quaternion.Euler(0f, 0f, rot);
            Body.localScale = new Vector3(sx, sy, 1f);

            // blink while idle
            if (_mood == Mood.Idle && Face != null)
            {
                if (!_blinking && now >= _nextBlink) { _blinking = true; Face.sprite = Blink; }
                else if (_blinking && now >= _nextBlink + 0.14f) { _blinking = false; Face.sprite = Idle; _nextBlink = now + Random.Range(2.5f, 5f); }
            }

            // sweat drop slides down and fades
            if (Drop != null && Drop.gameObject.activeSelf)
            {
                float d = (t % 1.2f) / 1.2f;
                Drop.rectTransform.anchoredPosition = new Vector2(Drop.rectTransform.anchoredPosition.x, 26f - 22f * d);
                var c = Drop.color; c.a = d < 0.3f ? d / 0.3f : 1f - (d - 0.3f) / 0.7f; Drop.color = c;
            }

            // sparkles burst once per happy or excited mood
            if (Sparks.Length > 0 && Sparks[0] != null && Sparks[0].gameObject.activeSelf)
            {
                float p = Mathf.Clamp01(t / 0.6f);
                for (int i = 0; i < Sparks.Length; i++)
                {
                    var s = Sparks[i];
                    s.rectTransform.anchoredPosition = SparkDirs[i % SparkDirs.Length] * (70f * p);
                    var c = s.color; c.a = 1f - p; s.color = c;
                    if (p >= 1f) s.gameObject.SetActive(false);
                }
            }

            // speech bubble: pops in, stays, fades
            if (Bubble != null && Bubble.gameObject.activeSelf)
            {
                float b = now - _lineStart;
                float k = b < 0.3f ? OutBack(b / 0.3f) : 1f;
                Bubble.localScale = Vector3.one * Mathf.LerpUnclamped(0.4f, 1f, k);
                if (b > LineSeconds) Bubble.gameObject.SetActive(false);
            }
        }

        static float OutBack(float t) { const float c = 1.7f; float u = t - 1f; return 1f + (c + 1f) * u * u * u + c * u * u; }
    }
}
