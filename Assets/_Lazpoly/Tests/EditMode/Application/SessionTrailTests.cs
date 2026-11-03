using System.Linq;
using Lazpoly.App.Session;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Events;
using Lazpoly.Core.State;
using NUnit.Framework;

namespace Lazpoly.App.Tests
{
    public class SessionTrailTests
    {
        [Test]
        public void KeepsOnlyTheNewestLinesNumbered()
        {
            var trail = new SessionTrail(3);
            for (int i = 1; i <= 5; i++) trail.Add($"line {i}");

            Assert.AreEqual(3, trail.Lines.Count);
            CollectionAssert.AreEqual(new[] { "   3  line 3", "   4  line 4", "   5  line 5" }, trail.Lines.ToArray());
            trail.Add("   ");
            Assert.AreEqual(3, trail.Lines.Count, "Blank lines are not worth a slot.");
        }

        [Test]
        public void DescribesCommandsAndCountsEventRuns()
        {
            var trail = new SessionTrail();
            trail.Command(new BuildCommand(2, 7));
            trail.Command(new ChooseDiceCommand(1, 1), RejectionReason.InvalidChoice);
            trail.Command(new ProposeTradeCommand(new TradeOffer(1, 2, 50, 0, new[] { 3 }, null)));
            trail.Events(new GameEvent[]
            {
                new DiceRolledEvent(1, 2, 3), new MoneyChangedEvent(1, -5, MoneyReason.Tax), new MoneyChangedEvent(2, 5, MoneyReason.Rent),
                new PhaseChangedEvent(GamePhase.TurnEnd),
            });

            string[] lines = trail.Lines.ToArray();
            StringAssert.Contains("> Build(p2, sq7) -> ok", lines[0]);
            StringAssert.Contains("> ChooseDice(p1, 1) -> REJECTED InvalidChoice", lines[1]);
            StringAssert.Contains("> ProposeTrade(p1->p2, give 50+1sq, take 0+0sq) -> ok", lines[2]);
            StringAssert.Contains("< DiceRolled MoneyChangedx2 PhaseChanged", lines[3]);
        }
    }
}
