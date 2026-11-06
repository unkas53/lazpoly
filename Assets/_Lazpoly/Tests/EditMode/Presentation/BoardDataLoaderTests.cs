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

            Assert.AreEqual(28, data.Board.Count);
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
        public void EveryCityOnTheShippedBoardIsADifferentOne()
        {
            BoardData data = BoardDataLoader.LoadFromResources();
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < data.Board.Count; i++)
            {
                if (data.Board[i].Type != SquareType.Property) continue;
                Assert.IsTrue(seen.Add(data.Board[i].Name), $"{data.Board[i].Name} is on the board twice.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(data.GroupName(data.Board[i].GroupId)), $"{data.Board[i].Name} has no region.");
            }
            Assert.AreEqual("İstanbul", data.Board[data.Board.Count - 2].Name, "the dearest city closes the lap");
        }

        [Test]
        public void ShippedBoardHasTheSpecialSquaresAndADeck()
        {
            BoardData data = BoardDataLoader.LoadFromResources();
            foreach (SquareType type in new[] { SquareType.Card, SquareType.Pot, SquareType.Travel, SquareType.Lookout })
            {
                bool found = false;
                for (int i = 0; i < data.Board.Count; i++) found |= data.Board[i].Type == type;
                Assert.IsTrue(found, $"The board has no {type} square.");
            }

            Assert.IsTrue(data.Board.HasPot);
            Assert.That(data.Board.Cards.Count, Is.GreaterThanOrEqualTo(20));
            foreach (CardDefinition card in data.Board.Cards) Assert.IsFalse(string.IsNullOrWhiteSpace(card.Text));
        }

        [Test]
        public void ParsesSpecialSquaresAndCards()
        {
            const string json = @"{
                ""startingMoney"": 1000, ""passStartBonus"": 100, ""diceSides"": 6, ""maxConsecutiveDoubles"": 3,
                ""fullGroupRentMultiplier"": 2, ""buildingSellbackPercent"": 50,
                ""squares"": [
                    { ""type"": ""start"", ""name"": ""S"" },
                    { ""type"": ""card"", ""name"": ""Haber"" },
                    { ""type"": ""pot"", ""name"": ""Kumbara"" },
                    { ""type"": ""travel"", ""name"": ""Otogar"", ""fee"": 40 },
                    { ""type"": ""lookout"", ""name"": ""Teras"" }
                ],
                ""cards"": [
                    { ""kind"": ""collect"", ""amount"": 10, ""text"": ""Al"" },
                    { ""kind"": ""moveTo"", ""to"": ""Otogar"", ""text"": ""Git"" },
                    { ""kind"": ""repairs"", ""amount"": 5, ""hotel"": 20, ""text"": ""Tamir"" }
                ]
            }";

            BoardData data = BoardDataLoader.Parse(json);
            Assert.AreEqual(40, data.Board[3].Fee);
            Assert.AreEqual(SquareType.Lookout, data.Board[4].Type);
            Assert.AreEqual(3, data.Board.Cards.Count);
            Assert.AreEqual(CardKind.MoveTo, data.Board.Cards[1].Kind);
            Assert.AreEqual(3, data.Board.Cards[1].Target);
            Assert.AreEqual(20, data.Board.Cards[2].HotelAmount);

            Assert.Throws<FormatException>(() => BoardDataLoader.Parse(json.Replace(@"""to"": ""Otogar""", @"""to"": ""Yok""")));
            Assert.Throws<FormatException>(() => BoardDataLoader.Parse(json.Replace(@"""kind"": ""collect""", @"""kind"": ""magic""")));
        }

        [Test]
        public void ShippedBoardIsFullyDevelopable()
        {
            BoardData data = BoardDataLoader.LoadFromResources();

            Assert.AreEqual(50, data.Rules.BuildingSellbackPercent);
            for (int i = 0; i < data.Board.Count; i++)
            {
                SquareDefinition square = data.Board[i];
                if (square.Type != SquareType.Property || square.IsResort) continue;
                Assert.IsTrue(square.IsDevelopable, $"Square {i} '{square.Name}' takes no buildings.");
                Assert.AreEqual(SquareDefinition.MaxBuildingLevel, square.DevelopedRents.Count);
            }
        }

        [Test]
        public void TheShippedBoardHasOneResortOnEachSide()
        {
            BoardData data = BoardDataLoader.LoadFromResources();
            int side = data.Board.Count / 4;
            var sides = new bool[4];
            int resorts = 0;
            for (int i = 0; i < data.Board.Count; i++)
            {
                SquareDefinition square = data.Board[i];
                if (!square.IsResort) continue;
                resorts++;
                Assert.AreNotEqual(0, i % side, $"{square.Name} is not on a corner");
                Assert.IsFalse(sides[i / side], $"{square.Name} shares its side with another resort");
                sides[i / side] = true;
                Assert.IsFalse(square.IsDevelopable, $"{square.Name} takes no buildings");
                Assert.AreEqual(50, square.BaseRent);
                Assert.AreEqual(200, square.Price);
                Assert.IsFalse(string.IsNullOrWhiteSpace(data.GroupName(square.GroupId)));
            }

            Assert.AreEqual(4, resorts);
        }

        [Test]
        public void ReadsAResortAndRefusesOneWithBuildings()
        {
            const string json = @"{
                ""startingMoney"": 500, ""passStartBonus"": 50, ""diceSides"": 6,
                ""maxConsecutiveDoubles"": 3, ""fullGroupRentMultiplier"": 2,
                ""groups"": [ { ""id"": 9, ""name"": ""Tatil"", ""color"": ""#26A69A"" } ],
                ""squares"": [
                    { ""type"": ""start"", ""name"": ""S"" },
                    { ""type"": ""resort"", ""name"": ""Bodrum"", ""price"": 200, ""rent"": 50, ""group"": 9 },
                    { ""type"": ""resort"", ""name"": ""Bali"", ""price"": 200, ""rent"": 50, ""group"": 9 }
                ]
            }";

            BoardData data = BoardDataLoader.Parse(json);
            Assert.AreEqual(SquareType.Property, data.Board[1].Type);
            Assert.IsTrue(data.Board[1].IsResort);
            Assert.IsFalse(data.Board[1].IsDevelopable);
            Assert.AreEqual(200, data.Board[2].Price);
            Assert.AreEqual(50, data.Board[2].BaseRent);

            Assert.Throws<FormatException>(() => BoardDataLoader.Parse(json.Replace(@"""rent"": 50, ""group"": 9 },", @"""rent"": 50, ""group"": 9, ""buildingCost"": 50 },")));
            Assert.Throws<FormatException>(() => BoardDataLoader.Parse(json.Replace(@"""group"": 9 }", @"""group"": 7 }")));
        }

        [Test]
        public void ReadsBuildingCostsAndRents()
        {
            const string json = @"{
                ""startingMoney"": 500, ""passStartBonus"": 50, ""diceSides"": 6,
                ""maxConsecutiveDoubles"": 3, ""fullGroupRentMultiplier"": 2,
                ""buildingSellbackPercent"": 40,
                ""groups"": [ { ""id"": 1, ""name"": ""G"", ""color"": ""#FF0000"" } ],
                ""squares"": [
                    { ""type"": ""start"", ""name"": ""S"" },
                    { ""type"": ""property"", ""name"": ""P"", ""price"": 60, ""rent"": 5, ""group"": 1,
                      ""buildingCost"": 50, ""rents"": [25, 75, 200, 275, 350] },
                    { ""type"": ""property"", ""name"": ""Q"", ""price"": 60, ""rent"": 5, ""group"": 1 }
                ]
            }";

            BoardData data = BoardDataLoader.Parse(json);

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
