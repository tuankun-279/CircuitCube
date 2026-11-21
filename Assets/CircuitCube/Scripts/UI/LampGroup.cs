using CircuitCube.Game;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CircuitCube.UI
{
    /// <summary>
    /// One colour in the lamp tray above Undo/Restart (prefab item, built by Build 1.2.0 FX), as in the approved
    /// mockup (option D): a card tinted in the colour, a bar split into one cell per lamp, or one continuous bar past
    /// <see cref="Segs"/> lamps; the colour's symbol (for colour-blind players), name and lit / total; and for a mixed
    /// colour the formula tag above it (source + source → colour). Once every lamp of the colour is lit the border
    /// turns solid and glows, with a tick in front of the name.
    /// </summary>
    public sealed class LampGroup : MonoBehaviour
    {
        [Header("Bar split into cells (up to one cell per lamp)")]
        public GameObject SegRow;
        public Image[] Segs = new Image[0];
        [Tooltip("Glow behind each lit cell (child of the cell).")]
        public Image[] SegGlows = new Image[0];

        [Header("Continuous bar (more lamps than cells)")]
        public GameObject Bar;
        public Image BarTrack;
        public RectTransform BarFill;
        public Image BarFillImage, BarGlow;

        [Header("Card tinted in the colour")]
        public Image Card;
        public Image Border;
        [Tooltip("Ring and glow round the card once the colour is done.")]
        public Image Done;
        [Tooltip("Symbol per colour set, index 1..7 (red, blue, purple, yellow, orange, green, white).")]
        public Sprite[] Symbols = new Sprite[8];
        public Image Symbol;

        [Header("Name, tick, formula")]
        public TMP_Text Label;
        public Image Check;
        public GameObject Formula;
        public Image FormulaA, FormulaB, FormulaResult;
        public CanvasGroup Group;

        int _lit = -1, _total = -1;
        Color _color, _a, _b;
        string _name;
        bool _mixed;
        int _set;

        void OnEnable() => Themes.Changed += Reapply;
        void OnDisable() => Themes.Changed -= Reapply;

        void Reapply()
        {
            if (_total < 0) return;
            int lit = _lit, total = _total;
            _lit = _total = -1;
            Set(_color, _name, lit, total, _a, _b, _mixed, _set);
        }

        public void Set(Color color, string name, int lit, int total, Color mixA, Color mixB, bool mixed, int set = 0)
        {
            if (lit == _lit && total == _total && name == _name && color == _color && Formula.activeSelf == mixed) return;
            _lit = lit; _total = total; _color = color; _name = name; _a = mixA; _b = mixB; _mixed = mixed; _set = set;
            bool dark = Themes.IsDark;
            bool done = total > 0 && lit >= total;
            var off = Fade(color, 0.22f);
            Card.color = Fade(color, done ? (dark ? 0.26f : 0.30f) : (dark ? 0.16f : 0.20f));
            Border.color = done ? color : Fade(color, 0.45f);
            Done.gameObject.SetActive(done);
            Done.color = color;
            var glow = color; glow.a = 0.67f;
            bool cells = total <= Segs.Length;
            SegRow.SetActive(cells);
            Bar.SetActive(!cells);
            if (cells)
            {
                for (int i = 0; i < Segs.Length; i++)
                {
                    bool on = i < total;
                    Segs[i].gameObject.SetActive(on);
                    if (!on) continue;
                    bool l = i < lit;
                    Segs[i].color = l ? color : off;
                    if (i < SegGlows.Length && SegGlows[i] != null)
                    {
                        SegGlows[i].gameObject.SetActive(l);
                        SegGlows[i].color = glow;
                    }
                }
            }
            else
            {
                float p = total > 0 ? Mathf.Clamp01(lit / (float)total) : 0f;
                BarTrack.color = off;
                BarFill.anchorMax = new Vector2(p, 1f);
                BarFillImage.color = color;
                BarFillImage.enabled = p > 0f;
                var g = color; g.a = 1f;
                BarGlow.color = g;
                BarGlow.enabled = p > 0f;
            }
            Check.gameObject.SetActive(done);
            Check.color = color;
            var sym = set > 0 && set < Symbols.Length ? Symbols[set] : null;
            Symbol.gameObject.SetActive(sym != null);
            Symbol.sprite = sym;
            Symbol.color = color;
            // dark: the name in the colour, the count white; light: both in the text colour (mockup D)
            Label.text = dark
                ? string.Concat("<color=#", ColorUtility.ToHtmlStringRGB(color), ">", name, "</color> <color=#FFFFFF><b>", lit.ToString(), "/", total.ToString(), "</b></color>")
                : string.Concat(name, " <b>", lit.ToString(), "/", total.ToString(), "</b>");
            Formula.SetActive(mixed);
            if (mixed) { FormulaA.color = mixA; FormulaB.color = mixB; FormulaResult.color = color; }
            if (Group != null) Group.alpha = 1f;
        }

        static Color Fade(Color c, float a) { c.a = a; return c; }

        public void Forget() { _lit = _total = -1; }
    }
}
