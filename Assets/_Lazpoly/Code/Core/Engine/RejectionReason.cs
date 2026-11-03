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

        /// <summary>No arrival on this own town this visit, the lap or hotel limit reached, or the town is not developable.</summary>
        CannotBuild = 11,

        /// <summary>Nothing to sell.</summary>
        CannotSell = 12,

        /// <summary>The trade is empty, names squares the sides do not own, or squares with buildings.</summary>
        InvalidTrade = 15,

        /// <summary>A choice that is not one of those offered (for example a third roll).</summary>
        InvalidChoice = 16,
    }
}
