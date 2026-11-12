using System.Collections.Generic;
using System.Linq;
using Lazpoly.Core.Events;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Effects;
using Lazpoly.Presentation.Game;
using Lazpoly.Presentation.Models;
using Lazpoly.Presentation.Tabletop;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    /// <summary>Faz 25: the owner's money pieces, the players' money cases and hatches, the bank's safe on the lawn and the payment's way.</summary>
    public class MoneyTests
    {
        [Test]
        public void APaymentIsShownInMoneyStacksAndGoldBars()
        {
            CollectionAssert.IsEmpty(MoneyFlight.Pieces(0));
            CollectionAssert.AreEqual(new[] { MoneyPiece.Stack }, MoneyFlight.Pieces(10000), "even a small payment goes as a stack");
            CollectionAssert.AreEqual(new[] { MoneyPiece.Stack }, MoneyFlight.Pieces(100000));
            CollectionAssert.AreEqual(new[] { MoneyPiece.Stack, MoneyPiece.Stack, MoneyPiece.Stack }, MoneyFlight.Pieces(260000));
            CollectionAssert.AreEqual(new[] { MoneyPiece.Gold }, MoneyFlight.Pieces(500000), "a gold bar for every full 500K");
            CollectionAssert.AreEqual(new[] { MoneyPiece.Stack, MoneyPiece.Stack, MoneyPiece.Gold }, MoneyFlight.Pieces(650000), "the gold last, at the row's end");
            CollectionAssert.AreEqual(new[] { MoneyPiece.Stack, MoneyPiece.Stack, MoneyPiece.Gold, MoneyPiece.Gold }, MoneyFlight.Pieces(5000000));
            Assert.LessOrEqual(MoneyFlight.Pieces(499000).Length, MoneyFlight.MaxPieces);
            foreach (MoneyPiece piece in new[] { MoneyPiece.Stack, MoneyPiece.Gold })
            {
                Assert.GreaterOrEqual(MoneyBundle.Height(piece), 0.25f * MoneyBundle.Width(piece), $"{piece}: thick, not a single note");
            }
        }

        [Test]
        public void EachPlayersMoneyLiesAtTheTablesEdgeFacingTheirCorner()
        {
            CollectionAssert.IsEmpty(MoneyPile.Pieces(0));
            CollectionAssert.AreEqual(new[] { MoneyPiece.Stack }, MoneyPile.Pieces(50000));
            CollectionAssert.AreEqual(new[] { MoneyPiece.Gold, MoneyPiece.Stack, MoneyPiece.Stack }, MoneyPile.Pieces(1500000));
            Assert.AreEqual(MoneyPile.MaxPieces, MoneyPile.Pieces(100000000).Length);
            Assert.AreEqual(Vector3.zero, Vector3.Scale(MoneyPile.SlotOffset(0, 1), new Vector3(1f, 0f, 1f)), "a lone piece in the middle");
            Assert.AreEqual(0f, MoneyPile.SlotOffset(0, 2).z, 1e-5f, "two lie side by side across the middle");
            Assert.AreEqual(0f, MoneyPile.SlotOffset(0, 2).x + MoneyPile.SlotOffset(1, 2).x, 1e-5f);
            Vector3 inside = MoneyPile.Inside;
            for (int count = 1; count <= MoneyPile.MaxPieces; count++)
            {
                for (int i = 0; i < count; i++)
                {
                    Vector3 slot = MoneyPile.SlotOffset(i, count);
                    Assert.AreEqual(MoneyPile.MoneyFloor, slot.y, 1e-5f, "on the lining");
                    Assert.LessOrEqual(Mathf.Abs(slot.x) + MoneyBundle.Length(MoneyPiece.Stack) * MoneyPile.PieceScale / 2f, inside.x / 2f + 1e-4f, "inside the safe's walls along");
                    Assert.LessOrEqual(Mathf.Abs(slot.z) + MoneyBundle.Width(MoneyPiece.Stack) * MoneyPile.PieceScale / 2f, inside.z / 2f + 1e-4f, "and across");
                    for (int other = 0; other < i; other++)
                    {
                        Vector3 gap = slot - MoneyPile.SlotOffset(other, count);
                        Assert.IsTrue(Mathf.Abs(gap.x) >= MoneyBundle.Length(MoneyPiece.Stack) * MoneyPile.PieceScale - 1e-4f
                            || Mathf.Abs(gap.z) >= MoneyBundle.Width(MoneyPiece.Stack) * MoneyPile.PieceScale - 1e-4f, $"{count} pieces: {other} and {i} do not overlap");
                    }
                }
            }

            Assert.Less(MoneyPile.SlotOffset(0, 4).z, MoneyPile.SlotOffset(2, 4).z, "the first two in the front row, towards the camera");
            Assert.Less(MoneyBundle.Height(MoneyPiece.Stack) * MoneyPile.PieceScale + MoneyPile.MoneyFloor, MoneyPile.FootHeight + MoneyPile.BodyHeight, "the money fits under the shut lid");

            float yaw = BoardCameraRig.BaseYaw, half = 8f;
            Vector3 up = BoardView.ScreenUp(yaw), right = BoardView.ScreenRight(yaw);
            for (int seat = 0; seat < 4; seat++)
            {
                Vector3 spot = BoardView.MoneyPileSpot(seat, 4, half, yaw);
                Assert.AreEqual(seat < 2 ? 1f : -1f, Mathf.Sign(Vector3.Dot(spot, up)), $"seat {seat + 1}: towards its card's edge of the screen");
                Assert.AreEqual(seat % 2 == 0 ? -1f : 1f, Mathf.Sign(Vector3.Dot(spot, right)), $"seat {seat + 1}: towards its card's side");
                Assert.Greater(Mathf.Max(Mathf.Abs(spot.x), Mathf.Abs(spot.z)), half, $"seat {seat + 1}: outside the board's edge");
                Vector3 outward = BoardView.MoneyEdgeOutward(seat, yaw);
                Assert.AreEqual(1f, Mathf.Max(Mathf.Abs(outward.x), Mathf.Abs(outward.z)), 1e-4f, $"seat {seat + 1}: across one of the board's edges");
                Quaternion turn = BoardView.MoneyPileTurn(seat, yaw);
                Assert.AreEqual(0f, Vector3.Dot(turn * Vector3.right, outward), 1e-4f, $"seat {seat + 1}: the row runs along the edge");
                Assert.Greater(Vector3.Dot(turn * Vector3.forward, up), 0f, $"seat {seat + 1}: the print reads to the camera");
            }

            Vector3 first = BoardView.MoneyPileSpot(0, 8, half, yaw), fifth = BoardView.MoneyPileSpot(4, 8, half, yaw);
            Assert.Greater(Vector3.Distance(first, fifth), 2f * BoardView.MoneyPileSpread - 0.01f, "two players on one edge lie apart");
            Assert.Less(Vector3.Distance(BoardView.MoneyPileSpot(0, 4, half, yaw), BoardView.MoneyPileSpot(0, 4, half, yaw)), 1e-4f);

            // Two safes sharing a far edge are slid along it towards the board's far corner, clear of the corner cards
            // (their lids reached under the second card of an 8-player game); near edges and unshared edges are not.
            for (int seat = 0; seat < 8; seat++)
            {
                Vector3 slide = BoardView.MoneyEdgeSlide(seat, 8, yaw), outward = BoardView.MoneyEdgeOutward(seat, yaw);
                Assert.AreEqual(Vector3.zero, BoardView.MoneyEdgeSlide(seat % 4, 4, yaw), $"seat {seat % 4 + 1}: alone on its edge, in the middle of it");
                if (Vector3.Dot(outward, up) <= 0f)
                {
                    Assert.AreEqual(Vector3.zero, slide, $"seat {seat + 1}: a near edge stays as it was");
                    continue;
                }

                Assert.AreEqual(BoardView.FarEdgeSlide, slide.magnitude, 1e-4f, $"seat {seat + 1}: slid on a shared far edge");
                Assert.AreEqual(0f, Vector3.Dot(slide, outward), 1e-4f, $"seat {seat + 1}: along the edge");
                Assert.Greater(Vector3.Dot(slide, up), 0f, $"seat {seat + 1}: towards the far corner");
                Assert.AreEqual(seat % 2 == 0 ? 1f : -1f, Mathf.Sign(Vector3.Dot(slide, right)), $"seat {seat + 1}: away from its side of the screen");
            }

            Assert.AreEqual(2f * BoardView.MoneyPileSpread, Vector3.Distance(first, fifth), 1e-3f, "slid together, they keep their distance");

            var holder = new GameObject("Board Test");
            try
            {
                BoardView board = BoardView.Create(holder.transform, BoardDataLoader.LoadFromResources());
                board.CreateMoneyPiles(new[] { Color.red, Color.blue, Color.green });
                Assert.AreEqual(3, board.MoneyPiles.Count);
                board.ShowSeatMoney(1, 1500000);
                CollectionAssert.AreEqual(MoneyPile.Pieces(1500000), board.MoneyPiles[1].Shown);
                Assert.AreSame(board.MoneyPiles[1], board.MoneyPileOf(1));
                Assert.IsNull(board.MoneyPileOf(3), "no case for a seat nobody sits in");
                Assert.AreEqual(3, board.MoneyShelves.Count, "every tray stands on a shelf");
                for (int seat = 0; seat < 3; seat++)
                {
                    Bounds shelf = board.MoneyShelves[seat].Find("Meadow").GetComponent<Renderer>().bounds;
                    Transform tray = board.MoneyPiles[seat].Root;
                    Assert.AreEqual(tray.position.y, shelf.max.y, 1e-3f, $"seat {seat + 1}: the tray lies on its shelf");
                    Assert.Less(shelf.max.y, 0f, $"seat {seat + 1}: the shelf a little under the plate's top");
                    // The shelf a little bigger than the safe all round (the owner's wish, 2026-11-11), the safe on its feet on it, shut.
                    Vector3 outward = board.transform.TransformDirection(BoardView.MoneyEdgeOutward(seat, yaw));
                    Vector3 along = Vector3.Cross(Vector3.up, outward);
                    float Reach(Bounds box, Vector3 way) => Vector3.Dot(box.center, way) + Vector3.Dot(box.extents, new Vector3(Mathf.Abs(way.x), Mathf.Abs(way.y), Mathf.Abs(way.z)));
                    MoneyPile pile = board.MoneyPiles[seat];
                    Bounds body = pile.Safe.Find("Body").GetComponent<Renderer>().bounds;
                    Assert.Greater(BoardView.ShelfMargin, 0.05f, "the shelf shows round the safe");
                    foreach (Vector3 way in new[] { outward, -outward, along, -along })
                        Assert.AreEqual(Reach(shelf, way), Reach(body, way) + BoardView.ShelfMargin, 2e-3f, $"seat {seat + 1}: the shelf a margin past the safe");
                    Bounds feet = pile.Safe.Find("Feet").GetComponent<Renderer>().bounds;
                    Assert.AreEqual(shelf.max.y, feet.min.y, 1e-3f, $"seat {seat + 1}: the safe stands on its feet on it");
                    Assert.AreEqual(feet.max.y, body.min.y, 1e-3f, $"seat {seat + 1}: the body on its feet");
                    Assert.AreEqual(0f, pile.Openness, 1e-4f, $"seat {seat + 1}: shut");

                    // The lid is hinged at the back, away from the camera, and opens up and back so it never hides the money.
                    Vector3 screenUp = BoardView.ScreenUp(yaw);
                    Vector3 hinge = board.transform.InverseTransformPoint(pile.Lid.position) - board.transform.InverseTransformPoint(tray.position);
                    Vector3 back = board.transform.InverseTransformDirection(tray.forward);
                    Assert.Greater(Vector3.Dot(back, screenUp), 0.5f, $"seat {seat + 1}: its back away from the camera, its dial towards it");
                    Assert.AreEqual(MoneyPile.SafeSize.z / 2f, Vector3.Dot(hinge, back), 1e-3f, $"seat {seat + 1}: hinged on the back edge");
                    Bounds shutLid = pile.Lid.Find("Lid top").GetComponent<Renderer>().bounds;
                    pile.ShowLid(1f);
                    Bounds openLid = pile.Lid.Find("Lid top").GetComponent<Renderer>().bounds;
                    Assert.Greater(openLid.max.y, shutLid.max.y + MoneyPile.SafeSize.z * 0.8f, $"seat {seat + 1}: open, it stands up");
                    Assert.Greater(Vector3.Dot(board.transform.InverseTransformPoint(openLid.center) - board.transform.InverseTransformPoint(body.center), screenUp), 0f,
                        $"seat {seat + 1}: behind the safe");
                    pile.ShowLid(0f);

                    Vector3 inner = board.transform.InverseTransformPoint(shelf.center) - BoardView.MoneyEdgeOutward(seat, yaw) * (BoardView.ShelfSize.z / 2f);
                    Assert.AreEqual(board.HalfExtent + BoardView.ShelfGapAt(seat, yaw), Vector3.Dot(inner, BoardView.MoneyEdgeOutward(seat, yaw)), 1e-3f, $"seat {seat + 1}: a small gap between the plate and the shelf");
                    Assert.GreaterOrEqual(BoardView.ShelfGapAt(seat, yaw), Mathf.Min(BoardView.ShelfGap, BoardView.NearShelfGap), "the gap shows");
                    if (Vector3.Dot(BoardView.MoneyEdgeOutward(seat, yaw), BoardView.ScreenUp(yaw)) > 0f)
                        Assert.AreEqual(1.3f, BoardView.ShelfGapAt(seat, yaw), 1e-5f, "the safes at the top stand further off the table (the owner's wish, 2026-11-11, asked twice)");
                }

                board.ShowSeatMoney(1, 0);
                CollectionAssert.IsEmpty(board.MoneyPiles[1].Shown, "a player with nothing has an empty safe");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void EachPieceIsTheOwnersModelSharingOneMaterial()
        {
            var root = new GameObject("Table");
            try
            {
                foreach (MoneyPiece kind in new[] { MoneyPiece.Stack, MoneyPiece.Gold })
                {
                    Assert.IsTrue(MoneyBundle.HasModel(kind), $"{kind}: run Tools/Art/make_corner_models.py (see the README)");
                    GameObject piece = MoneyBundle.Create(kind, kind.ToString(), root.transform);
                    Renderer body = piece.GetComponentInChildren<Renderer>();
                    Assert.IsNotNull(body.sharedMaterial.mainTexture, $"{kind}: the model's colour map");
                    Bounds bounds = body.bounds;
                    Assert.That(bounds.size.x, Is.EqualTo(MoneyBundle.Length(kind)).Within(0.05f), $"{kind}: its length");
                    Assert.That(bounds.size.y, Is.EqualTo(MoneyBundle.Height(kind)).Within(0.05f), $"{kind}: its thickness");
                    Assert.That(bounds.size.z, Is.EqualTo(MoneyBundle.Width(kind)).Within(0.05f), $"{kind}: its width");
                    Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.01f), $"{kind} lies on y = 0");
                    foreach (Renderer part in piece.GetComponentsInChildren<Renderer>())
                    {
                        Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, part.shadowCastingMode, part.name);
                    }

                    Material shared = MoneyBundle.Create(kind, "Other", root.transform).GetComponentInChildren<Renderer>().sharedMaterial;
                    Assert.AreSame(body.sharedMaterial, shared, $"{kind}: pieces share their material");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PaidMoneyGoesDownThePayersHatchAndUpTheReceiversOneByOne()
        {
            Vector3 from = new Vector3(-4f, 0f, 0f), overA = new Vector3(-6f, 0.6f, 6f), underA = new Vector3(-6f, -0.4f, 6f);
            Vector3 underB = new Vector3(6f, -0.4f, 6f), overB = new Vector3(6f, 0.6f, 6f), to = new Vector3(4f, 0f, 0f);
            const int count = 3;
            for (int i = 0; i < count; i++)
            {
                Assert.AreEqual(from, MoneyFlight.PositionAt(0f, i, count, true, from, overA, underA, underB, overB, to), "it leaves from its place in the case");
                Assert.That(Vector3.Distance(MoneyFlight.PositionAt(MoneyFlight.SinkAt(i), i, count, true, from, overA, underA, underB, overB, to), overA), Is.LessThan(1e-3f), "over the payer's hatch");
                Assert.That(Vector3.Distance(MoneyFlight.PositionAt(MoneyFlight.GoneAt(i) - 1e-4f, i, count, true, from, overA, underA, underB, overB, to), underA), Is.LessThan(1e-2f), "down into it");
                Assert.IsTrue(MoneyFlight.Hidden((MoneyFlight.GoneAt(i) + MoneyFlight.RiseAt(i, count)) / 2f, i, count, true), "out of sight under the table");
                Assert.IsFalse(MoneyFlight.Hidden(MoneyFlight.SinkAt(i), i, count, true));
                Assert.That(Vector3.Distance(MoneyFlight.PositionAt(MoneyFlight.UpAt(i, count), i, count, true, from, overA, underA, underB, overB, to), overB), Is.LessThan(1e-3f), "up out of the other hatch");
                Assert.AreEqual(to, MoneyFlight.PositionAt(MoneyFlight.LandAt(i, count), i, count, true, from, overA, underA, underB, overB, to), "into the other case");
                Assert.IsTrue(MoneyFlight.Hidden(MoneyFlight.Seconds(count, false), i, count, false), "money for the bank stays under the table");
                if (i > 0) Assert.Greater(MoneyFlight.SinkAt(i), MoneyFlight.SinkAt(i - 1), "one after another");
                if (i > 0) Assert.Greater(MoneyFlight.RiseAt(i, count), MoneyFlight.RiseAt(i - 1, count));

                // Out of the payer's safe, which is open while it rises, and into the other one, open while it comes down.
                Assert.That(Vector3.Distance(MoneyFlight.PositionAt(MoneyFlight.OutAt(i), i, count, true, from, overA, underA, underB, overB, to), from + Vector3.up * MoneyFlight.Lift), Is.LessThan(1e-3f), "straight up out of the safe");
                Assert.That(Vector3.Distance(MoneyFlight.PositionAt(MoneyFlight.OverAt(i, count), i, count, true, from, overA, underA, underB, overB, to), to + Vector3.up * MoneyFlight.Lift), Is.LessThan(1e-3f), "over the other safe");
                Assert.Greater(MoneyFlight.Lift + MoneyPile.MoneyFloor, MoneyPile.SafeSize.y, "clear of the shut lid's height");
                for (float t = MoneyFlight.LeaveAt(i); t <= MoneyFlight.OutAt(i); t += 0.05f)
                    Assert.AreEqual(1f, MoneyPile.OpennessAt(t, MoneyFlight.PayerSafeOpensAt, MoneyFlight.PayerSafeShutsAt(count)), 1e-4f, $"piece {i} rises out of an open safe");
                for (float t = MoneyFlight.OverAt(i, count); t <= MoneyFlight.LandAt(i, count); t += 0.05f)
                    Assert.AreEqual(1f, MoneyPile.OpennessAt(t, MoneyFlight.ReceiverSafeOpensAt(count), MoneyFlight.ReceiverSafeShutsAt(count)), 1e-4f, $"piece {i} goes down into an open safe");

                // The payer's hatch is open while each piece goes down, the other one while each comes up.
                for (float t = MoneyFlight.SinkAt(i); t <= MoneyFlight.GoneAt(i); t += 0.05f)
                    Assert.AreEqual(1f, MoneyHatch.OpennessAt(t, MoneyFlight.PayerOpensAt, MoneyFlight.PayerShutsAt(count)), 1e-4f, $"piece {i} goes down an open hatch");
                for (float t = MoneyFlight.RiseAt(i, count); t <= MoneyFlight.UpAt(i, count); t += 0.05f)
                    Assert.AreEqual(1f, MoneyHatch.OpennessAt(t, MoneyFlight.ReceiverOpensAt(count), MoneyFlight.ReceiverShutsAt(count)), 1e-4f, $"piece {i} comes up an open hatch");
            }

            Assert.AreEqual(0f, MoneyHatch.OpennessAt(0f, MoneyFlight.PayerOpensAt, MoneyFlight.PayerShutsAt(count)), "shut at first");
            Assert.AreEqual(0f, MoneyHatch.OpennessAt(MoneyFlight.Seconds(count, false), MoneyFlight.PayerOpensAt, MoneyFlight.PayerShutsAt(count)), 1e-4f, "and shut again at the end");
            Assert.AreEqual(0f, MoneyHatch.OpennessAt(MoneyFlight.RiseAt(0, count) - MoneyHatch.SwingSeconds, MoneyFlight.ReceiverOpensAt(count), MoneyFlight.ReceiverShutsAt(count)), 1e-4f,
                "the other hatch opens only after the payer's has shut");
            Assert.Greater(MoneyFlight.ReceiverOpensAt(count), MoneyFlight.PayerShutsAt(count));
            Assert.AreEqual(0f, MoneyPile.OpennessAt(0f, MoneyFlight.PayerSafeOpensAt, MoneyFlight.PayerSafeShutsAt(count)), "the payer's safe shut at first");
            Assert.AreEqual(0f, MoneyPile.OpennessAt(MoneyFlight.Seconds(count, false), MoneyFlight.PayerSafeOpensAt, MoneyFlight.PayerSafeShutsAt(count)), 1e-4f, "and shut again at the end");
            Assert.AreEqual(0f, MoneyPile.OpennessAt(MoneyFlight.Seconds(count, true), MoneyFlight.ReceiverSafeOpensAt(count), MoneyFlight.ReceiverSafeShutsAt(count)), 1e-4f, "the other safe shut when it is over");
            Assert.AreEqual(0f, MoneyPile.OpennessAt(MoneyFlight.UpAt(0, count), MoneyFlight.ReceiverSafeOpensAt(count), MoneyFlight.ReceiverSafeShutsAt(count)), 1e-4f, "and shut until the money comes near");
            Assert.LessOrEqual(MoneyFlight.Seconds(MoneyFlight.MaxPieces, true), 4.5f, "a payment never drags");

            var holder = new GameObject("Board Test");
            try
            {
                BoardView board = BoardView.Create(holder.transform, BoardDataLoader.LoadFromResources());
                board.CreateMoneyPiles(new[] { Color.red, Color.blue, Color.green });
                float seconds = MoneyFlight.Fly(holder.transform, 260000, board.MoneyPileOf(0), board.HatchOf(0), board.MoneyPileOf(1), board.HatchOf(1), 2f);
                Assert.AreEqual(MoneyFlight.Seconds(3, true) / 2f, seconds, 1e-4f, "the animation speed setting shortens it");
                MoneyFlight[] pieces = holder.GetComponentsInChildren<MoneyFlight>();
                Assert.AreEqual(3, pieces.Length, "three stacks for 260K");
                for (int i = 0; i < pieces.Length; i++)
                {
                    Transform piece = pieces[i].transform;
                    Assert.That(piece.lossyScale.x, Is.EqualTo(MoneyPile.PieceScale).Within(1e-4f), "its size in the case, never bigger");
                    Assert.That(Quaternion.Angle(piece.rotation, board.MoneyPileOf(0).Root.rotation), Is.LessThan(0.01f), "turned as it lies in the case");
                    Vector3 slot = board.MoneyPileOf(0).Root.TransformPoint(MoneyPile.SlotOffset(i, 3));
                    Assert.That(Vector3.Distance(piece.position, slot), Is.LessThan(1e-3f), "leaving from its place in the case");
                }

                Assert.AreEqual(0f, MoneyFlight.Fly(holder.transform, 0, board.MoneyPileOf(0), board.HatchOf(0), null, null, 1f), "nothing to pay, nothing flies");
                Assert.Greater(MoneyFlight.Fly(holder.transform, 100000, board.MoneyPileOf(2), board.HatchOf(2), null, null, 1f), 0f, "to the bank");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void TheStartingMoneyComesUpThroughEachPlayersHatchIntoTheirSafe()
        {
            const int count = 4;
            float start = MoneyFlight.DealStartsAt(count);
            for (int i = 0; i < count; i++)
            {
                Assert.IsTrue(MoneyFlight.Hidden(start, i, count, true), $"piece {i}: under the table when the deal starts");
                Assert.Greater(MoneyFlight.RiseAt(i, count), start, $"piece {i}: comes up after the hatch opens");
            }

            Assert.AreEqual(0f, MoneyHatch.OpennessAt(0f, MoneyFlight.ReceiverOpensAt(count) - start, MoneyFlight.ReceiverShutsAt(count) - start), 1e-4f, "the hatch starts shut");
            Assert.AreEqual(1f, MoneyHatch.OpennessAt(MoneyFlight.RiseAt(0, count) - start, MoneyFlight.ReceiverOpensAt(count) - start, MoneyFlight.ReceiverShutsAt(count) - start), 1e-4f,
                "and is open as the first piece comes up");
            Assert.Less(MoneyFlight.Seconds(count, true) - start + 7 * GameSceneController.DealStagger, 5f, "eight players' money is handed out in a few seconds");

            var holder = new GameObject("Board Test");
            try
            {
                BoardView board = BoardView.Create(holder.transform, BoardDataLoader.LoadFromResources());
                board.CreateMoneyPiles(new[] { Color.red, Color.blue });
                float seconds = MoneyFlight.Deal(holder.transform, 1500000, board.MoneyPileOf(1), board.HatchOf(1), 1f);
                Assert.AreEqual(MoneyFlight.Seconds(count, true) - start, seconds, 1e-4f);
                MoneyFlight[] pieces = holder.GetComponentsInChildren<MoneyFlight>();
                Assert.AreEqual(MoneyFlight.Pieces(1500000).Length, pieces.Length);
                foreach (MoneyFlight piece in pieces)
                {
                    Assert.AreEqual(Vector3.zero, piece.transform.localScale, "out of sight under the hatch at first");
                    Assert.That(Quaternion.Angle(piece.transform.rotation, board.MoneyPileOf(1).Root.rotation), Is.LessThan(0.01f), "turned as it will lie in the case");
                    Assert.Less(piece.transform.position.y, board.HatchOf(1).Mouth.y, "under the hatch");
                }

                Assert.AreEqual(0f, MoneyFlight.Deal(holder.transform, 0, board.MoneyPileOf(0), board.HatchOf(0), 1f), "nothing to hand out");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void TheSumIsWrittenInFrontOfEachHatchItPasses()
        {
            // The owner's wish (2026-11-12): "-50K" where money goes down, "+100K" where it comes up, in the game's short form.
            Assert.AreEqual("-50K", HatchAmount.Text(-50000));
            Assert.AreEqual("+100K", HatchAmount.Text(100000));
            Assert.AreEqual("+" + Texts.Lang.M(1500000), HatchAmount.Text(1500000));
            Assert.AreEqual(Texts.Lang.M(-1250000), HatchAmount.Text(-1250000));
            Assert.AreEqual(HatchAmount.Up, HatchAmount.Ink(1));
            Assert.AreEqual(HatchAmount.Down, HatchAmount.Ink(-1));
            Assert.Greater(HatchAmount.Up.g, HatchAmount.Up.r, "green for money that comes");
            Assert.Greater(HatchAmount.Down.r, HatchAmount.Down.g, "red for money that goes");
            foreach (Color ink in new[] { HatchAmount.Up, HatchAmount.Down })
                Assert.Greater(ink.grayscale, LawnSkin.Light.grayscale + 0.2f, "light enough to read on the meadow");

            // It appears as the lid starts to open, stays while the hatch is open and fades once it has shut.
            const float from = 1f, shuts = 2f;
            float until = HatchAmount.ShutAt(from, shuts);
            Assert.AreEqual(shuts + MoneyHatch.SwingSeconds, until, 1e-5f);
            Assert.AreEqual(from + 2f * MoneyHatch.SwingSeconds, HatchAmount.ShutAt(from, from + 0.01f), 1e-5f, "as the hatch itself: never shut before it is open");
            Assert.AreEqual(0f, HatchAmount.AlphaAt(from - 0.01f, from, until), "nothing before the lid moves");
            Assert.AreEqual(1f, HatchAmount.AlphaAt(from, from, until), 1e-5f);
            for (float t = from; t <= until; t += 0.05f)
                Assert.AreEqual(1f, HatchAmount.AlphaAt(t, from, until), 1e-5f, "whole while the hatch is open");

            Assert.That(HatchAmount.AlphaAt(HatchAmount.GoneAt(until) - HatchAmount.FadeSeconds / 2f, from, until), Is.InRange(0.4f, 0.6f), "fading");
            Assert.AreEqual(0f, HatchAmount.AlphaAt(HatchAmount.GoneAt(until), from, until), 1e-5f);
            Assert.Greater(HatchAmount.Linger, HatchAmount.FadeSeconds, "read a moment after the lid has shut");
            Assert.Less(HatchAmount.Linger, 1f, "gone before the next payment's hatch opens");
            Assert.AreEqual(0.6f, HatchAmount.ScaleAt(from, from), 1e-5f);
            Assert.AreEqual(1f, HatchAmount.ScaleAt(from + HatchAmount.PopSeconds, from), 1e-5f);
            Assert.AreEqual(1f, HatchAmount.ScaleAt(until, from), 1e-5f);

            // In front of the hatch: straight under it on the screen, clear of the collar's near edge, standing over the grass.
            Quaternion view = Quaternion.Euler(BoardCameraRig.Pitch, BoardCameraRig.BaseYaw, 0f);
            Vector3 up = view * Vector3.up, right = view * Vector3.right, forward = view * Vector3.forward;
            var mouth = new Vector3(1.5f, 0.2f, -2f);
            Vector3 offset = HatchAmount.Spot(mouth, up, forward) - mouth;
            Assert.AreEqual(-HatchAmount.Below, Vector3.Dot(offset, up), 1e-4f, "under the hatch on the screen");
            Assert.AreEqual(0f, Vector3.Dot(offset, right), 1e-4f, "neither left nor right of it");
            Assert.AreEqual(HatchAmount.Height, offset.y, 1e-4f, "over the grass");
            float letters = HatchAmount.CharacterSize * 96f * 0.1f * 0.75f; // a capital's height
            Assert.Greater(HatchAmount.Below - letters / 2f, MoneyHatch.OuterRadius * Mathf.Sin(BoardCameraRig.Pitch * Mathf.Deg2Rad) + 0.05f, "the letters do not lie on the collar");
            Assert.Greater(HatchAmount.Height, letters / 2f, "nor in the grass");
            Assert.That(letters, Is.InRange(0.3f, 0.45f), "about 20 pixels at 1080p in the whole view: read at a glance, smaller than the hatch");
            Assert.Less(Vector3.Distance(2f * HatchAmount.Spot(Vector3.zero, up, forward), HatchAmount.Spot(Vector3.zero, up, forward, 2f)), 1e-4f, "it follows the table's size");

            // A hatch to one side of the screen's middle writes outwards from its inner edge, so a long sum never reaches
            // further towards the middle (and the roll button there) than the hatch itself.
            Assert.AreEqual(1, HatchAmount.Side(mouth + right * 2f, mouth, right));
            Assert.AreEqual(-1, HatchAmount.Side(mouth - right * 2f + up, mouth, right));
            Assert.AreEqual(0, HatchAmount.Side(mouth + up * 3f + right * 0.1f, mouth, right));
            Assert.AreEqual(TextAnchor.MiddleLeft, HatchAmount.Anchor(1), "to the right it starts at the hatch's left edge");
            Assert.AreEqual(TextAnchor.MiddleRight, HatchAmount.Anchor(-1));
            Assert.AreEqual(TextAnchor.MiddleCenter, HatchAmount.Anchor(0));
            Vector3 toRight = HatchAmount.Spot(mouth, up, forward, 1f, 1) - mouth, toLeft = HatchAmount.Spot(mouth, up, forward, 1f, -1) - mouth;
            Assert.AreEqual(-MoneyHatch.OuterRadius, Vector3.Dot(toRight, right), 1e-4f, "under the hatch's inner edge");
            Assert.AreEqual(MoneyHatch.OuterRadius, Vector3.Dot(toLeft, right), 1e-4f);
            Assert.AreEqual(-HatchAmount.Below, Vector3.Dot(toRight, up), 1e-4f, "as far under the hatch as ever");
            Assert.AreEqual(HatchAmount.Height, toLeft.y, 1e-4f);
            Assert.That(HatchAmount.Edge, Is.InRange(0.02f, 0.04f), "a dark edge thick enough to hold pale ink on the grass, thin enough not to fill the letters");

            var holder = new GameObject("Board Test");
            try
            {
                BoardView board = BoardView.Create(holder.transform, BoardDataLoader.LoadFromResources());
                board.CreateMoneyPiles(new[] { Color.red, Color.blue, Color.green });
                Assert.IsNull(board.HatchOf(0).GetComponentInChildren<HatchAmount>(), "nothing written while no money moves");
                Assert.IsNull(HatchAmount.Show(board.HatchOf(0), 0, 0f, 1f, 1f));
                Assert.IsNull(HatchAmount.Show(null, 50000, 0f, 1f, 1f));

                // A payment between players: the sum with a minus at the payer's hatch, with a plus at the receiver's.
                MoneyFlight.Fly(holder.transform, 260000, board.MoneyPileOf(0), board.HatchOf(0), board.MoneyPileOf(1), board.HatchOf(1), 1f);
                HatchAmount paid = board.HatchOf(0).GetComponentInChildren<HatchAmount>();
                HatchAmount got = board.HatchOf(1).GetComponentInChildren<HatchAmount>();
                Assert.IsNotNull(paid);
                Assert.IsNotNull(got);
                Assert.AreEqual(-260000, paid.Amount);
                Assert.AreEqual("-260K", paid.Written);
                Assert.AreEqual(260000, got.Amount);
                Assert.AreEqual("+260K", got.Written);
                Assert.AreEqual(0f, paid.Alpha, "not yet: the money is still on its way to the hatch");
                Assert.AreEqual(0f, got.Alpha);
                Assert.IsNull(board.HatchOf(2).GetComponentInChildren<HatchAmount>(), "nothing at a hatch the money does not pass");
                TextMesh[] lines = paid.GetComponentsInChildren<TextMesh>();
                Assert.AreEqual(1 + HatchAmount.EdgeCopies, lines.Length, "the line and its dark edge");
                foreach (TextMesh line in lines) Assert.AreEqual("-260K", line.text);

                // To the bank: only the payer's. A new sum at a hatch takes the place of the one still written there.
                MoneyFlight.Fly(holder.transform, 100000, board.MoneyPileOf(2), board.HatchOf(2), null, null, 1f);
                Assert.AreEqual("-100K", board.HatchOf(2).GetComponentInChildren<HatchAmount>().Written);
                MoneyFlight.Fly(holder.transform, 50000, board.MoneyPileOf(0), board.HatchOf(0), null, null, 1f);
                HatchAmount[] atFirst = board.HatchOf(0).GetComponentsInChildren<HatchAmount>();
                Assert.AreEqual(1, atFirst.Length);
                Assert.AreEqual("-50K", atFirst[0].Written);
                Assert.AreEqual("+260K", board.HatchOf(1).GetComponentInChildren<HatchAmount>().Written, "the other hatch keeps its own");

                // The starting money: written as the hatch opens for it, which is at once.
                MoneyFlight.Deal(holder.transform, 1500000, board.MoneyPileOf(1), board.HatchOf(1), 1f);
                HatchAmount dealt = board.HatchOf(1).GetComponentInChildren<HatchAmount>();
                Assert.AreEqual(1, board.HatchOf(1).GetComponentsInChildren<HatchAmount>().Length);
                Assert.AreEqual(1500000, dealt.Amount);
                Assert.AreEqual(1f, dealt.Alpha, 1e-5f);
                dealt.Remove();
                Assert.IsNull(board.HatchOf(1).GetComponentInChildren<HatchAmount>());
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void EachSeatsHatchLiesOnTheLawnOnItsSide()
        {
            var holder = new GameObject("Board Test");
            try
            {
                BoardView board = BoardView.Create(holder.transform, BoardDataLoader.LoadFromResources());
                float yaw = BoardCameraRig.BaseYaw;
                float inner = board.Layout.HalfSize * 2f - board.Layout.Depth * 1.05f, lawnHalf = inner * BoardView.LawnShare / 2f;
                var colors = new Color[8];
                for (int i = 0; i < colors.Length; i++) colors[i] = Color.HSVToRGB(i / 8f, 0.7f, 0.9f);
                board.CreateMoneyPiles(colors);
                Assert.AreEqual(8, board.MoneyHatches.Count, "a hatch for every seat");
                Vector3 size = MoneyHatch.Size;
                // The grass's top is where the hatches stand: it must not rise over their holes, or the money sinks into green.
                float grassTop = float.MinValue;
                foreach (MeshRenderer part in holder.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (part.name == "Grass") grassTop = part.bounds.max.y;
                }

                Assert.AreEqual(BoardView.LawnHeight, grassTop, 1e-4f, "the grass ends at the lawn's height");
                for (int seat = 0; seat < 8; seat++)
                {
                    MoneyHatch hatch = board.HatchOf(seat);
                    Assert.Greater(hatch.transform.Find("Hole").GetComponent<MeshRenderer>().bounds.min.y, grassTop, $"seat {seat + 1}: the hole shows over the grass");
                    Vector3 spot = hatch.transform.localPosition;
                    Assert.AreEqual(BoardView.LawnHeight, spot.y, 1e-4f, $"seat {seat + 1}: on the grass");
                    Vector3 outward = BoardView.MoneyEdgeOutward(seat, yaw);
                    Vector3 along = Vector3.Cross(Vector3.up, outward);
                    Assert.LessOrEqual(Vector3.Dot(spot, outward) + size.z / 2f, lawnHalf - 0.15f, $"seat {seat + 1}: inside the lawn's edge");
                    Assert.LessOrEqual(Mathf.Abs(Vector3.Dot(spot, along)) + size.x / 2f, lawnHalf - 0.15f, $"seat {seat + 1}: inside the lawn");
                    Assert.Greater(Vector3.Dot(spot, outward), lawnHalf * 0.75f, $"seat {seat + 1}: on its own side of the lawn");
                    // The lawn's edge wanders: the whole frame still lies on the flat grass, in from the bank.
                    foreach (float side in new[] { -0.5f, 0.5f })
                    foreach (float end in new[] { -0.5f, 0.5f })
                    {
                        Vector3 corner = spot + along * (size.x * side) + outward * (size.z * end);
                        Assert.IsTrue(LawnShape.Covers(new Vector2(corner.x, corner.z) / lawnHalf, LawnShape.Bevel), $"seat {seat + 1}: a corner of its frame is off the grass");
                    }
                    Vector3 pile = BoardView.MoneyPileSpot(seat, 8, board.HalfExtent, yaw);
                    Assert.AreEqual(Mathf.Sign(Vector3.Dot(pile, along)), Mathf.Sign(Vector3.Dot(spot, along)), $"seat {seat + 1}: beside its case's way");
                    Assert.That(Quaternion.Angle(hatch.transform.localRotation, BoardView.MoneyPileTurn(seat, yaw)), Is.LessThan(0.01f), $"seat {seat + 1}: turned as its case, so the money fits");

                    for (int other = 0; other < seat; other++)
                    {
                        Vector3 gap = spot - board.HatchOf(other).transform.localPosition;
                        Assert.Greater(gap.magnitude, size.x, $"seats {other + 1} and {seat + 1}: their hatches lie apart");
                    }
                }

                MoneyHatch first = board.HatchOf(0);
                Assert.Greater(MoneyHatch.HoleLength, MoneyBundle.Length(MoneyPiece.Stack) * MoneyPile.PieceScale, "a stack fits the hole lengthwise");
                Assert.Greater(MoneyHatch.HoleWidth, MoneyBundle.Width(MoneyPiece.Stack) * MoneyPile.PieceScale, "and across");
                first.Show(0f);
                Bounds shut = first.Lid.GetComponentInChildren<Renderer>().bounds;
                Assert.Less(shut.max.y, first.Mouth.y + MoneyHatch.LidThickness + 0.01f, "shut, the lid lies flat over the hole");
                first.Show(1f);
                Bounds open = first.Lid.GetComponentInChildren<Renderer>().bounds;
                Assert.Greater(open.max.y, first.Mouth.y + MoneyHatch.HoleWidth * 0.8f, "open, it stands up");
                Assert.Greater(Vector3.Dot(open.center - first.Mouth, BoardView.ScreenUp(yaw)), 0f, "behind the hole, away from the camera");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void APaymentGoesToThePlayerWhoGainsItOrToTheBank()
        {
            var rent = new MoneyChangedEvent(1, -120, MoneyReason.Rent);
            Assert.AreEqual(2, GameSceneController.PaymentReceiver(rent, new GameEvent[] { new MoneyChangedEvent(2, 120, MoneyReason.Rent) }));
            Assert.AreEqual(GameState.NoOwner, GameSceneController.PaymentReceiver(rent, new GameEvent[0]));
            var tax = new MoneyChangedEvent(1, -100, MoneyReason.Tax);
            Assert.AreEqual(GameState.NoOwner, GameSceneController.PaymentReceiver(tax, new GameEvent[] { new TurnStartedEvent(2), new MoneyChangedEvent(2, 100, MoneyReason.Tax) }));
            Assert.AreEqual(GameState.NoOwner, GameSceneController.PaymentReceiver(new MoneyChangedEvent(1, 200, MoneyReason.PassedStart), new GameEvent[0]));

            var house = new MoneyChangedEvent(1, -50, MoneyReason.Building);
            var second = new MoneyChangedEvent(1, -50, MoneyReason.Building);
            var later = new MoneyChangedEvent(1, -50, MoneyReason.Building);
            List<MoneyChangedEvent> along = GameSceneController.PaidAlong(house, new GameEvent[]
            {
                new BuildingLevelChangedEvent(3, 1), second, new BuildingLevelChangedEvent(3, 2), new PlayerMovedEvent(1, 3, 5, false), later,
            });
            CollectionAssert.AreEqual(new[] { second }, along, "houses bought together fly once; a later one flies on its own");
            Assert.IsEmpty(GameSceneController.PaidAlong(rent, new GameEvent[] { new MoneyChangedEvent(1, -120, MoneyReason.Rent) }));

            var purchase = new MoneyChangedEvent(1, -200, MoneyReason.PropertyPurchase);
            var houses = new MoneyChangedEvent(1, -150, MoneyReason.Building);
            CollectionAssert.AreEqual(new[] { houses }, GameSceneController.PaidAlong(purchase, new GameEvent[]
            {
                new PropertyOwnerChangedEvent(3, GameState.NoOwner, 1), houses, new BuildingLevelChangedEvent(3, 3),
            }), "a town bought and built on the same card is paid in one flight");

            var bought = new PropertyOwnerChangedEvent(3, GameState.NoOwner, 1);
            Assert.AreEqual(4, GameSceneController.BuiltWithPurchase(bought, new GameEvent[] { new BuildingLevelChangedEvent(3, 4), new PhaseChangedEvent(GamePhase.TurnEnd) }),
                "bought with four houses: they go up at once and the plot never shows");
            Assert.AreEqual(0, GameSceneController.BuiltWithPurchase(bought, new GameEvent[] { new PhaseChangedEvent(GamePhase.TurnEnd) }), "bought bare: the plot shows");
            Assert.AreEqual(0, GameSceneController.BuiltWithPurchase(bought, new GameEvent[] { new BuildingLevelChangedEvent(5, 2) }), "another town's houses");
            Assert.AreEqual(0, GameSceneController.BuiltWithPurchase(new PropertyOwnerChangedEvent(3, 2, 1), new GameEvent[] { new BuildingLevelChangedEvent(3, 4) }), "a trade is not a purchase");
        }

        [Test]
        public void EachHatchIsAPlainRoundIronCoverWithGoldDeepInside()
        {
            // The owner's wish (2026-11-12): plainer and more elegant than the stone and brass hatch; "it could even be a manhole cover".
            var holder = new GameObject("Board Test");
            try
            {
                BoardView board = BoardView.Create(holder.transform, BoardDataLoader.LoadFromResources());
                board.CreateMoneyPiles(new[] { Color.red, Color.blue });
                MoneyHatch hatch = board.HatchOf(0);
                Bounds Cut(Transform part) => part.GetComponent<MeshFilter>().sharedMesh.bounds;

                // Nothing but dark iron: a collar, the hole, and the lid's plate, its cast relief and its underside.
                var parts = new List<string>();
                foreach (Transform part in hatch.transform) parts.Add(part.name);
                CollectionAssert.AreEquivalent(new[] { "Collar", "Hole", "Lid" }, parts, "no stone, brass or chrome any more");
                parts.Clear();
                foreach (Transform part in hatch.Lid) parts.Add(part.name);
                CollectionAssert.AreEqual(new[] { "Lid plate", "Lid relief", "Lid underside" }, parts, "the lid's own plate comes first: its colour is the lid's");
                foreach (Renderer part in hatch.GetComponentsInChildren<Renderer>())
                {
                    if (part.name == "Hole") continue;
                    Color.RGBToHSV(part.sharedMaterial.color, out _, out float saturation, out float value);
                    Assert.Less(saturation, 0.2f, $"{part.name}: grey iron, no colour");
                    Assert.Less(value, 0.3f, $"{part.name}: dark");
                }

                // Round, and low in the grass.
                Transform collar = hatch.transform.Find("Collar"), plate = hatch.Lid.Find("Lid plate"), relief = hatch.Lid.Find("Lid relief"), underside = hatch.Lid.Find("Lid underside");
                Assert.That(Cut(collar).size.x, Is.EqualTo(MoneyHatch.Size.x).Within(1e-3f), "the collar is the hatch's whole width");
                Assert.That(Cut(collar).size.z, Is.EqualTo(MoneyHatch.Size.z).Within(1e-3f), "the same both ways");
                Assert.AreEqual(MoneyHatch.Size.x, MoneyHatch.Size.z, 1e-6f);
                foreach (Vector3 corner in collar.GetComponent<MeshFilter>().sharedMesh.vertices)
                    Assert.LessOrEqual(new Vector2(corner.x, corner.z).magnitude, MoneyHatch.OuterRadius + 1e-3f, "nothing of the collar outside its round");
                Vector3 middle = Cut(plate).center;
                foreach (Vector3 corner in plate.GetComponent<MeshFilter>().sharedMesh.vertices)
                    Assert.LessOrEqual(new Vector2(corner.x - middle.x, corner.z - middle.z).magnitude, MoneyHatch.LidRadius + 1e-3f, "a round lid");
                Assert.Less(MoneyHatch.Size.y, 0.08f, "nearly flush with the grass");
                Assert.That(Paint(collar).r, Is.EqualTo(MoneyPile.Steel.r).Within(0.01f), "the safes' dark steel");
                Assert.Greater(Paint(collar).r, MoneyHatch.LidColor.r, "a little lighter than the lid it holds");

                // A stack lying flat passes the hole whichever way it is turned.
                float length = MoneyBundle.Length(MoneyPiece.Stack) * MoneyPile.PieceScale, width = MoneyBundle.Width(MoneyPiece.Stack) * MoneyPile.PieceScale;
                Assert.Greater(2f * MoneyHatch.HoleRadius, Mathf.Sqrt(length * length + width * width), "the hole is wider than the stack's corner to corner");

                // The lid lies in the collar's lip, its top level with it, and covers the hole.
                Assert.Greater(MoneyHatch.LidRadius, MoneyHatch.HoleRadius, "the lid covers the hole");
                Assert.Less(MoneyHatch.LidRadius + MoneyHatch.LidGap, MoneyHatch.OuterRadius - 0.02f, "and leaves the collar's lip showing round it");
                Assert.That(Cut(plate).max.y, Is.EqualTo(0f).Within(1e-4f), "the hinge is level with the lid's top");
                Assert.That(Cut(plate).size.y, Is.EqualTo(MoneyHatch.LidThickness).Within(1e-4f));
                hatch.Show(0f);
                Assert.That(plate.GetComponent<Renderer>().bounds.max.y, Is.EqualTo(hatch.transform.position.y + MoneyHatch.Size.y).Within(1e-3f), "shut, the lid's top is level with the lip");

                // The relief is the casting's own: nearly black like the lid, a shade lighter, standing a little proud, inside the lid's edge.
                Assert.Less(MoneyHatch.ReliefColor.maxColorComponent, 0.2f, "the relief stays nearly black too");
                Assert.Greater(MoneyHatch.ReliefColor.r, MoneyHatch.LidColor.r, "a shade lighter, so it reads as raised");
                Assert.That(Paint(relief).r, Is.EqualTo(MoneyHatch.ReliefColor.r).Within(0.01f));
                Assert.That(Cut(relief).max.y, Is.EqualTo(MoneyHatch.Relief).Within(1e-3f), "it stands proud of the lid");
                Assert.Less(MoneyHatch.Relief, 0.015f, "only a little: a plain cover");
                Assert.Less(Cut(relief).size.x, MoneyHatch.LidSize.x, "inside the lid's edge");
                Assert.Less(Cut(relief).size.z, MoneyHatch.LidSize.z);
                Assert.That(MoneyHatch.Bars, Is.InRange(8, 16), "a few bars between the rim and the ring");
                Assert.Less(MoneyHatch.Boss, MoneyHatch.RingFrom, "boss, ring and rim lie apart");
                Assert.Less(MoneyHatch.RingTo, MoneyHatch.RimFrom);

                // The underside faces the table when the lid stands open; shut, it fits down the hole.
                Assert.LessOrEqual(Cut(underside).max.y, -MoneyHatch.LidThickness + 1e-4f, "under the lid");
                Assert.Less(Cut(underside).size.x, 2f * MoneyHatch.HoleRadius, "it fits down the hole when the lid is shut");
                Assert.Less(Cut(underside).size.z, 2f * MoneyHatch.HoleRadius);

                // Down the round hole: black at the walls, a dim glimmer of gold deep in the middle.
                Transform hole = hatch.transform.Find("Hole");
                Assert.That(Cut(hole).size.x, Is.EqualTo(2f * MoneyHatch.HoleRadius).Within(1e-3f));
                Assert.That(Cut(hole).size.z, Is.EqualTo(2f * MoneyHatch.HoleRadius).Within(1e-3f));
                Assert.AreSame(HatchSkin.Hole(), hole.GetComponent<Renderer>().sharedMaterial.mainTexture);
                Color glimmer = HatchSkin.ColourAt(0.5f, 0.5f);
                Assert.That(HatchSkin.ColourAt(0.02f, 0.5f).r, Is.EqualTo(HatchSkin.Dark.r).Within(1e-3f), "black at the walls");
                Assert.That(HatchSkin.ColourAt(0f, 0f).r, Is.EqualTo(HatchSkin.Dark.r).Within(1e-3f));
                Assert.Greater(glimmer.r, glimmer.b * 2f, "a warm glimmer");
                Assert.Less(glimmer.maxColorComponent, 0.4f, "dim: a glimmer, not a lamp");
                float last = glimmer.r;
                for (int i = 1; i <= 10; i++)
                {
                    float r = HatchSkin.ColourAt(0.5f + i * 0.05f, 0.5f).r;
                    Assert.LessOrEqual(r, last + 1e-4f, "it only fades towards the walls");
                    last = r;
                }
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        private static Color Paint(Transform part) => part.GetComponent<Renderer>().sharedMaterial.color;

        [Test]
        public void TheLawnHasNoSafeAndEveryHatchIsTheSameDarkColour()
        {
            var holder = new GameObject("Board Test");
            try
            {
                BoardView board = BoardView.Create(holder.transform, BoardDataLoader.LoadFromResources());
                board.CreateMoneyPiles(new[] { Color.red, Color.blue, Color.green });
                foreach (Transform part in holder.GetComponentsInChildren<Transform>(true))
                    Assert.AreNotEqual("Safe", part.name, "the bank's safe is gone: paid money goes down the hatches");
                foreach (MoneyHatch hatch in board.MoneyHatches)
                {
                    Color lid = hatch.Lid.GetComponentInChildren<Renderer>().sharedMaterial.color;
                    Assert.That(lid.r, Is.EqualTo(MoneyHatch.LidColor.r).Within(0.01f), $"{hatch.name}: one colour for every player");
                    Assert.Less(Mathf.Max(lid.r, lid.g, lid.b), 0.2f, $"{hatch.name}: nearly black");
                }

                Assert.Greater(MoneyHatch.LidColor.r, MoneyHatch.HoleDark.r + 0.05f, "the hole stays darker than the lid");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void EachSafeStandsOnAFloatingIslandThatNarrowsToAPoint()
        {
            // The owner's wishes (2026-11-11): the safes' shelves are little floating islands, professionally made.
            var holder = new GameObject("Board Test");
            try
            {
                BoardView board = BoardView.Create(holder.transform, BoardDataLoader.LoadFromResources());
                board.CreateMoneyPiles(new[] { Color.red, Color.blue });
                Bounds Of(Transform island, string part) => island.Find(part).GetComponent<Renderer>().bounds;
                for (int seat = 0; seat < 2; seat++)
                {
                    Transform island = board.MoneyShelves[seat];
                    Bounds grass = Of(island, "Meadow");
                    Assert.AreEqual(BoardView.ShelfSize.x * BoardView.ShelfSize.z, grass.size.x * grass.size.z, 1e-2f, $"seat {seat + 1}: the meadow is the shelf's top");
                    Assert.Less(grass.min.y, grass.max.y - IslandShape.SkirtShort, $"seat {seat + 1}: grass hangs over the edge");
                    Assert.GreaterOrEqual(grass.min.y, grass.max.y - IslandShape.SkirtLong - 1e-3f);

                    Bounds rock = Of(island, "Rock");
                    Assert.Less(rock.max.y, grass.max.y, $"seat {seat + 1}: the body under the meadow");
                    Assert.AreEqual(grass.max.y - IslandParts.Depth, rock.min.y, 1e-3f, $"seat {seat + 1}: as deep as the island is said to be");
                    Assert.AreSame(IslandSkin.Rock(), island.Find("Rock").GetComponent<Renderer>().sharedMaterial.mainTexture);
                    Assert.AreSame(IslandSkin.Grass(), island.Find("Meadow").GetComponent<Renderer>().sharedMaterial.mainTexture);

                    Transform vines = island.Find(IslandParts.VinesName);
                    Assert.AreEqual(IslandShape.Vines, vines.childCount, $"seat {seat + 1}: vines hang from under the grass");
                    var parts = new System.Collections.Generic.List<Bounds> { rock, Of(island, "Tufts"), Of(island, "Flowers") };
                    float lowestVine = grass.max.y;
                    foreach (Transform vine in vines)
                    {
                        Bounds b = vine.GetComponent<Renderer>().bounds;
                        Assert.AreEqual(grass.max.y - 0.06f, vine.position.y, 1e-3f, $"seat {seat + 1}: {vine.name} is rooted just under the grass");
                        Assert.LessOrEqual(b.max.y, vine.position.y + 0.05f, $"seat {seat + 1}: {vine.name} hangs from its foot, so it can sway about it");
                        lowestVine = Mathf.Min(lowestVine, b.min.y);
                        parts.Add(b);
                    }

                    Assert.Less(lowestVine, grass.max.y - 0.5f, $"seat {seat + 1}: vines trail down");
                    Assert.Greater(Of(island, "Tufts").max.y, grass.max.y + 0.08f, $"seat {seat + 1}: tufts stand on the meadow");
                    Assert.Greater(Of(island, "Flowers").min.y, grass.max.y, $"seat {seat + 1}: and flowers");
                    Assert.Less(Of(island, "Tufts").max.y, grass.max.y + MoneyPile.SafeSize.y / 2f, $"seat {seat + 1}: all of it low beside the safe");

                    Transform stones = island.Find(IslandParts.StonesName);
                    Assert.AreEqual(4, stones.childCount, $"seat {seat + 1}: loose stones adrift under it");
                    Assert.IsNotNull(stones.GetComponent<IslandDrift>(), $"seat {seat + 1}: and they bob");

                    // Afloat: the island and its safe rise and sink together about where the board put them.
                    IslandFloat afloat = island.GetComponent<IslandFloat>();
                    Assert.IsNotNull(afloat, $"seat {seat + 1}: the island floats");
                    Transform safe = board.MoneyPiles[seat].Root;
                    Vector3 islandRest = island.localPosition, safeRest = safe.localPosition;
                    float peak = IslandFloat.SecondsOf(seat) * (0.25f - Mathf.Repeat(seat * 0.618034f, 1f));
                    afloat.Show(peak);
                    Assert.AreEqual(IslandFloat.Rise, island.localPosition.y - islandRest.y, 1e-4f, $"seat {seat + 1}: up by a little");
                    Assert.AreEqual(IslandFloat.Rise, safe.localPosition.y - safeRest.y, 1e-4f, $"seat {seat + 1}: and the safe with it");
                    Assert.AreEqual(islandRest.x, island.localPosition.x, 1e-5f, $"seat {seat + 1}: only up and down");
                    afloat.Show(peak + IslandFloat.SecondsOf(seat) / 2f);
                    Assert.AreEqual(-IslandFloat.Rise, safe.localPosition.y - safeRest.y, 1e-4f, $"seat {seat + 1}: and down");
                    board.FaceCamera(BoardCameraRig.BaseYaw);
                    Assert.AreEqual(safeRest.y, safe.localPosition.y, 1e-5f, $"seat {seat + 1}: placed again, both are back at rest");
                    Assert.AreEqual(islandRest.y, island.localPosition.y, 1e-5f);
                    foreach (Transform stone in stones)
                    {
                        Bounds b = stone.GetComponent<Renderer>().bounds;
                        Assert.Less(b.max.y, grass.max.y - 1f, $"seat {seat + 1}: {stone.name} well under the meadow");
                        Assert.Greater(b.min.y - IslandDrift.Rise, grass.max.y - IslandParts.Depth, $"seat {seat + 1}: {stone.name} no lower than the point");
                        parts.Add(b);
                    }

                    foreach (Bounds b in parts)
                    {
                        Assert.IsTrue(b.min.x >= grass.min.x - 0.02f && b.max.x <= grass.max.x + 0.02f && b.min.z >= grass.min.z - 0.02f && b.max.z <= grass.max.z + 0.02f,
                            $"seat {seat + 1}: everything keeps within the meadow's outline ({b})");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void AnIslandsBodyStepsInLedgeByLedgeToAPointAndEachIslandIsItsOwn()
        {
            var top = new Vector2(BoardView.ShelfSize.x, BoardView.ShelfSize.z);

            // The body: widest under the grass, each ledge set in under the one above, nothing at the point.
            Assert.AreEqual(IslandShape.BodyShare, IslandShape.ShareAt(0f), 1e-4f);
            Assert.AreEqual(0f, IslandShape.ShareAt(IslandShape.Depth), 1e-4f);
            Assert.Greater(IslandShape.ShareAt(IslandShape.Ledges[0]), IslandShape.ShareAt(IslandShape.SoilDepth), "the first ledge of rock juts out under the soil");
            for (int ledge = 1; ledge < IslandShape.Ledges.Length; ledge++)
            {
                float above = IslandShape.Ledges[ledge - 1], here = IslandShape.Ledges[ledge];
                Assert.Less(IslandShape.ShareAt(here), IslandShape.ShareAt(above) - 0.15f, $"ledge {ledge + 1} is well narrower than ledge {ledge}");
                float cliff = IslandShape.ShareAt(above) - IslandShape.ShareAt(above + (here - above) * 0.6f);
                float under = IslandShape.ShareAt(above + (here - above) * 0.6f) - IslandShape.ShareAt(here);
                Assert.Less(cliff, under, $"ledge {ledge}: a steep face, then cut in sharply underneath");
            }

            // The meadow's edge touches the top's four sides and never passes them; two islands differ.
            Vector2[] edge = IslandShape.MeadowEdge(top, 0), other = IslandShape.MeadowEdge(top, 1);
            float right = 0f, left = 0f, far = 0f, near = 0f, differs = 0f;
            for (int i = 0; i < edge.Length; i++)
            {
                right = Mathf.Max(right, edge[i].x);
                left = Mathf.Min(left, edge[i].x);
                far = Mathf.Max(far, edge[i].y);
                near = Mathf.Min(near, edge[i].y);
                differs = Mathf.Max(differs, (edge[i] - other[i]).magnitude);
            }

            Assert.AreEqual(top.x / 2f, right, 1e-4f);
            Assert.AreEqual(-top.x / 2f, left, 1e-4f);
            Assert.AreEqual(top.y / 2f, far, 1e-4f);
            Assert.AreEqual(-top.y / 2f, near, 1e-4f);
            Assert.Greater(differs, 0.005f, "each island has its own edge");
            Assert.AreNotEqual(IslandShape.LeanAt(top, 0, IslandShape.Depth), IslandShape.LeanAt(top, 1, IslandShape.Depth), "and its own lean");
            Assert.AreEqual(Vector2.zero, IslandShape.LeanAt(top, 0, 0f), "upright under the grass");

            // Cut in flat faces, each looking out: its corners share one facing, and it is wound to look that way.
            // The rock is cut in flat faces (its corners share one facing); the grass and the vines are rounded off. Every
            // face is wound to look the way its corners face.
            Mesh[] chiselled = { IslandShape.Rock(top, 0), IslandShape.Flowers(top, 0), IslandShape.Drifter(0) };
            Mesh[] soft = { IslandShape.Meadow(top, 0), IslandShape.Vine(top, 0, 0), IslandShape.Tufts(top, 0) };
            foreach (Mesh mesh in chiselled.Concat(soft))
            {
                bool flat = chiselled.Contains(mesh);
                Vector3[] vertices = mesh.vertices, normals = mesh.normals;
                int[] triangles = mesh.triangles;
                Assert.Greater(triangles.Length, 0, mesh.name);
                Assert.AreEqual(vertices.Length, mesh.uv.Length, mesh.name);
                int rounded = 0;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                    Vector3 first = normals[triangles[i]], facing = first + normals[triangles[i + 1]] + normals[triangles[i + 2]];
                    if (flat) Assert.AreEqual(first, normals[triangles[i + 1]], mesh.name);
                    if (flat) Assert.AreEqual(first, normals[triangles[i + 2]], mesh.name);
                    if ((first - normals[triangles[i + 1]]).magnitude > 0.01f) rounded++;
                    Assert.Greater(Vector3.Dot(Vector3.Cross(b - a, c - a), facing), 0f, $"{mesh.name}: face {i / 3} is wound to look along its facing");
                }

                if (!flat) Assert.Greater(rounded, triangles.Length / 3 / 4, $"{mesh.name}: rounded off");
            }

            Assert.AreEqual(0f, Vector3.Angle(Vector3.up, IslandShape.Meadow(top, 0).normals[0]), 0.5f, "the meadow's middle faces straight up");

            Assert.AreSame(IslandShape.Rock(top, 0), IslandShape.Rock(top, 0), "the same island is cut once");
            Assert.AreNotSame(IslandShape.Rock(top, 0), IslandShape.Rock(top, 1));

            // The meadow's top faces up and the body's faces look out from its middle.
            Mesh rock = IslandShape.Rock(top, 0);
            Vector3[] rockAt = rock.vertices, rockFacing = rock.normals;
            int outward = 0;
            for (int i = 0; i < rockAt.Length; i++)
            {
                if (Vector3.Dot(new Vector3(rockAt[i].x, 0f, rockAt[i].z), rockFacing[i]) > -0.05f) outward++;
            }

            Assert.Greater(outward, rockAt.Length * 0.8f, "the body's faces look out, not in");

            // The paint: brown soil under the grass, the board's warm stone below, darker and hazier towards the point.
            float V(float depth) => 1f - depth / IslandShape.Depth;
            Color soil = Average(V(0.2f)), high = Average(V(0.45f)), low = Average(V(1.95f));
            Assert.Greater(soil.r, soil.b + 0.12f, "soil is brown");
            Assert.Greater(high.grayscale, soil.grayscale + 0.1f, "the stone is lighter than the soil");
            Assert.Greater(high.r, high.b, "a warm stone, like the board's");
            Assert.Less(low.grayscale, high.grayscale - 0.05f, "darker towards the point");
            Assert.Greater(low.b - low.r, high.b - high.r + 0.03f, "with the sky's haze on it");
            Assert.AreEqual(0f, ((Vector4)(IslandSkin.RockAt(0f, 0.6f) - IslandSkin.RockAt(1f, 0.6f))).magnitude, 1e-4f, "the picture meets itself round the island");
            Assert.AreEqual(0f, ((Vector4)(LawnSkin.Light - IslandSkin.GrassAt(1f))).magnitude, 1e-4f, "the lawn's own green on top");
            Assert.Less(IslandSkin.GrassAt(0f).grayscale, IslandSkin.GrassAt(0.7f).grayscale - 0.05f, "deeper at the tips of the hanging grass");
            Assert.Less(IslandSkin.GrassAt(0.7f).grayscale, IslandSkin.GrassAt(1f).grayscale);

            // The loose stones rise and sink a little, each in its own time, and turn.
            float most = 0f;
            for (float seconds = 0f; seconds < 20f; seconds += 0.05f) most = Mathf.Max(most, Mathf.Abs(IslandDrift.RiseAt(seconds, 0, 0)));
            Assert.AreEqual(IslandDrift.Rise, most, 1e-3f);
            Assert.AreNotEqual(IslandDrift.SecondsOf(0, 0), IslandDrift.SecondsOf(1, 0));
            for (int stone = 0; stone < 4; stone++) Assert.That(IslandDrift.SecondsOf(stone, 3), Is.InRange(IslandDrift.QuickSeconds, IslandDrift.SlowSeconds));

            // Calm: everything moves by little and slowly, each island in its own time.
            Assert.LessOrEqual(IslandFloat.Rise, 0.06f);
            Assert.GreaterOrEqual(IslandFloat.QuickSeconds, 5f);
            Assert.AreNotEqual(IslandFloat.SecondsOf(0), IslandFloat.SecondsOf(1));
            Assert.AreNotEqual(IslandFloat.RiseAt(1f, 0), IslandFloat.RiseAt(1f, 1));
            Assert.LessOrEqual(IslandDrift.Sway, 5f);
            Assert.GreaterOrEqual(IslandDrift.BreezeSeconds, 4f);
            float widest = 0f;
            for (float seconds = 0f; seconds < 30f; seconds += 0.05f) widest = Mathf.Max(widest, Mathf.Abs(IslandDrift.SwayAt(seconds, 3, 1)));
            Assert.That(widest, Is.InRange(IslandDrift.Sway * 0.6f, IslandDrift.Sway + 1e-3f));
            Assert.AreNotEqual(IslandDrift.SwayAt(2f, 0, 0), IslandDrift.SwayAt(2f, 1, 0), "each vine a little behind the other");

            var stones = new GameObject("Stones");
            var vines = new GameObject("Vines");
            try
            {
                var child = new GameObject("Stone 1");
                child.transform.SetParent(stones.transform, false);
                child.transform.localPosition = new Vector3(1f, -1.5f, 0.5f);
                var vine = new GameObject("Vine 1");
                vine.transform.SetParent(vines.transform, false);
                vine.transform.localPosition = new Vector3(0.5f, -0.06f, 0.2f);
                IslandDrift drift = IslandDrift.Attach(stones.transform, vines.transform, 2);
                drift.Show(1.7f);
                Assert.AreEqual(new Vector3(0.5f, -0.06f, 0.2f), vine.transform.localPosition, "a vine sways about its foot, which stays put");
                Assert.AreEqual(IslandDrift.SwayAt(1.7f, 0, 2), Mathf.DeltaAngle(0f, vine.transform.localEulerAngles.z), 0.05f);
                float quarter = IslandDrift.SecondsOf(0, 2) / 4f;
                drift.Show(quarter);
                Vector3 one = child.transform.localPosition;
                drift.Show(quarter * 3f);
                Vector3 two = child.transform.localPosition;
                Assert.AreEqual(1f, one.x, 1e-5f, "only up and down");
                Assert.AreEqual(0.5f, one.z, 1e-5f);
                Assert.AreEqual(-3f, one.y + two.y, 1e-4f, "about its rest");
                Assert.That(Mathf.Abs(one.y + 1.5f), Is.LessThanOrEqualTo(IslandDrift.Rise + 1e-5f));
                Assert.AreNotEqual(Quaternion.identity, child.transform.localRotation, "and turning");
            }
            finally
            {
                Object.DestroyImmediate(stones);
                Object.DestroyImmediate(vines);
            }
        }

        private static Color Average(float v)
        {
            Color sum = Color.clear;
            const int samples = 64;
            for (int i = 0; i < samples; i++) sum += IslandSkin.RockAt((i + 0.5f) / samples, v);
            return sum / samples;
        }
    }
}
