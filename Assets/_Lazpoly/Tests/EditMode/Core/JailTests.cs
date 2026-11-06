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
    /// <summary>
    /// Faz 23 jail: landing there, rolling three doubles in a row or drawing the jail card locks a player in
    /// for three of their turns; on each of them they may pay the fee and roll at once, or roll for free
    /// and leave on a double.
    /// </summary>
    public class JailTests
    {
        private const int JailSquare = 4;
        private const int Fee = 50;
        private const int Turns = 3;

        private const int P2 = 10;
        private const int CardSquare = 11;

        // 0 Start, 1 P1, 2-3 Rest, 4 Jail 50, 5-9 Rest, 10 P2, 11 Card (the jail card).
        private static BoardDefinition Board()
        {
            var squares = new SquareDefinition[12];
            squares[0] = SquareDefinition.Start("Start");
            squares[1] = SquareDefinition.Property("P1", 60, 10, 0);
            for (int i = 2; i < squares.Length; i++) squares[i] = SquareDefinition.Rest($"Rest {i}");
            squares[JailSquare] = SquareDefinition.Jail("Jail", Fee);
            squares[P2] = SquareDefinition.Property("P2", 100, 10, 0);
            squares[CardSquare] = SquareDefinition.Card("News");
            return new BoardDefinition(squares, new[] { CardDefinition.GoToJail("Straight to jail") });
        }

        private static RulesConfig Rules(int startingMoney = 1000) =>
            new RulesConfig(startingMoney, 200, 6, 3, 2, 50, jailTurns: Turns);

        private static GameEngine NewGame(RulesConfig rules, params int[] dice) =>
            new GameEngine(Board(), rules, TestSeats(2), new ScriptedDice(dice));

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

        /// <summary>Alice rolls a double onto the jail, Bob takes a quiet turn, and it is Alice's jailed turn.</summary>
        private static GameEngine AliceJailedOnHerTurn(RulesConfig rules, params int[] more)
        {
            GameEngine engine = NewGame(rules, new[] { 2, 2, 1, 2 }.Concat(more).ToArray());
            Accept(engine, new RollDiceCommand(Alice));
            Accept(engine, new EndTurnCommand(Alice));
            Accept(engine, new RollDiceCommand(Bob));
            Accept(engine, new EndTurnCommand(Bob));
            return engine;
        }

        [Test]
        public void LandingOnTheJailLocksThePlayerInAndEndsTheTurnEvenAfterADouble()
        {
            GameEngine engine = NewGame(Rules(), 2, 2);
            CommandResult landed = Accept(engine, new RollDiceCommand(Alice));

            Assert.AreEqual(JailSquare, Player(engine, Alice).Position);
            Assert.AreEqual(Turns, Player(engine, Alice).JailTurns);
            Assert.IsTrue(Player(engine, Alice).InJail);
            Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase, "the double earns no second roll");
            Assert.IsTrue(landed.Events.OfType<JailChangedEvent>().Any(e => e.PlayerId == Alice && e.Turns == Turns));
            Assert.AreEqual(RejectionReason.WrongPhase, Reason(engine, new RollDiceCommand(Alice)));
        }

        [Test]
        public void AJailedTurnStartsWithTheChoiceToPayOrWait()
        {
            GameEngine engine = AliceJailedOnHerTurn(Rules());

            Assert.AreEqual(Alice, engine.State.CurrentPlayer.Id);
            Assert.AreEqual(GamePhase.AwaitingJailDecision, engine.State.Phase);
            Assert.AreEqual(RejectionReason.WrongPhase, Reason(engine, new RollDiceCommand(Alice)));
            Assert.AreEqual(RejectionReason.WrongPhase, Reason(engine, new EndTurnCommand(Alice)));
            Assert.AreEqual(RejectionReason.NotYourTurn, Reason(engine, new RollInJailCommand(Bob)));
            Assert.AreEqual(Fee, GameRules.JailFee(engine.Board));
        }

        [Test]
        public void PayingTheFeeLetsThePlayerOutToRollAtOnce()
        {
            GameEngine engine = AliceJailedOnHerTurn(Rules(), 1, 3);
            CommandResult paid = Accept(engine, new PayJailFeeCommand(Alice));

            Assert.AreEqual(1000 - Fee, Player(engine, Alice).Money);
            Assert.IsFalse(Player(engine, Alice).InJail);
            Assert.AreEqual(GamePhase.WaitingForRoll, engine.State.Phase);
            Assert.IsTrue(paid.Events.OfType<MoneyChangedEvent>().Any(e => e.PlayerId == Alice && e.Delta == -Fee && e.Reason == MoneyReason.Jail));

            Accept(engine, new RollDiceCommand(Alice));
            Assert.AreEqual(JailSquare + 4, Player(engine, Alice).Position);
        }

        [Test]
        public void PayingNeedsTheFeeInHandButRollingIsFree()
        {
            GameEngine engine = AliceJailedOnHerTurn(Rules(startingMoney: Fee - 1), 1, 2);

            Assert.AreEqual(RejectionReason.InsufficientFunds, Reason(engine, new PayJailFeeCommand(Alice)));
            Accept(engine, new RollInJailCommand(Alice));
            Assert.AreEqual(Fee - 1, Player(engine, Alice).Money, "rolling in jail is free");
        }

        [Test]
        public void AfterThreeRollsWithoutADoubleThePlayerIsFree()
        {
            GameEngine engine = AliceJailedOnHerTurn(Rules(), 1, 2, 1, 2, 1, 2, 1, 2, 1, 2, 1, 2, 1, 3);

            for (int wait = Turns - 1; wait >= 0; wait--)
            {
                Assert.AreEqual(GamePhase.AwaitingJailDecision, engine.State.Phase);
                CommandResult rolled = Accept(engine, new RollInJailCommand(Alice));
                Assert.IsTrue(rolled.Events.OfType<DiceRolledEvent>().Any(e => !e.IsDouble), "the dice are rolled every jailed turn");
                Assert.AreEqual(wait, Player(engine, Alice).JailTurns);
                Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase, "waiting ends the turn");
                Assert.AreEqual(JailSquare, Player(engine, Alice).Position);
                Accept(engine, new EndTurnCommand(Alice));
                Accept(engine, new RollDiceCommand(Bob));
                Accept(engine, new EndTurnCommand(Bob));
            }

            Assert.AreEqual(1000, Player(engine, Alice).Money, "rolling in jail costs nothing");
            Assert.AreEqual(GamePhase.WaitingForRoll, engine.State.Phase, "the fourth turn is free");
            Accept(engine, new RollDiceCommand(Alice));
            Assert.AreEqual(JailSquare + 4, Player(engine, Alice).Position);
        }

        [Test]
        public void ADoubleInJailLetsThePlayerOutToMoveByItWithoutASecondRoll()
        {
            GameEngine engine = AliceJailedOnHerTurn(Rules(), 3, 3);
            CommandResult rolled = Accept(engine, new RollInJailCommand(Alice));

            Assert.IsFalse(Player(engine, Alice).InJail);
            Assert.AreEqual(P2, Player(engine, Alice).Position, "the double moves the player");
            Assert.AreEqual(1000, Player(engine, Alice).Money, "leaving on a double is free");
            Assert.IsTrue(rolled.Events.OfType<JailChangedEvent>().Any(e => e.Turns == 0 && e.RolledOut));
            Assert.IsTrue(engine.State.RolledOutOfJail);
            Assert.AreEqual(GamePhase.AwaitingBuyDecision, engine.State.Phase);

            Accept(engine, new BuyPropertyCommand(Alice));
            Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase, "the double that opened the cell earns no second roll");
            Accept(engine, new EndTurnCommand(Alice));
            Assert.IsFalse(engine.State.RolledOutOfJail, "a new turn forgets it");
        }

        [Test]
        public void TheJailCardFliesThePlayerToJailWithoutPassingStart()
        {
            GameEngine engine = NewGame(Rules(), 5, 6, 0);
            CommandResult drawn = Accept(engine, new RollDiceCommand(Alice));

            Assert.AreEqual(JailSquare, Player(engine, Alice).Position);
            Assert.AreEqual(Turns, Player(engine, Alice).JailTurns);
            Assert.AreEqual(1000, Player(engine, Alice).Money, "no start bonus");
            Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase);
            PlayerMovedEvent flight = drawn.Events.OfType<PlayerMovedEvent>().Last();
            Assert.AreEqual(MoveKind.Jump, flight.Kind, "a flight, not a walk");
            Assert.IsFalse(flight.PassedStart);

            Assert.Throws<ArgumentException>(() => new BoardDefinition(
                new[] { SquareDefinition.Start("S"), SquareDefinition.Card("C") }, new[] { CardDefinition.GoToJail("Go") }),
                "a jail card needs a jail");
        }

        [Test]
        public void ThreeDoublesInARowSendThePlayerToJail()
        {
            GameEngine engine = NewGame(Rules(), 3, 3, 1, 1, 2, 2);
            Accept(engine, new RollDiceCommand(Alice));
            Accept(engine, new RollDiceCommand(Alice));
            Assert.AreEqual(8, Player(engine, Alice).Position);

            CommandResult third = Accept(engine, new RollDiceCommand(Alice));
            Assert.AreEqual(JailSquare, Player(engine, Alice).Position, "the third double goes straight to jail, not 4 squares on");
            Assert.AreEqual(Turns, Player(engine, Alice).JailTurns);
            Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase);
            Assert.AreEqual(1000, Player(engine, Alice).Money, "going back to jail is not a lap");
            Assert.IsTrue(third.Events.OfType<PlayerMovedEvent>().Any(e => e.Kind == MoveKind.Jump && e.ToSquare == JailSquare && !e.PassedStart));
        }

        [Test]
        public void TheJailedTurnsSurviveASnapshotAndCountInTheHash()
        {
            GameEngine engine = AliceJailedOnHerTurn(Rules());
            GameStateSnapshot snapshot = GameStateSnapshot.Capture(engine.State);
            Assert.AreEqual(Turns, snapshot.Players[0].JailTurns);

            GameState copy = snapshot.Restore();
            Assert.AreEqual(Turns, copy.FindPlayer(Alice).JailTurns);
            Assert.AreEqual(engine.State.ComputeHash(), copy.ComputeHash());

            GameStateReducer.Apply(copy, new JailChangedEvent(Alice, 1));
            Assert.AreNotEqual(engine.State.ComputeHash(), copy.ComputeHash());

            GameState out_ = GameStateSnapshot.Capture(engine.State).Restore();
            GameStateReducer.Apply(out_, new JailChangedEvent(Alice, 0, rolledOut: true));
            Assert.IsTrue(GameStateSnapshot.Capture(out_).Restore().RolledOutOfJail);
            Assert.AreNotEqual(engine.State.ComputeHash(), out_.ComputeHash());
            Assert.Throws<InvalidOperationException>(() => GameStateReducer.Apply(copy, new JailChangedEvent(Alice, -1)));

            snapshot.Players[0].JailTurns = -1;
            Assert.Throws<ArgumentException>(() => snapshot.Restore());
        }

        [Test]
        public void ABoardHasAtMostOneJailAndItsExitHasAPrice()
        {
            Assert.Throws<ArgumentException>(() => new BoardDefinition(new[]
            {
                SquareDefinition.Start("S"), SquareDefinition.Jail("J1", 50), SquareDefinition.Jail("J2", 50),
            }));
            Assert.Throws<ArgumentException>(() => new BoardDefinition(new[] { SquareDefinition.Start("S"), SquareDefinition.Jail("J", 0) }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RulesConfig(1000, 200, 6, 3, 2, 50, jailTurns: -1));

            Assert.AreEqual(JailSquare, Board().JailIndex);
            Assert.IsFalse(TestBoard().HasJail);
            Assert.AreEqual(-1, TestBoard().JailIndex);
            Assert.AreEqual(Turns, Rules().WithTable(TableRules.Default).JailTurns, "the table keeps the board's jail");
        }
    }
}
