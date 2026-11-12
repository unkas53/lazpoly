using System.Collections.Generic;
using Lazpoly.App.AI;
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
    /// <summary>Faz 30: how a computer player uses the strategic cards in its hand, each from a hand-made situation.</summary>
    public class AiCardTests
    {
        private const int Me = 1;
        private const int Other = 2;
        private const int Third = 3;
        private const int A1 = 1, A2 = 2, B1 = 4, B2 = 5;

        private const int Lookout = 0, Shield = 1, Demolish = 2, ForceSale = 3, Fund = 4, RentShield = 5, Strike = 6;

        private static readonly BoardDefinition Board = new BoardDefinition(new[]
        {
            SquareDefinition.Start("Start"),
            SquareDefinition.Property("A1", 100, 10, 1, 50, new[] { 50, 150, 400, 550, 700 }),
            SquareDefinition.Property("A2", 100, 10, 1, 50, new[] { 50, 150, 400, 550, 700 }),
            SquareDefinition.Card("Chance"),
            SquareDefinition.Property("B1", 200, 20, 2, 100, new[] { 100, 300, 800, 1100, 1400 }),
            SquareDefinition.Property("B2", 200, 20, 2, 100, new[] { 100, 300, 800, 1100, 1400 }),
            SquareDefinition.Rest("Rest"),
            SquareDefinition.Rest("Rest 2"),
        }, new[]
        {
            CardDefinition.Lookout("Spyglass"),
            CardDefinition.Strategic("Shield", CardKind.Shield),
            CardDefinition.Strategic("Demolish", CardKind.Demolish),
            CardDefinition.Strategic("Force sale", CardKind.ForceSale),
            CardDefinition.Fund("Fund", 150, 200, 500),
            CardDefinition.Strategic("Rent shield", CardKind.RentShield),
            CardDefinition.Strategic("Strike", CardKind.Strike, 3),
        });

        private static readonly RulesConfig Rules = new RulesConfig(1000, 0, 6, 3, 2, 50);

        private sealed class NoMistakes : IRandomSource
        {
            public uint NextUInt() => uint.MaxValue;
            public int NextInt(int minInclusive, int maxExclusive) => maxExclusive - 1;
        }

        private static GameState NewState()
        {
            var seats = new List<PlayerSeat> { new PlayerSeat(Me, "Me"), new PlayerSeat(Other, "Other"), new PlayerSeat(Third, "Third") };
            return GameState.CreateInitial(Board, Rules, seats);
        }

        private static void Apply(GameState state, params GameEvent[] events)
        {
            foreach (GameEvent gameEvent in events) GameStateReducer.Apply(state, gameEvent);
        }

        private static void Own(GameState state, int square, int playerId) =>
            Apply(state, new PropertyOwnerChangedEvent(square, state.GetOwner(square), playerId));

        private static void Give(GameState state, int playerId, params int[] cards)
        {
            foreach (int card in cards) Apply(state, new CardDrawnEvent(playerId, card, kept: true));
        }

        private static GameCommand Decide(GameState state)
        {
            GameCommand command = new AiPlayer(Me, AiDifficulty.Hard).Decide(Board, Rules, state, new NoMistakes());
            Assert.IsNotNull(command);
            Assert.AreEqual(RejectionReason.None, GameRules.Validate(Board, Rules, state, command), $"The AI chose an illegal {command.GetType().Name}.");
            return command;
        }

        [Test]
        public void AttacksTheLeaderButNeverAShieldedTown()
        {
            GameState state = NewState();
            Give(state, Me, Demolish);
            Own(state, B1, Other);
            Own(state, B2, Other);
            Own(state, A1, Third);
            Apply(state, new MoneyChangedEvent(Other, 2000, MoneyReason.Rent), new ShieldChangedEvent(B1, true));

            var play = (PlayCardCommand)Decide(state);
            Assert.AreEqual(Demolish, play.Card);
            Assert.AreEqual(B2, play.Target, "the leader's open town");

            Apply(state, new ShieldChangedEvent(B2, true));
            play = (PlayCardCommand)Decide(state);
            Assert.AreEqual(A1, play.Target, "the leader's towns are all shielded: another rival's");
        }

        [Test]
        public void ShieldsItsDearestTownWhenNothingIsWorthAttacking()
        {
            GameState state = NewState();
            Give(state, Me, Shield);
            Own(state, A1, Me);
            Own(state, B1, Me);

            var play = (PlayCardCommand)Decide(state);
            Assert.AreEqual(Shield, play.Card);
            Assert.AreEqual(B1, play.Target);
        }

        [Test]
        public void InvestsWhenRichAndTakesTheCashWhenNot()
        {
            GameState state = NewState();
            Give(state, Me, Fund);
            Assert.AreEqual(1, ((PlayCardCommand)Decide(state)).Option, "1000 less 200 keeps half the starting money");

            Apply(state, new MoneyChangedEvent(Me, -500, MoneyReason.Rent));
            Assert.AreEqual(0, ((PlayCardCommand)Decide(state)).Option);
        }

        [Test]
        public void PlaysOneCardATurnThenRolls()
        {
            GameState state = NewState();
            Give(state, Me, Fund, Strike);
            Own(state, B1, Other);
            Apply(state, new CardPlayedEvent(Me, Fund));
            Assert.IsInstanceOf<RollDiceCommand>(Decide(state));
        }

        [Test]
        public void UsesACardJustDrawnWhenItIsWorthItAndKeepsItOtherwise()
        {
            // An attack with the leader's town open: at once.
            GameState state = NewState();
            Own(state, B1, Other);
            Give(state, Me, Shield, Demolish);
            Apply(state, new PhaseChangedEvent(GamePhase.AwaitingCardUse));
            var play = (PlayCardCommand)Decide(state);
            Assert.AreEqual(Demolish, play.Card, "the card just drawn, not the older shield");
            Assert.AreEqual(B1, play.Target);

            // No town of its own to protect: the shield waits in the hand.
            state = NewState();
            Give(state, Me, Shield);
            Apply(state, new PhaseChangedEvent(GamePhase.AwaitingCardUse));
            Assert.IsInstanceOf<KeepCardCommand>(Decide(state));

            // The rent shield answers rent, never the draw.
            state = NewState();
            Give(state, Me, RentShield);
            Apply(state, new PhaseChangedEvent(GamePhase.AwaitingCardUse));
            Assert.IsInstanceOf<KeepCardCommand>(Decide(state));

            // A card was played this turn already: the new one is kept.
            state = NewState();
            Own(state, B1, Other);
            Give(state, Me, Fund, Demolish);
            Apply(state, new CardPlayedEvent(Me, Fund), new PhaseChangedEvent(GamePhase.AwaitingCardUse));
            Assert.IsInstanceOf<KeepCardCommand>(Decide(state));
        }

        [Test]
        public void LetsTheLeastUsefulCardGo()
        {
            GameState state = NewState();
            Give(state, Me, Shield, Demolish, Lookout, ForceSale, Fund);
            Apply(state, new PhaseChangedEvent(GamePhase.AwaitingCardDiscard));
            var discard = (DiscardCardCommand)Decide(state);
            Assert.AreEqual(Lookout, discard.Card);
        }

        [Test]
        public void UsesTheRentShieldOnlyOnABigRent()
        {
            GameState state = NewState();
            Give(state, Me, RentShield);
            Own(state, B1, Other);
            Apply(state, new RentDueEvent(Me, Other, 100), new PhaseChangedEvent(GamePhase.AwaitingRentShield));
            Assert.IsInstanceOf<PayRentCommand>(Decide(state), "a tenth of the cash");

            Apply(state, new RentDueClearedEvent(Me), new RentDueEvent(Me, Other, 400));
            Assert.IsInstanceOf<PlayCardCommand>(Decide(state));
        }
    }
}
