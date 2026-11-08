using System.Collections.Generic;
using Lazpoly.Presentation.Effects;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    public class FireworkShowTests
    {
        [Test]
        public void TheShowIsShortAndEndsWhenTheLastSparksAreGone()
        {
            Assert.AreEqual(0f, FireworkShow.Seconds(0));
            float three = FireworkShow.Seconds(3);
            Assert.AreEqual(FireworkShow.BurstAt(2, FireworkShow.RocketsPerTown - 1) + FireworkShow.BurstSeconds, three, 1e-4f);
            Assert.That(three, Is.InRange(1.5f, 3f), "A short show: the table waits for it.");
            Assert.That(FireworkShow.Seconds(4), Is.LessThan(3.5f));
        }

        [Test]
        public void TheTownsGoUpOneAfterTheOtherAndEachRocketBurstsAtTheTop()
        {
            Assert.AreEqual(0f, FireworkShow.LaunchAt(0, 0));
            Assert.Greater(FireworkShow.LaunchAt(1, 0), FireworkShow.LaunchAt(0, 0));
            Assert.Greater(FireworkShow.LaunchAt(0, 1), FireworkShow.LaunchAt(1, 0), "A town's second rocket follows the others' first.");
            Assert.AreEqual(FireworkShow.LaunchAt(1, 1) + FireworkShow.RiseSeconds, FireworkShow.BurstAt(1, 1), 1e-4f);

            Vector3 tile = new Vector3(4f, 0.3f, -6f), inward = Vector3.forward;
            Assert.AreEqual(tile, FireworkShow.RocketPosition(tile, inward, 0f));
            Vector3 top = FireworkShow.RocketPosition(tile, inward, FireworkShow.RiseSeconds);
            Assert.AreEqual(FireworkShow.BurstHeight, top.y - tile.y, 1e-4f);
            Assert.AreEqual(FireworkShow.InwardDrift, top.z - tile.z, 1e-4f, "It drifts towards the board's middle.");
            Assert.Less(FireworkShow.RocketHeight(0.1f), FireworkShow.RocketHeight(0.3f));
        }

        [Test]
        public void SparksSpreadRoundFallAndShrinkAway()
        {
            List<Vector3> directions = FireworkShow.SparkDirections(FireworkShow.SparksPerBurst);
            Assert.AreEqual(FireworkShow.SparksPerBurst, directions.Count);
            int up = 0, down = 0;
            foreach (Vector3 direction in directions)
            {
                Assert.AreEqual(1f, direction.magnitude, 1e-3f);
                if (direction.y > 0.2f) up++;
                if (direction.y < -0.2f) down++;
            }

            Assert.Greater(up, 5);
            Assert.Greater(down, 5, "A round burst, not a fountain.");

            Assert.AreEqual(Vector3.zero, FireworkShow.SparkOffset(Vector3.right, 2f, 0f));
            Vector3 early = FireworkShow.SparkOffset(Vector3.right, 2f, 0.3f), late = FireworkShow.SparkOffset(Vector3.right, 2f, 1.1f);
            Assert.Greater(late.x, early.x);
            Assert.Less(late.x - early.x, early.x, "The air slows the sparks.");
            Assert.Less(late.y, early.y, "They fall.");

            Assert.AreEqual(1f, FireworkShow.SparkSize(0f, 0));
            Assert.AreEqual(0f, FireworkShow.SparkSize(FireworkShow.BurstSeconds, 0));
            Assert.Less(FireworkShow.SparkSize(FireworkShow.BurstSeconds * 0.9f, 1), 0.5f);
        }

        [Test]
        public void TheSparksAreUnlitAndTheBurstHasItsSound()
        {
            var material = Resources.Load<Material>(Fireworks.SparkMaterialResource);
            Assert.IsNotNull(material, "Resources/" + Fireworks.SparkMaterialResource);
            Assert.AreEqual("Universal Render Pipeline/Unlit", material.shader.name);
            Assert.IsNotNull(Resources.Load<AudioClip>(Audio.SoundCues.ResourcePath(Audio.SoundCue.Firework)));
        }
    }
}
