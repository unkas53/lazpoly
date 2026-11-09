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
        public int jailTurns;
        public int fullGroupRentMultiplier;
        public int buildingSellbackPercent;
        public GroupEntry[] groups;
        public SquareEntry[] squares;

        /// <summary>The deck drawn on the card squares ("Şans").</summary>
        public CardEntry[] cards;
    }

    [Serializable]
    public sealed class CardEntry
    {
        public string text;

        /// <summary>
        /// collect, pay, moveTo, moveBy, repairs, lookout or goToJail; and the cards kept in hand (Faz 30): lawyer, rentShield,
        /// chauffeur, cupTransfer, landDeal, renewal, fund, shield, strike, audit, demolish, illegalFloor, forceSale.
        /// </summary>
        public string kind;

        /// <summary>
        /// Money for collect and pay, steps for moveBy (negative = back), cost per house for repairs; the per cent of landDeal,
        /// renewal and audit, the turns of strike, the cash now of fund.
        /// </summary>
        public int amount;

        /// <summary>What investing costs on a fund card.</summary>
        public int cost;

        /// <summary>What a fund card's investment pays at Start.</summary>
        public int payout;

        /// <summary>Cost per hotel for repairs.</summary>
        public int hotel;

        /// <summary>Name of the destination square for moveTo.</summary>
        public string to;
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

        /// <summary>
        /// On a tax square instead of an amount: the share of the visitor's property value it takes, in percent. On a cup
        /// square: how much the first pick raises its town's rent, in percent.
        /// </summary>
        public int percent;

        /// <summary>On a cup square: the percent every pick adds to the next one's raise.</summary>
        public int step;

        /// <summary>On a cup square: the highest raise, in percent; 0 or missing for no limit.</summary>
        public int limit;

        /// <summary>Price of a ride from a travel square, of leaving jail early, or of bringing the cup to a town.</summary>
        public int fee;

        /// <summary>Cost of one house or the hotel; 0 or missing means the property takes no buildings.</summary>
        public int buildingCost;

        /// <summary>Rent with 1-4 houses and with a hotel (5 values).</summary>
        public int[] rents;
    }
}
