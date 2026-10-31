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
            int fullGroupRentMultiplier)
        {
            if (startingMoney <= 0) throw new ArgumentOutOfRangeException(nameof(startingMoney));
            if (passStartBonus < 0) throw new ArgumentOutOfRangeException(nameof(passStartBonus));
            if (diceSides < 2) throw new ArgumentOutOfRangeException(nameof(diceSides));
            if (maxConsecutiveDoubles < 1) throw new ArgumentOutOfRangeException(nameof(maxConsecutiveDoubles));
            if (fullGroupRentMultiplier < 1) throw new ArgumentOutOfRangeException(nameof(fullGroupRentMultiplier));

            StartingMoney = startingMoney;
            PassStartBonus = passStartBonus;
            DiceSides = diceSides;
            MaxConsecutiveDoubles = maxConsecutiveDoubles;
            FullGroupRentMultiplier = fullGroupRentMultiplier;
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
    }
}
