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
                    StartAuction(player.Position);
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
                case MortgageCommand c:
                    Emit(new MortgageChangedEvent(c.SquareIndex, true));
                    Emit(new MoneyChangedEvent(player.Id, Board[c.SquareIndex].MortgageValue, MoneyReason.Mortgage));
                    break;
                case RedeemCommand c:
                    Emit(new MoneyChangedEvent(player.Id, -Rules.RedeemCost(Board[c.SquareIndex].MortgageValue), MoneyReason.Redeem));
                    Emit(new MortgageChangedEvent(c.SquareIndex, false));
                    break;
                case PlaceBidCommand c:
                    Emit(new BidPlacedEvent(player.Id, c.Amount));
                    CloseAuctionIfDecided();
                    break;
                case PassBidCommand _:
                    Emit(new BidPassedEvent(player.Id));
                    CloseAuctionIfDecided();
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

            // Landing may have opened a decision (buy, debt); the move finishes once it is made.
            if (State.Phase == GamePhase.WaitingForRoll) FinishMove();
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
                        // Offered even when the player is short: they may mortgage to afford it, or decline into an auction.
                        Emit(new PhaseChangedEvent(GamePhase.AwaitingBuyDecision));
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

        /// <summary>Moves money to the creditor (or the bank), or opens a debt the payer must settle.</summary>
        private void Charge(PlayerState payer, int creditorId, int amount, MoneyReason reason)
        {
            if (amount <= 0) return;

            if (payer.Money >= amount)
            {
                Transfer(payer.Id, creditorId, amount, reason);
                return;
            }

            Emit(new DebtIncurredEvent(payer.Id, creditorId, amount, reason));
            Emit(new PhaseChangedEvent(GamePhase.AwaitingDebtPayment));
        }

        private void Transfer(int payerId, int receiverId, int amount, MoneyReason reason)
        {
            if (amount <= 0) return;
            Emit(new MoneyChangedEvent(payerId, -amount, reason));
            if (receiverId != GameState.NoOwner) Emit(new MoneyChangedEvent(receiverId, amount, reason));
        }

        private void PayDebt(PlayerState player)
        {
            DebtState debt = State.Debt;
            Transfer(player.Id, debt.CreditorId, debt.Amount, debt.Reason);
            Emit(new DebtClearedEvent(player.Id));
            FinishMove();
        }

        private void StartAuction(int squareIndex)
        {
            Emit(new AuctionStartedEvent(squareIndex));
            Emit(new PhaseChangedEvent(GamePhase.AuctionInProgress));
            CloseAuctionIfDecided();
        }

        /// <summary>
        /// Ends the auction once it is decided: the leader is the only one left, or everyone passed
        /// without a bid (the property then stays with the bank).
        /// </summary>
        private void CloseAuctionIfDecided()
        {
            AuctionState auction = State.Auction;
            int stillIn = 0;
            bool othersStillIn = false;
            foreach (PlayerState candidate in State.Players)
            {
                if (candidate.IsBankrupt || auction.HasPassed(candidate.Id)) continue;
                stillIn++;
                if (candidate.Id != auction.HighestBidderId) othersStillIn = true;
            }

            bool hasLeader = auction.HighestBidderId != GameState.NoOwner;
            if (hasLeader ? othersStillIn : stillIn > 0) return;

            int square = auction.SquareIndex;
            int winner = auction.HighestBidderId;
            int price = auction.HighestBid;
            Emit(new AuctionEndedEvent(square, winner, price));
            if (hasLeader)
            {
                Emit(new MoneyChangedEvent(winner, -price, MoneyReason.Auction));
                Emit(new PropertyOwnerChangedEvent(square, GameState.NoOwner, winner));
            }

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
        /// all money and properties pass to the creditor; a bank creditor takes the properties back
        /// free of mortgages.
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
                if (creditorId != GameState.NoOwner) Emit(new MoneyChangedEvent(creditorId, remaining, MoneyReason.Bankruptcy));
            }

            for (int square = 0; square < Board.Count; square++)
            {
                if (State.GetOwner(square) != player.Id) continue;
                if (creditorId == GameState.NoOwner && State.IsMortgaged(square)) Emit(new MortgageChangedEvent(square, false));
                Emit(new PropertyOwnerChangedEvent(square, player.Id, creditorId));
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
    }
}
