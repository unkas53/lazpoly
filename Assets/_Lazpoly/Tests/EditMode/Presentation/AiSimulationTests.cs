using System.Collections.Generic;
using Lazpoly.App.AI;
using Lazpoly.App.Session;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Events;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Data;
using NUnit.Framework;

namespace Lazpoly.Presentation.Tests
{
    /// <summary>
    /// Phase 5 gate: whole games of eight computer players on the shipped board and rules. Every move
    /// must be accepted, the state must stay sound, and every game must end with a winner.
    /// </summary>
    public class AiSimulationTests
    {
        private const int Games = 25;
        private const int CommandLimit = 40000;

        [Test]
        public void EightComputerPlayersFinishEveryGame()
        {
            BoardData data = BoardDataLoader.LoadFromResources();
            var seen = new HashSet<System.Type>();
            int totalTurns = 0;

            for (ulong seed = 1; seed <= Games; seed++)
            {
                var seats = new PlayerSeat[8];
                var ai = new Dictionary<int, AiDifficulty>();
                for (int i = 0; i < seats.Length; i++)
                {
                    seats[i] = new PlayerSeat(i + 1, $"AI {i + 1}");
                    ai.Add(i + 1, (AiDifficulty)(1 + (int)((seed + (ulong)i) % 3)));
                }

                LocalGame game = LocalGame.Create(data.Board, data.Rules, seats, ai, seed);
                LocalGameSession session = game.Session;
                session.EventsPublished += events =>
                {
                    foreach (GameEvent gameEvent in events) seen.Add(gameEvent.GetType());
                };

                int commands = 0;
                while (session.State.Phase != GamePhase.GameOver)
                {
                    Assert.Less(commands, CommandLimit, $"Seed {seed}: no winner after {CommandLimit} moves (turn {session.State.TurnNumber}).");
                    Assert.IsTrue(session.StepAi(), $"Seed {seed}: the game waited although only computers play.");
                    commands++;
                    AssertSound(session.State, data, seed);
                }

                Assert.IsFalse(session.State.FindPlayer(session.State.WinnerId).IsBankrupt, $"Seed {seed}: the winner is bankrupt.");
                totalTurns += session.State.TurnNumber;
            }

            foreach (System.Type expected in new[]
                     {
                         typeof(BuildingLevelChangedEvent), typeof(AuctionEndedEvent), typeof(BidPlacedEvent), typeof(TradeResolvedEvent),
                         typeof(MortgageChangedEvent), typeof(DebtIncurredEvent), typeof(PlayerBankruptedEvent),
                     })
            {
                Assert.IsTrue(seen.Contains(expected), $"The computer players never produced {expected.Name}.");
            }

            TestContext.WriteLine($"{Games} games, {totalTurns / Games} turns on average.");
        }

        private static void AssertSound(GameState state, BoardData data, ulong seed)
        {
            foreach (PlayerState player in state.Players)
            {
                Assert.That(player.Money, Is.GreaterThanOrEqualTo(0), $"Seed {seed}: {player.Name} has negative money.");
            }

            for (int square = 0; square < state.SquareCount; square++)
            {
                if (state.GetBuildingLevel(square) == 0) continue;
                Assert.IsFalse(state.IsMortgaged(square), $"Seed {seed}: square {square} has houses and a mortgage.");
                Assert.IsTrue(GameRules.OwnsWholeGroup(data.Board, state, state.GetOwner(square), data.Board[square].GroupId),
                    $"Seed {seed}: houses on square {square} without the whole group.");
            }
        }
    }
}
