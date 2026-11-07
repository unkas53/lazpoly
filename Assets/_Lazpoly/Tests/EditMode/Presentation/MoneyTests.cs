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
    /// <summary>Faz 25: the owner's money set, the bank's safe on the lawn and the three-second payment flight.</summary>
    public class MoneyTests
    {
        [Test]
        public void TheMoneySetHoldsTheCoinAndTheNoteWithTheirLogos()
        {
            var asset = Resources.Load<TextAsset>(MoneyModels.Resource);
            Assert.IsNotNull(asset, "run Tools/Art/make_money.py");
            IReadOnlyList<MoneyPart> parts = MoneyPack.Parse(asset.bytes);
            foreach (string model in new[] { "Coin", "Note" })
            {
                MoneyPart[] own = parts.Where(p => p.Model == model).ToArray();
                Assert.Greater(own.Length, 2, $"{model}: its colours and its logo");
                Assert.AreEqual(1, own.Count(p => p.Logo), $"{model}: one logo part");
                Vector3[] all = own.SelectMany(p => p.Positions).ToArray();
                Assert.That(all.Min(v => v.y), Is.EqualTo(0f).Within(1e-3f), $"{model} lies on y = 0");
                float length = all.Max(v => v.x) - all.Min(v => v.x);
                Assert.That(length, Is.EqualTo(MoneyModels.Length(model == "Coin" ? MoneyKind.Coin : MoneyKind.Note)).Within(0.01f));
            }

            MoneyPart logo = parts.First(p => p.Model == "Coin" && p.Logo);
            Assert.Greater(Vector3.Dot(logo.Normals[0], Vector3.up), 0.99f, "the coin's logo faces up");
            Assert.Throws<System.FormatException>(() => MoneyPack.Parse(new byte[] { 1, 2, 3, 4 }));
        }

        [Test]
        public void APieceIsBuiltFromSharedPartsWithItsLogoCutOut()
        {
            var root = new GameObject("Money");
            try
            {
                Assert.IsTrue(MoneyModels.IsAvailable);
                GameObject coin = MoneyModels.Create(MoneyKind.Coin, "Coin", root.transform);
                GameObject other = MoneyModels.Create(MoneyKind.Coin, "Coin", root.transform);
                MeshRenderer[] renderers = coin.GetComponentsInChildren<MeshRenderer>();
                Assert.Greater(renderers.Length, 2);
                Assert.AreSame(renderers[0].sharedMaterial, other.GetComponentsInChildren<MeshRenderer>()[0].sharedMaterial, "shared, not copied per piece");
                MeshRenderer logo = renderers.Single(r => r.sharedMaterial.mainTexture != null);
                Assert.IsTrue(logo.sharedMaterial.IsKeywordEnabled("_ALPHATEST_ON"), "the logo's clear pixels are cut away");
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, logo.shadowCastingMode);
                var cutout = Resources.Load<Material>(MoneyModels.CutoutMaterialResource);
                Assert.IsNotNull(cutout);
                Assert.AreEqual(1f, cutout.GetFloat("_AlphaClip"));
                Assert.AreEqual(Resources.Load<Material>(ModelParts.MaterialResource).shader, cutout.shader);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void APaymentIsShownInThickBundles()
        {
            Assert.AreEqual(0, MoneyFlight.Bundles(0));
            Assert.AreEqual(1, MoneyFlight.Bundles(10), "even a small payment goes as a bundle");
            Assert.AreEqual(1, MoneyFlight.Bundles(100));
            Assert.AreEqual(3, MoneyFlight.Bundles(260));
            Assert.AreEqual(MoneyFlight.MaxBundles, MoneyFlight.Bundles(5000));
            Assert.GreaterOrEqual(MoneyBundle.Height, 0.25f * MoneyBundle.Width, "a thick tomar, not a single note");
            Assert.Greater(MoneyBundle.Height, 8f * MoneyBundle.NoteThickness);
        }

        [Test]
        public void TheBundlesLieSideBySideAndPileUp()
        {
            Vector3 first = MoneyFlight.StackOffset(0, 3), second = MoneyFlight.StackOffset(1, 3), third = MoneyFlight.StackOffset(2, 3);
            Assert.AreEqual(0f, first.y);
            Assert.AreEqual(first.y, second.y, 1e-5f, "two side by side in a layer");
            Assert.Greater(Mathf.Abs(second.z - first.z), MoneyBundle.Width, "beside each other, not overlapping");
            Assert.AreEqual(0f, first.z + second.z, 1e-5f, "the layer is centred across");
            Assert.AreEqual(MoneyBundle.Height + MoneyFlight.LayerGap, third.y - first.y, 1e-5f, "the next layer lies on top");
            Assert.AreEqual(0f, third.z, 1e-5f, "a lone bundle in the middle of its layer");
            Assert.AreEqual(0f, MoneyFlight.StackOffset(0, 1).z, 1e-5f);
            for (int i = 0; i < MoneyFlight.MaxBundles; i++)
            {
                Assert.LessOrEqual(Mathf.Abs(MoneyFlight.StackOffset(i, MoneyFlight.MaxBundles).x), MoneyFlight.Jitter, "squared up");
            }

            for (int i = 1; i < MoneyBundle.Sheets; i++)
            {
                Vector3 below = MoneyBundle.SheetOffset(i - 1), above = MoneyBundle.SheetOffset(i);
                Assert.Greater(above.y, below.y, "the sheets lie on each other");
                Assert.LessOrEqual(Mathf.Abs(above.x), MoneyBundle.SheetJitter);
                Assert.LessOrEqual(Mathf.Abs(above.z), MoneyBundle.SheetJitter);
            }

            Assert.Less(MoneyBundle.SheetOffset(MoneyBundle.Sheets - 1).y, MoneyBundle.Height - MoneyBundle.NoteThickness, "under the top note");
        }

        [Test]
        public void ABundleIsPaperWithANoteOnTopAndABand()
        {
            var root = new GameObject("Table");
            try
            {
                GameObject bundle = MoneyBundle.Create("Bundle", root.transform);
                Assert.IsNotNull(bundle.transform.Find("Sheets"));
                Assert.IsNotNull(bundle.transform.Find("Band"));
                Assert.IsNotNull(bundle.transform.Find("Top Note"), "the owner's note lies on top");
                foreach (Renderer part in bundle.GetComponentsInChildren<Renderer>())
                {
                    Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, part.shadowCastingMode, part.name);
                    Assert.IsNotNull(part.sharedMaterial, part.name);
                }

                Material shared = MoneyBundle.Create("Other", root.transform).transform.Find("Sheets").GetComponent<Renderer>().sharedMaterial;
                Assert.AreSame(bundle.transform.Find("Sheets").GetComponent<Renderer>().sharedMaterial, shared, "bundles share their materials");
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
                Assert.AreEqual(3, pieces.Length, "three bundles for 260");
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
                Assert.Greater(Vector3.Dot(middle, BoardView.ScreenRight(BoardCameraRig.BaseYaw)), 1f + MoneyBundle.Length / 2f, "beside the dice on the right (their outer edge is about 1 unit out), clear of them");
                Assert.Greater(board.Safe.GetComponentsInChildren<MeshRenderer>().Length, 8, "feet, body, door, dial, handle, hinges and slot");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }
    }
}
