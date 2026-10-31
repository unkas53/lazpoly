using System.Collections.Generic;
using Lazpoly.Presentation.Tabletop;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    public class BoardLayoutTests
    {
        private static void AssertNear(Vector3 expected, Vector3 actual)
        {
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(1e-4f), $"Expected {expected}, got {actual}.");
        }

        [Test]
        public void CornersLandOnTheBoardCorners()
        {
            var layout = new BoardLayout(24, 6f);

            AssertNear(new Vector3(6f, 0f, -6f), layout.SquareCenter(0));
            AssertNear(new Vector3(-6f, 0f, -6f), layout.SquareCenter(6));
            AssertNear(new Vector3(-6f, 0f, 6f), layout.SquareCenter(12));
            AssertNear(new Vector3(6f, 0f, 6f), layout.SquareCenter(18));
        }

        [Test]
        public void SquaresAreEvenlySpacedAndDistinct()
        {
            var layout = new BoardLayout(24, 6f);
            var seen = new HashSet<Vector3>();

            for (int i = 0; i < 24; i++)
            {
                Vector3 here = layout.SquareCenter(i);
                Vector3 next = layout.SquareCenter(i + 1);
                Assert.AreEqual(layout.Spacing, Vector3.Distance(here, next), 1e-4f, $"Gap after square {i}.");
                Assert.IsTrue(seen.Add(here), $"Square {i} overlaps another.");
            }
        }

        [Test]
        public void IndexWrapsAround()
        {
            var layout = new BoardLayout(24, 6f);
            AssertNear(layout.SquareCenter(0), layout.SquareCenter(24));
            AssertNear(layout.SquareCenter(23), layout.SquareCenter(-1));
        }
    }
}
