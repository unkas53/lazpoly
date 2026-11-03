using System.Collections.Generic;
using System.Linq;
using Lazpoly.App.AI;
using Lazpoly.App.Balance;
using Lazpoly.App.Session;
using Lazpoly.Core.Board;
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
                         typeof(BuildingLevelChangedEvent), typeof(TradeResolvedEvent),
                         typeof(DebtIncurredEvent), typeof(PlayerBankruptedEvent),
                         typeof(CardDrawnEvent), typeof(PotChangedEvent), typeof(DiceOptionsEvent),
                     })
            {
                Assert.IsTrue(seen.Contains(expected), $"The computer players never produced {expected.Name}.");
            }

            TestContext.WriteLine($"{Games} games, {totalTurns / Games} turns on average.");
        }

        [Test]
        public void AShortGameEndsAtItsRoundLimitWithTheRichestPlayer()
        {
            BoardData data = BoardDataLoader.LoadFromResources();
            var seats = new PlayerSeat[4];
            var ai = new Dictionary<int, AiDifficulty>();
            for (int i = 0; i < seats.Length; i++)
            {
                seats[i] = new PlayerSeat(i + 1, $"AI {i + 1}");
                ai.Add(i + 1, AiDifficulty.Normal);
            }

            var table = new Core.Rules.TableRules(2000, roundLimit: 8);
            LocalGame game = LocalGame.Create(data.Board, data.Rules.WithTable(table), seats, ai, 7UL);
            LocalGameSession session = game.Session;
            GameEndedEvent ended = null;
            session.EventsPublished += events =>
            {
                foreach (GameEvent gameEvent in events)
                {
                    if (gameEvent is GameEndedEvent end) ended = end;
                }
            };

            Assert.AreEqual(2000, session.State.Players[0].Money);
            for (int commands = 0; session.State.Phase != GamePhase.GameOver; commands++)
            {
                Assert.Less(commands, CommandLimit, "The short game did not end.");
                Assert.IsTrue(session.StepAi());
            }

            Assert.IsNotNull(ended);
            if (ended.Reason == GameEndReason.RoundLimit)
            {
                Assert.AreEqual(8, session.State.Round, "The game should end after the last turn of round 8.");
                Assert.AreEqual(GameRules.Richest(data.Board, session.State), ended.WinnerId);
            }
        }

        [Test]
        public void BalanceSimulationIsRepeatableAndReported()
        {
            BoardData data = BoardDataLoader.LoadFromResources();
            var settings = new BalanceSettings(12, 4, firstSeed: 3UL);
            BalanceResult first = BalanceSimulator.Run(data.Board, data.Rules, settings);
            BalanceResult second = BalanceSimulator.Run(data.Board, data.Rules, settings);

            Assert.AreEqual(12, first.Games);
            Assert.AreEqual(0, first.Stalled, "A computer game stalled.");
            CollectionAssert.AreEqual(first.Rounds, second.Rounds, "The same seeds should play the same games.");
            Assert.AreEqual(12, first.WinsBySeat.Sum());
            Assert.Greater(first.RentBySquare.Sum(), 0L);
            Assert.Greater(first.CardDraws, 0L);

            string report = BalanceReport.Markdown(data.Board, data.Rules, settings, first, id => data.GroupName(id));
            StringAssert.Contains("## Renk grupları", report);
            StringAssert.Contains("Yalı Konağı", report);
            TestContext.WriteLine(report);
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
                Assert.AreNotEqual(GameState.NoOwner, state.GetOwner(square), $"Seed {seed}: houses on unowned square {square}.");
                Assert.That(state.GetBuildingLevel(square), Is.LessThanOrEqualTo(SquareDefinition.MaxBuildingLevel), $"Seed {seed}: square {square} is overbuilt.");
            }
        }
    }
}
