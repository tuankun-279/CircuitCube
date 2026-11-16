using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CircuitCube.UI
{
    /// <summary>
    /// One colour in the lamp tray above Undo/Restart (prefab item, built by Build 1.2.0 FX): a pip per lamp (filled
    /// when lit), the colour's name with lit / total, and for a mixed colour the formula tag above it
    /// (source + source → colour). With more lamps than <see cref="Pips"/>, the pips show the lit share.
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
                bool litPip = i < (total <= Pips.Length ? lit : Mathf.RoundToInt(lit * (float)shown / total));   // more lamps than pips: pips show the share
                p.sprite = litPip ? PipOn : PipOff;
                p.color = litPip ? color : new Color(color.r, color.g, color.b, 0.6f);
            }
            Label.text = string.Concat(name, " ", lit.ToString(), "/", total.ToString());
            Formula.SetActive(mixed);
            if (mixed) { FormulaA.color = mixA; FormulaB.color = mixB; FormulaResult.color = color; }
            if (Group != null) Group.alpha = lit >= total ? 0.55f : 1f;
        }

        public void Forget() { _lit = _total = -1; }

        // the tray shares its width between the colours: the pip row shrinks to fit a narrow group
        void OnRectTransformDimensionsChange() => Fit();
        void OnEnable() => Fit();

        void Fit()
        {
            if (Pips.Length == 0 || Pips[0] == null) return;
            var row = (RectTransform)Pips[0].transform.parent;
            float room = ((RectTransform)transform).rect.width - 20f;
            float need = row.rect.width;
            float k = need > 0f ? Mathf.Clamp01(room / need) : 1f;
            row.localScale = new Vector3(k, k, 1f);
        }
    }
}
