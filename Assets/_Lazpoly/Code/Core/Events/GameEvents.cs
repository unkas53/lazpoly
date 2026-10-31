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
        Building = 5,
        BuildingSale = 6,
        Mortgage = 7,
        Redeem = 8,
        Auction = 9,
        Trade = 10,
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

    /// <summary>A property's buildings changed (built, sold or cleared by bankruptcy).</summary>
    public sealed class BuildingLevelChangedEvent : GameEvent
    {
        public BuildingLevelChangedEvent(int squareIndex, int level)
        {
            SquareIndex = squareIndex;
            Level = level;
        }

        public int SquareIndex { get; }
        public int Level { get; }
    }

    public sealed class MortgageChangedEvent : GameEvent
    {
        public MortgageChangedEvent(int squareIndex, bool isMortgaged)
        {
            SquareIndex = squareIndex;
            IsMortgaged = isMortgaged;
        }

        public int SquareIndex { get; }
        public bool IsMortgaged { get; }
    }

    public sealed class AuctionStartedEvent : GameEvent
    {
        public AuctionStartedEvent(int squareIndex) { SquareIndex = squareIndex; }
        public int SquareIndex { get; }
    }

    public sealed class BidPlacedEvent : GameEvent
    {
        public BidPlacedEvent(int playerId, int amount)
        {
            PlayerId = playerId;
            Amount = amount;
        }

        public int PlayerId { get; }
        public int Amount { get; }
    }

    public sealed class BidPassedEvent : GameEvent
    {
        public BidPassedEvent(int playerId) { PlayerId = playerId; }
        public int PlayerId { get; }
    }

    /// <summary>The auction closed. Payment and ownership follow as their own events.</summary>
    public sealed class AuctionEndedEvent : GameEvent
    {
        public AuctionEndedEvent(int squareIndex, int winnerId, int price)
        {
            SquareIndex = squareIndex;
            WinnerId = winnerId;
            Price = price;
        }

        public int SquareIndex { get; }

        /// <summary><see cref="State.GameState.NoOwner"/> when nobody bid.</summary>
        public int WinnerId { get; }

        public int Price { get; }
    }

    public sealed class TradeProposedEvent : GameEvent
    {
        public TradeProposedEvent(State.TradeOffer offer) { Offer = offer; }
        public State.TradeOffer Offer { get; }
    }

    public enum TradeOutcome
    {
        Accepted = 1,
        Rejected = 2,
        Cancelled = 3,
    }

    /// <summary>The open trade was answered. On acceptance the transfers precede this event.</summary>
    public sealed class TradeResolvedEvent : GameEvent
    {
        public TradeResolvedEvent(TradeOutcome outcome) { Outcome = outcome; }
        public TradeOutcome Outcome { get; }
    }

    /// <summary>The current player could not pay; the game waits until they pay or go bankrupt.</summary>
    public sealed class DebtIncurredEvent : GameEvent
    {
        public DebtIncurredEvent(int playerId, int creditorId, int amount, MoneyReason reason)
        {
            PlayerId = playerId;
            CreditorId = creditorId;
            Amount = amount;
            Reason = reason;
        }

        public int PlayerId { get; }
        public int CreditorId { get; }
        public int Amount { get; }
        public MoneyReason Reason { get; }
    }

    /// <summary>The open debt is closed, paid or written off by bankruptcy.</summary>
    public sealed class DebtClearedEvent : GameEvent
    {
        public DebtClearedEvent(int playerId) { PlayerId = playerId; }
        public int PlayerId { get; }
    }
}
