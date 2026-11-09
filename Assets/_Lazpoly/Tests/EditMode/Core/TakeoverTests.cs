using System;
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
    /// <summary>
    /// Faz 31 takeovers: after paying rent on a rival's city, a player who still holds 150% of its worth (land and buildings)
    /// may take it over with its buildings; the money goes to the owner. Hotels, shielded towns and resorts are not offered.
    /// </summary>
    public class TakeoverTests
    {
        private const int B1 = 5;

        /// <summary>Bob owns B1 (price 120, houses 100 each) with <paramref name="level"/> levels; Alice rolls 2 + 3 onto it.</summary>
        private static GameEngine AliceOnBobsTown(int level = 2, int money = 1000, Action<GameStateSnapshot> more = null)
        {
            BoardDefinition board = DevelopableBoard();
            var fresh = new GameEngine(board, TestRules(), TestSeats(2), new ScriptedDice());
            GameStateSnapshot snapshot = GameStateSnapshot.Capture(fresh.State);
            snapshot.Owners[B1] = Bob;
            snapshot.BuildingLevels[B1] = level;
            snapshot.Players[0].Money = money;
            more?.Invoke(snapshot);
            GameEngine engine = GameEngine.Restore(board, TestRules(), snapshot, new ScriptedDice(2, 3));
            Accept(engine, new RollDiceCommand(Alice));
            Assert.AreEqual(B1, AliceIn(engine).Position);
            return engine;
        }

        private static PlayerState AliceIn(GameEngine engine) => engine.State.FindPlayer(Alice);
        private static PlayerState BobIn(GameEngine engine) => engine.State.FindPlayer(Bob);

        private static CommandResult Accept(GameEngine engine, GameCommand command)
        {
            CommandResult result = engine.Execute(command);
            Assert.IsTrue(result.IsAccepted, $"{command.GetType().Name} was rejected: {result.Reason}");
            return result;
        }

        [Test]
        public void TheTakeoverPriceIsHalfAgainTheTownsWorth()
        {
            GameEngine engine = AliceOnBobsTown();
            Assert.AreEqual(120 + 2 * 100, GameRules.TakeoverWorth(engine.Board, engine.State, B1));
            Assert.AreEqual(480, GameRules.TakeoverPrice(engine.Board, engine.State, B1));
        }

        [Test]
        public void AfterPayingTheRentTheTownMayBeTakenOverWithItsBuildings()
        {
            GameEngine engine = AliceOnBobsTown();
            int rent = 20 * 15;
            Assert.AreEqual(GamePhase.AwaitingTakeoverDecision, engine.State.Phase, "the rent is paid first, then the offer");
            Assert.AreEqual(1000 - rent, AliceIn(engine).Money);

            CommandResult result = Accept(engine, new TakeOverCommand(Alice));
            Assert.AreEqual(Alice, engine.State.GetOwner(B1));
            Assert.AreEqual(2, engine.State.GetBuildingLevel(B1), "the houses go with the town");
            Assert.AreEqual(1000 - rent - 480, AliceIn(engine).Money);
            Assert.AreEqual(1000 + rent + 480, BobIn(engine).Money, "the price goes to the owner");
            Assert.IsTrue(result.Events.OfType<MoneyChangedEvent>().Any(e => e.Reason == MoneyReason.Takeover && e.PlayerId == Bob && e.Delta == 480));
            Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase, "not a double: the turn can end");
        }

        [Test]
        public void DecliningLeavesTheTownWithItsOwner()
        {
            GameEngine engine = AliceOnBobsTown();
            Assert.AreEqual(RejectionReason.WrongPhase, engine.Validate(new EndTurnCommand(Alice)));
            Accept(engine, new DeclineTakeoverCommand(Alice));
            Assert.AreEqual(Bob, engine.State.GetOwner(B1));
            Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase);
        }

        [Test]
        public void OnlyTheLandedPlayerDecides()
        {
            GameEngine engine = AliceOnBobsTown();
            Assert.AreEqual(RejectionReason.NotYourTurn, engine.Validate(new TakeOverCommand(Bob)));
            Assert.AreEqual(RejectionReason.NotYourTurn, engine.Validate(new DeclineTakeoverCommand(Bob)));
        }

        [Test]
        public void AHotelIsNeverOffered()
        {
            GameEngine engine = AliceOnBobsTown(level: SquareDefinition.MaxBuildingLevel, money: 100000);
            Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase);
            Assert.AreEqual(RejectionReason.WrongPhase, engine.Validate(new TakeOverCommand(Alice)));
        }

        [Test]
        public void AShieldedTownIsNeverOffered()
        {
            GameEngine engine = AliceOnBobsTown(more: s => s.Shields[B1] = true);
            Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase);
        }

        [Test]
        public void APlayerShortOfThePriceAfterTheRentIsNotAsked()
        {
            // 700 - 300 rent = 400, short of 480.
            GameEngine engine = AliceOnBobsTown(money: 700);
            Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase);
            Assert.AreEqual(400, AliceIn(engine).Money);
        }

        [Test]
        public void ABareTownIsOfferedToo()
        {
            GameEngine engine = AliceOnBobsTown(level: 0);
            Assert.AreEqual(GamePhase.AwaitingTakeoverDecision, engine.State.Phase);
            Assert.AreEqual(180, GameRules.TakeoverPrice(engine.Board, engine.State, B1));
        }

        [Test]
        public void AResortIsNotATownToTakeOver()
        {
            var board = new BoardDefinition(new[]
            {
                SquareDefinition.Start("Start"), SquareDefinition.Rest("Rest 1"), SquareDefinition.Rest("Rest 2"),
                SquareDefinition.Rest("Rest 3"), SquareDefinition.Rest("Rest 4"), SquareDefinition.Resort("Sea", 200, 50, 0),
                SquareDefinition.Rest("Rest 5"), SquareDefinition.Rest("Rest 6"),
            });
            var fresh = new GameEngine(board, TestRules(), TestSeats(2), new ScriptedDice());
            GameStateSnapshot snapshot = GameStateSnapshot.Capture(fresh.State);
            snapshot.Owners[5] = Bob;
            GameEngine engine = GameEngine.Restore(board, TestRules(), snapshot, new ScriptedDice(2, 3));
            Accept(engine, new RollDiceCommand(Alice));
            Assert.AreEqual(950, AliceIn(engine).Money, "the resort's rent is paid");
            Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase);
        }
    }
}
