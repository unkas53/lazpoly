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
    /// each command with <see cref="GameRules"/>, decides the resulting events and applies them
    /// through <see cref="GameStateReducer"/>.
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

        private GameEngine(BoardDefinition board, RulesConfig rules, GameState state, IRandomSource random)
        {
            Board = board;
            Rules = rules;
            State = state;
            _random = random;
        }

        /// <summary>Continues a game from a saved snapshot. Throws if the snapshot does not fit the board.</summary>
        public static GameEngine Restore(BoardDefinition board, RulesConfig rules, GameStateSnapshot snapshot, IRandomSource random)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (random == null) throw new ArgumentNullException(nameof(random));

            GameState state = snapshot.Restore();
            if (state.SquareCount != board.Count) throw new ArgumentException("Snapshot was taken on a different board.", nameof(snapshot));
            for (int i = 0; i < board.Count; i++)
            {
                if (state.GetOwner(i) != GameState.NoOwner && board[i].Type != SquareType.Property)
                {
                    throw new ArgumentException($"Snapshot gives square {i} an owner but it is not a property.", nameof(snapshot));
                }
            }

            return new GameEngine(board, rules, state, random);
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
            PlayerState player = State.FindPlayer(command.PlayerId);
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
                case BuildCommand c:
                    Emit(new MoneyChangedEvent(player.Id, -Board[c.SquareIndex].BuildingCost, MoneyReason.Building));
                    Emit(new BuildingLevelChangedEvent(c.SquareIndex, State.GetBuildingLevel(c.SquareIndex) + 1));
                    break;
                case SellBuildingCommand c:
                    Emit(new BuildingLevelChangedEvent(c.SquareIndex, State.GetBuildingLevel(c.SquareIndex) - 1));
                    Emit(new MoneyChangedEvent(player.Id, Rules.SellbackValue(Board[c.SquareIndex].BuildingCost), MoneyReason.BuildingSale));
                    break;
                case SellPropertyCommand c:
                    // Buildings come down first, then the town goes back to the bank; one payment covers all of it.
                    int proceeds = GameRules.PropertySaleProceeds(Board, Rules, State, c.SquareIndex);
                    if (State.GetBuildingLevel(c.SquareIndex) > 0) Emit(new BuildingLevelChangedEvent(c.SquareIndex, 0));
                    Emit(new PropertyOwnerChangedEvent(c.SquareIndex, player.Id, GameState.NoOwner));
                    Emit(new MoneyChangedEvent(player.Id, proceeds, MoneyReason.PropertySale));
                    break;
                case ProposeTradeCommand c:
                    Emit(new TradeProposedEvent(c.Offer));
                    Emit(new PhaseChangedEvent(GamePhase.AwaitingTradeResponse));
                    break;
                case AcceptTradeCommand _:
                    CompleteTrade();
                    break;
                case RejectTradeCommand _:
                    CloseTrade(TradeOutcome.Rejected);
                    break;
                case CancelTradeCommand _:
                    CloseTrade(TradeOutcome.Cancelled);
                    break;
                case PayDebtCommand _:
                    PayDebt(player);
                    break;
                case DeclareBankruptcyCommand _:
                    GoBankrupt(player, State.Debt.CreditorId);
                    break;
                case TravelCommand c:
                    Travel(player, c.SquareIndex);
                    break;
                case DeclineTravelCommand _:
                    FinishMove();
                    break;
                case ChooseDiceCommand c:
                    int[] chosen = GameRules.DiceOption(State, c.Option);
                    MoveWithDice(player, chosen[0], chosen[1]);
                    break;
                default:
                    throw new InvalidOperationException($"Validated command {command.GetType().Name} has no handler.");
            }

            List<GameEvent> events = _pending;
            _pending = null;
            return CommandResult.Accepted(events);
        }

        /// <summary>Checks a command against the current state without changing anything.</summary>
        public RejectionReason Validate(GameCommand command) => GameRules.Validate(Board, Rules, State, command);

        /// <summary>Rent the owner would collect on a property right now.</summary>
        public int CalculateRent(int squareIndex) => GameRules.CalculateRent(Board, Rules, State, squareIndex);

        private void Emit(GameEvent gameEvent)
        {
            GameStateReducer.Apply(State, gameEvent);
            _pending.Add(gameEvent);
        }

        private void RollAndMove(PlayerState player)
        {
            if (player.HasLookout)
            {
                // A lookout: two rolls now, the player keeps one of them (ChooseDiceCommand).
                int a1 = RollDie(), a2 = RollDie(), b1 = RollDie(), b2 = RollDie();
                Emit(new LookoutChangedEvent(player.Id, false));
                Emit(new DiceOptionsEvent(player.Id, a1, a2, b1, b2));
                Emit(new PhaseChangedEvent(GamePhase.AwaitingDiceChoice));
                return;
            }

            int die1 = RollDie();
            int die2 = RollDie();
            MoveWithDice(player, die1, die2);
        }

        private int RollDie() => _random.NextInt(1, Rules.DiceSides + 1);

        private void MoveWithDice(PlayerState player, int die1, int die2)
        {
            Emit(new DiceRolledEvent(player.Id, die1, die2));

            if (State.LastRollWasDouble && State.ConsecutiveDoubles >= Rules.MaxConsecutiveDoubles)
            {
                // Too many doubles in a row: the move is forfeited and the turn ends.
                Emit(new PhaseChangedEvent(GamePhase.TurnEnd));
                return;
            }

            MovePlayer(player, die1 + die2);

            // Landing may open a decision (buy, debt, ride); the move finishes once it is made.
            if (!ResolveLanding(player, drawCards: true)) FinishMove();
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

        private void MovePlayerBack(PlayerState player, int steps)
        {
            int from = player.Position;
            int to = ((from - steps) % Board.Count + Board.Count) % Board.Count;
            Emit(new PlayerMovedEvent(player.Id, from, to, false, MoveKind.Back));
        }

        /// <summary>
        /// Applies the square the player now stands on. Returns true when it left a decision open
        /// (buy, pay a debt, take a ride); the move is then finished by that decision.
        /// </summary>
        /// <param name="drawCards">False after a card moved the player, so one landing draws at most one card.</param>
        private bool ResolveLanding(PlayerState player, bool drawCards)
        {
            SquareDefinition square = Board[player.Position];
            switch (square.Type)
            {
                case SquareType.Property:
                    int ownerId = State.GetOwner(player.Position);
                    if (ownerId == GameState.NoOwner)
                    {
                        // Offered even when the player is short: they may sell something to afford it, or decline into an auction.
                        Emit(new PhaseChangedEvent(GamePhase.AwaitingBuyDecision));
                        return true;
                    }

                    return ownerId != player.Id && Charge(player, ownerId, CalculateRent(player.Position), MoneyReason.Rent);

                case SquareType.Tax:
                    return Charge(player, GameRules.FineCollector(Board), square.TaxAmount, MoneyReason.Tax);

                case SquareType.Pot:
                    if (State.Pot > 0)
                    {
                        int pot = State.Pot;
                        Emit(new PotChangedEvent(-pot));
                        Emit(new MoneyChangedEvent(player.Id, pot, MoneyReason.Pot));
                    }

                    return false;

                case SquareType.Travel:
                    // Offered only when the player can pay the fare.
                    if (player.Money < square.Fee) return false;
                    Emit(new PhaseChangedEvent(GamePhase.AwaitingTravelDecision));
                    return true;

                case SquareType.Lookout:
                    if (!player.HasLookout) Emit(new LookoutChangedEvent(player.Id, true));
                    return false;

                case SquareType.Card:
                    return drawCards && DrawCard(player);

                default:
                    return false;
            }
        }

        private bool DrawCard(PlayerState player)
        {
            int index = _random.NextInt(0, Board.Cards.Count);
            CardDefinition card = Board.Cards[index];
            Emit(new CardDrawnEvent(player.Id, index));

            switch (card.Kind)
            {
                case CardKind.Collect:
                    Emit(new MoneyChangedEvent(player.Id, card.Amount, MoneyReason.Card));
                    return false;

                case CardKind.Pay:
                    return Charge(player, GameRules.FineCollector(Board), card.Amount, MoneyReason.Card);

                case CardKind.MoveTo:
                    MovePlayer(player, (card.Target - player.Position + Board.Count) % Board.Count);
                    return ResolveLanding(player, drawCards: false);

                case CardKind.MoveBy:
                    if (card.Amount > 0) MovePlayer(player, card.Amount);
                    else MovePlayerBack(player, -card.Amount);
                    return ResolveLanding(player, drawCards: false);

                case CardKind.PayPerBuilding:
                    int bill = 0;
                    for (int square = 0; square < Board.Count; square++)
                    {
                        if (State.GetOwner(square) != player.Id) continue;
                        int level = State.GetBuildingLevel(square);
                        bill += level >= SquareDefinition.MaxBuildingLevel ? card.HotelAmount : level * card.Amount;
                    }

                    return Charge(player, GameRules.FineCollector(Board), bill, MoneyReason.Card);

                case CardKind.Lookout:
                    if (!player.HasLookout) Emit(new LookoutChangedEvent(player.Id, true));
                    return false;

                default:
                    throw new InvalidOperationException($"Card kind {card.Kind} has no handler.");
            }
        }

        /// <summary>
        /// Pays the fare and walks forward to the chosen square as after a roll (passing Start pays the
        /// bonus), then lands there.
        /// </summary>
        private void Travel(PlayerState player, int target)
        {
            int fee = Board[player.Position].Fee;
            if (fee > 0) Emit(new MoneyChangedEvent(player.Id, -fee, MoneyReason.Travel));
            MovePlayer(player, (target - player.Position + Board.Count) % Board.Count);
            if (!ResolveLanding(player, drawCards: true)) FinishMove();
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

        /// <summary>
        /// Moves money to the creditor (a player, the pot or the bank), or opens a debt the payer must
        /// settle by selling. A debt larger than everything the payer could raise ends their game at
        /// once: selling would not save them, so nothing is asked of them. Returns true when the move
        /// does not go on (a debt was opened or the payer went bankrupt).
        /// </summary>
        private bool Charge(PlayerState payer, int creditorId, int amount, MoneyReason reason)
        {
            if (amount <= 0) return false;

            if (payer.Money >= amount)
            {
                Transfer(payer.Id, creditorId, amount, reason);
                return false;
            }

            Emit(new DebtIncurredEvent(payer.Id, creditorId, amount, reason));
            if (GameRules.MaxRaisableMoney(Board, Rules, State, payer.Id) < amount)
            {
                GoBankrupt(payer, creditorId);
                return true;
            }

            Emit(new PhaseChangedEvent(GamePhase.AwaitingDebtPayment));
            return true;
        }

        private void Transfer(int payerId, int receiverId, int amount, MoneyReason reason)
        {
            if (amount <= 0) return;
            Emit(new MoneyChangedEvent(payerId, -amount, reason));
            Credit(receiverId, amount, reason);
        }

        /// <summary>Hands money to a player or into the pot; the bank simply keeps it.</summary>
        private void Credit(int receiverId, int amount, MoneyReason reason)
        {
            if (receiverId == GameState.PotId) Emit(new PotChangedEvent(amount));
            else if (GameState.IsPlayerId(receiverId)) Emit(new MoneyChangedEvent(receiverId, amount, reason));
        }

        private void PayDebt(PlayerState player)
        {
            DebtState debt = State.Debt;
            Transfer(player.Id, debt.CreditorId, debt.Amount, debt.Reason);
            Emit(new DebtClearedEvent(player.Id));
            FinishMove();
        }

        private void CompleteTrade()
        {
            TradeOffer offer = State.PendingTrade;
            Transfer(offer.FromPlayerId, offer.ToPlayerId, offer.GiveMoney, MoneyReason.Trade);
            Transfer(offer.ToPlayerId, offer.FromPlayerId, offer.TakeMoney, MoneyReason.Trade);
            foreach (int square in offer.GiveSquares)
            {
                Emit(new PropertyOwnerChangedEvent(square, offer.FromPlayerId, offer.ToPlayerId));
            }

            foreach (int square in offer.TakeSquares)
            {
                Emit(new PropertyOwnerChangedEvent(square, offer.ToPlayerId, offer.FromPlayerId));
            }

            CloseTrade(TradeOutcome.Accepted);
        }

        private void CloseTrade(TradeOutcome outcome)
        {
            GamePhase back = State.TradeReturnPhase;
            Emit(new TradeResolvedEvent(outcome));
            Emit(new PhaseChangedEvent(back));
        }

        /// <summary>
        /// The player leaves the game. Buildings go back to the bank for their sell-back value, then
        /// all money and properties pass to the creditor; a bank creditor takes the properties back.
        /// </summary>
        private void GoBankrupt(PlayerState player, int creditorId)
        {
            int proceeds = 0;
            for (int square = 0; square < Board.Count; square++)
            {
                int level = State.GetBuildingLevel(square);
                if (State.GetOwner(square) != player.Id || level == 0) continue;
                proceeds += level * Rules.SellbackValue(Board[square].BuildingCost);
                Emit(new BuildingLevelChangedEvent(square, 0));
            }

            if (proceeds > 0) Emit(new MoneyChangedEvent(player.Id, proceeds, MoneyReason.BuildingSale));

            if (player.Money > 0)
            {
                int remaining = player.Money;
                Emit(new MoneyChangedEvent(player.Id, -remaining, MoneyReason.Bankruptcy));
                Credit(creditorId, remaining, MoneyReason.Bankruptcy);
            }

            // Properties go to a player creditor; owed to the bank or the pot, they return to the bank.
            int heir = GameState.IsPlayerId(creditorId) ? creditorId : GameState.NoOwner;
            for (int square = 0; square < Board.Count; square++)
            {
                if (State.GetOwner(square) != player.Id) continue;
                Emit(new PropertyOwnerChangedEvent(square, player.Id, heir));
            }

            if (State.Debt != null) Emit(new DebtClearedEvent(player.Id));
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

            if (State.CurrentPlayer.Id != player.Id) return;

            StartNextTurn();
        }

        private void StartNextTurn()
        {
            int count = State.Players.Count;
            for (int step = 1; step <= count; step++)
            {
                int index = (State.CurrentPlayerIndex + step) % count;
                PlayerState next = State.Players[index];
                if (!next.IsBankrupt)
                {
                    // The turn going round to the first seat again would start a round past the table's limit.
                    if (Rules.RoundLimit > 0 && index <= State.CurrentPlayerIndex && State.Round >= Rules.RoundLimit)
                    {
                        Emit(new GameEndedEvent(GameRules.Richest(Board, State), GameEndReason.RoundLimit));
                        return;
                    }

                    Emit(new TurnStartedEvent(next.Id));
                    Emit(new PhaseChangedEvent(GamePhase.WaitingForRoll));
                    return;
                }
            }

            throw new InvalidOperationException("No active player left to take a turn.");
        }
    }
}
