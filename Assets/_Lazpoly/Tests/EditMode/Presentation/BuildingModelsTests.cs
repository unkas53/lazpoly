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

        private static readonly int Door = Array.IndexOf(BuildingModels.Slots, "Door");

        /// <summary>The meshes of one model (or its light copy), one per slot it has.</summary>
        private static List<BuildingMesh> PartsOf(IReadOnlyList<BuildingMesh> pack, string model)
        {
            var parts = new List<BuildingMesh>();
            foreach (BuildingMesh mesh in pack) if (mesh.Name.StartsWith(model + "/", StringComparison.Ordinal)) parts.Add(mesh);
            return parts;
        }

        private static int TrianglesOf(List<BuildingMesh> parts)
        {
            int count = 0;
            foreach (BuildingMesh part in parts) count += part.Triangles.Length / 3;
            return count;
        }

        private static Bounds BoundsOf(List<BuildingMesh> parts)
        {
            var bounds = new Bounds(parts[0].Positions[0], Vector3.zero);
            foreach (BuildingMesh part in parts) foreach (Vector3 position in part.Positions) bounds.Encapsulate(position);
            return bounds;
        }

        /// <summary>The Blender file's triangles per model (all its slots): the whole copies keep every one.</summary>
        private static readonly int[] SourceTriangles = { 2156, 7272, 14236, 19856, 25668, 51776 };

        [Test]
        public void ThePackHoldsEachLevelWholeAndAsALightCopy()
        {
            IReadOnlyList<BuildingMesh> pack = BuildingPack.Parse(PackFile());
            Assert.AreEqual(SquareDefinition.MaxBuildingLevel + 1, BuildingModels.Names.Length, "the plot, four houses and the hotel");

            var names = new HashSet<string>();
            foreach (BuildingMesh mesh in pack) Assert.IsTrue(names.Add(mesh.Name), $"{mesh.Name} is in the pack once");

            int lightTotal = 0;
            int meshes = 0;
            for (int level = 0; level < BuildingModels.Names.Length; level++)
            {
                string model = BuildingModels.Names[level];
                string lowModel = model + BuildingModels.LowSuffix;
                for (int slot = 0; slot < BuildingModels.Slots.Length; slot++)
                {
                    bool whole = names.Contains(BuildingModels.PartKey(model, slot));
                    Assert.AreEqual(whole, names.Contains(BuildingModels.PartKey(lowModel, slot)), $"{model} {BuildingModels.Slots[slot]}: the light copy has the same slots");
                    if (slot == BuildingModels.WallSlot) Assert.IsTrue(whole, $"{model} has walls to paint");
                }

                List<BuildingMesh> full = PartsOf(pack, model);
                List<BuildingMesh> light = PartsOf(pack, lowModel);
                meshes += full.Count + light.Count;
                Assert.AreEqual(SourceTriangles[level], TrianglesOf(full), $"{model} keeps all its detail");
                Assert.Less(TrianglesOf(light), SourceTriangles[level] * 0.5f, $"{lowModel} is thinned (the small plot keeps a little more than the others)");
                lightTotal += TrianglesOf(light);

                foreach (List<BuildingMesh> parts in new[] { full, light })
                {
                    Bounds bounds = BoundsOf(parts);
                    string name = parts[0].Name;
                    Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.005f), $"{name} stands on the ground");
                    Assert.That(bounds.size.x, Is.EqualTo(BuildingModels.Width).Within(0.02f), $"{name} footprint along the front");
                    Assert.That(bounds.size.z, Is.EqualTo(BuildingModels.Depth).Within(0.02f), $"{name} footprint front to back");
                    Assert.That(bounds.center.x, Is.EqualTo(0f).Within(0.01f));
                    Assert.That(bounds.center.z, Is.EqualTo(0f).Within(0.01f));
                }

                if (level > 0)
                {
                    // The door is on the front, towards +Z, where the board turns the buildings' fronts out.
                    List<BuildingMesh> door = PartsOf(pack, model).FindAll(part => part.Name == BuildingModels.PartKey(model, Door));
                    Assert.AreEqual(1, door.Count, $"{model} has a door");
                    Assert.Greater(BoundsOf(door).center.z, 0.05f, $"{model}'s door is on its front");
                }
            }

            Assert.AreEqual(meshes, pack.Count, "nothing else is in the pack");
            Assert.Less(lightTotal, 50000, "the light copies together stay light");

            foreach (BuildingMesh mesh in pack)
            {
                Assert.AreEqual(mesh.Positions.Length, mesh.Normals.Length);
                float worst = 0f;
                foreach (Vector3 normal in mesh.Normals) worst = Mathf.Max(worst, Mathf.Abs(normal.magnitude - 1f));
                Assert.Less(worst, 0.01f, $"{mesh.Name} normals are unit length");
            }

            float lastHeight = -1f;
            foreach (string name in BuildingModels.Names)
            {
                float top = BoundsOf(PartsOf(pack, name)).max.y;
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
        public void EachLevelIsOneBuildingInColouredPartsThatShowsAlone()
        {
            var root = new GameObject("Building Test");
            try
            {
                Assert.IsTrue(BuildingModels.IsAvailable, "the building pack is not in Resources");
                Assert.AreEqual(BuildingModels.Slots.Length, BuildingModels.SlotColors.Length);
                for (int level = 0; level <= SquareDefinition.MaxBuildingLevel; level++)
                {
                    GameObject building = BuildingModels.Create(level, $"Level {level}", root.transform);
                    Assert.IsNotNull(building);
                    Assert.Greater(building.transform.childCount, 1, $"level {level} is more than its walls");
                    Assert.AreEqual(BuildingModels.Slots[BuildingModels.WallSlot], building.transform.GetChild(0).name, "the walls come first");
                    foreach (Transform part in building.transform)
                    {
                        int slot = Array.IndexOf(BuildingModels.Slots, part.name);
                        Assert.GreaterOrEqual(slot, 0, $"{part.name} is a slot");
                        Assert.Greater(part.GetComponent<MeshFilter>().sharedMesh.vertexCount, 0);
                        Assert.That(Vector4.Distance(BuildingModels.SlotColors[slot], part.GetComponent<Renderer>().sharedMaterial.color), Is.LessThan(1e-4f),
                            $"{part.name} starts in its own colour");
                    }
                }

                // Owned, only the walls change; the door, frames, glass and roof keep their colours.
                GameObject house = BuildingModels.Create(2, "House", root.transform);
                var owner = new Color(0.9f, 0.2f, 0.2f);
                BuildingModels.PaintOwner(house, owner);
                foreach (Transform part in house.transform)
                {
                    int slot = Array.IndexOf(BuildingModels.Slots, part.name);
                    Color expected = slot == BuildingModels.WallSlot ? Color.Lerp(owner, Color.white, BuildingModels.OwnerLightening) : BuildingModels.SlotColors[slot];
                    Assert.That(Vector4.Distance(expected, part.GetComponent<Renderer>().sharedMaterial.color), Is.LessThan(1e-4f), part.name);
                }

                BuildingModels.PaintOwner(house, null);
                Assert.That(Vector4.Distance(BuildingModels.SlotColors[BuildingModels.WallSlot], house.transform.GetChild(0).GetComponent<Renderer>().sharedMaterial.color),
                    Is.LessThan(1e-4f), "unowned, the walls are white again");

                Assert.IsNull(BuildingModels.Create(SquareDefinition.MaxBuildingLevel + 1, "Too high", root.transform));

                // High quality shows the whole models; a lower one swaps every building already standing to its light copy.
                GameObject hotel = BuildingModels.Create(SquareDefinition.MaxBuildingLevel, "Hotel", root.transform);
                MeshFilter filter = hotel.GetComponentInChildren<MeshFilter>();
                try
                {
                    BuildingModels.SetFullDetail(true);
                    Assert.AreSame(BuildingModels.MeshFor(SquareDefinition.MaxBuildingLevel, BuildingModels.WallSlot, true), filter.sharedMesh);
                    BuildingModels.SetFullDetail(false);
                    Assert.AreSame(BuildingModels.MeshFor(SquareDefinition.MaxBuildingLevel, BuildingModels.WallSlot, false), filter.sharedMesh);
                    Assert.Less(filter.sharedMesh.GetIndexCount(0), BuildingModels.MeshFor(SquareDefinition.MaxBuildingLevel, BuildingModels.WallSlot, true).GetIndexCount(0));
                    foreach (MeshFilter part in hotel.GetComponentsInChildren<MeshFilter>())
                    {
                        Assert.AreSame(BuildingModels.MeshFor(SquareDefinition.MaxBuildingLevel, Array.IndexOf(BuildingModels.Slots, part.name), false), part.sharedMesh,
                            $"the hotel's {part.name} is light too");
                    }
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
                Assert.Greater(marker.transform.childCount, 1, "the plot's paving, sign and posts");

                var owner = new Color(0.2f, 0.4f, 0.9f);
                marker.Paint(owner);
                Color painted = marker.transform.GetChild(0).GetComponent<Renderer>().sharedMaterial.color;
                Color expected = Color.Lerp(owner, Color.white, BuildingModels.OwnerLightening);
                Assert.That(Vector4.Distance(expected, painted), Is.LessThan(1e-4f), $"expected {expected}, got {painted}");
                Transform posts = marker.transform.Find(BuildingModels.Slots[Door]);
                Assert.IsNotNull(posts, "the sign stands on posts");
                Assert.That(Vector4.Distance(BuildingModels.SlotColors[Door], posts.GetComponent<Renderer>().sharedMaterial.color), Is.LessThan(1e-4f),
                    "the posts keep their wood colour");

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
