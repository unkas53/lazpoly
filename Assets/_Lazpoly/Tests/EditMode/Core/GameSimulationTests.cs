using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Events;
using Lazpoly.Core.Random;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;
using NUnit.Framework;

namespace Lazpoly.Core.Tests
{
    /// <summary>
    /// Plays many seeded games with random but legal decisions and checks the rules never
    /// reach an impossible state, and that clients replaying the events end up identical.
    /// </summary>
    public class GameSimulationTests
    {
        private const int CommandLimit = 3000;

        private sealed class SimulationResult
        {
            public GameEngine Engine;
            public List<GameEvent> Events = new List<GameEvent>();
        }

        private static PlayerSeat[] Seats(int count)
        {
            var seats = new PlayerSeat[count];
            for (int i = 0; i < count; i++) seats[i] = new PlayerSeat(i + 1, $"Player {i + 1}");
            return seats;
        }

        private static SimulationResult Play(ulong seed, int playerCount, RulesConfig rules)
        {
            var result = new SimulationResult
            {
                Engine = new GameEngine(TestData.TestBoard(), rules, Seats(playerCount), new Pcg32Random(seed)),
            };
            var decisions = new Pcg32Random(seed, stream: 7UL);
            GameEngine engine = result.Engine;

            for (int i = 0; i < CommandLimit && engine.State.Phase != GamePhase.GameOver; i++)
            {
                int playerId = engine.State.CurrentPlayer.Id;
                GameCommand command;
                switch (engine.State.Phase)
                {
                    case GamePhase.WaitingForRoll:
                        command = new RollDiceCommand(playerId);
                        break;
                    case GamePhase.AwaitingBuyDecision:
                        command = decisions.NextInt(0, 4) == 0
                            ? (GameCommand)new DeclinePurchaseCommand(playerId)
                            : new BuyPropertyCommand(playerId);
                        break;
                    default:
                        command = new EndTurnCommand(playerId);
                        break;
                }

                CommandResult outcome = engine.Execute(command);
                Assert.IsTrue(outcome.IsAccepted, $"Seed {seed}, step {i}: legal {command.GetType().Name} rejected ({outcome.Reason}).");
                result.Events.AddRange(outcome.Events);
                AssertInvariants(engine, seed, i);
            }

            return result;
        }

        private static void AssertInvariants(GameEngine engine, ulong seed, int step)
        {
            GameState state = engine.State;
            string where = $"seed {seed}, step {step}";

            Assert.IsFalse(state.CurrentPlayer.IsBankrupt && state.Phase != GamePhase.GameOver,
                $"{where}: a bankrupt player holds the turn.");

            foreach (PlayerState player in state.Players)
            {
                Assert.That(player.Position, Is.InRange(0, engine.Board.Count - 1), $"{where}: position out of board.");
                if (player.IsBankrupt)
                {
                    Assert.AreEqual(0, player.Money, $"{where}: bankrupt player kept money.");
                }
                else
                {
                    Assert.That(player.Money, Is.GreaterThanOrEqualTo(0), $"{where}: negative money.");
                }
            }

            for (int square = 0; square < engine.Board.Count; square++)
            {
                int owner = state.GetOwner(square);
                if (owner == GameState.NoOwner) continue;

                Assert.AreEqual(SquareType.Property, engine.Board[square].Type, $"{where}: non-property square {square} has an owner.");
                PlayerState ownerState = state.FindPlayer(owner);
                Assert.IsNotNull(ownerState, $"{where}: square {square} owned by unknown player.");
                Assert.IsFalse(ownerState.IsBankrupt, $"{where}: bankrupt player still owns square {square}.");
            }

            if (state.Phase == GamePhase.GameOver)
            {
                Assert.AreEqual(1, state.ActivePlayerCount(), $"{where}: game over with several players left.");
                Assert.IsFalse(state.FindPlayer(state.WinnerId).IsBankrupt, $"{where}: winner is bankrupt.");
            }
        }

        [Test]
        public void ManySeededGamesNeverBreakTheRules()
        {
            // Low starting money and pass bonus so that bankruptcies and game endings happen often.
            var rules = new RulesConfig(startingMoney: 400, passStartBonus: 50, diceSides: 6,
                maxConsecutiveDoubles: 3, fullGroupRentMultiplier: 2);
            int finished = 0;

            for (ulong seed = 1; seed <= 200; seed++)
            {
                int players = 2 + (int)(seed % 7); // 2..8
                SimulationResult result = Play(seed, players, rules);
                if (result.Engine.State.Phase == GamePhase.GameOver) finished++;
            }

            Assert.That(finished, Is.GreaterThan(0), "No simulated game reached the end; bankruptcy path untested.");
        }

        [Test]
        public void SameSeedPlaysTheSameGame()
        {
            RulesConfig rules = TestData.TestRules();
            SimulationResult first = Play(42UL, 4, rules);
            SimulationResult second = Play(42UL, 4, rules);

            Assert.AreEqual(first.Events.Count, second.Events.Count);
            Assert.AreEqual(first.Engine.State.ComputeHash(), second.Engine.State.ComputeHash());
        }

        [Test]
        public void ReplayingTheEventsRebuildsTheHostState()
        {
            RulesConfig rules = new RulesConfig(400, 50, 6, 3, 2);
            for (ulong seed = 1; seed <= 20; seed++)
            {
                SimulationResult host = Play(seed, 4, rules);

                // A client starts from the same lobby setup and applies only what the host broadcast.
                GameState client = GameState.CreateInitial(TestData.TestBoard(), rules, Seats(4));
                foreach (GameEvent gameEvent in host.Events)
                {
                    GameStateReducer.Apply(client, gameEvent);
                }

                Assert.AreEqual(host.Engine.State.ComputeHash(), client.ComputeHash(), $"Seed {seed}: client drifted.");
            }
        }
    }
}
