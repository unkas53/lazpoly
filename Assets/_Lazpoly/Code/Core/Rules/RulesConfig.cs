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
            int roundLimit = 0,
            int turnSeconds = TableRules.DefaultTurnSeconds,
            int jailTurns = 0,
            int timeLimitMinutes = 0)
        {
            if (startingMoney <= 0) throw new ArgumentOutOfRangeException(nameof(startingMoney));
            if (passStartBonus < 0) throw new ArgumentOutOfRangeException(nameof(passStartBonus));
            if (diceSides < 2) throw new ArgumentOutOfRangeException(nameof(diceSides));
            if (maxConsecutiveDoubles < 1) throw new ArgumentOutOfRangeException(nameof(maxConsecutiveDoubles));
            if (fullGroupRentMultiplier < 1) throw new ArgumentOutOfRangeException(nameof(fullGroupRentMultiplier));
            if (buildingSellbackPercent < 0 || buildingSellbackPercent > 100) throw new ArgumentOutOfRangeException(nameof(buildingSellbackPercent));
            if (roundLimit < 0) throw new ArgumentOutOfRangeException(nameof(roundLimit));
            if (turnSeconds < 0) throw new ArgumentOutOfRangeException(nameof(turnSeconds));
            if (jailTurns < 0) throw new ArgumentOutOfRangeException(nameof(jailTurns));
            if (timeLimitMinutes < 0) throw new ArgumentOutOfRangeException(nameof(timeLimitMinutes));

            StartingMoney = startingMoney;
            PassStartBonus = passStartBonus;
            DiceSides = diceSides;
            MaxConsecutiveDoubles = maxConsecutiveDoubles;
            FullGroupRentMultiplier = fullGroupRentMultiplier;
            BuildingSellbackPercent = buildingSellbackPercent;
            RoundLimit = roundLimit;
            TurnSeconds = turnSeconds;
            JailTurns = jailTurns;
            TimeLimitMinutes = timeLimitMinutes;
        }

        public int StartingMoney { get; }

        /// <summary>Paid each time a player passes or lands on the start square.</summary>
        public int PassStartBonus { get; }

        /// <summary>Sides per die. Two dice are rolled each time.</summary>
        public int DiceSides { get; }

        /// <summary>Rolling this many doubles in one turn sends the player to jail (or, on a board without one, forfeits the move); the turn ends.</summary>
        public int MaxConsecutiveDoubles { get; }

        /// <summary>Rent multiplier when the owner holds every property of the group.</summary>
        public int FullGroupRentMultiplier { get; }

        /// <summary>Share of the building cost the bank pays back when a building is sold, in percent.</summary>
        public int BuildingSellbackPercent { get; }

        /// <summary>
        /// Rounds (every player has had a turn) after which the game ends and the richest player
        /// wins; 0 plays until one player is left.
        /// </summary>
        public int RoundLimit { get; }

        /// <summary>Seconds per move online (0 = no clock); see <see cref="TableRules.TurnSeconds"/>.</summary>
        public int TurnSeconds { get; }

        /// <summary>How many of their turns a jailed player waits before walking out free (they may pay to leave sooner).</summary>
        public int JailTurns { get; }

        /// <summary>
        /// Minutes of game time after which the game ends when the turn on play is over, the richest player winning (the
        /// owner's rule, Faz 28); 0 plays without a game clock.
        /// </summary>
        public int TimeLimitMinutes { get; }

        /// <summary>The game time limit in seconds; 0 without one.</summary>
        public int TimeLimitSeconds => TimeLimitMinutes * 60;

        /// <summary>The choices made at the table for this game.</summary>
        public TableRules Table => new TableRules(StartingMoney, RoundLimit, TurnSeconds, TimeLimitMinutes);

        /// <summary>These rules with the table's choices applied; a table value of 0 keeps the board's starting money.</summary>
        public RulesConfig WithTable(TableRules table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            return new RulesConfig(table.StartingMoney > 0 ? table.StartingMoney : StartingMoney, PassStartBonus, DiceSides,
                MaxConsecutiveDoubles, FullGroupRentMultiplier, BuildingSellbackPercent,
                table.RoundLimit, table.TurnSeconds, JailTurns, table.TimeLimitMinutes);
        }

        /// <summary>What the bank pays for one building level that cost <paramref name="buildingCost"/>.</summary>
        public int SellbackValue(int buildingCost) => buildingCost * BuildingSellbackPercent / 100;
    }
}
