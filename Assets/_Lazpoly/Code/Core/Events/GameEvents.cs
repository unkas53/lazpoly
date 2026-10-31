namespace Lazpoly.Core.Events
{
    /// <summary>
    /// A fact the host has decided. Each event changes one thing, so clients can apply
    /// the same list in the same order and animate it step by step.
    /// </summary>
    public abstract class GameEvent
    {
    }

    public enum MoneyReason
    {
        PassedStart = 0,
        PropertyPurchase = 1,
        Rent = 2,
        Tax = 3,
        Bankruptcy = 4,
    }

    public sealed class TurnStartedEvent : GameEvent
    {
        public TurnStartedEvent(int playerId) { PlayerId = playerId; }
        public int PlayerId { get; }
    }

    public sealed class DiceRolledEvent : GameEvent
    {
        public DiceRolledEvent(int playerId, int die1, int die2)
        {
            PlayerId = playerId;
            Die1 = die1;
            Die2 = die2;
        }

        public int PlayerId { get; }
        public int Die1 { get; }
        public int Die2 { get; }
        public bool IsDouble => Die1 == Die2;
    }

    public sealed class PlayerMovedEvent : GameEvent
    {
        public PlayerMovedEvent(int playerId, int fromSquare, int toSquare, bool passedStart)
        {
            PlayerId = playerId;
            FromSquare = fromSquare;
            ToSquare = toSquare;
            PassedStart = passedStart;
        }

        public int PlayerId { get; }
        public int FromSquare { get; }
        public int ToSquare { get; }
        public bool PassedStart { get; }
    }

    public sealed class MoneyChangedEvent : GameEvent
    {
        public MoneyChangedEvent(int playerId, int delta, MoneyReason reason)
        {
            PlayerId = playerId;
            Delta = delta;
            Reason = reason;
        }

        public int PlayerId { get; }

        /// <summary>Positive when the player receives money, negative when they pay.</summary>
        public int Delta { get; }

        public MoneyReason Reason { get; }
    }

    public sealed class PropertyOwnerChangedEvent : GameEvent
    {
        public PropertyOwnerChangedEvent(int squareIndex, int previousOwnerId, int newOwnerId)
        {
            SquareIndex = squareIndex;
            PreviousOwnerId = previousOwnerId;
            NewOwnerId = newOwnerId;
        }

        public int SquareIndex { get; }

        /// <summary><see cref="State.GameState.NoOwner"/> when bought from the bank.</summary>
        public int PreviousOwnerId { get; }

        /// <summary><see cref="State.GameState.NoOwner"/> when returned to the bank.</summary>
        public int NewOwnerId { get; }
    }

    public sealed class PlayerBankruptedEvent : GameEvent
    {
        public PlayerBankruptedEvent(int playerId, int creditorId)
        {
            PlayerId = playerId;
            CreditorId = creditorId;
        }

        public int PlayerId { get; }

        /// <summary>The player who received the assets, or <see cref="State.GameState.NoOwner"/> for the bank.</summary>
        public int CreditorId { get; }
    }

    public sealed class PhaseChangedEvent : GameEvent
    {
        public PhaseChangedEvent(State.GamePhase phase) { Phase = phase; }
        public State.GamePhase Phase { get; }
    }

    public sealed class GameEndedEvent : GameEvent
    {
        public GameEndedEvent(int winnerId) { WinnerId = winnerId; }
        public int WinnerId { get; }
    }
}
