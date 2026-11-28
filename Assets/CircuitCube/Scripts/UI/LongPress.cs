using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CircuitCube.UI
{
    /// <summary>
    /// Raises <see cref="Pressed"/> when a finger stays on the element for <see cref="Seconds"/> without moving
    /// (a scroll of the list cancels it). Drags still go to the scroll view: only press and release are handled here.
    /// </summary>
    public sealed class LongPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public float Seconds = 0.55f;
        [Tooltip("Movement in pixels that cancels the press (a scroll).")]
        public float Slop = 24f;

        public event Action Pressed;

        bool _down;
        float _since;
        Vector2 _at;

        public void OnPointerDown(PointerEventData e)
        {
            _down = true;
            _since = Time.unscaledTime;
            _at = e.position;
        }

        public void OnPointerUp(PointerEventData e) => _down = false;
        public void OnPointerExit(PointerEventData e) => _down = false;

        void OnDisable() => _down = false;

        void Update()
        {
            if (!_down) return;
            var pointer = Pointer.current;
            if (pointer != null && (pointer.position.ReadValue() - _at).sqrMagnitude > Slop * Slop) { _down = false; return; }
            if (Time.unscaledTime - _since < Seconds) return;
            _down = false;
            Pressed?.Invoke();
        }
    }
}
