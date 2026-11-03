using Lazpoly.Presentation.Effects;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    public class BubbleQueueTests
    {
        [Test]
        public void LinesGoUpOneAtATimeAndWaitToBeRead()
        {
            var queue = new BubbleQueue();
            Assert.IsFalse(queue.ShouldAdvance(false, 0f), "nothing to say");
            queue.Add(2, "Ben yokum.");
            queue.Add(3, "Ben yokum.");
            Assert.IsTrue(queue.ShouldAdvance(false, 0f), "the table is free");
            Assert.AreEqual((2, "Ben yokum."), queue.Take());
            Assert.IsFalse(queue.ShouldAdvance(true, 0.5f), "the bubble on show is read first");
            Assert.IsTrue(queue.ShouldAdvance(true, BubbleQueue.MinShowSeconds));
            Assert.AreEqual(3, queue.Take().PlayerId);
            Assert.AreEqual(0, queue.Waiting);
        }

        [Test]
        public void ANewerLineReplacesTheOldAndTheQueueStaysShort()
        {
            var queue = new BubbleQueue();
            queue.Add(2, "Artırıyorum!");
            queue.Add(2, "Benim oldu!");
            Assert.AreEqual(1, queue.Waiting, "one line per player");
            Assert.AreEqual("Benim oldu!", queue.Take().Text);

            for (int player = 1; player <= BubbleQueue.MaxWaiting + 2; player++) queue.Add(player, "Ben yokum.");
            Assert.AreEqual(BubbleQueue.MaxWaiting, queue.Waiting);
            Assert.AreEqual(3, queue.Take().PlayerId, "the oldest lines went");

            queue.Forget(4);
            Assert.AreEqual(1, queue.Waiting);
            queue.Add(7, "");
            Assert.AreEqual(1, queue.Waiting, "an empty line is not said");
        }

        [Test]
        public void MoneyPopupsStackInsteadOfOverlapping()
        {
            var at = new Vector3(1f, 0f, 1f);
            Assert.AreEqual(0, FloatingText.StackIndex(new Vector3[0], new float[0], at));
            Vector3[] starts = { at, at + Vector3.up * FloatingText.LineGap, new Vector3(5f, 0f, 5f) };
            float[] ages = { 0.1f, 0.2f, 0.1f };
            Assert.AreEqual(2, FloatingText.StackIndex(starts, ages, at), "two fresh lines here, the third goes above them");
            ages[0] = ages[1] = FloatingText.StackSeconds + 0.1f;
            Assert.AreEqual(0, FloatingText.StackIndex(starts, ages, at), "lines that have risen away leave the spot free");

            // Even the third line of a stack ends its rise under the speech bubble over the figure's head.
            float top = Pawns.PawnView.CharacterHeight * Game.GameSceneController.MoneyHeight + 2 * FloatingText.LineGap + FloatingText.Rise;
            Assert.Less(top, SpeechBubble.Lift, "money lines stay under the bubbles");
        }
    }
}
