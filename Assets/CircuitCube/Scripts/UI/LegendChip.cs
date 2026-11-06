using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.UI
{
    /// <summary>Colour key in the HUD (prefab LegendChip): a tinted dot and the colour's name.</summary>
    public sealed class LegendChip : MonoBehaviour
    {
        public Image Dot;
        public Text Label;

        public void Set(Color color, string name) { Dot.color = color; Label.text = name; }
    }
}
