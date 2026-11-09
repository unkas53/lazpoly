using System;

namespace Lazpoly.Core.Board
{
    /// <summary>What a card does when it is drawn.</summary>
    public enum CardKind
    {
        /// <summary>The bank pays the player <see cref="CardDefinition.Amount"/>.</summary>
        Collect = 0,

        /// <summary>The player pays <see cref="CardDefinition.Amount"/> into the pot (or to the bank).</summary>
        Pay = 1,

        /// <summary>The player walks forward to <see cref="CardDefinition.Target"/>, collecting the bonus when passing start.</summary>
        MoveTo = 2,

        /// <summary>The player moves <see cref="CardDefinition.Amount"/> squares; negative moves back and never passes start.</summary>
        MoveBy = 3,

        /// <summary>Repairs: <see cref="CardDefinition.Amount"/> per house and <see cref="CardDefinition.HotelAmount"/> per hotel, into the pot.</summary>
        PayPerBuilding = 4,

        /// <summary>
        /// Kept in hand (Faz 30); played at the start of a turn, the player's next roll is a choice of two, as on a lookout
        /// square.
        /// </summary>
        Lookout = 5,

        /// <summary>The player flies straight to the board's jail, without passing start, and is locked in.</summary>
        GoToJail = 6,

        // The strategic cards (the owner's plan, Faz 30). Each is kept in the drawer's hand and played later, one per turn.

        /// <summary>Leaves jail at once, free; the player then rolls as usual.</summary>
        Lawyer = 7,

        /// <summary>Asked when rent falls due: the rent is not paid at all.</summary>
        RentShield = 8,

        /// <summary>Instead of rolling, the player walks to a square they pick (not the jail or the tax square).</summary>
        Chauffeur = 9,

        /// <summary>Moves the champions' cup to one of the player's towns for free, at its current raise.</summary>
        CupTransfer = 10,

        /// <summary>Buys any free town or resort on the board <see cref="CardDefinition.Amount"/> per cent cheaper.</summary>
        LandDeal = 11,

        /// <summary>Puts one building level on one of the player's towns at <see cref="CardDefinition.Amount"/> per cent of its cost.</summary>
        Renewal = 12,

        /// <summary>
        /// A choice: take <see cref="CardDefinition.Amount"/> now, or pay <see cref="CardDefinition.Cost"/> and collect
        /// <see cref="CardDefinition.Payout"/> on the next pass of Start.
        /// </summary>
        Fund = 13,

        /// <summary>Puts a shield on one of the player's towns: it stops the first attack card played on that town.</summary>
        Shield = 14,

        /// <summary>An attack: a rival town collects no rent until the player's <see cref="CardDefinition.Amount"/>th turn after this one ends.</summary>
        Strike = 15,

        /// <summary>An attack on a player: the rival picked pays <see cref="CardDefinition.Amount"/> per cent of their net worth to the bank.</summary>
        Audit = 16,

        /// <summary>An attack: a rival town without a hotel loses its buildings and goes back to the bank, nothing paid back.</summary>
        Demolish = 17,

        /// <summary>An attack: a rival town with houses but no hotel loses one level, nothing paid back.</summary>
        IllegalFloor = 18,

        /// <summary>An attack: a rival town is sold to the bank; its owner gets its whole value.</summary>
        ForceSale = 19,
    }

    /// <summary>What a card played from the hand is aimed at, so screens and computer players know what to pick.</summary>
    public enum CardTarget
    {
        /// <summary>Nothing to pick.</summary>
        None = 0,

        /// <summary>One of the player's own towns.</summary>
        OwnTown = 1,

        /// <summary>A town or resort nobody owns.</summary>
        FreeProperty = 2,

        /// <summary>A town another player owns.</summary>
        RivalTown = 3,

        /// <summary>Another player still in the game.</summary>
        Rival = 4,

        /// <summary>A square of the board.</summary>
        Square = 5,
    }

