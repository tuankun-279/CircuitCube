using CircuitCube.Game;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CircuitCube.UI
{
    /// <summary>
    /// One colour in the lamp tray above Undo/Restart (prefab item, built by Build 1.2.0 FX), as in the approved
    /// mockup (option A): a bar split into one cell per lamp, or one continuous bar past <see cref="Segs"/> lamps;
    /// the colour's name with lit / total (a tick once done, then the group dims); and for a mixed colour the formula
    /// tag above it (source + source → colour).
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

        void OnEnable() => Themes.Changed += Reapply;
        void OnDisable() => Themes.Changed -= Reapply;

        void Reapply()
        {
            if (_total < 0) return;
            int lit = _lit, total = _total;
            _lit = _total = -1;
            Set(_color, _name, lit, total, _a, _b, _mixed);
        }

        public void Set(Color color, string name, int lit, int total, Color mixA, Color mixB, bool mixed)
        {
            if (lit == _lit && total == _total && name == _name && color == _color && Formula.activeSelf == mixed) return;
            _lit = lit; _total = total; _color = color; _name = name; _a = mixA; _b = mixB; _mixed = mixed;
            var track = Themes.Current != null ? Themes.Current.Get(UiRole.Track) : (Color)new Color32(42, 49, 80, 255);
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
                    Segs[i].color = l ? color : track;
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
                BarTrack.color = track;
                BarFill.anchorMax = new Vector2(p, 1f);
                BarFillImage.color = color;
                BarFillImage.enabled = p > 0f;
                var g = color; g.a = 1f;
                BarGlow.color = g;
                BarGlow.enabled = p > 0f;
            }
            bool done = total > 0 && lit >= total;
            Check.gameObject.SetActive(done);
            Check.color = CubeColors.Energy(6);
            Label.text = string.Concat(name, " ", lit.ToString(), "/", total.ToString());
            Formula.SetActive(mixed);
            if (mixed) { FormulaA.color = mixA; FormulaB.color = mixB; FormulaResult.color = color; }
            if (Group != null) Group.alpha = done ? 0.5f : 1f;
        }

        public void Forget() { _lit = _total = -1; }
    }
}
