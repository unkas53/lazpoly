using Lazpoly.Core.Events;

namespace Lazpoly.Core.State
{
    /// <summary>A payment the current player could not make yet. Immutable.</summary>
    public sealed class DebtState
    {
        public DebtState(int playerId, int creditorId, int amount, MoneyReason reason)
        {
            PlayerId = playerId;
            CreditorId = creditorId;
            Amount = amount;
            Reason = reason;
        }

        public int PlayerId { get; }

        /// <summary>The player owed, or <see cref="GameState.NoOwner"/> for the bank.</summary>
        public int CreditorId { get; }

        public int Amount { get; }
        public MoneyReason Reason { get; }
    }
}
