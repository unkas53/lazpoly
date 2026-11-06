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
                int[] loungers = { 1, 2, 3, 3 };
                int[] parasols = { 1, 1, 2, 2 };
                int previous = 0;
                for (int stage = 1; stage <= ResortParts.Stages; stage++)
                {
                    GameObject beach = ResortParts.Stage(stage, $"Beach {stage}", holder.transform);
                    Assert.AreEqual(loungers[stage - 1], Count(beach, "Lounger"), $"stage {stage} loungers");
                    Assert.AreEqual(parasols[stage - 1], Count(beach, "Parasol"), $"stage {stage} parasols");
                    Renderer[] parts = beach.GetComponentsInChildren<Renderer>(true);
                    Assert.Greater(parts.Length, previous, $"stage {stage} has more on it than the one before");
                    previous = parts.Length;

                    Bounds bounds = Extent(parts);
                    Assert.LessOrEqual(bounds.extents.x, ResortParts.Width / 2f, $"stage {stage} stays within its width");
                    Assert.LessOrEqual(bounds.extents.z * 2f, ResortParts.Depth + 1e-3f, $"stage {stage} stays within its depth");
                    Assert.LessOrEqual(Mathf.Abs(bounds.center.z) + bounds.extents.z, ResortParts.Depth / 2f + 1e-3f, $"stage {stage} is centred on its spot");
                    Assert.GreaterOrEqual(bounds.min.y, -1e-3f, $"stage {stage} stands on the tile");
                    Assert.LessOrEqual(bounds.max.y, ResortParts.Height, $"stage {stage} is no taller than a palm");
                }

                GameObject full = holder.transform.Find("Beach 4").gameObject;
                Assert.Greater(Count(full, "Water"), 0, "the full resort has its pool");
                Assert.Greater(Count(full, "Bar"), 0, "and its beach bar");
                Assert.AreEqual(2, Count(full, "Palm"));
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

                Assert.GreaterOrEqual(owned, 5, "two loungers' cushions, the parasol, the towel and the ball");
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
                Assert.That(board.Layout.Width, Is.EqualTo(BoardView.SquareWidth).Within(1e-4f), "the squares keep their width on the 28-square board");

                var resorts = new List<int>();
                for (int i = 0; i < data.Board.Count; i++)
                {
                    if (data.Board[i].IsResort) resorts.Add(i);
                }

                Assert.AreEqual(4, resorts.Count);
                var shores = new List<Transform>();
                foreach (Transform child in holder.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == "Shore") shores.Add(child);
                }

                Assert.AreEqual(resorts.Count, shores.Count, "a strip of sea on every resort, and on nothing else");
                foreach (Transform shore in shores)
                {
                    StringAssert.Contains("Square", shore.parent.name, "the shore belongs to its tile");
                    Assert.IsNotNull(shore.Find("Sea"));
                    Assert.AreEqual(BoardView.SurfScallops, Count(shore.gameObject, "Surf"));
                }

                var beaches = new List<OwnerMarker>();
                foreach (OwnerMarker marker in holder.GetComponentsInChildren<OwnerMarker>(true))
                {
                    if (marker.IsResort) beaches.Add(marker);
                }

                Assert.AreEqual(resorts.Count, beaches.Count, "each resort has a beach in place of the plot");
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

                board.SetOwner(resorts[0], null);
                Assert.IsFalse(shown.gameObject.activeSelf, "a resort for sale has no beach");
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
