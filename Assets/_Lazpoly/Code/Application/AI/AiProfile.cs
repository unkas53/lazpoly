using System;

namespace Lazpoly.App.AI
{
    /// <summary>
    /// The knobs that make the difficulty levels play differently. Money amounts are percentages of
    /// the starting money, so the same profile fits any board.
    /// </summary>
    public sealed class AiProfile
    {
        private static readonly AiProfile EasyProfile = new AiProfile(AiDifficulty.Easy,
            cashReservePercent: 3, buildReserveMultiplier: 3, bidPercent: 80, groupBidBonusPercent: 0, blockBidBonusPercent: 0,
            proposesTrades: false, tradeOfferPercent: 0, tradeAcceptMarginPercent: 0, mistakePercent: 25);

        private static readonly AiProfile NormalProfile = new AiProfile(AiDifficulty.Normal,
            cashReservePercent: 8, buildReserveMultiplier: 1, bidPercent: 100, groupBidBonusPercent: 50, blockBidBonusPercent: 0,
            proposesTrades: true, tradeOfferPercent: 150, tradeAcceptMarginPercent: 10, mistakePercent: 5);

        private static readonly AiProfile HardProfile = new AiProfile(AiDifficulty.Hard,
            cashReservePercent: 8, buildReserveMultiplier: 1, bidPercent: 110, groupBidBonusPercent: 80, blockBidBonusPercent: 30,
            proposesTrades: true, tradeOfferPercent: 150, tradeAcceptMarginPercent: 15, mistakePercent: 0);

        private AiProfile(AiDifficulty difficulty, int cashReservePercent, int buildReserveMultiplier, int bidPercent,
            int groupBidBonusPercent, int blockBidBonusPercent, bool proposesTrades, int tradeOfferPercent,
            int tradeAcceptMarginPercent, int mistakePercent)
        {
            Difficulty = difficulty;
            CashReservePercent = cashReservePercent;
            BuildReserveMultiplier = buildReserveMultiplier;
            BidPercent = bidPercent;
            GroupBidBonusPercent = groupBidBonusPercent;
            BlockBidBonusPercent = blockBidBonusPercent;
            ProposesTrades = proposesTrades;
            TradeOfferPercent = tradeOfferPercent;
            TradeAcceptMarginPercent = tradeAcceptMarginPercent;
            MistakePercent = mistakePercent;
        }

        public AiDifficulty Difficulty { get; }

        /// <summary>Money kept back from purchases and bids, as a percentage of the starting money.</summary>
        public int CashReservePercent { get; }

        /// <summary>How many reserves must remain after paying for a house (Easy builds late).</summary>
        public int BuildReserveMultiplier { get; }

        /// <summary>Highest auction bid as a percentage of the square's price.</summary>
        public int BidPercent { get; }

        /// <summary>Extra bid allowance when the square would complete the AI's own group.</summary>
        public int GroupBidBonusPercent { get; }

        /// <summary>Extra bid allowance when the square would complete another player's group.</summary>
        public int BlockBidBonusPercent { get; }

        /// <summary>Whether the AI offers money for the last property it lacks in a group.</summary>
        public bool ProposesTrades { get; }

        /// <summary>Money offered for such a property, as a percentage of its price.</summary>
        public int TradeOfferPercent { get; }

        /// <summary>How much more an offer must be worth than what the AI gives away before it accepts.</summary>
        public int TradeAcceptMarginPercent { get; }

        /// <summary>Chance of skipping a good purchase or bid. Mistakes are always legal moves.</summary>
        public int MistakePercent { get; }

        public int CashReserve(int startingMoney) => startingMoney * CashReservePercent / 100;

        public static AiProfile For(AiDifficulty difficulty)
        {
            switch (difficulty)
            {
                case AiDifficulty.Easy: return EasyProfile;
                case AiDifficulty.Normal: return NormalProfile;
                case AiDifficulty.Hard: return HardProfile;
                default: throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Unknown AI difficulty.");
            }
        }
    }
}
