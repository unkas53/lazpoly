using System;
using System.Linq;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Events;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;
using NUnit.Framework;
using static Lazpoly.Core.Tests.TestData;

namespace Lazpoly.Core.Tests
{
    /// <summary>Phase 7c rules: cards, the pot, rides, lookouts, the round limit and table rules.</summary>
    public class SpecialSquaresTests
    {
        private const int P1 = 1;
        private const int Tax = 3;
        private const int CardSquare = 4;
        private const int PotSquare = 5;
        private const int P2 = 6;
        private const int Ride = 7;
        private const int Lookout = 8;
        private const int P3 = 9;

        // 0 Start, 1 P1, 2 Rest, 3 Tax 100, 4 Card, 5 Pot, 6 P2, 7 Travel 50, 8 Lookout, 9 P3, 10-11 Rest.
        private static BoardDefinition Board(params CardDefinition[] cards) => new BoardDefinition(new[]
        {
            SquareDefinition.Start("Start"),
            SquareDefinition.Property("P1", 60, 10, 0),
            SquareDefinition.Rest("Rest"),
            SquareDefinition.Tax("Tax", 100),
            SquareDefinition.Card("News"),
            SquareDefinition.Pot("Pot"),
            SquareDefinition.Property("P2", 100, 20, 0),
            SquareDefinition.Travel("Bus", 50),
            SquareDefinition.Lookout("View"),
            SquareDefinition.Property("P3", 200, 30, 0),
            SquareDefinition.Rest("Rest 2"),
            SquareDefinition.Rest("Rest 3"),
        }, cards.Length == 0 ? new[] { CardDefinition.Collect("Default", 1) } : cards);

        private static GameEngine NewGame(BoardDefinition board, RulesConfig rules, params int[] dice) =>
            new GameEngine(board, rules, TestSeats(2), new ScriptedDice(dice));

        private static GameEngine NewGame(BoardDefinition board, params int[] dice) => NewGame(board, TestRules(), dice);

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

        private static PlayerState Player(GameEngine engine, int id) => engine.State.FindPlayer(id);

        // ---- Pot ----

        [Test]
        public void TaxFillsThePotAndThePotSquarePaysItOut()
        {
            GameEngine engine = NewGame(Board(), 1, 2, 2, 3);
            Accept(engine, new RollDiceCommand(Alice));
            Assert.AreEqual(900, Player(engine, Alice).Money);
            Assert.AreEqual(100, engine.State.Pot);

            Accept(engine, new EndTurnCommand(Alice));
            CommandResult landed = Accept(engine, new RollDiceCommand(Bob));
            Assert.AreEqual(PotSquare, Player(engine, Bob).Position);
            Assert.AreEqual(1100, Player(engine, Bob).Money);
            Assert.AreEqual(0, engine.State.Pot);
            Assert.IsTrue(landed.Events.OfType<MoneyChangedEvent>().Any(e => e.Reason == MoneyReason.Pot && e.Delta == 100));
        }

        [Test]
        public void WithoutAPotSquareTaxGoesToTheBank()
        {
            var board = new BoardDefinition(new[]
            {
                SquareDefinition.Start("Start"), SquareDefinition.Rest("R"), SquareDefinition.Rest("R2"), SquareDefinition.Tax("Tax", 100),
            });
            GameEngine engine = NewGame(board, 1, 2);
            Accept(engine, new RollDiceCommand(Alice));
            Assert.AreEqual(900, Player(engine, Alice).Money);
            Assert.AreEqual(0, engine.State.Pot);
        }

        // 0 Start, 1 A (100, houses 50), 2 B (100, houses 50), 3 a tax of 10% of property value, 4 Rest.
        private static BoardDefinition PercentTaxBoard() => new BoardDefinition(new[]
        {
            SquareDefinition.Start("Start"),
            SquareDefinition.Property("A", 100, 10, 1, 50, new[] { 50, 150, 400, 550, 700 }),
            SquareDefinition.Property("B", 100, 10, 1, 50, new[] { 50, 150, 400, 550, 700 }),
            SquareDefinition.PercentTax("Vergi", 10),
            SquareDefinition.Rest("Rest"),
        });

