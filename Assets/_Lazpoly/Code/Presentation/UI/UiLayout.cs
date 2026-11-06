using UnityEngine;

namespace Lazpoly.Presentation.UI
{
    /// <summary>
    /// The fixed measures of the interface, in pixels of the 1920x1080 reference screen, and the
    /// arithmetic that turns them into screen pixels. The canvas scales with the screen (half by
    /// width, half by height, as <see cref="UnityEngine.UI.CanvasScaler"/> does) and again by the
    /// interface size setting, so the same numbers serve a 1280x720 laptop and a 4K television.
    /// </summary>
    public static class UiLayout
    {
        /// <summary>Width of a player's card in a corner of the game screen (the player card kit's 320 by 112 card, drawn a little larger).</summary>
        public const float CornerWidth = 360f;

        /// <summary>Diameter of a big round button with a caption under it.</summary>
        public const float DockButton = 64f;

        /// <summary>
        /// Reference pixels kept clear of the board at each side edge. The game screen once had a dock of
        /// buttons on the right that took this much (with its margins); the board keeps the framing it had
        /// then, so it stays clear of the corner cards at every supported screen.
        /// </summary>
        public const float SideReserve = 148f;

        /// <summary>Gap between the screen edge and a panel, and between panels.</summary>
        public const float Margin = 16f;

        /// <summary>Width of a decision window: city card, building card, pause menu, settings, end of game.</summary>
        public const float WindowWidth = 520f;

        /// <summary>Width of the trade window, which shows two property lists side by side.</summary>
        public const float WideWindowWidth = 760f;

        /// <summary>Width of the property card, whose six level tiles sit in one row.</summary>
        public const float PropertyCardWidth = 880f;

        /// <summary>Height of the buttons that decide something on a card.</summary>
        public const float CardButtonHeight = 52f;

        /// <summary>The big "roll" button over the lawn while it is this person's roll: its size, and how far below the screen's centre it sits.</summary>
        public const float RollPromptWidth = 300f;
        public const float RollPromptHeight = 76f;
        public const float RollPromptDrop = 110f; // Faz 23: higher, off the bottom row's squares (was 170).

        /// <summary>
        /// Screen pixels kept clear of the board at each side edge (<see cref="SideReserve"/>). The corner
        /// cards sit in the empty triangles beside the diamond-shaped board, so they reserve nothing more.
        /// </summary>
        public static float ReservedSidePixels(float canvasScale) => SideReserve * canvasScale;

        /// <summary>How far in from the right edge the right-hand corner cards sit: the same margin as on the left.</summary>
        public const float RightInset = Margin;

        /// <summary>
        /// The factor the canvas scaler applies for a screen of this size: the geometric mean of
        /// the width and height ratios to the reference, times the interface size setting.
        /// </summary>
        public static float CanvasScale(int screenWidth, int screenHeight, float uiScale = 1f)
        {
            float byWidth = Mathf.Log(screenWidth * uiScale / UiTheme.ReferenceResolution.x, 2f);
            float byHeight = Mathf.Log(screenHeight * uiScale / UiTheme.ReferenceResolution.y, 2f);
            return Mathf.Pow(2f, 0.5f * byWidth + 0.5f * byHeight);
        }

        /// <summary>Screen pixels left for the board between the side reserves.</summary>
        public static float BoardAreaPixels(int screenWidth, int screenHeight, float uiScale = 1f) =>
            screenWidth - 2f * ReservedSidePixels(CanvasScale(screenWidth, screenHeight, uiScale));

        /// <summary>Whether a window of <paramref name="width"/> by <paramref name="height"/> reference pixels fits the screen with a margin all round.</summary>
        public static bool WindowFits(float width, float height, int screenWidth, int screenHeight, float uiScale = 1f)
        {
            float scale = CanvasScale(screenWidth, screenHeight, uiScale);
            return (width + 2f * Margin) * scale <= screenWidth && (height + 2f * Margin) * scale <= screenHeight;
        }
    }
}
