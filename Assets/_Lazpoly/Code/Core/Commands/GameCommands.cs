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

    /// <summary>
    /// Buys the square the player stands on, and with <see cref="Levels"/> above zero puts that many building levels up on
    /// it in the same step: the level picked on the city card is built at once, the plot never shows on its own, and the
    /// price and the buildings are paid in one payment (the owner's wish).
    /// </summary>
    public sealed class BuyPropertyCommand : GameCommand
    {
        public BuyPropertyCommand(int playerId, int levels = 0) : base(playerId)
        {
            Levels = levels;
        }

        /// <summary>How many building levels go up with the purchase; the rules allow 0 up to the first-lap or full limit.</summary>
        public int Levels { get; }
    }

    public sealed class DeclinePurchaseCommand : GameCommand
    {
        public DeclinePurchaseCommand(int playerId) : base(playerId) { }
    }

    public sealed class EndTurnCommand : GameCommand
    {
        public EndTurnCommand(int playerId) : base(playerId) { }
    }

    /// <summary>Base for commands that act on one board square.</summary>
    public abstract class SquareCommand : GameCommand
    {
        protected SquareCommand(int playerId, int squareIndex) : base(playerId)
        {
            SquareIndex = squareIndex;
        }

        public int SquareIndex { get; }
    }

    /// <summary>
    /// Adds building levels (houses, then the hotel) to a property in one go: the level picked on the property card is
    /// built at once and paid in one payment (the owner's wish).
    /// </summary>
    public sealed class BuildCommand : SquareCommand
    {
        public BuildCommand(int playerId, int squareIndex, int levels = 1) : base(playerId, squareIndex)
        {
            Levels = levels;
        }

        /// <summary>How many levels go up at once; the rules allow 1 up to what the visit allows.</summary>
        public int Levels { get; }
    }

    /// <summary>Sells one building level back to the bank.</summary>
    public sealed class SellBuildingCommand : SquareCommand
    {
        public SellBuildingCommand(int playerId, int squareIndex) : base(playerId, squareIndex) { }
    }

    /// <summary>Sells a property back to the bank for its whole price, its buildings at their sell-back value (all of their cost on the shipped board); it becomes unowned.</summary>
    public sealed class SellPropertyCommand : SquareCommand
    {
        public SellPropertyCommand(int playerId, int squareIndex) : base(playerId, squareIndex) { }
    }

    public sealed class ProposeTradeCommand : GameCommand
    {
        public ProposeTradeCommand(State.TradeOffer offer) : base(offer?.FromPlayerId ?? -1)
        {
            Offer = offer;
        }

        public State.TradeOffer Offer { get; }
    }

    public sealed class AcceptTradeCommand : GameCommand
    {
        public AcceptTradeCommand(int playerId) : base(playerId) { }
    }

    public sealed class RejectTradeCommand : GameCommand
    {
        public RejectTradeCommand(int playerId) : base(playerId) { }
    }

    /// <summary>The proposer withdraws the open offer.</summary>
    public sealed class CancelTradeCommand : GameCommand
    {
        public CancelTradeCommand(int playerId) : base(playerId) { }
    }

    public sealed class PayDebtCommand : GameCommand
    {
        public PayDebtCommand(int playerId) : base(playerId) { }
    }

    /// <summary>Pays the travel square's fee and jumps to <see cref="SquareCommand.SquareIndex"/>.</summary>
    public sealed class TravelCommand : SquareCommand
    {
        public TravelCommand(int playerId, int squareIndex) : base(playerId, squareIndex) { }
    }

    /// <summary>Stays on the travel square.</summary>
    public sealed class DeclineTravelCommand : GameCommand
    {
        public DeclineTravelCommand(int playerId) : base(playerId) { }
    }

    /// <summary>
    /// Pays the cup square's fee and brings the cup to <see cref="SquareCommand.SquareIndex"/>, one of the player's towns (it may
    /// already hold it: it then keeps it at the new, higher raise).
    /// </summary>
    public sealed class HostCupCommand : SquareCommand
    {
        public HostCupCommand(int playerId, int squareIndex) : base(playerId, squareIndex) { }
    }

    /// <summary>Passes on the cup: nothing is paid and no rent changes.</summary>
    public sealed class DeclineCupCommand : GameCommand
    {
        public DeclineCupCommand(int playerId) : base(playerId) { }
    }

    /// <summary>Pays the jail's fee to leave it at once; the player then rolls as usual.</summary>
    public sealed class PayJailFeeCommand : GameCommand
    {
        public PayJailFeeCommand(int playerId) : base(playerId) { }
    }

    /// <summary>
    /// Rolls the dice in jail for free: a double lets the player out to move by it (with no second roll);
    /// any other roll waits out this turn and ends it.
    /// </summary>
    public sealed class RollInJailCommand : GameCommand
    {
        public RollInJailCommand(int playerId) : base(playerId) { }
    }

    /// <summary>Picks one of the two rolls a lookout gave: 0 for the first pair, 1 for the second.</summary>
    public sealed class ChooseDiceCommand : GameCommand
    {
        public ChooseDiceCommand(int playerId, int option) : base(playerId)
        {
            Option = option;
        }

        public int Option { get; }
    }

    /// <summary>Gives up: everything goes to the creditor (or the bank) and the player leaves the game.</summary>
    public sealed class DeclareBankruptcyCommand : GameCommand
    {
        public DeclareBankruptcyCommand(int playerId) : base(playerId) { }
    }

    /// <summary>
    /// Plays a card from the player's hand (Faz 30): <see cref="Card"/> is its deck index, <see cref="Target"/> the square
    /// or player it aims at (-1 when it aims at nothing) and <see cref="Option"/> the choice it offers (the fund: 0 takes the
    /// cash, 1 invests). One card a turn, at the turn's start; the rent shield when rent is due.
    /// </summary>
    public sealed class PlayCardCommand : GameCommand
    {
        public PlayCardCommand(int playerId, int card, int target = -1, int option = 0) : base(playerId)
        {
            Card = card;
            Target = target;
            Option = option;
        }

        public int Card { get; }
        public int Target { get; }
        public int Option { get; }
    }

    /// <summary>Lets a card of a full hand go (the one just drawn or an older one); it goes back into the deck.</summary>
    public sealed class DiscardCardCommand : GameCommand
    {
        public DiscardCardCommand(int playerId, int card) : base(playerId)
        {
            Card = card;
        }

        public int Card { get; }
    }

    /// <summary>Pays the rent that fell due instead of using the rent shield; the shield stays in hand.</summary>
    public sealed class PayRentCommand : GameCommand
    {
        public PayRentCommand(int playerId) : base(playerId) { }
    }

    /// <summary>Takes over the rival's town the player stands on, buildings and all, paying its owner the takeover price (Faz 31).</summary>
    public sealed class TakeOverCommand : GameCommand
    {
        public TakeOverCommand(int playerId) : base(playerId) { }
    }

    /// <summary>Leaves the rival's town with its owner and moves on.</summary>
    public sealed class DeclineTakeoverCommand : GameCommand
    {
        public DeclineTakeoverCommand(int playerId) : base(playerId) { }
    }
}
