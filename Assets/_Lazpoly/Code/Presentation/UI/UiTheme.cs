using UnityEngine;
using UnityEngine.UI;

namespace Lazpoly.Presentation.UI
{
    public enum UiTextStyle
    {
        Title,
        Heading,
        Body,
        Small,
    }

    public enum UiButtonStyle
    {
        Primary,
        Secondary,
        Danger,
        Selected,
    }

    /// <summary>Colours, font and sizes of the interface, in pixels of the 1920x1080 reference screen.</summary>
    public static class UiTheme
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        // Palette of a seaside town at dusk: deep sea blues for the panels, sand for the text,
        // sunset amber as the accent and a teal for anything selected.
        public static readonly Color Backdrop = new Color(0.05f, 0.09f, 0.13f, 1f);
        public static readonly Color Dim = new Color(0.01f, 0.03f, 0.06f, 0.62f);
        /// <summary>A thinner veil than <see cref="Dim"/>, over the 3D scene behind the main menu.</summary>
        public static readonly Color Veil = new Color(0.02f, 0.04f, 0.07f, 0.38f);
        public static readonly Color Panel = new Color(0.09f, 0.15f, 0.21f, 0.94f);
        public static readonly Color Card = new Color(0.14f, 0.22f, 0.30f, 1f);
        public static readonly Color Field = new Color(0.06f, 0.11f, 0.16f, 1f);
        public static readonly Color Text = new Color(0.96f, 0.93f, 0.86f);
        public static readonly Color Muted = new Color(0.66f, 0.72f, 0.76f);
        public static readonly Color Accent = new Color(0.98f, 0.70f, 0.24f);
        public static readonly Color Warning = new Color(0.96f, 0.47f, 0.40f);
        public static readonly Color Gain = new Color(0.48f, 0.84f, 0.56f);

        private static readonly Color PrimaryFill = Accent;
        private static readonly Color SecondaryFill = new Color(0.22f, 0.33f, 0.42f);
        private static readonly Color DangerFill = new Color(0.70f, 0.26f, 0.24f);
        private static readonly Color SelectedFill = new Color(0.16f, 0.58f, 0.60f);
        private static readonly Color DarkText = new Color(0.12f, 0.08f, 0.03f);

        public const float Spacing = 8f;
        public const int Padding = 16;
        public const float ButtonHeight = 44f;
        public const float SmallButtonHeight = 34f;
        public const float FieldHeight = 40f;
        public const float CornerRadius = 10f;

        private const string RegularFontResource = "Lazpoly/Fonts/Nunito-Regular";
        private const string BoldFontResource = "Lazpoly/Fonts/Nunito-Bold";

        private static Font _font;
        private static Font _boldFont;

        /// <summary>The game's typeface (Nunito, SIL OFL); falls back to Unity's built-in font if the asset is missing.</summary>
        public static Font Font => _font != null ? _font : _font = Load(RegularFontResource);

        public static Font BoldFont => _boldFont != null ? _boldFont : _boldFont = Load(BoldFontResource);

        private static Font Load(string resource)
        {
            Font font = Resources.Load<Font>(resource);
            if (font != null) return font;
            Debug.LogWarning($"[Lazpoly] Font '{resource}' is missing; using the built-in font.");
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public static bool IsBold(UiTextStyle style) => style == UiTextStyle.Title || style == UiTextStyle.Heading;

        public static int FontSize(UiTextStyle style)
        {
            switch (style)
            {
                case UiTextStyle.Title: return 64;
                case UiTextStyle.Heading: return 26;
                case UiTextStyle.Small: return 17;
                default: return 20;
            }
        }

        public static void Apply(Text text, UiTextStyle style)
        {
            text.font = IsBold(style) ? BoldFont : Font;
            text.fontSize = FontSize(style);
            text.fontStyle = FontStyle.Normal;
            text.color = style == UiTextStyle.Small ? Muted : Text;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
        }

        public static Color Fill(UiButtonStyle style)
        {
            switch (style)
            {
                case UiButtonStyle.Primary: return PrimaryFill;
                case UiButtonStyle.Danger: return DangerFill;
                case UiButtonStyle.Selected: return SelectedFill;
                default: return SecondaryFill;
            }
        }

        public static Color Label(UiButtonStyle style) => style == UiButtonStyle.Primary ? DarkText : Text;
    }
}
