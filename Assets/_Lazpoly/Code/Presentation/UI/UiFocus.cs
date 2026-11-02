using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lazpoly.Presentation.UI
{
    /// <summary>
    /// Keyboard focus for the whole interface. Tab and Shift+Tab walk the buttons and fields of the
    /// window on top (or of the whole screen when no window is open) in reading order; Enter
    /// presses the focused button through the event system's Submit. The focused element wears an
    /// accent ring. The ring and the walking start with the first Tab and stop at the first mouse
    /// click, so mouse players never see a focus they did not ask for. While a window is open in
    /// keyboard mode, its first button (the primary one when it has one) takes the focus by itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiFocus : MonoBehaviour
    {
        private static readonly Color RingColor = new Color(0.98f, 0.70f, 0.24f, 0.95f);
        private const float RingDistance = 3f;

        private readonly List<Selectable> _candidates = new List<Selectable>();
        private readonly List<Rect> _rects = new List<Rect>();
        private Graphic _ringed;
        private Outline _ring;

        /// <summary>The keyboard has been used to move the focus since the last mouse click.</summary>
        public bool KeyboardMode { get; private set; }

        /// <summary>A button holds the keyboard focus, so Enter presses it instead of acting as a shortcut.</summary>
        public static bool ButtonFocused
        {
            get
            {
                UiFocus focus = Current;
                if (focus == null || !focus.KeyboardMode || EventSystem.current == null) return false;
                GameObject selected = EventSystem.current.currentSelectedGameObject;
                return selected != null && selected.activeInHierarchy && selected.GetComponent<Button>() != null;
            }
        }

        public static UiFocus Current { get; private set; }

        public static void Ensure(EventSystem eventSystem)
        {
            if (eventSystem == null || eventSystem.GetComponent<UiFocus>() != null) return;
            eventSystem.gameObject.AddComponent<UiFocus>();
        }

        private void OnEnable() => Current = this;

        private void OnDisable()
        {
            if (Current == this) Current = null;
            Unring();
        }

        private void LateUpdate()
        {
            EventSystem events = EventSystem.current;
            if (events == null) return;

            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                KeyboardMode = false;
                // A click selects the clicked button; dropping that keeps Enter and Space as shortcuts for mouse players. Text fields keep their caret.
                GameObject clicked = events.currentSelectedGameObject;
                if (clicked != null && clicked.GetComponent<InputField>() == null) events.SetSelectedGameObject(null);
            }

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                KeyboardMode = true;
                Collect();
                bool backwards = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                int current = IndexOf(events.currentSelectedGameObject);
                int next = FocusOrder.Step(FocusOrder.Sort(_rects), current, backwards);
                if (next >= 0) events.SetSelectedGameObject(_candidates[next].gameObject);
            }
            else if (KeyboardMode)
            {
                Collect();
                // Nothing focused, or the focus is on something a window now covers: take the window's primary button.
                if (_candidates.Count > 0 && IndexOf(events.currentSelectedGameObject) < 0) events.SetSelectedGameObject(Preferred().gameObject);
            }

            Ring(KeyboardMode ? events.currentSelectedGameObject : null);
        }

        /// <summary>The elements Tab may reach now: those inside the window on top, or everything when no window is open.</summary>
        public void Collect()
        {
            _candidates.Clear();
            _rects.Clear();
            Transform scope = TopScope();
            foreach (Selectable selectable in Selectable.allSelectablesArray)
            {
                if (!selectable.IsActive() || !selectable.interactable || selectable.navigation.mode == Navigation.Mode.None) continue;
                if (scope != null && !selectable.transform.IsChildOf(scope)) continue;
                if (selectable is Scrollbar) continue;
                _candidates.Add(selectable);
                _rects.Add(ScreenRect((RectTransform)selectable.transform));
            }
        }

        public int CandidateCount => _candidates.Count;

        private Selectable Preferred()
        {
            foreach (Selectable candidate in _candidates)
            {
                if (candidate.GetComponent<UiPrimaryMark>() != null) return candidate;
            }

            return _candidates[FocusOrder.Sort(_rects)[0]];
        }

        private int IndexOf(GameObject selected)
        {
            if (selected == null) return -1;
            for (int i = 0; i < _candidates.Count; i++)
            {
                if (_candidates[i].gameObject == selected) return i;
            }

            return -1;
        }

        /// <summary>The open window drawn on top of the others, if any: the highest canvas, then the latest sibling.</summary>
        public static Transform TopScope()
        {
            Transform top = null;
            int topOrder = int.MinValue, topSibling = int.MinValue;
            foreach (UiFocusScope scope in FindObjectsByType<UiFocusScope>())
            {
                if (!scope.gameObject.activeInHierarchy) continue;
                Canvas canvas = scope.GetComponentInParent<Canvas>();
                int order = canvas != null ? canvas.sortingOrder : 0;
                int sibling = scope.transform.GetSiblingIndex();
                if (order > topOrder || (order == topOrder && sibling > topSibling))
                {
                    top = scope.transform;
                    topOrder = order;
                    topSibling = sibling;
                }
            }

            return top;
        }

        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private void Ring(GameObject selected)
        {
            Graphic graphic = null;
            if (selected != null && selected.activeInHierarchy)
            {
                var selectable = selected.GetComponent<Selectable>();
                graphic = selectable != null && selectable.targetGraphic != null ? selectable.targetGraphic : selected.GetComponent<Graphic>();
            }

            if (graphic == _ringed) return;
            Unring();
            if (graphic == null) return;
            _ringed = graphic;
            _ring = graphic.gameObject.AddComponent<Outline>();
            _ring.effectColor = RingColor;
            _ring.effectDistance = new Vector2(RingDistance, RingDistance);
            _ring.useGraphicAlpha = false;
        }

        private void Unring()
        {
            if (_ring != null) Destroy(_ring);
            _ring = null;
            _ringed = null;
        }
    }

    /// <summary>Marks a window whose buttons are the only ones Tab reaches while it is open; <see cref="UiRoot.Modal"/> adds it.</summary>
    [DisallowMultipleComponent]
    public sealed class UiFocusScope : MonoBehaviour
    {
    }

    /// <summary>Marks the button a window hands the keyboard focus to when it opens.</summary>
    [DisallowMultipleComponent]
    public sealed class UiPrimaryMark : MonoBehaviour
    {
    }
}
