using System;
using Lazpoly.Core.Random;
using NUnit.Framework;

namespace Lazpoly.Core.Tests
{
    public class Pcg32RandomTests
    {
        [Test]
        public void MatchesPcgReferenceOutput()
        {
            // Reference values from the official pcg32-demo (seed 42, stream 54).
            var random = new Pcg32Random(42UL, 54UL);
            uint[] expected = { 0xa15c02b7u, 0x7b47f409u, 0xba1d3330u, 0x83d2f293u, 0xbfa4784bu, 0xcbed606eu };

            foreach (uint value in expected)
            {
                Assert.AreEqual(value, random.NextUInt());
            }
        }

        [Test]
        public void SameSeedProducesSameSequence()
        {
            var a = new Pcg32Random(12345UL);
            var b = new Pcg32Random(12345UL);

            for (int i = 0; i < 1000; i++)
            {
                Assert.AreEqual(a.NextInt(1, 7), b.NextInt(1, 7));
            }
        }

        [Test]
        public void RestoredStateContinuesSameSequence()
        {
            var original = new Pcg32Random(777UL);
            original.NextUInt();
            original.NextUInt();

            var restored = Pcg32Random.FromState(original.State, original.Increment);

            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(original.NextUInt(), restored.NextUInt());
            }
        }

        [Test]
        public void NextIntStaysInRangeAndHitsEveryDieFace()
        {
            var random = new Pcg32Random(2026UL);
            var counts = new int[7];
            const int rolls = 60000;

            for (int i = 0; i < rolls; i++)
            {
                int face = random.NextInt(1, 7);
                Assert.That(face, Is.InRange(1, 6));
                counts[face]++;
            }

            // Each face should land near 10,000; 5% tolerance is far outside normal variance.
            for (int face = 1; face <= 6; face++)
            {
                Assert.That(counts[face], Is.InRange(9500, 10500), $"Face {face} count");
            }
        }

        [Test]
        public void NextIntHandlesFullIntRange()
        {
            var random = new Pcg32Random(1UL);
            Assert.DoesNotThrow(() => random.NextInt(int.MinValue, int.MaxValue));
        }

        [Test]
        public void NextIntRejectsEmptyRange()
        {
            var random = new Pcg32Random(1UL);
            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(5, 5));
        }

        [Test]
        public void FromStateRejectsEvenIncrement()
        {
            Assert.Throws<ArgumentException>(() => Pcg32Random.FromState(1UL, 2UL));
        }
    }
}
