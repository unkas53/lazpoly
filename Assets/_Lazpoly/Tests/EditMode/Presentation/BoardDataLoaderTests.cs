using System;
using Lazpoly.Core.Board;
using Lazpoly.Presentation.Data;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    public class BoardDataLoaderTests
    {
        [Test]
        public void ShippedBoardLoadsAndIsValid()
        {
            BoardData data = BoardDataLoader.LoadFromResources();

            Assert.AreEqual(24, data.Board.Count);
            Assert.AreEqual(SquareType.Start, data.Board[0].Type);
            Assert.That(data.Rules.StartingMoney, Is.GreaterThan(0));

            for (int i = 0; i < data.Board.Count; i++)
            {
                SquareDefinition square = data.Board[i];
                Assert.IsFalse(string.IsNullOrWhiteSpace(square.Name), $"Square {i} has no name.");
                if (square.Type == SquareType.Property && square.GroupId != 0)
                {
                    Assert.That(data.Board.CountInGroup(square.GroupId), Is.GreaterThanOrEqualTo(2),
                        $"Group {square.GroupId} has a single property.");
                }
            }
        }

        [Test]
        public void ShippedBoardIsFullyDevelopable()
        {
            BoardData data = BoardDataLoader.LoadFromResources();

            Assert.AreEqual(10, data.Rules.MortgageInterestPercent);
            Assert.AreEqual(50, data.Rules.BuildingSellbackPercent);
            for (int i = 0; i < data.Board.Count; i++)
            {
                SquareDefinition square = data.Board[i];
                if (square.Type != SquareType.Property) continue;
                Assert.IsTrue(square.IsDevelopable, $"Square {i} '{square.Name}' takes no buildings.");
                Assert.AreEqual(SquareDefinition.MaxBuildingLevel, square.DevelopedRents.Count);
            }
        }

        [Test]
        public void ReadsBuildingCostsAndRents()
        {
            const string json = @"{
                ""startingMoney"": 500, ""passStartBonus"": 50, ""diceSides"": 6,
                ""maxConsecutiveDoubles"": 3, ""fullGroupRentMultiplier"": 2,
                ""mortgageInterestPercent"": 15, ""buildingSellbackPercent"": 40,
                ""groups"": [ { ""id"": 1, ""name"": ""G"", ""color"": ""#FF0000"" } ],
                ""squares"": [
                    { ""type"": ""start"", ""name"": ""S"" },
                    { ""type"": ""property"", ""name"": ""P"", ""price"": 60, ""rent"": 5, ""group"": 1,
                      ""buildingCost"": 50, ""rents"": [25, 75, 200, 275, 350] },
                    { ""type"": ""property"", ""name"": ""Q"", ""price"": 60, ""rent"": 5, ""group"": 1 }
                ]
            }";

            BoardData data = BoardDataLoader.Parse(json);

            Assert.AreEqual(15, data.Rules.MortgageInterestPercent);
            Assert.AreEqual(40, data.Rules.BuildingSellbackPercent);
            Assert.AreEqual(50, data.Board[1].BuildingCost);
            CollectionAssert.AreEqual(new[] { 25, 75, 200, 275, 350 }, data.Board[1].DevelopedRents);
            Assert.IsFalse(data.Board[2].IsDevelopable);
        }

        [Test]
        public void RejectsBuildingCostWithoutRents()
        {
            const string json = @"{
                ""startingMoney"": 500, ""passStartBonus"": 50, ""diceSides"": 6,
                ""maxConsecutiveDoubles"": 3, ""fullGroupRentMultiplier"": 2,
                ""groups"": [ { ""id"": 1, ""name"": ""G"", ""color"": ""#FF0000"" } ],
                ""squares"": [
                    { ""type"": ""start"", ""name"": ""S"" },
                    { ""type"": ""property"", ""name"": ""P"", ""price"": 60, ""rent"": 5, ""group"": 1, ""buildingCost"": 50 }
                ]
            }";

            Assert.Throws<FormatException>(() => BoardDataLoader.Parse(json));
        }

        [Test]
        public void ParsesAMinimalBoard()
        {
            const string json = @"{
                ""startingMoney"": 500, ""passStartBonus"": 50, ""diceSides"": 6,
                ""maxConsecutiveDoubles"": 3, ""fullGroupRentMultiplier"": 2,
                ""groups"": [ { ""id"": 1, ""name"": ""G"", ""color"": ""#FF0000"" } ],
                ""squares"": [
                    { ""type"": ""start"", ""name"": ""S"" },
                    { ""type"": ""property"", ""name"": ""P"", ""price"": 60, ""rent"": 5, ""group"": 1 },
                    { ""type"": ""tax"", ""name"": ""T"", ""amount"": 20 },
                    { ""type"": ""rest"", ""name"": ""R"" }
                ]
            }";

            BoardData data = BoardDataLoader.Parse(json);

            Assert.AreEqual(4, data.Board.Count);
            Assert.AreEqual(60, data.Board[1].Price);
            Assert.AreEqual(5, data.Board[1].BaseRent);
            Assert.AreEqual(20, data.Board[2].TaxAmount);
            Assert.AreEqual(500, data.Rules.StartingMoney);
            Assert.AreEqual(Color.red, data.GroupColor(1));
        }

        [Test]
        public void RejectsUnknownSquareType()
        {
            const string json = @"{ ""startingMoney"": 500, ""passStartBonus"": 50, ""diceSides"": 6,
                ""maxConsecutiveDoubles"": 3, ""fullGroupRentMultiplier"": 2,
                ""squares"": [ { ""type"": ""start"", ""name"": ""S"" }, { ""type"": ""jail"", ""name"": ""J"" } ] }";

            Assert.Throws<FormatException>(() => BoardDataLoader.Parse(json));
        }

        [Test]
        public void RejectsUndefinedGroup()
        {
            const string json = @"{ ""startingMoney"": 500, ""passStartBonus"": 50, ""diceSides"": 6,
                ""maxConsecutiveDoubles"": 3, ""fullGroupRentMultiplier"": 2,
                ""squares"": [ { ""type"": ""start"", ""name"": ""S"" },
                               { ""type"": ""property"", ""name"": ""P"", ""price"": 60, ""rent"": 5, ""group"": 9 } ] }";

            Assert.Throws<FormatException>(() => BoardDataLoader.Parse(json));
        }

        [Test]
        public void RejectsMissingRules()
        {
            const string json = @"{ ""squares"": [ { ""type"": ""start"", ""name"": ""S"" }, { ""type"": ""rest"", ""name"": ""R"" } ] }";

            Assert.Throws<ArgumentOutOfRangeException>(() => BoardDataLoader.Parse(json));
        }
    }
}
