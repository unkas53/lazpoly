using System;
using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Events;
using Lazpoly.Core.Random;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;

namespace Lazpoly.Core.Engine
{
    /// <summary>
    /// The authoritative rules. Runs only on the host (or locally in single player): it validates
    /// each command, decides the resulting events and applies them through <see cref="GameStateReducer"/>.
    /// </summary>
    public sealed class GameEngine
    {
        private readonly IRandomSource _random;
        private List<GameEvent> _pending;

        public GameEngine(BoardDefinition board, RulesConfig rules, IReadOnlyList<PlayerSeat> seats, IRandomSource random)
        {
            Board = board ?? throw new ArgumentNullException(nameof(board));
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            State = GameState.CreateInitial(board, rules, seats);
        }

        public BoardDefinition Board { get; }
        public RulesConfig Rules { get; }
        public GameState State { get; }

        public CommandResult Execute(GameCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            RejectionReason rejection = Validate(command);
            if (rejection != RejectionReason.None)
            {
                return CommandResult.Rejected(rejection);
            }

            _pending = new List<GameEvent>();
            PlayerState player = State.CurrentPlayer;
            switch (command)
            {
                case RollDiceCommand _:
                    RollAndMove(player);
                    break;
                case BuyPropertyCommand _:
                    BuyCurrentSquare(player);
                    break;
                case DeclinePurchaseCommand _:
                    FinishMove();
                    break;
                case EndTurnCommand _:
                    StartNextTurn();
                    break;
            }

            List<GameEvent> events = _pending;
            _pending = null;
            return CommandResult.Accepted(events);
        }

        /// <summary>Checks a command against the current state without changing anything.</summary>
        public RejectionReason Validate(GameCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (State.Phase == GamePhase.GameOver) return RejectionReason.GameOver;
            if (State.FindPlayer(command.PlayerId) == null) return RejectionReason.UnknownPlayer;
            if (State.CurrentPlayer.Id != command.PlayerId) return RejectionReason.NotYourTurn;

            switch (command)
            {
                case RollDiceCommand _:
                    return RequirePhase(GamePhase.WaitingForRoll);
                case BuyPropertyCommand _:
                    RejectionReason phase = RequirePhase(GamePhase.AwaitingBuyDecision);
                    if (phase != RejectionReason.None) return phase;
                    SquareDefinition square = Board[State.CurrentPlayer.Position];
                    return State.CurrentPlayer.Money >= square.Price
                        ? RejectionReason.None
                        : RejectionReason.InsufficientFunds;
                case DeclinePurchaseCommand _:
                    return RequirePhase(GamePhase.AwaitingBuyDecision);
                case EndTurnCommand _:
                    return RequirePhase(GamePhase.TurnEnd);
                default:
                    return RejectionReason.UnknownCommand;
            }
        }

        /// <summary>Rent the owner would collect on a property right now.</summary>
        public int CalculateRent(int squareIndex)
        {
            SquareDefinition square = Board[squareIndex];
            if (square.Type != SquareType.Property) return 0;

            int ownerId = State.GetOwner(squareIndex);
            if (ownerId == GameState.NoOwner) return 0;

            return OwnsWholeGroup(ownerId, square.GroupId)
                ? square.BaseRent * Rules.FullGroupRentMultiplier
                : square.BaseRent;
        }

        private RejectionReason RequirePhase(GamePhase expected) =>
            State.Phase == expected ? RejectionReason.None : RejectionReason.WrongPhase;

        private void Emit(GameEvent gameEvent)
        {
            GameStateReducer.Apply(State, gameEvent);
            _pending.Add(gameEvent);
        }

        private void RollAndMove(PlayerState player)
        {
            int die1 = _random.NextInt(1, Rules.DiceSides + 1);
            int die2 = _random.NextInt(1, Rules.DiceSides + 1);
            Emit(new DiceRolledEvent(player.Id, die1, die2));

            if (State.LastRollWasDouble && State.ConsecutiveDoubles >= Rules.MaxConsecutiveDoubles)
            {
                // Too many doubles in a row: the move is forfeited and the turn ends.
                Emit(new PhaseChangedEvent(GamePhase.TurnEnd));
                return;
            }

            MovePlayer(player, die1 + die2);
            ResolveLanding(player);

            if (player.IsBankrupt || State.Phase == GamePhase.AwaitingBuyDecision || State.Phase == GamePhase.GameOver)
            {
                return;
            }

            FinishMove();
        }

