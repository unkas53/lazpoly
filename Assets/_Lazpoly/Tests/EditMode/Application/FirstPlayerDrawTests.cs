using System.Collections.Generic;
using Lazpoly.App.AI;
using Lazpoly.App.Session;
using Lazpoly.Core.Board;
using Lazpoly.Core.Random;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;
using NUnit.Framework;

namespace Lazpoly.App.Tests
{
    public class FirstPlayerDrawTests
    {
        private static readonly BoardDefinition Board = new BoardDefinition(new[]
        {
            SquareDefinition.Start("Start"),
            SquareDefinition.Property("A1", 60, 6, 1, 50, new[] { 30, 90, 240, 330, 420 }),
            SquareDefinition.Property("A2", 80, 8, 1, 50, new[] { 40, 120, 320, 440, 560 }),
            SquareDefinition.Rest("Rest"),
            SquareDefinition.Property("B1", 120, 12, 2, 100, new[] { 60, 180, 480, 660, 840 }),
            SquareDefinition.Property("B2", 140, 14, 2, 100, new[] { 70, 210, 560, 770, 980 }),
        });

        private static readonly RulesConfig Rules = new RulesConfig(5000, 100, 6, 3, 2, 50);

        private static PlayerSeat[] Seats(int count)
        {
            var seats = new PlayerSeat[count];
            for (int i = 0; i < count; i++) seats[i] = new PlayerSeat(i + 1, $"P{i + 1}");
            return seats;
        }

        private static int[] Ids(IReadOnlyList<PlayerSeat> seats)
        {
            var ids = new int[seats.Count];
            for (int i = 0; i < seats.Count; i++) ids[i] = seats[i].Id;
            return ids;
        }

        /// <summary>Always gives the same number, so a test can pick the drawn seat.</summary>
        private sealed class FixedRandom : IRandomSource
        {
            private readonly int _value;
            public FixedRandom(int value) { _value = value; }
            public int Calls { get; private set; }
            public uint NextUInt() => (uint)_value;

            public int NextInt(int minInclusive, int maxExclusive)
            {
                Calls++;
                Assert.AreEqual(0, minInclusive);
                Assert.Greater(maxExclusive, _value);
                return _value;
            }
        }

        [Test]
        public void TheDrawnSeatStartsAndTheOthersKeepTheirOrderRoundTheTable()
        {
            var random = new FixedRandom(2);
            IReadOnlyList<PlayerSeat> seats = FirstPlayerDraw.Draw(Seats(4), random);
            CollectionAssert.AreEqual(new[] { 3, 4, 1, 2 }, Ids(seats));
            Assert.AreEqual(1, random.Calls, "One draw among all the seats.");

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, Ids(FirstPlayerDraw.StartWith(Seats(3), 0)), "Drawing the first seat changes nothing.");
            CollectionAssert.AreEqual(new[] { 2, 1 }, Ids(FirstPlayerDraw.StartWith(Seats(2), 1)));
        }

        [Test]
        public void EverySeatCanBeDrawn()
        {
            var starters = new HashSet<int>();
            var random = new Pcg32Random(12345UL);
            for (int i = 0; i < 200; i++) starters.Add(FirstPlayerDraw.Draw(Seats(4), random)[0].Id);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4 }, starters);
        }

        [Test]
        public void ColoursFollowTheLobbyNotTheTurnOrder()
        {
            GameState state = GameState.CreateInitial(Board, Rules, FirstPlayerDraw.StartWith(Seats(3), 1));
            Assert.AreEqual(2, state.CurrentPlayer.Id, "The drawn player rolls first.");
            Assert.AreEqual(0, FirstPlayerDraw.LobbySeat(state.Players, 1));
            Assert.AreEqual(1, FirstPlayerDraw.LobbySeat(state.Players, 2));
            Assert.AreEqual(2, FirstPlayerDraw.LobbySeat(state.Players, 3));
        }

        [Test]
        public void ARoundEndsWhenTheTurnComesBackToWhoeverStarted()
        {
            var ai = new Dictionary<int, AiDifficulty> { { 1, AiDifficulty.Easy }, { 2, AiDifficulty.Easy }, { 3, AiDifficulty.Easy } };
            LocalGame game = LocalGame.Create(Board, Rules, FirstPlayerDraw.StartWith(Seats(3), 2), ai, 21UL);
            var turns = new List<(int Player, int Round)>();
            GameState state = game.Session.State;
            turns.Add((state.CurrentPlayer.Id, state.Round));
            for (int step = 0; step < 500 && turns.Count < 5; step++)
            {
                Assert.IsTrue(game.Session.StepAi());
                state = game.Session.State;
                (int Player, int Round) last = turns[turns.Count - 1];
                if (state.CurrentPlayer.Id != last.Player) turns.Add((state.CurrentPlayer.Id, state.Round));
            }

            CollectionAssert.AreEqual(new[] { (3, 1), (1, 1), (2, 1), (3, 2), (1, 2) }, turns);
        }
    }
}
