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
                    Bounds shelf = board.MoneyShelves[seat].GetComponent<Renderer>().bounds;
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
                    Assert.GreaterOrEqual(BoardView.ShelfGapAt(seat, yaw), BoardView.ShelfGap, "the gap shows");
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
    }
}
