namespace Lazpoly.Core.State
{
    public enum GamePhase
    {
        /// <summary>The current player must roll the dice.</summary>
        WaitingForRoll = 0,

        /// <summary>The current player landed on an unowned property and may buy it or leave it.</summary>
        AwaitingBuyDecision = 1,

        /// <summary>The current player has finished moving and may end the turn.</summary>
        TurnEnd = 2,

        /// <summary>Only one player is left solvent.</summary>
        GameOver = 3,

        /// <summary>The current player offered a trade and waits for the other player's answer.</summary>
        AwaitingTradeResponse = 5,

        /// <summary>The current player owes more than they hold and must raise money or go bankrupt.</summary>
        AwaitingDebtPayment = 6,

        /// <summary>The current player stands on a travel square and may pay for a ride or stay.</summary>
        AwaitingTravelDecision = 7,

        /// <summary>The current player rolled twice thanks to a lookout and must pick one roll.</summary>
        AwaitingDiceChoice = 8,

        /// <summary>The current player is in jail: they pay the fee to leave and roll, or wait out this turn.</summary>
        AwaitingJailDecision = 9,

        /// <summary>The current player stands on the cup square and may pay to bring the cup to one of their towns, or pass.</summary>
        AwaitingCupChoice = 10,
    }
}
