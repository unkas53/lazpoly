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
        private static readonly RulesConfig Rules = new RulesConfig(1000, 0, 6, 3, 2, 10, 50);

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
        public void BidsUpToItsCeilingThenPasses()
        {
            GameState state = NewState();
            Apply(state, new AuctionStartedEvent(B1), new PhaseChangedEvent(GamePhase.AuctionInProgress));

            var bid = Decide(state, Me, AiDifficulty.Normal) as PlaceBidCommand;
            Assert.IsNotNull(bid);
            Assert.AreEqual(20, bid.Amount, "Bids rise in steps of a tenth of the price.");

            Apply(state, new BidPlacedEvent(Other, 190));
            Assert.IsInstanceOf<PassBidCommand>(Decide(state, Me, AiDifficulty.Normal), "Normal pays at most the price.");

            Assert.IsNull(Decide(state, Other, AiDifficulty.Normal), "The leader has nothing to do.");
        }

        [Test]
        public void HardPaysMoreToCompleteAGroup()
        {
            GameState state = NewState();
            Own(state, B2, Me);
            Apply(state, new AuctionStartedEvent(B1), new PhaseChangedEvent(GamePhase.AuctionInProgress), new BidPlacedEvent(Other, 300));

            Assert.IsInstanceOf<PlaceBidCommand>(Decide(state, Me, AiDifficulty.Hard));
            Assert.IsInstanceOf<PassBidCommand>(Decide(state, Third, AiDifficulty.Normal));
        }

        [Test]
        public void PaysADebtItCanAfford()
        {
            GameState state = NewState();
            Apply(state, new DebtIncurredEvent(Me, Other, 100, MoneyReason.Rent), new PhaseChangedEvent(GamePhase.AwaitingDebtPayment));

            Assert.IsInstanceOf<PayDebtCommand>(Decide(state, Me, AiDifficulty.Easy));
        }

        [Test]
        public void SellsHousesBeforeMortgagingAndGivesUpLast()
        {
            GameState state = NewState();
            Own(state, A1, Me);
            Own(state, A2, Me);
            Own(state, Solo, Me);
            Apply(state, new BuildingLevelChangedEvent(A1, 1), new BuildingLevelChangedEvent(A2, 2));
            SetMoney(state, Me, 10);
            Apply(state, new DebtIncurredEvent(Me, Other, 500, MoneyReason.Rent), new PhaseChangedEvent(GamePhase.AwaitingDebtPayment));

            var sell = Decide(state, Me, AiDifficulty.Normal) as SellBuildingCommand;
            Assert.IsNotNull(sell);
            Assert.AreEqual(A2, sell.SquareIndex, "Sells evenly, from the tallest building.");

            Apply(state, new BuildingLevelChangedEvent(A1, 0), new BuildingLevelChangedEvent(A2, 0));
            var mortgage = Decide(state, Me, AiDifficulty.Normal) as MortgageCommand;
            Assert.IsNotNull(mortgage);
            Assert.AreEqual(A2, mortgage.SquareIndex, "Mortgages the cheapest property first (ties: the later square).");

            foreach (int square in new[] { A1, A2, Solo }) Apply(state, new MortgageChangedEvent(square, true));
            Assert.IsInstanceOf<DeclareBankruptcyCommand>(Decide(state, Me, AiDifficulty.Normal));
        }

        [Test]
        public void BuildsWithSpareMoneyAndEasyWaitsLonger()
        {
            GameState state = NewState();
            Own(state, A1, Me);
            Own(state, A2, Me);

            Assert.IsInstanceOf<BuildCommand>(Decide(state, Me, AiDifficulty.Normal));

            SetMoney(state, Me, 120);
            Assert.IsInstanceOf<RollDiceCommand>(Decide(state, Me, AiDifficulty.Easy), "Easy keeps three reserves before building.");
        }

        [Test]
        public void RedeemsMortgagesWhenRich()
        {
            GameState state = NewState();
            Own(state, Solo, Me);
            Apply(state, new MortgageChangedEvent(Solo, true));

            var redeem = Decide(state, Me, AiDifficulty.Normal) as RedeemCommand;
            Assert.IsNotNull(redeem);
            Assert.AreEqual(Solo, redeem.SquareIndex);
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
        public void HardMortgagesALonePropertyToPayForAHouse()
        {
            GameState state = NewState();
            Own(state, A1, Me);
            Own(state, A2, Me);
            Own(state, Solo, Me);
            SetMoney(state, Me, 100);

            Assert.IsInstanceOf<RollDiceCommand>(Decide(state, Me, AiDifficulty.Normal), "Normal waits until it can pay from its spare money.");
            var mortgage = Decide(state, Me, AiDifficulty.Hard) as MortgageCommand;
            Assert.IsNotNull(mortgage, "Hard should raise the money for a house.");
            Assert.AreEqual(Solo, mortgage.SquareIndex);

            Apply(state, new MortgageChangedEvent(Solo, true), new MoneyChangedEvent(Me, Board[Solo].MortgageValue, MoneyReason.Mortgage));
            Assert.IsInstanceOf<BuildCommand>(Decide(state, Me, AiDifficulty.Hard), "The mortgage pays for a house, it is not bought back at once.");
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
        public void AuctionAsksBiddersInSeatOrderFromThePlayerOnTurn()
        {
            GameState state = NewState(4);
            Apply(state, new AuctionStartedEvent(B1), new PhaseChangedEvent(GamePhase.AuctionInProgress),
                new BidPlacedEvent(2, 20), new BidPassedEvent(3));

            CollectionAssert.AreEqual(new[] { 1, 4 }, AiTurnOrder.PlayersToAct(state));
        }

        [Test]
        public void RosterOnlyMovesForItsOwnPlayers()
        {
            GameState state = NewState();
            var roster = new AiRoster(new Pcg32Random(1UL));
            roster.Add(Other, AiDifficulty.Normal);

            Assert.IsNull(roster.NextCommand(Board, Rules, state), "Player 1 (a person) is on turn.");

            Apply(state, new AuctionStartedEvent(B1), new PhaseChangedEvent(GamePhase.AuctionInProgress));
            GameCommand command = roster.NextCommand(Board, Rules, state);
            Assert.IsNotNull(command);
            Assert.AreEqual(Other, command.PlayerId, "The AI bids while the person still decides.");
            Assert.Throws<ArgumentException>(() => roster.Add(Other, AiDifficulty.Hard));
        }
    }
}
