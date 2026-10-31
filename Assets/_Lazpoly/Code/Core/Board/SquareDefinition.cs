using System;
using System.Collections.Generic;

namespace Lazpoly.Core.Board
{
    /// <summary>Immutable description of one board square. Loaded from data, never changed during a game.</summary>
    public sealed class SquareDefinition
    {
        /// <summary>Building levels a property can reach: four houses, then a hotel.</summary>
        public const int MaxBuildingLevel = 5;

        private static readonly int[] NoRents = new int[0];
        private readonly int[] _developedRents;

        private SquareDefinition(string name, SquareType type, int price, int baseRent, int groupId, int taxAmount,
            int buildingCost, int[] developedRents, int fee = 0)
        {
            Fee = fee;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Type = type;
            Price = price;
            BaseRent = baseRent;
            GroupId = groupId;
            TaxAmount = taxAmount;
            BuildingCost = buildingCost;
            _developedRents = developedRents ?? NoRents;
        }

        public string Name { get; }
        public SquareType Type { get; }

        /// <summary>Purchase price. Only meaningful for <see cref="SquareType.Property"/>.</summary>
        public int Price { get; }

        /// <summary>Rent before group bonuses. Only meaningful for <see cref="SquareType.Property"/>.</summary>
        public int BaseRent { get; }

        /// <summary>Colour/district group. 0 means the property belongs to no group.</summary>
        public int GroupId { get; }

        /// <summary>Amount paid to the bank. Only meaningful for <see cref="SquareType.Tax"/>.</summary>
        public int TaxAmount { get; }

        /// <summary>Price of a ride. Only meaningful for <see cref="SquareType.Travel"/>.</summary>
        public int Fee { get; }

        /// <summary>Price of one building level (house or hotel). 0 means the property cannot be developed.</summary>
        public int BuildingCost { get; }

        public bool IsDevelopable => BuildingCost > 0;

        /// <summary>Rent at building levels 1..<see cref="MaxBuildingLevel"/>; empty when not developable.</summary>
        public IReadOnlyList<int> DevelopedRents => _developedRents;

        /// <summary>What the bank lends against the property: half its price.</summary>
        public int MortgageValue => Price / 2;

        public static SquareDefinition Start(string name) => new SquareDefinition(name, SquareType.Start, 0, 0, 0, 0, 0, null);

        public static SquareDefinition Rest(string name) => new SquareDefinition(name, SquareType.Rest, 0, 0, 0, 0, 0, null);

        /// <summary>Draws a card from the board's deck.</summary>
        public static SquareDefinition Card(string name) => new SquareDefinition(name, SquareType.Card, 0, 0, 0, 0, 0, null);

        /// <summary>Pays out the pot that taxes and card fines fill.</summary>
        public static SquareDefinition Pot(string name) => new SquareDefinition(name, SquareType.Pot, 0, 0, 0, 0, 0, null);

        /// <summary>Offers a ride to any other square for <paramref name="fee"/>.</summary>
        public static SquareDefinition Travel(string name, int fee) =>
            new SquareDefinition(name, SquareType.Travel, 0, 0, 0, 0, 0, null, fee);

        /// <summary>Gives the next roll a choice of two.</summary>
        public static SquareDefinition Lookout(string name) => new SquareDefinition(name, SquareType.Lookout, 0, 0, 0, 0, 0, null);

        public static SquareDefinition Tax(string name, int amount) =>
            new SquareDefinition(name, SquareType.Tax, 0, 0, 0, amount, 0, null);

        /// <summary>A property without buildings.</summary>
        public static SquareDefinition Property(string name, int price, int baseRent, int groupId) =>
            new SquareDefinition(name, SquareType.Property, price, baseRent, groupId, 0, 0, null);

        /// <summary>A property that takes up to <see cref="MaxBuildingLevel"/> buildings once its whole group is owned.</summary>
        /// <param name="developedRents">Rent at levels 1..<see cref="MaxBuildingLevel"/>.</param>
        public static SquareDefinition Property(string name, int price, int baseRent, int groupId, int buildingCost,
            IReadOnlyList<int> developedRents)
        {
            if (developedRents == null) throw new ArgumentNullException(nameof(developedRents));
            var rents = new int[developedRents.Count];
            for (int i = 0; i < rents.Length; i++) rents[i] = developedRents[i];
            return new SquareDefinition(name, SquareType.Property, price, baseRent, groupId, 0, buildingCost, rents);
        }
    }
}
