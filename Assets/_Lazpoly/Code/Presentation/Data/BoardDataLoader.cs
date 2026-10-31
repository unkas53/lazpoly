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
                file.maxConsecutiveDoubles, file.fullGroupRentMultiplier, file.mortgageInterestPercent, file.buildingSellbackPercent);

            var groupColors = new Dictionary<int, Color>();
            if (file.groups != null)
            {
                foreach (GroupEntry group in file.groups)
                {
                    if (!ColorUtility.TryParseHtmlString(group.color, out Color color))
                    {
                        throw new FormatException($"Group {group.id} has an invalid color '{group.color}'.");
                    }

                    groupColors[group.id] = color;
                }
            }

            var squares = new List<SquareDefinition>(file.squares.Length);
            for (int i = 0; i < file.squares.Length; i++)
            {
                squares.Add(ToSquare(file.squares[i], i, groupColors));
            }

            return new BoardData(new BoardDefinition(squares), rules, groupColors);
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
