using System;
using Lazpoly.Presentation.Audio;
using Lazpoly.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Lazpoly.Presentation.Menu
{
    /// <summary>
    /// The menu's pictures (the logo, character portraits, button and panel skins, icons), shipped as PNG bytes under
    /// Resources/Lazpoly/Art/Menu (the skins by Tools/Art/make_ui_skins.py) and decoded once into sprites; and the
    /// few ways the menu puts them on screen, in its 1920x1080 design coordinates (x and y from the top left).
    /// A missing picture leaves a plain shape, never an error.
    /// </summary>
    public static class MenuKit
    {
        public const string Folder = KitArt.Folder;

        public const string Logo = "Lazpoly_Logo";
        public const string Panel = KitArt.Panel;
        public const string Chip = KitArt.Chip;
        public const string Circle = KitArt.Circle;

        /// <summary>The menu's colours: deep navy ink, its muted grey-blue, ivory labels on the navy button and its gold icons.</summary>
        public static readonly Color Ink = Hex(0x14213F);
        public static readonly Color Muted = Hex(0x5A6684);
        public static readonly Color Cream = Hex(0xFFFCF3);
        public static readonly Color Brass = Hex(0xF2C75C);
        public static readonly Color LightBrass = Hex(0xFFE7A0);

        /// <summary>The quieter lines written on the navy rail.</summary>
        public static readonly Color OnNight = Hex(0xC2CCE4);

        /// <summary>The dimmed layer under the menu's windows.</summary>
        public static Color Dim => UiBlend.AsTunedLayer(new Color(0x08 / 255f, 0x10 / 255f, 0x2A / 255f, 0xBE / 255f));

        /// <summary>Every picture the menu uses, so a test can check they are all in the build.</summary>
        public static readonly string[] Pictures =
        {
            Logo, Panel, Chip, Circle,
            "Hero_Normal", "Hero_Hover", "Hero_Pressed", "Hero_Disabled",
            "Primary_Normal", "Primary_Hover", "Primary_Pressed", "Primary_Disabled",
            "Secondary_Normal", "Secondary_Hover", "Secondary_Pressed", "Secondary_Disabled",
            "Icon_Arrow", "Icon_Book", "Icon_Case", "Icon_Close", "Icon_Dice", "Icon_Gear", "Icon_Globe",
            "Icon_Join", "Icon_Mute", "Icon_People", "Icon_Plus", "Icon_Sound", "Icon_Home", "Icon_Check", "Icon_Hotel",
        };

        /// <summary>The 9-slice borders of the stretchable skins, in picture pixels (see <see cref="KitArt.Borders"/>).</summary>
        public static Vector4 Borders(string name) => KitArt.Borders(name);

        /// <summary>The button looks: the gold one (Hemen Oyna), the navy ones (the ways into a game) and the ivory ones.</summary>
        public enum Skin { Primary, Secondary, Hero }

        /// <summary>A kit picture as a sprite (sliced when it has borders), or null when it is not in the build.</summary>
        public static Sprite Get(string name) => KitArt.Get(name);

        /// <summary>A rectangle at the kit's design position: <paramref name="x"/>, <paramref name="y"/> from the parent's top left, in design pixels.</summary>
        public static RectTransform Place(Transform parent, string name, float x, float y, float w, float h)
        {
            RectTransform rect = UiRoot.Rect(parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        /// <summary>A kit picture filling <paramref name="rect"/>; <paramref name="color"/> tints it (icons are white).</summary>
        public static Image Picture(RectTransform rect, string name, Color color, bool keepAspect = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = Get(name);
            image.color = color;
            image.raycastTarget = false;
            image.preserveAspect = keepAspect;
            if (image.sprite != null && image.sprite.border != Vector4.zero) image.type = Image.Type.Sliced;
            return image;
        }

        /// <summary>A line of text in the kit's look, in the game's own Nunito.</summary>
        public static Text Words(RectTransform rect, string value, int size, Color color, bool bold = false, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var text = rect.gameObject.AddComponent<Text>();
            text.font = bold ? UiTheme.BoldFont : UiTheme.Font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(10, size * 2 / 3);
            text.resizeTextMaxSize = size;
            return text;
        }

        /// <summary>
        /// A button at a design position: its skin swaps on hover and press and it lifts and sinks under the pointer
        /// (<see cref="UiHover"/>); an icon at the left (and an optional one at the right) and a label after it, all
        /// standing on the key, above its lip. Clicks sound like every other button.
        /// </summary>
        public static Button ButtonAt(Transform parent, string name, Rect design, Skin skin, string icon, string label, int fontSize,
            Action onClick, string trailingIcon = null)
        {
            string kind = skin == Skin.Hero ? "Hero_" : skin == Skin.Primary ? "Primary_" : "Secondary_";
            Color ink = skin == Skin.Primary ? Cream : Ink;
            Color iconInk = skin == Skin.Primary ? Brass : Ink;

            RectTransform rect = Place(parent, name, design.x, design.y, design.width, design.height);
            // Buttons grow about their middle under the pointer.
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(design.x + design.width / 2f, -design.y - design.height / 2f);
            Image skinImage = Picture(rect, kind + "Normal", Color.white);
            skinImage.raycastTarget = true;
            if (skinImage.sprite == null) skinImage.color = skin == Skin.Hero ? Brass : skin == Skin.Primary ? Ink : Cream;

            rect.gameObject.AddComponent<UiHover>();
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = skinImage;
            if (skinImage.sprite != null)
            {
                button.transition = Selectable.Transition.SpriteSwap;
                button.spriteState = new SpriteState
                {
                    highlightedSprite = Get(kind + "Hover"),
                    selectedSprite = Get(kind + "Hover"),
                    pressedSprite = Get(kind + "Pressed"),
                    disabledSprite = Get(kind + "Disabled"),
                };
            }

            // What is written stands in the middle of the key, not of the picture with its lip (see KitArt.ButtonFoot).
            float key = design.height - (skinImage.sprite != null ? KitArt.ButtonFootPixels : 0f);
            float iconSize = Mathf.Min(32f, key * 0.5f);
            float left = Mathf.Max(18f, key * 0.3f);
            if (!string.IsNullOrEmpty(icon))
            {
                Picture(Place(rect, "Icon", left, (key - iconSize) / 2f, iconSize, iconSize), icon, iconInk);
                left += iconSize + 12f;
            }

            float right = string.IsNullOrEmpty(trailingIcon) ? 16f : 24f + iconSize + 10f;
            Words(Place(rect, "Label", left, 0f, design.width - left - right, key), label, fontSize, ink, bold: true);
            if (!string.IsNullOrEmpty(trailingIcon))
            {
                Picture(Place(rect, "Arrow", design.width - 24f - iconSize, (key - iconSize) / 2f, iconSize, iconSize), trailingIcon,
                    skin == Skin.Primary ? LightBrass : Ink);
            }

            if (onClick != null)
            {
                button.onClick.AddListener(() =>
                {
                    AudioPlayer.Play(SoundCue.UiClick);
                    onClick();
                });
            }

            return button;
        }

        /// <summary>A square button holding only an icon (the sound button, a window's close cross).</summary>
        public static Button IconButtonAt(Transform parent, string name, Rect design, string icon, Action onClick)
        {
            Button button = ButtonAt(parent, name, design, Skin.Secondary, null, "", 12, onClick);
            float key = design.height - (button.transition == Selectable.Transition.SpriteSwap ? KitArt.ButtonFootPixels : 0f);
            float size = Mathf.Min(design.width, key) * 0.5f;
            Picture(Place(button.transform, "Icon", (design.width - size) / 2f, (key - size) / 2f, size, size), icon, Ink);
            return button;
        }

        private static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
