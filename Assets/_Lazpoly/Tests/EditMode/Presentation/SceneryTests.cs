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
        public void LongNamesShrinkToTheirTile()
        {
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
        public void TheTownColoursAreCloseTonesOfOneWarmFamilyAndEachGroupStillStandsApart()
        {
            // The owner's wish (2026-11-10): the squares' colours pleasing and in tones close to each other, not every
            // colour of the rainbow. One warm family: from wheat through apricot and rose to mauve, each group a step
            // deeper and a step further round than the one before (the dearer the town, the deeper its colour), each
            // town's printed band still far enough from every other to tell the groups apart.
            BoardData data = BoardDataLoader.LoadFromResources();
            var bands = new List<Color>();
            float lastWarmth = 1f, lastLight = float.MaxValue;
            for (int group = 1; group <= 8; group++)
            {
                Color colour = data.GroupColor(group);
                Color.RGBToHSV(colour, out float hue, out float saturation, out float value);
                // Hues counted from the cool side of mauve, so the warm family is one unbroken stretch: mauve low, wheat high.
                float warmth = Mathf.Repeat(hue + 0.25f, 1f);
                Assert.That(warmth, Is.InRange(0.08f, 0.42f), $"group {group} is one of the warm family (no green, no blue)");
                Assert.Less(warmth, lastWarmth, $"group {group} lies a step further round than the one before");
                Assert.That(saturation, Is.InRange(0.28f, 0.55f), $"group {group} is neither washed out nor loud");
                Assert.That(value, Is.InRange(0.58f, 0.9f), $"group {group} is not so bright that the table's light burns it out");
                Color band = BoardView.BandColor(colour);
                Assert.AreEqual(Color.Lerp(colour, BoardView.PaperColor, BoardView.BandPastel), band, $"group {group} is light enough for the ink as it is");
                Assert.GreaterOrEqual(BoardView.Contrast(band, BoardView.InkColor), BoardView.BandContrast, $"group {group}'s names read");
                Assert.LessOrEqual(Mathf.Max(band.r, Mathf.Max(band.g, band.b)), 0.9f, $"group {group} keeps its colour under the table's light");
                float light = BoardView.Contrast(band, Color.black);
                Assert.Less(light, lastLight, $"group {group} is deeper than the one before");
                lastWarmth = warmth;
                lastLight = light;
                bands.Add(band);
            }

            for (int a = 0; a < bands.Count; a++)
            {
                for (int b = a + 1; b < bands.Count; b++)
                {
                    float distance = new Vector3(bands[a].r - bands[b].r, bands[a].g - bands[b].g, bands[a].b - bands[b].b).magnitude;
                    Assert.Greater(distance, 0.045f, $"groups {a + 1} and {b + 1} look alike");
                    Assert.Less(distance, 0.4f, $"groups {a + 1} and {b + 1} are tones of one family");
                }
            }
        }

        [Test]
        public void TheOtherSquaresAreWarmCreamsAndTheResortsSeaIsTheOneSoftCoolTone()
        {
            // The squares that are no town: one family of light, warm, quiet tones on the paper, none burned out by the light.
            Color[] others = { BoardView.StartColor, BoardView.TaxColor, BoardView.CardColor, BoardView.CupColor, BoardView.TravelColor, BoardView.JailColor };
            var tiles = new List<Color>();
            foreach (Color other in others)
            {
                Color tile = Color.Lerp(other, BoardView.PaperColor, BoardView.Pastel);
                Color.RGBToHSV(tile, out _, out float saturation, out float value);
                Assert.LessOrEqual(saturation, 0.4f, $"{other} is a quiet tone");
                Assert.That(value, Is.InRange(0.78f, 0.88f), $"{other} is light, and keeps its tone under the table's light");
                Assert.GreaterOrEqual(tile.r, tile.b, $"{other} is not a cool tone");
                Assert.GreaterOrEqual(BoardView.Contrast(tile, BoardView.InkColor), 7f, $"the ink reads on {other}");
                tiles.Add(tile);
            }

            for (int a = 0; a < tiles.Count; a++)
            {
                for (int b = a + 1; b < tiles.Count; b++)
                {
                    float distance = new Vector3(tiles[a].r - tiles[b].r, tiles[a].g - tiles[b].g, tiles[a].b - tiles[b].b).magnitude;
                    Assert.Less(distance, 0.3f, $"squares {a} and {b} are tones of one family");
                }
            }

            Color sand = Color.Lerp(BoardView.SandColor, BoardView.PaperColor, BoardView.SandPastel);
            Assert.That(Mathf.Max(sand.r, Mathf.Max(sand.g, sand.b)), Is.InRange(0.8f, 0.88f), "the sand is light but not burned out");
            Assert.Greater(sand.r, sand.b, "and warm");
            Color.RGBToHSV(BoardView.SeaColor, out float seaHue, out float seaSaturation, out _);
            Assert.That(seaHue, Is.InRange(0.45f, 0.6f), "the sea is still a sea");
            Assert.LessOrEqual(seaSaturation, 0.35f, "but a soft one");
            Assert.Greater(new Vector3(sand.r - BoardView.SeaColor.r, sand.g - BoardView.SeaColor.g, sand.b - BoardView.SeaColor.b).magnitude, 0.3f, "that shows on the sand");
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
                    Color edge = skin.GetPixelBilinear(0.5f, lowEnd ? 0.03f : 0.97f);
                    Assert.Greater(Mathf.Abs(edge.r - underPrice.r) + Mathf.Abs(edge.g - underPrice.g) + Mathf.Abs(edge.b - underPrice.b), 0.1f,
                        $"{square.Name}: the colour runs out to the frame on the tile's edge (no plain margin since the squares touch)");
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
        public void TheStartCornerHasAnArrowPointingTheWayThePawnsGo()
        {
            BoardData data = BoardDataLoader.LoadFromResources();
            var holder = new GameObject("Board Test");
            try
            {
                BoardView board = BoardView.Create(holder.transform, data);
                Assert.IsNotNull(board.StartArrow, "an arrow is printed on the start (the owner's wish, Faz 24)");
                Assert.IsNull(board.transform.Find("Scene Start"), "the pier and lighthouse are gone");
                Vector3 way = board.Layout.SquareCenter(1) - board.Layout.SquareCenter(0);
                way.y = 0f;
                Vector3 forward = board.StartArrow.localRotation * Vector3.forward;
                Assert.Greater(Vector3.Dot(forward, way.normalized), 0.99f, "it points at the next square");
                Assert.Less(Mathf.Abs(forward.y), 0.01f, "it lies flat");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void TownsCornersTaxAndChanceArePrintedInCapitalsTheResortsAsWritten()
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
                    bool chance = square.Type == SquareType.Card && !board.Layout.IsCorner(i);
                    bool capitals = square.Type == SquareType.Property || square.Type == SquareType.Start || square.Type == SquareType.Travel || square.Type == SquareType.Jail || square.Type == SquareType.Tax || square.Type == SquareType.Cup || chance;
                    string expected = square.Type == SquareType.Cup ? BoardView.TwoLines(Texts.Lang.Upper(square.Name))
                        : capitals ? Texts.Lang.Upper(square.Name) : square.Name;
                    Assert.AreEqual(expected, board.NameLabel(i).text, $"square {i}");
                    if (square.Name == "İstanbul") Assert.AreEqual("İSTANBUL", board.NameLabel(i).text);
                    if (square.Type == SquareType.Jail) Assert.AreEqual("HAPİS", board.NameLabel(i).text, "the jail too, larger (the owner's wish)");
                    if (square.Name == "Rio de Janeiro") Assert.AreEqual("RİO DE JANEİRO", board.NameLabel(i).text, "a Turkish capital i keeps its dot");
                    if (square.Type == SquareType.Tax)
                    {
                        Assert.AreEqual("VERGİ", board.NameLabel(i).text, "the tax in capitals, larger (the owner's wish, Faz 24)");
                        Assert.IsNull(BoardView.Subtitle(square), "no %10 printed on the tax");
                        Assert.IsNotNull(board.Document(i), "a document is printed on the tax square");
                    }
                    else
                    {
                        Assert.IsNull(board.Document(i), $"square {i} has no document");
                    }

                    if (chance)
                    {
                        Assert.AreEqual("ŞANS", board.NameLabel(i).text, "Şans in capitals, larger (the owner's wish, Faz 24)");
                        Assert.IsNotNull(board.ChanceMark(i), "a question mark is printed on the Şans tile");
                        Assert.AreEqual("?", board.ChanceMark(i).text);
                        Assert.Greater(board.ChanceMark(i).characterSize, board.NameLabel(i).characterSize * 2f, "the question mark is the tile's big print");
                        Bounds mark = board.ChanceMark(i).GetComponent<MeshRenderer>().bounds;
                        Bounds name = board.NameLabel(i).GetComponent<MeshRenderer>().bounds;
                        Assert.IsFalse(mark.Intersects(name), $"square {i}: the question mark runs into ŞANS");
                        Assert.IsNull(board.transform.Find("Sign Card"), "no standing sign on a Şans square");
                    }
                    else
                    {
                        Assert.IsNull(board.ChanceMark(i), $"square {i} has no question mark");
                    }
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
        public void TheLawnIsPlainGrass()
        {
            BoardData data = BoardDataLoader.LoadFromResources();
            var holder = new GameObject("Board Test");
            try
            {
                BoardView board = BoardView.Create(holder.transform, data);
                Transform lawn = null;
                foreach (Transform child in holder.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == "Lawn") lawn = child;
                }

                Assert.IsNotNull(lawn);
                var names = new List<string>();
                foreach (Transform child in lawn) names.Add(child.name);
                CollectionAssert.AreEquivalent(new[] { "Road", "Grass" }, names, "no trees, bushes, bunting or paths: nothing stands on the grass");

                // Round the lawn an asphalt lane with a dashed middle line (Faz 25) on sandy ground, its edges mixed with
                // sand and its line bending round the corners with it (the owner's wish, 2026-11-10).
                var road = (Texture2D)lawn.Find("Road").GetComponent<MeshRenderer>().sharedMaterial.mainTexture;
                Assert.IsNotNull(road, "the road has its texture");
                float strip = (1f - BoardView.LawnShare) / 2f, reach = RoadSkin.LineReach(strip), bend = RoadSkin.LineBend(strip);
                float lineHalf = strip * RoadSkin.LineShare;
                float quarter = RoadSkin.QuarterLength(strip), dash = quarter / Mathf.Round(quarter / RoadSkin.DashStep);
                Assert.IsTrue(RoadSkin.OnLine(new Vector2(reach, 0f), strip), "a dash on the middle of a side");
                Assert.IsTrue(RoadSkin.OnLine(new Vector2(0f, -reach), strip), "and of every side");
                Assert.IsFalse(RoadSkin.OnLine(new Vector2(reach, dash / 2f), strip), "a gap between dashes");
                Assert.IsTrue(RoadSkin.OnLine(new Vector2(reach, dash), strip), "the next dash");
                Assert.IsFalse(RoadSkin.OnLine(new Vector2(reach + 2f * lineHalf, 0f), strip), "the line is narrow");
                Assert.IsFalse(RoadSkin.OnLine(Vector2.zero, strip), "no line under the lawn");
                Assert.IsFalse(RoadSkin.OnLine(new Vector2(reach, reach), strip), "the line does not run square into the corner");
                RoadSkin.Lane(new Vector2(reach - bend, reach - bend) + new Vector2(1f, 1f).normalized * bend, strip, out float across, out float along);
                Assert.AreEqual(0f, across, 1e-4f, "it bends round the corner instead");
                Assert.AreEqual(quarter / 2f, along, 1e-4f, "a quarter of the way round from side to side, halfway at the diagonal");
                Assert.AreEqual(reach - bend, (1f - 2f * strip) * (1f - LawnShape.Corner), 1e-5f, "the bend turns about the lawn's own corner");
                Assert.Less(RoadSkin.Asphalt.grayscale, 0.35f, "the lane is dark asphalt");
                Color.RGBToHSV(RoadSkin.Sand, out _, out float sandSaturation, out _);
                Assert.Greater(RoadSkin.Sand.r, RoadSkin.Sand.b + 0.15f, "the sand is warm");
                Assert.Less(sandSaturation, 0.45f, "and matt");
                Assert.Greater(RoadSkin.Sand.grayscale, RoadSkin.Asphalt.grayscale + 0.2f, "it stands out from the asphalt");

                // Asphalt along the line; sand by the squares, in the corners and wherever the grass is near.
                for (int i = 0; i < 720; i++)
                {
                    float angle = i * Mathf.PI / 360f;
                    var outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    float edge = (1f - 2f * strip) * LawnShape.Radius(angle);
                    foreach (float rough in new[] { 0f, 0.5f, 1f })
                    {
                        Assert.AreEqual(0f, RoadSkin.Cover(outward * (edge + 0.001f), strip, rough), 1e-4f, $"sand where the grass ends ({i / 2f} degrees)");
                        Assert.AreEqual(0f, RoadSkin.Cover(outward * (edge + 2f * strip * RoadSkin.FringeMin * 0.5f), strip, rough), 1e-4f, "a sandy fringe before the asphalt");
                    }
                }

                foreach (float rough in new[] { 0f, 0.5f, 1f })
                {
                    Assert.AreEqual(1f, RoadSkin.Cover(new Vector2(reach, 0.3f), strip, rough), 1e-4f, "asphalt under the line");
                    Assert.AreEqual(1f, RoadSkin.Cover(new Vector2(-0.4f, reach + lineHalf), strip, rough), 1e-4f, "and either side of it");
                    Assert.AreEqual(1f, RoadSkin.Cover(new Vector2(-0.4f, reach - lineHalf), strip, rough), 1e-4f);
                    Assert.AreEqual(0f, RoadSkin.Cover(new Vector2(0.999f, 0.2f), strip, rough), 1e-4f, "sand along the squares");
                    Assert.AreEqual(0f, RoadSkin.Cover(new Vector2(0.99f, -0.99f), strip, rough), 1e-4f, "sand in the corner");
                }

                // Every painted dash lies wholly on asphalt, whatever the noise: the line is set right for the sandy edges.
                for (int i = 0; i < 2000; i++)
                {
                    float angle = i * Mathf.PI / 1000f;
                    var outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    float far = 1f / Mathf.Max(Mathf.Abs(outward.x), Mathf.Abs(outward.y));
                    for (float away = far * 0.8f; away < far; away += 0.002f)
                    {
                        Vector2 point = outward * away;
                        if (!RoadSkin.OnLine(point, strip)) continue;
                        foreach (float rough in new[] { 0f, 0.5f, 1f }) Assert.AreEqual(1f, RoadSkin.Cover(point, strip, rough), 1e-4f, $"a dash on sand at {point}");
                    }
                }

                // The grass itself is a natural, matt meadow with a wandering edge (the owner's wish, 2026-11-10), no objects added.
                Transform grass = lawn.Find("Grass");
                Assert.AreSame(LawnSkin.Texture(), grass.GetComponent<MeshRenderer>().sharedMaterial.mainTexture);
                Color.RGBToHSV(LawnSkin.Light, out _, out float saturation, out float value);
                Assert.Greater(LawnSkin.Light.g, LawnSkin.Light.r + 0.1f, "still a green");
                Assert.Less(saturation, 0.45f, "a muted green, not a bright one");
                Assert.Less(value, 0.65f, "and not a pale one");
                Assert.Less(LawnSkin.Deep.grayscale, LawnSkin.Light.grayscale, "two tones, a deeper and a lighter");

                // Its edge wanders in and out, never past its allowance: off the hatches inside, short of the road's line outside.
                float least = float.MaxValue, most = float.MinValue;
                for (int i = 0; i < 3600; i++)
                {
                    float wander = LawnShape.Wander(i * Mathf.PI / 1800f);
                    least = Mathf.Min(least, wander);
                    most = Mathf.Max(most, wander);
                }

                Assert.GreaterOrEqual(least, -(LawnShape.Inward + LawnShape.Fine) - 1e-4f);
                Assert.LessOrEqual(most, LawnShape.Outward + LawnShape.Fine + 1e-4f);
                Assert.Greater(most - least, 0.06f, "the edge is not a straight line");
                Assert.Less(least, -0.02f, "it comes in");
                Assert.Greater(most, 0.015f, "and goes out");
                Assert.AreEqual(1f, LawnShape.BaseRadius(0f), 1e-5f, "a straight edge's middle is one half-lawn away");
                Assert.Less(LawnShape.BaseRadius(Mathf.PI / 4f), Mathf.Sqrt(2f) - 0.05f, "the corners are well rounded");
                float inner = board.Inner, lawnHalf = inner * BoardView.LawnShare / 2f;
                Assert.Less(lawnHalf * (1f + LawnShape.Outward + LawnShape.Fine), inner / 2f * (reach - lineHalf) - 0.08f, "the grass stays clear of the road's painted line");
                Assert.AreEqual(BoardView.LawnHeight, grass.GetComponent<MeshRenderer>().bounds.max.y, 1e-4f, "the grass's top is the lawn's height, where the hatches, the clock and the pot stand");
                Assert.AreEqual(0f, grass.GetComponent<MeshRenderer>().bounds.min.y, 1e-4f, "it stands on the board's floor");

                // The mesh: a flat top at the lawn's height, a bank and a skirt down to the ground, every face turned outwards.
                Mesh mesh = grass.GetComponent<MeshFilter>().sharedMesh;
                Assert.AreSame(LawnShape.Build(lawnHalf, BoardView.LawnHeight), mesh, "made once and shared");
                Assert.AreEqual(BoardView.LawnHeight, mesh.bounds.max.y, 1e-5f);
                Assert.AreEqual(0f, mesh.bounds.min.y, 1e-5f);
                Assert.LessOrEqual(Mathf.Max(mesh.bounds.max.x, -mesh.bounds.min.x), lawnHalf * (1f + LawnShape.Outward + LawnShape.Fine) + 1e-3f);
                Vector3[] vertices = mesh.vertices, normals = mesh.normals;
                Vector2[] uvs = mesh.uv;
                int[] triangles = mesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                    Vector3 facing = Vector3.Cross(b - a, c - a);
                    Vector3 lit = normals[triangles[i]] + normals[triangles[i + 1]] + normals[triangles[i + 2]];
                    Assert.Greater(Vector3.Dot(facing, lit), 0f, $"triangle {i / 3} faces the way it is lit");
                }

                foreach (Vector2 uv in uvs)
                    Assert.IsTrue(uv.x > 0f && uv.x < 1f && uv.y > 0f && uv.y < 1f, "the texture covers the whole lawn");

                // The paint: an earthy rim where the grass ends, plain grass a little way in; daisies in loose patches, never in the calm middle.
                Assert.AreEqual(1f, LawnSkin.Rim(0f, 0.5f), 1e-4f, "earth at the very edge");
                Assert.AreEqual(0f, LawnSkin.Rim(LawnSkin.RimWidth, 1f), 1e-4f, "none past the rim");
                Assert.Greater(LawnSkin.Rim(0.01f, 1f), LawnSkin.Rim(0.01f, 0f), "the rim's inner side is ragged");
                Assert.AreEqual(0f, LawnSkin.Shade(LawnSkin.ShadeWidth), 1e-5f);
                Assert.That(LawnSkin.Flowers.Count, Is.InRange(50, 130), "a sprinkle, not a carpet");
                foreach (Vector3 flower in LawnSkin.Flowers)
                {
                    var at = new Vector2(flower.x, flower.y);
                    Assert.Greater(at.magnitude, 0.3f, "the middle stays calm for the dice total and the pot");
                    Assert.IsTrue(LawnShape.Covers(at, LawnSkin.RimWidth), "a daisy stands on grass, not on the rim");
                }
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
        public void ASpeechBubbleDropsBelowTheTurnBanner()
        {
            var holder = new GameObject("Camera");
            var target = new RenderTexture(800, 600, 16);
            try
            {
                Camera camera = holder.AddComponent<Camera>();
                camera.targetTexture = target;
                camera.fieldOfView = 40f;
                holder.transform.position = new Vector3(0f, 0f, -10f);
                Vector3 high = camera.ScreenToWorldPoint(new Vector3(400f, 590f, 10f));
                Assert.AreEqual(Vector3.zero, SpeechBubble.DropBelow(camera, high, 0.1f, 0f), "nothing kept clear, nothing moves");
                Vector3 drop = SpeechBubble.DropBelow(camera, high, 0.1f, 100f);
                Assert.Less(drop.y, 0f, "a bubble under the band drops");
                Vector3 top = camera.WorldToScreenPoint(high + drop + Vector3.up * 0.1f);
                Assert.That(top.y, Is.EqualTo(camera.pixelHeight - 100f).Within(0.5f), "its top meets the band's lower edge");
                Vector3 low = camera.ScreenToWorldPoint(new Vector3(400f, 200f, 10f));
                Assert.AreEqual(Vector3.zero, SpeechBubble.DropBelow(camera, low, 0.1f, 100f), "a low bubble stays put");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
                UnityEngine.Object.DestroyImmediate(target);
            }
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

            // Held on a point (the screenshot presets): right on it, at the aim's own height, never off the board.
            Assert.AreEqual(new Vector3(5f, aim.y, -1f), BoardCameraRig.HeldAim(aim, new Vector3(5f, 0.9f, -1f), center, 6f));
            Assert.AreEqual(new Vector3(7f, aim.y, -4f), BoardCameraRig.HeldAim(aim, new Vector3(40f, 0f, -50f), center, 6f));
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

        [Test]
        public void TheStartAndTheBusStationAreInCapitalsAndTheFareIsNotPrinted()
        {
            // The owner's wish (Faz 28).
            Lazpoly.Presentation.Texts.Lang.Set(Lazpoly.Infra.Settings.Language.Turkish);
            Assert.AreEqual("BAŞLANGIÇ", BoardView.PrintedName(SquareDefinition.Start("Başlangıç"), corner: true));
            Assert.AreEqual("OTOGAR", BoardView.PrintedName(SquareDefinition.Travel("Otogar", 50000), corner: true));
            Assert.IsNull(BoardView.Subtitle(SquareDefinition.Travel("Otogar", 50000)), "No \"bilet 50K\" under the bus station.");
        }

        [Test]
        public void TheJailCellStandsOnTheLeftAndHapisLowerRight()
        {
            // The owner's wish (Faz 28): the cell towards the tile's left corner, the lettering towards its lower right.
            BoardData data = BoardDataLoader.LoadFromResources();
            var holder = new GameObject("Board Test");
            try
            {
                BoardView board = BoardView.Create(holder.transform, data);
                int jail = data.Board.JailIndex;
                Vector3 center = board.Layout.SquareCenter(jail);
                Vector3 right = BoardView.ScreenRight(BoardCameraRig.BaseYaw), up = BoardView.ScreenUp(BoardCameraRig.BaseYaw);

                Transform cell = board.transform.Find("Scene Jail");
                Assert.IsNotNull(cell);
                Vector3 toCell = Vector3.ProjectOnPlane(cell.localPosition - center, Vector3.up);
                Assert.Greater(Vector3.Dot(toCell.normalized, -right), 0.9f, "the cell stands towards the left corner");

                Vector3 toName = Vector3.ProjectOnPlane(board.NameLabel(jail).transform.localPosition - center, Vector3.up);
                Assert.Greater(Vector3.Dot(toName, right), 0f, "HAPİS lies to the right");
                Assert.Less(Vector3.Dot(toName, up), 0f, "and low on the tile");
                Vector3 line = board.NameLabel(jail).transform.localRotation * Vector3.right;
                Assert.Greater(Vector3.Dot(line, right), 0.6f, "HAPİS reads from lower left (turned a quarter to the left, the owner's wish)");
                Assert.Greater(Vector3.Dot(line, up), 0.6f, "to upper right");
                Assert.Greater(Vector3.Dot(board.NameLabel(jail).transform.localRotation * Vector3.forward, Vector3.down), 0.99f, "still lying flat");
                board.FaceCamera(BoardCameraRig.BaseYaw + 90f);
                Vector3 turned = board.NameLabel(jail).transform.localRotation * Vector3.right;
                Assert.Greater(Vector3.Dot(turned, BoardView.ScreenUp(BoardCameraRig.BaseYaw + 90f)), 0.6f, "never upside down with the table turned");
                board.FaceCamera(BoardCameraRig.BaseYaw);

                Vector3 toPawns = Vector3.ProjectOnPlane(board.transform.InverseTransformPoint(board.PawnSpot(jail)) - center, Vector3.up);
                Assert.Greater(Vector3.Dot(toPawns, up), 0f, "the pawns wait towards the top corner, clear of both");
                Assert.Greater(Vector3.Distance(toPawns, toName), board.Layout.SquareSize(jail).y * 0.3f);
                Assert.Greater(Vector3.Distance(toPawns, toCell), board.Layout.SquareSize(jail).y * 0.3f);
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }
    }
}
