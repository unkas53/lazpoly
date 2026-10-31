using Lazpoly.Core.Board;
using Lazpoly.Core.Events;
using Lazpoly.Core.State;

using static Lazpoly.Presentation.Texts.Lang;
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
                    return F($"Sıra: {NameOf(state, e.PlayerId)}");
                case DiceRolledEvent e:
                    return e.IsDouble
                        ? F($"{NameOf(state, e.PlayerId)} zar attı: {e.Die1} + {e.Die2} (çift)")
                        : F($"{NameOf(state, e.PlayerId)} zar attı: {e.Die1} + {e.Die2}");
                case PlayerMovedEvent e:
                    return $"{NameOf(state, e.PlayerId)} → {L(board[e.ToSquare].Name)}";
                case MoneyChangedEvent e:
                    return $"{NameOf(state, e.PlayerId)}: {(e.Delta > 0 ? "+" : string.Empty)}{e.Delta} ({L(ReasonText(e.Reason))})";
                case PropertyOwnerChangedEvent e:
                    return e.NewOwnerId == GameState.NoOwner
                        ? F($"{L(board[e.SquareIndex].Name)} bankaya döndü")
                        : $"{L(board[e.SquareIndex].Name)} → {NameOf(state, e.NewOwnerId)}";
                case PlayerBankruptedEvent e:
                    return F($"{NameOf(state, e.PlayerId)} iflas etti!");
                case GameEndedEvent e:
                    return e.Reason == GameEndReason.RoundLimit
                        ? F($"Tur sınırı doldu! En zengin oyuncu kazandı: {NameOf(state, e.WinnerId)}")
                        : F($"Oyun bitti! Kazanan: {NameOf(state, e.WinnerId)}");
                case BuildingLevelChangedEvent e:
                    return $"{L(board[e.SquareIndex].Name)}: {LevelText(e.Level)}";
                case MortgageChangedEvent e:
                    return e.IsMortgaged ? F($"{L(board[e.SquareIndex].Name)} ipotek edildi") : F($"{L(board[e.SquareIndex].Name)} ipotekten kurtarıldı");
                case AuctionStartedEvent e:
                    return F($"{L(board[e.SquareIndex].Name)} açık artırmada");
                case BidPlacedEvent e:
                    return F($"{NameOf(state, e.PlayerId)} teklif: {e.Amount}");
                case BidPassedEvent e:
                    return F($"{NameOf(state, e.PlayerId)} artırmadan çekildi");
                case AuctionEndedEvent e:
                    return e.WinnerId == GameState.NoOwner
                        ? F($"{L(board[e.SquareIndex].Name)} satılmadı")
                        : $"{L(board[e.SquareIndex].Name)} → {NameOf(state, e.WinnerId)} ({e.Price})";
                case TradeProposedEvent e:
                    return F($"{NameOf(state, e.Offer.FromPlayerId)}, {NameOf(state, e.Offer.ToPlayerId)} oyuncusuna takas teklif etti");
                case TradeResolvedEvent e:
                    return e.Outcome == TradeOutcome.Accepted ? L("Takas kabul edildi")
                        : e.Outcome == TradeOutcome.Rejected ? L("Takas reddedildi")
                        : L("Takas geri çekildi");
                case DebtIncurredEvent e:
                    return F($"{NameOf(state, e.PlayerId)} {e.Amount} ödeyemiyor ({ReasonText(e.Reason)}): para bulmalı ya da iflas etmeli");
                case DebtClearedEvent _:
                    return null;
                case CardDrawnEvent e:
                    return F($"{NameOf(state, e.PlayerId)}, Sokak Haberi: {L(CardText(board, e.CardIndex))}");
                case LookoutChangedEvent e:
                    return e.HasLookout ? F($"{NameOf(state, e.PlayerId)} sonraki zarında iki atıştan birini seçecek") : null;
                case DiceOptionsEvent e:
                    return F($"{NameOf(state, e.PlayerId)} iki kez attı: {e.FirstDie1} + {e.FirstDie2} ya da {e.SecondDie1} + {e.SecondDie2}");
                case PotChangedEvent _:
                    return null;
                case PlayerTimedOutEvent e:
                    return e.StandInTookOver
                        ? F($"{NameOf(state, e.PlayerId)} yanıt vermiyor: bilgisayar onun yerine oynuyor, bir hamle yapınca kontrol ona döner")
                        : F($"{NameOf(state, e.PlayerId)} için süre doldu, en temkinli hamle yapıldı");
                default:
                    return null;
            }
        }

        public static string ReasonText(MoneyReason reason)
        {
            switch (reason)
            {
                case MoneyReason.PassedStart: return L("başlangıçtan geçiş");
                case MoneyReason.PropertyPurchase: return L("mülk alımı");
                case MoneyReason.Rent: return L("kira");
                case MoneyReason.Tax: return L("vergi");
                case MoneyReason.Bankruptcy: return L("iflas");
                case MoneyReason.Building: return L("inşaat");
                case MoneyReason.BuildingSale: return L("bina satışı");
                case MoneyReason.Mortgage: return L("ipotek");
                case MoneyReason.Redeem: return L("ipotek ödemesi");
                case MoneyReason.Auction: return L("açık artırma");
                case MoneyReason.Trade: return L("takas");
                case MoneyReason.Card: return L("Sokak Haberi");
                case MoneyReason.Pot: return L("çay kumbarası");
                case MoneyReason.Travel: return L("otogar bileti");
                default: return reason.ToString();
            }
        }

        /// <summary>"bina yok", "2 ev" or "otel".</summary>
        public static string LevelText(int level)
        {
            if (level <= 0) return L("bina yok");
            return level >= SquareDefinition.MaxBuildingLevel ? L("otel") : F($"{level} ev");
        }

        public static string CardText(BoardDefinition board, int cardIndex) =>
            cardIndex >= 0 && cardIndex < board.Cards.Count ? board.Cards[cardIndex].Text : "?";

        /// <summary>Who a debt is owed to, as the HUD names it.</summary>
        public static string CreditorText(GameState state, int creditorId) =>
            creditorId == GameState.PotId ? L("çay kumbarası") : creditorId == GameState.NoOwner ? L("banka") : NameOf(state, creditorId);

        private static string NameOf(GameState state, int playerId) => state.FindPlayer(playerId)?.Name ?? $"#{playerId}";
    }
}
