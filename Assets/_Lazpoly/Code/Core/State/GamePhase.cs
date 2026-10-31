namespace Lazpoly.Core.State
{
    public enum GamePhase
    {
        /// <summary>The current player must roll the dice.</summary>
        WaitingForRoll = 0,

        /// <summary>The current player landed on an unowned property they can afford and must buy or decline.</summary>
        AwaitingBuyDecision = 1,

        /// <summary>The current player has finished moving and may end the turn.</summary>
        TurnEnd = 2,

        /// <summary>Only one player is left solvent.</summary>
        GameOver = 3,
    }
}
