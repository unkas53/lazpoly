using Lazpoly.Presentation.Dice;
using Lazpoly.Presentation.Menu;
using Lazpoly.Presentation.Tabletop;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    public class SceneryTests
    {
        [TestCase(BoardCameraRig.BaseYaw)]
        [TestCase(BoardCameraRig.BaseYaw + 90f)]
        [TestCase(BoardCameraRig.BaseYaw + 180f)]
        [TestCase(BoardCameraRig.BaseYaw - 90f)]
        public void LabelsLieFlatAlongTheirEdgeAndReadLeftToRightFromAnyCorner(float yaw)
        {
            Vector3 cameraRight = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            foreach (Vector3 outward in new[] { Vector3.back, Vector3.forward, Vector3.left, Vector3.right })
            {
                Quaternion rotation = BoardView.LabelRotation(outward, yaw);
                Assert.That(Vector3.Distance(rotation * Vector3.back, Vector3.up), Is.LessThan(1e-4f), $"{outward}: the text does not face up");
                Vector3 tops = rotation * Vector3.up;
                Assert.That(Mathf.Abs(Vector3.Dot(tops, outward)), Is.EqualTo(1f).Within(1e-4f), $"{outward}: the letters do not stand across the edge");
                Vector3 line = rotation * Vector3.right;
                Assert.That(Mathf.Abs(Vector3.Dot(line, outward)), Is.LessThan(1e-4f), $"{outward}: the line does not run along the edge");
                Assert.Greater(Vector3.Dot(line, cameraRight), 0.5f, $"{outward}: the line reads right to left from yaw {yaw}");
            }

            Quaternion facing = BoardView.FlatFacing(yaw);
            Assert.That(Vector3.Distance(facing * Vector3.back, Vector3.up), Is.LessThan(1e-4f), "the score does not face up");
            Assert.That(Vector3.Distance(facing * Vector3.right, cameraRight), Is.LessThan(1e-4f), "the score is not square to the screen");
            Assert.That(Vector3.Distance(BoardView.ScreenUp(yaw), Quaternion.Euler(0f, yaw, 0f) * Vector3.forward), Is.LessThan(1e-4f));
        }

        [Test]
        public void LongNamesShrinkToTheirTileAndTheLawnShowsTheRoll()
        {
            Assert.AreEqual("6  4", BoardView.ScoreText(6, 4));
            Assert.AreEqual("–  –", BoardView.ScoreText(0, 0), "nothing rolled yet");
            Assert.Greater(BoardView.PlateThickness, 0f);
            Assert.Greater(BoardView.LawnHeight, 0f);

            Assert.AreEqual(0.02f, BoardView.FitCharacterSize(0.02f, 1.2f, 1.5f), "a label that fits keeps its size");
            Assert.That(BoardView.FitCharacterSize(0.02f, 2f, 1.5f), Is.EqualTo(0.015f).Within(1e-6f), "a long label shrinks to its tile");
            Assert.AreEqual(0.02f, BoardView.FitCharacterSize(0.02f, 0f, 1.5f), "an unmeasured label is left alone");
            Assert.That(BoardView.FitCharacterSize(0.02f, new Vector2(1f, 0.6f), new Vector2(1.5f, 0.4f)), Is.EqualTo(0.02f * 0.4f / 0.6f).Within(1e-6f),
                "a tall line shrinks to its strip");
            Assert.That(BoardView.FitCharacterSize(0.02f, new Vector2(2f, 0.6f), new Vector2(1.5f, 0.4f)), Is.EqualTo(0.02f * 0.4f / 0.6f).Within(1e-6f),
                "the tighter of the two limits wins");
        }

        [Test]
        public void EverythingOnASquareHasItsOwnStripFromTheInnerEdgeToTheOuter()
        {
            var layout = new BoardLayout(24, 6f, BoardView.DepthRatio);
            float widthInDepths = layout.Width / layout.Depth;
            float pawnReach = PawnSlots.Pitch * widthInDepths; // Half the 3x3 slot grid, in tile depths.

            Assert.Greater(BoardView.BuildingOffset, BoardView.PawnOffset, "the building row lies behind the pawns");
            Assert.Less(BoardView.BuildingOffset + BoardView.HouseSize * widthInDepths / 2f, 0.5f, "houses stay on the tile");
            Assert.Less(-BoardView.PawnOffset + pawnReach, BoardView.NameOffset - BoardView.NameDepth / 2f, "the back row of pawns stops short of the name");
            Assert.LessOrEqual(BoardView.NameOffset + BoardView.NameDepth / 2f, BoardView.PriceOffset - BoardView.PriceDepth / 2f, "the price sits under the name");
            Assert.LessOrEqual(BoardView.PriceOffset + BoardView.PriceDepth / 2f, BoardView.BandOffset - BoardView.BandDepth / 2f, "the band lies beyond the price");
            Assert.Less(BoardView.BandOffset + BoardView.BandDepth / 2f, 0.5f, "the band stays on the tile");
            Assert.Greater(BoardView.NameSize, BoardView.PriceSize, "the name is the larger line");

            // On a corner the scene stands towards the board's corner and the pawns step the other way; neither reaches the other.
            Assert.LessOrEqual(BoardView.SceneOffset + BoardView.SceneSize / 2f, 0.5f, "the scene stays on its corner along the edge");
            Assert.LessOrEqual(BoardView.SceneInset + BoardView.SceneSize / 2f, 0.5f, "the scene stays on its corner across it");
            Assert.Less(-BoardView.CornerPawnShift + pawnReach, BoardView.SceneOffset - BoardView.SceneSize / 2f, "the pawns stay clear of the scene");
            Assert.LessOrEqual(BoardView.SceneSize / 2f - BoardView.SceneInset, BoardView.NameOffset - BoardView.NameDepth / 2f, "the scene ends before the corner's name");
        }

        [Test]
        public void TheDayRunsFromNoonToDuskOverTheGame()
        {
            Assert.AreEqual(0f, DayCycle.Progress(1, 25, gameOver: false));
            Assert.AreEqual(1f, DayCycle.Progress(25, 25, gameOver: false));
            Assert.AreEqual(1f, DayCycle.Progress(40, 25, gameOver: false), "past the limit it stays dusk");
            Assert.AreEqual(1f, DayCycle.Progress(3, 25, gameOver: true), "the end of the game is evening whatever the round");
            Assert.AreEqual(0f, DayCycle.Progress(1, 0, gameOver: false));
            Assert.AreEqual(1f, DayCycle.Progress(DayCycle.RoundsToEvening, 0, gameOver: false), "with no limit a long game reaches dusk on its own");

            float last = -1f;
            for (int round = 1; round <= 30; round++)
            {
                float progress = DayCycle.Progress(round, 0, gameOver: false);
                Assert.GreaterOrEqual(progress, last, "the day never runs backwards");
                Assert.That(progress, Is.InRange(0f, 1f));
                last = progress;
            }
        }

        [Test]
        public void EveningLightIsWarmerLowerAndDimmer()
        {
            DayLight noon = DayCycle.At(0f);
            DayLight dusk = DayCycle.At(1f);
            Assert.AreEqual(0f, noon.Evening);
            Assert.AreEqual(1f, dusk.Evening, 1e-4f);
            Assert.Less(dusk.SunPitch, noon.SunPitch, "the sun is lower in the evening");
            Assert.Less(dusk.SunIntensity, noon.SunIntensity);
            Assert.Greater(dusk.SunColor.r - dusk.SunColor.b, noon.SunColor.r - noon.SunColor.b, "the evening sun is redder");
            Assert.AreEqual(0f, DayCycle.At(0.3f).Evening, 1e-4f, "the first half of the day is still daylight");
            Assert.Greater(DayCycle.At(0.8f).Evening, 0.5f);
        }

        [Test]
        public void ThePeekNeverPullsTheViewOffTheBoard()
        {
            var center = new Vector3(1f, 0f, 2f);
            Vector3 inside = BoardCameraRig.ClampToBoard(new Vector3(3f, 0f, 1f), center, 6f);
            Assert.AreEqual(new Vector3(3f, 0f, 1f), inside);
            Vector3 clamped = BoardCameraRig.ClampToBoard(new Vector3(40f, 0f, -50f), center, 6f);
            Assert.AreEqual(new Vector3(7f, 0f, -4f), clamped);

            var aim = new Vector3(1f, -0.3f, 3f);
            Assert.AreEqual(aim, BoardCameraRig.PeekAim(aim, null, center, 6f), "nothing pinned, nothing moves");
            Vector3 peeked = BoardCameraRig.PeekAim(aim, new Vector3(7f, 0.2f, 3f), center, 6f);
            Assert.That(peeked.x, Is.EqualTo(aim.x + 6f * BoardCameraRig.PeekAmount).Within(1e-4f), "slides a fraction of the way");
            Assert.AreEqual(aim.y, peeked.y, "the aim keeps its height");
            Vector3 farPeek = BoardCameraRig.PeekAim(aim, new Vector3(100f, 0f, 3f), center, 6f);
            Assert.That(Vector3.Distance(farPeek, peeked), Is.LessThan(1e-4f), "a point off the board counts as its edge");
        }

        [Test]
        public void TheMenuBackdropBreathesAroundTheFramedView()
        {
            Assert.AreEqual(10f, MenuBackdrop.BreathingSize(10f, 0f), 1e-4f);
            Assert.AreEqual(10f * (1f + MenuBackdrop.Breath), MenuBackdrop.BreathingSize(10f, MenuBackdrop.BreathSeconds / 4f), 1e-4f);
            Assert.AreEqual(10f, MenuBackdrop.BreathingSize(10f, MenuBackdrop.BreathSeconds), 1e-3f, "a full breath comes back");
            Assert.Less(MenuBackdrop.Breath, 0.1f, "the breathing stays gentle");
        }

        [Test]
        public void SettledDiceSquareUpToTheCameraAndShadowsShrinkInTheAir()
        {
            Assert.AreEqual(30f, DiceView.RestingYaw(30f, 0));
            Assert.AreEqual(30f + DiceView.FacingJitterDegrees, DiceView.RestingYaw(30f, 90), "the jitter is capped");
            Assert.Greater(DiceView.ShadowScale(0f), DiceView.ShadowScale(1f));
            Assert.Greater(DiceView.ShadowScale(1f), 0f);
        }
    }
}
