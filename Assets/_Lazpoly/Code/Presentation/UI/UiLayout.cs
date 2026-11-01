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
        /// <summary>Width of each side column of the game screen.</summary>
        public const float SideWidth = 380f;

        /// <summary>Gap between the screen edge and a panel, and between panels.</summary>
        public const float Margin = 16f;

        /// <summary>Width of a decision window: city card, building card, pause menu, settings, end of game.</summary>
        public const float WindowWidth = 520f;

        /// <summary>Width of the trade window, which shows two property lists side by side.</summary>
        public const float WideWindowWidth = 760f;

        /// <summary>Height of the buttons that decide something on a card.</summary>
        public const float CardButtonHeight = 52f;

        /// <summary>Screen pixels each side column takes, margins on both sides included.</summary>
        public static float ReservedSidePixels(float canvasScale) => (SideWidth + 2f * Margin) * canvasScale;

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

        /// <summary>Screen pixels left for the board between the two side columns.</summary>
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
