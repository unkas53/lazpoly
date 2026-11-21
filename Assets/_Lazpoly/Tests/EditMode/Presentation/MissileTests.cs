using Lazpoly.Presentation.Effects;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    /// <summary>
    /// The owner's card (2026-11-21): "bir şehre füze at". A missile takes off from its player's hatch, comes down on
    /// the town picked and blows up what is built there, with animations that suit the table and please the eye. Its
    /// way over the table, what is laid in the air for it and how a building blown up goes are plain arithmetic, tested
    /// here without a table.
    /// </summary>
    public class MissileTests
    {
        private const float Tile = 1.78f;
        private static readonly Vector3 Mouth = new Vector3(-3f, 0.2f, -1f);
        private static readonly Vector3 Foot = new Vector3(4f, 0.2f, 2.5f);
        private const float Width = 0.8f;
        private const float Height = 1.1f;

        [Test]
        public void TheMissileLeavesItsHatchStraightUpLeansOverTheTableAndComesDownSteeply()
        {
            float length = MissileStrike.Size * Tile;
            var target = new Vector3(4f, 0.75f, 2.5f);
            var flight = new MissileFlight(Mouth, target, Tile, length);
            Assert.AreEqual(length, flight.Length, 1e-5f);

            // It starts inside the hatch, all of it under the mouth, and goes straight up out of it.
            Assert.AreEqual(Mouth.x, flight.From.x, 1e-4f);
            Assert.AreEqual(Mouth.z, flight.From.z, 1e-4f);
            Assert.Less(flight.Nose(0f).y, Mouth.y, "its nose shows before it moves");
            Assert.Greater(flight.Heading(0f).y, 0.999f, "it leaves its hatch at a slant");
            for (int i = 0; i <= 10; i++)
            {
                // While any of it is still in the hatch it stays over the hole.
                float time = i * 0.02f;
                if (flight.Tail(time).y > Mouth.y) break;
                Vector3 nose = flight.Nose(time);
                Assert.Less(new Vector2(nose.x - Mouth.x, nose.z - Mouth.z).magnitude, 0.2f * Tile, $"it scrapes its hatch at {time}");
            }

            // It ends on the town, nose down, still going the way of the town.
            AssertSame(target, flight.To, "it strikes somewhere else");
            AssertSame(target, flight.Position(MissileFlight.Seconds), "it is not on the town when its time is up");
            AssertSame(target, flight.Position(MissileFlight.Seconds + 1f), "it flies on after the hit");
            Vector3 last = flight.Heading(MissileFlight.Seconds);
            Assert.Less(last.y, -0.7f, "it comes down on the town at a shallow slant");
            Assert.Greater(last.x * (target.x - Mouth.x) + last.z * (target.z - Mouth.z), 0f, "it comes down from beyond the town");

            // It rises well over the buildings, but not out of the picture.
            float top = flight.Top();
            Assert.That(top - Mouth.y, Is.InRange(1.2f * Tile, MissileFlight.Climb * Tile), "the top of its way");

            // Slow off the ground and fastest at the hit, never standing still or going back.
            Assert.AreEqual(0f, MissileFlight.Along(0f), 1e-6f);
            Assert.AreEqual(0f, MissileFlight.Along(-1f), 1e-6f);
            Assert.AreEqual(1f, MissileFlight.Along(MissileFlight.Seconds), 1e-6f);
            Assert.Less(MissileFlight.Along(MissileFlight.Seconds * 0.5f), 0.4f, "it is half way in half its time: it should gather speed");
            float before = -1f;
            for (int i = 0; i <= 50; i++)
            {
                float along = MissileFlight.Along(MissileFlight.Seconds * i / 50f);
                Assert.Greater(along, before, "it stands still or goes back");
                before = along;
            }

            float first = (flight.Position(0.15f) - flight.Position(0.05f)).magnitude;
            float end = (flight.Position(MissileFlight.Seconds) - flight.Position(MissileFlight.Seconds - 0.1f)).magnitude;
            Assert.Greater(end, first * 3f, "it strikes no faster than it lifts off");

            // Its nose leads and its tail follows, a length apart, and once out it stays over the ground.
            for (int i = 0; i <= 40; i++)
            {
                float time = MissileFlight.Seconds * i / 40f;
                Assert.AreEqual(length, (flight.Nose(time) - flight.Tail(time)).magnitude, 1e-3f);
                Assert.AreEqual(1f, flight.Heading(time).magnitude, 1e-3f);
                if (time > 0.5f) Assert.Greater(flight.Position(time).y, Mathf.Min(Mouth.y, target.y) - 1e-3f, $"it dips under the table at {time}");
            }

            Assert.That(MissileFlight.Seconds, Is.InRange(1.2f, 2.2f), "long enough to follow with the eye, short enough not to hold the game up");
            Assert.That(MissileFlight.OpenSeconds, Is.InRange(0.2f, 0.5f), "the lid is open before it comes out");
        }

        [Test]
        public void AStrikeLaysItsSmokeItsTrailAndItsBlastByTheClockAndClearsItselfAway()
        {
            var holder = new GameObject("Strike Test");
            try
            {
                MissileStrike strike = MissileStrike.Play(holder.transform, Mouth, Foot, Width, Height, Tile, Color.red, false, 5);
                Assert.IsFalse(strike.Blocked);
                Assert.AreEqual(MissileStrike.FlightParticles + MissileStrike.HitParticles, strike.Particles.Count);
                Assert.AreEqual(MissileFlight.OpenSeconds, strike.LaunchAt, 1e-5f);
                Assert.AreEqual(MissileFlight.OpenSeconds + MissileFlight.Seconds, strike.ImpactAt, 1e-5f);
                AssertSame(Foot + Vector3.up * (Height * 0.5f), strike.Flight.To, "it comes down on the middle of the building");
                Assert.AreEqual(MissileStrike.Size * Tile, strike.Flight.Length, 1e-5f);
                Assert.That(MissileStrike.TrailPuffs, Is.InRange(25, 60), "a trail without gaps, but not a hundred quads");

                // Nothing is in the air before the lid is open; the hit's pieces wait for the hit.
                Assert.AreEqual(0, strike.Showing(strike.LaunchAt - 0.01f), "smoke before the lid is open");
                Assert.Greater(strike.Showing(strike.LaunchAt + 0.4f), MissileStrike.LaunchPuffs, "no smoke as it lifts off");
                float hit = strike.ImpactAt;
                int stars = 0, flames = 0, smokes = 0, scorches = 0, trail = 0;
                foreach (SoftParticle particle in strike.Particles)
                {
                    Assert.Greater(particle.Life, 0f);
                    bool ofTheHit = particle.Born >= hit - 0.03f;
                    bool fire = Same(particle.Color, MissileStrike.Fire) || Same(particle.Color, MissileStrike.FireRim) || Same(particle.Color, MissileStrike.FireLight);
                    if (fire) Assert.IsTrue(ofTheHit, "there is fire in the air before the hit");
                    if (!ofTheHit)
                    {
                        Assert.Less(particle.Born + particle.Life, hit + 1f, "the smoke of the flight hangs about long after the blast");
                        if (particle.Sprite == SoftSprite.Cloud && particle.Born > strike.LaunchAt + 0.35f) trail++;
                        continue;
                    }

                    if (particle.Sprite == SoftSprite.Star)
                    {
                        stars++;
                        Assert.Less(particle.Born, hit + 0.05f, "the burst star is late");
                        Assert.Less(particle.Life, 0.5f, "the burst star is gone in a blink");
                        AssertSame(strike.Flight.To, particle.From, "the burst star is where the missile struck");
                        Assert.Greater(particle.Fore, 0f, "the burst is drawn before the building it takes");
                    }

                    if (particle.Sprite == SoftSprite.Cloud && Same(particle.Color, MissileStrike.Fire))
                    {
                        flames++;
                        Assert.Less(particle.Born + particle.Life, hit + 0.8f, "the fire is out quickly");
                    }

                    if (particle.Sprite == SoftSprite.Cloud && Same(particle.Color, MissileStrike.Smoke))
                    {
                        smokes++;
                        Assert.Greater(particle.Born, hit + 0.15f, "the smoke comes after the fire");
                        Assert.Greater(particle.Position(1f).y, particle.From.y + 0.3f * Tile, "the smoke rises");
                    }

                    if (Same(particle.Color, MissileStrike.Scorch))
                    {
                        scorches++;
                        Assert.IsTrue(particle.Flat, "the scorch lies on the tile");
                        Assert.Greater(particle.Life, 2f, "the scorch stays a while after the fire");
                        Assert.LessOrEqual(particle.SizeTo, 1.1f * Tile, "the scorch stays on its tile");
                    }
                }

                Assert.AreEqual(3, stars, "a burst star of three tones");
                Assert.AreEqual(MissileStrike.Flames, flames);
                Assert.AreEqual(MissileStrike.Smokes, smokes);
                Assert.AreEqual(1, scorches);
                Assert.Greater(trail, MissileStrike.TrailPuffs / 2, "the trail is laid along the whole flight");

                // The missile itself: out of sight until the lid is open, then on its way, gone at the hit.
                Assert.IsFalse(strike.Launched);
                Assert.IsFalse(strike.Missile.gameObject.activeSelf, "it shows before the lid is open");
                strike.Step(strike.LaunchAt + 0.6f);
                Assert.IsTrue(strike.Launched);
                Assert.IsFalse(strike.Struck);
                Assert.IsTrue(strike.Missile.gameObject.activeSelf, "it is not seen flying");
                AssertSame(strike.Flight.Position(0.6f), strike.Missile.localPosition, "it is not on its way");
                AssertSame(Vector3.one * (MissileStrike.Size * Tile), strike.Missile.localScale, "its size");
                strike.Step(MissileFlight.Seconds - 0.6f + 0.02f);
                Assert.IsTrue(strike.Struck);
                Assert.IsTrue(strike.Missile == null, "the missile is still there after the hit");
                Assert.IsFalse(strike == null, "the blast is gone with the missile");
                Assert.Greater(strike.Showing(strike.Age + 0.1f), MissileStrike.Flames, "nothing bursts at the hit");

                // The smoke and the scorch outlast the hit, but not by long; then nothing is left.
                Assert.That(strike.Seconds - hit, Is.InRange(1.5f, 3.2f));
                strike.Step(strike.Seconds);
                Assert.IsTrue(strike == null, "the strike stays after its time");
                Assert.AreEqual(0, holder.transform.childCount, "the strike leaves something behind");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void AMissileThatMeetsAShieldBurstsOverTheRoofWithoutFire()
        {
            var holder = new GameObject("Strike Test");
            try
            {
                MissileStrike strike = MissileStrike.Play(holder.transform, Mouth, Foot, Width, Height, Tile, Color.red, true, 5);
                Assert.IsTrue(strike.Blocked);
                Assert.AreEqual(MissileStrike.FlightParticles + MissileStrike.BlockedParticles, strike.Particles.Count);
                Assert.Greater(strike.Flight.To.y, Foot.y + Height, "it bursts on the shield over the roof, not in the building");
                int glow = 0;
                foreach (SoftParticle particle in strike.Particles)
                {
                    Assert.IsFalse(Same(particle.Color, MissileStrike.Fire) || Same(particle.Color, MissileStrike.FireRim), "fire on a shield");
                    Assert.IsFalse(Same(particle.Color, MissileStrike.Smoke) || Same(particle.Color, MissileStrike.Scorch), "a shield leaves smoke or a scorch");
                    if (particle.Born < strike.ImpactAt - 0.03f) continue;
                    Assert.GreaterOrEqual(particle.From.y, Foot.y + Height, "the burst is over the roof");
                    for (int i = 0; i <= 10; i++)
                    {
                        Vector3 at = particle.Position(i / 10f);
                        Assert.Less(new Vector2(at.x - Foot.x, at.z - Foot.z).magnitude, 1.2f * Tile, "the burst flies far from the town");
                    }

                    if (Same(particle.Color, MissileStrike.ShieldGlow)) glow++;
                }

                Assert.AreEqual(2 + MissileStrike.ShieldSparks, glow, "the rings and the sparks are the shield's blue");
                Assert.Less(strike.Seconds, strike.ImpactAt + 1.2f, "a blocked missile is over quickly");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void TheFlameCatchesAsItLiftsOffAndFlickers()
        {
            Assert.AreEqual(0f, MissileStrike.FlameScale(0f).y, 1e-5f, "no flame inside the hatch");
            float least = float.MaxValue, most = 0f;
            for (int i = 0; i <= 200; i++)
            {
                Vector3 flame = MissileStrike.FlameScale(0.2f + i * 0.007f);
                least = Mathf.Min(least, flame.y);
                most = Mathf.Max(most, flame.y);
                Assert.That(flame.x, Is.InRange(0.9f, 1.1f), "the flame keeps its width");
                Assert.AreEqual(flame.x, flame.z, 1e-6f);
            }

            Assert.Greater(least, 0.55f, "the flame goes out in flight");
            Assert.Less(most, 1.15f);
            Assert.Greater(most - least, 0.2f, "the flame does not flicker");
        }

        [Test]
        public void ABuildingBlownUpSwellsAtTheHitAndIsGoneIntoTheFire()
        {
            Assert.AreEqual(LeavingMotion.BlastedSeconds, LeavingMotion.Seconds(Leaving.Blasted));
            Assert.That(LeavingMotion.BlastedSeconds, Is.InRange(0.3f, 0.5f), "gone while the ball of fire still hides it");

            LeavingPose start = LeavingMotion.Blasted(0f);
            AssertSame(Vector3.one, start.Scale, "it starts at its own size");
            Assert.AreEqual(0f, start.Lift, 1e-4f);
            Assert.AreEqual(0f, start.Sink, 1e-4f);

            LeavingPose struck = LeavingMotion.Blasted(LeavingMotion.Hit - 1e-4f);
            Assert.AreEqual(1f + LeavingMotion.Swell, struck.Scale.x, 1e-2f, "it swells at the hit");
            Assert.AreEqual(struck.Scale.x, struck.Scale.z, 1e-5f);
            Assert.AreEqual(0f, struck.Lift, 1e-4f, "it is still on its tile when it has swollen");
            Assert.AreEqual(0f, struck.Shake, 1e-2f, "the shake has died away");
            float shaken = 0f;
            for (int i = 0; i <= 50; i++) shaken = Mathf.Max(shaken, Mathf.Abs(LeavingMotion.Blasted(LeavingMotion.Hit * i / 50f).Shake));
            Assert.Greater(shaken, 0.3f, "the hit does not shake it");

            float size = float.MaxValue;
            for (int i = 0; i <= 50; i++)
            {
                LeavingPose pose = LeavingMotion.Blasted(LeavingMotion.Hit + (1f - LeavingMotion.Hit) * i / 50f);
                Assert.LessOrEqual(pose.Scale.y, size + 1e-5f, "it grows again as it goes");
                Assert.LessOrEqual(pose.Sink, 1e-5f, "it goes into the ground instead of into the fire");
                size = pose.Scale.y;
            }

            LeavingPose end = LeavingMotion.Blasted(1f);
            Assert.Less(end.Scale.x, 0.05f, "it is still there at the end");
            Assert.AreEqual(1f, end.Lift, 1e-4f, "it is thrown up");
            Assert.AreEqual(LeavingMotion.BlastTurn, end.Turn, 1e-3f);
            Assert.AreEqual(LeavingMotion.BlastLean, end.Tilt, 1e-3f);
            AssertSame(LeavingMotion.Blasted(0.7f).Scale, LeavingMotion.At(Leaving.Blasted, 0.7f).Scale, "At");
        }

        [Test]
        public void ABlastThrowsMoreRubbleHigherAndFurtherThanADemolition()
        {
            var holder = new GameObject("Burst Test");
            try
            {
                LeavingBurst blast = LeavingBurst.Play(holder.transform, Leaving.Blasted, Foot, Width, Height, Tile, 0f, Color.white, 3, 6f, Color.blue);
                Assert.AreEqual(Leaving.Blasted, blast.How);
                Assert.AreEqual(LeavingBurst.BlastedParticles, blast.Particles.Count);
                Assert.AreEqual(LeavingBurst.BlastRubble, blast.RubbleCount);
                Assert.Greater(LeavingBurst.BlastRubble, LeavingBurst.Rubble, "a missile leaves no more rubble than a bulldozer");
                foreach (SoftParticle particle in blast.Particles)
                {
                    // Dust only: the fire is the missile's own.
                    Assert.IsFalse(Same(particle.Color, MissileStrike.Fire), "the building's own burst has fire in it");
                    for (int i = 0; i <= 10; i++) Assert.GreaterOrEqual(particle.Position(i / 10f).y, Foot.y - 1e-4f, $"a {particle.Sprite} goes under the ground");
                }

                // Its rubble is in the air at once and flies higher than the roof.
                float highest = 0f;
                int shown = 0;
                for (int step = 0; step < 30; step++)
                {
                    blast.Step(0.02f);
                    for (int i = 0; i < blast.RubbleCount; i++)
                    {
                        if (!blast.RubbleShown(i)) continue;
                        if (step == 10) shown++;
                        highest = Mathf.Max(highest, blast.RubblePlace(i).y - Foot.y);
                        Assert.GreaterOrEqual(blast.RubblePlace(i).y, Foot.y - 1e-3f, "rubble goes under the table");
                    }
                }

                Assert.AreEqual(LeavingBurst.BlastRubble, shown, "all the rubble is thrown at the hit");
                Assert.Greater(highest, Height * 1.3f, "the rubble is not thrown over the roof");
                Assert.That(blast.Seconds, Is.InRange(1.2f, 3.2f));
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void TheBurstStarHasSharpPointsAndDeepNotches()
        {
            Assert.AreEqual(1f, SoftSprites.Alpha(SoftSprite.Star, 0f, 0f), 1e-4f, "a star is solid at its heart");
            float step = 2f * Mathf.PI / SoftSprites.StarPoints;
            Assert.AreEqual(SoftSprites.StarReach, SoftSprites.StarRadius(0f), 1e-4f, "a long point");
            Assert.Less(SoftSprites.StarRadius(step), SoftSprites.StarReach, "every other point is shorter");
            Assert.Greater(SoftSprites.StarRadius(step), SoftSprites.StarNotch + 0.15f, "a short point is still a point");
            Assert.AreEqual(SoftSprites.StarNotch, SoftSprites.StarRadius(step * 0.5f), 1e-4f, "a notch between two points");
            Assert.Less(SoftSprites.StarReach, 0.95f, "its points stay inside its square");
            for (int i = 0; i < SoftSprites.StarPoints; i++)
            {
                float point = step * i, notch = step * (i + 0.5f);
                float tip = SoftSprites.StarRadius(point) - 0.08f, between = SoftSprites.StarNotch + 0.1f;
                Assert.AreEqual(1f, SoftSprites.Alpha(SoftSprite.Star, Mathf.Cos(point) * tip, Mathf.Sin(point) * tip), 1e-3f, $"point {i} is solid near its tip");
                Assert.AreEqual(0f, SoftSprites.Alpha(SoftSprite.Star, Mathf.Cos(notch) * between, Mathf.Sin(notch) * between), 1e-3f, $"notch {i} is clear");
            }
        }

        private static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;

        private static void AssertSame(Vector3 expected, Vector3 actual, string what)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-3f, what);
            Assert.AreEqual(expected.y, actual.y, 1e-3f, what);
            Assert.AreEqual(expected.z, actual.z, 1e-3f, what);
        }
    }
}
