using System;

namespace Lazpoly.Core.Rules
{
    /// <summary>
    /// Numbers that tune a game. Every value is passed in explicitly: Lazpoly's final
    /// economy will come from game data, so the core ships no hidden defaults.
    /// </summary>
    public sealed class RulesConfig
    {
        public const int AbsoluteMinPlayers = 2;
        public const int AbsoluteMaxPlayers = 8;

        public RulesConfig(
            int startingMoney,
            int passStartBonus,
            int diceSides,
            int maxConsecutiveDoubles,
            int fullGroupRentMultiplier,
            int buildingSellbackPercent,
            bool auctionsEnabled = true,
            int roundLimit = 0,
            int turnSeconds = TableRules.DefaultTurnSeconds,
            BankReturns bankReturns = BankReturns.FirstComer)
        {
            if (startingMoney <= 0) throw new ArgumentOutOfRangeException(nameof(startingMoney));
            if (passStartBonus < 0) throw new ArgumentOutOfRangeException(nameof(passStartBonus));
            if (diceSides < 2) throw new ArgumentOutOfRangeException(nameof(diceSides));
            if (maxConsecutiveDoubles < 1) throw new ArgumentOutOfRangeException(nameof(maxConsecutiveDoubles));
            if (fullGroupRentMultiplier < 1) throw new ArgumentOutOfRangeException(nameof(fullGroupRentMultiplier));
            if (buildingSellbackPercent < 0 || buildingSellbackPercent > 100) throw new ArgumentOutOfRangeException(nameof(buildingSellbackPercent));
            if (roundLimit < 0) throw new ArgumentOutOfRangeException(nameof(roundLimit));
            if (turnSeconds < 0) throw new ArgumentOutOfRangeException(nameof(turnSeconds));

            StartingMoney = startingMoney;
            PassStartBonus = passStartBonus;
            DiceSides = diceSides;
            MaxConsecutiveDoubles = maxConsecutiveDoubles;
            FullGroupRentMultiplier = fullGroupRentMultiplier;
            BuildingSellbackPercent = buildingSellbackPercent;
            AuctionsEnabled = auctionsEnabled;
            RoundLimit = roundLimit;
            TurnSeconds = turnSeconds;
            BankReturns = bankReturns;
        }

        public int StartingMoney { get; }

        /// <summary>Paid each time a player passes or lands on the start square.</summary>
        public int PassStartBonus { get; }

        /// <summary>Sides per die. Two dice are rolled each time.</summary>
        public int DiceSides { get; }

        /// <summary>Rolling this many doubles in one turn forfeits the move and ends the turn.</summary>
        public int MaxConsecutiveDoubles { get; }

        /// <summary>Rent multiplier when the owner holds every property of the group.</summary>
        public int FullGroupRentMultiplier { get; }

        /// <summary>Share of the building cost the bank pays back when a building is sold, in percent.</summary>
        public int BuildingSellbackPercent { get; }

        /// <summary>Whether a declined purchase goes to auction; otherwise the square simply stays with the bank.</summary>
        public bool AuctionsEnabled { get; }

        /// <summary>
        /// Rounds (every player has had a turn) after which the game ends and the richest player
        /// wins; 0 plays until one player is left.
        /// </summary>
        public int RoundLimit { get; }

        /// <summary>Seconds per move online (0 = no clock); see <see cref="TableRules.TurnSeconds"/>.</summary>
        public int TurnSeconds { get; }

        /// <summary>Where a bankrupt player's squares go when owed to the bank; see <see cref="TableRules.BankReturns"/>.</summary>
        public BankReturns BankReturns { get; }

        /// <summary>Whether a bankrupt player's squares are auctioned at once (auctions on and the table says so).</summary>
        public bool AuctionsBankReturns => AuctionsEnabled && BankReturns == BankReturns.Auction;

        /// <summary>The choices made at the table for this game.</summary>
        public TableRules Table => new TableRules(StartingMoney, AuctionsEnabled, RoundLimit, TurnSeconds, BankReturns);

        /// <summary>These rules with the table's choices applied; a table value of 0 keeps the board's starting money.</summary>
        public RulesConfig WithTable(TableRules table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            return new RulesConfig(table.StartingMoney > 0 ? table.StartingMoney : StartingMoney, PassStartBonus, DiceSides,
                MaxConsecutiveDoubles, FullGroupRentMultiplier, BuildingSellbackPercent,
                table.AuctionsEnabled, table.RoundLimit, table.TurnSeconds, table.BankReturns);
        }

        /// <summary>What the bank pays for one building level that cost <paramref name="buildingCost"/>.</summary>
        public int SellbackValue(int buildingCost) => buildingCost * BuildingSellbackPercent / 100;
    }
}
