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
    /// <summary>Faz 25: the owner's dollar stack, the bank's safe on the lawn and the three-second payment flight.</summary>
    public class MoneyTests
    {
        [Test]
        public void APaymentIsShownInMoneyStacksAndGoldBars()
        {
            CollectionAssert.IsEmpty(MoneyFlight.Pieces(0));
            CollectionAssert.AreEqual(new[] { MoneyPiece.Stack }, MoneyFlight.Pieces(10), "even a small payment goes as a stack");
            CollectionAssert.AreEqual(new[] { MoneyPiece.Stack }, MoneyFlight.Pieces(100));
            CollectionAssert.AreEqual(new[] { MoneyPiece.Stack, MoneyPiece.Stack, MoneyPiece.Stack }, MoneyFlight.Pieces(260));
            CollectionAssert.AreEqual(new[] { MoneyPiece.Gold }, MoneyFlight.Pieces(500), "a gold bar for every full 500");
            CollectionAssert.AreEqual(new[] { MoneyPiece.Stack, MoneyPiece.Stack, MoneyPiece.Gold }, MoneyFlight.Pieces(650), "the gold last, at the row's end");
            CollectionAssert.AreEqual(new[] { MoneyPiece.Stack, MoneyPiece.Stack, MoneyPiece.Gold, MoneyPiece.Gold }, MoneyFlight.Pieces(5000));
            Assert.LessOrEqual(MoneyFlight.Pieces(499).Length, MoneyFlight.MaxPieces);
            foreach (MoneyPiece piece in new[] { MoneyPiece.Stack, MoneyPiece.Gold })
            {
                Assert.GreaterOrEqual(MoneyBundle.Height(piece), 0.25f * MoneyBundle.Width(piece), $"{piece}: thick, not a single note");
            }
        }

        [Test]
        public void ThePiecesFlyInOneNeatRow()
        {
            MoneyPiece[] row = MoneyFlight.Pieces(5000);
            float previousEnd = float.NegativeInfinity;
            for (int i = 0; i < row.Length; i++)
            {
                Vector3 at = MoneyFlight.RowOffset(i, row);
                Assert.AreEqual(0f, at.y, 1e-5f, "all on one level, none piled");
                Assert.AreEqual(0f, at.z, 1e-5f, "on one straight line");
                float start = at.x - MoneyBundle.Length(row[i]) / 2f;
                if (i > 0) Assert.AreEqual(MoneyFlight.RowGap, start - previousEnd, 1e-4f, "even gaps, side by side");
                previousEnd = at.x + MoneyBundle.Length(row[i]) / 2f;
            }

            Assert.AreEqual(0f, MoneyFlight.RowOffset(0, row).x + MoneyFlight.RowOffset(row.Length - 1, row).x
                + (MoneyBundle.Length(row[row.Length - 1]) - MoneyBundle.Length(row[0])) / 2f, 1e-4f, "the row is centred");
            Assert.AreEqual(0f, MoneyFlight.RowOffset(0, new[] { MoneyPiece.Gold }).x, 1e-5f, "a lone piece in the middle");
            Assert.LessOrEqual(MoneyFlight.RowLength(row) / 2f, 3.2f, "the longest row fits the lawn below the dice");
        }

        [Test]
        public void EachPlayersMoneyLiesAtTheTablesEdgeFacingTheirCorner()
        {
            CollectionAssert.IsEmpty(MoneyPile.Pieces(0));
            CollectionAssert.AreEqual(new[] { MoneyPiece.Stack }, MoneyPile.Pieces(50));
            CollectionAssert.AreEqual(new[] { MoneyPiece.Gold, MoneyPiece.Stack, MoneyPiece.Stack }, MoneyPile.Pieces(1500));
            Assert.AreEqual(MoneyPile.MaxPieces, MoneyPile.Pieces(100000).Length);
            Assert.AreEqual(0f, MoneyPile.SlotOffset(0, 1).x, 1e-5f, "a lone piece in the middle");
            for (int i = 0; i < MoneyPile.MaxPieces; i++)
            {
                Vector3 slot = MoneyPile.SlotOffset(i, MoneyPile.MaxPieces);
                Assert.AreEqual(0f, slot.z, 1e-5f, "one row, none behind another");
                Assert.LessOrEqual(Mathf.Abs(slot.x) + MoneyBundle.Length(MoneyPiece.Stack) * MoneyPile.PieceScale / 2f, MoneyPile.CaseSize.x / 2f - MoneyPile.CaseWall + 1e-4f, "inside the case's walls");
                if (i > 0) Assert.AreEqual(MoneyBundle.Length(MoneyPiece.Stack) * MoneyPile.PieceScale + MoneyPile.Gap, slot.x - MoneyPile.SlotOffset(i - 1, MoneyPile.MaxPieces).x, 1e-4f, "side by side, even gaps");
            }

            Assert.AreEqual(0f, MoneyPile.SlotOffset(0, 3).x + MoneyPile.SlotOffset(2, 3).x, 1e-5f, "the row is centred on the tray");

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
                board.ShowSeatMoney(1, 1500);
                CollectionAssert.AreEqual(MoneyPile.Pieces(1500), board.MoneyPiles[1].Shown);
                Assert.Greater(board.SeatMoneySpot(1).y, 0f, "money leaves from over the tray");
                Assert.AreEqual(3, board.MoneyShelves.Count, "every tray stands on a shelf");
                for (int seat = 0; seat < 3; seat++)
                {
                    Bounds shelf = board.MoneyShelves[seat].GetComponent<Renderer>().bounds;
                    Transform tray = board.MoneyPiles[seat].Root;
                    Assert.AreEqual(tray.position.y, shelf.max.y, 1e-3f, $"seat {seat + 1}: the tray lies on its shelf");
                    Assert.Less(shelf.max.y, 0f, $"seat {seat + 1}: the shelf a little under the plate's top");
                    foreach (Transform half in new[] { board.MoneyPiles[seat].Case, board.MoneyPiles[seat].Lid })
                    foreach (float x in new[] { -0.5f, 0.5f })
                    foreach (float z in new[] { -0.5f, 0.5f })
                    {
                        Vector3 corner = half.TransformPoint(Vector3.Scale(MoneyPile.CaseSize, new Vector3(x, 0f, z)));
                        Assert.IsTrue(shelf.min.x - 1e-3f <= corner.x && corner.x <= shelf.max.x + 1e-3f && shelf.min.z - 1e-3f <= corner.z && corner.z <= shelf.max.z + 1e-3f,
                            $"seat {seat + 1}: the {half.name}'s corners on the shelf");
                    }

                    Vector3 lidWay = board.transform.InverseTransformDirection(board.MoneyPiles[seat].Lid.position - tray.position);
                    Assert.Greater(Vector3.Dot(lidWay, BoardView.MoneyEdgeOutward(seat, yaw)), MoneyPile.CaseDepth - 1e-3f, $"seat {seat + 1}: the lid lies open away from the board");
                    Assert.AreEqual(0f, Vector3.Dot(lidWay, Vector3.up), 1e-3f, $"seat {seat + 1}: flat on the shelf");

                    Vector3 inner = board.transform.InverseTransformPoint(shelf.center) - BoardView.MoneyEdgeOutward(seat, yaw) * (BoardView.ShelfSize.z / 2f);
                    Assert.Less(Vector3.Dot(inner, BoardView.MoneyEdgeOutward(seat, yaw)), board.HalfExtent, $"seat {seat + 1}: the shelf comes out from under the plate");
                }

                board.ShowSeatMoney(1, 0);
                CollectionAssert.IsEmpty(board.MoneyPiles[1].Shown, "a player with nothing has an empty case");
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
        public void ThePaymentGoesStraightThroughTheMiddleAndEndsInThreeSeconds()
        {
            Vector3 from = new Vector3(-4f, 1f, 0f), middle = new Vector3(0f, 1.6f, 0f), to = new Vector3(4f, 1f, 2f);
            Assert.AreEqual(3f, MoneyFlight.Seconds, "the owner's three seconds");
            Assert.LessOrEqual(MoneyFlight.ArriveAt + MoneyFlight.SinkSeconds, MoneyFlight.Seconds + 1e-4f, "gone in time");
            Assert.AreEqual(from, MoneyFlight.PositionAt(0f, from, middle, to));
            Assert.That(Vector3.Distance(MoneyFlight.PositionAt(MoneyFlight.GatherAt, from, middle, to), middle), Is.LessThan(1e-4f), "it waits in the middle");
            Assert.AreEqual(to, MoneyFlight.PositionAt(MoneyFlight.ArriveAt, from, middle, to));
            Vector3 halfway = MoneyFlight.PositionAt((MoneyFlight.LeaveAt + MoneyFlight.ArriveAt) / 2f, from, middle, to);
            Assert.That(Vector3.Distance(halfway, (middle + to) / 2f), Is.LessThan(1e-3f), "straight on, no arc (the owner's wish)");
            Assert.AreEqual(0f, MoneyFlight.ScaleAt(0f));
            Assert.AreEqual(1f, MoneyFlight.ScaleAt(MoneyFlight.GatherAt));
            Assert.AreEqual(0f, MoneyFlight.ScaleAt(MoneyFlight.Seconds), 1e-4f, "it sinks into whoever gets it");

            var root = new GameObject("Table");
            try
            {
                float seconds = MoneyFlight.Fly(root.transform, from, middle, to, 260, Quaternion.identity, 2f);
                Assert.AreEqual(1.5f, seconds, 1e-4f, "the animation speed setting shortens it");
                MoneyFlight[] pieces = root.GetComponentsInChildren<MoneyFlight>();
                Assert.AreEqual(3, pieces.Length, "three stacks for 260");
                Assert.AreEqual(pieces[0].transform.position.y, pieces[2].transform.position.y, 1e-4f, "the bundles leave side by side, none piled");
                Assert.AreEqual(2f * (MoneyBundle.Length(MoneyPiece.Stack) + MoneyFlight.RowGap),
                    Vector3.Distance(pieces[0].transform.position, pieces[2].transform.position), 1e-3f, "in one neat row with even gaps");
            }
            finally
            {
                Object.DestroyImmediate(root);
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
        public void TheBanksSafeStandsOnTheLawnBesideTheMiddle()
        {
            var holder = new GameObject("Board Test");
            try
            {
                BoardView board = BoardView.Create(holder.transform, BoardDataLoader.LoadFromResources());
                Assert.IsNotNull(board.Safe, "the kasa");
                Vector3 front = board.Safe.localRotation * Vector3.forward;
                Assert.Greater(Vector3.Dot(front, -BoardView.ScreenUp(BoardCameraRig.BaseYaw)), 0.99f, "its door faces the camera");
                Vector3 place = board.Safe.localPosition;
                Assert.Less(Vector3.Dot(place, BoardView.ScreenRight(BoardCameraRig.BaseYaw)), -2f, "to the screen's left of the middle");
                Assert.That(Vector3.Dot(place, BoardView.ScreenUp(BoardCameraRig.BaseYaw)), Is.EqualTo(0f).Within(0.01f), "level with the middle, clear of the dice below it and the score above");
                Assert.Greater(board.SafeSlot.y, board.Safe.position.y + BoardView.SafeSize * 0.9f, "money goes in at the top");
                Assert.Greater(board.MoneyMiddle.y, BoardView.LawnHeight + 0.2f, "money gathers in the air over the lawn");
                Vector3 middle = new Vector3(board.MoneyMiddle.x, 0f, board.MoneyMiddle.z);
                Assert.That(Vector3.Dot(middle, BoardView.ScreenUp(BoardCameraRig.BaseYaw)), Is.EqualTo(-BoardView.MoneyBack).Within(0.01f), "below the dice");
                Assert.Greater(BoardView.MoneyBack, 1.5f + 0.35f + MoneyBundle.Width(MoneyPiece.Stack), "clear of the dice, which lie 1.5 units down");
                // From the 35 degree camera a stack's top shows this far up the picture from its foot; the waiting row's
                // nearest tops still stay below the dice's front (1.5 down, a turned die reaching half its 0.7 diagonal).
                float tops = (BoardView.MoneyHover + MoneyBundle.Height(MoneyPiece.Stack)) / Mathf.Tan(BoardCameraRig.Pitch * Mathf.Deg2Rad);
                Assert.Greater(BoardView.MoneyBack - MoneyBundle.Width(MoneyPiece.Stack) / 2f - tops, 1.5f + 0.7f * 0.71f + 0.3f, "the waiting row never hides the dice");
                MoneyPiece[] longest = MoneyFlight.Pieces(5000);
                float lawnCorner = board.Layout.HalfSize * 2f - board.Layout.Depth * 1.05f;
                Assert.Less(MoneyFlight.RowLength(longest) / 2f + BoardView.MoneyBack + MoneyBundle.Width(MoneyPiece.Stack) / 2f,
                    lawnCorner / 2f * Mathf.Sqrt(2f) - 0.5f, "the longest row stays over the lawn");
                Assert.That(Vector3.Dot(middle, BoardView.ScreenRight(BoardCameraRig.BaseYaw)), Is.EqualTo(0f).Within(0.01f), "in the middle of the table");
                Assert.Greater(board.Safe.GetComponentsInChildren<MeshRenderer>().Length, 8, "feet, body, door, dial, handle, hinges and slot");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }
    }
}
