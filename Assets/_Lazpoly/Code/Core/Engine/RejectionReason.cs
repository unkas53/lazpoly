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
    }
}
