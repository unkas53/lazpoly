using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Models;
using Lazpoly.Presentation.Tabletop;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    /// <summary>The holiday resorts on the board: squares as wide as ever, a beach tile, and the owner's beach in four stages.</summary>
    public class ResortViewTests
    {
        [Test]
        public void ABiggerBoardKeepsTheSquaresAsWideAsTheTwentyFourSquareOne()
        {
            Assert.That(BoardView.HalfSizeFor(24), Is.EqualTo(5.7f).Within(1e-4f), "the 24-square board keeps its old size");
            foreach (int count in new[] { 24, 28, 32 })
            {
                var layout = new BoardLayout(count, BoardView.HalfSizeFor(count), BoardView.DepthRatio);
                Assert.That(layout.Width, Is.EqualTo(BoardView.SquareWidth).Within(1e-4f), $"{count} squares");
            }

            Assert.Greater(BoardView.HalfSizeFor(28), BoardView.HalfSizeFor(24), "more squares make a bigger board");
            Assert.That(new BoardLayout(30, BoardView.HalfSizeFor(30)).Width, Is.EqualTo(BoardView.SquareWidth).Within(1e-4f), "a count without corners too");
        }

        [Test]
        public void TheBeachStandsOnTheSandBetweenTheSeaAndTheName()
        {
            var layout = new BoardLayout(28, BoardView.HalfSizeFor(28), BoardView.DepthRatio);
            float widthInDepths = (layout.Width - BoardView.Gap) / (layout.Depth - BoardView.Gap);
            float half = ResortParts.Depth * BoardView.BuildingScale * widthInDepths / 2f;
            Assert.GreaterOrEqual(BoardView.ResortOffset - half, BoardView.SeaOffset + BoardView.SeaDepth / 2f - 0.01f, "the beach starts where the sea ends");
            Assert.LessOrEqual(BoardView.ResortOffset + half, BoardView.NameOffset - BoardView.StripDepth / 2f, "and ends before the name");
            Assert.GreaterOrEqual(BoardView.SeaOffset - BoardView.SeaDepth / 2f, -0.5f, "the sea stays on the tile");
            Assert.Less(ResortParts.Width * BoardView.BuildingScale, 1f - BoardView.Gap / layout.Width, "the beach stays inside its tile's width");
        }

        [Test]
        public void EachStageAddsToTheBeachAndStaysOnItsSpot()
        {
            var holder = new GameObject("Beach Test");
            try
            {
                // The owner's wish (2026-11-11): loungers at one resort held, a small hut at two, two huts at three, a big hut at four.
                int[] loungers = { 2, 2, 2, 2 };
                int[] parasols = { 1, 1, 1, 1 };
                int[] huts = { 0, 1, 2, 0 };
                int[] bigHuts = { 0, 0, 0, 1 };
                int previous = 0;
                for (int stage = 1; stage <= ResortParts.Stages; stage++)
                {
                    GameObject beach = ResortParts.Layout(stage, $"Beach {stage}", holder.transform);
                    Assert.AreEqual(loungers[stage - 1], Count(beach, "Lounger"), $"stage {stage} loungers");
                    Assert.AreEqual(parasols[stage - 1], Count(beach, "Parasol"), $"stage {stage} parasols");
                    Assert.AreEqual(huts[stage - 1], Count(beach, "Hut"), $"stage {stage} small huts");
                    Assert.AreEqual(bigHuts[stage - 1], Count(beach, "Big hut"), $"stage {stage} big hut");
                    Assert.AreEqual(0, Count(beach, "Palm"), $"stage {stage}: the palms stand apart, on every resort");
                    Renderer[] parts = beach.GetComponentsInChildren<Renderer>(true);
                    Assert.Greater(parts.Length, previous, $"stage {stage} has more on it than the one before");
                    previous = parts.Length;

                    // The board's copy is the same beach merged into a mesh per colour: far fewer draws, the same extent.
                    GameObject merged = ResortParts.Stage(stage, $"Merged {stage}", holder.transform);
                    Renderer[] draws = merged.GetComponentsInChildren<Renderer>(true);
                    Assert.LessOrEqual(draws.Length, 9, $"stage {stage} draws in a handful of calls");
                    Assert.AreEqual(1, Count(merged, ResortParts.OwnerPart), $"stage {stage}: one mesh carries the owner's colour");
                    Bounds mergedBounds = Extent(draws);

                    Bounds bounds = Extent(parts);
                    Assert.That(Vector3.Distance(bounds.min, mergedBounds.min) + Vector3.Distance(bounds.max, mergedBounds.max), Is.LessThan(1e-3f), $"stage {stage} merges in place");
                    Assert.LessOrEqual(bounds.extents.x, ResortParts.Width / 2f, $"stage {stage} stays within its width");
                    Assert.LessOrEqual(Mathf.Max(-bounds.min.x, bounds.max.x), ResortParts.StageHalfWidth + 1e-3f, $"stage {stage} keeps between the palms");
                    Assert.LessOrEqual(bounds.extents.z * 2f, ResortParts.Depth + 1e-3f, $"stage {stage} stays within its depth");
                    Assert.LessOrEqual(Mathf.Abs(bounds.center.z) + bounds.extents.z, ResortParts.Depth / 2f + 1e-3f, $"stage {stage} is centred on its spot");
                    Assert.GreaterOrEqual(bounds.min.y, -1e-3f, $"stage {stage} stands on the tile");
                    Assert.LessOrEqual(bounds.max.y, ResortParts.Height, $"stage {stage} is no taller than a palm");
                }

                GameObject full = holder.transform.Find("Beach 4").gameObject;
                Assert.Greater(Count(full, "Post"), 0, "the big hut has its porch");
                Assert.AreEqual(2, Count(full, "Window"), "and a window either side of its door");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void EveryResortHasTwoPalmsAtTheEndsOfItsBeach()
        {
            var holder = new GameObject("Palm Test");
            try
            {
                GameObject loose = ResortParts.PalmLayout("Palms", holder.transform);
                Assert.AreEqual(2, Count(loose, "Palm"));
                Assert.AreEqual(0, Count(loose, ResortParts.OwnerPart), "the palms keep their own colours");
                Assert.Greater(ResortParts.PalmSpread, ResortParts.StageHalfWidth, "they stand beyond every stage");

                GameObject merged = ResortParts.Palms("Merged palms", holder.transform);
                Renderer[] draws = merged.GetComponentsInChildren<Renderer>(true);
                Assert.LessOrEqual(draws.Length, 5, "the palms draw in a few calls");
                Bounds bounds = Extent(draws);
                Bounds looseBounds = Extent(loose.GetComponentsInChildren<Renderer>(true));
                Assert.That(Vector3.Distance(bounds.min, looseBounds.min) + Vector3.Distance(bounds.max, looseBounds.max), Is.LessThan(1e-3f), "they merge in place");
                Assert.LessOrEqual(Mathf.Max(-bounds.min.x, bounds.max.x), ResortParts.Width / 2f, "within the beach's width");
                Assert.LessOrEqual(Mathf.Max(-bounds.min.z, bounds.max.z), ResortParts.Depth / 2f, "and its depth");
                Assert.GreaterOrEqual(bounds.min.y, -1e-3f, "on the sand");
                Assert.LessOrEqual(bounds.max.y, ResortParts.Height, "no taller than the beach allows");
                Assert.Greater(bounds.max.y, ResortParts.Height * 0.75f, "and the tallest thing on it");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void TheCardsPictureStageHoldsEveryBeachOutOfSightUntilAsked()
        {
            var holder = new GameObject("Preview Holder");
            var preview = new BuildingPreview(holder.transform);
            try
            {
                for (int stage = 1; stage <= ResortParts.Stages; stage++)
                {
                    Transform beach = preview.Root.transform.Find($"Beach {stage}");
                    Assert.IsNotNull(beach, $"stage {stage} is on the picture's stage");
                    Assert.IsFalse(beach.gameObject.activeSelf, "it stands only while its picture is taken");
                }

                Assert.Less(BuildingPreview.ModelScale * ResortParts.Width, BuildingPreview.TileWidth, "the beach fits across the picture's tile");
                Assert.Less(BuildingPreview.RowDepth + BuildingPreview.ModelScale * ResortParts.Depth / 2f, BuildingPreview.TileDepth / 2f, "and stays on it");
                Assert.IsNull(preview.BeachPicture(2, Color.white, Color.red), "no pictures are drawn outside play mode");
            }
            finally
            {
                preview.Dispose();
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void TheCushionsParasolsAndTowelsWearTheOwnersColour()
        {
            var holder = new GameObject("Beach Test");
            try
            {
                GameObject beach = ResortParts.Stage(2, "Beach", holder.transform);
                ResortParts.PaintOwner(beach, Color.red);
                int owned = 0;
                foreach (Renderer part in beach.GetComponentsInChildren<Renderer>(true))
                {
                    if (part.sharedMaterial == null) continue;
                    if (part.name == ResortParts.OwnerPart)
                    {
                        owned++;
                        Assert.AreEqual(Color.red, part.sharedMaterial.color);
                    }
                    else
                    {
                        Assert.AreNotEqual(Color.red, part.sharedMaterial.color, $"{part.name} keeps its own colour");
                    }
                }

                Assert.AreEqual(1, owned, "the cushions, the parasol, the towel and the ball are one mesh");
                ResortParts.PaintOwner(beach, null);
                foreach (Renderer part in beach.GetComponentsInChildren<Renderer>(true))
                {
                    if (part.name == ResortParts.OwnerPart && part.sharedMaterial != null) Assert.AreEqual(ResortParts.Unowned, part.sharedMaterial.color);
                }
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void TheShippedBoardPrintsABeachOnEachResortAndGrowsItsOwnersBeach()
        {
            BoardData data = BoardDataLoader.LoadFromResources();
            var holder = new GameObject("Board Test");
            try
            {
                BoardView board = BoardView.Create(holder.transform, data);
                Assert.That(board.Layout.Width, Is.EqualTo(BoardView.SquareWidth).Within(1e-4f), "the squares keep their width on the 32-square board");

                var resorts = new List<int>();
                for (int i = 0; i < data.Board.Count; i++)
                {
                    if (data.Board[i].IsResort) resorts.Add(i);
                }

                Assert.AreEqual(4, resorts.Count);
                foreach (Transform child in holder.GetComponentsInChildren<Transform>(true))
                {
                    Assert.AreNotEqual("Shore", child.name, "the sea is printed into the tile, not laid over it");
                }

                foreach (int resort in resorts)
                {
                    Transform tile = null;
                    foreach (Transform child in holder.GetComponentsInChildren<Transform>(true))
                    {
                        if (child.name.StartsWith($"Square {resort:00} ")) tile = child;
                    }

                    Material material = tile.GetComponent<Renderer>().sharedMaterial;
                    Assert.AreEqual(Color.white, material.color, "the sand and the sea are in the texture");
                    var skin = (Texture2D)material.mainTexture;
                    Vector3 down = BoardView.ScreenDown(BoardLayout.Outward(board.Layout.SquareCenter(resort)));
                    bool lowEnd = Vector3.Dot(tile.forward, down) > 0f;
                    float V(float fromTop) => lowEnd ? fromTop : 1f - fromTop;
                    Color sea = skin.GetPixelBilinear(0.5f, V(0.5f + BoardView.SeaOffset + 0.01f));
                    // A soft sea (the owner's wish for close tones, 2026-11-10): still plainly bluer than it is red, and no sand.
                    Assert.Greater(sea.b, sea.r + 0.1f, "the sea runs along the top inside the frame");
                    Assert.Greater(sea.g, sea.r + 0.1f, "a sea green, not the sand");
                    Color edge = skin.GetPixelBilinear(0.5f, V(0.02f));
                    Assert.Greater(edge.b, edge.r, "the sea runs out to the frame on the tile's edge");
                    Color sand = skin.GetPixelBilinear(0.5f, V(0.5f + BoardView.ResortOffset));
                    Assert.Greater(sand.r, sand.b + 0.1f, "the beach stands on sand");
                }

                var beaches = new List<OwnerMarker>();
                foreach (OwnerMarker marker in holder.GetComponentsInChildren<OwnerMarker>(true))
                {
                    if (marker.IsResort) beaches.Add(marker);
                }

                Assert.AreEqual(resorts.Count, beaches.Count, "each resort has a beach in place of the plot");
                for (int i = 0; i < data.Board.Count; i++)
                {
                    if (!data.Board[i].IsResort)
                    {
                        Assert.IsNull(board.Palms(i), $"square {i} has no palms");
                        continue;
                    }

                    Assert.IsNotNull(board.Palms(i), $"{data.Board[i].Name} has its palms");
                    Assert.IsTrue(board.Palms(i).gameObject.activeInHierarchy, $"{data.Board[i].Name}: the palms stand while it is for sale");
                    Assert.AreEqual(beaches[0].RestScale(board.Layout.Width - BoardView.Gap), board.Palms(i).localScale, "at the beach's size");
                }

                Assert.AreEqual(Vector3.one * (board.Layout.Width - BoardView.Gap) * BoardView.BuildingScale, beaches[0].RestScale(board.Layout.Width - BoardView.Gap));

                board.SetOwner(resorts[0], Color.red);
                board.SetResortStage(resorts[0], 3);
                board.SetResortStage(resorts[1], 2);
                OwnerMarker shown = null;
                foreach (OwnerMarker beach in beaches)
                {
                    if (beach.gameObject.activeSelf) shown = beach;
                }

                Assert.IsNotNull(shown, "the bought resort shows its beach");
                Assert.AreEqual(3, shown.Stage, "the stage of three resorts held");
                int active = 0;
                foreach (Transform stage in shown.transform)
                {
                    if (stage.gameObject.activeSelf) active++;
                }

                Assert.AreEqual(1, active, "one stage stands at a time");
                shown.ShowStage(9);
                Assert.AreEqual(ResortParts.Stages, shown.Stage, "more than four is the full resort");
                shown.ShowStage(0);
                Assert.AreEqual(1, shown.Stage);

                Assert.IsTrue(board.Palms(resorts[0]).gameObject.activeInHierarchy, "the palms stand beside the owner's beach");
                board.SetOwner(resorts[0], null);
                Assert.IsFalse(shown.gameObject.activeSelf, "a resort for sale has no beach");
                Assert.IsTrue(board.Palms(resorts[0]).gameObject.activeInHierarchy, "but keeps its palms");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        /// <summary>
        /// The box round the parts' vertices themselves: a renderer's own bounds turn the mesh's box with the part,
        /// which makes a turned part (the bar's roof) look wider than it is.
        /// </summary>
        private static Bounds Extent(IEnumerable<Renderer> parts)
        {
            Bounds? bounds = null;
            foreach (Renderer part in parts)
            {
                Mesh mesh = part.GetComponent<MeshFilter>().sharedMesh;
                Matrix4x4 toWorld = part.transform.localToWorldMatrix;
                foreach (Vector3 vertex in mesh.vertices)
                {
                    Vector3 point = toWorld.MultiplyPoint3x4(vertex);
                    if (bounds.HasValue)
                    {
                        Bounds grown = bounds.Value;
                        grown.Encapsulate(point);
                        bounds = grown;
                    }
                    else
                    {
                        bounds = new Bounds(point, Vector3.zero);
                    }
                }
            }

            return bounds ?? new Bounds();
        }

        private static int Count(GameObject root, string name)
        {
            int count = 0;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name) count++;
            }

            return count;
        }
    }
}
