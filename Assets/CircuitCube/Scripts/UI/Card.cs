using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.UI
{
    /// <summary>A pop-up card prefab (result or tip). Plays its "In" clip when opened.</summary>
    public sealed class Card : MonoBehaviour
    {
        public Animation Anim;          // clip "In"
        public Text Title, Body;
        public StarRow Stars;           // result card only
        public Button Primary, SecondaryA, SecondaryB;
        public Text PrimaryLabel;

        public void Open()
        {
            gameObject.SetActive(true);
            if (Anim != null && Anim.GetClip("In") != null) { Anim.Stop(); Anim.Play("In"); }
        }
    }
}
