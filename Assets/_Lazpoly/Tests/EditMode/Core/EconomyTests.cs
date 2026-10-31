using System.Linq;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Events;
using Lazpoly.Core.State;
using NUnit.Framework;
using static Lazpoly.Core.Tests.TestData;

namespace Lazpoly.Core.Tests
{
    /// <summary>Phase 4 rules: buildings, mortgages, auctions, trades and debts.</summary>
    public class EconomyTests
    {
        // 0 Start, 1 A1 and 2 A2 (group 1, buildings cost 50), 3 Rest, 4 Solo (no group), 5 Rest.
        private const int A1 = 1;
        private const int A2 = 2;
        private const int Solo = 4;

        private static BoardDefinition Board() => new BoardDefinition(new[]
        {
            SquareDefinition.Start("Start"),
            SquareDefinition.Property("A1", 60, 10, 1, 50, new[] { 50, 150, 400, 550, 700 }),
            SquareDefinition.Property("A2", 80, 12, 1, 50, new[] { 60, 180, 480, 660, 840 }),
            SquareDefinition.Rest("Rest"),
            SquareDefinition.Property("Solo", 100, 20, 0),
            SquareDefinition.Rest("Rest 2"),
        });

        private static GameEngine NewGame(int players = 2, int money = 1000, params int[] dice) =>
            new GameEngine(Board(), TestRules(money, passStartBonus: 0), TestSeats(players), new ScriptedDice(dice));

        private static void Give(GameEngine engine, int square, int playerId) =>
            GameStateReducer.Apply(engine.State, new PropertyOwnerChangedEvent(square, engine.State.GetOwner(square), playerId));

        private static CommandResult Accept(GameEngine engine, GameCommand command)
        {
            CommandResult result = engine.Execute(command);
            Assert.IsTrue(result.IsAccepted, $"{command.GetType().Name} by {command.PlayerId} was rejected: {result.Reason}");
            return result;
        }

        private static RejectionReason Reason(GameEngine engine, GameCommand command)
        {
            ulong before = engine.State.ComputeHash();
            CommandResult result = engine.Execute(command);
            Assert.IsFalse(result.IsAccepted, $"{command.GetType().Name} was accepted.");
            Assert.AreEqual(before, engine.State.ComputeHash(), "A rejected command changed the state.");
            return result.Reason;
        }

        private static int Money(GameEngine engine, int id) => engine.State.FindPlayer(id).Money;

        // ---- Buildings ----

