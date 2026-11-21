using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lazpoly.Presentation.UI
{
    /// <summary>
    /// Makes a button answer the pointer like a key: it lifts a little while the pointer is over it and sinks while
    /// it is held down, quickly, so the interface feels alive before and during a click (the owner's wish, 2026-11-21:
    /// menus supported by animation). The first touch of the pointer is told to <see cref="Hovered"/>, which the audio
    /// player ticks on. Uses unscaled time, so it works while the game is paused; a disabled button stays flat and silent.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        /// <summary>How large a button is under the pointer, and while it is held down.</summary>
        public const float Lift = 1.04f;
        public const float Sink = 0.97f;

        /// <summary>How long the lift takes; the sink under a click is quicker.</summary>
        public const float LiftSeconds = 0.09f;
        public const float SinkSeconds = 0.05f;

        /// <summary>The shortest time between two hover ticks, so sweeping across a row does not rattle.</summary>
        public const float TickGap = 0.07f;

        private Selectable _selectable;
        private bool _over;
        private bool _down;

        private static Vector3 _tickedAt = new Vector3(float.MinValue, 0f, 0f);
        private static float _tickedTime = float.MinValue;

        /// <summary>Raised when the pointer comes onto a button that can be pressed; the audio player ticks on it.</summary>
        public static event Action Hovered;

        /// <summary>Whether the pointer is over the element now (for tests).</summary>
        public bool IsOver => _over;

        /// <summary>The size a button goes to: sunk while held, lifted under the pointer, flat otherwise and whenever it cannot be pressed.</summary>
        public static float Target(bool over, bool down, bool pressable) => !pressable ? 1f : down ? Sink : over ? Lift : 1f;

        /// <summary>
        /// Whether a hover should tick: not when the pointer has not moved since the last tick (a window redrawn under a
        /// resting pointer builds new buttons there, which is no new touch), and not within <see cref="TickGap"/> of it.
        /// </summary>
        public static bool Ticks(Vector3 pointer, Vector3 lastPointer, float now, float lastTime) =>
            pointer != lastPointer && now - lastTime >= TickGap;

        private void Awake() => _selectable = GetComponent<Selectable>();

        private bool Pressable => _selectable == null || _selectable.interactable;

        public void OnPointerEnter(PointerEventData eventData)
        {
            _over = true;
            if (!Pressable) return;
            Vector3 pointer = eventData != null ? (Vector3)eventData.position : Input.mousePosition;
            float now = Time.unscaledTime;
            if (!Ticks(pointer, _tickedAt, now, _tickedTime)) return;
            _tickedAt = pointer;
            _tickedTime = now;
            Hovered?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _over = false;
            _down = false;
        }

        public void OnPointerDown(PointerEventData eventData) => _down = true;

        public void OnPointerUp(PointerEventData eventData) => _down = false;

        private void OnDisable()
        {
            _over = false;
            _down = false;
            transform.localScale = Vector3.one;
        }

        private void Update()
        {
            float target = Target(_over, _down, Pressable);
            float current = transform.localScale.x;
            if (Mathf.Approximately(current, target)) return;
            float speed = target < current && _down ? (Lift - Sink) / SinkSeconds : (Lift - 1f) / LiftSeconds;
            transform.localScale = Vector3.one * Mathf.MoveTowards(current, target, Time.unscaledDeltaTime * speed);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _tickedAt = new Vector3(float.MinValue, 0f, 0f);
            _tickedTime = float.MinValue;
        }
    }
}
