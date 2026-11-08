using System;
using System.Collections.Generic;

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
        /// <summary>A property (and its buildings) sold back to the bank.</summary>
        PropertySale = 7,
        Trade = 10,

        /// <summary>A drawn card paid or charged the player.</summary>
        Card = 11,

        /// <summary>The player collected the pot.</summary>
        Pot = 12,

        /// <summary>The fare of a ride from a travel square.</summary>
        Travel = 13,

        /// <summary>The fee for leaving jail early.</summary>
        Jail = 14,

        /// <summary>The fee for bringing the champions' cup to a town.</summary>
        Cup = 15,
    }

    /// <summary>How a pawn gets to its new square, so every screen animates the move the same way.</summary>
    public enum MoveKind
    {
        /// <summary>Forward square by square (dice, or a card sending the player ahead).</summary>
        Walk = 0,

        /// <summary>Backward square by square; never passes start.</summary>
        Back = 1,

        /// <summary>
        /// Straight to the square: a player sent to jail by three doubles in a row (rides used it until they became
        /// walks, Faz 15b).
        /// </summary>
        Jump = 2,
    }

    /// <summary>Why the game ended.</summary>
    public enum GameEndReason
    {
        /// <summary>Everyone else went bankrupt.</summary>
        LastPlayerStanding = 0,

        /// <summary>The table's round limit was reached; the richest player won.</summary>
        RoundLimit = 1,

        /// <summary>The table's game time ran out; the richest player won when the turn on play ended (Faz 28).</summary>
        TimeLimit = 2,
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
        public PlayerMovedEvent(int playerId, int fromSquare, int toSquare, bool passedStart, MoveKind kind = MoveKind.Walk)
        {
            PlayerId = playerId;
            FromSquare = fromSquare;
            ToSquare = toSquare;
            PassedStart = passedStart;
            Kind = kind;
        }

        public int PlayerId { get; }
        public int FromSquare { get; }
        public int ToSquare { get; }
        public bool PassedStart { get; }
        public MoveKind Kind { get; }
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
        public GameEndedEvent(int winnerId, GameEndReason reason = GameEndReason.LastPlayerStanding)
        {
            WinnerId = winnerId;
            Reason = reason;
        }

        public int WinnerId { get; }
        public GameEndReason Reason { get; }
    }

    /// <summary>Money went into the pot (positive) or was paid out of it (negative).</summary>
    public sealed class PotChangedEvent : GameEvent
    {
        public PotChangedEvent(int delta) { Delta = delta; }
        public int Delta { get; }
    }

    /// <summary>A player drew a card; its effect follows as separate events.</summary>
    public sealed class CardDrawnEvent : GameEvent
    {
        public CardDrawnEvent(int playerId, int cardIndex)
        {
            PlayerId = playerId;
            CardIndex = cardIndex;
        }

        public int PlayerId { get; }

        /// <summary>Index into <see cref="Board.BoardDefinition.Cards"/>.</summary>
        public int CardIndex { get; }
    }

    /// <summary>
    /// An online player let their time run out. The host moved for them (the events that follow),
    /// or, when <see cref="StandInTookOver"/>, handed their seat to the computer until they act again.
    /// Announces only; it changes no state.
    /// </summary>
    public sealed class PlayerTimedOutEvent : GameEvent
    {
        public PlayerTimedOutEvent(int playerId, bool standInTookOver)
        {
            PlayerId = playerId;
            StandInTookOver = standInTookOver;
        }

        public int PlayerId { get; }
        public bool StandInTookOver { get; }
    }

    /// <summary>
    /// How much game time has been played, in whole seconds, as counted where the rules run (the game clock, Faz 28);
    /// sent every few seconds so saves and the other machines keep it. A table with a time limit ends when it runs out.
    /// </summary>
    public sealed class GameClockEvent : GameEvent
    {
        public GameClockEvent(int playedSeconds)
        {
            PlayedSeconds = playedSeconds;
        }

        public int PlayedSeconds { get; }
    }

    /// <summary>A player went to jail (<see cref="Turns"/> to wait), waited a turn there, or left (<see cref="Turns"/> 0).</summary>
    public sealed class JailChangedEvent : GameEvent
    {
        public JailChangedEvent(int playerId, int turns, bool rolledOut = false)
        {
            PlayerId = playerId;
            Turns = turns;
            RolledOut = rolledOut;
        }

        public int PlayerId { get; }
        public int Turns { get; }

        /// <summary>The player left jail with a double: they move by it, but it earns no second roll.</summary>
        public bool RolledOut { get; }
    }

    /// <summary>A player gained (or used up) the choice of two rolls next time.</summary>
    public sealed class LookoutChangedEvent : GameEvent
    {
        public LookoutChangedEvent(int playerId, bool hasLookout)
        {
            PlayerId = playerId;
            HasLookout = hasLookout;
        }

        public int PlayerId { get; }
        public bool HasLookout { get; }
    }

    /// <summary>
    /// The cup came to a town: from now on its rent is <see cref="Percent"/> per cent higher, the town that held the cup
    /// before goes back to its own rent, and the game's pick count goes up by one.
    /// </summary>
    public sealed class CupHostedEvent : GameEvent
    {
        public CupHostedEvent(int playerId, int squareIndex, int percent)
        {
            PlayerId = playerId;
            SquareIndex = squareIndex;
            Percent = percent;
        }

        public int PlayerId { get; }
        public int SquareIndex { get; }
        public int Percent { get; }
    }

    /// <summary>A player with a lookout rolled twice; they pick one pair, which then counts as their roll.</summary>
    public sealed class DiceOptionsEvent : GameEvent
    {
        public DiceOptionsEvent(int playerId, int firstDie1, int firstDie2, int secondDie1, int secondDie2)
        {
            PlayerId = playerId;
            FirstDie1 = firstDie1;
            FirstDie2 = firstDie2;
            SecondDie1 = secondDie1;
            SecondDie2 = secondDie2;
        }

        public int PlayerId { get; }
        public int FirstDie1 { get; }
        public int FirstDie2 { get; }
        public int SecondDie1 { get; }
        public int SecondDie2 { get; }
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
