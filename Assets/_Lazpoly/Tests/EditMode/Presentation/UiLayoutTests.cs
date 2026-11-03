using Lazpoly.Infra.Settings;
using Lazpoly.Presentation.UI;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    /// <summary>
    /// The screen sizes the game is meant to run on, at every interface size: the board must keep
    /// room beside the dock and every window must fit with a margin.
    /// </summary>
    public class UiLayoutTests
    {
        private static readonly Vector2Int[] Screens =
        {
            new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(1920, 1200), new Vector2Int(2560, 1080), new Vector2Int(3840, 2160),
        };

        private static readonly float[] Scales =
        {
            GameSettings.Default.WithUiScale(UiScale.Small).UiScaleFactor, 1f, GameSettings.Default.WithUiScale(UiScale.Large).UiScaleFactor,
        };

        /// <summary>The tallest windows, in reference pixels: settings (language row, 400 scrolling, button) and trade (two 260 lists and fields).</summary>
        private const float TallestWindow = 620f;

        [Test]
        public void TheRollPromptSitsOverTheLawnClearOfTheCardsAndTheBottomStack()
        {
            // At the smallest supported screen the button still lies between the corner cards and above the bottom stack's hint.
            Assert.Less(UiLayout.RollPromptWidth, 1280f - 2f * UiLayout.CornerWidth - 2f * UiLayout.Margin, "the button runs into the corner cards");
            Assert.Less(UiLayout.RollPromptDrop + UiLayout.RollPromptHeight / 2f, 720f / 2f - 120f, "the button sits on the bottom stack");
            Assert.Greater(UiLayout.RollPromptDrop, UiLayout.RollPromptHeight, "the button covers the dice score in the lawn's middle");
        }

        [Test]
        public void TheReferenceScreenScalesOneToOne()
        {
            Assert.AreEqual(1f, UiLayout.CanvasScale(1920, 1080), 0.001f);
            Assert.AreEqual(2f, UiLayout.CanvasScale(3840, 2160), 0.001f);
            Assert.AreEqual(UiLayout.DockWidth + 2f * UiLayout.Margin, UiLayout.ReservedSidePixels(1f));
            Assert.Less(UiLayout.DockWidth, UiLayout.CornerWidth, "the dock is slimmer than a corner card");
            Assert.AreEqual(UiLayout.ReservedSidePixels(1f), UiLayout.RightInset, "the right-hand cards start where the board's reserve ends");
        }

        [Test]
        public void ABiggerInterfaceLeavesLessBoardButNeverTooLittle()
        {
            foreach (Vector2Int screen in Screens)
            {
                float previous = float.MaxValue;
                foreach (float scale in Scales)
                {
                    float board = UiLayout.BoardAreaPixels(screen.x, screen.y, scale);
                    Assert.Less(board, previous, $"{screen.x}x{screen.y}: a bigger interface should leave less board.");
                    Assert.GreaterOrEqual(board / screen.x, 0.45f, $"{screen.x}x{screen.y} at {scale:0.00}: the board gets under 45% of the width.");
                    previous = board;
                }
            }
        }

        [Test]
        public void EveryWindowFitsEveryScreenAtEveryInterfaceSize()
        {
            foreach (Vector2Int screen in Screens)
            {
                foreach (float scale in Scales)
                {
                    Assert.IsTrue(UiLayout.WindowFits(UiLayout.WideWindowWidth, TallestWindow, screen.x, screen.y, scale),
                        $"The trade window does not fit {screen.x}x{screen.y} at {scale:0.00}.");
                    Assert.IsTrue(UiLayout.WindowFits(UiLayout.WindowWidth, TallestWindow, screen.x, screen.y, scale),
                        $"A decision window does not fit {screen.x}x{screen.y} at {scale:0.00}.");
                }
            }
        }

        /// <summary>The property card: its band, six tiles, three lines and the big button, in reference pixels.</summary>
        private const float PropertyCardHeight = 520f;

        [Test]
        public void ThePropertyCardHoldsSixTilesInARowAndFitsEveryScreen()
        {
            float tiles = 6f * Lazpoly.Presentation.Game.PropertyCard.TileWidth + 5f * UiTheme.Spacing;
            Assert.LessOrEqual(tiles, UiLayout.PropertyCardWidth - 2f * (UiTheme.Padding + 8f), "the tiles overflow the window's padding");
            Assert.LessOrEqual(Lazpoly.Presentation.Game.PropertyCard.PictureSize + 16f, Lazpoly.Presentation.Game.PropertyCard.TileWidth, "the picture overflows its tile");
            foreach (Vector2Int screen in Screens)
            {
                foreach (float scale in Scales)
                {
                    Assert.IsTrue(UiLayout.WindowFits(UiLayout.PropertyCardWidth, PropertyCardHeight, screen.x, screen.y, scale),
                        $"The property card does not fit {screen.x}x{screen.y} at {scale:0.00}.");
                }
            }
        }

        [Test]
        public void UltrawideScalesBetweenItsWidthAndItsHeight()
        {
            float scale = UiLayout.CanvasScale(2560, 1080);
            Assert.Greater(scale, 1f);
            Assert.Less(scale, 2560f / 1920f);
        }
    }
}
