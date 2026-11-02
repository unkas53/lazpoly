using System.Collections.Generic;
using Lazpoly.Presentation.Tabletop;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    public class BoardLayoutTests
    {
        private static void AssertNear(Vector3 expected, Vector3 actual, string note = null)
        {
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(1e-4f), $"Expected {expected}, got {actual}. {note}");
        }

        [TestCase(1f)]
        [TestCase(1.4f)]
        public void CornersLandOnTheBoardCorners(float depthRatio)
        {
            var layout = new BoardLayout(24, 6f, depthRatio);

            AssertNear(new Vector3(6f, 0f, -6f), layout.SquareCenter(0));
            AssertNear(new Vector3(-6f, 0f, -6f), layout.SquareCenter(6));
            AssertNear(new Vector3(-6f, 0f, 6f), layout.SquareCenter(12));
            AssertNear(new Vector3(6f, 0f, 6f), layout.SquareCenter(18));
            for (int i = 0; i < 24; i++) Assert.AreEqual(i % 6 == 0, layout.IsCorner(i), $"square {i}");
        }

        [Test]
        public void EqualSquaresAreEvenlySpacedAndDistinct()
        {
            var layout = new BoardLayout(24, 6f);
            var seen = new HashSet<Vector3>();

            Assert.AreEqual(layout.Width, layout.Depth, "with no extra depth every square is the same size");
            for (int i = 0; i < 24; i++)
            {
                Vector3 here = layout.SquareCenter(i);
                Vector3 next = layout.SquareCenter(i + 1);
                Assert.AreEqual(layout.Width, Vector3.Distance(here, next), 1e-4f, $"Gap after square {i}.");
                Assert.IsTrue(seen.Add(here), $"Square {i} overlaps another.");
            }
        }

        [Test]
        public void EdgeSquaresAreDeeperThanWideBetweenSquareCorners()
        {
            var layout = new BoardLayout(24, 6f, 1.4f);

            Assert.AreEqual(5, layout.PerSide);
            Assert.AreEqual(1.4f * layout.Width, layout.Depth, 1e-4f);
            Assert.AreEqual(12f, layout.Depth + 5 * layout.Width, 1e-4f, "a side is half a corner, five squares and half a corner");
            Assert.AreEqual(6f + layout.Depth / 2f, layout.OuterHalf, 1e-4f);
            Assert.AreEqual(new Vector2(layout.Depth, layout.Depth), layout.SquareSize(0), "a corner is a square of the edge squares' depth");
            Assert.AreEqual(new Vector2(layout.Width, layout.Depth), layout.SquareSize(1));

            // Along the near side (z = -6) from the right corner: the corner, then the squares edge to edge.
            Assert.AreEqual((layout.Depth + layout.Width) / 2f, Vector3.Distance(layout.SquareCenter(0), layout.SquareCenter(1)), 1e-4f);
            for (int i = 1; i < 5; i++)
            {
                Assert.AreEqual(layout.Width, Vector3.Distance(layout.SquareCenter(i), layout.SquareCenter(i + 1)), 1e-4f, $"gap after square {i}");
                Assert.AreEqual(-6f, layout.SquareCenter(i).z, 1e-4f);
            }

            Assert.AreEqual((layout.Depth + layout.Width) / 2f, Vector3.Distance(layout.SquareCenter(5), layout.SquareCenter(6)), 1e-4f);
            // The squares fill the side exactly: the last one ends where the next corner begins.
            Assert.AreEqual(-6f + layout.Depth / 2f, layout.SquareCenter(5).x - layout.Width / 2f, 1e-4f);

            AssertNear(Vector3.back, layout.Outward(0));
            AssertNear(Vector3.back, layout.Outward(3));
            AssertNear(Vector3.left, layout.Outward(9));
            AssertNear(Vector3.forward, layout.Outward(15));
            AssertNear(Vector3.right, layout.Outward(21));
            AssertNear(Vector3.right, layout.Cornerward(0), "the near-right corner's scene stands towards +x");
            AssertNear(Vector3.left, layout.Cornerward(6));
            AssertNear(Vector3.left, layout.Cornerward(12));
            AssertNear(Vector3.right, layout.Cornerward(18));
            AssertNear(Vector3.zero, layout.Cornerward(1));
        }

        [Test]
        public void ACountThatDoesNotShareOutGetsEqualSquaresRoundTheEdge()
        {
            var layout = new BoardLayout(10, 5f, 1.4f);
            Assert.IsFalse(layout.HasCorners);
            Assert.AreEqual(layout.Width, layout.Depth);
            Assert.AreEqual(4f, layout.Width, 1e-4f, "forty units of edge over ten squares");
            for (int i = 0; i < 10; i++)
            {
                Assert.IsFalse(layout.IsCorner(i));
                Vector3 here = layout.SquareCenter(i);
                Vector3 next = layout.SquareCenter(i + 1);
                Assert.IsTrue(Mathf.Abs(Mathf.Abs(here.x) - 5f) < 1e-4f || Mathf.Abs(Mathf.Abs(here.z) - 5f) < 1e-4f, $"square {i} is off the edge");
                // Four units along the edge, even where the walk turns the board's corner.
                Assert.AreEqual(4f, Mathf.Abs(next.x - here.x) + Mathf.Abs(next.z - here.z), 1e-4f, $"gap after square {i}");
            }
        }

        [Test]
        public void IndexWrapsAround()
        {
            var layout = new BoardLayout(24, 6f, 1.4f);
            AssertNear(layout.SquareCenter(0), layout.SquareCenter(24));
            AssertNear(layout.SquareCenter(23), layout.SquareCenter(-1));
            Assert.IsTrue(layout.IsCorner(-6));
        }
    }
}
