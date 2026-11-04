using System;
using System.Collections.Generic;
using System.IO;
using Lazpoly.Core.Board;
using Lazpoly.Presentation.Models;
using Lazpoly.Presentation.Tabletop;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    public class BuildingModelsTests
    {
        private static byte[] PackFile()
        {
            string path = Path.Combine(Application.dataPath, "_Lazpoly", "Data", "Resources", "Lazpoly", "Models", "buildings.bytes");
            Assert.IsTrue(File.Exists(path), "run Tools/Art/make_buildings.py");
            return File.ReadAllBytes(path);
        }

        private static BuildingMesh Find(IReadOnlyList<BuildingMesh> pack, string name)
        {
            foreach (BuildingMesh model in pack) if (model.Name == name) return model;
            Assert.Fail($"the pack has no {name}");
            return null;
        }

        /// <summary>The source models' triangle counts (Tools/Art/Models/Buildings/Validation.json): the whole copies keep every one.</summary>
        private static readonly int[] SourceTriangles = { 2028, 7160, 14168, 19788, 25600, 51268 };

        [Test]
        public void ThePackHoldsEachLevelWholeAndAsALightCopy()
        {
            IReadOnlyList<BuildingMesh> pack = BuildingPack.Parse(PackFile());
            Assert.AreEqual(2 * BuildingModels.Names.Length, pack.Count);
            Assert.AreEqual(SquareDefinition.MaxBuildingLevel + 1, BuildingModels.Names.Length, "the plot, four houses and the hotel");

            int lightTotal = 0;
            for (int level = 0; level < BuildingModels.Names.Length; level++)
            {
                BuildingMesh whole = Find(pack, BuildingModels.Names[level]);
                BuildingMesh light = Find(pack, BuildingModels.Names[level] + BuildingModels.LowSuffix);
                Assert.AreEqual(SourceTriangles[level], whole.Triangles.Length / 3, $"{whole.Name} keeps all its detail");
                Assert.Less(light.Triangles.Length / 3, SourceTriangles[level] * 0.4f, $"{light.Name} is thinned");
                lightTotal += light.Triangles.Length / 3;
            }

            Assert.Less(lightTotal, 50000, "the light copies together stay light");

            foreach (BuildingMesh model in pack)
            {
                Assert.AreEqual(model.Positions.Length, model.Normals.Length);

                var bounds = new Bounds(model.Positions[0], Vector3.zero);
                foreach (Vector3 position in model.Positions) bounds.Encapsulate(position);
                Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.005f), $"{model.Name} stands on the ground");
                Assert.That(bounds.size.x, Is.EqualTo(BuildingModels.Width).Within(0.02f), $"{model.Name} footprint along the front");
                Assert.That(bounds.size.z, Is.EqualTo(BuildingModels.Depth).Within(0.02f), $"{model.Name} footprint front to back");
                Assert.That(bounds.center.x, Is.EqualTo(0f).Within(0.01f));
                Assert.That(bounds.center.z, Is.EqualTo(0f).Within(0.01f));
                float worst = 0f;
                foreach (Vector3 normal in model.Normals) worst = Mathf.Max(worst, Mathf.Abs(normal.magnitude - 1f));
                Assert.Less(worst, 0.01f, $"{model.Name} normals are unit length");
            }

            float lastHeight = -1f;
            foreach (string name in BuildingModels.Names)
            {
                float top = 0f;
                foreach (Vector3 position in Find(pack, name).Positions) top = Mathf.Max(top, position.y);
                Assert.Greater(top, lastHeight, $"{name} stands taller than the level below");
                lastHeight = top;
            }
        }

        [Test]
        public void ABrokenPackIsRefused()
        {
            byte[] good = PackFile();
            Assert.Throws<FormatException>(() => BuildingPack.Parse(new byte[] { 1, 2, 3, 4, 0, 0, 0, 0 }), "wrong magic");
            var cut = new byte[good.Length - 10];
            Array.Copy(good, cut, cut.Length);
            Assert.Throws<FormatException>(() => BuildingPack.Parse(cut), "cut short");
            var longer = new byte[good.Length + 1];
            Array.Copy(good, longer, good.Length);
            Assert.Throws<FormatException>(() => BuildingPack.Parse(longer), "extra data");

            // One model with a single triangle that points past its only vertex.
            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            writer.Write(BuildingPack.Magic);
            writer.Write(1);
            writer.Write((byte)1);
            writer.Write((byte)'x');
            writer.Write(1);
            writer.Write(0f); writer.Write(0f); writer.Write(0f);
            writer.Write((short)0); writer.Write((short)32767); writer.Write((short)0);
            writer.Write((byte)2);
            writer.Write(3);
            writer.Write((ushort)0); writer.Write((ushort)0); writer.Write((ushort)5);
            Assert.Throws<FormatException>(() => BuildingPack.Parse(stream.ToArray()), "an index past the vertices");
        }

        [Test]
        public void EachLevelIsOneWhiteBuildingThatShowsAlone()
        {
            var root = new GameObject("Building Test");
            try
            {
                Assert.IsTrue(BuildingModels.IsAvailable, "the building pack is not in Resources");
                for (int level = 0; level <= SquareDefinition.MaxBuildingLevel; level++)
                {
                    GameObject building = BuildingModels.Create(level, $"Level {level}", root.transform);
                    Assert.IsNotNull(building);
                    Assert.AreEqual(1, building.transform.childCount);
                    Transform part = building.transform.GetChild(0);
                    Assert.AreEqual(BuildingModels.PartName, part.name);
                    Assert.Greater(part.GetComponent<MeshFilter>().sharedMesh.vertexCount, 0);
                }

                Assert.IsNull(BuildingModels.Create(SquareDefinition.MaxBuildingLevel + 1, "Too high", root.transform));

                // High quality shows the whole models; a lower one swaps every building already standing to its light copy.
                GameObject hotel = BuildingModels.Create(SquareDefinition.MaxBuildingLevel, "Hotel", root.transform);
                MeshFilter filter = hotel.GetComponentInChildren<MeshFilter>();
                try
                {
                    BuildingModels.SetFullDetail(true);
                    Assert.AreSame(BuildingModels.MeshFor(SquareDefinition.MaxBuildingLevel, true), filter.sharedMesh);
                    BuildingModels.SetFullDetail(false);
                    Assert.AreSame(BuildingModels.MeshFor(SquareDefinition.MaxBuildingLevel, false), filter.sharedMesh);
                    Assert.Less(filter.sharedMesh.GetIndexCount(0), BuildingModels.MeshFor(SquareDefinition.MaxBuildingLevel, true).GetIndexCount(0));
                    Assert.IsTrue(RoomDressing.FullBuildingsFor(Lazpoly.Infra.Settings.GraphicsQuality.High));
                    Assert.IsFalse(RoomDressing.FullBuildingsFor(Lazpoly.Infra.Settings.GraphicsQuality.Medium));
                    Assert.IsFalse(RoomDressing.FullBuildingsFor(Lazpoly.Infra.Settings.GraphicsQuality.Low));
                }
                finally
                {
                    BuildingModels.SetFullDetail(true);
                }

                // On the board one building stands per level: the house of that many storeys, or the hotel.
                for (int level = 0; level <= SquareDefinition.MaxBuildingLevel; level++)
                {
                    int shown = 0;
                    for (int index = 0; index < SquareDefinition.MaxBuildingLevel; index++)
                    {
                        if (!BoardView.Shows(index, level)) continue;
                        shown++;
                        Assert.AreEqual(level - 1, index);
                    }

                    Assert.AreEqual(level == 0 ? 0 : 1, shown, $"level {level}");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TheOwnerMarkerIsThePlotInTheOwnersColour()
        {
            var root = new GameObject("Marker Test");
            try
            {
                OwnerMarker marker = OwnerMarker.Create("Owner marker", root.transform, 0f);
                Assert.IsTrue(marker.IsPlot, "the plot stands for the owner when the models are in the build");
                Assert.AreEqual(1, marker.transform.childCount);

                var owner = new Color(0.2f, 0.4f, 0.9f);
                marker.Paint(owner);
                Color painted = marker.GetComponentInChildren<Renderer>().sharedMaterial.color;
                Color expected = Color.Lerp(owner, Color.white, BoardView.BuildingLightening);
                Assert.That(Vector4.Distance(expected, painted), Is.LessThan(1e-4f), $"expected {expected}, got {painted}");

                var spot = new Vector3(1f, 0.2f, -5f);
                marker.Place(Vector3.back, spot);
                Assert.AreEqual(spot, marker.transform.localPosition, "the plot stands where the buildings will");
                Assert.That(Vector3.Distance(marker.transform.localRotation * Vector3.forward, Vector3.back), Is.LessThan(1e-4f),
                    "its sign faces out like the buildings' fronts");
                Assert.AreEqual(Vector3.one * (2f * BoardView.BuildingScale), marker.RestScale(2f), "as big as the buildings");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
