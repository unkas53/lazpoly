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

        private static GameState State() => GameState.CreateInitial(Board, new RulesConfig(1000, 100, 6, 3, 2, 50),
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
        public void DescribesEconomyEvents()
        {
            GameState state = State();

            Assert.AreEqual("Lale Sokağı: 2 ev", EventDescriber.Describe(new BuildingLevelChangedEvent(1, 2), state, Board));
            Assert.AreEqual("Lale Sokağı: otel", EventDescriber.Describe(new BuildingLevelChangedEvent(1, 5), state, Board));
            Assert.AreEqual("Ayşe: +30 (mülk satışı)", EventDescriber.Describe(new MoneyChangedEvent(1, 30, MoneyReason.PropertySale), state, Board));
            Assert.AreEqual("Ayşe, Can oyuncusuna takas teklif etti",
                EventDescriber.Describe(new TradeProposedEvent(new TradeOffer(1, 2, 50, 0, new int[0], new[] { 1 })), state, Board));
            Assert.AreEqual("Takas reddedildi", EventDescriber.Describe(new TradeResolvedEvent(TradeOutcome.Rejected), state, Board));
            Assert.AreEqual("Ayşe 200 ödeyemiyor (kira): para bulmalı ya da iflas etmeli",
                EventDescriber.Describe(new DebtIncurredEvent(1, 2, 200, MoneyReason.Rent), state, Board));
        }

        [Test]
        public void PhaseChangesHaveNoLogLine()
        {
            Assert.IsNull(EventDescriber.Describe(new PhaseChangedEvent(GamePhase.TurnEnd), State(), Board));
        }

        [Test]
        public void EachLineKnowsWhoseItIs()
        {
            Assert.AreEqual(2, EventDescriber.ActorOf(new TurnStartedEvent(2)));
            Assert.AreEqual(1, EventDescriber.ActorOf(new MoneyChangedEvent(1, -4, MoneyReason.Rent)), "The payer's line, not the landlord's.");
            Assert.AreEqual(1, EventDescriber.ActorOf(new PropertyOwnerChangedEvent(1, GameState.NoOwner, 1)), "A bought town is the buyer's line.");
            Assert.AreEqual(GameState.NoOwner, EventDescriber.ActorOf(new PropertyOwnerChangedEvent(1, 1, GameState.NoOwner)), "A town back at the bank is nobody's.");
            Assert.AreEqual(GameState.NoOwner, EventDescriber.ActorOf(new PhaseChangedEvent(GamePhase.TurnEnd)));
        }
    }
}
