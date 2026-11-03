using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Lazpoly.Presentation.Game
{
    /// <summary>
    /// Makes an interface element (a property swatch in a player's corner card) stand for a square:
    /// a click on it opens that square's card, as if the square itself had been clicked.
    /// </summary>
    public sealed class SquareClickTarget : MonoBehaviour, IPointerClickHandler
    {
        private int _square;
        private Action<int> _clicked;

        public void Bind(int square, Action<int> clicked)
        {
            _square = square;
            _clicked = clicked;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) _clicked?.Invoke(_square);
        }
    }
}
