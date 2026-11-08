using System.Collections.Generic;
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
    /// <summary>Phase 4 rules: buildings, property sales, trades and debts.</summary>
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
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new Rules.RulesConfig(1000, 0, 6, 3, 2, 101));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new Rules.RulesConfig(1000, 0, 6, 3, 2, -1));
        }

        /// <summary>Puts a player on a square without a roll, the way a move event would.</summary>
        private static void Stand(GameEngine engine, int playerId, int square, bool passedStart = false) =>
            GameStateReducer.Apply(engine.State, new PlayerMovedEvent(playerId, engine.State.FindPlayer(playerId).Position, square, passedStart));

        /// <summary>Advances the round counter: every seat takes a turn and play returns to the first.</summary>
        private static void NextRound(GameEngine engine)
        {
            int first = engine.State.CurrentPlayerIndex;
            int count = engine.State.Players.Count;
            for (int step = 1; step <= count; step++)
                GameStateReducer.Apply(engine.State, new TurnStartedEvent(engine.State.Players[(first + step) % count].Id));
            GameStateReducer.Apply(engine.State, new PhaseChangedEvent(GamePhase.WaitingForRoll));
        }

        [Test]
        public void BuildingHappensWhereTheOwnerStandsAndNeedsNoGroup()
        {
            var engine = NewGame();
            Give(engine, A1, Alice);
            Assert.AreEqual(RejectionReason.CannotBuild, Reason(engine, new BuildCommand(Alice, A1)), "Alice is not on A1.");
            Assert.AreEqual(0, GameRules.BuildableNow(engine.Board, engine.State, engine.State.FindPlayer(Alice), A1));

            Stand(engine, Alice, A1);
            Assert.AreEqual(1, GameRules.BuildableNow(engine.Board, engine.State, engine.State.FindPlayer(Alice), A1));
            Accept(engine, new BuildCommand(Alice, A1));

            Assert.AreEqual(1, engine.State.GetBuildingLevel(A1));
            Assert.AreEqual(950, Money(engine, Alice));
            Assert.AreEqual(50, engine.CalculateRent(A1), "A house raises the rent even without the rest of the colour.");
            Assert.AreEqual(1, engine.State.BuiltThisVisit);
        }

        [Test]
        public void OneHouseUntilTheFirstLapThenFour()
        {
            var engine = NewGame(money: 5000);
            Give(engine, A1, Alice);
            Stand(engine, Alice, A1);

            Accept(engine, new BuildCommand(Alice, A1));
            Assert.AreEqual(RejectionReason.CannotBuild, Reason(engine, new BuildCommand(Alice, A1)), "One house before passing start.");

            NextRound(engine);
            Assert.AreEqual(2, engine.State.Round);
            Assert.AreEqual(GameState.NotArrived, engine.State.BuiltThisVisit);
            Assert.AreEqual(0, GameRules.BuildableNow(engine.Board, engine.State, engine.State.FindPlayer(Alice), A1), "Standing still is not a visit.");
            Stand(engine, Alice, A1);
            Assert.AreEqual(0, GameRules.BuildableNow(engine.Board, engine.State, engine.State.FindPlayer(Alice), A1), "Rounds do not matter; passing start does.");

            Stand(engine, Alice, A1, passedStart: true);
            Assert.AreEqual(1, engine.State.FindPlayer(Alice).Laps);
            Assert.AreEqual(3, GameRules.BuildableNow(engine.Board, engine.State, engine.State.FindPlayer(Alice), A1));
            for (int i = 0; i < 3; i++) Accept(engine, new BuildCommand(Alice, A1));
            Assert.AreEqual(4, engine.State.GetBuildingLevel(A1));
            Assert.AreEqual(RejectionReason.CannotBuild, Reason(engine, new BuildCommand(Alice, A1)), "The hotel waits for a later visit.");
        }

        [Test]
        public void ThePickedLevelGoesUpAtOnceInOnePayment()
        {
            var engine = NewGame(money: 5000);
            Give(engine, A1, Alice);
            Stand(engine, Alice, A1, passedStart: true);
            int cost = engine.Board[A1].BuildingCost;
            int before = Money(engine, Alice);
            Assert.AreEqual(RejectionReason.CannotBuild, Reason(engine, new BuildCommand(Alice, A1, 0)));
            Assert.AreEqual(RejectionReason.CannotBuild, Reason(engine, new BuildCommand(Alice, A1, 5)), "No more than the visit allows.");

            CommandResult result = Accept(engine, new BuildCommand(Alice, A1, 3));
            Assert.AreEqual(1, result.Events.OfType<BuildingLevelChangedEvent>().Count(), "one building step, not one per house");
            Assert.AreEqual(3, result.Events.OfType<BuildingLevelChangedEvent>().Single().Level);
            Assert.AreEqual(-3 * cost, result.Events.OfType<MoneyChangedEvent>().Single().Delta, "one payment for all three");
            Assert.AreEqual(before - 3 * cost, Money(engine, Alice));
            Assert.AreEqual(3, engine.State.GetBuildingLevel(A1));
            Assert.AreEqual(3, engine.State.BuiltThisVisit);
            Assert.AreEqual(1, GameRules.BuildableNow(engine.Board, engine.State, engine.State.FindPlayer(Alice), A1), "the fourth house is still allowed");
            Assert.AreEqual(RejectionReason.CannotBuild, Reason(engine, new BuildCommand(Alice, A1, 2)));
        }

        [Test]
        public void BuyingWithHousesIsOneStepAndOnePayment()
        {
            var engine = NewGame(money: 5000);
            Stand(engine, Alice, A1);
            GameStateReducer.Apply(engine.State, new PhaseChangedEvent(GamePhase.AwaitingBuyDecision));
            Assert.AreEqual(RejectionReason.CannotBuild, Reason(engine, new BuyPropertyCommand(Alice, 2)), "One house before passing start.");

            Stand(engine, Alice, A1, passedStart: true);
            GameStateReducer.Apply(engine.State, new PhaseChangedEvent(GamePhase.AwaitingBuyDecision));
            Assert.AreEqual(RejectionReason.CannotBuild, Reason(engine, new BuyPropertyCommand(Alice, 5)), "No hotel with the purchase.");
            Assert.AreEqual(RejectionReason.CannotBuild, Reason(engine, new BuyPropertyCommand(Alice, -1)));
            int price = engine.Board[A1].Price, cost = engine.Board[A1].BuildingCost;
            int before = Money(engine, Alice);

            CommandResult result = Accept(engine, new BuyPropertyCommand(Alice, 4));
            Assert.AreEqual(-(price + 4 * cost), result.Events.OfType<MoneyChangedEvent>().Single().Delta, "the land and the four houses in one payment");
            Assert.AreEqual(4, result.Events.OfType<BuildingLevelChangedEvent>().Single().Level, "straight to the fourth house, no plot step");
            Assert.AreEqual(before - price - 4 * cost, Money(engine, Alice));
            Assert.AreEqual(Alice, engine.State.GetOwner(A1));
            Assert.AreEqual(4, engine.State.GetBuildingLevel(A1));
            Assert.AreEqual(0, GameRules.BuildableNow(engine.Board, engine.State, engine.State.FindPlayer(Alice), A1), "the hotel waits for a later visit");
        }

        [Test]
        public void BuyingWithHousesNeedsTheMoneyForAllOfIt()
        {
            var engine = NewGame(money: 5000);
            Stand(engine, Alice, A1, passedStart: true);
            GameStateReducer.Apply(engine.State, new PhaseChangedEvent(GamePhase.AwaitingBuyDecision));
            int price = engine.Board[A1].Price, cost = engine.Board[A1].BuildingCost;
            GameStateReducer.Apply(engine.State, new MoneyChangedEvent(Alice, price + 2 * cost - Money(engine, Alice), MoneyReason.Building));
            Assert.AreEqual(RejectionReason.InsufficientFunds, Reason(engine, new BuyPropertyCommand(Alice, 3)));
            CommandResult bare = Accept(engine, new BuyPropertyCommand(Alice, 2));
            Assert.AreEqual(0, Money(engine, Alice));
            Assert.AreEqual(2, bare.Events.OfType<BuildingLevelChangedEvent>().Single().Level);
        }

        [Test]
        public void ManyLevelsAtOnceNeedTheMoneyForAllOfThem()
        {
            var engine = NewGame(money: 5000);
            Give(engine, A1, Alice);
            Stand(engine, Alice, A1, passedStart: true);
            int cost = engine.Board[A1].BuildingCost;
            GameStateReducer.Apply(engine.State, new MoneyChangedEvent(Alice, 2 * cost + 1 - Money(engine, Alice), MoneyReason.Building));
            Assert.AreEqual(RejectionReason.InsufficientFunds, Reason(engine, new BuildCommand(Alice, A1, 3)));
            Accept(engine, new BuildCommand(Alice, A1, 2));
        }

        [Test]
        public void TheWholeColourDoublesTheRentAtEveryLevel()
        {
            var engine = NewGame();
            Give(engine, A1, Alice);
            Assert.AreEqual(10, engine.CalculateRent(A1));
            GameStateReducer.Apply(engine.State, new BuildingLevelChangedEvent(A1, 2));
            Assert.AreEqual(150, engine.CalculateRent(A1), "Two houses without the rest of the colour.");

            Give(engine, A2, Alice);
            Assert.AreEqual(300, engine.CalculateRent(A1), "The whole colour doubles the houses' rent too (the owner's wish, Faz 26).");
            Assert.AreEqual(24, engine.CalculateRent(A2), "and the bare land's.");
            GameStateReducer.Apply(engine.State, new BuildingLevelChangedEvent(A1, SquareDefinition.MaxBuildingLevel));
            Assert.AreEqual(1400, engine.CalculateRent(A1), "and the hotel's.");

            Give(engine, A2, Bob);
            Assert.AreEqual(700, engine.CalculateRent(A1), "Losing a town of the colour takes the double away.");
        }

        [Test]
        public void TheHotelNeedsAReturnVisitToAFourHouseTown()
        {
            var engine = NewGame(money: 5000);
            Give(engine, A1, Alice);
            Stand(engine, Alice, A1, passedStart: true);
            for (int i = 0; i < 4; i++) Accept(engine, new BuildCommand(Alice, A1));
            Assert.IsTrue(GameRules.NextIsHotel(engine.State, A1));
            Assert.AreEqual(0, GameRules.BuildableNow(engine.Board, engine.State, engine.State.FindPlayer(Alice), A1));

            Stand(engine, Alice, Solo);
            Stand(engine, Alice, A1);
            Assert.AreEqual(0, engine.State.BuiltThisVisit, "Moving starts a fresh visit.");
            Assert.AreEqual(1, GameRules.BuildableNow(engine.Board, engine.State, engine.State.FindPlayer(Alice), A1));
            Accept(engine, new BuildCommand(Alice, A1));

            Assert.AreEqual(SquareDefinition.MaxBuildingLevel, engine.State.GetBuildingLevel(A1));
            Assert.AreEqual(700, engine.CalculateRent(A1));
            Assert.AreEqual(RejectionReason.CannotBuild, Reason(engine, new BuildCommand(Alice, A1)));
        }

        [Test]
        public void SellingBuildingsIsPerTown()
        {
            var engine = NewGame(money: 5000);
            Give(engine, A1, Alice);
            Give(engine, A2, Alice);
            Stand(engine, Alice, A1);
            Accept(engine, new BuildCommand(Alice, A1));

            Assert.AreEqual(RejectionReason.CannotSell, Reason(engine, new SellBuildingCommand(Alice, A2)));

            int before = Money(engine, Alice);
            Accept(engine, new SellBuildingCommand(Alice, A1));
            Assert.AreEqual(before + 25, Money(engine, Alice), "Buildings sell back at half price.");
            Assert.AreEqual(0, engine.State.GetBuildingLevel(A1));
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

        // ---- Property sales ----

        [Test]
        public void APropertyIsSoldOnlyToPayADebt()
        {
            GameEngine engine = AliceOwesCem();
            Give(engine, A1, Alice);
            Assert.AreEqual(RejectionReason.WrongPhase, Reason(engine, new SellPropertyCommand(Alice, A1)), "No selling while the money lasts.");

            Accept(engine, new RollDiceCommand(Alice)); // Lands on Cem's Solo and cannot pay the rent.
            Assert.AreEqual(GamePhase.AwaitingDebtPayment, engine.State.Phase);
            Assert.AreEqual(MoneyReason.Rent, engine.State.Debt.Reason);
            Assert.AreEqual(RejectionReason.NotYourTurn, Reason(engine, new SellPropertyCommand(Bob, A1)));
            Assert.AreEqual(RejectionReason.NotOwner, Reason(engine, new SellPropertyCommand(Alice, A2)));
            Assert.AreEqual(RejectionReason.InvalidSquare, Reason(engine, new SellPropertyCommand(Alice, 3)));

            Accept(engine, new SellPropertyCommand(Alice, A1));

            Assert.AreEqual(GameState.NoOwner, engine.State.GetOwner(A1));
            Assert.AreEqual(10 + 60, Money(engine, Alice), "The bank pays the whole price.");
            Assert.AreEqual(RejectionReason.NotOwner, Reason(engine, new SellPropertyCommand(Alice, A1)));
            Assert.AreEqual(GamePhase.AwaitingDebtPayment, engine.State.Phase, "The debt is still to be paid.");
        }

        [Test]
        public void SellingABuiltTownPaysForItsBuildingsToo()
        {
            var engine = NewGame(money: 5000);
            Give(engine, A1, Alice);
            Stand(engine, Alice, A1);
            Accept(engine, new BuildCommand(Alice, A1));
            Assert.AreEqual(60 + 25, GameRules.PropertySaleProceeds(engine.Board, engine.Rules, engine.State, A1));
            Assert.AreEqual(RejectionReason.WrongPhase, Reason(engine, new SellPropertyCommand(Alice, A1)));

            // Alice owes Bob more than she holds.
            GameStateReducer.Apply(engine.State, new DebtIncurredEvent(Alice, Bob, 9000, MoneyReason.Rent));
            GameStateReducer.Apply(engine.State, new PhaseChangedEvent(GamePhase.AwaitingDebtPayment));
            int before = Money(engine, Alice);
            CommandResult result = Accept(engine, new SellPropertyCommand(Alice, A1));

            Assert.AreEqual(before + 85, Money(engine, Alice), "The whole price plus the buildings' sellback value.");
            Assert.AreEqual(0, engine.State.GetBuildingLevel(A1));
            Assert.AreEqual(GameState.NoOwner, engine.State.GetOwner(A1));
            Assert.IsTrue(result.Events.Any(e => e is BuildingLevelChangedEvent b && b.Level == 0), "The buildings come down before the town changes hands.");
        }

        // ---- Trades ----

        [Test]
        public void AcceptedTradeSwapsPropertiesAndMoney()
        {
            var engine = NewGame(players: 3);
            Give(engine, A1, Alice);
            Give(engine, Solo, Bob);

            var offer = new TradeOffer(Alice, Bob, 50, 0, new[] { A1 }, new[] { Solo });
            Accept(engine, new ProposeTradeCommand(offer));
            Assert.AreEqual(GamePhase.AwaitingTradeResponse, engine.State.Phase);
            Assert.AreEqual(RejectionReason.WrongPhase, Reason(engine, new RollDiceCommand(Alice)));
            Assert.AreEqual(RejectionReason.NotYourTurn, Reason(engine, new AcceptTradeCommand(Cem)));
            Assert.AreEqual(RejectionReason.NotYourTurn, Reason(engine, new AcceptTradeCommand(Alice)));

            Accept(engine, new AcceptTradeCommand(Bob));

            Assert.AreEqual(Bob, engine.State.GetOwner(A1));
            Assert.AreEqual(Alice, engine.State.GetOwner(Solo));
            Assert.AreEqual(1000 - 50, Money(engine, Alice));
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

            Stand(engine, Alice, A1);
            Accept(engine, new BuildCommand(Alice, A1));
            Assert.AreEqual(RejectionReason.InvalidTrade, Reason(engine, new ProposeTradeCommand(new TradeOffer(Alice, Bob, 0, 0, new[] { A1 }, new[] { Solo }))),
                "A town with buildings cannot be traded.");
            Accept(engine, new ProposeTradeCommand(new TradeOffer(Alice, Bob, 0, 0, new[] { A2 }, new[] { Solo })));
        }

        // ---- Debts and bankruptcy ----

        [Test]
        public void DebtBlocksTheTurnUntilPaidAfterSellingAProperty()
        {
            // Alice starts on square 5 and rolls 1+1 to land on Bob's A1 (one house, without the rest of the colour: rent 50).
            // She holds 40 and Solo.
            var engine = NewGame(money: 1000, dice: new[] { 1, 1 });
            Give(engine, A1, Bob);
            GameStateReducer.Apply(engine.State, new BuildingLevelChangedEvent(A1, 1));
            Give(engine, Solo, Alice);
            GameStateReducer.Apply(engine.State, new MoneyChangedEvent(Alice, -960, MoneyReason.Tax));
            GameStateReducer.Apply(engine.State, new PlayerMovedEvent(Alice, 0, 5, false));

            Accept(engine, new RollDiceCommand(Alice));

            Assert.AreEqual(GamePhase.AwaitingDebtPayment, engine.State.Phase);
            Assert.AreEqual(50, engine.State.Debt.Amount);
            Assert.AreEqual(Bob, engine.State.Debt.CreditorId);
            Assert.AreEqual(RejectionReason.WrongPhase, Reason(engine, new RollDiceCommand(Alice)));
            Assert.AreEqual(RejectionReason.InsufficientFunds, Reason(engine, new PayDebtCommand(Alice)));

            Accept(engine, new SellPropertyCommand(Alice, Solo)); // +100, its whole price
            Accept(engine, new PayDebtCommand(Alice));

            Assert.AreEqual(90, Money(engine, Alice));
            Assert.AreEqual(GameState.NoOwner, engine.State.GetOwner(Solo));
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
        public void BankruptcySellsEverythingToTheBankAndHandsTheMoneyToTheCreditor()
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
            Assert.AreEqual(1000 + 10 + 4 * 25 + 60 + 80, Money(engine, Cem), "Cash, the buildings' sell-back value and the towns' sale value.");
            Assert.AreEqual(GameState.NoOwner, engine.State.GetOwner(A1), "The towns are sold to the bank, not handed over.");
            Assert.AreEqual(GameState.NoOwner, engine.State.GetOwner(A2));
            Assert.AreEqual(0, engine.State.GetBuildingLevel(A1));
            Assert.AreEqual(0, engine.State.GetBuildingLevel(A2));
            Assert.IsNull(engine.State.Debt);
            Assert.AreEqual(Bob, engine.State.CurrentPlayer.Id);
            Assert.AreEqual(GamePhase.WaitingForRoll, engine.State.Phase);
        }

        // Alice holds A1 and A2, has no money and owes the bank 50 on her own turn.
        private static GameEngine AliceOwesTheBank()
        {
            var engine = new GameEngine(Board(), TestRules(1000, passStartBonus: 0), TestSeats(3), new ScriptedDice());
            Give(engine, A1, Alice);
            Give(engine, A2, Alice);
            GameStateReducer.Apply(engine.State, new MoneyChangedEvent(Alice, -1000, MoneyReason.Tax));
            GameStateReducer.Apply(engine.State, new DebtIncurredEvent(Alice, GameState.NoOwner, 50, MoneyReason.Tax));
            GameStateReducer.Apply(engine.State, new PhaseChangedEvent(GamePhase.AwaitingDebtPayment));
            return engine;
        }

        [Test]
        public void ABankruptPlayersSquaresGoBackToTheBankForTheNextVisitor()
        {
            GameEngine engine = AliceOwesTheBank();
            Accept(engine, new DeclareBankruptcyCommand(Alice));

            Assert.IsTrue(engine.State.FindPlayer(Alice).IsBankrupt);
            Assert.AreEqual(GameState.NoOwner, engine.State.GetOwner(A1));
            Assert.AreEqual(GameState.NoOwner, engine.State.GetOwner(A2));
            Assert.AreEqual(Bob, engine.State.CurrentPlayer.Id, "The next player's turn begins at once.");
            Assert.AreEqual(GamePhase.WaitingForRoll, engine.State.Phase);
        }

        [Test]
        public void TheCreditorGetsTheSaleMoneyNotTheProperties()
        {
            GameEngine engine = AliceOwesCem();
            Give(engine, A1, Alice);

            Accept(engine, new RollDiceCommand(Alice));
            CommandResult result = Accept(engine, new DeclareBankruptcyCommand(Alice));

            Assert.AreEqual(GameState.NoOwner, engine.State.GetOwner(A1), "The creditor no longer inherits the towns (the owner's rule, Faz 23).");
            Assert.IsFalse(result.Events.OfType<PropertyOwnerChangedEvent>().Any(e => e.NewOwnerId == Cem));
            Assert.AreEqual(1000 + 10 + 30, Money(engine, Cem), "All she had: 10 in hand and the 30 the bank paid for A1.");
            Assert.AreEqual(0, Money(engine, Alice));
        }

        // ---- Automatic sale ----

        // Alice holds the money and the towns given (with house counts) and owes the bank on her own turn.
        private static GameEngine AliceOwesTheBank(int money, int debt, params (int square, int houses)[] towns)
        {
            var engine = new GameEngine(Board(), TestRules(1000, passStartBonus: 0), TestSeats(3), new ScriptedDice());
            foreach ((int square, int houses) in towns)
            {
                Give(engine, square, Alice);
                if (houses > 0) GameStateReducer.Apply(engine.State, new BuildingLevelChangedEvent(square, houses));
            }

            GameStateReducer.Apply(engine.State, new MoneyChangedEvent(Alice, money - 1000, MoneyReason.Tax));
            GameStateReducer.Apply(engine.State, new DebtIncurredEvent(Alice, GameState.NoOwner, debt, MoneyReason.Tax));
            GameStateReducer.Apply(engine.State, new PhaseChangedEvent(GamePhase.AwaitingDebtPayment));
            return engine;
        }

        private static IReadOnlyList<GameCommand> Plan(GameEngine engine) =>
            DebtSettlement.Plan(engine.Board, engine.Rules, engine.State, Alice);

        private static void Run(GameEngine engine, IReadOnlyList<GameCommand> plan)
        {
            foreach (GameCommand command in plan) Accept(engine, command);
        }

        [Test]
        public void TheAutomaticSaleSellsTheCheapestTownThatCoversTheDebt()
        {
            // Missing 35: A1 brings 30 (too little), A2 brings 40, Solo 50.
            GameEngine engine = AliceOwesTheBank(0, 35, (A1, 0), (A2, 0), (Solo, 0));
            IReadOnlyList<GameCommand> plan = Plan(engine);

            CollectionAssert.AreEqual(new[] { A2 }, DebtSettlement.SquaresSold(plan));
            Assert.IsInstanceOf<PayDebtCommand>(plan.Last(), "The plan ends by paying.");
            Run(engine, plan);
            Assert.IsNull(engine.State.Debt);
            Assert.AreEqual(5, Money(engine, Alice));
            Assert.AreEqual(Alice, engine.State.GetOwner(A1));
            Assert.AreEqual(Alice, engine.State.GetOwner(Solo));
        }

        [Test]
        public void WhenNoSingleTownCoversItTheSmallestGoFirst()
        {
            // Missing 80: nothing covers it alone, so A1 (30) goes, then Solo (50) covers the remaining 50.
            GameEngine engine = AliceOwesTheBank(0, 80, (A1, 0), (A2, 0), (Solo, 0));
            IReadOnlyList<GameCommand> plan = Plan(engine);

            CollectionAssert.AreEqual(new[] { A1, Solo }, DebtSettlement.SquaresSold(plan));
            Run(engine, plan);
            Assert.IsNull(engine.State.Debt);
            Assert.AreEqual(0, Money(engine, Alice));
            Assert.AreEqual(Alice, engine.State.GetOwner(A2), "A2 is kept.");
        }

        [Test]
        public void TheAutomaticSaleKeepsTheTownsWithBuildings()
        {
            // A1 with a house brings 55, Solo brings 50; both cover 50, the unbuilt one goes.
            GameEngine engine = AliceOwesTheBank(0, 50, (A1, 1), (Solo, 0));
            CollectionAssert.AreEqual(new[] { Solo }, DebtSettlement.SquaresSold(Plan(engine)));
            Run(engine, Plan(engine));
            Assert.AreEqual(1, engine.State.GetBuildingLevel(A1));
        }

        [Test]
        public void TheAutomaticSaleSellsNothingWhenTheCashIsEnoughOrNothingIsOwed()
        {
            GameEngine rich = AliceOwesTheBank(100, 50, (A1, 0));
            IReadOnlyList<GameCommand> plan = Plan(rich);
            Assert.AreEqual(1, plan.Count);
            Assert.IsInstanceOf<PayDebtCommand>(plan[0]);

            Assert.AreEqual(0, DebtSettlement.Plan(Board(), TestRules(1000), NewGame().State, Alice).Count, "No debt, no sale.");
            Assert.AreEqual(0, DebtSettlement.Plan(rich.Board, rich.Rules, rich.State, Bob).Count, "Only the debtor sells.");
        }

        [Test]
        public void ADebtNothingCouldCoverIsGivenUp()
        {
            GameEngine engine = AliceOwesTheBank(0, 9000, (A1, 0));
            IReadOnlyList<GameCommand> plan = Plan(engine);
            Assert.AreEqual(1, plan.Count);
            Assert.IsInstanceOf<DeclareBankruptcyCommand>(plan[0]);
        }

        [Test]
        public void ARentNothingCouldCoverBankruptsThePlayerAtOnce()
        {
            GameEngine engine = AliceOwesCem(); // 10 in cash, nothing to sell, rent 20.
            CommandResult result = Accept(engine, new RollDiceCommand(Alice));

            Assert.IsTrue(result.Events.Any(e => e is DebtIncurredEvent d && d.Amount == 20), "The debt is still told, so the screen can say why.");
            Assert.IsTrue(result.Events.Any(e => e is PlayerBankruptedEvent));
            Assert.IsFalse(result.Events.Any(e => e is PhaseChangedEvent p && p.Phase == GamePhase.AwaitingDebtPayment), "No selling screen.");
            Assert.IsTrue(engine.State.FindPlayer(Alice).IsBankrupt);
            Assert.AreEqual(1000 + 10, Money(engine, Cem), "The creditor gets what there was.");
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
            var built = NewGame(players: 3, money: 2000, dice: new[] { 1, 3 });
            Give(built, A1, Alice);
            Give(built, A2, Alice);
            Stand(built, Alice, A1);
            Accept(built, new BuildCommand(Alice, A1));
            Stand(built, Alice, 0);
            Accept(built, new RollDiceCommand(Alice));
            AssertSnapshotRoundTrip(built);
            Assert.AreEqual(1, GameStateSnapshot.Capture(built.State).Restore().GetBuildingLevel(A1));

            var trade = NewGame();
            Give(trade, A1, Alice);
            Accept(trade, new ProposeTradeCommand(new TradeOffer(Alice, Bob, 5, 0, new[] { A1 }, null)));
            AssertSnapshotRoundTrip(trade);

            GameEngine debt = AliceOwesCem();
            Give(debt, A1, Alice); // Something to sell, or the rent would bankrupt her at once.
            Accept(debt, new RollDiceCommand(Alice));
            AssertSnapshotRoundTrip(debt);
            Assert.AreEqual(20, GameStateSnapshot.Capture(debt.State).Restore().Debt.Amount);
        }
    }
}
