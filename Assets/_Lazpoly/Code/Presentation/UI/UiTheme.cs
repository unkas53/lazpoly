using UnityEngine;
using UnityEngine.UI;

namespace Lazpoly.Presentation.UI
{
    public enum UiTextStyle
    {
        Title,
        /// <summary>A screen's one-line greeting, between the title and a heading.</summary>
        Display,
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

    /// <summary>
    /// Colours, font and sizes of the interface, in pixels of the 1920x1080 reference screen. The colours
    /// come from the <see cref="UiPalette"/> in force: <see cref="UiPalette.Paper"/> everywhere (menu, lobby
    /// and table); <see cref="UiPalette.Dusk"/>, the table's first dark look, stays for comparison (see <see cref="Use"/>).
    /// </summary>
    public static class UiTheme
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        /// <summary>The smallest a button label shrinks to before it is allowed to wrap (see <see cref="UiBuilder.ShrinkToFit"/>).</summary>
        public const int MinButtonFontSize = 12;

        private static UiPalette _palette = UiPalette.Paper;

        /// <summary>The colours in force; elements built from now on take them.</summary>
        public static UiPalette Palette => _palette;

        /// <summary>Switches the colours: a scene calls this before it builds its interface.</summary>
        public static void Use(UiPalette palette) => _palette = palette ?? throw new System.ArgumentNullException(nameof(palette));

        public static Color Backdrop => _palette.Backdrop;
        public static Color Dim => UiBlend.AsTunedLayer(_palette.Dim);
        public static Color Veil => UiBlend.AsTuned(_palette.Veil);
        public static Color Panel => _palette.Panel;
        public static Color Card => _palette.Card;
        public static Color Field => _palette.Field;
        public static Color Text => _palette.Text;
        public static Color Muted => _palette.Muted;
        public static Color Accent => _palette.Accent;
        public static Color Warning => _palette.Warning;
        public static Color Gain => _palette.Gain;
        public static Color Money => _palette.Money;
        public static Color ShadowColor => UiBlend.AsTuned(_palette.ShadowColor);
        public static Color NoticeFill => _palette.NoticeFill;
        public static Color ScrollTrack => UiBlend.AsTuned(_palette.ScrollTrack);

        /// <summary>A disc that cannot be pressed right now: a step up from the panel, so the button keeps its shape.</summary>
        public static Color RestingFill => _palette.RestingFill;

        public const float Spacing = 8f;
        public const int Padding = 16;
        public const float ButtonHeight = 44f;
        public const float SmallButtonHeight = 34f;
        public const float FieldHeight = 40f;
        public const float CornerRadius = 10f;

        private const string RegularFontResource = "Lazpoly/Fonts/Nunito-Regular";
        private const string BoldFontResource = "Lazpoly/Fonts/Nunito-Bold";
        private const string BlackFontResource = "Lazpoly/Fonts/Nunito-Black";

        private static Font _font;
        private static Font _boldFont;
        private static Font _blackFont;

        /// <summary>The game's typeface (Nunito, SIL OFL); falls back to Unity's built-in font if the asset is missing.</summary>
        public static Font Font => _font != null ? _font : _font = Load(RegularFontResource);

        public static Font BoldFont => _boldFont != null ? _boldFont : _boldFont = Load(BoldFontResource);

        /// <summary>The heaviest cut (Nunito Black), for text printed on the board that must read from across the table.</summary>
        public static Font BlackFont => _blackFont != null ? _blackFont : _blackFont = Load(BlackFontResource);

        private static Font Load(string resource)
        {
            Font font = Resources.Load<Font>(resource);
            if (font != null) return font;
            Debug.LogWarning($"[Lazpoly] Font '{resource}' is missing; using the built-in font.");
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public static bool IsBold(UiTextStyle style) => style == UiTextStyle.Title || style == UiTextStyle.Display || style == UiTextStyle.Heading;

        public static int FontSize(UiTextStyle style)
        {
            switch (style)
            {
                case UiTextStyle.Title: return 64;
                case UiTextStyle.Display: return 40;
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
                case UiButtonStyle.Primary: return _palette.PrimaryFill;
                case UiButtonStyle.Danger: return _palette.DangerFill;
                case UiButtonStyle.Selected: return _palette.SelectedFill;
                default: return _palette.SecondaryFill;
            }
        }

        public static Color Label(UiButtonStyle style)
        {
            switch (style)
            {
                case UiButtonStyle.Primary: return _palette.PrimaryLabel;
                case UiButtonStyle.Danger:
                case UiButtonStyle.Selected: return _palette.StrongLabel;
                default: return _palette.SecondaryLabel;
            }
        }
    }
}
