using System;
using Lazpoly.Core.Board;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Texts
{
    /// <summary>The first-game hints. Each is shown once, when its situation first comes up.</summary>
    public enum TipId
    {
        FirstRoll = 0,
        BuyOrDecline = 1,
        CanBuild = 3,
        ShortOfMoney = 4,
        Debt = 5,
        Travel = 6,
        DiceChoice = 7,
        RoundLimit = 8,
        Trade = 9,
        Pot = 10,
    }

    public readonly struct Tip
    {
        public Tip(TipId id, string text)
        {
            Id = id;
            Text = text;
        }

        public TipId Id { get; }
        public string Text { get; }
    }

    /// <summary>
    /// Picks the hint that fits what <paramref name="playerId"/> faces right now. Pure: the HUD
    /// remembers which hints were seen and decides when to ask.
    /// </summary>
    public static class Tips
    {
        public const int LowMoney = 150;

        /// <summary>The most useful unseen hint for this player now, or null.</summary>
        public static Tip? Next(BoardDefinition board, RulesConfig rules, GameState state, int playerId, Func<TipId, bool> seen)
        {
            if (board == null || rules == null || state == null || seen == null || state.Phase == GamePhase.GameOver) return null;
            PlayerState me = state.FindPlayer(playerId);
            if (me == null || me.IsBankrupt) return null;
            bool myTurn = state.CurrentPlayer.Id == playerId;

            Tip? Offer(TipId id, bool when, string text) => when && !seen(id) ? new Tip(id, text) : (Tip?)null;

            // Decisions first: they explain the buttons the player is looking at.
            return Offer(TipId.Debt, myTurn && state.Phase == GamePhase.AwaitingDebtPayment,
                       L("Borcunu ödeyecek kadar paran yok. Bina ya da mülk satarak para bul; bulamazsan iflas edersin."))
                   ?? Offer(TipId.Travel, myTurn && state.Phase == GamePhase.AwaitingTravelDecision,
                       F($"Otogardasın. Tahtada halkalı karelerden birine tıkla ve {board[me.Position].Fee} ödeyerek oraya yürü. İstemezsen \"Burada kal\"."))
                   ?? Offer(TipId.DiceChoice, myTurn && state.Phase == GamePhase.AwaitingDiceChoice,
                       L("Seyir Terası sayesinde iki kez attın. Düğmelerde her atışın seni nereye götüreceği yazıyor; birini seç."))
                   ?? Offer(TipId.BuyOrDecline, myTurn && state.Phase == GamePhase.AwaitingBuyDecision,
                       L("Bu kare sahipsiz. Satın alabilirsin; almazsan bankada kalır."))
                   ?? Offer(TipId.RoundLimit, rules.RoundLimit > 0,
                       F($"Bu oyun {rules.RoundLimit} tur sürecek. Sonunda parası, mülkleri ve binaları en değerli olan kazanır."))
                   ?? Offer(TipId.FirstRoll, myTurn && state.Phase == GamePhase.WaitingForRoll,
                       L("Sıra sende: \"Zar At\" düğmesine bas. Bir kareye fareyle gelince ya da tıklayınca o karenin kartı açılır."))
                   ?? Offer(TipId.CanBuild, myTurn && GameRules.BuildableNow(board, state, me, me.Position) > 0,
                       F($"Kendi şehrine geldin! Açılan bina kartından ev kurabilirsin: başlangıçtan geçene kadar bir ev, sonra {GameRules.MaxHouses} eve kadar. Dört evli şehre yeniden gelince otel kurulur."))
                   ?? Offer(TipId.ShortOfMoney, me.Money < LowMoney && HasSellable(board, state, playerId),
                       L("Paran azaldı. Bir ödemeye yetmezse şehirlerin haritada halkalanır: tıkladığını bankaya satarsın, banka fiyatın yarısını öder."))
                   ?? Offer(TipId.Pot, board.HasPot && state.Pot >= 100,
                       F($"Çay kumbarasında {state.Pot} birikti. Vergiler ve kart cezaları buraya girer; Çay Molası'na gelen hepsini alır."))
                   ?? Offer(TipId.Trade, myTurn && state.Phase == GamePhase.WaitingForRoll && state.Round >= 3,
                       L("Bir rengin tamamı sende olunca boş kirası katlanır. Eksik kareyi \"Takas Teklif Et\" ile para ya da mülk karşılığında iste."));
        }

        private static bool HasSellable(BoardDefinition board, GameState state, int playerId)
        {
            for (int square = 0; square < board.Count; square++)
            {
                if (state.GetOwner(square) == playerId) return true;
            }

            return false;
        }
    }
}
