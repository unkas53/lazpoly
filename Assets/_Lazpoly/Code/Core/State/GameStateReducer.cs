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
                {
                    int next = RequireIndex(state, e.PlayerId);
                    // Going round to a seat at or before the current one starts a new round.
                    if (next <= state.CurrentPlayerIndex) state.Round++;
                    state.CurrentPlayerIndex = next;
                    state.TurnNumber++;
                    state.LastDie1 = 0;
                    state.LastDie2 = 0;
                    state.ConsecutiveDoubles = 0;
                    state.DiceOptions = null;
                    state.BuiltThisVisit = GameState.NotArrived;
                    break;
                }

                case DiceRolledEvent e:
                    state.LastDie1 = e.Die1;
                    state.LastDie2 = e.Die2;
                    state.DiceOptions = null;
                    if (e.IsDouble) state.ConsecutiveDoubles++;
                    break;

                case DiceOptionsEvent e:
                    RequirePlayer(state, e.PlayerId);
                    state.DiceOptions = new[] { e.FirstDie1, e.FirstDie2, e.SecondDie1, e.SecondDie2 };
                    break;

                case LookoutChangedEvent e:
                    RequirePlayer(state, e.PlayerId).HasLookout = e.HasLookout;
                    break;

                case JailChangedEvent e:
                    if (e.Turns < 0) throw new InvalidOperationException("Jail turns cannot be negative.");
                    RequirePlayer(state, e.PlayerId).JailTurns = e.Turns;
                    break;

                case PotChangedEvent e:
                    if (state.Pot + e.Delta < 0) throw new InvalidOperationException("The pot cannot go below zero.");
                    state.Pot += e.Delta;
                    break;

                case CardDrawnEvent e:
                    // Only announces the card; its effect arrives as the events that follow.
                    RequirePlayer(state, e.PlayerId);
                    break;

                case PlayerTimedOutEvent e:
                    RequirePlayer(state, e.PlayerId);
                    break;

                case PlayerMovedEvent e:
                {
                    PlayerState mover = RequirePlayer(state, e.PlayerId);
                    mover.Position = e.ToSquare;
                    if (e.PassedStart) mover.Laps++;
                    state.BuiltThisVisit = 0;
                    break;
                }

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

                case BuildingLevelChangedEvent e:
                    RequireSquare(state, e.SquareIndex);
                    if (e.Level > state.GetBuildingLevel(e.SquareIndex)) state.BuiltThisVisit = Math.Max(0, state.BuiltThisVisit) + 1;
                    state.SetBuildingLevel(e.SquareIndex, e.Level);
                    break;


                case TradeProposedEvent e:
                    RequirePlayer(state, e.Offer.FromPlayerId);
                    RequirePlayer(state, e.Offer.ToPlayerId);
                    state.PendingTrade = e.Offer;
                    state.TradeReturnPhase = state.Phase;
                    break;

                case TradeResolvedEvent _:
                    if (state.PendingTrade == null) throw new InvalidOperationException("No trade is open.");
                    state.PendingTrade = null;
                    break;

                case DebtIncurredEvent e:
                    RequirePlayer(state, e.PlayerId);
                    state.Debt = new DebtState(e.PlayerId, e.CreditorId, e.Amount, e.Reason);
                    break;

                case DebtClearedEvent _:
                    if (state.Debt == null) throw new InvalidOperationException("No debt is open.");
                    state.Debt = null;
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

        private static void RequireSquare(GameState state, int squareIndex)
        {
            if (squareIndex < 0 || squareIndex >= state.SquareCount)
            {
                throw new InvalidOperationException($"Event refers to square {squareIndex} outside the board.");
            }
        }

        private static int RequireIndex(GameState state, int playerId)
        {
            int index = state.IndexOfPlayer(playerId);
            if (index < 0) throw new InvalidOperationException($"Event refers to unknown player {playerId}.");
            return index;
        }
    }
}
