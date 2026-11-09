using System;
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
    /// <summary>Single decisions of the computer player, each from a hand-made situation.</summary>
    public class AiPlayerTests
    {
        private const int Me = 1;
        private const int Other = 2;
        private const int Third = 3;
        private const int A1 = 1, A2 = 2, B1 = 4, B2 = 5, Solo = 7;

        private static readonly BoardDefinition Board = new BoardDefinition(new[]
        {
            SquareDefinition.Start("Start"),
            SquareDefinition.Property("A1", 100, 10, 1, 50, new[] { 50, 150, 400, 550, 700 }),
            SquareDefinition.Property("A2", 100, 10, 1, 50, new[] { 50, 150, 400, 550, 700 }),
            SquareDefinition.Rest("Rest"),
            SquareDefinition.Property("B1", 200, 20, 2, 100, new[] { 100, 300, 800, 1100, 1400 }),
            SquareDefinition.Property("B2", 200, 20, 2, 100, new[] { 100, 300, 800, 1100, 1400 }),
            SquareDefinition.Tax("Tax", 50),
            SquareDefinition.Property("Solo", 150, 15, 0),
        });

        // Starting money 1000: reserves are Easy 30, Normal 80, Hard 80.
        private static readonly RulesConfig Rules = new RulesConfig(1000, 0, 6, 3, 2, 50);

        /// <summary>Always draws the highest value, so the AI never makes its deliberate mistakes.</summary>
        private sealed class NoMistakes : IRandomSource
        {
            public uint NextUInt() => uint.MaxValue;
            public int NextInt(int minInclusive, int maxExclusive) => maxExclusive - 1;
        }

        private static GameState NewState(int players = 3)
        {
            var seats = new List<PlayerSeat>();
            for (int i = 1; i <= players; i++) seats.Add(new PlayerSeat(i, $"P{i}"));
            return GameState.CreateInitial(Board, Rules, seats);
        }

        private static void Apply(GameState state, params GameEvent[] events)
        {
            foreach (GameEvent gameEvent in events) GameStateReducer.Apply(state, gameEvent);
        }

        private static void Own(GameState state, int square, int playerId) =>
            Apply(state, new PropertyOwnerChangedEvent(square, state.GetOwner(square), playerId));

        private static void SetMoney(GameState state, int playerId, int money) =>
            Apply(state, new MoneyChangedEvent(playerId, money - state.FindPlayer(playerId).Money, MoneyReason.Rent));

        private static void StandOn(GameState state, int playerId, int square) =>
            Apply(state, new PlayerMovedEvent(playerId, state.FindPlayer(playerId).Position, square, false),
                new PhaseChangedEvent(GamePhase.AwaitingBuyDecision));

        private static GameCommand Decide(GameState state, int playerId, AiDifficulty difficulty) =>
            Decide(new AiPlayer(playerId, difficulty), state);

        private static GameCommand Decide(AiPlayer ai, GameState state)
        {
            GameCommand command = ai.Decide(Board, Rules, state, new NoMistakes());
            if (command != null)
            {
                Assert.AreEqual(RejectionReason.None, GameRules.Validate(Board, Rules, state, command),
                    $"The AI chose an illegal {command.GetType().Name}.");
            }

            return command;
        }

        [Test]
        public void InJailTheComputerPaysToLeaveWhenItCanSpareTheFeeAndWaitsOtherwise()
        {
            var board = new BoardDefinition(new[]
            {
                SquareDefinition.Start("Start"), SquareDefinition.Property("A1", 100, 10, 1), SquareDefinition.Jail("Jail", 50),
                SquareDefinition.Rest("Rest"),
            });
            var rules = new RulesConfig(1000, 0, 6, 3, 2, 50, jailTurns: 3);
            GameState state = GameState.CreateInitial(board, rules, new[] { new PlayerSeat(Me, "Me"), new PlayerSeat(Other, "Other") });
            Apply(state, new PlayerMovedEvent(Me, 0, 2, false), new JailChangedEvent(Me, 2), new PhaseChangedEvent(GamePhase.AwaitingJailDecision));
            var ai = new AiPlayer(Me, AiDifficulty.Normal);

            Assert.IsInstanceOf<PayJailFeeCommand>(ai.Decide(board, rules, state, new NoMistakes()));

            Apply(state, new MoneyChangedEvent(Me, 100 - state.FindPlayer(Me).Money, MoneyReason.Rent));
            GameCommand poor = ai.Decide(board, rules, state, new NoMistakes());
            Assert.IsInstanceOf<RollInJailCommand>(poor, "the fee would eat into its reserve");
            Assert.AreEqual(RejectionReason.None, GameRules.Validate(board, rules, state, poor));
        }

        [Test]
        public void BuysWhenItKeepsItsReserve()
        {
            GameState state = NewState();
            StandOn(state, Me, B1);

            Assert.IsInstanceOf<BuyPropertyCommand>(Decide(state, Me, AiDifficulty.Normal));
        }

        [Test]
        public void DeclinesWhenThePurchaseWouldEatTheReserve()
        {
            GameState state = NewState();
            StandOn(state, Me, B1);
            SetMoney(state, Me, 250);

            Assert.IsInstanceOf<DeclinePurchaseCommand>(Decide(state, Me, AiDifficulty.Normal));
        }

        [Test]
        public void StretchesForTheLastPieceOfAGroup()
        {
            GameState state = NewState();
            Own(state, A1, Me);
            StandOn(state, Me, A2);
            SetMoney(state, Me, 150);

            Assert.IsInstanceOf<BuyPropertyCommand>(Decide(state, Me, AiDifficulty.Normal));
        }

        [Test]
        public void PaysADebtItCanAfford()
        {
            GameState state = NewState();
            Apply(state, new DebtIncurredEvent(Me, Other, 100, MoneyReason.Rent), new PhaseChangedEvent(GamePhase.AwaitingDebtPayment));

            Assert.IsInstanceOf<PayDebtCommand>(Decide(state, Me, AiDifficulty.Easy));
        }

        [Test]
        public void SellsWholeTownsWithTheirBuildingsAsAPersonDoesAndGivesUpLast()
        {
            // The owner's wish (Faz 27): a computer player sells a town with its buildings in one go, as a person does, never house by house.
            GameState state = NewState();
            Own(state, A1, Me);
            Own(state, A2, Me);
            Own(state, Solo, Me);
            Apply(state, new BuildingLevelChangedEvent(A1, 1), new BuildingLevelChangedEvent(A2, 2));
            SetMoney(state, Me, 10);
            Apply(state, new DebtIncurredEvent(Me, Other, 200, MoneyReason.Rent), new PhaseChangedEvent(GamePhase.AwaitingDebtPayment));

            // Missing 190: Solo (150, no buildings) goes first, then A1 with its house (100 + 25) covers the remaining 40.
            var first = Decide(state, Me, AiDifficulty.Normal) as SellPropertyCommand;
            Assert.IsNotNull(first, "A whole town, not a building.");
            Assert.AreEqual(Solo, first.SquareIndex, "Towns without buildings go first.");
            Apply(state, new PropertyOwnerChangedEvent(Solo, Me, GameState.NoOwner));
            SetMoney(state, Me, 160);

            var second = Decide(state, Me, AiDifficulty.Normal) as SellPropertyCommand;
            Assert.IsNotNull(second, "Still a whole town.");
            Assert.AreEqual(A1, second.SquareIndex, "The cheapest sale that covers the rest, its house with it.");
            Assert.AreEqual(1, state.GetBuildingLevel(A1), "Nothing was pulled down first.");
            Apply(state, new BuildingLevelChangedEvent(A1, 0), new PropertyOwnerChangedEvent(A1, Me, GameState.NoOwner));
            SetMoney(state, Me, 285);
            Assert.IsInstanceOf<PayDebtCommand>(Decide(state, Me, AiDifficulty.Normal));

            SetMoney(state, Me, 0);
            Assert.IsInstanceOf<DeclareBankruptcyCommand>(Decide(state, Me, AiDifficulty.Normal), "A2 alone (150) cannot make 200.");
        }

        [Test]
        public void NeverSellsABuildingOnItsOwn()
        {
            GameState state = NewState();
            Own(state, A1, Me);
            Own(state, A2, Me);
            Apply(state, new BuildingLevelChangedEvent(A1, 3), new BuildingLevelChangedEvent(A2, 3));
            SetMoney(state, Me, 0);
            Apply(state, new DebtIncurredEvent(Me, Other, 20, MoneyReason.Rent), new PhaseChangedEvent(GamePhase.AwaitingDebtPayment));

            GameCommand decision = Decide(state, Me, AiDifficulty.Normal);
            Assert.IsNotInstanceOf<SellBuildingCommand>(decision, "Even when one house would cover it.");
            Assert.IsInstanceOf<SellPropertyCommand>(decision);
        }

        [Test]
        public void BuildsWithSpareMoneyAndEasyWaitsLonger()
        {
            GameState state = NewState();
            Own(state, A1, Me);
            Own(state, A2, Me);
            Assert.IsInstanceOf<RollDiceCommand>(Decide(state, Me, AiDifficulty.Normal), "Nothing to build away from its towns.");

            Apply(state, new PlayerMovedEvent(Me, 0, A1, false));
            Assert.IsInstanceOf<BuildCommand>(Decide(state, Me, AiDifficulty.Normal));

            SetMoney(state, Me, 120);
            Assert.IsInstanceOf<RollDiceCommand>(Decide(state, Me, AiDifficulty.Easy), "Easy keeps three reserves before building.");
        }

        [Test]
        public void OffersMoneyForTheMissingPieceOncePerTurn()
        {
            GameState state = NewState();
            Own(state, A1, Me);
            Own(state, A2, Other);
            var ai = new AiPlayer(Me, AiDifficulty.Hard);

            var propose = Decide(ai, state) as ProposeTradeCommand;
            Assert.IsNotNull(propose);
            Assert.AreEqual(Other, propose.Offer.ToPlayerId);
            Assert.AreEqual(150, propose.Offer.GiveMoney, "Hard offers one and a half times the price.");
            CollectionAssert.AreEqual(new[] { A2 }, propose.Offer.TakeSquares);

            Assert.IsInstanceOf<RollDiceCommand>(Decide(ai, state), "One offer per turn.");
            Assert.IsInstanceOf<RollDiceCommand>(Decide(state, Me, AiDifficulty.Easy), "Easy never offers trades.");
        }

        [Test]
        public void OffersGrowWhenNobodySellsForLong()
        {
            GameState state = NewState();
            Own(state, A1, Me);
            Own(state, A2, Other);
            for (int i = 0; i < 14; i++) Apply(state, new TurnStartedEvent(Me));
            Assert.AreEqual(15, state.Round);

            var propose = Decide(state, Me, AiDifficulty.Hard) as ProposeTradeCommand;
            Assert.IsNotNull(propose);
            Assert.AreEqual(100 * (150 + AiPlayer.StalemateBonusPercent * 3) / 100, propose.Offer.GiveMoney);
        }

        [Test]
        public void NobodySellsAPropertyToPayForAHouse()
        {
            GameState state = NewState();
            Own(state, A1, Me);
            Own(state, A2, Me);
            Own(state, Solo, Me);
            Apply(state, new PlayerMovedEvent(Me, 0, A1, false));
            SetMoney(state, Me, 100);

            Assert.IsInstanceOf<RollDiceCommand>(Decide(state, Me, AiDifficulty.Normal), "Normal waits until it can pay from its spare money.");
            Assert.IsInstanceOf<RollDiceCommand>(Decide(state, Me, AiDifficulty.Hard), "Towns are sold only to pay a debt, never to build.");
        }

        [Test]
        public void JudgesTradeOffersByWhatTheyAreWorth()
        {
            Assert.IsInstanceOf<AcceptTradeCommand>(Answer(AiDifficulty.Normal, money: 150));
            Assert.IsInstanceOf<RejectTradeCommand>(Answer(AiDifficulty.Normal, money: 100), "Normal wants a margin.");

            // Hard also charges for handing over the piece that completes the buyer's group.
            Assert.IsInstanceOf<AcceptTradeCommand>(Answer(AiDifficulty.Hard, money: 230));
            Assert.IsInstanceOf<RejectTradeCommand>(Answer(AiDifficulty.Hard, money: 220));
        }

        private static GameCommand Answer(AiDifficulty difficulty, int money)
        {
            GameState state = NewState();
            Own(state, A1, Me);
            Own(state, A2, Other);
            var offer = new TradeOffer(Me, Other, money, 0, Array.Empty<int>(), new[] { A2 });
            Apply(state, new TradeProposedEvent(offer), new PhaseChangedEvent(GamePhase.AwaitingTradeResponse));

            Assert.IsNull(Decide(state, Me, AiDifficulty.Hard), "The proposer waits for the answer.");
            return Decide(state, Other, difficulty);
        }

        [Test]
        public void NotOnTurnMeansNothingToDo()
        {
            GameState state = NewState();
            Assert.IsNull(Decide(state, Other, AiDifficulty.Hard));
        }

        [Test]
        public void AnOpenTradeWaitsOnlyForItsAnswer()
        {
            GameState state = NewState(4);
            CollectionAssert.AreEqual(new[] { Me }, AiTurnOrder.PlayersToAct(state), "Otherwise the player on turn acts.");

            var offer = new TradeOffer(Me, Third, 10, 0, Array.Empty<int>(), Array.Empty<int>());
            Apply(state, new TradeProposedEvent(offer), new PhaseChangedEvent(GamePhase.AwaitingTradeResponse));

            CollectionAssert.AreEqual(new[] { Third }, AiTurnOrder.PlayersToAct(state));
        }

        [Test]
        public void RosterOnlyMovesForItsOwnPlayers()
        {
            GameState state = NewState();
            var roster = new AiRoster(new Pcg32Random(1UL));
            roster.Add(Other, AiDifficulty.Normal);

            Assert.IsNull(roster.NextCommand(Board, Rules, state), "Player 1 (a person) is on turn.");

            var offer = new TradeOffer(Me, Other, 10, 0, Array.Empty<int>(), Array.Empty<int>());
            Apply(state, new TradeProposedEvent(offer), new PhaseChangedEvent(GamePhase.AwaitingTradeResponse));
            GameCommand command = roster.NextCommand(Board, Rules, state);
            Assert.IsNotNull(command);
            Assert.AreEqual(Other, command.PlayerId, "The AI answers the person's offer although it is not its turn.");
            Assert.Throws<ArgumentException>(() => roster.Add(Other, AiDifficulty.Hard));
        }

        [Test]
        public void TheAiBringsTheCupToItsDearestTownWhenItCanSpareTheFee()
        {
            var board = new BoardDefinition(new[]
            {
                SquareDefinition.Start("Start"),
                SquareDefinition.Property("Cheap", 60, 10, 0),
                SquareDefinition.Property("Dear", 200, 40, 0),
                SquareDefinition.Resort("Beach", 200, 50, 0),
                SquareDefinition.Cup("Cup", 50, 50, 10),
            });
            GameState state = GameState.CreateInitial(board, Rules, new[] { new PlayerSeat(1, "P1"), new PlayerSeat(2, "P2") });
            foreach (int square in new[] { 1, 2, 3 }) GameStateReducer.Apply(state, new PropertyOwnerChangedEvent(square, GameState.NoOwner, 1));
            GameStateReducer.Apply(state, new PlayerMovedEvent(1, 0, 4, false));
            GameStateReducer.Apply(state, new PhaseChangedEvent(GamePhase.AwaitingCupChoice));

            GameCommand command = new AiPlayer(1, AiDifficulty.Hard).Decide(board, Rules, state, new NoMistakes());
            Assert.IsInstanceOf<HostCupCommand>(command);
            Assert.AreEqual(2, ((HostCupCommand)command).SquareIndex, "the town with the highest rent, never the resort");

            GameStateReducer.Apply(state, new MoneyChangedEvent(1, 60 - state.FindPlayer(1).Money, MoneyReason.Rent));
            Assert.IsInstanceOf<DeclineCupCommand>(new AiPlayer(1, AiDifficulty.Hard).Decide(board, Rules, state, new NoMistakes()),
                "the fee would eat its reserve");
        }

        [Test]
        public void TakesOverARivalsTownThatCompletesItsGroupWhenTheReserveStays()
        {
            // Me owns A1 and stands on Other's A2 (worth 100 + 2 x 50 = 200, price 300) after paying its rent.
            GameState state = NewState();
            Own(state, A1, Me);
            Own(state, A2, Other);
            Apply(state, new BuildingLevelChangedEvent(A2, 2),
                new PlayerMovedEvent(Me, 0, A2, false), new PhaseChangedEvent(GamePhase.AwaitingTakeoverDecision));
            Assert.AreEqual(300, GameRules.TakeoverPrice(Board, state, A2));

            Assert.IsInstanceOf<TakeOverCommand>(Decide(state, Me, AiDifficulty.Normal), "the group is worth it");

            SetMoney(state, Me, 350);
            Assert.IsInstanceOf<DeclineTakeoverCommand>(Decide(state, Me, AiDifficulty.Normal), "the price would eat its reserve");
        }

        [Test]
        public void TakesOverAnOrdinaryTownOnlyWithTwiceItsReserveLeft()
        {
            GameState state = NewState();
            Own(state, B1, Other);
            Apply(state, new PlayerMovedEvent(Me, 0, B1, false), new PhaseChangedEvent(GamePhase.AwaitingTakeoverDecision));
            Assert.AreEqual(300, GameRules.TakeoverPrice(Board, state, B1));

            Assert.IsInstanceOf<TakeOverCommand>(Decide(state, Me, AiDifficulty.Normal));
            SetMoney(state, Me, 300 + 100);
            Assert.IsInstanceOf<DeclineTakeoverCommand>(Decide(state, Me, AiDifficulty.Normal), "100 left is under twice the reserve of 80");
        }
    }
}
