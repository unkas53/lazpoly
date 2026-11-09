using Lazpoly.Core.Board;
using Lazpoly.Core.Events;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Texts;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Game
{
    /// <summary>
    /// The words around a strategic card (Faz 30): its short name for the hand and the buttons, the card in the middle
    /// when someone plays one ("İMARSIZ YAPI", "Ayşe oynadı → Paris"), and what a blocked attack says. The card stays up
    /// for <see cref="Seconds"/> while the table waits. Pure, so it is tested without a scene.
    /// </summary>
    public static class CardPlay
    {
        public const float Seconds = 3f;

        /// <summary>The card's short name: "Avukat", "İmarsız Yapı"...; "Kart" for a card that is not kept.</summary>
        public static string Name(CardKind kind)
        {
            switch (kind)
            {
                case CardKind.Lookout: return L("Dürbün");
                case CardKind.Lawyer: return L("Avukat");
                case CardKind.RentShield: return L("Kira Kalkanı");
                case CardKind.Chauffeur: return L("Özel Şoför");
                case CardKind.CupTransfer: return L("Kupa Transferi");
                case CardKind.LandDeal: return L("Emlak Fırsatı");
                case CardKind.Renewal: return L("Kentsel Dönüşüm");
                case CardKind.Fund: return L("Yatırım Fonu");
                case CardKind.Shield: return L("Koruma");
                case CardKind.Strike: return L("Kira Grevi");
                case CardKind.Audit: return L("Vergi Denetimi");
                case CardKind.Demolish: return L("İmarsız Yapı");
                case CardKind.IllegalFloor: return L("Kaçak Kat");
                case CardKind.ForceSale: return L("Satmaya Zorla");
                default: return L("Kart");
            }
        }

        public static string Name(BoardDefinition board, int card) =>
            card >= 0 && card < board.Cards.Count ? Name(board.Cards[card].Kind) : "?";

        /// <summary>What the card is played on, for its card and the log: a town's name, a rival's, the fund's choice, or null.</summary>
        public static string TargetText(BoardDefinition board, GameState state, CardPlayedEvent played)
        {
            if (played.CardIndex < 0 || played.CardIndex >= board.Cards.Count) return null;
            CardDefinition card = board.Cards[played.CardIndex];
            if (card.Kind == CardKind.Fund) return played.Option == 1 ? L("yatırım") : L("nakit");
            if (played.Target < 0) return null;
            if (card.Aim == CardTarget.Rival) return EventDescriber.NameOf(state, played.Target);
            return played.Target < board.Count ? L(board[played.Target].Name) : null;
        }

        /// <summary>"Ayşe kart oynadı: İmarsız Yapı → Paris", the log's line.</summary>
        public static string LogLine(BoardDefinition board, GameState state, CardPlayedEvent played)
        {
            string name = EventDescriber.NameOf(state, played.PlayerId);
            string card = Name(board, played.CardIndex);
            string target = TargetText(board, state, played);
            return target == null ? F($"{name} kart oynadı: {card}") : F($"{name} kart oynadı: {card} → {target}");
        }

        /// <summary>The card's title in the middle: its name in capitals.</summary>
        public static string Title(BoardDefinition board, int card) => Upper(Name(board, card));

        /// <summary>The card's first line in the middle: "Ayşe oynadı → Paris", or "Ayşe oynadı".</summary>
        public static string Headline(BoardDefinition board, GameState state, CardPlayedEvent played)
        {
            string name = EventDescriber.NameOf(state, played.PlayerId);
            string target = TargetText(board, state, played);
            return target == null ? F($"{name} oynadı") : F($"{name} oynadı → {target}");
        }

        /// <summary>Under an attack that met a protection card's shield.</summary>
        public static string BlockedText => L("Koruma kalkanı saldırıyı durdurdu; kalkan kalktı.");

        /// <summary>Whether <paramref name="next"/> says the card just played hit a shield.</summary>
        public static bool Blocked(GameEvent next) => next is ShieldChangedEvent shield && shield.Blocked;
    }
}
