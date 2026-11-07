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
            CollectionAssert.AreEqual(new[] { MoneyPiece.Gold, MoneyPiece.Stack, MoneyPiece.Stack }, MoneyFlight.Pieces(650), "the gold first, at the bottom");
            CollectionAssert.AreEqual(new[] { MoneyPiece.Gold, MoneyPiece.Gold, MoneyPiece.Stack, MoneyPiece.Stack }, MoneyFlight.Pieces(5000));
            Assert.LessOrEqual(MoneyFlight.Pieces(499).Length, MoneyFlight.MaxPieces);
            foreach (MoneyPiece piece in new[] { MoneyPiece.Stack, MoneyPiece.Gold })
            {
                Assert.GreaterOrEqual(MoneyBundle.Height(piece), 0.25f * MoneyBundle.Width(piece), $"{piece}: thick, not a single note");
            }
        }

        [Test]
        public void ThePiecesLieSideBySideAndPileUp()
        {
            MoneyPiece[] three = { MoneyPiece.Stack, MoneyPiece.Stack, MoneyPiece.Stack };
            Vector3 first = MoneyFlight.StackOffset(0, three), second = MoneyFlight.StackOffset(1, three), third = MoneyFlight.StackOffset(2, three);
            Assert.AreEqual(0f, first.y);
            Assert.AreEqual(first.y, second.y, 1e-5f, "two side by side in a layer");
            Assert.Greater(Mathf.Abs(second.z - first.z), MoneyBundle.Width(MoneyPiece.Stack), "beside each other, not overlapping");
            Assert.AreEqual(0f, first.z + second.z, 1e-5f, "the layer is centred across");
            Assert.AreEqual(MoneyBundle.Height(MoneyPiece.Stack) + MoneyFlight.LayerGap, third.y - first.y, 1e-5f, "the next layer lies on top");
            Assert.AreEqual(0f, third.z, 1e-5f, "a lone piece in the middle of its layer");
            Assert.AreEqual(0f, MoneyFlight.StackOffset(0, new[] { MoneyPiece.Gold }).z, 1e-5f);

            MoneyPiece[] mixed = MoneyFlight.Pieces(650);
            Vector3 gold = MoneyFlight.StackOffset(0, mixed), beside = MoneyFlight.StackOffset(1, mixed), top = MoneyFlight.StackOffset(2, mixed);
            Assert.Greater(beside.z - gold.z, (MoneyBundle.Width(MoneyPiece.Gold) + MoneyBundle.Width(MoneyPiece.Stack)) / 2f, "gold and a stack side by side");
            Assert.AreEqual(Mathf.Max(MoneyBundle.Height(MoneyPiece.Gold), MoneyBundle.Height(MoneyPiece.Stack)) + MoneyFlight.LayerGap, top.y, 1e-5f, "on the taller one below");

            MoneyPiece[] most = MoneyFlight.Pieces(5000);
            for (int i = 0; i < most.Length; i++)
            {
                Assert.LessOrEqual(Mathf.Abs(MoneyFlight.StackOffset(i, most).x), MoneyFlight.Jitter, "squared up");
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
                Assert.Less(Vector3.Distance(pieces[0].transform.position, pieces[2].transform.position), 1.5f, "the bundles leave together, piled");
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
                Assert.Greater(board.MoneyMiddle.y, BoardView.LawnHeight + 0.5f, "money gathers in the air over the lawn");
                Vector3 middle = new Vector3(board.MoneyMiddle.x, 0f, board.MoneyMiddle.z);
                Assert.That(Vector3.Dot(middle, BoardView.ScreenUp(BoardCameraRig.BaseYaw)), Is.EqualTo(-BoardView.MoneyBack).Within(0.01f), "level with the dice");
                Assert.Greater(Vector3.Dot(middle, BoardView.ScreenRight(BoardCameraRig.BaseYaw)), 1f + MoneyBundle.Length(MoneyPiece.Stack) / 2f, "beside the dice on the right (their outer edge is about 1 unit out), clear of them");
                Assert.Greater(board.Safe.GetComponentsInChildren<MeshRenderer>().Length, 8, "feet, body, door, dial, handle, hinges and slot");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }
    }
}
