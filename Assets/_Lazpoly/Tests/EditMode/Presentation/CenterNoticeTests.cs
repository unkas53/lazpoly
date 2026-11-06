using Lazpoly.Presentation.Game;
using NUnit.Framework;

namespace Lazpoly.Presentation.Tests
{
    public class CenterNoticeTests
    {
        [Test]
        public void TheCardStaysFiveSecondsAndFadesOnlyInTheLastHalfSecond()
        {
            Assert.AreEqual(5f, CenterNotice.Seconds);
            Assert.AreEqual(0.5f, CenterNotice.FadeOutSeconds);
            Assert.AreEqual(1f, CenterNotice.Alpha(1f));
            Assert.AreEqual(1f, CenterNotice.Alpha(4.5f), 1e-4f, "Fully there until the last half second.");
            Assert.AreEqual(0.5f, CenterNotice.Alpha(4.75f), 1e-4f);
            Assert.Less(CenterNotice.Alpha(4.99f), 0.05f);
            Assert.AreEqual(0f, CenterNotice.Alpha(5f));
            Assert.IsTrue(CenterNotice.Showing(4.99f));
            Assert.IsFalse(CenterNotice.Showing(5f), "Gone at five seconds, when the table goes on.");
            Assert.IsFalse(CenterNotice.Showing(-1f));
        }

        [Test]
        public void TheCardFadesInQuickly()
        {
            Assert.AreEqual(0f, CenterNotice.Alpha(0f));
            Assert.AreEqual(1f, CenterNotice.Alpha(CenterNotice.FadeInSeconds), 1e-4f);
        }
    }
}
