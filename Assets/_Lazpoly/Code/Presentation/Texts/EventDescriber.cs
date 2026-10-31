using Lazpoly.Core.Board;
using Lazpoly.Core.Events;
using Lazpoly.Core.State;

namespace Lazpoly.Presentation.Texts
{
    /// <summary>Turns game events into the Turkish lines shown in the game log.</summary>
    public static class EventDescriber
    {
        /// <summary>Returns the log line for an event, or null when the event needs no line.</summary>
        public static string Describe(GameEvent gameEvent, GameState state, BoardDefinition board)
        {
            switch (gameEvent)
            {
                case TurnStartedEvent e:
                    return $"Sıra: {NameOf(state, e.PlayerId)}";
                case DiceRolledEvent e:
                    return $"{NameOf(state, e.PlayerId)} zar attı: {e.Die1} + {e.Die2}{(e.IsDouble ? " (çift)" : string.Empty)}";
                case PlayerMovedEvent e:
                    return $"{NameOf(state, e.PlayerId)} → {board[e.ToSquare].Name}";
                case MoneyChangedEvent e:
                    return $"{NameOf(state, e.PlayerId)}: {(e.Delta > 0 ? "+" : string.Empty)}{e.Delta} ({ReasonText(e.Reason)})";
                case PropertyOwnerChangedEvent e:
                    return e.NewOwnerId == GameState.NoOwner
                        ? $"{board[e.SquareIndex].Name} bankaya döndü"
                        : $"{board[e.SquareIndex].Name} → {NameOf(state, e.NewOwnerId)}";
                case PlayerBankruptedEvent e:
                    return $"{NameOf(state, e.PlayerId)} iflas etti!";
                case GameEndedEvent e:
                    return $"Oyun bitti! Kazanan: {NameOf(state, e.WinnerId)}";
                default:
                    return null;
            }
        }

        public static string ReasonText(MoneyReason reason)
        {
            switch (reason)
            {
                case MoneyReason.PassedStart: return "başlangıçtan geçiş";
                case MoneyReason.PropertyPurchase: return "mülk alımı";
                case MoneyReason.Rent: return "kira";
                case MoneyReason.Tax: return "vergi";
                case MoneyReason.Bankruptcy: return "iflas";
                default: return reason.ToString();
            }
        }

        private static string NameOf(GameState state, int playerId) => state.FindPlayer(playerId)?.Name ?? $"#{playerId}";
    }
}
