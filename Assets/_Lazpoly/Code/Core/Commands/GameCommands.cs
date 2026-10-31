namespace Lazpoly.Core.Commands
{
    /// <summary>
    /// A request from a player (human, remote or AI) to change the game. Commands never change
    /// state themselves: the host validates them and turns accepted ones into events.
    /// </summary>
    public abstract class GameCommand
    {
        protected GameCommand(int playerId)
        {
            PlayerId = playerId;
        }

        public int PlayerId { get; }
    }

    public sealed class RollDiceCommand : GameCommand
    {
        public RollDiceCommand(int playerId) : base(playerId) { }
    }

    public sealed class BuyPropertyCommand : GameCommand
    {
        public BuyPropertyCommand(int playerId) : base(playerId) { }
    }

    public sealed class DeclinePurchaseCommand : GameCommand
    {
        public DeclinePurchaseCommand(int playerId) : base(playerId) { }
    }

    public sealed class EndTurnCommand : GameCommand
    {
        public EndTurnCommand(int playerId) : base(playerId) { }
    }
}
