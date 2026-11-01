using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lazpoly.Presentation.UI
{
    /// <summary>
    /// Lifts a button slightly while the pointer is over it, so the interface answers the mouse before
    /// a click. Uses unscaled time, so it works while the game is paused; a disabled button stays flat.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public const float Lift = 1.03f;
        private const float Speed = 0.5f;

        private Selectable _selectable;
        private bool _over;

        /// <summary>Whether the pointer is over the element now (for tests).</summary>
        public bool IsOver => _over;

        private void Awake() => _selectable = GetComponent<Selectable>();

        public void OnPointerEnter(PointerEventData eventData) => _over = true;

        public void OnPointerExit(PointerEventData eventData) => _over = false;

        private void OnDisable()
        {
            _over = false;
            transform.localScale = Vector3.one;
        }

        private void Update()
        {
            float target = _over && (_selectable == null || _selectable.interactable) ? Lift : 1f;
            float scale = Mathf.MoveTowards(transform.localScale.x, target, Time.unscaledDeltaTime * Speed);
            transform.localScale = Vector3.one * scale;
        }
    }
}
