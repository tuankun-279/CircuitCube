using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.UI
{
    /// <summary>A pop-up card prefab (result, tip or new shape). Plays its "In" clip when opened.</summary>
    public sealed class Card : MonoBehaviour
    {
        public Animation Anim;          // clip "In"
        public Text Title, Body;
        [Tooltip("Small line above the title, e.g. CUBE · LEVEL 12.")]
        public Text Caption;
        [Tooltip("Picture on the card: the mascot on the result card, the shape icon on the new shape card.")]
        public Image Art;
        public StarRow Stars;           // result card only
        [Tooltip("Result card: values of the Moves, Par and Lamps tiles.")]
        public Text[] Stats = new Text[0];
        [Tooltip("Result card: shown when the level is solved at par.")]
        public GameObject Badge;
        public Button Primary, SecondaryA, SecondaryB;
        public Text PrimaryLabel;

        public void Open()
        {
            gameObject.SetActive(true);
            if (Anim != null && Anim.GetClip("In") != null) { Anim.Stop(); Anim.Play("In"); }
        }
    }
}
