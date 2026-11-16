using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CircuitCube.UI
{
    /// <summary>
    /// One colour in the lamp tray above Undo/Restart (prefab item, built by Build 1.2.0 FX): a pip per lamp (filled
    /// when lit), the colour's name with lit / total, and for a mixed colour the formula tag above it
    /// (source + source → colour). Pips beyond <see cref="Pips"/> are summed in the label only.
    /// </summary>
    public sealed class LampGroup : MonoBehaviour
    {
        public Image[] Pips = new Image[0];
        public Sprite PipOn, PipOff;
        public TMP_Text Label;
        public GameObject Formula;
        public Image FormulaA, FormulaB, FormulaResult;
        public CanvasGroup Group;

        int _lit = -1, _total = -1;

        public void Set(Color color, string name, int lit, int total, Color mixA, Color mixB, bool mixed)
        {
            if (lit == _lit && total == _total && Label.text.Length > 0 && Formula.activeSelf == mixed) return;
            _lit = lit; _total = total;
            int shown = Mathf.Min(total, Pips.Length);
            for (int i = 0; i < Pips.Length; i++)
            {
                var p = Pips[i];
                bool on = i < shown;
                p.gameObject.SetActive(on);
                if (!on) continue;
                bool litPip = i < lit;
                p.sprite = litPip ? PipOn : PipOff;
                p.color = litPip ? color : new Color(color.r, color.g, color.b, 0.6f);
            }
            Label.text = string.Concat(name, " ", lit.ToString(), "/", total.ToString());
            Formula.SetActive(mixed);
            if (mixed) { FormulaA.color = mixA; FormulaB.color = mixB; FormulaResult.color = color; }
            if (Group != null) Group.alpha = lit >= total ? 0.55f : 1f;
        }

        public void Forget() { _lit = _total = -1; }
    }
}