        [Test]
        public void APercentTaxTakesItsShareOfThePropertyValueNotTheCash()
        {
            BoardDefinition board = PercentTaxBoard();
            GameEngine engine = NewGame(board, 1, 2);
            GameStateReducer.Apply(engine.State, new PropertyOwnerChangedEvent(1, GameState.NoOwner, Alice));
            GameStateReducer.Apply(engine.State, new PropertyOwnerChangedEvent(2, GameState.NoOwner, Alice));
            GameStateReducer.Apply(engine.State, new BuildingLevelChangedEvent(1, 2));
            Assert.AreEqual(100 + 100 + 2 * 50, GameRules.PropertyValue(board, engine.State, Alice));

            CommandResult result = Accept(engine, new RollDiceCommand(Alice));

            // 10% of 300 (two towns at their price, two houses at their cost); her 1000 in cash does not count.
            MoneyChangedEvent tax = result.Events.OfType<MoneyChangedEvent>().Single(e => e.Reason == MoneyReason.Tax);
            Assert.AreEqual(-30, tax.Delta);
            Assert.AreEqual(1000 - 30, Player(engine, Alice).Money);
            Assert.AreEqual(30, GameRules.TaxDue(board, engine.State, 3, Alice));
            Assert.AreEqual(0, GameRules.TaxDue(board, engine.State, 4, Alice), "a square that is no tax takes nothing");
        }

        [Test]
        public void APercentTaxTakesNothingFromAPlayerWithoutProperty()
        {
            GameEngine engine = NewGame(PercentTaxBoard(), 1, 2);
            CommandResult result = Accept(engine, new RollDiceCommand(Alice));
            Assert.IsFalse(result.Events.OfType<MoneyChangedEvent>().Any(e => e.Reason == MoneyReason.Tax));
            Assert.AreEqual(1000, Player(engine, Alice).Money);
        }

        [Test]
        public void ATaxNeedsAnAmountOrAPercentUpToAHundred()
        {
            BoardDefinition board = PercentTaxBoard();
            Assert.AreEqual(10, board[3].TaxPercent);
            Assert.AreEqual(0, board[3].TaxAmount);
            Assert.Throws<System.ArgumentException>(() => new BoardDefinition(new[] { SquareDefinition.Start("S"), SquareDefinition.PercentTax("T", 0) }));
            Assert.Throws<System.ArgumentException>(() => new BoardDefinition(new[] { SquareDefinition.Start("S"), SquareDefinition.PercentTax("T", 101) }));
        }

        // ---- Cards ----

        [Test]
        public void ACollectCardPaysThePlayer()
        {
            GameEngine engine = NewGame(Board(CardDefinition.Collect("Prize", 50)), 1, 3, 0);
            CommandResult result = Accept(engine, new RollDiceCommand(Alice));
            Assert.AreEqual(1050, Player(engine, Alice).Money);
            Assert.IsTrue(result.Events.OfType<CardDrawnEvent>().Any(e => e.PlayerId == Alice && e.CardIndex == 0));
            Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase);
        }

        [Test]
        public void APayCardFillsThePot()
        {
            GameEngine engine = NewGame(Board(CardDefinition.Collect("Prize", 50), CardDefinition.Pay("Fine", 30)), 1, 3, 1);
            Accept(engine, new RollDiceCommand(Alice));
            Assert.AreEqual(970, Player(engine, Alice).Money);
            Assert.AreEqual(30, engine.State.Pot);
        }

        [Test]
        public void AMoveToCardWalksForwardPastStartAndLandsThere()
        {
            GameEngine engine = NewGame(Board(CardDefinition.MoveTo("Go to P1", P1)), 1, 3, 0);
            CommandResult result = Accept(engine, new RollDiceCommand(Alice));
            PlayerMovedEvent move = result.Events.OfType<PlayerMovedEvent>().Last();
            Assert.AreEqual(P1, move.ToSquare);
            Assert.IsTrue(move.PassedStart);
            Assert.AreEqual(MoveKind.Walk, move.Kind);
            Assert.AreEqual(1200, Player(engine, Alice).Money, "Passing start pays the bonus.");
            Assert.AreEqual(GamePhase.AwaitingBuyDecision, engine.State.Phase);
        }

        [Test]
        public void AMoveBackCardNeverPaysTheStartBonus()
        {
            GameEngine engine = NewGame(Board(CardDefinition.MoveBy("Back", -3)), 1, 3, 0);
            CommandResult result = Accept(engine, new RollDiceCommand(Alice));
            PlayerMovedEvent move = result.Events.OfType<PlayerMovedEvent>().Last();
            Assert.AreEqual(P1, move.ToSquare);
            Assert.AreEqual(MoveKind.Back, move.Kind);
            Assert.IsFalse(move.PassedStart);
            Assert.AreEqual(1000, Player(engine, Alice).Money);
            Assert.AreEqual(GamePhase.AwaitingBuyDecision, engine.State.Phase);
        }

