using System;
using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Rules;
using UnityEngine;

namespace Lazpoly.Presentation.Data
{
    public static class BoardDataLoader
    {
        public const string DefaultResourcePath = "Lazpoly/board";

        public static BoardData LoadFromResources(string resourcePath = DefaultResourcePath)
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                throw new InvalidOperationException($"Board data not found at Resources/{resourcePath}.json.");
            }

            return Parse(asset.text);
        }

        /// <summary>Builds validated Core types from JSON. Throws with a readable message on bad data.</summary>
        public static BoardData Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Board JSON is empty.", nameof(json));

            BoardFile file = JsonUtility.FromJson<BoardFile>(json);
            if (file?.squares == null || file.squares.Length == 0)
            {
                throw new FormatException("Board JSON has no squares.");
            }

            var rules = new RulesConfig(file.startingMoney, file.passStartBonus, file.diceSides,
                file.maxConsecutiveDoubles, file.fullGroupRentMultiplier, file.buildingSellbackPercent);

            var groupColors = new Dictionary<int, Color>();
            var groupNames = new Dictionary<int, string>();
            if (file.groups != null)
            {
                foreach (GroupEntry group in file.groups)
                {
                    if (!ColorUtility.TryParseHtmlString(group.color, out Color color))
                    {
                        throw new FormatException($"Group {group.id} has an invalid color '{group.color}'.");
                    }

                    groupColors[group.id] = color;
                    if (!string.IsNullOrWhiteSpace(group.name)) groupNames[group.id] = group.name;
                }
            }

            var squares = new List<SquareDefinition>(file.squares.Length);
            for (int i = 0; i < file.squares.Length; i++)
            {
                squares.Add(ToSquare(file.squares[i], i, groupColors));
            }

            var cards = new List<CardDefinition>();
            if (file.cards != null)
            {
                for (int i = 0; i < file.cards.Length; i++) cards.Add(ToCard(file.cards[i], i, squares));
            }

            return new BoardData(new BoardDefinition(squares, cards), rules, groupColors, groupNames);
        }

        private static CardDefinition ToCard(CardEntry entry, int index, List<SquareDefinition> squares)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.text)) throw new FormatException($"Card {index} has no text.");
            switch (entry.kind)
            {
                case "collect": return CardDefinition.Collect(entry.text, entry.amount);
                case "pay": return CardDefinition.Pay(entry.text, entry.amount);
                case "moveBy": return CardDefinition.MoveBy(entry.text, entry.amount);
                case "repairs": return CardDefinition.Repairs(entry.text, entry.amount, entry.hotel);
                case "lookout": return CardDefinition.Lookout(entry.text);
                case "moveTo":
                    int target = squares.FindIndex(square => square.Name == entry.to);
                    if (target < 0) throw new FormatException($"Card {index} '{entry.text}' goes to unknown square '{entry.to}'.");
                    return CardDefinition.MoveTo(entry.text, target);
                default:
                    throw new FormatException($"Card {index} '{entry.text}' has unknown kind '{entry.kind}'.");
            }
        }

        private static SquareDefinition ToSquare(SquareEntry entry, int index, Dictionary<int, Color> groups)
        {
            switch (entry.type)
            {
                case "start":
                    return SquareDefinition.Start(entry.name);
                case "rest":
                    return SquareDefinition.Rest(entry.name);
                case "tax":
                    return SquareDefinition.Tax(entry.name, entry.amount);
                case "card":
                    return SquareDefinition.Card(entry.name);
                case "pot":
                    return SquareDefinition.Pot(entry.name);
                case "travel":
                    return SquareDefinition.Travel(entry.name, entry.fee);
                case "lookout":
                    return SquareDefinition.Lookout(entry.name);
                case "property":
                    if (entry.group != 0 && !groups.ContainsKey(entry.group))
                    {
                        throw new FormatException($"Square {index} '{entry.name}' uses undefined group {entry.group}.");
                    }

                    bool developable = entry.buildingCost > 0;
                    bool hasRents = entry.rents != null && entry.rents.Length > 0;
                    if (developable != hasRents)
                    {
                        throw new FormatException($"Square {index} '{entry.name}' needs both buildingCost and rents, or neither.");
                    }

                    return developable
                        ? SquareDefinition.Property(entry.name, entry.price, entry.rent, entry.group, entry.buildingCost, entry.rents)
                        : SquareDefinition.Property(entry.name, entry.price, entry.rent, entry.group);
                default:
                    throw new FormatException($"Square {index} '{entry.name}' has unknown type '{entry.type}'.");
            }
        }
    }
}
