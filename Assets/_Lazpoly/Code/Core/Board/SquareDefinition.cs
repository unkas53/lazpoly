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
            int buildingCost, int[] developedRents, int fee = 0, bool isResort = false, int taxPercent = 0, int boostPercent = 0, int boostStep = 0)
        {
            Fee = fee;
            BoostPercent = boostPercent;
            BoostStep = boostStep;
            TaxPercent = taxPercent;
            IsResort = isResort;
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

        /// <summary>Amount paid to the bank. Only meaningful for <see cref="SquareType.Tax"/>; 0 on a tax taken as a share (<see cref="TaxPercent"/>).</summary>
        public int TaxAmount { get; }

        /// <summary>
        /// On a tax square without a fixed <see cref="TaxAmount"/>: the share, in percent, of the visitor's property value
        /// that the tax takes (GameRules.TaxDue; Faz 24). 0 otherwise.
        /// </summary>
        public int TaxPercent { get; }

        /// <summary>
        /// On a cup square: how much, in percent, the first town to get the cup raises its rent (GameRules.CupPercent);
        /// every pick after it in the game adds <see cref="BoostStep"/> more. 0 on other squares.
        /// </summary>
        public int BoostPercent { get; }

        /// <summary>On a cup square: the percent each pick adds to the next one's raise. 0 on other squares.</summary>
        public int BoostStep { get; }

        /// <summary>
        /// Price of a ride (<see cref="SquareType.Travel"/>), of leaving jail early (<see cref="SquareType.Jail"/>), or of bringing the
        /// cup to a town (<see cref="SquareType.Cup"/>).
        /// </summary>
        public int Fee { get; }

        /// <summary>Price of one building level (house or hotel). 0 means the property cannot be developed.</summary>
        public int BuildingCost { get; }

        public bool IsDevelopable => BuildingCost > 0;

        /// <summary>
        /// A holiday resort ("tatil yeri"): a property that takes no buildings, whose rent is <see cref="BaseRent"/>
        /// times the number of resorts its owner holds (one 50, two 100 each, three 150 each, four 200 each).
        /// </summary>
        public bool IsResort { get; }

        /// <summary>Rent at building levels 1..<see cref="MaxBuildingLevel"/>; empty when not developable.</summary>
        public IReadOnlyList<int> DevelopedRents => _developedRents;

        /// <summary>What the bank lends against the property: half its price.</summary>
        /// <summary>What the bank pays when the owner sells the property back: half its price.</summary>
        public int SaleValue => Price / 2;

        public static SquareDefinition Start(string name) => new SquareDefinition(name, SquareType.Start, 0, 0, 0, 0, 0, null);

        public static SquareDefinition Rest(string name) => new SquareDefinition(name, SquareType.Rest, 0, 0, 0, 0, 0, null);

        /// <summary>Draws a card from the board's deck.</summary>
        public static SquareDefinition Card(string name) => new SquareDefinition(name, SquareType.Card, 0, 0, 0, 0, 0, null);

        /// <summary>Pays out the pot that taxes and card fines fill.</summary>
        public static SquareDefinition Pot(string name) => new SquareDefinition(name, SquareType.Pot, 0, 0, 0, 0, 0, null);

        /// <summary>Offers a ride to any other square for <paramref name="fee"/>.</summary>
        public static SquareDefinition Travel(string name, int fee) =>
            new SquareDefinition(name, SquareType.Travel, 0, 0, 0, 0, 0, null, fee);

        /// <summary>Locks the player in for a few turns; <paramref name="fee"/> lets them out early.</summary>
        public static SquareDefinition Jail(string name, int fee) =>
            new SquareDefinition(name, SquareType.Jail, 0, 0, 0, 0, 0, null, fee);

        /// <summary>
        /// The champions' cup: for <paramref name="fee"/> the visitor raises one of their towns' rent by <paramref name="percent"/>
        /// per cent, plus <paramref name="step"/> per cent for every pick made before in the game.
        /// </summary>
        public static SquareDefinition Cup(string name, int fee, int percent, int step) =>
            new SquareDefinition(name, SquareType.Cup, 0, 0, 0, 0, 0, null, fee, boostPercent: percent, boostStep: step);

        /// <summary>Gives the next roll a choice of two.</summary>
        public static SquareDefinition Lookout(string name) => new SquareDefinition(name, SquareType.Lookout, 0, 0, 0, 0, 0, null);

        public static SquareDefinition Tax(string name, int amount) =>
            new SquareDefinition(name, SquareType.Tax, 0, 0, 0, amount, 0, null);

        /// <summary>A tax of <paramref name="percent"/> per cent of the visitor's property value (towns and resorts at their price, buildings at their cost).</summary>
        public static SquareDefinition PercentTax(string name, int percent) =>
            new SquareDefinition(name, SquareType.Tax, 0, 0, 0, 0, 0, null, taxPercent: percent);

        /// <summary>A property without buildings.</summary>
        public static SquareDefinition Property(string name, int price, int baseRent, int groupId) =>
            new SquareDefinition(name, SquareType.Property, price, baseRent, groupId, 0, 0, null);

        /// <summary>A holiday resort: bought like a town, never built on, its rent <paramref name="rentPerResort"/> per resort the owner holds.</summary>
        public static SquareDefinition Resort(string name, int price, int rentPerResort, int groupId) =>
            new SquareDefinition(name, SquareType.Property, price, rentPerResort, groupId, 0, 0, null, 0, isResort: true);

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
