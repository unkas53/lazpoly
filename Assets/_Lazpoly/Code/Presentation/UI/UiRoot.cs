using System.Collections.Generic;
using Lazpoly.Infra;
using Lazpoly.Infra.Settings;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lazpoly.Presentation.UI
{
    /// <summary>Creates canvases and the fixed panels that <see cref="UiBuilder"/> fills.</summary>
    public static class UiRoot
    {
        private static float _scale = 1f;
        private static readonly List<CanvasScaler> _scalers = new List<CanvasScaler>();

        /// <summary>The interface size in force (see <see cref="GameSettings.UiScaleFactor"/>); new canvases take it, open ones follow <see cref="ApplyScale"/>.</summary>
        public static float Scale => _scale;

        /// <summary>Redraws every open canvas at a new interface size, and remembers it for the next one.</summary>
        public static void ApplyScale(float scale)
        {
            _scale = Mathf.Clamp(scale, 0.5f, 2f);
            _scalers.RemoveAll(scaler => scaler == null); // Canvases of scenes that have gone.
            foreach (CanvasScaler scaler in _scalers) scaler.referenceResolution = UiTheme.ReferenceResolution / _scale;
        }

        public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder)
        {
            if (GameServices.Registry != null && GameServices.Registry.TryGet(out SettingsService settings)) _scale = settings.Current.UiScaleFactor;
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvas.pixelPerfect = false;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UiTheme.ReferenceResolution / _scale;
            _scalers.Add(scaler);
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

        public static Image Background(RectTransform rect, Color color, bool rounded = true, bool shadow = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            if (rounded)
            {
                image.sprite = UiSprites.Rounded;
                image.type = Image.Type.Sliced;
            }

            if (shadow) AddShadow(rect.gameObject, 4f);
            return image;
        }

        /// <summary>A soft drop shadow under an image, so panels and buttons lift off the scene behind them.</summary>
        public static Shadow AddShadow(GameObject element, float distance)
        {
            var shadow = element.AddComponent<Shadow>();
            shadow.effectColor = UiTheme.ShadowColor;
            shadow.effectDistance = new Vector2(0f, -distance);
            shadow.useGraphicAlpha = true;
            return shadow;
        }

        /// <summary>A panel with a thin accent line along its top edge, the look shared by every fixed panel.</summary>
        public static Image Panel(RectTransform rect)
        {
            Image image = Background(rect, UiTheme.Panel, shadow: true);
            RectTransform trim = Rect(rect, "Trim", new Vector2(0f, 1f), Vector2.one, new Vector2(14f, -3f), new Vector2(-14f, -1f));
            Image line = Background(trim, UiTheme.Accent, rounded: false);
            line.raycastTarget = false;
            return image;
        }

        /// <summary>Width of the scrollbar track at the right edge of a scrolling panel.</summary>
        public const float ScrollbarWidth = 8f;

        /// <summary>
        /// A panel whose content stacks from the top and scrolls when it is taller than the panel. A thin
        /// scrollbar on the right shows that there is more; it hides when everything fits.
        /// </summary>
        public static UiBuilder ScrollPanel(RectTransform panel, int padding = UiTheme.Padding)
        {
            RectTransform viewport = Rect(panel, "Viewport", Vector2.zero, Vector2.one, new Vector2(padding, padding),
                new Vector2(-padding - ScrollbarWidth - 4f, -padding));
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
            scroll.verticalScrollbar = AddScrollbar(panel, padding);
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return new UiBuilder(content);
        }

        private static Scrollbar AddScrollbar(RectTransform panel, int padding)
        {
            RectTransform track = Rect(panel, "Scrollbar", new Vector2(1f, 0f), Vector2.one, new Vector2(-padding - ScrollbarWidth, padding), new Vector2(-padding, -padding));
            Background(track, new Color(1f, 1f, 1f, 0.07f));
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            RectTransform sliding = Rect(track, "Sliding Area", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            RectTransform handle = Rect(sliding, "Handle", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image handleImage = Background(handle, UiTheme.Muted);
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            return scrollbar;
        }

        /// <summary>A full-screen dimmed layer with a centred window that sizes itself to its content.</summary>
        public static UiBuilder Modal(Transform canvas, string name, float width, out GameObject layer)
        {
            RectTransform dim = Stretch(canvas, name);
            Background(dim, UiTheme.Dim, rounded: false);
            layer = dim.gameObject;

            RectTransform window = Rect(dim, "Window", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            window.sizeDelta = new Vector2(width, 0f);
            Panel(window);
            UiBuilder.AddColumnLayout(window.gameObject, UiTheme.Padding + 8, UiTheme.Spacing);
            window.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            UiFadeIn.Attach(layer, window);
            return new UiBuilder(window);
        }
    }
}
