using System;
using Lazpoly.Presentation.Texts;
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

        /// <summary>Tells the listeners of <see cref="Pressed"/> that something built by hand was pressed (a plate that is its own tick box), so it clicks like the rest.</summary>
        public static void Press() => Pressed?.Invoke();

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
            Button button = MakeButton(element, onClick, enabled, style, height);
            Fix(element, height: height);
            Text text = TextChild(element.transform, label, height >= UiTheme.ButtonHeight ? UiTextStyle.Body : UiTextStyle.Small);
            text.color = LabelColor(style, enabled, Skinned(button));
            text.alignment = TextAnchor.MiddleCenter;
            text.font = UiTheme.BoldFont;
            ShrinkToFit(text);
            return button;
        }

        /// <summary>
        /// A label that shrinks a little instead of spilling out of its button: English runs longer than
        /// Turkish, and the Large interface size leaves less room. Never below <see cref="UiTheme.MinButtonFontSize"/>.
        /// </summary>
        public static void ShrinkToFit(Text text)
        {
            text.resizeTextForBestFit = true;
            text.resizeTextMaxSize = text.fontSize;
            text.resizeTextMinSize = Mathf.Min(text.fontSize, UiTheme.MinButtonFontSize);
        }

        /// <summary>A button with a small icon before its label.</summary>
        public Button IconButton(UiIcon icon, string label, Action onClick, bool enabled = true, UiButtonStyle style = UiButtonStyle.Secondary,
            float height = UiTheme.ButtonHeight)
        {
            GameObject element = NewChild($"Button {label}");
            Button button = MakeButton(element, onClick, enabled, style, height);
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
            UiIcons.Create((RectTransform)element.transform, icon, size, IconColor(style, enabled, Skinned(button)));
            Text text = TextChild(element.transform, label, height >= UiTheme.ButtonHeight ? UiTextStyle.Body : UiTextStyle.Small, stretch: false);
            text.color = LabelColor(style, enabled, Skinned(button));
            text.alignment = TextAnchor.MiddleLeft;
            text.font = UiTheme.BoldFont;
            ShrinkToFit(text);
            return button;
        }

        /// <summary>
        /// A big round button with a short caption under it, for the dock beside the board. The whole
        /// column (disc and caption) takes the click, so the caption names the button for tests and readers.
        /// </summary>
        public Button RoundButton(UiIcon icon, string caption, Action onClick, bool enabled = true, UiButtonStyle style = UiButtonStyle.Secondary,
            float size = UiLayout.DockButton)
        {
            GameObject element = NewChild($"Round {caption}");
            var column = element.AddComponent<VerticalLayoutGroup>();
            column.spacing = 4f;
            column.childAlignment = TextAnchor.UpperCenter;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = false;
            column.childForceExpandHeight = false;

            // A resting button keeps its disc and a muted icon: Unity's disabled tint would sink it into the dark dock.
            RectTransform disc = UiRoot.Rect(element.transform, "Disc", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            Image image = UiRoot.Background(disc, enabled ? UiTheme.Fill(style) : UiTheme.RestingFill);
            image.sprite = UiSprites.Circle;
            image.type = Image.Type.Simple;
            UiRoot.AddShadow(disc.gameObject, 3f);
            Fix(disc.gameObject, size, size);
            RectTransform picture = UiIcons.Create(disc, icon, size * 0.5f, enabled ? IconColor(style, true) : UiTheme.Muted);
            picture.anchorMin = picture.anchorMax = new Vector2(0.5f, 0.5f);
            picture.anchoredPosition = Vector2.zero;
            picture.sizeDelta = new Vector2(size * 0.5f, size * 0.5f);

            Text text = TextChild(element.transform, caption, UiTextStyle.Small, stretch: false);
            text.alignment = TextAnchor.UpperCenter;
            text.font = UiTheme.BoldFont;
            text.color = enabled ? UiTheme.Text : UiTheme.Muted;
            Fix(text, width: size + 36f);

            element.AddComponent<UiHover>();
            var button = element.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = ButtonColors();
            colors.disabledColor = Color.white;
            button.colors = colors;
            button.interactable = enabled;
            if (style == UiButtonStyle.Primary && enabled) element.AddComponent<UiPrimaryMark>();
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

        /// <summary>
        /// A clickable card: whatever is added to the returned builder sits inside it, and a click
        /// anywhere on it runs <paramref name="onClick"/>. For choices that deserve a title and a line of text.
        /// </summary>
        public UiBuilder Tile(Action onClick, Color? color = null, int padding = 14)
        {
            GameObject element = NewChild("Tile");
            Button button = MakeButton(element, onClick, true, UiButtonStyle.Secondary, -1f, color ?? UiTheme.Card);
            // A tile is a card that answers a click: the kit's card skin, lighter under the pointer.
            if (UiRoot.DressCard((Image)button.targetGraphic, color)) RemoveShadow(element);

            AddColumnLayout(element, padding, 6f);
            return new UiBuilder((RectTransform)element.transform, Changed);
        }

        /// <summary>How much of the accent a chosen <see cref="Choice"/>'s ground takes, and how strong the edge line of the others is.</summary>
        public const float ChosenWash = 0.14f;
        public const float ChoiceEdge = 0.16f;

        /// <summary>
        /// One of a few choices side by side (the levels of a building): a flat rounded card with a thin edge line;
        /// the chosen ones stand on a ground washed with the accent, ringed in it. Whatever is added to the returned
        /// builder stacks inside. A click anywhere on it runs <paramref name="onClick"/>; with none it does not answer
        /// the pointer at all. <paramref name="dimmed"/> sinks its ground, for a choice that is not open yet.
        /// </summary>
        public UiBuilder Choice(Action onClick, bool chosen, bool dimmed = false, int padding = 6, float spacing = 3f)
        {
            GameObject element = NewChild("Choice");
            Color ground = dimmed ? UiTheme.Field : chosen ? Color.Lerp(UiTheme.Card, UiTheme.Accent, ChosenWash) : UiTheme.Card;
            Button button = MakeButton(element, onClick, true, UiButtonStyle.Secondary, -1f, ground);
            RemoveShadow(element);
            ColorBlock colors = button.colors;
            colors.disabledColor = Color.white; // A choice that cannot be clicked keeps its look; its content says why.
            button.colors = colors;
            button.interactable = onClick != null;

            RectTransform ring = UiRoot.Stretch(element.transform, "Ring");
            ring.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var line = ring.gameObject.AddComponent<Image>();
            line.sprite = UiSprites.Ring;
            line.type = Image.Type.Sliced;
            line.color = chosen ? UiTheme.Accent : new Color(UiTheme.Text.r, UiTheme.Text.g, UiTheme.Text.b, ChoiceEdge);
            line.raycastTarget = false;

            AddColumnLayout(element, padding, spacing);
            return new UiBuilder((RectTransform)element.transform, Changed);
        }

        /// <summary>
        /// The close mark of a card with a dark band at its top: a faint disc with the kit's cross on it (a plain × without
        /// the kit's pictures), <paramref name="size"/> across.
        /// </summary>
        public Button CloseMark(Action onClick, Color ink, float size = 40f)
        {
            GameObject element = NewChild("Button ×");
            Button button = MakeButton(element, onClick, true, UiButtonStyle.Secondary, -1f, new Color(ink.r, ink.g, ink.b, 0.14f));
            RemoveShadow(element);
            var disc = (Image)button.targetGraphic;
            disc.sprite = UiSprites.Circle;
            disc.type = Image.Type.Simple;
            Fix(element, size, size);

            Sprite cross = KitArt.Get(CloseIcon);
            if (cross != null)
            {
                RectTransform mark = UiRoot.Rect(element.transform, "Mark", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                mark.sizeDelta = new Vector2(size * 0.5f, size * 0.5f);
                var picture = mark.gameObject.AddComponent<Image>();
                picture.sprite = cross;
                picture.color = ink;
                picture.raycastTarget = false;
            }
            else
            {
                Text text = TextChild(element.transform, "×", UiTextStyle.Body);
                text.color = ink;
                text.alignment = TextAnchor.MiddleCenter;
                text.font = UiTheme.BoldFont;
            }

            return button;
        }

        /// <summary>The kit's pictures of a cross and of a check mark.</summary>
        public const string CloseIcon = "Icon_Close";
        public const string CheckIcon = "Icon_Check";

        /// <summary>A small picture, such as beside a title.</summary>
        public RectTransform Icon(UiIcon icon, float size = 28f, Color? color = null) =>
            UiIcons.Create(Container, icon, size, color ?? UiTheme.Accent);

        /// <summary>
        /// A button's ground. Buttons wear the kit's skins (the dark petrol one for <see cref="UiButtonStyle.Primary"/>
        /// and a selected choice, the cream one otherwise, tinted brick red for leaving), which change picture on
        /// hover, press and when they cannot be pressed. A <paramref name="fill"/> keeps the plain rounded shape in
        /// that colour, for the callers that colour their own; so does a build without the kit's pictures.
        /// </summary>
        private Button MakeButton(GameObject element, Action onClick, bool enabled, UiButtonStyle style, float height, Color? fill = null)
        {
            Image image = UiRoot.Background((RectTransform)element.transform, fill ?? UiTheme.Fill(style));
            element.AddComponent<UiHover>();
            var button = element.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = ButtonColors();
            button.interactable = enabled;
            if (fill.HasValue || !DressButton(button, image, style, height)) UiRoot.AddShadow(element, 2f);
            if (style == UiButtonStyle.Primary) element.AddComponent<UiPrimaryMark>();
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

        /// <summary>The kit skin a button style wears.</summary>
        public static KitArt.Skin SkinOf(UiButtonStyle style) =>
            style == UiButtonStyle.Primary || style == UiButtonStyle.Selected ? KitArt.Skin.Primary : KitArt.Skin.Secondary;

        /// <summary>Puts the kit's skin for <paramref name="style"/> on a button; false when the pictures are not in the build.</summary>
        private static bool DressButton(Button button, Image image, UiButtonStyle style, float height)
        {
            KitArt.Skin skin = SkinOf(style);
            // A selected choice rests on the lighter petrol of the dark skin, a step apart from the main action.
            string resting = KitArt.Picture(skin, style == UiButtonStyle.Selected ? "Hover" : "Normal");
            if (!KitArt.Dress(image, resting, height > 0f ? height : UiTheme.ButtonHeight)) return false;

            // The skins carry their own colour and shadow: white leaves them as drawn. A danger button is the kit's
            // cream one (its "Oyundan Çık"), told apart by its brick red words (see LabelColor).
            image.color = Color.white;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = KitArt.Get(KitArt.Picture(skin, "Hover")),
                selectedSprite = KitArt.Get(resting),
                pressedSprite = KitArt.Get(KitArt.Picture(skin, "Pressed")),
                disabledSprite = KitArt.Get(KitArt.Picture(skin, "Disabled")),
            };
            return true;
        }

        /// <summary>Whether a button wears a kit skin (they change picture by state; the plain ones change tint).</summary>
        private static bool Skinned(Button button) => button.transition == Selectable.Transition.SpriteSwap;

        /// <summary>
        /// A button's label: its style's ink, faded when it cannot be pressed (the skin turns grey under it). On the
        /// kit's cream skin a danger button writes in brick red.
        /// </summary>
        private static Color LabelColor(UiButtonStyle style, bool enabled, bool skinned = false)
        {
            Color color = skinned && style == UiButtonStyle.Danger ? UiTheme.Warning : UiTheme.Label(style);
            if (!enabled) color.a *= 0.55f;
            return color;
        }

        /// <summary>An icon on a button: the kit's brass on its dark buttons, the label's ink on the others.</summary>
        private static Color IconColor(UiButtonStyle style, bool enabled, bool skinned = false)
        {
            Color color = style == UiButtonStyle.Primary ? UiPalette.Ink.Gold : LabelColor(style, true, skinned);
            if (!enabled) color.a *= 0.55f;
            return color;
        }

        private static void RemoveShadow(GameObject element)
        {
            var shadow = element.GetComponent<Shadow>();
            if (shadow == null) return;
            if (Application.isPlaying) Object.Destroy(shadow);
            else Object.DestroyImmediate(shadow);
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
            KitArt.Dress(boxImage, KitArt.Chip, 28f, KitArt.ChipDesignHeight);
            Fix(box.gameObject, 28f, 28f);
            RectTransform check = UiRoot.Rect(box, "Check", Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
            Image checkImage = UiRoot.Background(check, UiTheme.Fill(UiButtonStyle.Primary));

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
            UiRoot.Background(fill, UiTheme.Fill(UiButtonStyle.Primary));
            RectTransform handleArea = UiRoot.Rect(element.transform, "Handle Slide Area", Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f));
            RectTransform handle = UiRoot.Rect(handleArea, "Handle", Vector2.zero, new Vector2(0f, 1f), new Vector2(-10f, 0f), new Vector2(10f, 0f));
            Image handleImage = UiRoot.Background(handle, UiTheme.Text);
            // The kit's knob: a petrol disc with a brass rim.
            Sprite knob = KitArt.Get(KitArt.Circle);
            if (knob != null)
            {
                handleImage.sprite = knob;
                handleImage.type = Image.Type.Simple;
                handleImage.color = Color.white;
            }

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
            if (KitArt.Dress(image, KitArt.Chip, UiTheme.FieldHeight, KitArt.ChipDesignHeight)) image.color = Color.white; // The kit's field: cream with a thin grey edge.
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

        /// <summary>A coloured square (or disc), such as a player's colour; an optional letter on it names the colour for those who do not see it.</summary>
        public Image Swatch(Color color, float size = 18f, bool round = false, string mark = null)
        {
            GameObject element = NewChild("Swatch");
            Image image = UiRoot.Background((RectTransform)element.transform, color);
            if (round)
            {
                image.sprite = UiSprites.Circle;
                image.type = Image.Type.Simple;
            }

            Fix(element, size, size);
            if (!string.IsNullOrEmpty(mark))
            {
                Text letter = TextChild(element.transform, mark, UiTextStyle.Small);
                letter.alignment = TextAnchor.MiddleCenter;
                letter.font = UiTheme.BoldFont;
                letter.fontSize = Mathf.Max(8, Mathf.RoundToInt(size * 0.62f));
                letter.color = ColorVision.TextOn(color);
                letter.horizontalOverflow = HorizontalWrapMode.Overflow;
            }

            return image;
        }

        public void Space(float height = 12f) => Fix(NewChild("Space"), height: height);

        /// <summary>A short bold line in small capitals above a heading, in a colour of its own.</summary>
        public Text Eyebrow(string text, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            Text label = Label(Lang.Upper(text), UiTextStyle.Small, color, alignment);
            label.font = UiTheme.BoldFont;
            return label;
        }

        /// <summary>A pill with a few bold words in capitals on a tinted ground: a tag on a card, a status beside a name. It keeps its own width inside a row that does not share out the width.</summary>
        public Text Chip(string text, Color fill, Color ink, float height = 30f)
        {
            text = Lang.Upper(text);
            GameObject element = NewChild($"Chip {text}");
            Image image = UiRoot.Background((RectTransform)element.transform, fill);
            image.sprite = UiSprites.Pill;
            image.raycastTarget = false;
            Fix(element, height: height);
            var row = element.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(14, 14, 0, 0);
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            Text label = TextChild(element.transform, text, UiTextStyle.Small, stretch: false);
            label.font = UiTheme.BoldFont;
            label.color = ink;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            return label;
        }

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
            UiRoot.Card((RectTransform)element.transform, color);

            AddColumnLayout(element, padding, 6f);
            return new UiBuilder((RectTransform)element.transform, Changed);
        }

        /// <summary>A heading with an icon before it and a thin rule under it, for the top of a window or a panel.</summary>
        public Text Title(UiIcon icon, string text, Color? color = null)
        {
            UiBuilder row = Row(10f, expand: false);
            row.Icon(icon, 28f, color ?? UiTheme.Text);
            Text label = row.Label(text, UiTextStyle.Heading);
            Fix(label, -1f).flexibleWidth = 1f;
            Rule();
            return label;
        }

        /// <summary>
        /// The top of a card that asks for a decision: a colour swatch (a property's group) or
        /// nothing, the title, a line under it and the same rule every window has, so the city,
        /// building, debt, auction and trade cards all open the same way.
        /// </summary>
        public Text CardHeader(string title, string subtitle = null, Color? swatch = null, string mark = null)
        {
            UiBuilder row = Row(10f, expand: false);
            if (swatch.HasValue) row.Swatch(swatch.Value, 26f, mark: mark);
            Text label = row.Label(title, UiTextStyle.Heading);
            Fix(label, -1f).flexibleWidth = 1f;
            if (!string.IsNullOrEmpty(subtitle)) Label(subtitle, UiTextStyle.Small);
            Rule();
            return label;
        }

        /// <summary>A thin accent line across the container.</summary>
        public void Rule()
        {
            GameObject element = NewChild("Rule");
            // The kit draws no heavy lines: a hairline of ink, barely there.
            Image line = UiRoot.Background((RectTransform)element.transform, new Color(UiTheme.Text.r, UiTheme.Text.g, UiTheme.Text.b, 0.14f), rounded: false);
            line.raycastTarget = false;
            Fix(element, height: 2f);
        }

        /// <summary>
        /// The buttons at the foot of a card, always in the same order: the way out on the left,
        /// the action on the right. Pass a null <paramref name="cancelLabel"/> for a card with one button.
        /// </summary>
        public Button Actions(string cancelLabel, Action cancel, string primaryLabel, Action primary, bool primaryEnabled = true,
            UiButtonStyle cancelStyle = UiButtonStyle.Secondary)
        {
            UiBuilder row = Row();
            if (cancelLabel != null) row.Button(cancelLabel, cancel, style: cancelStyle, height: UiLayout.CardButtonHeight);
            return row.Button(primaryLabel, primary, primaryEnabled, UiButtonStyle.Primary, UiLayout.CardButtonHeight);
        }

        /// <summary>
        /// A warning in a tinted card: what went wrong and, in the same sentence or the next, what
        /// to do about it. An optional button does that thing.
        /// </summary>
        public UiBuilder Notice(string text, string actionLabel = null, Action action = null)
        {
            UiBuilder card = Card(UiTheme.NoticeFill, 12);
            UiBuilder row = card.Row(10f, expand: false);
            row.Icon(UiIcon.Warning, 24f, UiTheme.Warning);
            Fix(row.Label(text, UiTextStyle.Small, UiTheme.Text), -1f).flexibleWidth = 1f;
            if (actionLabel != null) Fix(card.Button(actionLabel, action, height: UiTheme.SmallButtonHeight), width: 180f);
            return card;
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
