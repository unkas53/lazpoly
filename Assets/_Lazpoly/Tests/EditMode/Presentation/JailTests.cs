using System.Collections;
using System.Collections.Generic;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Models;
using Lazpoly.Presentation.Pawns;
using Lazpoly.Presentation.Tabletop;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    /// <summary>
    /// The jail (the owner's wish, 2026-11-12): a model that suits the table and pleases the eye, and whoever is sent to
    /// jail shrinks and goes into it: the building, the places inside it, and the piece's way in and out.
    /// </summary>
    public class JailTests
    {
        private static Color Paint(Transform part) => part.GetComponent<Renderer>().sharedMaterial.color;

        private static void AssertNear(Vector3 expected, Vector3 actual, string message = null) =>
            Assert.Less(Vector3.Distance(expected, actual), 1e-3f, $"{message}: expected {expected}, was {actual}");

        [Test]
        public void TheJailIsAPlainLimestoneLockUpWithBarsAndALowSlateRoof()
        {
            var holder = new GameObject("Jail Test");
            try
            {
                GameObject jail = CornerParts.Cell("Jail", holder.transform);
                var counts = new Dictionary<string, int>();
                var colours = new List<Color>();
                foreach (Transform part in jail.transform)
                {
                    counts[part.name] = counts.TryGetValue(part.name, out int seen) ? seen + 1 : 1;
                    Color colour = Paint(part);
                    if (!colours.Exists(known => Vector4.Distance(known, colour) < 0.01f)) colours.Add(colour);
                }

                Assert.AreEqual(4, counts["Pier"], "a pier at each corner");
                Assert.AreEqual(6, counts["Bar"], "a few bars on the door and on the long side: the prisoners show between them");
                Assert.AreEqual(2, counts["Rail"], "one rail across each");
                foreach (string single in new[] { "Plinth", "Step", "Back wall", "Side wall", "Lintel", "Cornice", "Pediment", "Roof", "Lock" })
                    Assert.AreEqual(1, counts[single], single);
                Assert.LessOrEqual(colours.Count, 5, "plain: two stones, slate, iron and the lock's steel, nothing more");

                // Warm limestone like the table's squares, the roof a quiet slate, the bars dark; no brass or gold on it.
                Color stone = Paint(jail.transform.Find("Pier"));
                Assert.Greater(stone.r, stone.b + 0.1f, "a warm stone");
                Color.RGBToHSV(stone, out _, out float saturation, out float value);
                Assert.That(saturation, Is.InRange(0.1f, 0.3f), "a quiet one");
                Assert.That(value, Is.InRange(0.7f, 0.88f), "light, not burned out under the table's light");
                Color roof = Paint(jail.transform.Find("Roof"));
                Assert.Greater(roof.b, roof.r, "a slate roof");
                Assert.Less(Mathf.Max(Paint(jail.transform.Find("Bar")).r, Paint(jail.transform.Find("Bar")).b), 0.3f, "dark iron bars");
                foreach (Color colour in colours)
                {
                    Color.RGBToHSV(colour, out float hue, out float rich, out _);
                    Assert.IsFalse(rich > 0.45f && hue > 0.08f && hue < 0.18f, $"{colour}: no brass or gold on the jail");
                }

                // The door looks out at -X under the pediment; the long side at +Z is barred too; the other two are walls.
                Assert.Less(jail.transform.Find("Lock").localPosition.x, -CornerParts.CellPierX, "the lock is on the door, outside the bars");
                Assert.Less(jail.transform.Find("Step").localPosition.x, -CornerParts.CellPierX, "a step before the door");
                Assert.Greater(jail.transform.Find("Back wall").localPosition.x, 0f);
                Assert.Less(jail.transform.Find("Side wall").localPosition.z, 0f);
                int door = 0, side = 0;
                foreach (Transform part in jail.transform)
                {
                    if (part.name != "Bar") continue;
                    Assert.AreEqual(CornerParts.CellFloor, part.localPosition.y, 1e-4f, "the bars stand on the floor");
                    Assert.AreEqual(CornerParts.CellWall, part.localScale.y, 1e-4f, "and reach the lintel");
                    if (Mathf.Abs(part.localPosition.x + CornerParts.CellPierX) < 1e-4f) door++;
                    if (Mathf.Abs(part.localPosition.z - CornerParts.CellPierZ) < 1e-4f) side++;
                }

                Assert.AreEqual(2, door);
                Assert.AreEqual(4, side);
                Assert.Less(jail.transform.Find("Bar").localScale.x, 0.013f, "thin bars");

                // A low roof over a pediment that shows at both ends under the slates.
                Transform slates = jail.transform.Find("Roof"), pediment = jail.transform.Find("Pediment");
                Assert.AreSame(MeshFactory.Prism(), slates.GetComponent<MeshFilter>().sharedMesh);
                Assert.Less(slates.localScale.y, slates.localScale.z / 4f, "a low pitch");
                Assert.Greater(pediment.localScale.x, slates.localScale.x, "the pediment shows past the slates' ends");
                Assert.Less(pediment.localScale.y / pediment.localScale.z, slates.localScale.y / slates.localScale.z + 1e-4f, "and stays under them");
                Assert.Less(pediment.localScale.y, slates.localScale.y);
                Assert.That(slates.localPosition.y + slates.localScale.y, Is.InRange(0.9f, 1.15f), "about as tall as it is long");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void EveryPrisonerShowsBetweenTheBarsUnderTheLintel()
        {
            // The table's camera looks at the cell's corner between the door (-X) and the barred side (+Z), down at its pitch.
            // In the scene's frame it looks along (+x, -z). A piece shows when the line from it back to the camera leaves
            // the cell through one of the two openings, clear of the piers, with its head still under the lintel there.
            float rise = Mathf.Tan(BoardCameraRig.Pitch * Mathf.Deg2Rad);
            const float pier = 0.055f, margin = 0.02f;
            Assert.GreaterOrEqual(CornerParts.PrisonerSpots.Length, 5, "room for a crowd");
            for (int i = 0; i < CornerParts.PrisonerSpots.Length; i++)
            {
                Vector3 spot = CornerParts.PrisonerSpots[i];
                Assert.AreEqual(CornerParts.CellFloor, spot.y, 1e-5f, $"spot {i} is on the floor");
                Assert.Less(Mathf.Abs(spot.x), CornerParts.CellPierX - 0.1f, $"spot {i} is inside");
                Assert.Less(Mathf.Abs(spot.z), CornerParts.CellPierZ - 0.1f, $"spot {i} is inside");

                float toSide = (CornerParts.CellPierZ - spot.z) * Mathf.Sqrt(2f), toDoor = (spot.x + CornerParts.CellPierX) * Mathf.Sqrt(2f);
                float along = Mathf.Min(toSide, toDoor);
                float leaves = toSide < toDoor ? spot.x - toSide / Mathf.Sqrt(2f) : spot.z + toDoor / Mathf.Sqrt(2f);
                float opening = (toSide < toDoor ? CornerParts.CellPierX : CornerParts.CellPierZ) - pier - margin;
                Assert.Less(Mathf.Abs(leaves), opening, $"spot {i} is seen through an opening, not through a pier");
                Assert.LessOrEqual(spot.y + CornerParts.PrisonerRoom + along * rise, CornerParts.CellFloor + CornerParts.CellWall, $"spot {i}: the head shows under the lintel");

                for (int other = 0; other < i; other++)
                {
                    // Across the screen (the scene's x + z turned to the camera) they stand apart.
                    Vector3 apart = spot - CornerParts.PrisonerSpots[other];
                    Assert.Greater(Mathf.Abs(apart.x + apart.z) / Mathf.Sqrt(2f), 0.12f, $"spots {other} and {i} do not hide each other");
                }
            }

            Assert.Less(CornerParts.CellDoor.x, -CornerParts.CellPierX - 0.15f, "the door's spot is outside, before the door");
            Assert.AreEqual(0f, CornerParts.CellDoor.y, "on the tile");
        }

        [Test]
        public void TheJailStandsLargeOnItsCornerAndThePrisonersInsideIt()
        {
            BoardData data = BoardDataLoader.LoadFromResources();
            var holder = new GameObject("Board Test");
            try
            {
                BoardView board = BoardView.Create(holder.transform, data);
                int jail = data.Board.JailIndex;
                Transform scene = board.JailScene;
                Assert.IsNotNull(scene);
                Assert.AreSame(board.transform.Find("Scene Jail"), scene);
                float side = board.Layout.SquareSize(jail).y;
                Assert.AreEqual(side * BoardView.JailSceneSize, scene.localScale.x, 1e-4f);
                Assert.Greater(BoardView.JailSceneSize, BoardView.SceneSize * 1.3f, "larger than the other drawn scenes: pieces stand inside it");

                // It stays on its tile, and the table's camera sees its door and its barred side.
                Vector3 center = board.transform.TransformPoint(board.Layout.SquareCenter(jail));
                Vector3 right = BoardView.ScreenRight(BoardCameraRig.BaseYaw), up = BoardView.ScreenUp(BoardCameraRig.BaseYaw);
                float reach = float.MinValue;
                foreach (Renderer part in scene.GetComponentsInChildren<Renderer>())
                {
                    Bounds bounds = part.bounds;
                    Assert.LessOrEqual(Mathf.Abs(bounds.min.x - center.x), side / 2f + 1e-3f, $"{part.name} stays on the tile");
                    Assert.LessOrEqual(Mathf.Abs(bounds.max.x - center.x), side / 2f + 1e-3f, $"{part.name} stays on the tile");
                    Assert.LessOrEqual(Mathf.Abs(bounds.min.z - center.z), side / 2f + 1e-3f, $"{part.name} stays on the tile");
                    Assert.LessOrEqual(Mathf.Abs(bounds.max.z - center.z), side / 2f + 1e-3f, $"{part.name} stays on the tile");
                    foreach (float x in new[] { bounds.min.x, bounds.max.x })
                    {
                        foreach (float z in new[] { bounds.min.z, bounds.max.z })
                            reach = Mathf.Max(reach, Vector3.Dot(new Vector3(x, 0f, z) - center, right));
                    }
                }

                Assert.Less(Vector3.Dot(scene.TransformDirection(Vector3.left), up), -0.5f, "the door faces the camera");
                Assert.Less(Vector3.Dot(scene.TransformDirection(Vector3.forward), up), -0.5f, "and so does the barred side");

                // The free pieces on the square wait to the right of the building on the screen, not behind it.
                Assert.Greater(Vector3.Dot(board.PawnSpot(jail) - center, right), reach, "the building does not stand before the waiting pieces' feet");

                // Inside: each place is in the cell, on its floor; the door's spot is on the tile outside it.
                for (int slot = 0; slot < 8; slot++)
                {
                    Vector3 local = scene.InverseTransformPoint(board.CellSpot(slot));
                    AssertNear(CornerParts.PrisonerSpots[slot % CornerParts.PrisonerSpots.Length], local, $"place {slot}");
                }

                AssertNear(CornerParts.CellDoor, scene.InverseTransformPoint(board.CellDoor), "the door's spot");
                AssertNear(scene.position, board.CellMiddle, "the building's middle");

                // The hop from the square's waiting place to the door swings round the building's corner, never through the stone.
                for (int step = 0; step <= 20; step++)
                {
                    Vector3 on = scene.InverseTransformPoint(PawnView.JailHopPoint(board.PawnSpot(jail), board.CellDoor, board.CellMiddle, step / 20f));
                    Assert.IsTrue(Mathf.Abs(on.x) > 0.49f + 0.08f || Mathf.Abs(on.z) > 0.42f + 0.08f, $"step {step}: the hop passes through the building at {on}");
                }

                Assert.LessOrEqual(Mathf.Abs(board.CellDoor.x - center.x), side / 2f, "before the door, still on the tile");
                Assert.LessOrEqual(Mathf.Abs(board.CellDoor.z - center.z), side / 2f, "before the door, still on the tile");

                // A locked-up character fits under the lintel.
                Assert.LessOrEqual(PawnView.JailedSize * PawnView.CharacterHeight, CornerParts.PrisonerRoom * scene.lossyScale.y + 1e-4f);
                Assert.Greater(PawnView.JailedSize * PawnView.CharacterHeight, 0.5f, "and is not too small to make out (about 35 pixels at 1080p)");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void APieceSentToJailIsSmallInsideTheCellUntilItIsFree()
        {
            BoardData data = BoardDataLoader.LoadFromResources();
            var holder = new GameObject("Board Test");
            try
            {
                BoardView board = BoardView.Create(holder.transform, data);
                int jail = data.Board.JailIndex;
                var crowd = new PawnCrowd(4);
                PawnView pawn = PawnView.Create(holder.transform, board, 0, Color.red, crowd, 1, "suit");
                PawnView second = PawnView.Create(holder.transform, board, 1, Color.blue, crowd, 2, "suit");
                Assert.IsFalse(pawn.Jailed);

                // Set straight from the state (a loaded game): inside, small, solid, no ring.
                pawn.PlaceInJail(jail);
                Assert.IsTrue(pawn.Jailed);
                Assert.AreEqual(jail, pawn.Square);
                AssertNear(Vector3.one * pawn.CellSize, pawn.transform.localScale, "small");
                AssertNear(board.CellSpot(0), pawn.transform.position, "in the cell's first place");
                Assert.IsFalse(TurnMarker.Shows(pawn), "no full-size ring round the cell");
                pawn.SetFaded(true);
                Assert.IsFalse(pawn.Faded, "a locked-up piece stays solid while its player waits");

                // A second prisoner takes another place; a free piece on the square waits outside at its own size.
                second.PlaceInJail(jail);
                Assert.Greater(Vector3.Distance(pawn.transform.position, second.transform.position), 0.1f, "two prisoners do not stand in each other");
                second.PlaceOn(jail);
                Assert.IsFalse(second.Jailed);
                AssertNear(Vector3.one, second.transform.localScale, "free at its own size");
                Assert.Greater(Vector3.Distance(second.transform.position, board.JailScene.position), board.Layout.SquareSize(jail).y * 0.3f, "outside the building");
                Assert.IsTrue(TurnMarker.Shows(second));
                second.SetFaded(true);
                Assert.IsTrue(second.Faded, "a free piece fades as before");
                second.SetFaded(false);

                // Going in: it is a prisoner from its first step, which starts where it stood at its own size.
                Vector3 stood = second.transform.position;
                IEnumerator going = second.EnterJail();
                Assert.IsTrue(going.MoveNext());
                Assert.IsTrue(second.Jailed);
                AssertNear(stood, second.transform.position, "the hop starts where it stood");
                AssertNear(Vector3.one, second.transform.localScale, "at its own size");
                Assert.IsFalse(second.EnterJail().MoveNext(), "it does not go in twice");

                // Coming out: still the cell's until it lands; a piece that is free has nowhere to come out of.
                IEnumerator leaving = pawn.LeaveJail();
                Assert.IsTrue(leaving.MoveNext());
                Assert.IsTrue(pawn.Jailed, "small until it is out of the door");
                AssertNear(board.CellSpot(0), pawn.transform.position, "the step out starts at its place");
                pawn.PlaceOn(0);
                Assert.IsFalse(pawn.Jailed);
                AssertNear(Vector3.one, pawn.transform.localScale, "a piece put on a square has its size again");
                Assert.IsFalse(pawn.LeaveJail().MoveNext());
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void TheWayIntoTheCellShrinksLateAndStepsLowThroughTheDoor()
        {
            Assert.AreEqual(1f, PawnView.JailingSize(0f, PawnView.JailedSize), 1e-6f);
            Assert.AreEqual(PawnView.JailedSize, PawnView.JailingSize(1f, PawnView.JailedSize), 1e-6f);
            Assert.Greater(PawnView.JailingSize(0.5f, PawnView.JailedSize), (1f + PawnView.JailedSize) / 2f, "it shrinks late, so the hop reads");
            Assert.AreEqual(1f, PawnView.JailingSize(-1f, PawnView.JailedSize), 1e-6f);
            Assert.AreEqual(PawnView.JailedSize, PawnView.JailingSize(2f, PawnView.JailedSize), 1e-6f);
            Assert.That(PawnView.JailedSize, Is.InRange(0.2f, 0.3f));
            Assert.Greater(PawnView.ShapeJailedSize, PawnView.JailedSize, "a shape is small already");

            // The hop to the door: from the square to the door in one arc, swung out away from the building.
            Vector3 ground = new Vector3(0f, 0.1f, 2f), entry = new Vector3(2f, 0.1f, 0f), building = new Vector3(0f, 0.1f, 0f);
            AssertNear(ground, PawnView.JailHopPoint(ground, entry, building, 0f));
            AssertNear(entry, PawnView.JailHopPoint(ground, entry, building, 1f));
            Vector3 top = PawnView.JailHopPoint(ground, entry, building, 0.5f);
            float length = Vector3.Distance(ground, entry);
            Assert.Greater(top.y, 0.1f + 0.3f, "an arc over the ground");
            Assert.Greater(new Vector2(top.x, top.z).magnitude, new Vector2(1f, 1f).magnitude + 0.4f * PawnView.JailSwing * length, "swung out past the straight way's middle");
            Assert.That(PawnView.JailSwing, Is.InRange(0.4f, 1f));

            Vector3 door = new Vector3(1f, 0.1f, 0f), spot = new Vector3(2f, 0.2f, 1f);
            AssertNear(door, PawnView.CellStep(door, spot, 0f, PawnView.JailedSize));
            AssertNear(spot, PawnView.CellStep(door, spot, 1f, PawnView.JailedSize));
            Vector3 half = PawnView.CellStep(door, spot, 0.5f, PawnView.JailedSize);
            Assert.AreEqual(1.5f, half.x, 1e-4f);
            Assert.That(half.y - 0.15f, Is.InRange(0.01f, 0.2f), "a low step, as small as the piece");
            Assert.That(PawnView.JailHopSeconds + PawnView.JailStepSeconds, Is.InRange(0.5f, 1.2f), "quick: the game waits for it");
        }
    }
}
