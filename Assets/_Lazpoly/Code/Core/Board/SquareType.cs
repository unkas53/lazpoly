namespace Lazpoly.Core.Board
{
    public enum SquareType
    {
        /// <summary>The square every player starts on; passing it pays the pass-start bonus.</summary>
        Start = 0,

        /// <summary>A purchasable square that charges rent.</summary>
        Property = 1,

        /// <summary>Landing pays a fixed amount to the bank.</summary>
        Tax = 2,

        /// <summary>No effect.</summary>
        Rest = 3,
    }
}
