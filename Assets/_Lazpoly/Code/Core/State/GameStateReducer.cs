using System;
using Lazpoly.Core.Events;

namespace Lazpoly.Core.State
{
    /// <summary>
    /// The only code that mutates <see cref="GameState"/>. The host runs it right after deciding
    /// each event; clients run it on events received from the host. Same input, same result.
    /// </summary>
    public static class GameStateReducer
    {
        public static void Apply(GameState state, GameEvent gameEvent)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            switch (gameEvent)
            {
                case TurnStartedEvent e:
                    state.CurrentPlayerIndex = RequireIndex(state, e.PlayerId);
                    state.TurnNumber++;
                    state.LastDie1 = 0;
                    state.LastDie2 = 0;
                    state.ConsecutiveDoubles = 0;
                    break;

                case DiceRolledEvent e:
                    state.LastDie1 = e.Die1;
                    state.LastDie2 = e.Die2;
                    if (e.IsDouble) state.ConsecutiveDoubles++;
                    break;

                case PlayerMovedEvent e:
                    RequirePlayer(state, e.PlayerId).Position = e.ToSquare;
                    break;

                case MoneyChangedEvent e:
                    RequirePlayer(state, e.PlayerId).Money += e.Delta;
                    break;

                case PropertyOwnerChangedEvent e:
                    state.SetOwner(e.SquareIndex, e.NewOwnerId);
                    break;

                case PlayerBankruptedEvent e:
                    RequirePlayer(state, e.PlayerId).IsBankrupt = true;
                    break;

                case PhaseChangedEvent e:
                    state.Phase = e.Phase;
                    break;

                case GameEndedEvent e:
                    state.WinnerId = e.WinnerId;
                    state.Phase = GamePhase.GameOver;
                    break;

                case null:
                    throw new ArgumentNullException(nameof(gameEvent));

                default:
                    throw new ArgumentException($"Unknown event type {gameEvent.GetType().Name}.", nameof(gameEvent));
            }
        }

        private static PlayerState RequirePlayer(GameState state, int playerId)
        {
            return state.FindPlayer(playerId)
                   ?? throw new InvalidOperationException($"Event refers to unknown player {playerId}.");
        }

        private static int RequireIndex(GameState state, int playerId)
        {
            int index = state.IndexOfPlayer(playerId);
            if (index < 0) throw new InvalidOperationException($"Event refers to unknown player {playerId}.");
            return index;
        }
    }
}
