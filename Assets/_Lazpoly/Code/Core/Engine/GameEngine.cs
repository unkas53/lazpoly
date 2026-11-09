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

            if (state.CupSquare != GameState.NoCup && !GameRules.CanHoldCup(board, state.CupSquare))
            {
                throw new ArgumentException($"Snapshot puts the cup on square {state.CupSquare}, which is not a town.", nameof(snapshot));
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
                case BuyPropertyCommand c:
                    BuyCurrentSquare(player, c.Levels);
                    break;
                case DeclinePurchaseCommand _:
                    FinishMove();
                    break;
                case EndTurnCommand _:
                    StartNextTurn();
                    break;
                case BuildCommand c:
                    // The levels picked go up at once, paid in one payment.
                    Emit(new MoneyChangedEvent(player.Id, -Board[c.SquareIndex].BuildingCost * c.Levels, MoneyReason.Building));
                    Emit(new BuildingLevelChangedEvent(c.SquareIndex, State.GetBuildingLevel(c.SquareIndex) + c.Levels));
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
                case HostCupCommand c:
                    int cupFee = Board[player.Position].Fee;
                    if (cupFee > 0) Emit(new MoneyChangedEvent(player.Id, -cupFee, MoneyReason.Cup));
                    Emit(new CupHostedEvent(player.Id, c.SquareIndex, GameRules.NextCupPercent(Board, State, player.Position)));
                    FinishMove();
                    break;
                case DeclineCupCommand _:
                    FinishMove();
                    break;
                case PayJailFeeCommand _:
                    Transfer(player.Id, GameRules.FineCollector(Board), Board[Board.JailIndex].Fee, MoneyReason.Jail);
                    Emit(new JailChangedEvent(player.Id, 0));
                    Emit(new PhaseChangedEvent(GamePhase.WaitingForRoll));
                    break;
                case RollInJailCommand _:
                    RollInJail(player);
                    break;
                case PlayCardCommand c:
                    PlayCard(player, c);
                    break;
                case DiscardCardCommand c:
                    Emit(new CardDiscardedEvent(player.Id, c.Card));
                    FinishMove();
                    break;
                case PayRentCommand _:
                {
                    DebtState due = State.RentDue;
                    Emit(new RentDueClearedEvent(player.Id));
                    if (!Charge(player, due.CreditorId, due.Amount, MoneyReason.Rent)) FinishMove();
                    break;
                }
                default:
                    throw new InvalidOperationException($"Validated command {command.GetType().Name} has no handler.");
            }

            List<GameEvent> events = _pending;
            _pending = null;
            return CommandResult.Accepted(events);
        }

        /// <summary>
        /// Records the game time played, as counted where the rules run (the game clock, Faz 28). Not a player's command:
        /// the session calls it every few seconds. Nothing happens once the game is over or when the time has not moved on.
        /// When a table's time limit runs out, the game ends as the turn on play ends (<see cref="StartNextTurn"/>).
        /// </summary>
        public CommandResult AdvanceClock(int playedSeconds)
        {
            if (playedSeconds < 0) throw new ArgumentOutOfRangeException(nameof(playedSeconds));
            if (State.Phase == GamePhase.GameOver || playedSeconds <= State.PlayedSeconds) return CommandResult.Accepted(Array.Empty<GameEvent>());

            _pending = new List<GameEvent>();
            Emit(new GameClockEvent(playedSeconds));
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
                // Too many doubles in a row: straight to jail, or on a board without one the move is forfeited; the turn ends.
                if (Board.HasJail)
                {
                    SendToJail(player);
                }
                else
                {
                    Emit(new PhaseChangedEvent(GamePhase.TurnEnd));
                }

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

            // An investment fund (Faz 30) pays out on the next pass of Start.
            if (passedStart && player.FundPayout > 0)
            {
                Emit(new MoneyChangedEvent(player.Id, player.FundPayout, MoneyReason.Fund));
                Emit(new FundChangedEvent(player.Id, 0));
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

                    if (ownerId == player.Id) return false;
                    int rent = CalculateRent(player.Position);

                    // A rent shield in hand (Faz 30) is offered first, unless the turn's card was already played.
                    if (rent > 0 && !State.CardPlayedThisTurn && GameRules.HeldCard(Board, player, CardKind.RentShield) >= 0)
                    {
                        Emit(new RentDueEvent(player.Id, ownerId, rent));
                        Emit(new PhaseChangedEvent(GamePhase.AwaitingRentShield));
                        return true;
                    }

                    return Charge(player, ownerId, rent, MoneyReason.Rent);

                case SquareType.Tax:
                    return Charge(player, GameRules.FineCollector(Board), GameRules.TaxDue(Board, State, player.Position, player.Id), MoneyReason.Tax);

                case SquareType.Pot:
                    if (State.Pot > 0)
                    {
                        int pot = State.Pot;
                        Emit(new PotChangedEvent(-pot));
                        Emit(new MoneyChangedEvent(player.Id, pot, MoneyReason.Pot));
                    }

                    return false;

                case SquareType.Travel:
                    // The ride waits for the player's next turn (the owner's rule, Faz 27): the turn ends here, even after a
                    // double. Next turn they pick where to go before rolling (StartNextTurn).
                    Emit(new PhaseChangedEvent(GamePhase.TurnEnd));
                    return true;

                case SquareType.Cup:
                    // Offered only to a player with a town to bring it to and the fee to pay.
                    if (!GameRules.CanBringCup(Board, State, player)) return false;
                    Emit(new PhaseChangedEvent(GamePhase.AwaitingCupChoice));
                    return true;

                case SquareType.Lookout:
                    if (!player.HasLookout) Emit(new LookoutChangedEvent(player.Id, true));
                    return false;

                case SquareType.Jail:
                    LockUp(player);
                    return true;

                case SquareType.Card:
                    return drawCards && DrawCard(player);

                default:
                    return false;
            }
        }

        /// <summary>
        /// A free roll in jail: a double sets the player free to move by it, and that double earns no second roll
        /// (<see cref="GameState.RolledOutOfJail"/>); any other roll uses up one of the jailed turns and ends the turn.
        /// </summary>
        private void RollInJail(PlayerState player)
        {
            int die1 = RollDie();
            int die2 = RollDie();
            Emit(new DiceRolledEvent(player.Id, die1, die2));
            if (die1 != die2)
            {
                Emit(new JailChangedEvent(player.Id, player.JailTurns - 1));
                Emit(new PhaseChangedEvent(GamePhase.TurnEnd));
                return;
            }

            Emit(new JailChangedEvent(player.Id, 0, rolledOut: true));
            MovePlayer(player, die1 + die2);
            if (!ResolveLanding(player, drawCards: true)) FinishMove();
        }

        /// <summary>Flies the player straight to the jail (never a lap: no start bonus) and locks them in.</summary>
        private void SendToJail(PlayerState player)
        {
            Emit(new PlayerMovedEvent(player.Id, player.Position, Board.JailIndex, false, MoveKind.Jump));
            LockUp(player);
        }

        /// <summary>Locks the player in jail for the rules' number of turns; their turn ends at once, even after a double.</summary>
        private void LockUp(PlayerState player)
        {
            if (Rules.JailTurns > 0) Emit(new JailChangedEvent(player.Id, Rules.JailTurns));
            Emit(new PhaseChangedEvent(GamePhase.TurnEnd));
        }

        private bool DrawCard(PlayerState player)
        {
            // Cards kept in someone's hand are out of the deck until played (Faz 30).
            List<int> drawable = GameRules.DrawableCards(Board, State);
            if (drawable.Count == 0) return false;
            int index = drawable[_random.NextInt(0, drawable.Count)];
            CardDefinition card = Board.Cards[index];
            if (card.IsKept)
            {
                // Kept for later; a hand past its limit lets one card go before the move goes on.
                Emit(new CardDrawnEvent(player.Id, index, kept: true));
                if (player.Hand.Count <= GameRules.HandLimit) return false;
                Emit(new PhaseChangedEvent(GamePhase.AwaitingCardDiscard));
                return true;
            }

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

                case CardKind.GoToJail:
                    SendToJail(player);
                    return true;

                default:
                    throw new InvalidOperationException($"Card kind {card.Kind} has no handler.");
            }
        }

        /// <summary>
        /// Plays a card from the hand (Faz 30). An attack on a shielded town only takes the shield down; otherwise the card
        /// acts and the turn goes on where it was (before the roll, or in jail), but for the chauffeur, the lawyer and the
        /// rent shield, which move the turn on themselves.
        /// </summary>
        private void PlayCard(PlayerState player, PlayCardCommand command)
        {
            CardDefinition card = Board.Cards[command.Card];
            int target = command.Target;
            Emit(new CardPlayedEvent(player.Id, command.Card, target, command.Option, ride: card.Kind == CardKind.Chauffeur));
            if (card.IsAttack && State.HasShield(target))
            {
                Emit(new ShieldChangedEvent(target, false, blocked: true));
                return;
            }

            switch (card.Kind)
            {
                case CardKind.Lawyer:
                    Emit(new JailChangedEvent(player.Id, 0));
                    Emit(new PhaseChangedEvent(GamePhase.WaitingForRoll));
                    break;

                case CardKind.RentShield:
                    Emit(new RentDueClearedEvent(player.Id));
                    FinishMove();
                    break;

                case CardKind.Chauffeur:
                    // The ride is the turn's move: no roll comes after it (FinishMove).
                    MovePlayer(player, (target - player.Position + Board.Count) % Board.Count);
                    if (!ResolveLanding(player, drawCards: true)) FinishMove();
                    break;

                case CardKind.CupTransfer:
                    if (State.CupSquare == GameState.NoCup)
                    {
                        Emit(new CupHostedEvent(player.Id, target, GameRules.NextCupPercent(Board, State, GameRules.CupCorner(Board))));
                    }
                    else
                    {
                        Emit(new CupHostedEvent(player.Id, target, State.CupPercent, moved: true));
                    }

                    break;

                case CardKind.Lookout:
                    Emit(new LookoutChangedEvent(player.Id, true));
                    break;

                case CardKind.LandDeal:
                    Emit(new MoneyChangedEvent(player.Id, -GameRules.LandDealCost(Board, card, target), MoneyReason.PropertyPurchase));
                    Emit(new PropertyOwnerChangedEvent(target, GameState.NoOwner, player.Id));
                    break;

                case CardKind.Renewal:
                    Emit(new MoneyChangedEvent(player.Id, -GameRules.RenewalCost(Board, card, target), MoneyReason.Building));
                    Emit(new BuildingLevelChangedEvent(target, State.GetBuildingLevel(target) + 1));
                    break;

                case CardKind.Fund:
                    if (command.Option == 0)
                    {
                        Emit(new MoneyChangedEvent(player.Id, card.Amount, MoneyReason.Card));
                    }
                    else
                    {
                        Emit(new MoneyChangedEvent(player.Id, -card.Cost, MoneyReason.Fund));
                        Emit(new FundChangedEvent(player.Id, card.Payout));
                    }

                    break;

                case CardKind.Shield:
                    Emit(new ShieldChangedEvent(target, true));
                    break;

                case CardKind.Strike:
                    Emit(new StrikeChangedEvent(target, player.Id, card.Amount));
                    break;

                case CardKind.Audit:
                    int due = GameRules.AuditDue(Board, State, card, target);
                    if (due > 0) Emit(new MoneyChangedEvent(target, -due, MoneyReason.Audit));
                    break;

                case CardKind.Demolish:
                {
                    int owner = State.GetOwner(target);
                    if (State.GetBuildingLevel(target) > 0) Emit(new BuildingLevelChangedEvent(target, 0));
                    Emit(new PropertyOwnerChangedEvent(target, owner, GameState.NoOwner));
                    break;
                }

                case CardKind.IllegalFloor:
                    Emit(new BuildingLevelChangedEvent(target, State.GetBuildingLevel(target) - 1));
                    break;

                case CardKind.ForceSale:
                {
                    int owner = State.GetOwner(target);
                    int proceeds = GameRules.PropertySaleProceeds(Board, Rules, State, target);
                    if (State.GetBuildingLevel(target) > 0) Emit(new BuildingLevelChangedEvent(target, 0));
                    Emit(new PropertyOwnerChangedEvent(target, owner, GameState.NoOwner));
                    Emit(new MoneyChangedEvent(owner, proceeds, MoneyReason.PropertySale));
                    break;
                }

                default:
                    throw new InvalidOperationException($"Card kind {card.Kind} cannot be played from the hand.");
            }
        }

        /// <summary>
        /// At the start of a turn spent at the bus stop: pays the fare and walks forward to the chosen square as after a roll
        /// (passing Start pays the bonus), then lands there; the turn's roll still comes after it (<see cref="FinishMove"/>).
        /// </summary>
        private void Travel(PlayerState player, int target)
        {
            int fee = Board[player.Position].Fee;
            if (fee > 0) Emit(new MoneyChangedEvent(player.Id, -fee, MoneyReason.Travel));
            MovePlayer(player, (target - player.Position + Board.Count) % Board.Count);
            if (!ResolveLanding(player, drawCards: true)) FinishMove();
        }

        /// <summary>Buys the square, with the levels picked on the card built at once: one payment for all of it.</summary>
        private void BuyCurrentSquare(PlayerState player, int levels)
        {
            int square = player.Position;
            Emit(new MoneyChangedEvent(player.Id, -(int)GameRules.PurchaseCost(Board, square, levels), MoneyReason.PropertyPurchase));
            Emit(new PropertyOwnerChangedEvent(square, GameState.NoOwner, player.Id));
            if (levels > 0) Emit(new BuildingLevelChangedEvent(square, levels));
            FinishMove();
        }

        /// <summary>
        /// After landing is resolved: a double earns another roll (not the one that got the player out of jail), otherwise the
        /// turn can end. A ride taken (or declined) at the start of a turn comes before the turn's roll, so the roll follows it.
        /// </summary>
        private void FinishMove()
        {
            bool notRolledYet = State.LastDie1 == 0 && !State.RodeThisTurn;
            bool again = notRolledYet || (State.LastRollWasDouble && !State.RolledOutOfJail);
            Emit(new PhaseChangedEvent(again ? GamePhase.WaitingForRoll : GamePhase.TurnEnd));
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
        /// The player leaves the game. Everything they own is sold to the bank first (buildings at their
        /// sell-back value, each property at its sale value) and the squares go back to the bank, free to
        /// buy again; then all the money they have goes to the creditor, however short of the debt it is
        /// (the owner's rule, Faz 23: the creditor no longer inherits the properties).
        /// </summary>
        private void GoBankrupt(PlayerState player, int creditorId)
        {
            int buildings = 0;
            int properties = 0;
            for (int square = 0; square < Board.Count; square++)
            {
                if (State.GetOwner(square) != player.Id) continue;
                int level = State.GetBuildingLevel(square);
                if (level > 0)
                {
                    buildings += level * Rules.SellbackValue(Board[square].BuildingCost);
                    Emit(new BuildingLevelChangedEvent(square, 0));
                }

                properties += Board[square].SaleValue;
                Emit(new PropertyOwnerChangedEvent(square, player.Id, GameState.NoOwner));
            }

            if (buildings > 0) Emit(new MoneyChangedEvent(player.Id, buildings, MoneyReason.BuildingSale));
            if (properties > 0) Emit(new MoneyChangedEvent(player.Id, properties, MoneyReason.PropertySale));

            if (player.Money > 0)
            {
                int remaining = player.Money;
                Emit(new MoneyChangedEvent(player.Id, -remaining, MoneyReason.Bankruptcy));
                Credit(creditorId, remaining, MoneyReason.Bankruptcy);
            }

            if (State.Debt != null) Emit(new DebtClearedEvent(player.Id));
            if (State.RentDue != null) Emit(new RentDueClearedEvent(player.Id));

            // Strikes the player called end with them (their towns' marks went with the towns).
            for (int square = 0; square < Board.Count; square++)
            {
                if (State.StrikerOf(square) == player.Id) Emit(new StrikeChangedEvent(square, GameState.NoOwner, 0));
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

            if (State.CurrentPlayer.Id != player.Id) return;

            StartNextTurn();
        }

        private void StartNextTurn()
        {
            // A strike ends when the turn of its player that it lasted to ends (Faz 30).
            int ending = State.CurrentPlayer.Id;
            for (int square = 0; square < Board.Count; square++)
            {
                if (State.StrikerOf(square) == ending && State.StrikeTurnsLeft(square) == 0) Emit(new StrikeChangedEvent(square, GameState.NoOwner, 0));
            }

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

                    // The game time is up: the turn on play has ended, the richest player wins (the owner's rule, Faz 28).
                    if (GameRules.TimeIsUp(Rules, State))
                    {
                        Emit(new GameEndedEvent(GameRules.Richest(Board, State), GameEndReason.TimeLimit));
                        return;
                    }

                    // A jailed player first chooses between paying to leave and waiting; one at the bus stop picks a ride first.
                    Emit(new TurnStartedEvent(next.Id));
                    for (int square = 0; square < Board.Count; square++)
                    {
                        if (State.StrikerOf(square) == next.Id && State.StrikeTurnsLeft(square) > 0)
                        {
                            Emit(new StrikeChangedEvent(square, next.Id, State.StrikeTurnsLeft(square) - 1));
                        }
                    }

                    Emit(new PhaseChangedEvent(next.InJail ? GamePhase.AwaitingJailDecision
                        : GameRules.WaitsForRide(Board, next) ? GamePhase.AwaitingTravelDecision : GamePhase.WaitingForRoll));
                    return;
                }
            }

            throw new InvalidOperationException("No active player left to take a turn.");
        }
    }
}
