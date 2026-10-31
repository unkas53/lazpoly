using System;
using Lazpoly.Core.Board;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;
using NUnit.Framework;

namespace Lazpoly.Core.Tests
{
    public class BoardAndSetupTests
    {
        [Test]
        public void BoardRequiresStartOnSquareZero()
        {
            Assert.Throws<ArgumentException>(() => new BoardDefinition(new[]
            {
                SquareDefinition.Rest("Rest"),
                SquareDefinition.Start("Start"),
            }));
        }

        [Test]
        public void BoardRejectsSecondStart()
        {
            Assert.Throws<ArgumentException>(() => new BoardDefinition(new[]
            {
                SquareDefinition.Start("Start"),
                SquareDefinition.Start("Another start"),
            }));
        }

        [Test]
        public void BoardRejectsFreeProperty()
        {
            Assert.Throws<ArgumentException>(() => new BoardDefinition(new[]
            {
                SquareDefinition.Start("Start"),
                SquareDefinition.Property("Free", 0, 10, 1),
            }));
        }

        [Test]
        public void BoardRejectsZeroTax()
        {
            Assert.Throws<ArgumentException>(() => new BoardDefinition(new[]
            {
                SquareDefinition.Start("Start"),
                SquareDefinition.Tax("Tax", 0),
            }));
        }

        [Test]
        public void CountInGroupCountsOnlyThatGroup()
        {
            BoardDefinition board = TestData.TestBoard();
            Assert.AreEqual(2, board.CountInGroup(1));
            Assert.AreEqual(2, board.CountInGroup(3));
            Assert.AreEqual(0, board.CountInGroup(0));
            Assert.AreEqual(0, board.CountInGroup(99));
        }

        [Test]
        public void RulesRejectInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RulesConfig(0, 200, 6, 3, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RulesConfig(1000, -1, 6, 3, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RulesConfig(1000, 200, 1, 3, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RulesConfig(1000, 200, 6, 0, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RulesConfig(1000, 200, 6, 3, 0));
        }

        [Test]
        public void InitialStateGivesEveryoneStartingMoneyOnStart()
        {
            GameState state = GameState.CreateInitial(TestData.TestBoard(), TestData.TestRules(startingMoney: 1234), TestData.TestSeats(3));

            Assert.AreEqual(3, state.Players.Count);
            foreach (PlayerState player in state.Players)
            {
                Assert.AreEqual(1234, player.Money);
                Assert.AreEqual(0, player.Position);
                Assert.IsFalse(player.IsBankrupt);
            }

            Assert.AreEqual(TestData.Alice, state.CurrentPlayer.Id);
            Assert.AreEqual(GamePhase.WaitingForRoll, state.Phase);
            Assert.AreEqual(1, state.TurnNumber);
            for (int square = 0; square < state.SquareCount; square++)
            {
                Assert.AreEqual(GameState.NoOwner, state.GetOwner(square));
            }
        }

        [Test]
        public void InitialStateRejectsWrongPlayerCounts()
        {
            Assert.Throws<ArgumentException>(() =>
                GameState.CreateInitial(TestData.TestBoard(), TestData.TestRules(), TestData.TestSeats(1)));

            var nine = new PlayerSeat[9];
            for (int i = 0; i < nine.Length; i++) nine[i] = new PlayerSeat(i, $"P{i}");
            Assert.Throws<ArgumentException>(() =>
                GameState.CreateInitial(TestData.TestBoard(), TestData.TestRules(), nine));
        }

        [Test]
        public void InitialStateRejectsDuplicateIds()
        {
            Assert.Throws<ArgumentException>(() => GameState.CreateInitial(TestData.TestBoard(), TestData.TestRules(),
                new[] { new PlayerSeat(1, "A"), new PlayerSeat(1, "B") }));
        }

        [Test]
        public void SameStateGivesSameHash()
        {
            GameState a = GameState.CreateInitial(TestData.TestBoard(), TestData.TestRules(), TestData.TestSeats(3));
            GameState b = GameState.CreateInitial(TestData.TestBoard(), TestData.TestRules(), TestData.TestSeats(3));
            Assert.AreEqual(a.ComputeHash(), b.ComputeHash());
        }
    }
}
