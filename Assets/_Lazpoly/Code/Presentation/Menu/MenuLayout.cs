using UnityEngine;

namespace Lazpoly.Presentation.Menu
{
    /// <summary>
    /// The measures of the main menu, in pixels of the 1920x1080 reference screen. The menu is one paper
    /// "window" with a green masthead, laid over the board behind it; the home page under the masthead
    /// is a title strip, a grid of cards three wide and two tall, and a footer line. Layout groups place
    /// the parts at run time; the arithmetic here says what size they come out at, for the tests.
    /// </summary>
    public static class MenuLayout
    {
        /// <summary>Sky showing round the window.</summary>
        public const float WindowMargin = 40f;

        /// <summary>The masthead band across the top of the window, tall enough for the logo.</summary>
        public const float MastheadHeight = 128f;

        /// <summary>Room between the window's edge and what is written on it.</summary>
        public const float Padding = 36f;

        /// <summary>The title strip under the masthead: eyebrow, greeting and a line under it.</summary>
        public const float TitleHeight = 136f;

        /// <summary>The logo picture at the left of the masthead (it is square).</summary>
        public const float LogoSize = 116f;

        /// <summary>Between the title strip, the cards and the footer.</summary>
        public const float RowGap = 20f;

        /// <summary>The footer line at the bottom of the window: the motto and the big button.</summary>
        public const float FooterHeight = 72f;

        /// <summary>Gap between cards.</summary>
        public const float CardGap = 24f;

        public const int CardColumns = 3;
        public const int CardRows = 2;
        public const int CardCount = CardColumns * CardRows;

        /// <summary>The home page's cards never get shorter than this, so their title, line and chip fit.</summary>
        public const float MinCardHeight = 170f;

        /// <summary>The coloured stripe down a card's left edge.</summary>
        public const float CardStripe = 9f;

        /// <summary>A mode's setup sheet in the middle of the window.</summary>
        public const float SheetWidth = 860f;

        /// <summary>The big button at the right of the footer.</summary>
        public static readonly Vector2 FooterButton = new Vector2(300f, 56f);

        /// <summary>The page under the masthead, inside the padding: how tall it is on a screen of this height.</summary>
        public static float PageHeight(float screenHeight) => screenHeight - 2f * WindowMargin - MastheadHeight - 2f * Padding;

        /// <summary>How tall each home card comes out on a screen of this height, before the minimum applies.</summary>
        public static float CardHeight(float screenHeight) =>
            (PageHeight(screenHeight) - TitleHeight - FooterHeight - 2f * RowGap - (CardRows - 1) * CardGap) / CardRows;

        /// <summary>How wide each home card comes out on a screen of this width.</summary>
        public static float CardWidth(float screenWidth) =>
            (screenWidth - 2f * WindowMargin - 2f * Padding - (CardColumns - 1) * CardGap) / CardColumns;

        /// <summary>The smallest reference screen height at which the home page's parts stack without overlapping.</summary>
        public static float MinScreenHeight =>
            2f * WindowMargin + MastheadHeight + 2f * Padding + TitleHeight + FooterHeight + 2f * RowGap + CardRows * MinCardHeight + (CardRows - 1) * CardGap;
    }
}
