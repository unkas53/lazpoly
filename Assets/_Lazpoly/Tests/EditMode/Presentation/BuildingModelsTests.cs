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

        /// <summary>The Blender file's triangles per model (all its slots, less the few with no area): the whole copies keep every one.</summary>
        private static readonly int[] SourceTriangles = { 2060, 7223, 14200, 19820, 51526 };

        [Test]
        public void ThePackHoldsEachLevelWholeAndAsALightCopy()
        {
            IReadOnlyList<BuildingMesh> pack = BuildingPack.Parse(PackFile());
            Assert.AreEqual(SquareDefinition.MaxBuildingLevel + 1, BuildingModels.Names.Length, "the plot, three houses and the hotel");

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
            Assert.LessOrEqual(pack.Count, BuildingPack.MaxMeshes, "the reader takes the whole pack");
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

                // The owner's one wish (2026-11-21): the roof in the player's colour. The rest suits the table: light
                // warm walls, frames lighter still (black frames were turned down), glass dark enough that a window
                // reads, a pale warm slab under the house (concrete grey was turned down), and the thin belts between
                // the floors in the roof's colour.
                Color walls = BuildingModels.SlotColors[BuildingModels.WallSlot];
                Color glass = BuildingModels.SlotColors[BuildingModels.GlassSlot];
                Color frames = BuildingModels.SlotColors[BuildingModels.FrameSlot];
                Color belts = BuildingModels.SlotColors[BuildingModels.BeltSlot];
                Color ground = BuildingModels.SlotColors[BuildingModels.BaseSlot];
                Assert.Greater(Mathf.Min(walls.r, walls.g, walls.b), 0.8f, "the walls are light");
                Assert.LessOrEqual(walls.maxColorComponent, 0.92f, "the walls burn out in the table's light");
                Assert.IsTrue(walls.r >= walls.g && walls.g >= walls.b, "the walls are a warm white");
                Assert.Greater(Mathf.Min(frames.r, frames.g, frames.b), walls.maxColorComponent - 0.02f, "the window frames are lighter than the walls");
                Assert.AreEqual(frames, belts, "nobody's belts are the frames' colour");
                Assert.Less(glass.grayscale, walls.grayscale - 0.4f, "the glass is dark enough against the walls for a window to read");
                Assert.Greater(glass.grayscale, 0.3f, "the glass is black");
                Assert.Greater(glass.b, glass.r + 0.2f, "the glass is blue");
                // The slab's top faces the light and shows about 1.15 times its colour: at 0.87 it was near white and
                // glared (the owner, 2026-11-21: "bina zeminindeki arsa çok parlıyor").
                Assert.LessOrEqual(ground.maxColorComponent, 0.78f, "the slab's lit top glares in the table's light (it did at 0.87)");
                Assert.Greater(Mathf.Min(ground.r, ground.g, ground.b), 0.55f, "the slab is dark: it is stone in the sun, not a shadow");
                Assert.Less(ground.grayscale, walls.grayscale - 0.12f, "the house stands lighter than the slab under it");
                Assert.Greater(ground.r - ground.b, 0.1f, "the slab is warm stone, not concrete grey");
                Assert.AreEqual("Glass", BuildingModels.Slots[BuildingModels.GlassSlot]);
                Assert.AreEqual("Roof", BuildingModels.Slots[BuildingModels.RoofSlot]);
                Assert.AreEqual("Base", BuildingModels.Slots[BuildingModels.BaseSlot]);
                Assert.AreEqual("Frame", BuildingModels.Slots[BuildingModels.FrameSlot]);
                Assert.AreEqual("Belt", BuildingModels.Slots[BuildingModels.BeltSlot]);
                Assert.AreEqual(walls, BuildingModels.WallColor);

                var owner = new Color(0.9f, 0.2f, 0.2f);
                Assert.AreEqual(owner, BuildingModels.RoofColor(owner), "the roof is the player's colour as it is");
                Assert.AreEqual(BuildingModels.SlotColors[BuildingModels.RoofSlot], BuildingModels.RoofColor(null), "nobody's roof is terracotta");
                Assert.AreEqual(owner, BuildingModels.BeltColor(owner), "the belts are the roof's colour");
                Assert.AreEqual(belts, BuildingModels.BeltColor(null), "nobody's belts are white");

                // Every house and the hotel has a roof to wear it (the flat-roofed ones their slabs), windows in frames
                // and a slab to stand on; all but the one-storey house have belts between their floors. Owned, only the
                // roof and the belts change.
                for (int level = 1; level <= SquareDefinition.MaxBuildingLevel; level++)
                {
                    GameObject house = BuildingModels.Create(level, $"Owned {level}", root.transform);
                    Assert.IsNotNull(house.transform.Find(BuildingModels.Slots[BuildingModels.RoofSlot]), $"level {level} has a roof");
                    Assert.IsNotNull(house.transform.Find(BuildingModels.Slots[BuildingModels.GlassSlot]), $"level {level} has windows");
                    Assert.IsNotNull(house.transform.Find(BuildingModels.Slots[BuildingModels.FrameSlot]), $"level {level}'s windows have frames");
                    Assert.IsNotNull(house.transform.Find(BuildingModels.Slots[BuildingModels.BaseSlot]), $"level {level} stands on its ground");
                    Assert.AreEqual(level > 1, house.transform.Find(BuildingModels.Slots[BuildingModels.BeltSlot]) != null, $"level {level}: belts between floors");
                    BuildingModels.PaintOwner(house, owner);
                    foreach (Transform part in house.transform)
                    {
                        int slot = Array.IndexOf(BuildingModels.Slots, part.name);
                        Color expected = slot == BuildingModels.RoofSlot || slot == BuildingModels.BeltSlot ? owner : BuildingModels.SlotColors[slot];
                        Assert.That(Vector4.Distance(expected, part.GetComponent<Renderer>().sharedMaterial.color), Is.LessThan(1e-4f), $"level {level} {part.name}");
                    }

                    BuildingModels.PaintOwner(house, null);
                    foreach (Transform part in house.transform)
                    {
                        int slot = Array.IndexOf(BuildingModels.Slots, part.name);
                        Assert.That(Vector4.Distance(BuildingModels.SlotColors[slot], part.GetComponent<Renderer>().sharedMaterial.color), Is.LessThan(1e-4f),
                            $"unowned, level {level}'s {part.name} is its own colour again");
                    }
                }

                // The plot has no roof: its paving and sign wear the colour, a little lightened.
                GameObject plot = BuildingModels.Create(0, "Owned plot", root.transform);
                Assert.IsNull(plot.transform.Find(BuildingModels.Slots[BuildingModels.RoofSlot]), "the plot has no roof");
                BuildingModels.PaintOwner(plot, owner);
                Assert.That(Vector4.Distance(Color.Lerp(owner, Color.white, BuildingModels.OwnerLightening), BuildingModels.PlotColor(owner)), Is.LessThan(1e-6f));
                Assert.That(Vector4.Distance(BuildingModels.PlotColor(owner), plot.transform.GetChild(0).GetComponent<Renderer>().sharedMaterial.color),
                    Is.LessThan(1e-4f), "the plot's paving is the owner's");
                BuildingModels.PaintOwner(plot, null);
                Assert.That(Vector4.Distance(walls, plot.transform.GetChild(0).GetComponent<Renderer>().sharedMaterial.color), Is.LessThan(1e-4f), "nobody's plot is the walls' ivory");

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
        public void EveryBuildingShowsItsFrontToTheCamera()
        {
            Vector3[] edges = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            foreach (float yaw in new[] { BoardCameraRig.BaseYaw, BoardCameraRig.BaseYaw + 90f, BoardCameraRig.BaseYaw + 180f, BoardCameraRig.BaseYaw - 90f })
            {
                foreach (Vector3 outward in edges)
                {
                    Vector3 front = BoardView.BuildingFront(outward, yaw);
                    Assert.That(Mathf.Abs(Vector3.Dot(front, outward)), Is.EqualTo(1f).Within(1e-4f), "the front stays square to the edge");
                    Assert.Less(Vector3.Dot(front, BoardView.ScreenUp(yaw)), 0f, $"yaw {yaw}, edge {outward}: the front looks back at the camera");
                }
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
                marker.Place(Vector3.back, spot, BoardCameraRig.BaseYaw);
                Assert.AreEqual(spot, marker.transform.localPosition, "the plot stands where the buildings will");
                Assert.That(Vector3.Distance(marker.transform.localRotation * Vector3.forward, Vector3.back), Is.LessThan(1e-4f),
                    "on a side near the camera its sign faces out like the buildings' fronts");
                marker.Turn(Vector3.forward, BoardCameraRig.BaseYaw);
                Assert.That(Vector3.Distance(marker.transform.localRotation * Vector3.forward, Vector3.back), Is.LessThan(1e-4f),
                    "on a far side it faces in, towards the camera");
                Assert.AreEqual(Vector3.one * (2f * BoardView.BuildingScale), marker.RestScale(2f), "as big as the buildings");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
