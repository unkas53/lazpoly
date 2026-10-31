using System.Collections.Generic;
using Lazpoly.Core.State;

namespace Lazpoly.App.AI
{
    /// <summary>Who the game is waiting on right now: the player on turn, the auction bidders or the trade partner.</summary>
    public static class AiTurnOrder
    {
        public static IReadOnlyList<int> PlayersToAct(GameState state)
        {
            var players = new List<int>();
            switch (state.Phase)
            {
                case GamePhase.GameOver:
                    break;

                case GamePhase.AuctionInProgress:
                    // In seat order from the player on turn, so the same state always asks the same bidder first.
                    for (int offset = 0; offset < state.Players.Count; offset++)
                    {
                        PlayerState player = state.Players[(state.CurrentPlayerIndex + offset) % state.Players.Count];
                        if (player.IsBankrupt || state.Auction.HasPassed(player.Id) || state.Auction.HighestBidderId == player.Id) continue;
                        players.Add(player.Id);
                    }

                    break;

                case GamePhase.AwaitingTradeResponse:
                    players.Add(state.PendingTrade.ToPlayerId);
                    break;

                default:
                    players.Add(state.CurrentPlayer.Id);
                    break;
            }

            return players;
        }
    }
}
