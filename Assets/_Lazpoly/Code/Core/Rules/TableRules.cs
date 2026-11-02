using System;

namespace Lazpoly.Core.Rules
{
    /// <summary>What happens to the properties of a player who goes bankrupt to the bank.</summary>
    public enum BankReturns
    {
        /// <summary>They stay with the bank; whoever lands there next may buy at the list price.</summary>
        FirstComer = 0,

        /// <summary>Each one is auctioned at once, before the next turn (needs auctions on).</summary>
        Auction = 1,
    }

    /// <summary>
    /// The house rules picked when a game is set up: they change one game, not the board data, so
    /// they are sent with the game (and saved with it) instead of being part of the board checksum.
    /// </summary>
    public sealed class TableRules
    {
        /// <summary>The turn time an online table gets when nobody picks one.</summary>
        public const int DefaultTurnSeconds = 90;

        /// <summary>Board starting money, auctions on, no round limit, 90-second turns, bankrupt's squares wait for the next visitor.</summary>
        public static readonly TableRules Default = new TableRules(0, true, 0);

        /// <param name="startingMoney">0 keeps the board's starting money.</param>
        /// <param name="roundLimit">0 plays until one player is left.</param>
        /// <param name="turnSeconds">Online: seconds a player has for a move before the host moves for them; 0 means no clock.</param>
        public TableRules(int startingMoney, bool auctionsEnabled, int roundLimit, int turnSeconds = DefaultTurnSeconds,
            BankReturns bankReturns = BankReturns.FirstComer)
        {
            if (startingMoney < 0) throw new ArgumentOutOfRangeException(nameof(startingMoney));
            if (roundLimit < 0) throw new ArgumentOutOfRangeException(nameof(roundLimit));
            if (turnSeconds < 0) throw new ArgumentOutOfRangeException(nameof(turnSeconds));
            if (!Enum.IsDefined(typeof(BankReturns), bankReturns)) throw new ArgumentOutOfRangeException(nameof(bankReturns));
            StartingMoney = startingMoney;
            AuctionsEnabled = auctionsEnabled;
            RoundLimit = roundLimit;
            TurnSeconds = turnSeconds;
            BankReturns = bankReturns;
        }

        public int StartingMoney { get; }
        public bool AuctionsEnabled { get; }
        public int RoundLimit { get; }

        /// <summary>Seconds per move in an online game; 0 means unlimited. Local games never run a clock.</summary>
        public int TurnSeconds { get; }

        /// <summary>Where a bankrupt player's squares go when the bank is the creditor.</summary>
        public BankReturns BankReturns { get; }

        /// <summary>Bank auctions need auctions at all; with them off the squares simply wait for the next visitor.</summary>
        public bool AuctionsBankReturns => AuctionsEnabled && BankReturns == BankReturns.Auction;

        public TableRules WithStartingMoney(int value) => new TableRules(value, AuctionsEnabled, RoundLimit, TurnSeconds, BankReturns);
        public TableRules WithAuctions(bool value) => new TableRules(StartingMoney, value, RoundLimit, TurnSeconds, BankReturns);
        public TableRules WithRoundLimit(int value) => new TableRules(StartingMoney, AuctionsEnabled, value, TurnSeconds, BankReturns);
        public TableRules WithTurnSeconds(int value) => new TableRules(StartingMoney, AuctionsEnabled, RoundLimit, value, BankReturns);
        public TableRules WithBankReturns(BankReturns value) => new TableRules(StartingMoney, AuctionsEnabled, RoundLimit, TurnSeconds, value);

        public bool SameAs(TableRules other) =>
            other != null && StartingMoney == other.StartingMoney && AuctionsEnabled == other.AuctionsEnabled && RoundLimit == other.RoundLimit &&
            TurnSeconds == other.TurnSeconds && BankReturns == other.BankReturns;
    }
}
