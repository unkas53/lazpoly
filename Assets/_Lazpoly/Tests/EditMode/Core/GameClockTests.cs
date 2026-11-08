using System.Linq;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Events;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;
using NUnit.Framework;
using static Lazpoly.Core.Tests.TestData;

namespace Lazpoly.Core.Tests
{
    /// <summary>The game clock (the owner's rule, Faz 28): game time is reported to the engine; when it runs out, the turn on play ends the game and the richest wins.</summary>
    public class GameClockTests
    {
        private static RulesConfig Minutes(int minutes) => new RulesConfig(1000, 200, 6, 3, 2, 50, timeLimitMinutes: minutes);

        private static GameEngine NewGame(RulesConfig rules, params int[] dice) =>
            new GameEngine(TestBoard(), rules, TestSeats(2), new ScriptedDice(dice));

        [Test]
        public void TheClockRecordsTheTimePlayedAndNeverGoesBack()
        {
            GameEngine engine = NewGame(Minutes(30));
            CommandResult result = engine.AdvanceClock(10);
            Assert.AreEqual(10, result.Events.OfType<GameClockEvent>().Single().PlayedSeconds);
            Assert.AreEqual(10, engine.State.PlayedSeconds);
            Assert.IsEmpty(engine.AdvanceClock(10).Events, "No news, no event.");
            Assert.IsEmpty(engine.AdvanceClock(5).Events);
            Assert.AreEqual(10, engine.State.PlayedSeconds);
            Assert.AreEqual(GamePhase.WaitingForRoll, engine.State.Phase, "The clock moves no turn.");

            GameState copy = GameStateSnapshot.Capture(engine.State).Restore();
            Assert.AreEqual(10, copy.PlayedSeconds);
            Assert.AreEqual(engine.State.ComputeHash(), copy.ComputeHash());
        }

        [Test]
        public void WhenTheTimeRunsOutTheTurnOnPlayEndsTheGameAndTheRichestWins()
        {
            GameEngine engine = NewGame(Minutes(1), 1, 3);
            Assert.IsFalse(GameRules.TimeIsUp(engine.Rules, engine.State));
            engine.AdvanceClock(60);
            Assert.IsTrue(GameRules.TimeIsUp(engine.Rules, engine.State));
            Assert.AreEqual(GamePhase.WaitingForRoll, engine.State.Phase, "The player on turn still plays it out.");

            Assert.IsTrue(engine.Execute(new RollDiceCommand(Alice)).IsAccepted);
            Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase);
            CommandResult end = engine.Execute(new EndTurnCommand(Alice));
            GameEndedEvent ended = end.Events.OfType<GameEndedEvent>().Single();
            Assert.AreEqual(GameEndReason.TimeLimit, ended.Reason);
            Assert.AreEqual(GameRules.Richest(engine.Board, engine.State), ended.WinnerId);
            Assert.AreEqual(GamePhase.GameOver, engine.State.Phase);
            Assert.IsFalse(end.Events.OfType<TurnStartedEvent>().Any(), "Nobody starts another turn.");
            Assert.IsEmpty(engine.AdvanceClock(70).Events, "The clock stops with the game.");
        }

        [Test]
        public void ATableWithoutAGameClockNeverRunsOut()
        {
            GameEngine engine = NewGame(Minutes(0), 1, 3);
            engine.AdvanceClock(100000);
            Assert.IsFalse(GameRules.TimeIsUp(engine.Rules, engine.State));
            Assert.IsTrue(engine.Execute(new RollDiceCommand(Alice)).IsAccepted);
            Assert.IsTrue(engine.Execute(new EndTurnCommand(Alice)).IsAccepted);
            Assert.AreEqual(Bob, engine.State.CurrentPlayer.Id);
        }

        [Test]
        public void TheTableCarriesTheTimeLimitIntoTheRules()
        {
            RulesConfig rules = TestRules().WithTable(new TableRules(0, 0, 15, 60));
            Assert.AreEqual(60, rules.TimeLimitMinutes);
            Assert.AreEqual(3600, rules.TimeLimitSeconds);
            Assert.AreEqual(60, rules.Table.TimeLimitMinutes);
            Assert.AreEqual(TableRules.DefaultTimeLimitMinutes, TestRules().WithTable(TableRules.Default).TimeLimitMinutes);
            Assert.IsFalse(new TableRules(0, 0, 15, 30).SameAs(new TableRules(0, 0, 15, 60)));
        }
    }
}
