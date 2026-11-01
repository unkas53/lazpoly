using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Lazpoly.Presentation.Game
{
    /// <summary>
    /// Makes an interface element (a property swatch in the side panel) stand for a square: while the
    /// pointer is over it the HUD shows that square's card, as if the square itself were pointed at.
    /// Going away, or the element being removed, takes the card down again.
    /// </summary>
    public sealed class SquareHoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private int _square;
        private Action<int, bool> _hovered;

        public void Bind(int square, Action<int, bool> hovered)
        {
            _square = square;
            _hovered = hovered;
        }

        public void OnPointerEnter(PointerEventData eventData) => _hovered?.Invoke(_square, true);

        public void OnPointerExit(PointerEventData eventData) => _hovered?.Invoke(_square, false);

        private void OnDisable() => _hovered?.Invoke(_square, false);
    }
}
