using Lazpoly.Presentation.Dice;
using Lazpoly.Presentation.Effects;
using Lazpoly.Presentation.Models;
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
            Assert.LessOrEqual(BoardView.PawnOffset + pawnReach, 0.5f, "on a far corner the pawns' outer row stays on the tile");
            Assert.Less(-BoardView.PawnOffset + pawnReach, BoardView.NameOffset - BoardView.NameDepth / 2f, "on a near corner the pawns stop short of the name");

            // The camera sits at the +x, -z corner: the far rows face +z and -x, the near rows -z and +x.
            Assert.IsTrue(BoardView.FacesAway(Vector3.forward));
            Assert.IsTrue(BoardView.FacesAway(Vector3.left));
            Assert.IsFalse(BoardView.FacesAway(Vector3.back));
            Assert.IsFalse(BoardView.FacesAway(Vector3.right));
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
        public void NothingOnTheLawnFallsOverTheSquaresUpTheScreen()
        {
            const float inner = 9.8f;
            float yaw = BoardCameraRig.BaseYaw;
            float lawnHalf = LawnDressing.LawnHalf * inner;
            float pathHalf = LawnDressing.PathHalf * inner;
            Assert.AreEqual(LawnDressing.TreeSpots.Length, LawnDressing.TreeKinds.Length);
            for (int i = 0; i < LawnDressing.TreeSpots.Length; i++)
            {
                Vector3 foot = LawnDressing.Ground(LawnDressing.TreeSpots[i], inner, 0f);
                Vector3 top = LawnDressing.GroundUnderTop(foot, LawnParts.TreeHeight, yaw);
                Assert.Less(Mathf.Max(Mathf.Abs(top.x), Mathf.Abs(top.z)), lawnHalf, $"tree {i}'s crown reaches past the lawn up the screen");
                Assert.Greater(Mathf.Min(Mathf.Abs(foot.x), Mathf.Abs(foot.z)), pathHalf + 0.3f, $"tree {i} stands on the path");
                Assert.Less(Vector3.Dot(foot, BoardView.ScreenUp(yaw)), 0.3f * inner, $"tree {i} stands at the top of the screen, over the title");
            }

            for (int i = 0; i < LawnDressing.BushSpots.Length; i++)
            {
                Vector3 foot = LawnDressing.Ground(LawnDressing.BushSpots[i], inner, 0f);
                Vector3 top = LawnDressing.GroundUnderTop(foot, LawnDressing.BushHeight, yaw);
                Assert.Less(Mathf.Max(Mathf.Abs(top.x), Mathf.Abs(top.z)), lawnHalf, $"bush {i} reaches past the lawn up the screen");
                Assert.Greater(Mathf.Min(Mathf.Abs(foot.x), Mathf.Abs(foot.z)), pathHalf + 0.2f, $"bush {i} sits on the path");
            }

            foreach (Vector2 spot in LawnDressing.PostSpots)
            {
                Vector3 foot = LawnDressing.Ground(spot, inner, 0f);
                Assert.LessOrEqual(Vector3.Dot(foot, BoardView.ScreenUp(yaw)), 1e-3f, "the bunting runs along the near sides of the path, below the score");
                Assert.Greater(Mathf.Min(Mathf.Abs(foot.x), Mathf.Abs(foot.z)), pathHalf, "a post stands just outside the path's corner");
            }

            var root = new GameObject("Lawn");
            try
            {
                LawnDressing.Dress(root.transform, inner, 0.08f);
                int trees = 0, pennants = 0;
                foreach (MeshFilter part in root.GetComponentsInChildren<MeshFilter>())
                {
                    if (part.name == "Trunk") trees++;
                    if (part.sharedMesh == MeshFactory.Pennant()) pennants++;
                }

                Assert.AreEqual(LawnDressing.TreeSpots.Length, trees);
                Assert.AreEqual((LawnDressing.PostSpots.Length - 1) * LawnDressing.PennantsPerString, pennants);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TheJarGathersCoinsAsThePotGrowsAndLosesThemWhenItIsPaidOut()
        {
            var root = new GameObject("Lawn");
            try
            {
                var pile = new PotPile(root.transform);
                Assert.AreEqual(0, pile.Coins);
                Assert.IsNotNull(pile.Root.Find("Jar"), "the jar is always there");
                pile.Show(PotPile.CoinValue * 4);
                Assert.AreEqual(4, pile.Coins);
                pile.Show(PotPile.CoinValue * 4 + 10);
                Assert.AreEqual(5, pile.Coins, "a part coin rounds up");
                pile.Show(0);
                Assert.AreEqual(0, pile.Coins);
                Assert.AreEqual(1, pile.Root.childCount, "only the jar is left");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PrintedTextTestsDepthAndFollowsItsFont()
        {
            Font font = Lazpoly.Presentation.UI.UiTheme.BoldFont;
            Material print = PrintMaterial.For(font);
            Assert.IsNotNull(print);
            Assert.IsTrue(PrintMaterial.IsPrinting(font), "the print shader is missing from Resources");
            Assert.AreEqual(PrintMaterial.ShaderName, print.shader.name);
            Assert.IsTrue(print.shader.isSupported, "the print shader does not compile");
            Assert.AreSame(font.material.mainTexture, print.mainTexture, "the print uses the font's own atlas");
            Assert.AreSame(print, PrintMaterial.For(font), "one material per font");
            Assert.AreNotSame(font.material, print, "the font's draw-through material is left alone");
        }

        [Test]
        public void ASpeechBubbleWrapsItsLineInPaperOverThePawn()
        {
            var root = new GameObject("Scene");
            try
            {
                var pawn = new GameObject("Pawn");
                pawn.transform.position = new Vector3(2f, 0.2f, -3f);
                SpeechBubble bubble = SpeechBubble.Say(root.transform, pawn.transform, "Takas edelim mi?", Color.red);
                Assert.AreEqual("Takas edelim mi?", bubble.Text);
                TextMesh line = bubble.GetComponentInChildren<TextMesh>();
                Vector3 text = line.GetComponent<MeshRenderer>().bounds.size;
                Assert.Greater(bubble.PaperSize.x, text.x + SpeechBubble.Padding, "the paper is wider than the line");
                Assert.Greater(bubble.PaperSize.y, text.y + SpeechBubble.Padding, "the paper is taller than the line");
                MeshFilter paper = bubble.transform.Find("Paper").GetComponent<MeshFilter>();
                Assert.That(paper.sharedMesh.bounds.size.x, Is.EqualTo(bubble.PaperSize.x).Within(0.02f), "the paper is cut to the line");
                Assert.Less(line.transform.localPosition.z, 0f, "the line sits on the camera's side of the paper");
                Assert.That(bubble.transform.position.x, Is.EqualTo(2f).Within(1e-4f));
                Assert.Greater(bubble.transform.position.y, 0.2f + SpeechBubble.Lift, "the bubble floats over the pawn");
                Assert.AreEqual(Vector3.zero, bubble.transform.localScale, "it pops in from nothing");
                SpeechBubble shorter = SpeechBubble.Say(root.transform, pawn.transform, "Pas.", Color.blue);
                Assert.Less(shorter.PaperSize.x, bubble.PaperSize.x, "a short line gets a small bubble");
                UnityEngine.Object.DestroyImmediate(pawn);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
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
