using CircuitCube.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CircuitCube.UI
{
    /// <summary>Click sound of a button (added to the prefabs by Tools/Circuit Cube/Audio/Build Audio). A disabled button plays the blocked sound.</summary>
    [RequireComponent(typeof(Selectable))]
    public sealed class UiClickSound : MonoBehaviour, IPointerClickHandler
    {
        Selectable _target;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (_target == null) _target = GetComponent<Selectable>();
            GameAudio.Click(_target != null && _target.IsInteractable());
        }
    }
}
