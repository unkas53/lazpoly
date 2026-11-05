using System;
using System.Collections.Generic;
using Lazpoly.Presentation.Audio;
using Lazpoly.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Lazpoly.Presentation.Menu
{
    /// <summary>
    /// The owner's main menu kit: its pictures (city background, wide logo, character portraits, button and panel
    /// skins, icons), shipped as PNG/JPEG bytes under Resources/Lazpoly/Art/Menu (Tools/Art/make_menu_art.py) and
    /// decoded once into sprites; and the few ways the menu puts them on screen, in the kit's 1920x1080 design
    /// coordinates (x and y from the top left). A missing picture leaves a plain shape, never an error.
    /// </summary>
    public static class MenuKit
    {
        public const string Folder = "Lazpoly/Art/Menu/";

        public const string Background = "Higgsfield_City_Background";
        public const string Logo = "Lazpoly_Logo";
        public const string Panel = "Panel";
        public const string Chip = "Chip";
        public const string Circle = "Circle";

        /// <summary>The kit's colours: dark petrol ink, its muted grey-blue, cream labels on the dark button and its brass icons.</summary>
        public static readonly Color Ink = Hex(0x163D50);
        public static readonly Color Muted = Hex(0x607982);
        public static readonly Color Cream = Hex(0xFFFDF5);
        public static readonly Color Brass = Hex(0xE9CE8B);
        public static readonly Color LightBrass = Hex(0xF5DEAA);

        /// <summary>The dimmed layer under the kit's windows.</summary>
        public static readonly Color Dim = new Color(0x14 / 255f, 0x37 / 255f, 0x47 / 255f, 0xB8 / 255f);

        /// <summary>Every picture the menu uses, so a test can check they are all in the build.</summary>
        public static readonly string[] Pictures =
        {
            Background, Logo, Panel, Chip, Circle,
            "Primary_Normal", "Primary_Hover", "Primary_Pressed", "Primary_Disabled",
            "Secondary_Normal", "Secondary_Hover", "Secondary_Pressed", "Secondary_Disabled",
            "Icon_Arrow", "Icon_Book", "Icon_Case", "Icon_Close", "Icon_Dice", "Icon_Gear", "Icon_Globe",
            "Icon_Join", "Icon_Mute", "Icon_People", "Icon_Plus", "Icon_Sound",
            "Portrait_Woman01", "Portrait_Woman02", "Portrait_Man01", "Portrait_Man02",
        };

        /// <summary>The 9-slice borders of the stretchable skins, in picture pixels (the kit's SpriteManifest.json).</summary>
        public static Vector4 Borders(string name)
        {
            if (name.StartsWith("Primary_", StringComparison.Ordinal) || name.StartsWith("Secondary_", StringComparison.Ordinal)) return new Vector4(32f, 32f, 32f, 32f);
            if (name == Panel) return new Vector4(36f, 36f, 36f, 36f);
            if (name == Chip) return new Vector4(30f, 30f, 30f, 30f);
            return Vector4.zero;
        }

        /// <summary>The kit's button looks: the wide dark one (Hemen Oyna) and the cream ones.</summary>
        public enum Skin { Primary, Secondary }

        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static readonly HashSet<string> Missing = new HashSet<string>();

        /// <summary>A kit picture as a sprite (sliced when it has borders), or null when it is not in the build.</summary>
        public static Sprite Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Sprites.TryGetValue(name, out Sprite cached) && cached != null) return cached;
            if (Missing.Contains(name)) return null;

            var asset = Resources.Load<TextAsset>(Folder + name);
            Texture2D texture = asset != null ? MenuArt.Decode(asset.bytes, name, mipmaps: Borders(name) == Vector4.zero) : null;
            if (texture == null)
            {
                Missing.Add(name);
                Debug.LogWarning($"[Lazpoly] Menu picture '{Folder}{name}' is missing; run Tools/Art/make_menu_art.py.");
                return null;
            }

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, Borders(name));
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            Sprites[name] = sprite;
            return sprite;
        }

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
        /// One of the kit's buttons at a design position: its skin swaps on hover and press, an icon at the left
        /// (and an optional one at the right) and a label after it. Clicks sound like every other button.
        /// </summary>
        public static Button ButtonAt(Transform parent, string name, Rect design, Skin skin, string icon, string label, int fontSize,
            Action onClick, string trailingIcon = null)
        {
            string kind = skin == Skin.Primary ? "Primary_" : "Secondary_";
            Color ink = skin == Skin.Primary ? Cream : Ink;
            Color iconInk = skin == Skin.Primary ? Brass : Ink;

            RectTransform rect = Place(parent, name, design.x, design.y, design.width, design.height);
            Image skinImage = Picture(rect, kind + "Normal", Color.white);
            skinImage.raycastTarget = true;
            if (skinImage.sprite == null) skinImage.color = skin == Skin.Primary ? Ink : Cream;

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

            float iconSize = 32f;
            float left = 22f;
            if (!string.IsNullOrEmpty(icon))
            {
                Picture(Place(rect, "Icon", left, (design.height - iconSize) / 2f, iconSize, iconSize), icon, iconInk);
                left += iconSize + 12f;
            }

            float right = string.IsNullOrEmpty(trailingIcon) ? 16f : 22f + iconSize + 12f;
            Words(Place(rect, "Label", left, 0f, design.width - left - right, design.height), label, fontSize, ink, bold: true);
            if (!string.IsNullOrEmpty(trailingIcon))
            {
                Picture(Place(rect, "Arrow", design.width - 22f - iconSize, (design.height - iconSize) / 2f, iconSize, iconSize), trailingIcon, LightBrass);
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

        /// <summary>A square kit button holding only an icon (the sound and settings buttons, a window's close cross).</summary>
        public static Button IconButtonAt(Transform parent, string name, Rect design, string icon, Action onClick)
        {
            Button button = ButtonAt(parent, name, design, Skin.Secondary, null, "", 12, onClick);
            float size = Mathf.Min(design.width, design.height) * 0.5f;
            Picture(Place(button.transform, "Icon", (design.width - size) / 2f, (design.height - size) / 2f, size, size), icon, Ink);
            return button;
        }

        private static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Sprites.Clear();
            Missing.Clear();
        }
    }
}
