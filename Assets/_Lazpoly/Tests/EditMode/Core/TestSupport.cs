using System;
using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Random;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;
using NUnit.Framework;

namespace Lazpoly.Core.Tests
{
    /// <summary>Dice that return a fixed script of faces, so a test controls every roll.</summary>
    internal sealed class ScriptedDice : IRandomSource
    {
        private readonly Queue<int> _faces;

        public ScriptedDice(params int[] faces)
        {
            _faces = new Queue<int>(faces);
        }

        public uint NextUInt() => throw new NotSupportedException("Scripted dice only serve NextInt.");

        public int NextInt(int minInclusive, int maxExclusive)
        {
            Assert.IsTrue(_faces.Count > 0, "The test rolled more dice than it scripted.");
            int face = _faces.Dequeue();
            Assert.That(face, Is.InRange(minInclusive, maxExclusive - 1), "Scripted face is outside the die.");
            return face;
        }
    }

    internal static class TestData
    {
        public const int Alice = 10;
        public const int Bob = 20;
        public const int Cem = 30;

        /// <summary>
        /// 12-square test board:
        /// 0 Start, 1-2 group 1, 3 Tax 100, 4 Rest, 5-6 group 2, 7 ungrouped, 8 Rest, 9-10 group 3, 11 Tax 50.
        /// </summary>
        public static BoardDefinition TestBoard()
        {
            return new BoardDefinition(new[]
            {
                SquareDefinition.Start("Start"),
                SquareDefinition.Property("A1", 60, 10, 1),
                SquareDefinition.Property("A2", 80, 12, 1),
                SquareDefinition.Tax("Tax", 100),
                SquareDefinition.Rest("Rest 1"),
                SquareDefinition.Property("B1", 120, 20, 2),
                SquareDefinition.Property("B2", 140, 24, 2),
                SquareDefinition.Property("Solo", 150, 30, 0),
                SquareDefinition.Rest("Rest 2"),
                SquareDefinition.Property("C1", 200, 40, 3),
                SquareDefinition.Property("C2", 220, 44, 3),
                SquareDefinition.Tax("Small tax", 50),
            });
        }

        /// <summary>
        /// Same layout as <see cref="TestBoard"/>, but groups 1-3 take buildings
        /// (cost 50, 100, 150; rents rising to a hotel).
        /// </summary>
        public static BoardDefinition DevelopableBoard()
        {
            SquareDefinition Dev(string name, int price, int rent, int group, int cost) =>
                SquareDefinition.Property(name, price, rent, group, cost,
                    new[] { rent * 5, rent * 15, rent * 40, rent * 55, rent * 70 });

            return new BoardDefinition(new[]
            {
                SquareDefinition.Start("Start"),
                Dev("A1", 60, 10, 1, 50),
                Dev("A2", 80, 12, 1, 50),
                SquareDefinition.Tax("Tax", 100),
                SquareDefinition.Rest("Rest 1"),
                Dev("B1", 120, 20, 2, 100),
                Dev("B2", 140, 24, 2, 100),
                SquareDefinition.Property("Solo", 150, 30, 0),
                SquareDefinition.Rest("Rest 2"),
                Dev("C1", 200, 40, 3, 150),
                Dev("C2", 220, 44, 3, 150),
                SquareDefinition.Tax("Small tax", 50),
            });
        }

        public static RulesConfig TestRules(int startingMoney = 1000, int passStartBonus = 200) =>
            new RulesConfig(startingMoney, passStartBonus, diceSides: 6, maxConsecutiveDoubles: 3,
                fullGroupRentMultiplier: 2, buildingSellbackPercent: 50);

        public static PlayerSeat[] TestSeats(int count = 2)
        {
            var all = new[] { new PlayerSeat(Alice, "Alice"), new PlayerSeat(Bob, "Bob"), new PlayerSeat(Cem, "Cem") };
            var seats = new PlayerSeat[count];
            Array.Copy(all, seats, count);
            return seats;
        }
    }
}
