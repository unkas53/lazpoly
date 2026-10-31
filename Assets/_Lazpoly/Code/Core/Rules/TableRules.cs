using System;

namespace Lazpoly.Core.Rules
{
    /// <summary>
    /// The house rules picked when a game is set up: they change one game, not the board data, so
    /// they are sent with the game (and saved with it) instead of being part of the board checksum.
    /// </summary>
    public sealed class TableRules
    {
        /// <summary>Board starting money, auctions on, no round limit.</summary>
        public static readonly TableRules Default = new TableRules(0, true, 0);

        /// <param name="startingMoney">0 keeps the board's starting money.</param>
        /// <param name="roundLimit">0 plays until one player is left.</param>
        public TableRules(int startingMoney, bool auctionsEnabled, int roundLimit)
        {
            if (startingMoney < 0) throw new ArgumentOutOfRangeException(nameof(startingMoney));
            if (roundLimit < 0) throw new ArgumentOutOfRangeException(nameof(roundLimit));
            StartingMoney = startingMoney;
            AuctionsEnabled = auctionsEnabled;
            RoundLimit = roundLimit;
        }

        public int StartingMoney { get; }
        public bool AuctionsEnabled { get; }
        public int RoundLimit { get; }

        public TableRules WithStartingMoney(int value) => new TableRules(value, AuctionsEnabled, RoundLimit);
        public TableRules WithAuctions(bool value) => new TableRules(StartingMoney, value, RoundLimit);
        public TableRules WithRoundLimit(int value) => new TableRules(StartingMoney, AuctionsEnabled, value);

        public bool SameAs(TableRules other) =>
            other != null && StartingMoney == other.StartingMoney && AuctionsEnabled == other.AuctionsEnabled && RoundLimit == other.RoundLimit;
    }
}
