using System.Collections.Generic;
using System.Linq;
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
            var rules = new RulesConfig(1000, 100, 6, 3, 2, 50);
            var seats = new[] { new PlayerSeat(1, "Bir"), new PlayerSeat(2, "İki") };
            return new LocalGameSession(new GameEngine(board, rules, seats, new Pcg32Random(5UL)));
        }

        [Test]
        public void TheGameTimerReportsEveryTenSecondsAndAtTheLimit()
        {
            var timer = new GameTimer(0);
            Assert.AreEqual(-1, timer.Add(0.5, 0));
            for (int i = 0; i < 18; i++) Assert.AreEqual(-1, timer.Add(0.5, 0), "Under ten seconds nothing is due.");
            Assert.AreEqual(10, timer.Add(0.5, 0));
            Assert.AreEqual(-1, timer.Add(30, 0), "A long hitch adds at most a second.");
            Assert.AreEqual(11, (int)timer.Played);

            var nearTheEnd = new GameTimer(55);
            Assert.AreEqual(-1, nearTheEnd.Add(1, 60));
            for (int i = 0; i < 3; i++) nearTheEnd.Add(1, 60);
            Assert.AreEqual(60, nearTheEnd.Add(1, 60), "The limit is reported the moment it is reached.");
            Assert.AreEqual(-1, nearTheEnd.Add(1, 60));
            Assert.AreEqual(-1, timer.Add(-1, 0));
            Assert.AreEqual(-1, timer.Add(double.NaN, 0));
        }

        [Test]
        public void PlayTimeReachesTheStateAsClockEvents()
        {
            LocalGameSession session = NewSession();
            var published = new List<IReadOnlyList<GameEvent>>();
            session.EventsPublished += events => published.Add(events);

            for (int i = 0; i < 25; i++) session.AddPlayTime(0.5);
            Assert.AreEqual(1, published.Count);
            Assert.IsInstanceOf<GameClockEvent>(published[0].Single());
            Assert.AreEqual(GameTimer.ReportSeconds, session.State.PlayedSeconds);
        }

        [Test]
        public void AcceptedCommandPublishesItsEventsOnce()
        {
            LocalGameSession session = NewSession();
            var published = new List<IReadOnlyList<GameEvent>>();
            session.EventsPublished += events => published.Add(events);

            var rejections = new List<RejectionReason>();
            session.CommandRejected += rejections.Add;

            session.Submit(new RollDiceCommand(1));

            Assert.IsEmpty(rejections);
            Assert.AreEqual(1, published.Count);
            Assert.IsInstanceOf<DiceRolledEvent>(published[0][0]);
        }

        [Test]
        public void RejectedCommandPublishesNothingAndReportsWhy()
        {
            LocalGameSession session = NewSession();
            int publishCount = 0;
            session.EventsPublished += _ => publishCount++;
            var rejections = new List<RejectionReason>();
            session.CommandRejected += rejections.Add;

            session.Submit(new RollDiceCommand(2));

            CollectionAssert.AreEqual(new[] { RejectionReason.NotYourTurn }, rejections);
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

        [Test]
        public void EveryoneAtTheTableCanBeControlledLocally()
        {
            LocalGameSession session = NewSession();

            Assert.IsTrue(session.IsReady);
            Assert.IsTrue(session.CanControl(1));
            Assert.IsTrue(session.CanControl(2));
            Assert.IsFalse(session.CanControl(3));
        }
    }
}