        [Test]
        public void RepairsChargePerHouseAndPerHotel()
        {
            GameEngine engine = NewGame(Board(CardDefinition.Repairs("Repairs", 25, 100)), 1, 3, 0);
            GameStateReducer.Apply(engine.State, new PropertyOwnerChangedEvent(P1, GameState.NoOwner, Alice));
            GameStateReducer.Apply(engine.State, new PropertyOwnerChangedEvent(P2, GameState.NoOwner, Alice));
            GameStateReducer.Apply(engine.State, new BuildingLevelChangedEvent(P1, 2));
            GameStateReducer.Apply(engine.State, new BuildingLevelChangedEvent(P2, SquareDefinition.MaxBuildingLevel));

            Accept(engine, new RollDiceCommand(Alice));
            Assert.AreEqual(1000 - 2 * 25 - 100, Player(engine, Alice).Money);
            Assert.AreEqual(150, engine.State.Pot);
        }

        [Test]
        public void ACardFineNothingCouldCoverBankruptsThePlayerToThePot()
        {
            GameEngine engine = NewGame(Board(CardDefinition.Pay("Big fine", 400)), TestRules(startingMoney: 100), 1, 3, 0);
            // 100 in hand and P3 (the bank pays its whole price, 200) cannot make 400.
            GameStateReducer.Apply(engine.State, new PropertyOwnerChangedEvent(P3, GameState.NoOwner, Alice));
            CommandResult result = Accept(engine, new RollDiceCommand(Alice));

            // Going bankrupt to the pot at once: P3 is sold to the bank and all the money (100 + 200) goes into the pot.
            DebtIncurredEvent debt = result.Events.OfType<DebtIncurredEvent>().Single();
            Assert.AreEqual(GameState.PotId, debt.CreditorId);
            Assert.AreEqual(MoneyReason.Card, debt.Reason);
            Assert.IsTrue(Player(engine, Alice).IsBankrupt);
            Assert.AreEqual(300, engine.State.Pot);
            Assert.AreEqual(GameState.NoOwner, engine.State.GetOwner(P3));
            Assert.AreEqual(GamePhase.GameOver, engine.State.Phase);
            Assert.AreEqual(Bob, engine.State.WinnerId);
        }

        // ---- Rides ----

