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

        [Test]
        public void ThePackHoldsTheSixLevelsThinnedForTheBoard()
        {
            IReadOnlyList<BuildingMesh> pack = BuildingPack.Parse(PackFile());
            Assert.AreEqual(BuildingModels.Names.Length, pack.Count);
            Assert.AreEqual(SquareDefinition.MaxBuildingLevel + 1, BuildingModels.Names.Length, "the plot, four houses and the hotel");

            float lastHeight = -1f;
            int total = 0;
            for (int level = 0; level < pack.Count; level++)
            {
                BuildingMesh model = pack[level];
                Assert.AreEqual(BuildingModels.Names[level], model.Name);
                Assert.AreEqual(model.Positions.Length, model.Normals.Length);
                int triangles = model.Triangles.Length / 3;
                total += triangles;
                Assert.Less(triangles, 20000, $"{model.Name} is thinned");

                var bounds = new Bounds(model.Positions[0], Vector3.zero);
                foreach (Vector3 position in model.Positions) bounds.Encapsulate(position);
                Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.005f), $"{model.Name} stands on the ground");
                Assert.That(bounds.size.x, Is.EqualTo(BuildingModels.Width).Within(0.02f), $"{model.Name} footprint along the front");
                Assert.That(bounds.size.z, Is.EqualTo(BuildingModels.Depth).Within(0.02f), $"{model.Name} footprint front to back");
                Assert.That(bounds.center.x, Is.EqualTo(0f).Within(0.01f));
                Assert.That(bounds.center.z, Is.EqualTo(0f).Within(0.01f));
                Assert.Greater(bounds.size.y, lastHeight, "each level stands taller");
                lastHeight = bounds.size.y;
                foreach (Vector3 normal in model.Normals) Assert.That(normal.magnitude, Is.EqualTo(1f).Within(0.01f));
            }

            Assert.Less(total, 50000, "all six together stay light");
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
    }
}
