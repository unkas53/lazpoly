using System;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Lazpoly.Presentation.UI
{
    /// <summary>
    /// Adds interface elements to a container from top to bottom (or left to right in a row). Screens
    /// describe their content with it and <see cref="UiView"/> rebuilds that content when it changes.
    /// Every click or toggle calls <see cref="Changed"/> so the view can redraw itself.
    /// </summary>
    public sealed class UiBuilder
    {
        public UiBuilder(RectTransform container, Action changed = null)
        {
            Container = container ? container : throw new ArgumentNullException(nameof(container));
            Changed = changed;
        }

        public RectTransform Container { get; }

        /// <summary>Called after a button, toggle or choice reacts, so the owner can redraw.</summary>
        public Action Changed { get; set; }

        /// <summary>Raised when any button or toggle made by a builder is pressed; the audio player clicks on it.</summary>
        public static event Action Pressed;

        /// <summary>Removes everything this builder added.</summary>
        public void Clear()
        {
            for (int i = Container.childCount - 1; i >= 0; i--)
            {
                GameObject child = Container.GetChild(i).gameObject;
                child.SetActive(false);
                child.transform.SetParent(null, false);
                if (Application.isPlaying) Object.Destroy(child);
                else Object.DestroyImmediate(child);
            }
        }

        public Text Label(string text, UiTextStyle style = UiTextStyle.Body, Color? color = null, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            GameObject element = NewChild("Label");
            var label = element.AddComponent<Text>();
            UiTheme.Apply(label, style);
            label.alignment = alignment;
            label.text = text;
            if (color.HasValue) label.color = color.Value;
            return label;
        }

        public Button Button(string label, Action onClick, bool enabled = true, UiButtonStyle style = UiButtonStyle.Secondary,
            float height = UiTheme.ButtonHeight)
        {
            GameObject element = NewChild($"Button {label}");
            Button button = MakeButton(element, onClick, enabled, style);
            Fix(element, height: height);
            Text text = TextChild(element.transform, label, height >= UiTheme.ButtonHeight ? UiTextStyle.Body : UiTextStyle.Small);
            text.color = UiTheme.Label(style);
            text.alignment = TextAnchor.MiddleCenter;
            text.font = UiTheme.BoldFont;
            return button;
        }

        /// <summary>A button with a small icon before its label.</summary>
        public Button IconButton(UiIcon icon, string label, Action onClick, bool enabled = true, UiButtonStyle style = UiButtonStyle.Secondary,
            float height = UiTheme.ButtonHeight)
        {
            GameObject element = NewChild($"Button {label}");
            Button button = MakeButton(element, onClick, enabled, style);
            Fix(element, height: height);
            var row = element.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(14, 14, 0, 0);
            row.spacing = 10f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            float size = height >= UiTheme.ButtonHeight ? 22f : 18f;
            UiIcons.Create((RectTransform)element.transform, icon, size, UiTheme.Label(style));
            Text text = TextChild(element.transform, label, height >= UiTheme.ButtonHeight ? UiTextStyle.Body : UiTextStyle.Small, stretch: false);
            text.color = UiTheme.Label(style);
            text.alignment = TextAnchor.MiddleLeft;
            text.font = UiTheme.BoldFont;
            return button;
        }

        /// <summary>
        /// A clickable card: whatever is added to the returned builder sits inside it, and a click
        /// anywhere on it runs <paramref name="onClick"/>. For choices that deserve a title and a line of text.
        /// </summary>
        public UiBuilder Tile(Action onClick, Color? color = null, int padding = 14)
        {
            GameObject element = NewChild("Tile");
            MakeButton(element, onClick, true, UiButtonStyle.Secondary, color ?? UiTheme.Card);
            AddColumnLayout(element, padding, 6f);
            return new UiBuilder((RectTransform)element.transform, Changed);
        }

        /// <summary>A small picture, such as beside a title.</summary>
        public RectTransform Icon(UiIcon icon, float size = 28f, Color? color = null) =>
            UiIcons.Create(Container, icon, size, color ?? UiTheme.Accent);

        private Button MakeButton(GameObject element, Action onClick, bool enabled, UiButtonStyle style, Color? fill = null)
        {
            Image image = UiRoot.Background((RectTransform)element.transform, fill ?? UiTheme.Fill(style));
            UiRoot.AddShadow(element, 2f);
            element.AddComponent<UiHover>();
            var button = element.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = ButtonColors();
            button.interactable = enabled;
            if (onClick != null)
            {
                button.onClick.AddListener(() =>
                {
                    Pressed?.Invoke();
                    onClick();
                    Changed?.Invoke();
                });
            }

            return button;
        }

        public Toggle Toggle(string label, bool value, Action<bool> changed, bool enabled = true)
        {
            GameObject element = NewChild($"Toggle {label}");
            Fix(element, height: 32f);
            // A see-through backdrop so a click on the label counts too, not only on the small box.
            UiRoot.Background((RectTransform)element.transform, Color.clear, rounded: false);
            var row = element.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 10f;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            RectTransform box = UiRoot.Rect(element.transform, "Box", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            Image boxImage = UiRoot.Background(box, UiTheme.Field);
            Fix(box.gameObject, 28f, 28f);
            RectTransform check = UiRoot.Rect(box, "Check", Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
            Image checkImage = UiRoot.Background(check, UiTheme.Accent);

            Text text = TextChild(element.transform, label, UiTextStyle.Body, stretch: false);
            text.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var toggle = element.AddComponent<Toggle>();
            toggle.targetGraphic = boxImage;
            toggle.graphic = checkImage;
            toggle.isOn = value;
            toggle.interactable = enabled;
            toggle.onValueChanged.AddListener(on =>
            {
                Pressed?.Invoke();
                changed?.Invoke(on);
                Changed?.Invoke();
            });
            return toggle;
        }

        /// <summary>A slider. It does not call <see cref="Changed"/>: redrawing would end the drag.</summary>
        public Slider Slider(float value, float min, float max, Action<float> changed)
        {
            GameObject element = NewChild("Slider");
            Fix(element, height: 28f);

            RectTransform track = UiRoot.Rect(element.transform, "Background", new Vector2(0f, 0.35f), new Vector2(1f, 0.65f), Vector2.zero, Vector2.zero);
            UiRoot.Background(track, UiTheme.Field);
            RectTransform fillArea = UiRoot.Rect(element.transform, "Fill Area", new Vector2(0f, 0.35f), new Vector2(1f, 0.65f), new Vector2(4f, 0f), new Vector2(-14f, 0f));
            RectTransform fill = UiRoot.Rect(fillArea, "Fill", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(10f, 0f));
            UiRoot.Background(fill, UiTheme.Accent);
            RectTransform handleArea = UiRoot.Rect(element.transform, "Handle Slide Area", Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f));
            RectTransform handle = UiRoot.Rect(handleArea, "Handle", Vector2.zero, new Vector2(0f, 1f), new Vector2(-10f, 0f), new Vector2(10f, 0f));
            Image handleImage = UiRoot.Background(handle, UiTheme.Text);

            var slider = element.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            if (changed != null) slider.onValueChanged.AddListener(v => changed(v));
            return slider;
        }

        /// <summary>A text field. Typing does not call <see cref="Changed"/>, so the field keeps its focus.</summary>
        public InputField Input(string value, Action<string> changed, int characterLimit = 24,
            InputField.ContentType contentType = InputField.ContentType.Standard)
        {
            GameObject element = NewChild("Input");
            Image image = UiRoot.Background((RectTransform)element.transform, UiTheme.Field);
            Fix(element, height: UiTheme.FieldHeight);

            Text text = TextChild(element.transform, "", UiTextStyle.Body);
            ((RectTransform)text.transform).offsetMin = new Vector2(12f, 0f);
            ((RectTransform)text.transform).offsetMax = new Vector2(-12f, 0f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.alignment = TextAnchor.MiddleLeft;

            var input = element.AddComponent<InputField>();
            input.targetGraphic = image;
            input.textComponent = text;
            input.characterLimit = characterLimit;
            input.contentType = contentType;
            input.SetTextWithoutNotify(value ?? "");
            if (changed != null) input.onValueChanged.AddListener(v => changed(v));
            return input;
        }

        /// <summary>A coloured square (or disc), such as a player's colour.</summary>
        public Image Swatch(Color color, float size = 18f, bool round = false)
        {
            GameObject element = NewChild("Swatch");
            Image image = UiRoot.Background((RectTransform)element.transform, color);
            if (round)
            {
                image.sprite = UiSprites.Circle;
                image.type = Image.Type.Simple;
            }

            Fix(element, size, size);
            return image;
        }

        public void Space(float height = 12f) => Fix(NewChild("Space"), height: height);

        /// <summary>Elements added to the returned builder sit side by side and share the width.</summary>
        /// <param name="expand">False keeps fixed-size elements (swatches) at their size instead of sharing out the width.</param>
        public UiBuilder Row(float spacing = UiTheme.Spacing, bool expand = true)
        {
            GameObject element = NewChild("Row");
            var row = element.AddComponent<HorizontalLayoutGroup>();
            row.spacing = spacing;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = expand;
            row.childForceExpandHeight = false;
            return new UiBuilder((RectTransform)element.transform, Changed);
        }

        /// <summary>Elements added to the returned builder stack top to bottom inside a row, taking the row's spare width.</summary>
        public UiBuilder Column(float spacing = 2f)
        {
            GameObject element = NewChild("Column");
            AddColumnLayout(element, 0, spacing);
            Fix(element, -1f).flexibleWidth = 1f;
            return new UiBuilder((RectTransform)element.transform, Changed);
        }

        /// <summary>A rounded card with its own padding; elements added to it stack inside.</summary>
        public UiBuilder Card(Color? color = null, int padding = 12)
        {
            GameObject element = NewChild("Card");
            UiRoot.Background((RectTransform)element.transform, color ?? UiTheme.Card, shadow: true);
            AddColumnLayout(element, padding, 6f);
            return new UiBuilder((RectTransform)element.transform, Changed);
        }

        /// <summary>A heading with an icon before it, for the top of a window or a group.</summary>
        public Text Title(UiIcon icon, string text, Color? color = null)
        {
            UiBuilder row = Row(10f, expand: false);
            row.Icon(icon, 28f, color ?? UiTheme.Accent);
            Text label = row.Label(text, UiTextStyle.Heading);
            Fix(label, -1f).flexibleWidth = 1f;
            return label;
        }

        /// <summary>A fixed-height list that scrolls.</summary>
        public UiBuilder Scroll(float height)
        {
            GameObject element = NewChild("Scroll");
            UiRoot.Background((RectTransform)element.transform, UiTheme.Field);
            Fix(element, height: height);
            UiBuilder content = UiRoot.ScrollPanel((RectTransform)element.transform, 8);
            content.Changed = Changed;
            return content;
        }

        /// <summary>Gives an element a fixed width and/or height inside a layout (a negative value leaves it free).</summary>
        public static LayoutElement Fix(Component component, float width = -1f, float height = -1f) => Fix(component.gameObject, width, height);

        public static LayoutElement Fix(GameObject element, float width = -1f, float height = -1f)
        {
            var layout = element.GetComponent<LayoutElement>();
            if (layout == null) layout = element.AddComponent<LayoutElement>();
            if (width >= 0f)
            {
                layout.minWidth = width;
                layout.preferredWidth = width;
                layout.flexibleWidth = 0f;
            }

            if (height >= 0f)
            {
                layout.minHeight = height;
                layout.preferredHeight = height;
            }

            return layout;
        }

        internal static void AddColumnLayout(GameObject element, int padding, float spacing)
        {
            var column = element.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(padding, padding, padding, padding);
            column.spacing = spacing;
            column.childAlignment = TextAnchor.UpperLeft;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
        }

        private GameObject NewChild(string name)
        {
            var element = new GameObject(name, typeof(RectTransform));
            element.transform.SetParent(Container, false);
            return element;
        }

        private static Text TextChild(Transform parent, string content, UiTextStyle style, bool stretch = true)
        {
            RectTransform rect = stretch
                ? UiRoot.Stretch(parent, "Text")
                : UiRoot.Rect(parent, "Text", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            var text = rect.gameObject.AddComponent<Text>();
            UiTheme.Apply(text, style);
            text.text = content;
            return text;
        }

        private static ColorBlock ButtonColors()
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.5f);
            colors.colorMultiplier = 1.2f;
            colors.fadeDuration = 0.08f;
            return colors;
        }
    }
}
