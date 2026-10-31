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

        /// <summary>The player's next roll is a choice of two, as on a lookout square.</summary>
        Lookout = 5,
    }

    /// <summary>One card of the board's deck. Immutable; loaded from data like the squares.</summary>
    public sealed class CardDefinition
    {
        public CardDefinition(string text, CardKind kind, int amount = 0, int target = 0, int hotelAmount = 0)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            Kind = kind;
            Amount = amount;
            Target = target;
            HotelAmount = hotelAmount;
        }

        /// <summary>The line shown to the players.</summary>
        public string Text { get; }
        public CardKind Kind { get; }
        public int Amount { get; }

        /// <summary>Destination square of a <see cref="CardKind.MoveTo"/> card.</summary>
        public int Target { get; }

        /// <summary>Cost per hotel on a <see cref="CardKind.PayPerBuilding"/> card.</summary>
        public int HotelAmount { get; }

        public static CardDefinition Collect(string text, int amount) => new CardDefinition(text, CardKind.Collect, amount);
        public static CardDefinition Pay(string text, int amount) => new CardDefinition(text, CardKind.Pay, amount);
        public static CardDefinition MoveTo(string text, int target) => new CardDefinition(text, CardKind.MoveTo, target: target);
        public static CardDefinition MoveBy(string text, int steps) => new CardDefinition(text, CardKind.MoveBy, steps);
        public static CardDefinition Repairs(string text, int perHouse, int perHotel) =>
            new CardDefinition(text, CardKind.PayPerBuilding, perHouse, hotelAmount: perHotel);
        public static CardDefinition Lookout(string text) => new CardDefinition(text, CardKind.Lookout);
    }
}
