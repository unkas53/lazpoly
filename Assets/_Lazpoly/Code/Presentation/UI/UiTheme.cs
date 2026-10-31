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

        public static readonly Color Backdrop = new Color(0.07f, 0.08f, 0.10f, 1f);
        public static readonly Color Dim = new Color(0f, 0f, 0f, 0.6f);
        public static readonly Color Panel = new Color(0.11f, 0.12f, 0.15f, 0.94f);
        public static readonly Color Card = new Color(0.17f, 0.18f, 0.23f, 1f);
        public static readonly Color Field = new Color(0.08f, 0.09f, 0.11f, 1f);
        public static readonly Color Text = new Color(0.93f, 0.93f, 0.95f);
        public static readonly Color Muted = new Color(0.64f, 0.66f, 0.72f);
        public static readonly Color Accent = new Color(0.96f, 0.70f, 0.20f);
        public static readonly Color Warning = new Color(0.95f, 0.45f, 0.40f);
        public static readonly Color Gain = new Color(0.45f, 0.85f, 0.50f);

        private static readonly Color PrimaryFill = Accent;
        private static readonly Color SecondaryFill = new Color(0.27f, 0.29f, 0.36f);
        private static readonly Color DangerFill = new Color(0.72f, 0.24f, 0.24f);
        private static readonly Color SelectedFill = new Color(0.30f, 0.55f, 0.85f);
        private static readonly Color DarkText = new Color(0.10f, 0.08f, 0.04f);

        public const float Spacing = 8f;
        public const int Padding = 16;
        public const float ButtonHeight = 44f;
        public const float SmallButtonHeight = 34f;
        public const float FieldHeight = 40f;
        public const float CornerRadius = 10f;

        private static Font _font;

        public static Font Font => _font != null ? _font : _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

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
            text.font = Font;
            text.fontSize = FontSize(style);
            text.fontStyle = style == UiTextStyle.Title || style == UiTextStyle.Heading ? FontStyle.Bold : FontStyle.Normal;
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
