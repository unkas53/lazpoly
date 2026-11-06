using System;
using System.Linq;
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

            Assert.AreEqual(32, data.Board.Count);
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
            foreach (SquareType type in new[] { SquareType.Card, SquareType.Jail, SquareType.Travel, SquareType.Lookout })
            {
                bool found = false;
                for (int i = 0; i < data.Board.Count; i++) found |= data.Board[i].Type == type;
                Assert.IsTrue(found, $"The board has no {type} square.");
            }

            Assert.IsFalse(data.Board.HasPot, "the tea break gave way to the jail; taxes and fines go to the bank");
            Assert.AreEqual(8, data.Board.JailIndex, "the jail stands on the first corner after the start");
            Assert.AreEqual(50, data.Board[data.Board.JailIndex].Fee);
            Assert.AreEqual(3, data.Rules.JailTurns);
            Assert.AreEqual(1, data.Board.Cards.Count(card => card.Kind == CardKind.GoToJail), "the card that led to the tea break now leads to jail");
            Assert.That(data.Board.Cards.Count, Is.GreaterThanOrEqualTo(20));
            foreach (CardDefinition card in data.Board.Cards) Assert.IsFalse(string.IsNullOrWhiteSpace(card.Text));
        }

        [Test]
        public void ShippedBoardHasThreeChanceSquaresAndThreeSouthAmericanCities()
        {
            BoardData data = BoardDataLoader.LoadFromResources();
            BoardDefinition board = data.Board;
            var chances = new System.Collections.Generic.List<int>();
            for (int i = 0; i < board.Count; i++)
            {
                if (board[i].Name == "Şans") chances.Add(i);
            }

            Assert.AreEqual(3, chances.Count, "three Şans squares (the owner's wish, Faz 24)");
            foreach (int i in chances) Assert.AreEqual(SquareType.Card, board[i].Type, "a Şans square draws a card");
            string Around(int i) => board[i - 1].Name + "|" + board[i + 1].Name;
            CollectionAssert.AreEquivalent(new[] { "Prag|Viyana", "Barselona|Roma", "Otogar|Paris" }, chances.Select(Around).ToArray());

            int rio = Enumerable.Range(0, board.Count).Single(i => board[i].Name == "Rio de Janeiro");
            Assert.AreEqual(3, board.CountInGroup(board[rio].GroupId), "South America has a third city");
            Assert.AreEqual("Lima", board[rio + 2].Name, "next to Rio de Janeiro and Buenos Aires");
            Assert.AreEqual(board[rio].GroupId, board[rio + 2].GroupId);

            // North Africa has a third city where the property tax was (the owner's wish, Faz 24); one tax is left.
            CollectionAssert.AreEqual(new[] { "Kahire", "Marakeş", "Tunus" }, new[] { 1, 2, 3 }.Select(i => board[i].Name).ToArray());
            Assert.AreEqual(3, board.CountInGroup(board[1].GroupId));
            Assert.AreEqual(0, Enumerable.Range(0, board.Count).Count(i => board[i].Type == SquareType.Tax), "both taxes gave way to cities");

            // East Asia too: Santorini moved up a square and Pekin took the luxury tax's place, next to Seul and Tokyo.
            int seoul = Enumerable.Range(0, board.Count).Single(i => board[i].Name == "Seul");
            CollectionAssert.AreEqual(new[] { "Roma", "Santorini", "Pekin", "Seul", "Tokyo" },
                Enumerable.Range(seoul - 3, 5).Select(i => board[i].Name).ToArray());
            Assert.AreEqual(3, board.CountInGroup(board[seoul].GroupId));
            Assert.AreEqual(board[seoul].GroupId, board[seoul - 1].GroupId);

            // Eight squares a side: the corners stay where the board's corners are.
            CollectionAssert.AreEqual(new[] { "Başlangıç", "Hapis", "Sokak Haberi", "Otogar" },
                new[] { 0, 8, 16, 24 }.Select(corner => board[corner].Name).ToArray());
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

            BoardData jailed = BoardDataLoader.Parse(json.Replace(@"""type"": ""pot"", ""name"": ""Kumbara""", @"""type"": ""jail"", ""name"": ""Hapis"", ""fee"": 30")
                .Replace(@"""diceSides"": 6,", @"""diceSides"": 6, ""jailTurns"": 2,")
                .Replace(@"""kind"": ""repairs"", ""amount"": 5, ""hotel"": 20, ""text"": ""Tamir""", @"""kind"": ""goToJail"", ""text"": ""Hapse"""));
            Assert.AreEqual(CardKind.GoToJail, jailed.Board.Cards[2].Kind);
            Assert.Throws<ArgumentException>(() => BoardDataLoader.Parse(json.Replace(@"""kind"": ""repairs"", ""amount"": 5, ""hotel"": 20, ""text"": ""Tamir""", @"""kind"": ""goToJail"", ""text"": ""Hapse""")),
                "a jail card on a board without a jail");
            Assert.AreEqual(SquareType.Jail, jailed.Board[2].Type);
            Assert.AreEqual(2, jailed.Board.JailIndex);
            Assert.AreEqual(30, jailed.Board[2].Fee);
            Assert.AreEqual(2, jailed.Rules.JailTurns);
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
                ""squares"": [ { ""type"": ""start"", ""name"": ""S"" }, { ""type"": ""prison"", ""name"": ""J"" } ] }";

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