        [Test]
        public void BoardRejectsBrokenBuildingData()
        {
            BoardDefinition With(SquareDefinition square) =>
                new BoardDefinition(new[] { SquareDefinition.Start("Start"), square });

            Assert.Throws<System.ArgumentException>(() => With(SquareDefinition.Property("X", 60, 10, 1, 50, new[] { 50, 150 })));
            Assert.Throws<System.ArgumentException>(() => With(SquareDefinition.Property("X", 60, 10, 1, 50, new[] { 50, 40, 400, 550, 700 })));
            Assert.Throws<System.ArgumentException>(() => With(SquareDefinition.Property("X", 60, 10, 0, 50, new[] { 50, 150, 400, 550, 700 })));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new Rules.RulesConfig(1000, 0, 6, 3, 2, 101, 50));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new Rules.RulesConfig(1000, 0, 6, 3, 2, 10, -1));
        }

        [Test]
        public void BuildingNeedsTheWholeGroupAndRaisesRent()
        {
            var engine = NewGame();
            Give(engine, A1, Alice);
            Assert.AreEqual(RejectionReason.CannotBuild, Reason(engine, new BuildCommand(Alice, A1)));

            Give(engine, A2, Alice);
            Assert.AreEqual(20, engine.CalculateRent(A1), "Whole group doubles the base rent.");
            Accept(engine, new BuildCommand(Alice, A1));

            Assert.AreEqual(1, engine.State.GetBuildingLevel(A1));
            Assert.AreEqual(950, Money(engine, Alice));
            Assert.AreEqual(50, engine.CalculateRent(A1));
        }

        [Test]
        public void BuildingAndSellingMustBeEvenAcrossTheGroup()
        {
            var engine = NewGame(money: 5000);
            Give(engine, A1, Alice);
            Give(engine, A2, Alice);

            Accept(engine, new BuildCommand(Alice, A1));
            Assert.AreEqual(RejectionReason.CannotBuild, Reason(engine, new BuildCommand(Alice, A1)));
            Accept(engine, new BuildCommand(Alice, A2));
            Accept(engine, new BuildCommand(Alice, A1));

            Assert.AreEqual(RejectionReason.CannotSell, Reason(engine, new SellBuildingCommand(Alice, A2)));
            int before = Money(engine, Alice);
            Accept(engine, new SellBuildingCommand(Alice, A1));
            Assert.AreEqual(before + 25, Money(engine, Alice), "Buildings sell back at half price.");
            Assert.AreEqual(1, engine.State.GetBuildingLevel(A1));
        }

        [Test]
        public void FifthLevelIsAHotelAndTheLast()
        {
            var engine = NewGame(money: 5000);
            Give(engine, A1, Alice);
            Give(engine, A2, Alice);
            for (int level = 0; level < SquareDefinition.MaxBuildingLevel; level++)
            {
                Accept(engine, new BuildCommand(Alice, A1));
                Accept(engine, new BuildCommand(Alice, A2));
            }

            Assert.AreEqual(700, engine.CalculateRent(A1));
            Assert.AreEqual(RejectionReason.CannotBuild, Reason(engine, new BuildCommand(Alice, A1)));
        }

        [Test]
        public void OnlyTheOwnerOnTurnBuildsOnDevelopableProperties()
        {
            var engine = NewGame();
            Give(engine, A1, Bob);
            Give(engine, A2, Bob);
            Give(engine, Solo, Alice);

            Assert.AreEqual(RejectionReason.NotYourTurn, Reason(engine, new BuildCommand(Bob, A1)));
            Assert.AreEqual(RejectionReason.NotOwner, Reason(engine, new BuildCommand(Alice, A1)));
            Assert.AreEqual(RejectionReason.CannotBuild, Reason(engine, new BuildCommand(Alice, Solo)));
            Assert.AreEqual(RejectionReason.InvalidSquare, Reason(engine, new BuildCommand(Alice, 3)));
            Assert.AreEqual(RejectionReason.InvalidSquare, Reason(engine, new BuildCommand(Alice, 99)));
        }

        // ---- Mortgages ----

        [Test]
        public void MortgagePaysHalfStopsRentAndCostsInterestToRedeem()
        {
            var engine = NewGame();
            Give(engine, A1, Alice);

            Accept(engine, new MortgageCommand(Alice, A1));
            Assert.IsTrue(engine.State.IsMortgaged(A1));
            Assert.AreEqual(1030, Money(engine, Alice));
            Assert.AreEqual(0, engine.CalculateRent(A1));
            Assert.AreEqual(RejectionReason.CannotMortgage, Reason(engine, new MortgageCommand(Alice, A1)));

            Accept(engine, new RedeemCommand(Alice, A1));
            Assert.IsFalse(engine.State.IsMortgaged(A1));
            Assert.AreEqual(1030 - 33, Money(engine, Alice), "Redeeming costs the value plus 10%.");
        }

        [Test]
        public void BuildingsAndMortgagesExcludeEachOther()
        {
            var engine = NewGame();
            Give(engine, A1, Alice);
            Give(engine, A2, Alice);

            Accept(engine, new MortgageCommand(Alice, A2));
            Assert.AreEqual(RejectionReason.CannotBuild, Reason(engine, new BuildCommand(Alice, A1)));
            Accept(engine, new RedeemCommand(Alice, A2));
            Accept(engine, new BuildCommand(Alice, A1));
            Assert.AreEqual(RejectionReason.CannotMortgage, Reason(engine, new MortgageCommand(Alice, A2)),
                "A group with buildings cannot be mortgaged.");
        }

        // ---- Auctions ----

        [Test]
        public void AuctionGoesToTheLastBidderStanding()
        {
            var engine = NewGame(players: 3, dice: new[] { 1, 3 });
            Accept(engine, new RollDiceCommand(Alice)); // -> Solo
            Accept(engine, new DeclinePurchaseCommand(Alice));

            Assert.AreEqual(RejectionReason.WrongPhase, Reason(engine, new EndTurnCommand(Alice)));
            Accept(engine, new PlaceBidCommand(Bob, 40));
            Assert.AreEqual(RejectionReason.InvalidBid, Reason(engine, new PlaceBidCommand(Cem, 40)));
            Assert.AreEqual(RejectionReason.InvalidBid, Reason(engine, new PassBidCommand(Bob)), "The leader cannot pass.");
            Accept(engine, new PlaceBidCommand(Cem, 70));
            Accept(engine, new PassBidCommand(Alice));
            Assert.AreEqual(RejectionReason.InvalidBid, Reason(engine, new PlaceBidCommand(Alice, 90)), "Passing is final.");
            CommandResult last = Accept(engine, new PassBidCommand(Bob));

            AuctionEndedEvent ended = last.Events.OfType<AuctionEndedEvent>().Single();
            Assert.AreEqual(Cem, ended.WinnerId);
            Assert.AreEqual(Cem, engine.State.GetOwner(Solo));
            Assert.AreEqual(930, Money(engine, Cem));
            Assert.AreEqual(1000, Money(engine, Bob));
            Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase);
            Assert.AreEqual(Alice, engine.State.CurrentPlayer.Id);
        }

        [Test]
        public void BidsCannotExceedTheBiddersMoney()
        {
            var engine = NewGame(money: 100, dice: new[] { 1, 3 });
            Accept(engine, new RollDiceCommand(Alice));
            Accept(engine, new DeclinePurchaseCommand(Alice));

            Assert.AreEqual(RejectionReason.InsufficientFunds, Reason(engine, new PlaceBidCommand(Bob, 101)));
            Assert.AreEqual(RejectionReason.InvalidBid, Reason(engine, new PlaceBidCommand(Bob, 0)));
            Accept(engine, new PlaceBidCommand(Bob, 100));
            Accept(engine, new PassBidCommand(Alice));

            Assert.AreEqual(Bob, engine.State.GetOwner(Solo));
            Assert.AreEqual(0, Money(engine, Bob));
        }

        // ---- Trades ----

        [Test]
        public void AcceptedTradeSwapsPropertiesAndMoney()
        {
            var engine = NewGame(players: 3);
            Give(engine, A1, Alice);
            Give(engine, Solo, Bob);
            Accept(engine, new MortgageCommand(Alice, A1));

            var offer = new TradeOffer(Alice, Bob, 50, 0, new[] { A1 }, new[] { Solo });
            Accept(engine, new ProposeTradeCommand(offer));
            Assert.AreEqual(GamePhase.AwaitingTradeResponse, engine.State.Phase);
            Assert.AreEqual(RejectionReason.WrongPhase, Reason(engine, new RollDiceCommand(Alice)));
            Assert.AreEqual(RejectionReason.NotYourTurn, Reason(engine, new AcceptTradeCommand(Cem)));
            Assert.AreEqual(RejectionReason.NotYourTurn, Reason(engine, new AcceptTradeCommand(Alice)));

            Accept(engine, new AcceptTradeCommand(Bob));

            Assert.AreEqual(Bob, engine.State.GetOwner(A1));
            Assert.AreEqual(Alice, engine.State.GetOwner(Solo));
            Assert.IsTrue(engine.State.IsMortgaged(A1), "A mortgage travels with the property.");
            Assert.AreEqual(1030 - 50, Money(engine, Alice));
            Assert.AreEqual(1050, Money(engine, Bob));
            Assert.AreEqual(GamePhase.WaitingForRoll, engine.State.Phase);
            Assert.IsNull(engine.State.PendingTrade);
        }

        [Test]
        public void RejectedOrCancelledTradeChangesNothing()
        {
            var engine = NewGame();
            Give(engine, A1, Alice);
            ulong before = engine.State.ComputeHash();

            Accept(engine, new ProposeTradeCommand(new TradeOffer(Alice, Bob, 0, 100, new[] { A1 }, null)));
            Accept(engine, new RejectTradeCommand(Bob));
            Assert.AreEqual(before, engine.State.ComputeHash());

            Accept(engine, new ProposeTradeCommand(new TradeOffer(Alice, Bob, 0, 100, new[] { A1 }, null)));
            Assert.AreEqual(RejectionReason.NotYourTurn, Reason(engine, new CancelTradeCommand(Bob)));
            Accept(engine, new CancelTradeCommand(Alice));
            Assert.AreEqual(before, engine.State.ComputeHash());
        }

        [Test]
        public void InvalidTradesAreRefused()
        {
            var engine = NewGame(players: 3, money: 5000);
            Give(engine, A1, Alice);
            Give(engine, A2, Alice);
            Give(engine, Solo, Bob);

            Assert.AreEqual(RejectionReason.NotYourTurn, Reason(engine, new ProposeTradeCommand(new TradeOffer(Bob, Alice, 10, 0, null, null))));
            Assert.AreEqual(RejectionReason.InvalidTrade, Reason(engine, new ProposeTradeCommand(new TradeOffer(Alice, Bob, 0, 0, null, null))));
            Assert.AreEqual(RejectionReason.InvalidTrade, Reason(engine, new ProposeTradeCommand(new TradeOffer(Alice, Alice, 10, 0, null, null))));
            Assert.AreEqual(RejectionReason.InvalidTrade, Reason(engine, new ProposeTradeCommand(new TradeOffer(Alice, Bob, 0, 0, new[] { Solo }, null))));
            Assert.AreEqual(RejectionReason.InvalidTrade, Reason(engine, new ProposeTradeCommand(new TradeOffer(Alice, Bob, 0, 0, new[] { A1, A1 }, null))));
            Assert.AreEqual(RejectionReason.InvalidTrade, Reason(engine, new ProposeTradeCommand(new TradeOffer(Alice, Bob, -5, 0, new[] { A1 }, null))));
            Assert.AreEqual(RejectionReason.InsufficientFunds, Reason(engine, new ProposeTradeCommand(new TradeOffer(Alice, Bob, 0, 6000, new[] { A1 }, null))));

            Accept(engine, new BuildCommand(Alice, A1));
            Assert.AreEqual(RejectionReason.InvalidTrade, Reason(engine, new ProposeTradeCommand(new TradeOffer(Alice, Bob, 0, 0, new[] { A2 }, new[] { Solo }))),
                "Properties of a group with buildings cannot be traded.");
        }

        // ---- Debts and bankruptcy ----

        [Test]
        public void DebtBlocksTheTurnUntilPaidAfterMortgaging()
        {
            // Alice starts on square 5 and rolls 1+1 to land on Bob's A1 (one house: rent 50). She holds 40 and Solo.
            var engine = NewGame(money: 1000, dice: new[] { 1, 1 });
            Give(engine, A1, Bob);
            Give(engine, A2, Bob);
            GameStateReducer.Apply(engine.State, new BuildingLevelChangedEvent(A1, 1));
            GameStateReducer.Apply(engine.State, new BuildingLevelChangedEvent(A2, 1));
            Give(engine, Solo, Alice);
            GameStateReducer.Apply(engine.State, new MoneyChangedEvent(Alice, -960, MoneyReason.Tax));
            GameStateReducer.Apply(engine.State, new PlayerMovedEvent(Alice, 0, 5, false));

            Accept(engine, new RollDiceCommand(Alice));

            Assert.AreEqual(GamePhase.AwaitingDebtPayment, engine.State.Phase);
            Assert.AreEqual(50, engine.State.Debt.Amount);
            Assert.AreEqual(Bob, engine.State.Debt.CreditorId);
            Assert.AreEqual(RejectionReason.WrongPhase, Reason(engine, new RollDiceCommand(Alice)));
            Assert.AreEqual(RejectionReason.InsufficientFunds, Reason(engine, new PayDebtCommand(Alice)));

            Accept(engine, new MortgageCommand(Alice, Solo)); // +50
            Accept(engine, new PayDebtCommand(Alice));

            Assert.AreEqual(40, Money(engine, Alice));
            Assert.AreEqual(1050, Money(engine, Bob));
            Assert.IsNull(engine.State.Debt);
            Assert.AreEqual(GamePhase.WaitingForRoll, engine.State.Phase, "Alice rolled a double and rolls again.");
        }

        // Alice stands on square 2 holding 10; rolling 1+1 lands her on Cem's Solo (rent 20).
        private static GameEngine AliceOwesCem()
        {
            var engine = NewGame(players: 3, money: 1000, dice: new[] { 1, 1 });
            Give(engine, Solo, Cem);
            GameStateReducer.Apply(engine.State, new MoneyChangedEvent(Alice, -990, MoneyReason.Tax));
            GameStateReducer.Apply(engine.State, new PlayerMovedEvent(Alice, 0, 2, false));
            return engine;
        }

        [Test]
        public void BankruptcySellsBuildingsAndHandsEverythingToTheCreditor()
        {
            GameEngine engine = AliceOwesCem();
            Give(engine, A1, Alice);
            Give(engine, A2, Alice);
            GameStateReducer.Apply(engine.State, new BuildingLevelChangedEvent(A1, 2));
            GameStateReducer.Apply(engine.State, new BuildingLevelChangedEvent(A2, 2));

            Accept(engine, new RollDiceCommand(Alice));
            Assert.AreEqual(GamePhase.AwaitingDebtPayment, engine.State.Phase);
            Accept(engine, new DeclareBankruptcyCommand(Alice));

            Assert.IsTrue(engine.State.FindPlayer(Alice).IsBankrupt);
            Assert.AreEqual(0, Money(engine, Alice));
            Assert.AreEqual(1000 + 10 + 4 * 25, Money(engine, Cem), "Cash plus the buildings' sell-back value.");
            Assert.AreEqual(Cem, engine.State.GetOwner(A1));
            Assert.AreEqual(Cem, engine.State.GetOwner(A2));
            Assert.AreEqual(0, engine.State.GetBuildingLevel(A1));
            Assert.AreEqual(0, engine.State.GetBuildingLevel(A2));
            Assert.IsNull(engine.State.Debt);
            Assert.AreEqual(Bob, engine.State.CurrentPlayer.Id);
            Assert.AreEqual(GamePhase.WaitingForRoll, engine.State.Phase);
        }

        [Test]
        public void MortgagesStayWhenAPlayerTakesOverAProperty()
        {
            GameEngine engine = AliceOwesCem();
            Give(engine, A1, Alice);
            GameStateReducer.Apply(engine.State, new MortgageChangedEvent(A1, true));

            Accept(engine, new RollDiceCommand(Alice));
            Accept(engine, new DeclareBankruptcyCommand(Alice));

            Assert.AreEqual(Cem, engine.State.GetOwner(A1));
            Assert.IsTrue(engine.State.IsMortgaged(A1));
        }

        private static void AssertSnapshotRoundTrip(GameEngine engine)
        {
            GameState copy = GameStateSnapshot.Capture(engine.State).Restore();
            Assert.AreEqual(engine.State.ComputeHash(), copy.ComputeHash());
            Assert.AreEqual(engine.State.Phase, copy.Phase);
        }

        [Test]
        public void PhaseFourStateSurvivesASnapshot()
        {
            var auction = NewGame(players: 3, money: 2000, dice: new[] { 1, 3 });
            Give(auction, A1, Alice);
            Give(auction, A2, Alice);
            Accept(auction, new BuildCommand(Alice, A1));
            Accept(auction, new RollDiceCommand(Alice));
            Accept(auction, new DeclinePurchaseCommand(Alice));
            Accept(auction, new PlaceBidCommand(Bob, 30));
            Accept(auction, new PassBidCommand(Cem));
            AssertSnapshotRoundTrip(auction);
            Assert.AreEqual(1, GameStateSnapshot.Capture(auction.State).Restore().GetBuildingLevel(A1));

            var trade = NewGame();
            Give(trade, A1, Alice);
            Accept(trade, new MortgageCommand(Alice, A1));
            Accept(trade, new ProposeTradeCommand(new TradeOffer(Alice, Bob, 5, 0, new[] { A1 }, null)));
            AssertSnapshotRoundTrip(trade);

            GameEngine debt = AliceOwesCem();
            Accept(debt, new RollDiceCommand(Alice));
            AssertSnapshotRoundTrip(debt);
            Assert.AreEqual(20, GameStateSnapshot.Capture(debt.State).Restore().Debt.Amount);
        }
    }
}
