using Lazpoly.Presentation.Dice;
using Lazpoly.Presentation.Models;
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
        public void SpheresAndSignsFaceOutwardAndStandOnTheTable()
        {
            Mesh sphere = MeshFactory.Sphere();
            AssertFacesOutward(sphere, Vector3.zero);
            Assert.That(sphere.bounds.extents.magnitude, Is.EqualTo(Mathf.Sqrt(3f)).Within(0.05f));

            foreach (Lazpoly.Core.Board.SquareType type in new[] { Lazpoly.Core.Board.SquareType.Pot, Lazpoly.Core.Board.SquareType.Travel, Lazpoly.Core.Board.SquareType.Lookout, Lazpoly.Core.Board.SquareType.Card })
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
                Object.DestroyImmediate(root);
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