    /// <summary>One card of the board's deck. Immutable; loaded from data like the squares.</summary>
    public sealed class CardDefinition
    {
        public CardDefinition(string text, CardKind kind, int amount = 0, int target = 0, int hotelAmount = 0, int cost = 0, int payout = 0)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            Kind = kind;
            Amount = amount;
            Target = target;
            HotelAmount = hotelAmount;
            Cost = cost;
            Payout = payout;
        }

        /// <summary>The line shown to the players.</summary>
        public string Text { get; }
        public CardKind Kind { get; }
        public int Amount { get; }

        /// <summary>Destination square of a <see cref="CardKind.MoveTo"/> card.</summary>
        public int Target { get; }

        /// <summary>Cost per hotel on a <see cref="CardKind.PayPerBuilding"/> card.</summary>
        public int HotelAmount { get; }

        /// <summary>What investing costs on a <see cref="CardKind.Fund"/> card.</summary>
        public int Cost { get; }

        /// <summary>What the investment pays on the next pass of Start, on a <see cref="CardKind.Fund"/> card.</summary>
        public int Payout { get; }

        /// <summary>Whether the card goes into the drawer's hand to be played later (Faz 30), rather than acting at once.</summary>
        public bool IsKept => IsKeptKind(Kind);

        /// <summary>Whether cards of <paramref name="kind"/> are kept in hand: the lookout and every strategic card.</summary>
        public static bool IsKeptKind(CardKind kind) => kind == CardKind.Lookout || kind >= CardKind.Lawyer;

        /// <summary>Whether the card hurts another player, so a shield on the town stops it.</summary>
        public bool IsAttack => Kind == CardKind.Strike || Kind == CardKind.Demolish || Kind == CardKind.IllegalFloor || Kind == CardKind.ForceSale;

        /// <summary>What playing the card aims at.</summary>
        public CardTarget Aim
        {
            get
            {
                switch (Kind)
                {
                    case CardKind.Chauffeur: return CardTarget.Square;
                    case CardKind.CupTransfer:
                    case CardKind.Renewal:
                    case CardKind.Shield: return CardTarget.OwnTown;
                    case CardKind.LandDeal: return CardTarget.FreeProperty;
                    case CardKind.Strike:
                    case CardKind.Demolish:
                    case CardKind.IllegalFloor:
                    case CardKind.ForceSale: return CardTarget.RivalTown;
                    case CardKind.Audit: return CardTarget.Rival;
                    default: return CardTarget.None;
                }
            }
        }

        public static CardDefinition Collect(string text, int amount) => new CardDefinition(text, CardKind.Collect, amount);
        public static CardDefinition Pay(string text, int amount) => new CardDefinition(text, CardKind.Pay, amount);
        public static CardDefinition MoveTo(string text, int target) => new CardDefinition(text, CardKind.MoveTo, target: target);
        public static CardDefinition MoveBy(string text, int steps) => new CardDefinition(text, CardKind.MoveBy, steps);
        public static CardDefinition Repairs(string text, int perHouse, int perHotel) =>
            new CardDefinition(text, CardKind.PayPerBuilding, perHouse, hotelAmount: perHotel);
        public static CardDefinition Lookout(string text) => new CardDefinition(text, CardKind.Lookout);
        public static CardDefinition GoToJail(string text) => new CardDefinition(text, CardKind.GoToJail);

        /// <summary>A strategic card (Faz 30); <paramref name="amount"/> is its per cent, turn count or cash as its kind says.</summary>
        public static CardDefinition Strategic(string text, CardKind kind, int amount = 0) => new CardDefinition(text, kind, amount);

        /// <summary>The investment fund card: <paramref name="cash"/> now, or <paramref name="cost"/> now for <paramref name="payout"/> at Start.</summary>
        public static CardDefinition Fund(string text, int cash, int cost, int payout) =>
            new CardDefinition(text, CardKind.Fund, cash, cost: cost, payout: payout);
    }
}
