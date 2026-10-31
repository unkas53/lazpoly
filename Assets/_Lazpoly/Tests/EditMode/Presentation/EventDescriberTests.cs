using Lazpoly.Core.Board;
using Lazpoly.Core.Events;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Texts;
using NUnit.Framework;

namespace Lazpoly.Presentation.Tests
{
    public class EventDescriberTests
    {
        private static readonly BoardDefinition Board = new BoardDefinition(new[]
        {
            SquareDefinition.Start("Başlangıç"),
            SquareDefinition.Property("Lale Sokağı", 60, 4, 0),
        });

        private static GameState State() => GameState.CreateInitial(Board, new RulesConfig(1000, 100, 6, 3, 2),
            new[] { new PlayerSeat(1, "Ayşe"), new PlayerSeat(2, "Can") });

        [Test]
        public void DescribesDiceMovesAndMoney()
        {
            GameState state = State();

            Assert.AreEqual("Ayşe zar attı: 3 + 3 (çift)", EventDescriber.Describe(new DiceRolledEvent(1, 3, 3), state, Board));
            Assert.AreEqual("Can → Lale Sokağı", EventDescriber.Describe(new PlayerMovedEvent(2, 0, 1, false), state, Board));
            Assert.AreEqual("Ayşe: -4 (kira)", EventDescriber.Describe(new MoneyChangedEvent(1, -4, MoneyReason.Rent), state, Board));
            Assert.AreEqual("Can: +4 (kira)", EventDescriber.Describe(new MoneyChangedEvent(2, 4, MoneyReason.Rent), state, Board));
        }

        [Test]
        public void DescribesOwnershipAndEnding()
        {
            GameState state = State();

            Assert.AreEqual("Lale Sokağı → Ayşe", EventDescriber.Describe(new PropertyOwnerChangedEvent(1, -1, 1), state, Board));
            Assert.AreEqual("Lale Sokağı bankaya döndü",
                EventDescriber.Describe(new PropertyOwnerChangedEvent(1, 1, GameState.NoOwner), state, Board));
            Assert.AreEqual("Oyun bitti! Kazanan: Can", EventDescriber.Describe(new GameEndedEvent(2), state, Board));
        }

        [Test]
        public void PhaseChangesHaveNoLogLine()
        {
            Assert.IsNull(EventDescriber.Describe(new PhaseChangedEvent(GamePhase.TurnEnd), State(), Board));
        }
    }
}
