using CircuitCube.Game;
using TMPro;
using UnityEngine;

namespace CircuitCube.UI
{
    /// <summary>
    /// Translates a fixed text placed in a prefab (a button label, a heading). The English typed in the prefab is the
    /// key (see <see cref="Loc"/>). Texts the game fills in from code are left alone: the component only rewrites the
    /// text while it still shows the prefab's own wording.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocText : MonoBehaviour
    {
        TMP_Text _text;
        string _source, _shown;

        void Awake()
        {
            _text = GetComponent<TMP_Text>();
            _source = _text.text;
            _shown = _source;
            Loc.Changed += Apply;
            Apply();
        }

        // Shown again: translate again. A label that got the English at start-up (its first Apply came before the
        // saved language was known, or failed) is put right the next time its screen opens.
        void OnEnable() { if (_text != null) Apply(); }

        void OnDestroy() => Loc.Changed -= Apply;

        void Apply()
        {
            if (_text == null) return;
            string now = _text.text;
            if (now != _shown && now != _source) return;   // code wrote its own text since
            _shown = Loc.T(_source);
            if (now != _shown) _text.text = _shown;
        }
    }
}
