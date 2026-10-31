using System.Collections.Generic;
using Lazpoly.App.Session;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Events;
using Lazpoly.Core.Random;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;
using NUnit.Framework;

namespace Lazpoly.App.Tests
{
    public class LocalGameSessionTests
    {
        private static LocalGameSession NewSession()
        {
            var board = new BoardDefinition(new[]
            {
                SquareDefinition.Start("Start"),
                SquareDefinition.Rest("A"),
                SquareDefinition.Rest("B"),
                SquareDefinition.Rest("C"),
            });
            var rules = new RulesConfig(1000, 100, 6, 3, 2);
            var seats = new[] { new PlayerSeat(1, "Bir"), new PlayerSeat(2, "İki") };
            return new LocalGameSession(new GameEngine(board, rules, seats, new Pcg32Random(5UL)));
        }

        [Test]
        public void AcceptedCommandPublishesItsEventsOnce()
        {
            LocalGameSession session = NewSession();
            var published = new List<IReadOnlyList<GameEvent>>();
            session.EventsPublished += events => published.Add(events);

            CommandResult result = session.Submit(new RollDiceCommand(1));

            Assert.IsTrue(result.IsAccepted);
            Assert.AreEqual(1, published.Count);
            Assert.AreSame(result.Events, published[0]);
            Assert.IsInstanceOf<DiceRolledEvent>(published[0][0]);
        }

        [Test]
        public void RejectedCommandPublishesNothing()
        {
            LocalGameSession session = NewSession();
            int publishCount = 0;
            session.EventsPublished += _ => publishCount++;

            CommandResult result = session.Submit(new RollDiceCommand(2));

            Assert.AreEqual(RejectionReason.NotYourTurn, result.Reason);
            Assert.AreEqual(0, publishCount);
        }

        [Test]
        public void SessionExposesTheEngineState()
        {
            LocalGameSession session = NewSession();
            session.Submit(new RollDiceCommand(1));

            Assert.AreEqual(4, session.Board.Count);
            Assert.AreNotEqual(0, session.State.LastDie1);
        }
    }
}
