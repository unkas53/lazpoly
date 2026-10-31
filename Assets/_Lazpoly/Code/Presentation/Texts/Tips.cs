using System;
using Lazpoly.Core.Board;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;

namespace Lazpoly.Presentation.Texts
{
    /// <summary>The first-game hints. Each is shown once, when its situation first comes up.</summary>
    public enum TipId
    {
        FirstRoll = 0,
        BuyOrDecline = 1,
        Auction = 2,
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
                       "Borcunu ödeyecek kadar paran yok. Bina satarak ya da mülk ipotek ederek para bul; bulamazsan iflas edersin.")
                   ?? Offer(TipId.Travel, myTurn && state.Phase == GamePhase.AwaitingTravelDecision,
                       $"Otogardasın. Tahtada bir kareye tıkla ve {board[me.Position].Fee} ödeyerek oraya git. İstemezsen \"Burada kal\".")
                   ?? Offer(TipId.DiceChoice, myTurn && state.Phase == GamePhase.AwaitingDiceChoice,
                       "Seyir Terası sayesinde iki kez attın. Düğmelerde her atışın seni nereye götüreceği yazıyor; birini seç.")
                   ?? Offer(TipId.BuyOrDecline, myTurn && state.Phase == GamePhase.AwaitingBuyDecision,
                       rules.AuctionsEnabled
                           ? "Bu kare sahipsiz. Satın alabilirsin; almazsan açık artırmaya çıkar ve herkes teklif verebilir."
                           : "Bu kare sahipsiz. Satın alabilirsin; almazsan bankada kalır.")
                   ?? Offer(TipId.Auction, state.Phase == GamePhase.AuctionInProgress && state.Auction != null && !state.Auction.HasPassed(playerId),
                       "Açık artırma: teklif vererek fiyatı yükselt ya da pas geçip çekil. Son kalan teklifi veren kareyi alır.")
                   ?? Offer(TipId.RoundLimit, rules.RoundLimit > 0,
                       $"Bu oyun {rules.RoundLimit} tur sürecek. Sonunda parası, mülkleri ve binaları en değerli olan kazanır.")
                   ?? Offer(TipId.FirstRoll, myTurn && state.Phase == GamePhase.WaitingForRoll,
                       "Sıra sende: \"Zar At\" düğmesine bas. Bir kareye fareyle gelince ya da tıklayınca o karenin kartı açılır.")
                   ?? Offer(TipId.CanBuild, myTurn && (state.Phase == GamePhase.WaitingForRoll || state.Phase == GamePhase.TurnEnd) && OwnsBuildableGroup(board, state, playerId),
                       "Bir rengin bütün kareleri sende! Artık \"Mülklerin\" listesindeki \"Ev +\" düğmesiyle ev kurabilirsin. Kira çok artar; evleri karelere eşit dağıtmalısın.")
                   ?? Offer(TipId.ShortOfMoney, me.Money < LowMoney && HasMortgageable(board, state, playerId),
                       "Paran azaldı. Sıkışırsan mülklerini ipotek edip bankadan para alabilirsin; sonra faiziyle geri alırsın.")
                   ?? Offer(TipId.Pot, board.HasPot && state.Pot >= 100,
                       $"Çay kumbarasında {state.Pot} birikti. Vergiler ve kart cezaları buraya girer; Çay Molası'na gelen hepsini alır.")
                   ?? Offer(TipId.Trade, myTurn && state.Phase == GamePhase.WaitingForRoll && state.Round >= 3,
                       "Eksik rengini başkası mı tutuyor? \"Takas Teklif Et\" ile para ya da mülk karşılığında iste.");
        }

        private static bool OwnsBuildableGroup(BoardDefinition board, GameState state, int playerId)
        {
            for (int square = 0; square < board.Count; square++)
            {
                if (board[square].IsDevelopable && state.GetOwner(square) == playerId &&
                    GameRules.OwnsWholeGroup(board, state, playerId, board[square].GroupId)) return true;
            }

            return false;
        }

        private static bool HasMortgageable(BoardDefinition board, GameState state, int playerId)
        {
            for (int square = 0; square < board.Count; square++)
            {
                if (state.GetOwner(square) == playerId && !state.IsMortgaged(square)) return true;
            }

            return false;
        }
    }
}
