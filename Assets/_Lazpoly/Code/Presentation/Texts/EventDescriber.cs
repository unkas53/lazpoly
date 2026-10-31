using Lazpoly.Core.Board;
using Lazpoly.Core.Events;
using Lazpoly.Core.State;

namespace Lazpoly.Presentation.Texts
{
    /// <summary>How a log line is coloured: money coming in, going out, or anything else.</summary>
    public enum LogTone
    {
        Neutral,
        Gain,
        Loss,
    }

    /// <summary>Turns game events into the Turkish lines shown in the game log.</summary>
    public static class EventDescriber
    {
        public static LogTone ToneOf(GameEvent gameEvent)
        {
            switch (gameEvent)
            {
                case MoneyChangedEvent e: return e.Delta > 0 ? LogTone.Gain : e.Delta < 0 ? LogTone.Loss : LogTone.Neutral;
                case PlayerBankruptedEvent _: return LogTone.Loss;
                case DebtIncurredEvent _: return LogTone.Loss;
                default: return LogTone.Neutral;
            }
        }

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
                    return e.Reason == GameEndReason.RoundLimit
                        ? $"Tur sınırı doldu! En zengin oyuncu kazandı: {NameOf(state, e.WinnerId)}"
                        : $"Oyun bitti! Kazanan: {NameOf(state, e.WinnerId)}";
                case BuildingLevelChangedEvent e:
                    return $"{board[e.SquareIndex].Name}: {LevelText(e.Level)}";
                case MortgageChangedEvent e:
                    return e.IsMortgaged ? $"{board[e.SquareIndex].Name} ipotek edildi" : $"{board[e.SquareIndex].Name} ipotekten kurtarıldı";
                case AuctionStartedEvent e:
                    return $"{board[e.SquareIndex].Name} açık artırmada";
                case BidPlacedEvent e:
                    return $"{NameOf(state, e.PlayerId)} teklif: {e.Amount}";
                case BidPassedEvent e:
                    return $"{NameOf(state, e.PlayerId)} artırmadan çekildi";
                case AuctionEndedEvent e:
                    return e.WinnerId == GameState.NoOwner
                        ? $"{board[e.SquareIndex].Name} satılmadı"
                        : $"{board[e.SquareIndex].Name} → {NameOf(state, e.WinnerId)} ({e.Price})";
                case TradeProposedEvent e:
                    return $"{NameOf(state, e.Offer.FromPlayerId)}, {NameOf(state, e.Offer.ToPlayerId)} oyuncusuna takas teklif etti";
                case TradeResolvedEvent e:
                    return e.Outcome == TradeOutcome.Accepted ? "Takas kabul edildi"
                        : e.Outcome == TradeOutcome.Rejected ? "Takas reddedildi"
                        : "Takas geri çekildi";
                case DebtIncurredEvent e:
                    return $"{NameOf(state, e.PlayerId)} {e.Amount} ödeyemiyor ({ReasonText(e.Reason)}): para bulmalı ya da iflas etmeli";
                case DebtClearedEvent _:
                    return null;
                case CardDrawnEvent e:
                    return $"{NameOf(state, e.PlayerId)}, Sokak Haberi: {CardText(board, e.CardIndex)}";
                case LookoutChangedEvent e:
                    return e.HasLookout ? $"{NameOf(state, e.PlayerId)} sonraki zarında iki atıştan birini seçecek" : null;
                case DiceOptionsEvent e:
                    return $"{NameOf(state, e.PlayerId)} iki kez attı: {e.FirstDie1} + {e.FirstDie2} ya da {e.SecondDie1} + {e.SecondDie2}";
                case PotChangedEvent _:
                    return null;
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
                case MoneyReason.Building: return "inşaat";
                case MoneyReason.BuildingSale: return "bina satışı";
                case MoneyReason.Mortgage: return "ipotek";
                case MoneyReason.Redeem: return "ipotek ödemesi";
                case MoneyReason.Auction: return "açık artırma";
                case MoneyReason.Trade: return "takas";
                case MoneyReason.Card: return "Sokak Haberi";
                case MoneyReason.Pot: return "çay kumbarası";
                case MoneyReason.Travel: return "otogar bileti";
                default: return reason.ToString();
            }
        }

        /// <summary>"bina yok", "2 ev" or "otel".</summary>
        public static string LevelText(int level)
        {
            if (level <= 0) return "bina yok";
            return level >= SquareDefinition.MaxBuildingLevel ? "otel" : $"{level} ev";
        }

        public static string CardText(BoardDefinition board, int cardIndex) =>
            cardIndex >= 0 && cardIndex < board.Cards.Count ? board.Cards[cardIndex].Text : "?";

        /// <summary>Who a debt is owed to, as the HUD names it.</summary>
        public static string CreditorText(GameState state, int creditorId) =>
            creditorId == GameState.PotId ? "çay kumbarası" : creditorId == GameState.NoOwner ? "banka" : NameOf(state, creditorId);

        private static string NameOf(GameState state, int playerId) => state.FindPlayer(playerId)?.Name ?? $"#{playerId}";
    }
}
