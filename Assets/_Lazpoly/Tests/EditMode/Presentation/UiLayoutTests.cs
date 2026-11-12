using Lazpoly.Infra.Settings;
using Lazpoly.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using BuildingPreview = Lazpoly.Presentation.Models.BuildingPreview;
using PropertyCard = Lazpoly.Presentation.Game.PropertyCard;
using SquareCard = Lazpoly.Presentation.Game.SquareCard;

namespace Lazpoly.Presentation.Tests
{
    /// <summary>
    /// The screen sizes the game is meant to run on, at every interface size: the board must keep
    /// room at its sides and every window must fit with a margin.
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
            Assert.AreEqual(UiLayout.SideReserve, UiLayout.ReservedSidePixels(1f));
            Assert.AreEqual(2f * UiLayout.SideReserve, UiLayout.ReservedSidePixels(2f));
            Assert.AreEqual(UiLayout.Margin, UiLayout.RightInset, "without the dock the right-hand cards sit at the same margin as the left-hand ones");
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

        /// <summary>The property card: its band, the large picture (beside it the strip and the box, with a notice when the money falls short) and the big button, in reference pixels.</summary>
        private const float PropertyCardHeight = 680f;

        /// <summary>The square card of a developed city: band, picture row, seven rent rows, group chips, notes and the button.</summary>
        private const float SquareCardHeight = 760f;

        [Test]
        public void TheSquareCardFitsEveryScreenWithItsPictureBesideTheFacts()
        {
            Assert.Less(Lazpoly.Presentation.Game.SquareCard.PictureSize + 14f + 200f, UiLayout.WindowWidth - 2f * (UiTheme.Padding + 8f), "the facts beside the picture get under 200 pixels");
            foreach (Vector2Int screen in Screens)
            {
                foreach (float scale in Scales)
                {
                    Assert.IsTrue(UiLayout.WindowFits(UiLayout.WindowWidth, SquareCardHeight, screen.x, screen.y, scale),
                        $"The square card does not fit {screen.x}x{screen.y} at {scale:0.00}.");
                }
            }
        }

        [Test]
        public void ThePropertyCardHoldsSixTilesInARowAndFitsEveryScreen()
        {
            float tiles = BuildingPreview.Levels * PropertyCard.TileWidth + (BuildingPreview.Levels - 1) * PropertyCard.StripGap;
            float inside = UiLayout.PropertyCardWidth - 2f * (UiTheme.Padding + 8f);
            Assert.LessOrEqual(PropertyCard.HeroWidth + PropertyCard.BodyGap + tiles, inside, "the picture and the tiles overflow the window's padding");
            Assert.GreaterOrEqual(PropertyCard.HeroWidth + PropertyCard.BodyGap + tiles, inside - 8f, "and fill it");
            Assert.LessOrEqual(PropertyCard.PictureWidth + 12f, PropertyCard.TileWidth, "the picture overflows its tile");
            // Every picture on the cards keeps the upright shape the pictures are drawn in, so nothing is squeezed.
            float shape = (float)BuildingPreview.PictureWidth / BuildingPreview.PictureHeight;
            Assert.That(PropertyCard.HeroWidth / PropertyCard.HeroHeight, Is.EqualTo(shape).Within(1e-3f));
            Assert.That(PropertyCard.PictureWidth / PropertyCard.PictureHeight, Is.EqualTo(shape).Within(1e-3f));
            Assert.That(SquareCard.PictureSize / SquareCard.PictureHeight, Is.EqualTo(shape).Within(1e-3f));
            // The band, the picture row and the button leave the window under the height the screens are checked against.
            float height = 2f * (UiTheme.Padding + 8f) + PropertyCard.BandHeight + PropertyCard.HeroHeight + PropertyCard.ConfirmHeight + 2f * UiTheme.Spacing;
            Assert.LessOrEqual(height + 60f, PropertyCardHeight, "with room for the notice when the money falls short");
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
