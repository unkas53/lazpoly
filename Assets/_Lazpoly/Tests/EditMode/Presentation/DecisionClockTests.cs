using Lazpoly.Presentation.Game;
using NUnit.Framework;

namespace Lazpoly.Presentation.Tests
{
    public class DecisionClockTests
    {
        [Test]
        public void RunsOutOnceAndStartsAgainForTheNextDecision()
        {
            var clock = new DecisionClock();
            Assert.IsFalse(clock.Tick(0, 1, 15, false));
            Assert.AreEqual(15, clock.Left, 1e-9);

            Assert.IsFalse(clock.Tick(14.5, 1, 15, false));
            Assert.AreEqual(0.5, clock.Left, 1e-9);
            Assert.IsTrue(clock.Tick(1, 1, 15, false), "The time ran out.");
            Assert.AreEqual(0, clock.Left);
            Assert.IsFalse(clock.Tick(1, 1, 15, false), "It fires only once for the same decision.");

            Assert.IsFalse(clock.Tick(1, 2, 15, false), "A new decision gets the full time.");
            Assert.AreEqual(14, clock.Left, 1e-9);
        }

        [Test]
        public void StandsStillWhileHeldAndNeverRunsWithoutALimit()
        {
            var clock = new DecisionClock();
            clock.Tick(5, 1, 10, false);
            Assert.IsFalse(clock.Tick(100, 1, 10, true), "Paused or animating: no time passes.");
            Assert.AreEqual(5, clock.Left, 1e-9);

            Assert.IsFalse(clock.Tick(1e6, 3, double.PositiveInfinity, false));
            Assert.IsTrue(double.IsPositiveInfinity(clock.Left));
        }
    }
}
