using Lazpoly.Core.Commands;
using Lazpoly.Core.State;

namespace Lazpoly.App.Session
{
    /// <summary>
    /// The move made for a player whose time ran out: the one that commits them to the least.
    /// Nothing is bought, bid, accepted or paid for them; the game only goes on.
    /// </summary>
    public static class TimeoutPolicy
    {
        /// <returns>The command, or null when no cautious move exists and the computer has to take over (an open debt).</returns>
        public static GameCommand CommandFor(GameState state, int playerId)
        {
            switch (state.Phase)
            {
                case GamePhase.WaitingForRoll: return new RollDiceCommand(playerId);
                case GamePhase.AwaitingBuyDecision: return new DeclinePurchaseCommand(playerId);
                case GamePhase.TurnEnd: return new EndTurnCommand(playerId);
                case GamePhase.AuctionInProgress: return new PassBidCommand(playerId);
                case GamePhase.AwaitingTradeResponse: return new RejectTradeCommand(playerId);
                case GamePhase.AwaitingTravelDecision: return new DeclineTravelCommand(playerId);
                case GamePhase.AwaitingDiceChoice: return new ChooseDiceCommand(playerId, 0);
                default: return null;
            }
        }
    }
}
