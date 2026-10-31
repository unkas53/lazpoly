namespace Lazpoly.Core.State
{
    public enum GamePhase
    {
        /// <summary>The current player must roll the dice.</summary>
        WaitingForRoll = 0,

        /// <summary>The current player landed on an unowned property and must buy it or send it to auction.</summary>
        AwaitingBuyDecision = 1,

        /// <summary>The current player has finished moving and may end the turn.</summary>
        TurnEnd = 2,

        /// <summary>Only one player is left solvent.</summary>
        GameOver = 3,

        /// <summary>A declined property is being auctioned; every solvent player may bid or pass.</summary>
        AuctionInProgress = 4,

        /// <summary>The current player offered a trade and waits for the other player's answer.</summary>
        AwaitingTradeResponse = 5,

        /// <summary>The current player owes more than they hold and must raise money or go bankrupt.</summary>
        AwaitingDebtPayment = 6,
    }
}
