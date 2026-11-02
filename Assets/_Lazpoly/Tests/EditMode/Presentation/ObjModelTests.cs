using System;
using System.IO;
using Lazpoly.Presentation.Models;
using Lazpoly.Presentation.Tabletop;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    public class ObjModelTests
    {
        private static void AssertColor(Color expected, Color actual) =>
            Assert.That(Vector4.Distance(expected, actual), Is.LessThan(1e-4f), $"expected {expected}, got {actual}");

        private const string Sample =
            "# a square floor and a post\n" +
            "o Floor\nusemtl Boards\n" +
            "v 0 0 0\nv 1 0 0\nv 1 0 -1\nv 0 0 -1\n" +
            "vn 0 1 0\n" +
            "f 1//1 2//1 3//1 4//1\n" +
            "o Post\nusemtl Steel\n" +
            "v 0 0 0\nv 0.1 0 0\nv 0 1 0\n" +
            "f -3 -2 -1\n";

        [Test]
        public void GroupsFacesAndMirrorsTheRightHandedFile()
        {
            ObjModel model = ObjModel.Parse(Sample);
            Assert.AreEqual(2, model.Groups.Count);
            Assert.AreEqual(3, model.TriangleCount, "a quad is two triangles, the post one");

            ObjGroup floor = model.Find("Boards");
            Assert.IsNotNull(floor);
            Assert.AreEqual("Floor", floor.Name);
            Assert.AreEqual(4, floor.Vertices.Length, "corners sharing position and normal share a vertex");
            Assert.That(floor.Bounds.min.x, Is.EqualTo(-1f).Within(1e-5f), "X is mirrored into Unity's handedness");

            // The file's floor faces up (counter-clockwise seen from above); mirrored and rewound it must still face up.
            Mesh mesh = floor.ToMesh("floor");
            Vector3[] v = mesh.vertices;
            int[] t = mesh.triangles;
            Vector3 normal = Vector3.Cross(v[t[1]] - v[t[0]], v[t[2]] - v[t[0]]);
            Assert.Greater(normal.y, 0f, "the floor faces down after import");
            Assert.That(Vector3.Dot(mesh.normals[0], Vector3.up), Is.EqualTo(1f).Within(1e-4f));

            ObjGroup post = model.Find("Steel");
            Assert.AreEqual(1, post.TriangleCount, "negative indices count back from the end");
            Assert.AreEqual(0, post.Normals.Length, "no normals in the file, so the mesh computes them");
            Assert.AreEqual(3, post.ToMesh("post").normals.Length);
        }

        [Test]
        public void BrokenFilesAreReportedByLine()
        {
            Assert.Throws<FormatException>(() => ObjModel.Parse("v 0 0\n"));
            Assert.Throws<FormatException>(() => ObjModel.Parse("v 0 0 0\nf 1 2 3\n"));
            Assert.Throws<FormatException>(() => ObjModel.Parse("v a b c\n"));
            Assert.AreEqual(0, ObjModel.Parse("# nothing here\n").Groups.Count);
        }

        [Test]
        public void TheTentAssetIsTrimmedToTheCanvasAndUnitSized()
        {
            string path = Path.Combine(Application.dataPath, "_Lazpoly", "Data", "Resources", "Lazpoly", "Models", "camp_tent.txt");
            Assert.IsTrue(File.Exists(path), "run Tools/Art/make_tent.py");
            ObjModel tent = ObjModel.Parse(File.ReadAllText(path));

            Assert.AreEqual(5, tent.Groups.Count, "guy lines are left out");
            Assert.IsNull(tent.Find("Guyline_Sand"));
            Assert.IsNotNull(tent.Find(TentParts.CanvasPart));
            Assert.IsNotNull(tent.Find(TentParts.SeamsPart));
            Assert.That(tent.TriangleCount, Is.InRange(1000, 1200));

            Bounds bounds = tent.Bounds;
            Assert.That(bounds.size.z, Is.EqualTo(1f).Within(0.01f), "the tent is one unit long");
            Assert.That(bounds.center.x, Is.EqualTo(0f).Within(0.01f));
            Assert.That(bounds.center.z, Is.EqualTo(0f).Within(0.01f));
            Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.01f), "the groundsheet lies on the tile");
            Assert.Less(bounds.size.x, 0.9f, "pegs and line tags are cut away");
            Assert.That(bounds.size.y, Is.InRange(0.4f, 0.7f));
        }

        [Test]
        public void TheOwnerMarkerIsTheTentPaintedInTheOwnersColour()
        {
            var root = new GameObject("Marker Test");
            try
            {
                Assert.IsTrue(TentParts.IsAvailable, "the tent model is not in Resources");
                OwnerMarker marker = OwnerMarker.Create("Owner marker", root.transform, 0f);
                Assert.IsTrue(marker.IsTent);
                Assert.AreEqual(5, marker.transform.childCount, "one part per material");

                var owner = new Color(0.2f, 0.4f, 0.9f);
                marker.Paint(owner);
                Material canvas = marker.transform.Find(TentParts.CanvasPart).GetComponent<Renderer>().sharedMaterial;
                Material seams = marker.transform.Find(TentParts.SeamsPart).GetComponent<Renderer>().sharedMaterial;
                Material poles = marker.transform.Find("Poles_Aluminum").GetComponent<Renderer>().sharedMaterial;
                AssertColor(owner, canvas.color);
                AssertColor(TentParts.Shade(owner), seams.color);
                Assert.Less(seams.color.grayscale, canvas.color.grayscale, "seams are darker than the canvas");
                AssertColor(TentParts.Aluminium, poles.color);

                marker.Place(new Vector3(0f, 0f, -6f), Vector3.back, new Vector2(2f, 2.8f), 0.2f, Vector3.zero);
                Vector3 scale = marker.RestScale(2f);
                Assert.That(scale.x, Is.EqualTo(scale.z).Within(1e-5f), "the tent scales uniformly");
                Assert.Less(marker.transform.localPosition.z, -6f + 2.8f * 0.5f - scale.z / 2f, "the tent stays inside the tile");
                Assert.Greater(marker.transform.localPosition.z, -6f);
                Vector3 along = marker.transform.localRotation * Vector3.forward;
                Assert.That(Mathf.Abs(along.x), Is.EqualTo(1f).Within(1e-4f), "the tent lies along the row");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
