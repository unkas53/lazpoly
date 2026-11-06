using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Dice;
using Lazpoly.Presentation.Effects;
using Lazpoly.Presentation.Models;
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
        public void ATownsColourIsPrintedFlatInsideItsFrameOnTopWithTheLightStripBelow()
        {
            BoardData data = BoardDataLoader.LoadFromResources();
            var light = new Color(1f, 0.84f, 0.31f);
            Assert.AreEqual(Color.Lerp(light, BoardView.PaperColor, BoardView.BandPastel), BoardView.BandColor(light), "a light group keeps its strong colour");
            Assert.GreaterOrEqual(BoardView.Contrast(BoardView.BandColor(new Color(0.2f, 0.2f, 0.5f)), BoardView.InkColor), BoardView.BandContrast, "a dark group is washed until the ink reads");

            var holder = new GameObject("Board Test");
            try
            {
                BoardView view = BoardView.Create(holder.transform, data);
                foreach (Transform child in holder.GetComponentsInChildren<Transform>(true))
                {
                    Assert.AreNotEqual("Name band", child.name, "the colour is no raised layer of its own");
                }

                for (int i = 0; i < data.Board.Count; i++)
                {
                    SquareDefinition square = data.Board[i];
                    Transform tile = null;
                    foreach (Transform child in holder.GetComponentsInChildren<Transform>(true))
                    {
                        if (child.name.StartsWith($"Square {i:00} ")) tile = child;
                    }

                    Assert.IsNotNull(tile, $"square {i}");
                    Material material = tile.GetComponent<Renderer>().sharedMaterial;
                    bool town = square.Type == SquareType.Property && !square.IsResort;
                    if (!town)
                    {
                        if (!square.IsResort) Assert.AreSame(TileSkin.Paper(), material.mainTexture, $"{square.Name}: only towns and resorts have their colours printed in");
                        continue;
                    }

                    Assert.AreEqual(Color.white, material.color, $"{square.Name}: the colours are in the texture");
                    var skin = (Texture2D)material.mainTexture;
                    Vector3 size = tile.GetComponent<MeshFilter>().sharedMesh.bounds.size;
                    Vector2 At(Vector3 world)
                    {
                        Vector3 local = tile.InverseTransformPoint(world);
                        return new Vector2(local.x / size.x + 0.5f, local.z / size.z + 0.5f);
                    }

                    // Up the screen the colour, with the name on it; down the screen the strip with the rent and the pawns.
                    Vector2 name = At(view.NameLabel(i).transform.position);
                    Vector2 price = At(view.PriceLabel(i).transform.position);
                    Color underName = skin.GetPixelBilinear(0.5f, name.y);
                    Color underPrice = skin.GetPixelBilinear(0.5f, price.y);
                    Assert.Greater(Mathf.Abs(underName.r - underPrice.r) + Mathf.Abs(underName.g - underPrice.g) + Mathf.Abs(underName.b - underPrice.b), 0.1f,
                        $"{square.Name}: the colour stands apart from the light strip");
                    Assert.That(underPrice.r, Is.GreaterThan(0.75f), $"{square.Name}: the strip is light");
                    Vector3 down = BoardView.ScreenDown(BoardLayout.Outward(view.Layout.SquareCenter(i)));
                    bool lowEnd = Vector3.Dot(tile.forward, down) > 0f;
                    float insideTop = TileSkin.FrameInset + 3f * TileSkin.FrameWidth;
                    Color atTop = skin.GetPixelBilinear(0.5f, lowEnd ? insideTop : 1f - insideTop);
                    Assert.Greater(Mathf.Abs(atTop.r - underPrice.r) + Mathf.Abs(atTop.g - underPrice.g) + Mathf.Abs(atTop.b - underPrice.b), 0.1f,
                        $"{square.Name}: the colour runs up to the frame line at the top");
                    Color edge = skin.GetPixelBilinear(0.5f, lowEnd ? 0.02f : 0.98f);
                    float spread = Mathf.Max(edge.r, Mathf.Max(edge.g, edge.b)) - Mathf.Min(edge.r, Mathf.Min(edge.g, edge.b));
                    Assert.That(spread, Is.LessThan(0.06f), $"{square.Name}: outside the frame the tile is the plain grey paving");
                    Assert.That(edge.g, Is.GreaterThan(0.7f), $"{square.Name}: and light");
                }
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void TownNamesLieOnTheTileWithoutAStripInADarkInk()
        {
            BoardData data = BoardDataLoader.LoadFromResources();
            var holder = new GameObject("Board Test");
            try
            {
                BoardView board = BoardView.Create(holder.transform, data);
                foreach (Transform child in holder.GetComponentsInChildren<Transform>(true))
                {
                    Assert.AreNotEqual("Group strip", child.name, "the raised strip under the names is gone");
                    Assert.AreNotEqual("Owner frame", child.name, "a bought town's edge keeps its own colour");
                }

                for (int i = 0; i < data.Board.Count; i++)
                {
                    if (data.Board[i].Type != SquareType.Property) continue;
                    Color ink = board.NameLabel(i).color;
                    Assert.Less(ink.grayscale, 0.4f, $"square {i}: the name reads dark on its pastel tile");
                    Assert.AreEqual((Color32)board.NameLabel(0).color, (Color32)ink, $"square {i}: every name is in the one ink, whatever its group");
                }
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void TownsArePrintedInCapitalsTheOtherSquaresAsWritten()
        {
            Texts.Lang.Set(Lazpoly.Infra.Settings.Language.Turkish);
            BoardData data = BoardDataLoader.LoadFromResources();
            var holder = new GameObject("Board Test");
            try
            {
                BoardView board = BoardView.Create(holder.transform, data);
                for (int i = 0; i < data.Board.Count; i++)
                {
                    SquareDefinition square = data.Board[i];
                    string expected = square.Type == SquareType.Property ? Texts.Lang.Upper(square.Name) : square.Name;
                    Assert.AreEqual(expected, board.NameLabel(i).text, $"square {i}");
                    if (square.Name == "İstanbul") Assert.AreEqual("İSTANBUL", board.NameLabel(i).text);
                    if (square.Name == "Rio de Janeiro") Assert.AreEqual("RİO DE JANEİRO", board.NameLabel(i).text, "a Turkish capital i keeps its dot");
                }
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void ATownShowsItsPriceUnderItsNameAndItsRentOnceOwned()
        {
            var holder = new GameObject("Board Test");
            try
            {
                BoardData data = BoardDataLoader.LoadFromResources();
                BoardView board = BoardView.Create(holder.transform, data);
                int towns = 0;
                for (int i = 0; i < data.Board.Count; i++)
                {
                    if (data.Board[i].Type != SquareType.Property) continue;
                    towns++;
                    Vector3 center = board.SquarePosition(i);
                    Vector3 down = BoardView.ScreenDown(BoardLayout.Outward(board.Layout.SquareCenter(i)));
                    Vector3 name = board.NameLabel(i).transform.position;
                    Vector3 price = board.PriceLabel(i).transform.position;
                    Assert.Greater(Vector3.Dot(price - name, down), 0f, $"square {i}: the price lies under the name on the screen");
                    Assert.That(Vector3.Dot(board.PawnSpot(i) - price, down), Is.EqualTo(0f).Within(1e-3f), $"square {i}: the pawns stand on the price line");
                    Assert.Greater(Vector3.Dot(price - center, down), 0f, $"square {i}: the price is on the bottom part");
                }

                Assert.Greater(towns, 0);
                const int town = 1;
                Assert.IsNull(BoardView.Subtitle(data.Board[town]));
                Assert.AreEqual(string.Empty, board.PriceLabel(town).text, "for sale, the town shows no figure");
                Assert.IsNull(board.RentShown(town));
                Color32 ink = board.PriceLabel(town).color;

                var owner = new Color(0.2f, 0.5f, 0.9f);
                board.SetOwner(town, owner);
                board.SetRent(town, 7);
                Assert.AreEqual(7, board.RentShown(town));
                Assert.AreEqual(Lazpoly.Presentation.Texts.Lang.F($"{7}"), board.PriceLabel(town).text, "owned, it shows the rent");
                // A TextMesh keeps its colour in bytes, so compare it that way.
                Assert.AreEqual(ink, (Color32)board.PriceLabel(town).color, "in the same ink as the price, not the owner's colour");

                board.SetRent(town, null);
                Assert.AreEqual(string.Empty, board.PriceLabel(town).text, "back on sale, the figure goes again");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void EverythingOnASquareHasItsOwnStripFromTheScreensTopToItsBottom()
        {
            var layout = new BoardLayout(28, 6f, BoardView.DepthRatio);
            float widthInDepths = layout.Width / layout.Depth;
            float pawnReach = PawnSlots.Pitch * widthInDepths; // Half the 3x3 slot grid, in tile depths.
            float buildingHalf = BuildingModels.Depth * BoardView.BuildingScale * widthInDepths / 2f;

            // From the top of the screen down: buildings, the name on its strip, the price with the pawns on it.
            Assert.Less(BoardView.BuildingOffset, BoardView.NameOffset, "the buildings stand above the name");
            Assert.GreaterOrEqual(BoardView.BuildingOffset - buildingHalf, -0.5f, "the building stays on the tile");
            Assert.LessOrEqual(BoardView.BuildingOffset + buildingHalf, BoardView.NameOffset - BoardView.StripDepth / 2f, "the building ends before the name's line");
            Assert.Less(BuildingModels.Width * BoardView.BuildingScale, 0.65f, "the building stays inside its tile's width, with room either side");
            Assert.LessOrEqual(BoardView.NameDepth, BoardView.StripDepth, "the name fits in its line");
            Assert.LessOrEqual(BoardView.NameOffset + BoardView.StripDepth / 2f, BoardView.PriceOffset - BoardView.PriceDepth / 2f, "the price sits under the name's line");
            Assert.LessOrEqual(BoardView.PriceOffset + BoardView.PriceDepth / 2f, 0.5f, "the price stays on the tile");
            Assert.AreEqual(BoardView.PriceOffset, BoardView.PawnOffset, "the pawns walk on the price line");
            Assert.LessOrEqual(BoardView.PawnOffset + pawnReach, 0.5f, "the pawns' bottom row stays on the tile");
            Assert.Greater(BoardView.PriceSize, BoardView.NameSize, "the price is the big line");
            Assert.LessOrEqual(BoardView.NameFill, 0.88f, "a long name keeps a margin at both ends of its tile");
            Assert.Less(BoardView.MarkedNameFill, BoardView.NameFill, "the group's letter keeps its room");

            // On a corner the scene stands towards the board's corner and the pawns step the other way; neither reaches the other.
            Assert.LessOrEqual(BoardView.SceneOffset + BoardView.SceneSize / 2f, 0.5f, "the scene stays on its corner along the edge");
            Assert.LessOrEqual(BoardView.SceneInset + BoardView.SceneSize / 2f, 0.5f, "the scene stays on its corner across it");
            Assert.LessOrEqual(Mathf.Abs(BoardView.StationOffset) + BoardView.StationSize / 2f, 0.5f, "the bus station stays on its corner along the edge");
            Assert.LessOrEqual(BoardView.StationInset + BoardView.StationSize * BoardView.StationDepth / 2f, 0.5f, "the bus station stays on its corner across it");
            Assert.Less(BoardView.StationPawnOffset, BoardView.StationInset - BoardView.StationSize * BoardView.StationDepth / 2f, "pawns at the bus station stand in front of the bus");
            Assert.Less(-BoardView.StationPawnOffset, BoardView.CornerNameOffset - BoardView.CornerNameDepth / 2f, "and short of the corner's name");
            Assert.GreaterOrEqual(BoardView.StationSize / BoardView.SceneSize, 1.7f, "nearly twice the drawn stop, as deep as the corner allows");
            Assert.Less(-BoardView.CornerPawnShift + pawnReach, BoardView.SceneOffset - BoardView.SceneSize / 2f, "the pawns stay clear of the scene");
            Assert.LessOrEqual(BoardView.SceneSize / 2f - BoardView.SceneInset, BoardView.CornerNameOffset - BoardView.CornerNameDepth / 2f, "the scene ends before the corner's name");
            Assert.LessOrEqual(BoardView.CornerPawnOffset + pawnReach, 0.5f, "on a far corner the pawns' outer row stays on the tile");
            Assert.LessOrEqual(BoardView.CornerNameOffset + BoardView.CornerNameDepth / 2f, BoardView.CornerPriceOffset - BoardView.CornerPriceDepth / 2f, "a corner's ticket line lies beyond its name");
            Assert.LessOrEqual(BoardView.CornerPriceOffset + BoardView.CornerPriceDepth / 2f, 0.5f, "and stays on the tile");
            Assert.Less(-BoardView.CornerPawnOffset + pawnReach, BoardView.CornerNameOffset - BoardView.CornerNameDepth / 2f, "on a near corner the pawns stop short of the name");

            // Down the screen is out over the edge on the near rows and in towards the lawn on the far ones.
            Assert.AreEqual(Vector3.back, BoardView.ScreenDown(Vector3.back));
            Assert.AreEqual(Vector3.right, BoardView.ScreenDown(Vector3.right));
            Assert.AreEqual(Vector3.back, BoardView.ScreenDown(Vector3.forward));
            Assert.AreEqual(Vector3.right, BoardView.ScreenDown(Vector3.left));

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
        public void TheLawnIsPlainGrass()
        {
            BoardData data = BoardDataLoader.LoadFromResources();
            var holder = new GameObject("Board Test");
            try
            {
                BoardView.Create(holder.transform, data);
                Transform lawn = null;
                foreach (Transform child in holder.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == "Lawn") lawn = child;
                }

                Assert.IsNotNull(lawn);
                var names = new List<string>();
                foreach (Transform child in lawn) names.Add(child.name);
                CollectionAssert.AreEquivalent(new[] { "Gravel", "Grass" }, names, "no trees, bushes, bunting, paths or patches: the owner wants plain grass");
            }
            finally
            {
                Object.DestroyImmediate(holder);
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
        public void BoardTextIsPrintedInTheHeaviestCut()
        {
            Font black = Lazpoly.Presentation.UI.UiTheme.BlackFont;
            Assert.AreEqual("Nunito-Black", black.name, "the heavy cut is missing from Resources");
            Assert.AreNotSame(Lazpoly.Presentation.UI.UiTheme.BoldFont, black);
            Assert.IsTrue(PrintMaterial.IsPrinting(black), "the print shader is missing from Resources");
            Assert.AreSame(black.material.mainTexture, PrintMaterial.For(black).mainTexture, "the print uses the heavy cut's own atlas");
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
        public void TheBuildingPreviewStagesEachLevelFarUnderTheBoard()
        {
            var holder = new GameObject("Preview Holder");
            var preview = new BuildingPreview(holder.transform);
            try
            {
                Assert.AreEqual(BuildingPreview.StageOrigin, preview.Root.transform.localPosition);
                Assert.Less(BuildingPreview.StageOrigin.y, -BoardCameraRig.Distance * 4f, "the stage lies beyond the main camera's reach");

                Camera camera = preview.Root.GetComponentInChildren<Camera>(true);
                Assert.IsNotNull(camera);
                Assert.IsFalse(camera.enabled, "the picture camera renders only on request");
                Assert.IsTrue(camera.orthographic);
                Assert.That(Quaternion.Angle(camera.transform.localRotation, Quaternion.Euler(BoardCameraRig.Pitch, BoardCameraRig.BaseYaw, 0f)), Is.LessThan(1e-3f),
                    "the pictures share the board's angle");
                float halfDiagonal = Mathf.Sqrt(BuildingPreview.TileWidth * BuildingPreview.TileWidth + BuildingPreview.TileDepth * BuildingPreview.TileDepth) / 2f;
                Assert.Greater(BuildingPreview.ViewSize, halfDiagonal * Mathf.Cos(45f * Mathf.Deg2Rad) * 1.1f, "the tile fits the picture with air around it");

                for (int level = 0; level < BuildingPreview.Levels; level++)
                {
                    preview.Show(level);
                    Assert.AreEqual(level, preview.Shown);
                    var standing = new List<string>();
                    foreach (Transform child in preview.Root.transform)
                    {
                        if (child.gameObject.activeSelf && child.GetComponent<Camera>() == null && child.name != "Tile") standing.Add(child.name);
                    }

                    CollectionAssert.AreEqual(new[] { BuildingModels.Names[level] }, standing, $"level {level}: its one model stands on the stage");
                }

                Assert.Less(BuildingPreview.ModelScale * BuildingModels.Width, BuildingPreview.TileWidth);
                Assert.Less(BuildingPreview.RowDepth + BuildingPreview.ModelScale * BuildingModels.Depth / 2f, BuildingPreview.TileDepth / 2f, "the building stays on the tile");

                float row = BuildingPreview.HouseSize + 3f * BuildingPreview.HouseStep;
                Assert.Less(row, BuildingPreview.TileWidth, "four houses fit across the tile");
                Assert.Less(BuildingPreview.MansionSize.x, BuildingPreview.TileWidth);
                Assert.Less(BuildingPreview.RowDepth + BuildingPreview.MansionSize.z / 2f, BuildingPreview.TileDepth / 2f, "the mansion stays on the tile");

                Assert.IsNull(preview.Picture(1, Color.white), "no pictures are drawn outside play mode");
            }
            finally
            {
                preview.Dispose();
                UnityEngine.Object.DestroyImmediate(holder);
            }
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
