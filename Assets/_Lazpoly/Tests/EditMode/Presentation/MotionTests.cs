using Lazpoly.Presentation.Effects;
using NUnit.Framework;

namespace Lazpoly.Presentation.Tests
{
    public class MotionTests
    {
        [Test]
        public void EasingCurvesStartAtZeroAndEndAtOne()
        {
            foreach (System.Func<float, float> curve in new System.Func<float, float>[] { Ease.OutCubic, Ease.InCubic, Ease.OutBack, Ease.OutBounce })
            {
                Assert.AreEqual(0f, curve(0f), 1e-4f);
                Assert.AreEqual(1f, curve(1f), 1e-4f);
                Assert.AreEqual(curve(1f), curve(3f), 1e-4f, "clamped past the end");
            }
        }

        [Test]
        public void OutBackOvershootsAndBumpComesBack()
        {
            Assert.Greater(Ease.OutBack(0.7f), 1f);
            Assert.AreEqual(0f, Ease.Bump(0f), 1e-4f);
            Assert.AreEqual(1f, Ease.Bump(0.5f), 1e-4f);
            Assert.AreEqual(0f, Ease.Bump(1f), 1e-4f);
        }

        [Test]
        public void RollingNumberCountsToTheTargetInTime()
        {
            var money = new RollingNumber(1000);
            Assert.AreEqual(1000, money.Shown);
            Assert.AreEqual(0, money.Direction);

            money.SetTarget(1200);
            Assert.AreEqual(1, money.Direction);
            Assert.IsTrue(money.Step(RollingNumber.Seconds / 3f));
            Assert.That(money.Shown, Is.InRange(1001, 1199));

            money.Step(RollingNumber.Seconds);
            Assert.AreEqual(1200, money.Shown);
            Assert.AreEqual(0, money.Direction);
            Assert.IsFalse(money.Step(1f));
        }

        [Test]
        public void RollingNumberRetargetsFromWhereItIs()
        {
            var money = new RollingNumber(500);
            money.SetTarget(0);
            money.Step(RollingNumber.Seconds / 2f);
            int halfway = money.Shown;
            Assert.AreEqual(-1, money.Direction);

            money.SetTarget(800);
            Assert.AreEqual(halfway, money.Shown, "no jump when the target changes");
            Assert.AreEqual(1, money.Direction);
            money.Step(RollingNumber.Seconds);
            Assert.AreEqual(800, money.Shown);
        }
    }
}