        private void MovePlayer(PlayerState player, int steps)
        {
            int from = player.Position;
            int total = from + steps;
            int to = total % Board.Count;
            bool passedStart = total >= Board.Count;

            Emit(new PlayerMovedEvent(player.Id, from, to, passedStart));
            if (passedStart && Rules.PassStartBonus > 0)
            {
                Emit(new MoneyChangedEvent(player.Id, Rules.PassStartBonus, MoneyReason.PassedStart));
            }
        }

        private void ResolveLanding(PlayerState player)
        {
            SquareDefinition square = Board[player.Position];
            switch (square.Type)
            {
                case SquareType.Property:
                    int ownerId = State.GetOwner(player.Position);
                    if (ownerId == GameState.NoOwner)
                    {
                        if (player.Money >= square.Price)
                        {
                            Emit(new PhaseChangedEvent(GamePhase.AwaitingBuyDecision));
                        }
                    }
                    else if (ownerId != player.Id)
                    {
                        Charge(player, ownerId, CalculateRent(player.Position), MoneyReason.Rent);
                    }

                    break;

                case SquareType.Tax:
                    Charge(player, GameState.NoOwner, square.TaxAmount, MoneyReason.Tax);
                    break;
            }
        }

        private void BuyCurrentSquare(PlayerState player)
        {
            SquareDefinition square = Board[player.Position];
            Emit(new MoneyChangedEvent(player.Id, -square.Price, MoneyReason.PropertyPurchase));
            Emit(new PropertyOwnerChangedEvent(player.Position, GameState.NoOwner, player.Id));
            FinishMove();
        }

        /// <summary>After landing is resolved: a double earns another roll, otherwise the turn can end.</summary>
        private void FinishMove()
        {
            Emit(new PhaseChangedEvent(State.LastRollWasDouble ? GamePhase.WaitingForRoll : GamePhase.TurnEnd));
        }

        /// <summary>Moves money from payer to creditor (or the bank). A payer who cannot pay goes bankrupt.</summary>
        private void Charge(PlayerState payer, int creditorId, int amount, MoneyReason reason)
        {
            if (amount <= 0) return;

            if (payer.Money >= amount)
            {
                Emit(new MoneyChangedEvent(payer.Id, -amount, reason));
                if (creditorId != GameState.NoOwner)
                {
                    Emit(new MoneyChangedEvent(creditorId, amount, reason));
                }

                return;
            }

            DeclareBankrupt(payer, creditorId);
        }

        // Phase 1 rule: a player who cannot pay is bankrupt at once. Selling and mortgaging
        // to raise money arrive with property development in Phase 4.
        private void DeclareBankrupt(PlayerState player, int creditorId)
        {
            int remaining = player.Money;
            if (remaining > 0)
            {
                Emit(new MoneyChangedEvent(player.Id, -remaining, MoneyReason.Bankruptcy));
                if (creditorId != GameState.NoOwner)
                {
                    Emit(new MoneyChangedEvent(creditorId, remaining, MoneyReason.Bankruptcy));
                }
            }

            for (int square = 0; square < Board.Count; square++)
            {
                if (State.GetOwner(square) == player.Id)
                {
                    Emit(new PropertyOwnerChangedEvent(square, player.Id, creditorId));
                }
            }

            Emit(new PlayerBankruptedEvent(player.Id, creditorId));

            if (State.ActivePlayerCount() == 1)
            {
                foreach (PlayerState candidate in State.Players)
                {
                    if (!candidate.IsBankrupt)
                    {
                        Emit(new GameEndedEvent(candidate.Id));
                        return;
                    }
                }
            }

            if (State.CurrentPlayer.Id == player.Id)
            {
                StartNextTurn();
            }
        }

        private void StartNextTurn()
        {
            int count = State.Players.Count;
            for (int step = 1; step <= count; step++)
            {
                PlayerState next = State.Players[(State.CurrentPlayerIndex + step) % count];
                if (!next.IsBankrupt)
                {
                    Emit(new TurnStartedEvent(next.Id));
                    Emit(new PhaseChangedEvent(GamePhase.WaitingForRoll));
                    return;
                }
            }

            throw new InvalidOperationException("No active player left to take a turn.");
        }

        private bool OwnsWholeGroup(int ownerId, int groupId)
        {
            int groupSize = Board.CountInGroup(groupId);
            if (groupSize == 0) return false;

            int owned = 0;
            for (int square = 0; square < Board.Count; square++)
            {
                SquareDefinition definition = Board[square];
                if (definition.Type == SquareType.Property && definition.GroupId == groupId &&
                    State.GetOwner(square) == ownerId)
                {
                    owned++;
                }
            }

            return owned == groupSize;
        }
    }
}
