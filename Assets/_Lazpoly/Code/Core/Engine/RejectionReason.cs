namespace Lazpoly.Core.Engine
{
    /// <summary>Why the host refused a command. Sent back to the player who issued it.</summary>
    public enum RejectionReason
    {
        None = 0,
        GameOver = 1,
        UnknownPlayer = 2,
        NotYourTurn = 3,
        WrongPhase = 4,
        InsufficientFunds = 5,
        UnknownCommand = 6,

        /// <summary>A network peer sent a command for a player that is not theirs.</summary>
        NotYourPlayer = 7,

        /// <summary>The command could not be delivered or the session is not running.</summary>
        NotConnected = 8,

        /// <summary>The square is not a property, or not one this action applies to.</summary>
        InvalidSquare = 9,

        /// <summary>The square belongs to someone else.</summary>
        NotOwner = 10,

        /// <summary>Building needs the whole group, unmortgaged, and even building across it.</summary>
        CannotBuild = 11,

        /// <summary>Nothing to sell, or selling would break even building.</summary>
        CannotSell = 12,

        /// <summary>Already mortgaged, not mortgaged, or the group still has buildings.</summary>
        CannotMortgage = 13,

        /// <summary>Bid too low, above the bidder's money, or the bidder already passed or leads.</summary>
        InvalidBid = 14,

        /// <summary>The trade is empty, names squares the sides do not own, or squares with buildings.</summary>
        InvalidTrade = 15,

        /// <summary>A choice that is not one of those offered (for example a third roll).</summary>
        InvalidChoice = 16,
    }
}
