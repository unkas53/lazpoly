using System;

namespace Lazpoly.Presentation.Data
{
    // JSON shape of Assets/_Lazpoly/Data/Resources/Lazpoly/board.json, read with JsonUtility.
    // Fields are public and lower-case to match the file; they are mapped to Core types by BoardDataLoader.

    [Serializable]
    public sealed class BoardFile
    {
        public int startingMoney;
        public int passStartBonus;
        public int diceSides;
        public int maxConsecutiveDoubles;
        public int fullGroupRentMultiplier;
        public int mortgageInterestPercent;
        public int buildingSellbackPercent;
        public GroupEntry[] groups;
        public SquareEntry[] squares;
    }

    [Serializable]
    public sealed class GroupEntry
    {
        public int id;
        public string name;
        public string color;
    }

    [Serializable]
    public sealed class SquareEntry
    {
        public string type;
        public string name;
        public int price;
        public int rent;
        public int group;
        public int amount;

        /// <summary>Cost of one house or the hotel; 0 or missing means the property takes no buildings.</summary>
        public int buildingCost;

        /// <summary>Rent with 1-4 houses and with a hotel (5 values).</summary>
        public int[] rents;
    }
}
