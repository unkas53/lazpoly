using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Events;
using Lazpoly.Core.State;
using NUnit.Framework;
using static Lazpoly.Core.Tests.TestData;

namespace Lazpoly.Core.Tests
{
    /// <summary>Holiday resorts: bought like towns, never built on, the rent grows with how many the owner holds.</summary>
    public class ResortTests
    {
        // 0 Start, 1-4 the four resorts (group 9, price 200, 50 per resort held), 5 Rest.
        private static readonly int[] Resorts = { 1, 2, 3, 4 };

        private static BoardDefinition Board() => new BoardDefinition(new[]
        {
            SquareDefinition.Start("Start"),
            SquareDefinition.Resort("Bodrum", 200, 50, 9),
            SquareDefinition.Resort("Bali", 200, 50, 9),
            SquareDefinition.Resort("Santorini", 200, 50, 9),
            SquareDefinition.Resort("Maldivler", 200, 50, 9),
            SquareDefinition.Rest("Rest"),
        });

        private static GameEngine NewGame(params int[] dice) =>
            new GameEngine(Board(), TestRules(1000, passStartBonus: 0), TestSeats(2), new ScriptedDice(dice));

        private static void Give(GameEngine engine, int square, int playerId) =>
            GameStateReducer.Apply(engine.State, new PropertyOwnerChangedEvent(square, engine.State.GetOwner(square), playerId));

        [Test]
        public void EachResortsRentIsFiftyTimesTheResortsItsOwnerHolds()
        {
            GameEngine engine = NewGame();
            Assert.AreEqual(0, engine.CalculateRent(1), "nobody owns it yet");
            int[] expected = { 50, 100, 150, 200 };
            for (int held = 1; held <= Resorts.Length; held++)
            {
                Give(engine, Resorts[held - 1], Bob);
                Assert.AreEqual(held, GameRules.ResortsOwned(engine.Board, engine.State, Bob));
                for (int i = 0; i < held; i++)
                {
                    Assert.AreEqual(expected[held - 1], engine.CalculateRent(Resorts[i]), $"{held} resorts held: each one asks {expected[held - 1]}");
                }
            }
        }

        [Test]
        public void ResortsHeldByAnotherPlayerDoNotRaiseTheRent()
        {
            GameEngine engine = NewGame();
            Give(engine, 1, Bob);
            Give(engine, 2, Bob);
            Give(engine, 3, Alice);
            Assert.AreEqual(100, engine.CalculateRent(1));
            Assert.AreEqual(50, engine.CalculateRent(3));
            Assert.AreEqual(0, GameRules.ResortsOwned(engine.Board, engine.State, Cem));
        }

        [Test]
        public void LandingOnAResortPaysItsOwnerForEveryResortTheyHold()
        {
            GameEngine engine = NewGame(1, 2);
            int alice = engine.State.CurrentPlayer.Id;
            int other = alice == Alice ? Bob : Alice;
            Give(engine, 1, other);
            Give(engine, 3, other);

            CommandResult result = engine.Execute(new RollDiceCommand(alice));
            Assert.IsTrue(result.IsAccepted, $"{result.Reason}");
            Assert.AreEqual(3, engine.State.FindPlayer(alice).Position);
            Assert.AreEqual(1000 - 100, engine.State.FindPlayer(alice).Money, "two resorts held: 100");
            Assert.AreEqual(1000 + 100, engine.State.FindPlayer(other).Money);
        }

        [Test]
        public void AResortIsBoughtButNeverBuiltOn()
        {
            GameEngine engine = NewGame(1, 1);
            int player = engine.State.CurrentPlayer.Id;
            Assert.IsTrue(engine.Execute(new RollDiceCommand(player)).IsAccepted);
            Assert.AreEqual(2, engine.State.FindPlayer(player).Position);
            CommandResult bought = engine.Execute(new BuyPropertyCommand(player));
            Assert.IsTrue(bought.IsAccepted, $"{bought.Reason}");
            Assert.AreEqual(player, engine.State.GetOwner(2));
            Assert.AreEqual(1000 - 200, engine.State.FindPlayer(player).Money);

            Assert.AreEqual(0, GameRules.BuildableNow(engine.Board, engine.State, engine.State.FindPlayer(player), 2));
            Assert.AreEqual(0, GameRules.BuildableAfterPurchase(engine.Board, engine.State.FindPlayer(player), 2));
            CommandResult build = engine.Execute(new BuildCommand(player, 2));
            Assert.IsFalse(build.IsAccepted);
            Assert.AreEqual(RejectionReason.CannotBuild, build.Reason);
        }

        [Test]
        public void AResortIsMarkedAndCountsInTheBoardChecksum()
        {
            Assert.IsTrue(Board()[1].IsResort);
            Assert.IsFalse(Board()[1].IsDevelopable);
            Assert.IsFalse(SquareDefinition.Property("Town", 100, 10, 1).IsResort);
            Assert.AreNotEqual(BoardChecksum.Compute(Board(), TestRules()), BoardChecksum.Compute(new BoardDefinition(new[]
            {
                SquareDefinition.Start("Start"),
                SquareDefinition.Property("Bodrum", 200, 50, 9),
                SquareDefinition.Resort("Bali", 200, 50, 9),
                SquareDefinition.Resort("Santorini", 200, 50, 9),
                SquareDefinition.Resort("Maldivler", 200, 50, 9),
                SquareDefinition.Rest("Rest"),
            }), TestRules()), "the checksum tells a resort from a town");
        }
    }
}
