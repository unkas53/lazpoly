using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lazpoly.Presentation.UI
{
    /// <summary>Creates canvases and the fixed panels that <see cref="UiBuilder"/> fills.</summary>
    public static class UiRoot
    {
        public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvas.pixelPerfect = false;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UiTheme.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        /// <summary>Buttons need one event system in the scene; the old input manager drives it.</summary>
        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        /// <summary>A rectangle anchored to its parent; offsets are in reference pixels.</summary>
        public static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        public static RectTransform Stretch(Transform parent, string name) => Rect(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        public static Image Background(RectTransform rect, Color color, bool rounded = true)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            if (rounded)
            {
                image.sprite = UiSprites.Rounded;
                image.type = Image.Type.Sliced;
            }

            return image;
        }

        /// <summary>A panel whose content stacks from the top and scrolls when it is taller than the panel.</summary>
        public static UiBuilder ScrollPanel(RectTransform panel, int padding = UiTheme.Padding)
        {
            RectTransform viewport = Rect(panel, "Viewport", Vector2.zero, Vector2.one, new Vector2(padding, padding), new Vector2(-padding, -padding));
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = Rect(viewport, "Content", new Vector2(0f, 1f), Vector2.one, Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(0.5f, 1f);
            UiBuilder.AddColumnLayout(content.gameObject, 0, UiTheme.Spacing);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            return new UiBuilder(content);
        }

        /// <summary>A full-screen dimmed layer with a centred window that sizes itself to its content.</summary>
        public static UiBuilder Modal(Transform canvas, string name, float width, out GameObject layer)
        {
            RectTransform dim = Stretch(canvas, name);
            Background(dim, UiTheme.Dim, rounded: false);
            layer = dim.gameObject;

            RectTransform window = Rect(dim, "Window", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            window.sizeDelta = new Vector2(width, 0f);
            Background(window, UiTheme.Panel);
            UiBuilder.AddColumnLayout(window.gameObject, UiTheme.Padding + 8, UiTheme.Spacing);
            window.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return new UiBuilder(window);
        }
    }
}
