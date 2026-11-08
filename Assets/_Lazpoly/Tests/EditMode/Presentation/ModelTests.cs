using Lazpoly.Presentation.Dice;
using Lazpoly.Presentation.Models;
using Lazpoly.Presentation.Tabletop;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    public class ModelTests
    {
        /// <summary>
        /// For a convex mesh, every triangle must face away from the inside point (Unity draws only
        /// the side where the vertices run clockwise), or that face would be invisible.
        /// </summary>
        private static void AssertFacesOutward(Mesh mesh, Vector3 inside)
        {
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Assert.Greater(triangles.Length, 0);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]];
                Vector3 b = vertices[triangles[i + 1]];
                Vector3 c = vertices[triangles[i + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-12f) continue; // Degenerate triangle at a pole.
                Vector3 centroid = (a + b + c) / 3f;
                Assert.Greater(Vector3.Dot(normal, centroid - inside), 0f, $"{mesh.name}: triangle {i / 3} faces inward.");
            }
        }

        [Test]
        public void RoundedBoxHasTheRequestedSizeAndFacesOutward()
        {
            Mesh box = MeshFactory.RoundedBox(new Vector3(2f, 0.5f, 1f), 0.1f);

            Assert.That(Vector3.Distance(new Vector3(2f, 0.5f, 1f), box.bounds.size), Is.LessThan(1e-3f));
            AssertFacesOutward(box, Vector3.zero);
            foreach (Vector3 normal in box.normals) Assert.That(normal.magnitude, Is.EqualTo(1f).Within(1e-3f));

            // A texture lies over the box from above: the top face shows all of it, corner to corner.
            Vector2[] uv = box.uv;
            Vector3[] vertices = box.vertices;
            Assert.AreEqual(vertices.Length, uv.Length);
            for (int i = 0; i < uv.Length; i++)
            {
                Assert.That(uv[i].x, Is.EqualTo(vertices[i].x / 2f + 0.5f).Within(1e-4f));
                Assert.That(uv[i].y, Is.EqualTo(vertices[i].z + 0.5f).Within(1e-4f));
            }
        }

        [Test]
        public void ThePaperSkinIsALightGrainWithAFrameLine()
        {
            Texture2D paper = TileSkin.Make(32);
            try
            {
                Assert.AreEqual(32, paper.width);
                Assert.AreEqual(32, paper.height);
                Assert.Greater(paper.mipmapCount, 1, "far tiles read small mipmaps instead of shimmering");
                Assert.GreaterOrEqual(paper.anisoLevel, 8, "the tiles lie at a slant and stay sharp far off");
                float darkest = 1f;
                float sum = 0f;
                foreach (Color pixel in paper.GetPixels())
                {
                    Assert.That(pixel.r, Is.EqualTo(pixel.g).Within(1e-3f), "the paper is grey; the tile's colour tints it");
                    darkest = Mathf.Min(darkest, pixel.r);
                    sum += pixel.r;
                }

                Assert.Greater(darkest, 0.45f, "nothing on the paper is dark: the frame line is the darkest part");
                Assert.Greater(sum / (32 * 32), 0.85f, "the paper is light");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(paper);
            }

            float middle = TileSkin.Value(0.5f, 0.5f);
            Assert.That(middle, Is.InRange(1f - TileSkin.Grain, 1f), "the middle of the tile lies between the dots");
            float dotCentre = TileSkin.Value(0.5f + 0.5f / TileSkin.DotsAcross, 0.5f);
            Assert.Less(dotCentre, middle, "a dot of the print screen is darker than the paper beside it");
            Assert.Less(TileSkin.Value(TileSkin.FrameInset, 0.5f), middle * (TileSkin.FrameShade + 0.02f), "the frame line is darker than the paper");
            Assert.Less(TileSkin.Value(0.5f, 1f - TileSkin.FrameInset), middle * (TileSkin.FrameShade + 0.02f), "the frame runs round all four sides");
            Assert.AreSame(TileSkin.Paper(), TileSkin.Paper(), "every tile shares one texture");

            // The frame's corners are square, like the squares themselves, which meet side by side.
            float corner = TileSkin.FrameInset;
            Assert.IsTrue(TileSkin.OnFrame(corner, corner), "the frame turns a square corner");
            Assert.IsTrue(TileSkin.OnFrame(1f - corner, corner) && TileSkin.OnFrame(corner, 1f - corner) && TileSkin.OnFrame(1f - corner, 1f - corner), "at all four corners");
        }

        [Test]
        public void ATownsSkinHasItsColourOnOneEndAndTheFrameLineOverIt()
        {
            var band = new Color(0.9f, 0.4f, 0.3f);
            var paving = new Color(0.91f, 0.90f, 0.91f);
            Texture2D skin = TileSkin.Town(band, paving, 0f, 0.6f, 1.4f);
            Assert.AreEqual(TileSkin.TownWidth, skin.width);
            Assert.AreEqual(Mathf.RoundToInt(TileSkin.TownWidth * 1.4f), skin.height, "the pixels stay square on a deep tile");
            Assert.AreSame(skin, TileSkin.Town(band, paving, 0f, 0.6f, 1.4f), "towns of one colour share a texture");

            Color coloured = skin.GetPixelBilinear(0.5f, 0.3f);
            Assert.Greater(coloured.r, coloured.b + 0.1f, "the colour is printed in from v 0 to the band's end");
            Color strip = skin.GetPixelBilinear(0.5f, 0.85f);
            Assert.That(strip.b, Is.GreaterThan(0.75f), "beyond it the light paving");

            // The frame line runs round the colour in the paper's frame tone, as on every square.
            int x = Mathf.RoundToInt(TileSkin.FrameInset * skin.width - 0.5f), y = Mathf.RoundToInt(0.3f * skin.height);
            float u = (x + 0.5f) / skin.width, v = (y + 0.5f) / skin.height;
            Assert.IsTrue(TileSkin.OnFrame(u, v));
            Assert.That(skin.GetPixel(x, y).r, Is.EqualTo(paving.r * TileSkin.Value(u, v)).Within(0.01f), "the frame line keeps the paper's tone");
            Color nearEdge = skin.GetPixelBilinear(2f * TileSkin.FrameWidth + 0.01f, 0.3f);
            Assert.That(nearEdge.b, Is.LessThan(0.75f), "the colour runs out to the frame on the tile's edge (no plain margin since the squares touch)");

            // A line like the frame's runs between the two parts, from one side of the frame to the other.
            // The row just past the band's end: paving, but on the line.
            int row = Mathf.CeilToInt(0.6f * skin.height);
            float du = (skin.width / 2 + 0.5f) / skin.width, dv = (row + 0.5f) / skin.height;
            Assert.IsTrue(TileSkin.OnDivider(du, dv, 0.6f));
            Assert.That(skin.GetPixel(skin.width / 2, row).b, Is.EqualTo(paving.b * TileSkin.Value(du, dv) * TileSkin.FrameShade).Within(0.01f),
                "the dividing line is in the frame's tone");
            Assert.IsFalse(TileSkin.OnDivider(TileSkin.FrameInset, 0.6f, 0.6f), "it stops at the frame line");

            // Round the strip the same frame tone.
            int fx = Mathf.RoundToInt(TileSkin.FrameInset * skin.width - 0.5f), fy = Mathf.RoundToInt(0.8f * skin.height);
            float fu = (fx + 0.5f) / skin.width, fv = (fy + 0.5f) / skin.height;
            Assert.IsTrue(TileSkin.OnFrame(fu, fv));
            Assert.That(skin.GetPixel(fx, fy).b, Is.EqualTo(paving.b * TileSkin.Value(fu, fv)).Within(0.01f),
                "the strip's frame is in the same tone");

            Texture2D flipped = TileSkin.Town(band, paving, 0.4f, 1f, 1.4f);
            Assert.Greater(flipped.GetPixelBilinear(0.5f, 0.7f).r, flipped.GetPixelBilinear(0.5f, 0.7f).b + 0.1f, "the colour can lie at the other end");
        }

        [Test]
        public void CornerScenesStandOnTheirSquareWithTheirBaseOnIt()
        {
            var root = new GameObject("Scenes");
            try
            {
                foreach (Lazpoly.Core.Board.SquareType type in new[] { Lazpoly.Core.Board.SquareType.Pot, Lazpoly.Core.Board.SquareType.Jail, Lazpoly.Core.Board.SquareType.Card, Lazpoly.Core.Board.SquareType.Travel, Lazpoly.Core.Board.SquareType.Cup })
                {
                    Assert.IsTrue(CornerParts.Has(type));
                    GameObject scene = CornerParts.For(type, type.ToString(), root.transform);
                    Assert.IsNotNull(scene, $"{type} has no scene.");
                    MeshFilter[] parts = scene.GetComponentsInChildren<MeshFilter>();
                    if (type == Lazpoly.Core.Board.SquareType.Travel || type == Lazpoly.Core.Board.SquareType.Cup)
                    {
                        Assert.IsTrue(CornerParts.ShowsFront(scene), $"{type}: the owner's textured model");
                        Assert.AreEqual(1, parts.Length);
                        Assert.Greater(parts[0].sharedMesh.triangles.Length / 3, 20000, "in full detail");
                    }
                    else
                    {
                        Assert.Greater(parts.Length, 3, $"{type}'s scene is too plain.");
                        Assert.IsFalse(CornerParts.ShowsFront(scene), $"{type}'s drawn scene faces out over its edge");
                    }

                    var low = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                    var high = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                    foreach (MeshFilter part in parts)
                    {
                        Bounds bounds = part.sharedMesh.bounds;
                        for (int corner = 0; corner < 8; corner++)
                        {
                            var local = new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x, (corner & 2) == 0 ? bounds.min.y : bounds.max.y, (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                            Vector3 point = scene.transform.InverseTransformPoint(part.transform.TransformPoint(local));
                            low = Vector3.Min(low, point);
                            high = Vector3.Max(high, point);
                        }
                    }

                    Assert.That(low.y, Is.EqualTo(0f).Within(0.02f), $"{type}'s scene floats or sinks.");
                    Assert.LessOrEqual(high.y, 1.3f, $"{type}'s scene is too tall.");
                    Assert.That(low.x, Is.GreaterThanOrEqualTo(-0.53f), $"{type}'s scene spills over its square.");
                    Assert.That(high.x, Is.LessThanOrEqualTo(0.53f), $"{type}'s scene spills over its square.");
                    Assert.That(low.z, Is.GreaterThanOrEqualTo(-0.53f), $"{type}'s scene spills over its square.");
                    Assert.That(high.z, Is.LessThanOrEqualTo(0.53f), $"{type}'s scene spills over its square.");
                }

                Assert.Greater(CornerParts.BusStop("Drawn stop", root.transform).GetComponentsInChildren<MeshFilter>().Length, 3, "the drawn stop stands in without the diorama");
                Assert.Greater(CornerParts.Trophy("Drawn cup", root.transform).GetComponentsInChildren<MeshFilter>().Length, 3, "the drawn cup stands in without the owner's model");
                Assert.IsFalse(CornerParts.Has(Lazpoly.Core.Board.SquareType.Lookout));
                Assert.IsFalse(CornerParts.Has(Lazpoly.Core.Board.SquareType.Start), "the start's pier and lighthouse are gone; an arrow is printed instead");
                Assert.IsNull(CornerParts.For(Lazpoly.Core.Board.SquareType.Start, "none", root.transform));
                Assert.IsNull(CornerParts.For(Lazpoly.Core.Board.SquareType.Tax, "none", root.transform));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TheJarStandsOnTheGrass()
        {
            var root = new GameObject("Lawn");
            try
            {
                Bounds jar = Extent(LawnParts.Jar("Jar", root.transform));
                Assert.That(jar.min.y, Is.EqualTo(0f).Within(0.02f));
                Assert.That(jar.max.y, Is.EqualTo(1f).Within(0.02f), "the jar is one unit tall, scaled by the board");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CoinsStackRoundTheJarAndStopAtTheLastStack()
        {
            Assert.AreEqual(0, LawnParts.CoinsFor(0, 50));
            Assert.AreEqual(1, LawnParts.CoinsFor(1, 50), "a little money is still a coin");
            Assert.AreEqual(3, LawnParts.CoinsFor(150, 50));
            Assert.AreEqual(LawnParts.CoinsPerStack * LawnParts.MaxStacks, LawnParts.CoinsFor(100000, 50), "the pile stops growing");
            Vector3 first = LawnParts.CoinSpot(0, 0.2f);
            Vector3 second = LawnParts.CoinSpot(1, 0.2f);
            Assert.AreEqual(0f, first.y);
            Assert.That(second.y, Is.EqualTo(LawnParts.CoinThickness).Within(1e-4f), "the second coin lies on the first");
            Assert.AreEqual(new Vector2(first.x, first.z), new Vector2(second.x, second.z), "the same stack");
            Vector3 next = LawnParts.CoinSpot(LawnParts.CoinsPerStack, 0.2f);
            Assert.AreEqual(0f, next.y, "a new stack starts on the ground");
            Assert.Greater(Vector2.Distance(new Vector2(first.x, first.z), new Vector2(next.x, next.z)), 2f * LawnParts.CoinRadius, "stacks do not overlap");
            Assert.Greater(new Vector2(first.x, first.z).magnitude, 0.2f + LawnParts.CoinRadius, "coins lie outside the jar");
        }

        /// <summary>The box round every mesh of a piece, in the piece's own space.</summary>
        private static Bounds Extent(GameObject piece)
        {
            var low = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var high = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (MeshFilter part in piece.GetComponentsInChildren<MeshFilter>())
            {
                Bounds bounds = part.sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var local = new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x, (corner & 2) == 0 ? bounds.min.y : bounds.max.y, (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                    Vector3 point = piece.transform.InverseTransformPoint(part.transform.TransformPoint(local));
                    low = Vector3.Min(low, point);
                    high = Vector3.Max(high, point);
                }
            }

            var extent = new Bounds();
            extent.SetMinMax(low, high);
            return extent;
        }

        [Test]
        public void SameRoundedBoxIsBuiltOnce()
        {
            Assert.AreSame(MeshFactory.RoundedBox(Vector3.one, 0.2f), MeshFactory.RoundedBox(Vector3.one, 0.2f));
        }

        [Test]
        public void TurnedShapesAndHousesFaceOutward()
        {
            Mesh cylinder = MeshFactory.Lathe("test-cylinder", new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
            });
            AssertFacesOutward(cylinder, new Vector3(0f, 0.5f, 0f));
            AssertFacesOutward(MeshFactory.House(), new Vector3(0f, 0.4f, 0f));
        }

        [Test]
        public void TheStartArrowIsAFlatPrintPointingAhead()
        {
            Mesh arrow = FloorPrint.ArrowMesh();
            Assert.Greater(arrow.triangles.Length, 0);
            Assert.Less(arrow.bounds.size.y, 0.01f, "printed flat, no relief");
            Assert.That(arrow.bounds.min.y, Is.InRange(-0.001f, 0.005f));
            Assert.That(arrow.bounds.max.z, Is.EqualTo(FloorPrint.ArrowSize.y / 2f).Within(0.01f), "the tip leads at +z");
            Assert.That(arrow.bounds.min.z, Is.EqualTo(-FloorPrint.ArrowSize.y / 2f).Within(0.01f));
            Assert.That(arrow.bounds.size.x, Is.EqualTo(FloorPrint.ArrowSize.x).Within(0.01f));
            Assert.That(arrow.bounds.center.x, Is.EqualTo(0f).Within(0.005f), "straight, not lopsided");
        }

        /// <summary>The ink picture's alpha at the page point (x across, z up the page).</summary>
        private static byte AlphaAt(Texture2D picture, Color32[] pixels, float x, float z)
        {
            int column = Mathf.FloorToInt((x / FloorPrint.DocumentSize.x + 0.5f) * picture.width);
            int row = Mathf.FloorToInt((z / FloorPrint.DocumentSize.y + 0.5f) * picture.height);
            return pixels[row * picture.width + column].a;
        }

        [Test]
        public void TheTaxDocumentIsAFlatPrintOfPaperAndInk()
        {
            foreach (Mesh print in new[] { FloorPrint.DocumentPaper(), FloorPrint.DocumentSheet(Color.black, 0.003f), FloorPrint.DocumentInkBars() })
            {
                Assert.Greater(print.triangles.Length, 0);
                Assert.That(print.bounds.min.y, Is.InRange(-0.001f, 0.005f), "it lies on the tile, the ink on the paper");
                Assert.Less(print.bounds.size.y, 0.01f, "printed flat, no relief");
                Assert.LessOrEqual(print.bounds.size.x, FloorPrint.DocumentSize.x + 0.01f);
                Assert.LessOrEqual(print.bounds.size.z, FloorPrint.DocumentSize.y + 0.01f);
            }

            var root = new GameObject("Holder");
            try
            {
                GameObject document = FloorPrint.Document("Doc", root.transform, 0.5f, Color.black);
                foreach (MeshRenderer renderer in document.GetComponentsInChildren<MeshRenderer>())
                {
                    Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, renderer.shadowCastingMode, "a print casts no shadow");
                }

                // Paper and ink are pictures drawn with the print shader, so their edges hold still under TAA.
                MeshRenderer ink = document.transform.Find("Ink").GetComponent<MeshRenderer>();
                MeshRenderer paper = document.transform.Find("Paper").GetComponent<MeshRenderer>();
                Assert.AreEqual(PrintMaterial.ShaderName, ink.sharedMaterial.shader.name, "the ink is printed like the names");
                Assert.AreEqual(PrintMaterial.ShaderName, paper.sharedMaterial.shader.name, "the paper too");
                Assert.AreSame(FloorPrint.DocumentPaperPicture(), paper.sharedMaterial.mainTexture);
                Assert.Greater(ink.sharedMaterial.renderQueue, paper.sharedMaterial.renderQueue, "the ink draws over the paper");
                Texture2D picture = FloorPrint.DocumentInkPicture();
                Assert.AreSame(picture, ink.sharedMaterial.mainTexture);
                Color32[] pixels = picture.GetPixels32();
                int inked = 0, soft = 0;
                foreach (Color32 pixel in pixels)
                {
                    if (pixel.a > 0) inked++;
                    if (pixel.a > 0 && pixel.a < 255) soft++;
                }

                Assert.That(inked, Is.InRange(pixels.Length / 10, pixels.Length * 2 / 3), "outline and writing, not a filled sheet");
                Assert.Greater(soft, 0, "the edges are soft");
                Assert.AreEqual(0, AlphaAt(picture, pixels, 0.25f, 0.01f), "no ink on the sheet right of the writing");
                Assert.AreEqual(255, AlphaAt(picture, pixels, 0f, 0.08f), "a line of writing");
                Assert.AreEqual(255, AlphaAt(picture, pixels, -FloorPrint.DocumentSize.x / 2f + 0.02f, 0f), "the left edge of the outline");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SpheresAndSignsFaceOutwardAndStandOnTheTable()
        {
            Mesh sphere = MeshFactory.Sphere();
            AssertFacesOutward(sphere, Vector3.zero);
            Assert.That(sphere.bounds.extents.magnitude, Is.EqualTo(Mathf.Sqrt(3f)).Within(0.05f));

            foreach (Lazpoly.Core.Board.SquareType type in new[] { Lazpoly.Core.Board.SquareType.Pot, Lazpoly.Core.Board.SquareType.Jail, Lazpoly.Core.Board.SquareType.Travel, Lazpoly.Core.Board.SquareType.Lookout })
            {
                Mesh sign = SquareIcons.For(type);
                Assert.IsNotNull(sign, $"{type} has no sign.");
                Assert.Greater(sign.triangles.Length, 0);
                Assert.That(sign.bounds.min.y, Is.EqualTo(0f).Within(0.02f), $"{type}'s sign floats or sinks.");
                Assert.LessOrEqual(sign.bounds.size.x, 1.05f, $"{type}'s sign is too wide for a square.");
                Assert.LessOrEqual(sign.bounds.size.z, 1.05f, $"{type}'s sign is too deep for a square.");
            }

            Assert.IsNull(SquareIcons.For(Lazpoly.Core.Board.SquareType.Property));
            Assert.IsNull(SquareIcons.For(Lazpoly.Core.Board.SquareType.Start));
            Assert.IsNull(SquareIcons.For(Lazpoly.Core.Board.SquareType.Card), "a Şans square has its question mark printed on the tile, not standing on it");
        }

        [Test]
        public void TheFlagsPennantFliesAwayFromThePole()
        {
            var root = new GameObject("Flag Test");
            try
            {
                GameObject flag = BuildingParts.Flag("Flag", root.transform);
                Transform pennant = flag.transform.Find(BuildingParts.PennantName);
                Assert.IsNotNull(pennant, "The flag has no pennant to paint.");
                Vector3 tip = pennant.TransformPoint(new Vector3(0f, 1f, 0f));   // The prism's apex.
                Vector3 foot = pennant.TransformPoint(new Vector3(0f, 0f, 0.5f)); // One end of its base.
                Assert.Greater(tip.x, 0.4f, "The pennant's tip is not out along +X.");
                Assert.Less(Mathf.Abs(foot.x), 0.1f, "The pennant's base is not on the pole.");
                Assert.Greater(foot.y, 0.6f, "The pennant hangs too low.");
                Assert.Greater(flag.transform.childCount, 2, "Pole, knob and pennant.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EveryDieValueEndsOnTop()
        {
            for (int value = 1; value <= 6; value++)
            {
                Assert.That(Vector3.Dot(DieFaces.Normal(value), DieFaces.Normal(7 - value)), Is.EqualTo(-1f).Within(1e-5f),
                    "Opposite faces add up to 7.");
                Assert.AreEqual(value, DieFaces.Pips(value).Length);
                foreach (float yaw in new[] { -25f, 0f, 17f })
                {
                    Vector3 top = DieFaces.ShowOnTop(value, yaw) * DieFaces.Normal(value);
                    Assert.That(Vector3.Distance(Vector3.up, top), Is.LessThan(1e-4f), $"{value} is not on top at yaw {yaw}.");
                }
            }
        }
    }
}
