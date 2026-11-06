namespace Lazpoly.Core.Board
{
    public enum SquareType
    {
        /// <summary>The square every player starts on; passing it pays the pass-start bonus.</summary>
        Start = 0,

        /// <summary>A purchasable square that charges rent.</summary>
        Property = 1,

        /// <summary>Landing pays a fixed amount into the pot (or to the bank when the board has no pot).</summary>
        Tax = 2,

        /// <summary>No effect.</summary>
        Rest = 3,

        /// <summary>Landing draws a card from the board's deck ("Sokak Haberi").</summary>
        Card = 4,

        /// <summary>Landing collects the pot that taxes and card fines have filled ("Çay Molası").</summary>
        Pot = 5,

        /// <summary>Landing offers a ride: pay the square's fee and jump to any other square ("Otogar").</summary>
        Travel = 6,

        /// <summary>Landing lets the player roll twice next time and pick one of the two rolls ("Seyir Terası").</summary>
        Lookout = 7,

        /// <summary>
        /// Landing (or three doubles in a row) locks the player in for <see cref="Rules.RulesConfig.JailTurns"/> of their
        /// turns; on each of them they may pay the square's fee to leave at once ("Hapis").
        /// </summary>
        Jail = 8,
    }
}