        /// <summary>
        /// Alice comes to the bus stop (3+4) and her turn ends there; Bob takes his turn (2+3, to the empty pot); Alice's next
        /// turn starts at the stop, where she picks a ride before rolling (the owner's rule, Faz 27). <paramref name="more"/> are her later dice.
        /// </summary>
        private static GameEngine AliceBackAtTheStop(RulesConfig rules, params int[] more)
        {
            var dice = new System.Collections.Generic.List<int> { 3, 4, 2, 3 };
            dice.AddRange(more);
            GameEngine engine = NewGame(Board(), rules, dice.ToArray());
            Accept(engine, new RollDiceCommand(Alice));
            Assert.AreEqual(Ride, Player(engine, Alice).Position);
            Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase, "Coming to the stop ends the turn: the ride waits for the next one.");
            Accept(engine, new EndTurnCommand(Alice));
            Accept(engine, new RollDiceCommand(Bob));
            Accept(engine, new EndTurnCommand(Bob));
            return engine;
        }

        private static GameEngine AliceBackAtTheStop(params int[] more) => AliceBackAtTheStop(TestRules(), more);

        [Test]
        public void TheRideWaitsForThePlayersNextTurn()
        {
            GameEngine engine = NewGame(Board(), 3, 4);
            CommandResult result = Accept(engine, new RollDiceCommand(Alice));
            Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase);
            Assert.IsFalse(result.Events.OfType<PhaseChangedEvent>().Any(e => e.Phase == GamePhase.AwaitingTravelDecision), "No ride on arriving.");
            Assert.AreEqual(1000, Player(engine, Alice).Money);

            engine = AliceBackAtTheStop();
            Assert.AreEqual(Alice, engine.State.CurrentPlayer.Id);
            Assert.AreEqual(GamePhase.AwaitingTravelDecision, engine.State.Phase, "Her next turn starts with the ride.");
            Assert.AreEqual(RejectionReason.WrongPhase, Reason(engine, new RollDiceCommand(Alice)), "The ride comes before the roll.");
        }

        [Test]
        public void ADoubleToTheBusStopEarnsNoSecondRoll()
        {
            var board = new BoardDefinition(new[]
            {
                SquareDefinition.Start("Start"), SquareDefinition.Rest("R1"), SquareDefinition.Rest("R2"), SquareDefinition.Rest("R3"),
                SquareDefinition.Travel("Bus", 50), SquareDefinition.Rest("R5"), SquareDefinition.Rest("R6"), SquareDefinition.Rest("R7"),
            });
            GameEngine engine = NewGame(board, TestRules(), 2, 2);
            Accept(engine, new RollDiceCommand(Alice));
            Assert.IsTrue(engine.State.LastRollWasDouble);
            Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase, "A double that brings the player to the stop ends the turn all the same.");
            Assert.AreEqual(RejectionReason.WrongPhase, Reason(engine, new RollDiceCommand(Alice)));
        }

        [Test]
        public void ARidePaysTheFareAndWalksThenTheTurnsRollFollows()
        {
            GameEngine engine = AliceBackAtTheStop(1, 2);

            Assert.AreEqual(RejectionReason.InvalidSquare, Reason(engine, new TravelCommand(Alice, Ride)));
            Assert.AreEqual(RejectionReason.InvalidSquare, Reason(engine, new TravelCommand(Alice, 99)));
            Assert.AreEqual(RejectionReason.NotYourTurn, Reason(engine, new TravelCommand(Bob, P3)));
            Assert.AreEqual(RejectionReason.WrongPhase, Reason(engine, new EndTurnCommand(Alice)));

            CommandResult result = Accept(engine, new TravelCommand(Alice, P3));
            PlayerMovedEvent move = result.Events.OfType<PlayerMovedEvent>().Single();
            Assert.AreEqual(MoveKind.Walk, move.Kind, "The pawn walks there square by square, as after a roll.");
            Assert.AreEqual(Ride, move.FromSquare);
            Assert.IsFalse(move.PassedStart);
            Assert.AreEqual(950, Player(engine, Alice).Money);
            Assert.AreEqual(P3, Player(engine, Alice).Position);
            Assert.AreEqual(GamePhase.AwaitingBuyDecision, engine.State.Phase);

            Accept(engine, new DeclinePurchaseCommand(Alice));
            Assert.AreEqual(GamePhase.WaitingForRoll, engine.State.Phase, "Then she rolls, as on any turn.");
            Accept(engine, new RollDiceCommand(Alice));
            Assert.AreEqual((P3 + 3) % engine.Board.Count, Player(engine, Alice).Position);
            Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase, "Not a double: the turn can end.");
        }

        [Test]
        public void ARideRoundPastStartCollectsTheBonus()
        {
            GameEngine engine = AliceBackAtTheStop();

            CommandResult result = Accept(engine, new TravelCommand(Alice, P1));
            Assert.IsTrue(result.Events.OfType<PlayerMovedEvent>().Single().PassedStart);
            Assert.IsTrue(result.Events.OfType<MoneyChangedEvent>().Any(e => e.Reason == MoneyReason.PassedStart && e.Delta == 200));
            Assert.AreEqual(1000 - 50 + 200, Player(engine, Alice).Money);
        }

        [Test]
        public void ARideNeverEndsOnAnotherPlayersTown()
        {
            GameEngine engine = AliceBackAtTheStop();
            GameStateReducer.Apply(engine.State, new PropertyOwnerChangedEvent(P3, GameState.NoOwner, Bob));
            GameStateReducer.Apply(engine.State, new PropertyOwnerChangedEvent(P2, GameState.NoOwner, Alice));

            Assert.AreEqual(RejectionReason.InvalidSquare, Reason(engine, new TravelCommand(Alice, P3)), "Bob's town is off the ride map.");
            Assert.IsTrue(GameRules.IsRideDestination(engine.Board, engine.State, Player(engine, Alice), P2), "Her own town is fine.");
            Assert.IsTrue(GameRules.IsRideDestination(engine.Board, engine.State, Player(engine, Alice), PotSquare), "So are the special squares.");
            Assert.IsTrue(GameRules.IsRideDestination(engine.Board, engine.State, Player(engine, Alice), P1), "And free towns.");
            Accept(engine, new TravelCommand(Alice, P2));
            Assert.AreEqual(GamePhase.WaitingForRoll, engine.State.Phase, "Her own town asks nothing; the roll follows.");
        }

        [Test]
        public void StayingAtTheStopRollsFromThere()
        {
            GameEngine engine = AliceBackAtTheStop(1, 1);
            Accept(engine, new DeclineTravelCommand(Alice));
            Assert.AreEqual(GamePhase.WaitingForRoll, engine.State.Phase);
            Assert.AreEqual(1000, Player(engine, Alice).Money);
            Accept(engine, new RollDiceCommand(Alice));
            Assert.AreEqual(Ride + 2, Player(engine, Alice).Position);
        }

        [Test]
        public void NoRideIsOfferedToAPlayerWhoCannotPayTheFare()
        {
            GameEngine engine = AliceBackAtTheStop(TestRules(startingMoney: 40));
            Assert.AreEqual(Alice, engine.State.CurrentPlayer.Id);
            Assert.AreEqual(GamePhase.WaitingForRoll, engine.State.Phase, "She simply rolls from the stop.");
        }

        // ---- Lookout ----

        [Test]
        public void ALookoutGivesTheNextRollAChoiceOfTwo()
        {
            GameEngine engine = NewGame(Board(), 3, 5, 1, 2, 1, 2, 4, 6);
            Accept(engine, new RollDiceCommand(Alice));
            Assert.AreEqual(Lookout, Player(engine, Alice).Position);
            Assert.IsTrue(Player(engine, Alice).HasLookout);
            Accept(engine, new EndTurnCommand(Alice));
            Accept(engine, new RollDiceCommand(Bob));
            Accept(engine, new EndTurnCommand(Bob));

            Accept(engine, new RollDiceCommand(Alice));
            Assert.AreEqual(GamePhase.AwaitingDiceChoice, engine.State.Phase);
            CollectionAssert.AreEqual(new[] { 1, 2, 4, 6 }, engine.State.DiceOptions);
            Assert.IsFalse(Player(engine, Alice).HasLookout, "The lookout is used up by the double roll.");
            Assert.AreEqual(RejectionReason.InvalidChoice, Reason(engine, new ChooseDiceCommand(Alice, 2)));
            Assert.AreEqual(RejectionReason.WrongPhase, Reason(engine, new RollDiceCommand(Alice)));

            Accept(engine, new ChooseDiceCommand(Alice, 1));
            Assert.IsNull(engine.State.DiceOptions);
            Assert.AreEqual(4, engine.State.LastDie1);
            Assert.AreEqual(6, engine.State.LastDie2);
            Assert.AreEqual(P2, Player(engine, Alice).Position, "8 + 10 wraps round to square 6.");
            Assert.AreEqual(GamePhase.AwaitingBuyDecision, engine.State.Phase);
        }

        [Test]
        public void ALookoutCardWorksLikeTheSquare()
        {
            GameEngine engine = NewGame(Board(CardDefinition.Lookout("Binoculars")), 1, 3, 0);
            Accept(engine, new RollDiceCommand(Alice));
            Assert.IsTrue(Player(engine, Alice).HasLookout);
        }

        // ---- Table rules ----

        [Test]
        public void TheRoundLimitEndsTheGameWithTheRichestPlayer()
        {
            RulesConfig rules = TestRules().WithTable(new TableRules(0, 1));
            GameEngine engine = NewGame(Board(), rules, 1, 2, 2, 3);
            Accept(engine, new RollDiceCommand(Alice));
            Accept(engine, new EndTurnCommand(Alice));
            Assert.AreEqual(1, engine.State.Round);
            Accept(engine, new RollDiceCommand(Bob));
            CommandResult end = Accept(engine, new EndTurnCommand(Bob));

            GameEndedEvent ended = end.Events.OfType<GameEndedEvent>().Single();
            Assert.AreEqual(GameEndReason.RoundLimit, ended.Reason);
            Assert.AreEqual(Bob, ended.WinnerId, "Bob collected the pot, so he is richer.");
            Assert.AreEqual(GamePhase.GameOver, engine.State.Phase);
        }

        [Test]
        public void RoundsCountWhenTheTurnComesBackToTheFirstSeat()
        {
            GameEngine engine = NewGame(Board(), 1, 2, 1, 2, 1, 2);
            Assert.AreEqual(1, engine.State.Round);
            Accept(engine, new RollDiceCommand(Alice));
            Accept(engine, new EndTurnCommand(Alice));
            Assert.AreEqual(1, engine.State.Round);
            Accept(engine, new RollDiceCommand(Bob));
            Accept(engine, new EndTurnCommand(Bob));
            Assert.AreEqual(2, engine.State.Round);
        }

        [Test]
        public void TableRulesReplaceOnlyWhatTheyChoose()
        {
            RulesConfig board = TestRules(startingMoney: 1500);
            RulesConfig same = board.WithTable(TableRules.Default);
            Assert.AreEqual(1500, same.StartingMoney);
            Assert.AreEqual(0, same.RoundLimit);

            RulesConfig custom = board.WithTable(new TableRules(2000, 20, 60));
            Assert.AreEqual(2000, custom.StartingMoney);
            Assert.AreEqual(20, custom.RoundLimit);
            Assert.AreEqual(60, custom.TurnSeconds);
            Assert.AreEqual(board.PassStartBonus, custom.PassStartBonus);
            Assert.IsTrue(custom.Table.SameAs(new TableRules(2000, 20, 60)));
        }

        // ---- Data and state ----

        [Test]
        public void BoardsRejectBrokenCardData()
        {
            SquareDefinition[] squares = { SquareDefinition.Start("S"), SquareDefinition.Card("C"), SquareDefinition.Rest("R") };
            Assert.Throws<ArgumentException>(() => new BoardDefinition(squares), "A card square needs cards.");
            Assert.Throws<ArgumentException>(() => new BoardDefinition(squares, new[] { CardDefinition.MoveTo("To the card", 1) }));
            Assert.Throws<ArgumentException>(() => new BoardDefinition(squares, new[] { CardDefinition.MoveTo("Off the board", 7) }));
            Assert.Throws<ArgumentException>(() => new BoardDefinition(squares, new[] { CardDefinition.Pay("Free", 0) }));
            Assert.Throws<ArgumentException>(() => new BoardDefinition(squares, new[] { CardDefinition.MoveBy("Nowhere", 0) }));
            Assert.Throws<ArgumentException>(() =>
                new BoardDefinition(new[] { SquareDefinition.Start("S"), SquareDefinition.Rest("R") }, new[] { CardDefinition.Collect("No square", 5) }));
            Assert.DoesNotThrow(() => new BoardDefinition(squares, new[] { CardDefinition.MoveTo("Home", 0) }));
        }

        [Test]
        public void CardsAndFaresArePartOfTheBoardChecksum()
        {
            ulong a = BoardChecksum.Compute(Board(CardDefinition.Collect("A", 10)), TestRules());
            ulong b = BoardChecksum.Compute(Board(CardDefinition.Collect("A", 20)), TestRules());
            Assert.AreNotEqual(a, b);
            Assert.AreEqual(a, BoardChecksum.Compute(Board(CardDefinition.Collect("A", 10)), TestRules()));

            var cheapRide = new BoardDefinition(new[] { SquareDefinition.Start("S"), SquareDefinition.Travel("Bus", 10) });
            var dearRide = new BoardDefinition(new[] { SquareDefinition.Start("S"), SquareDefinition.Travel("Bus", 90) });
            Assert.AreNotEqual(BoardChecksum.Compute(cheapRide, TestRules()), BoardChecksum.Compute(dearRide, TestRules()));
        }

        [Test]
        public void SnapshotsKeepThePotTheRoundAndAPendingDiceChoice()
        {
            GameEngine engine = NewGame(Board(), 1, 2, 2, 6, 2, 3, 1, 1, 2, 2);
            Accept(engine, new RollDiceCommand(Alice));
            Accept(engine, new EndTurnCommand(Alice));
            Accept(engine, new RollDiceCommand(Bob));
            Assert.AreEqual(Lookout, Player(engine, Bob).Position);
            Accept(engine, new EndTurnCommand(Bob));
            Assert.AreEqual(100, engine.State.Pot);
            Assert.AreEqual(2, engine.State.Round);

            GameState copy = GameStateSnapshot.Capture(engine.State).Restore();
            Assert.AreEqual(engine.State.ComputeHash(), copy.ComputeHash());
            Assert.IsTrue(copy.FindPlayer(Bob).HasLookout);
            Assert.AreEqual(100, copy.Pot);
            Assert.AreEqual(2, copy.Round);

            Accept(engine, new RollDiceCommand(Alice));
            Accept(engine, new EndTurnCommand(Alice));
            Accept(engine, new RollDiceCommand(Bob));
            Assert.AreEqual(GamePhase.AwaitingDiceChoice, engine.State.Phase);
            GameState choosing = GameStateSnapshot.Capture(engine.State).Restore();
            Assert.AreEqual(engine.State.ComputeHash(), choosing.ComputeHash());
            CollectionAssert.AreEqual(engine.State.DiceOptions, choosing.DiceOptions);
        }
    }
}
