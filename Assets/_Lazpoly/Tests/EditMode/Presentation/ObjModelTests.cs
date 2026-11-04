using System;
using Lazpoly.Presentation.Models;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    public class ObjModelTests
    {
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
    }
}
