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
    /// Faz 24 champions' cup: whoever lands on it may pay its fee to bring the cup to one of their towns, whose rent goes up
    /// by 50%, then 60%, 70%... with every pick in the game. The cup stands in one town at a time: a new pick moves it.
    /// </summary>
    public class CupTests
    {
        private const int P1 = 1;
        private const int P2 = 2;
        private const int Resort = 3;
        private const int Rest = 4;
        private const int CupSquare = 5;
        private const int Fee = 50;

        // 0 Start, 1 P1 (rent 10), 2 P2 (rent 20), 3 Resort (50), 4 Rest, 5 Cup (fee 50, 50% then +10%), 6-11 Rest.
        private static BoardDefinition Board()
        {
            var squares = new SquareDefinition[12];
            squares[0] = SquareDefinition.Start("Start");
            for (int i = 1; i < squares.Length; i++) squares[i] = SquareDefinition.Rest($"Rest {i}");
            squares[P1] = SquareDefinition.Property("P1", 60, 10, 0);
            squares[P2] = SquareDefinition.Property("P2", 100, 20, 0);
            squares[Resort] = SquareDefinition.Resort("Resort", 200, 50, 0);
            squares[CupSquare] = SquareDefinition.Cup("Cup", Fee, 50, 10);
            return new BoardDefinition(squares);
        }

        private static readonly RulesConfig Rules = TestRules();

        /// <summary>A game set up through a snapshot (owners, money, an earlier cup), then played on with scripted dice.</summary>
        private static GameEngine Game(Action<GameStateSnapshot> setup, params int[] dice)
        {
            BoardDefinition board = Board();
            var fresh = new GameEngine(board, Rules, TestSeats(2), new ScriptedDice());
            GameStateSnapshot snapshot = GameStateSnapshot.Capture(fresh.State);
            setup(snapshot);
            return GameEngine.Restore(board, Rules, snapshot, new ScriptedDice(dice));
        }

        /// <summary>Alice owns both towns and the resort and rolls 2 + 3 onto the cup.</summary>
        private static GameEngine AliceOnTheCup(Action<GameStateSnapshot> more = null)
        {
            GameEngine engine = Game(s =>
            {
                s.Owners[P1] = Alice;
                s.Owners[P2] = Alice;
                s.Owners[Resort] = Alice;
                more?.Invoke(s);
            }, 2, 3);
            Accept(engine, new RollDiceCommand(Alice));
            Assert.AreEqual(CupSquare, Player(engine, Alice).Position);
            return engine;
        }

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

        private static int Rent(GameEngine engine, int square) => GameRules.CalculateRent(engine.Board, engine.Rules, engine.State, square);

        [Test]
        public void TheFirstPickPaysTheFeeAndRaisesTheTownsRentByHalf()
        {
            GameEngine engine = AliceOnTheCup();
            Assert.AreEqual(GamePhase.AwaitingCupChoice, engine.State.Phase);
            Assert.AreEqual(50, GameRules.NextCupPercent(engine.Board, engine.State, CupSquare));

            CommandResult hosted = Accept(engine, new HostCupCommand(Alice, P2));

            Assert.AreEqual(1000 - Fee, Player(engine, Alice).Money);
            Assert.IsTrue(hosted.Events.OfType<MoneyChangedEvent>().Any(e => e.PlayerId == Alice && e.Delta == -Fee && e.Reason == MoneyReason.Cup));
            Assert.IsTrue(hosted.Events.OfType<CupHostedEvent>().Any(e => e.PlayerId == Alice && e.SquareIndex == P2 && e.Percent == 50));
            Assert.AreEqual(P2, engine.State.CupSquare);
            Assert.AreEqual(50, engine.State.CupPercent);
            Assert.AreEqual(1, engine.State.CupPicks);
            Assert.AreEqual(30, Rent(engine, P2), "20 and half of it");
            Assert.AreEqual(20, GameRules.RentWithoutCup(engine.Board, engine.Rules, engine.State, P2));
            Assert.AreEqual(10, Rent(engine, P1), "only the cup's town changes");
            Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase);
        }

        [Test]
        public void ANewPickMovesTheCupAndEveryPickRaisesTheShareByTen()
        {
            GameEngine engine = AliceOnTheCup(s =>
            {
                s.CupSquare = P2;
                s.CupPercent = 50;
                s.CupPicks = 1;
            });
            Assert.AreEqual(60, GameRules.NextCupPercent(engine.Board, engine.State, CupSquare), "the second pick in the game, whoever made the first");

            Accept(engine, new HostCupCommand(Alice, P1));

            Assert.AreEqual(P1, engine.State.CupSquare, "the cup stands in one town only");
            Assert.AreEqual(60, engine.State.CupPercent);
            Assert.AreEqual(2, engine.State.CupPicks);
            Assert.AreEqual(16, Rent(engine, P1), "10 and 60% of it");
            Assert.AreEqual(20, Rent(engine, P2), "the town the cup left is back to its own rent");
        }

        [Test]
        public void PickingTheSameTownAgainKeepsTheCupThereAtTheNewShare()
        {
            GameEngine engine = AliceOnTheCup(s =>
            {
                s.CupSquare = P2;
                s.CupPercent = 60;
                s.CupPicks = 2;
            });

            Accept(engine, new HostCupCommand(Alice, P2));

            Assert.AreEqual(P2, engine.State.CupSquare);
            Assert.AreEqual(70, engine.State.CupPercent, "not added to the 60 it had: the share is the pick's");
            Assert.AreEqual(3, engine.State.CupPicks);
            Assert.AreEqual(34, Rent(engine, P2));
        }

        [Test]
        public void PassingPaysNothingAndLeavesTheCupWhereItWas()
        {
            GameEngine engine = AliceOnTheCup(s =>
            {
                s.CupSquare = P1;
                s.CupPercent = 50;
                s.CupPicks = 1;
            });

            CommandResult passed = Accept(engine, new DeclineCupCommand(Alice));

            Assert.AreEqual(1000, Player(engine, Alice).Money);
            Assert.IsFalse(passed.Events.OfType<CupHostedEvent>().Any());
            Assert.AreEqual(P1, engine.State.CupSquare);
            Assert.AreEqual(1, engine.State.CupPicks);
            Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase);
        }

        [Test]
        public void TheCupGoesOnlyToATownOfThePlayerOnTurn()
        {
            GameEngine engine = AliceOnTheCup(s => s.Owners[P1] = Bob);

            Assert.AreEqual(RejectionReason.InvalidSquare, Reason(engine, new HostCupCommand(Alice, Resort)), "never a holiday resort");
            Assert.AreEqual(RejectionReason.InvalidSquare, Reason(engine, new HostCupCommand(Alice, Rest)));
            Assert.AreEqual(RejectionReason.NotOwner, Reason(engine, new HostCupCommand(Alice, P1)), "not another player's town");
            Assert.AreEqual(RejectionReason.NotYourTurn, Reason(engine, new HostCupCommand(Bob, P1)));
            Assert.AreEqual(RejectionReason.WrongPhase, Reason(engine, new RollDiceCommand(Alice)));
            Accept(engine, new HostCupCommand(Alice, P2));
            Assert.AreEqual(RejectionReason.WrongPhase, Reason(engine, new DeclineCupCommand(Alice)), "the choice is over");
        }

        [Test]
        public void TheCupIsNotOfferedWithoutATownOrTheFee()
        {
            GameEngine resortOnly = Game(s => s.Owners[Resort] = Alice, 2, 3);
            Accept(resortOnly, new RollDiceCommand(Alice));
            Assert.AreEqual(GamePhase.TurnEnd, resortOnly.State.Phase, "a resort cannot take the cup");

            GameEngine poor = Game(s =>
            {
                s.Owners[P1] = Alice;
                s.Players[0].Money = Fee - 1;
            }, 2, 3);
            Accept(poor, new RollDiceCommand(Alice));
            Assert.AreEqual(GamePhase.TurnEnd, poor.State.Phase, "the fee cannot be paid");
            Assert.AreEqual(Fee - 1, Player(poor, Alice).Money);
        }

        [Test]
        public void AVisitorPaysTheRaisedRentToTheCupsTown()
        {
            GameEngine engine = Game(s =>
            {
                s.Owners[P2] = Alice;
                s.CupSquare = P2;
                s.CupPercent = 60;
                s.CupPicks = 2;
                s.CurrentPlayerIndex = 1;
            }, 1, 1);

            Accept(engine, new RollDiceCommand(Bob));

            Assert.AreEqual(1000 - 32, Player(engine, Bob).Money);
            Assert.AreEqual(1000 + 32, Player(engine, Alice).Money);
        }

        [Test]
        public void TheCupTravelsWithTheSnapshotAndTheHash()
        {
            GameEngine engine = AliceOnTheCup();
            ulong before = engine.State.ComputeHash();
            Accept(engine, new HostCupCommand(Alice, P2));
            Assert.AreNotEqual(before, engine.State.ComputeHash());

            GameStateSnapshot snapshot = GameStateSnapshot.Capture(engine.State);
            GameEngine restored = GameEngine.Restore(engine.Board, engine.Rules, snapshot, new ScriptedDice());
            Assert.AreEqual(P2, restored.State.CupSquare);
            Assert.AreEqual(50, restored.State.CupPercent);
            Assert.AreEqual(1, restored.State.CupPicks);
            Assert.AreEqual(engine.State.ComputeHash(), restored.State.ComputeHash());

            snapshot.CupSquare = Resort;
            Assert.Throws<ArgumentException>(() => GameEngine.Restore(engine.Board, engine.Rules, snapshot, new ScriptedDice()), "a resort never holds the cup");
            snapshot.CupSquare = GameState.NoCup;
            Assert.Throws<ArgumentException>(() => snapshot.Restore(), "picks and a share but no town");
            snapshot.CupPercent = 0;
            snapshot.CupPicks = 0;
            Assert.DoesNotThrow(() => snapshot.Restore());
            snapshot.CupSquare = P1;
            Assert.Throws<ArgumentException>(() => snapshot.Restore(), "a town but no share");
        }

        [Test]
        public void ACupNeedsARaise()
        {
            Assert.Throws<ArgumentException>(() => new BoardDefinition(new[] { SquareDefinition.Start("S"), SquareDefinition.Cup("Cup", 50, 0, 10) }));
            Assert.Throws<ArgumentException>(() => new BoardDefinition(new[] { SquareDefinition.Start("S"), SquareDefinition.Cup("Cup", -1, 50, 10) }));
            Assert.DoesNotThrow(() => new BoardDefinition(new[] { SquareDefinition.Start("S"), SquareDefinition.Cup("Cup", 0, 50, 0) }));
        }

        [Test]
        public void TheChecksumKnowsTheCupsNumbers()
        {
            ulong Sum(int percent, int step, int limit = 0) => BoardChecksum.Compute(new BoardDefinition(new[]
            {
                SquareDefinition.Start("S"), SquareDefinition.Cup("Cup", 50, percent, step, limit),
            }), Rules);

            Assert.AreNotEqual(Sum(50, 10), Sum(60, 10));
            Assert.AreNotEqual(Sum(50, 10), Sum(50, 20));
            Assert.AreNotEqual(Sum(50, 50, 500), Sum(50, 50));
        }

        [Test]
        public void TheRaiseGrowsByHalfEachPickUpToFiveHundredPercent()
        {
            // The owner's rule (Faz 29): 50, 100, 150... and never more than 500.
            var squares = new SquareDefinition[12];
            squares[0] = SquareDefinition.Start("Start");
            for (int i = 1; i < squares.Length; i++) squares[i] = SquareDefinition.Rest($"Rest {i}");
            squares[P1] = SquareDefinition.Property("P1", 60, 10, 0);
            squares[CupSquare] = SquareDefinition.Cup("Cup", Fee, 50, 50, 500);
            var board = new BoardDefinition(squares);
            var fresh = new GameEngine(board, Rules, TestSeats(2), new ScriptedDice());
            int[] expected = { 50, 100, 150, 200, 250, 300, 350, 400, 450, 500, 500, 500 };
            for (int picks = 0; picks < expected.Length; picks++)
            {
                GameStateSnapshot snapshot = GameStateSnapshot.Capture(fresh.State);
                if (picks > 0)
                {
                    snapshot.CupSquare = P1;
                    snapshot.CupPercent = 50;
                    snapshot.CupPicks = picks;
                }

                GameState state = GameEngine.Restore(board, Rules, snapshot, new ScriptedDice()).State;
                Assert.AreEqual(expected[picks], GameRules.NextCupPercent(board, state, CupSquare), $"pick {picks + 1}");
            }

            Assert.Throws<ArgumentException>(() => new BoardDefinition(new[] { SquareDefinition.Start("S"), SquareDefinition.Cup("Cup", 50, 50, 50, 40) }),
                "a limit under the first raise");
        }
    }
}
