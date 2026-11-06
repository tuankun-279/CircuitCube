using UnityEngine;

namespace CircuitCube.UI
{
    /// <summary>Base for a screen prefab: shows and hides itself and plays its "In" clip if it has one.</summary>
    public abstract class UIScreen : MonoBehaviour
    {
        public Animation Anim;   // optional clip "In" set up on the prefab

        public virtual void Show()
        {
            gameObject.SetActive(true);
            if (Anim != null && Anim.GetClip("In") != null) { Anim.Stop(); Anim.Play("In"); }
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
