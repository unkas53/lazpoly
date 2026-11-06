using System.Collections.Generic;
using Lazpoly.Presentation.Effects;
using Lazpoly.Presentation.Tabletop;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    public class PawnSlotsTests
    {
        [Test]
        public void SlotsStayApartAndInsideTheSquare()
        {
            const float spacing = 2f;
            foreach (int capacity in new[] { 4, 9 })
            {
                var seen = new List<Vector3>();
                for (int slot = 0; slot < capacity; slot++)
                {
                    Vector3 offset = PawnSlots.Offset(slot, capacity, spacing);
                    Assert.AreEqual(0f, offset.y);
                    Assert.Less(Mathf.Abs(offset.x), spacing * 0.45f, "a slot sticks out of the square");
                    Assert.Less(Mathf.Abs(offset.z), spacing * 0.45f, "a slot sticks out of the square");
                    foreach (Vector3 other in seen) Assert.Greater(Vector3.Distance(offset, other), spacing * 0.18f, "two slots overlap");
                    seen.Add(offset);
                }
            }

            Assert.AreEqual(4, PawnSlots.Capacity(2));
            Assert.AreEqual(4, PawnSlots.Capacity(4));
            Assert.AreEqual(9, PawnSlots.Capacity(5));
            Assert.AreEqual(9, PawnSlots.Capacity(8));
        }

        [Test]
        public void StandingFiguresLineUpSideBySide()
        {
            const float spacing = 1.78f;
            foreach (int capacity in new[] { 4, 9 })
            {
                var seen = new List<Vector3>();
                for (int slot = 0; slot < capacity; slot++)
                {
                    Vector3 offset = PawnSlots.Line(slot, capacity, spacing);
                    Assert.AreEqual(0f, offset.y);
                    // A square spans about (width + depth) / sqrt 2 across the screen, a little over 1.7 widths: the line stays on it.
                    Assert.LessOrEqual(Mathf.Abs(offset.x), spacing * 0.8f, "a figure stands off the square");
                    Assert.LessOrEqual(Mathf.Abs(offset.z), spacing * 0.1f, "figures stay in one line, not rows behind each other");
                    // Side by side across the screen: a figure is about a third of its height wide (0.9 units at height 2.7),
                    // and neighbours stand about half of that apart, so every face and jacket shows.
                    foreach (Vector3 other in seen) Assert.Greater(Mathf.Abs(offset.x - other.x), 0.29f, $"slots of {capacity} stand behind each other");
                    seen.Add(offset);
                }
            }

            // Two players on a square of a four-player game stand clearly apart, either side of the middle.
            Vector3 first = PawnSlots.Line(0, 4, spacing), second = PawnSlots.Line(1, 4, spacing);
            Assert.Greater(Mathf.Abs(first.x - second.x), 0.45f, "two figures stand clear of each other");
            Assert.Less(first.x * second.x, 0f);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => PawnSlots.Line(4, 4, spacing));
        }

        [Test]
        public void PawnsOnOneSquareNeverShareASlot()
        {
            var crowd = new PawnCrowd(4);
            Assert.AreEqual(0, crowd.Place(1, 5));
            Assert.AreEqual(1, crowd.Place(2, 5));
            Assert.AreEqual(2, crowd.Place(3, 5));
            Assert.AreEqual(3, crowd.CountOn(5));
            Assert.AreEqual(0, crowd.Place(1, 5), "staying on the square keeps the slot");

            crowd.Place(2, 6);
            Assert.AreEqual(0, crowd.Place(2, 6), "the first pawn on a square takes the first slot");
            Assert.AreEqual(1, crowd.Place(4, 5), "the slot the second pawn left is free again");
            Assert.AreEqual(5, crowd.SquareOf(4));
            Assert.AreEqual(1, crowd.SlotOf(4));

            crowd.Remove(1);
            Assert.AreEqual(-1, crowd.SlotOf(1));
            Assert.AreEqual(0, crowd.Place(5, 5), "a bankrupt pawn's slot is free again");
        }

        [Test]
        public void ACrowdedSquareNeverRunsOutOfSlots()
        {
            var crowd = new PawnCrowd(8);
            for (int player = 1; player <= 8; player++) Assert.That(crowd.Place(player, 0), Is.InRange(0, crowd.Capacity - 1));
            Assert.AreEqual(8, crowd.CountOn(0));
            Assert.That(crowd.Place(9, 0), Is.InRange(0, crowd.Capacity - 1), "one more than the capacity still gets a slot");
        }

        [Test]
        public void TheFlagIsStillWhenThereIsNoWindAndSwingsALittleWhenThereIs()
        {
            Quaternion rest = Quaternion.Euler(10f, 20f, 30f);
            Assert.That(Quaternion.Angle(rest, FlagWave.Flutter(rest, 0f, 0f)), Is.LessThan(1e-3f));
            float widest = 0f;
            for (float time = 0f; time < 10f; time += 0.05f) widest = Mathf.Max(widest, Quaternion.Angle(rest, FlagWave.Flutter(rest, time, 1f)));
            Assert.Greater(widest, FlagWave.SwingDegrees * 0.8f);
            Assert.Less(widest, FlagWave.SwingDegrees + FlagWave.FlapDegrees + 0.5f);
        }
    }
}
