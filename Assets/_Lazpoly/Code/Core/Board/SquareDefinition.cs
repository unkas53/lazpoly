using System;

namespace Lazpoly.Core.Board
{
    /// <summary>Immutable description of one board square. Loaded from data, never changed during a game.</summary>
    public sealed class SquareDefinition
    {
        private SquareDefinition(string name, SquareType type, int price, int baseRent, int groupId, int taxAmount)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Type = type;
            Price = price;
            BaseRent = baseRent;
            GroupId = groupId;
            TaxAmount = taxAmount;
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

        public static SquareDefinition Start(string name) => new SquareDefinition(name, SquareType.Start, 0, 0, 0, 0);

        public static SquareDefinition Rest(string name) => new SquareDefinition(name, SquareType.Rest, 0, 0, 0, 0);

        public static SquareDefinition Tax(string name, int amount) =>
            new SquareDefinition(name, SquareType.Tax, 0, 0, 0, amount);

        public static SquareDefinition Property(string name, int price, int baseRent, int groupId) =>
            new SquareDefinition(name, SquareType.Property, price, baseRent, groupId, 0);
    }
}
