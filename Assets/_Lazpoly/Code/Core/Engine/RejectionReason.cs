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
    }
}
