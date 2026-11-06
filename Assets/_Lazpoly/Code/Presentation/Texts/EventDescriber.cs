using System.Collections.Generic;
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

    /// <summary>One line of the event log: its text, its colour, whose line it is and whether it opens a turn.</summary>
    public readonly struct LogEntry
    {
        public LogEntry(string text, LogTone tone, int playerId, bool isTurnStart)
        {
            Text = text;
            Tone = tone;
            PlayerId = playerId;
            IsTurnStart = isTurnStart;
        }

        public string Text { get; }
        public LogTone Tone { get; }

        /// <summary>The player the line is about, or <see cref="GameState.NoOwner"/> when it is about no one in particular.</summary>
        public int PlayerId { get; }

        /// <summary>True for the heading that groups the lines of one turn.</summary>
        public bool IsTurnStart { get; }
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

        /// <summary>The player an event's log line is about (the mover, the payer, the new owner...), or <see cref="GameState.NoOwner"/>.</summary>
        public static int ActorOf(GameEvent gameEvent)
        {
            switch (gameEvent)
            {
                case TurnStartedEvent e: return e.PlayerId;
                case DiceRolledEvent e: return e.PlayerId;
                case PlayerMovedEvent e: return e.PlayerId;
                case MoneyChangedEvent e: return e.PlayerId;
                case PropertyOwnerChangedEvent e: return e.NewOwnerId;
                case PlayerBankruptedEvent e: return e.PlayerId;
                case GameEndedEvent e: return e.WinnerId;
                case CardDrawnEvent e: return e.PlayerId;
                case PlayerTimedOutEvent e: return e.PlayerId;
                case LookoutChangedEvent e: return e.PlayerId;
                case JailChangedEvent e: return e.PlayerId;
                case TradeProposedEvent e: return e.Offer.FromPlayerId;
                case DebtIncurredEvent e: return e.PlayerId;
                default: return GameState.NoOwner;
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
                    return F($"{NameOf(state, e.PlayerId)} kart çekti: {L(CardText(board, e.CardIndex))}");
                case LookoutChangedEvent e:
                    return e.HasLookout ? F($"{NameOf(state, e.PlayerId)} sonraki zarında iki atıştan birini seçecek") : null;
                case JailChangedEvent e:
                    if (e.RolledOut) return F($"{NameOf(state, e.PlayerId)} çift attı, hapisten çıktı");
                    return e.Turns > 0
                        ? F($"{NameOf(state, e.PlayerId)} hapiste: {e.Turns} tur bekleyecek")
                        : F($"{NameOf(state, e.PlayerId)} hapisten çıktı");
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
                case MoneyReason.PropertySale: return L("mülk satışı");
                case MoneyReason.Trade: return L("takas");
                case MoneyReason.Card: return L("kart");
                case MoneyReason.Pot: return L("çay kumbarası");
                case MoneyReason.Travel: return L("otogar bileti");
                case MoneyReason.Jail: return L("hapisten çıkış");
                default: return reason.ToString();
            }
        }

        /// <summary>The line over the table when a debt ends a player's game: "Unkas kirayı ödeyecek parası yok".</summary>
        public static string CannotPayText(string name, MoneyReason reason)
        {
            switch (reason)
            {
                case MoneyReason.Rent: return F($"{name} kirayı ödeyecek parası yok");
                case MoneyReason.Tax: return F($"{name} vergiyi ödeyecek parası yok");
                case MoneyReason.Card: return F($"{name} cezayı ödeyecek parası yok");
                default: return F($"{name} borcunu ödeyecek parası yok");
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

        /// <summary>A player's name, or "#id" for one no longer at the table.</summary>
        public static string NameOf(GameState state, int playerId) => state.FindPlayer(playerId)?.Name ?? $"#{playerId}";
    }
}
