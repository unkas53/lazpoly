using System.Collections.Generic;
using Lazpoly.Core.State;

namespace Lazpoly.App.AI
{
    /// <summary>Who the game is waiting on right now: the player on turn or the trade partner.</summary>
    public static class AiTurnOrder
    {
        public static IReadOnlyList<int> PlayersToAct(GameState state)
        {
            var players = new List<int>();
            switch (state.Phase)
            {
                case GamePhase.GameOver:
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
