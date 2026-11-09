using System;
using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;

namespace Lazpoly.Core.Engine
{
    /// <summary>
    /// Every rule that decides whether a command is allowed, as pure functions of the state.
    /// The host's engine uses it before acting; clients use it to show only the actions that
    /// would be accepted. Changes nothing.
    /// </summary>
    public static class GameRules
    {
        public static RejectionReason Validate(BoardDefinition board, RulesConfig rules, GameState state, GameCommand command)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (command == null) throw new ArgumentNullException(nameof(command));

            if (state.Phase == GamePhase.GameOver) return RejectionReason.GameOver;
            PlayerState player = state.FindPlayer(command.PlayerId);
            if (player == null) return RejectionReason.UnknownPlayer;
            if (player.IsBankrupt) return RejectionReason.NotYourTurn;

            switch (command)
            {
                case RollDiceCommand _:
                    return OnTurn(state, player, GamePhase.WaitingForRoll);

                case BuyPropertyCommand c:
                {
                    RejectionReason turn = OnTurn(state, player, GamePhase.AwaitingBuyDecision);
                    if (turn != RejectionReason.None) return turn;
                    if (c.Levels < 0 || c.Levels > BuildableAfterPurchase(board, player, player.Position)) return RejectionReason.CannotBuild;
                    return player.Money >= PurchaseCost(board, player.Position, c.Levels) ? RejectionReason.None : RejectionReason.InsufficientFunds;
                }

                case DeclinePurchaseCommand _:
                    return OnTurn(state, player, GamePhase.AwaitingBuyDecision);

                case EndTurnCommand _:
                    return OnTurn(state, player, GamePhase.TurnEnd);

                case BuildCommand c:
                    return OnTurn(state, player, GamePhase.WaitingForRoll, GamePhase.TurnEnd)
                        .Then(() => CanBuild(board, state, player, c.SquareIndex, c.Levels));

                case SellBuildingCommand c:
                    return OnTurn(state, player, GamePhase.WaitingForRoll, GamePhase.TurnEnd, GamePhase.AwaitingBuyDecision,
                            GamePhase.AwaitingDebtPayment, GamePhase.AwaitingTravelDecision)
                        .Then(() => CanSell(board, state, player, c.SquareIndex));

                case SellPropertyCommand c:
                    // A property goes back to the bank only to raise money for a payment the player cannot make
                    // (rent above all); otherwise towns are kept, built on or traded.
                    return OnTurn(state, player, GamePhase.AwaitingDebtPayment)
                        .Then(() => RequireOwnProperty(board, state, player, c.SquareIndex));

                case ProposeTradeCommand c:
                    if (c.Offer == null) return RejectionReason.InvalidTrade;
                    return OnTurn(state, player, GamePhase.WaitingForRoll, GamePhase.TurnEnd)
                        .Then(() => ValidateTrade(board, state, c.Offer));

                case AcceptTradeCommand _:
                    if (state.Phase != GamePhase.AwaitingTradeResponse) return RejectionReason.WrongPhase;
                    if (state.PendingTrade.ToPlayerId != player.Id) return RejectionReason.NotYourTurn;
                    return ValidateTrade(board, state, state.PendingTrade);

                case RejectTradeCommand _:
                    if (state.Phase != GamePhase.AwaitingTradeResponse) return RejectionReason.WrongPhase;
                    return state.PendingTrade.ToPlayerId == player.Id ? RejectionReason.None : RejectionReason.NotYourTurn;

                case CancelTradeCommand _:
                    if (state.Phase != GamePhase.AwaitingTradeResponse) return RejectionReason.WrongPhase;
                    return state.PendingTrade.FromPlayerId == player.Id ? RejectionReason.None : RejectionReason.NotYourTurn;

                case PayDebtCommand _:
                {
                    RejectionReason turn = OnTurn(state, player, GamePhase.AwaitingDebtPayment);
                    if (turn != RejectionReason.None) return turn;
                    return player.Money >= state.Debt.Amount ? RejectionReason.None : RejectionReason.InsufficientFunds;
                }

                case DeclareBankruptcyCommand _:
                    return OnTurn(state, player, GamePhase.AwaitingDebtPayment);

                case TravelCommand c:
                {
                    RejectionReason turn = OnTurn(state, player, GamePhase.AwaitingTravelDecision);
                    if (turn != RejectionReason.None) return turn;
                    if (!IsRideDestination(board, state, player, c.SquareIndex)) return RejectionReason.InvalidSquare;
                    return player.Money >= board[player.Position].Fee ? RejectionReason.None : RejectionReason.InsufficientFunds;
                }

                case DeclineTravelCommand _:
                    return OnTurn(state, player, GamePhase.AwaitingTravelDecision);

                case HostCupCommand c:
                {
                    RejectionReason turn = OnTurn(state, player, GamePhase.AwaitingCupChoice);
                    if (turn != RejectionReason.None) return turn;
                    RejectionReason owned = RequireOwnProperty(board, state, player, c.SquareIndex);
                    if (owned != RejectionReason.None) return owned;
                    if (!CanHoldCup(board, c.SquareIndex)) return RejectionReason.InvalidSquare;
                    return player.Money >= board[player.Position].Fee ? RejectionReason.None : RejectionReason.InsufficientFunds;
                }

                case DeclineCupCommand _:
                    return OnTurn(state, player, GamePhase.AwaitingCupChoice);

                case PayJailFeeCommand _:
                {
                    RejectionReason turn = OnTurn(state, player, GamePhase.AwaitingJailDecision);
                    if (turn != RejectionReason.None) return turn;
                    return player.Money >= JailFee(board) ? RejectionReason.None : RejectionReason.InsufficientFunds;
                }

                case RollInJailCommand _:
                    return OnTurn(state, player, GamePhase.AwaitingJailDecision);

                case ChooseDiceCommand c:
                {
                    RejectionReason turn = OnTurn(state, player, GamePhase.AwaitingDiceChoice);
                    if (turn != RejectionReason.None) return turn;
                    return c.Option == 0 || c.Option == 1 ? RejectionReason.None : RejectionReason.InvalidChoice;
                }

                case PlayCardCommand c:
                    return CanPlayCard(board, state, player, c.Card, c.Target, c.Option);

                case DiscardCardCommand c:
                {
                    RejectionReason turn = OnTurn(state, player, GamePhase.AwaitingCardDiscard);
                    if (turn != RejectionReason.None) return turn;
                    return Holds(player, c.Card) ? RejectionReason.None : RejectionReason.CardNotHeld;
                }

                case PayRentCommand _:
                    return OnTurn(state, player, GamePhase.AwaitingRentShield);

                case TakeOverCommand _:
                    return OnTurn(state, player, GamePhase.AwaitingTakeoverDecision)
                        .Then(() => CanTakeOver(board, state, player) ? RejectionReason.None : RejectionReason.InvalidSquare);

                case DeclineTakeoverCommand _:
                    return OnTurn(state, player, GamePhase.AwaitingTakeoverDecision);

                default:
                    return RejectionReason.UnknownCommand;
            }
        }

        /// <summary>How many cards a player may keep in hand (the owner's rule, Faz 30); one more drawn means letting one go.</summary>
        public const int HandLimit = 4;

        /// <summary>The first card of <paramref name="kind"/> in the player's hand, or -1.</summary>
        public static int HeldCard(BoardDefinition board, PlayerState player, CardKind kind)
        {
            if (player == null) return -1;
            foreach (int card in player.Hand)
            {
                if (card >= 0 && card < board.Cards.Count && board.Cards[card].Kind == kind) return card;
            }

            return -1;
        }

        /// <summary>The square of the board's champions' cup, or -1.</summary>
        public static int CupCorner(BoardDefinition board)
        {
            for (int i = 0; i < board.Count; i++)
            {
                if (board[i].Type == SquareType.Cup) return i;
            }

            return -1;
        }

        private static bool Holds(PlayerState player, int card) => card >= 0 && player.HandCards.Contains(card);

        /// <summary>
        /// The cards a card square may give now: every card of the deck but those kept in someone's hand, which come back
        /// only once played or let go (Faz 30).
        /// </summary>
        public static List<int> DrawableCards(BoardDefinition board, GameState state)
        {
            var held = new HashSet<int>();
            foreach (PlayerState player in state.Players) held.UnionWith(player.Hand);
            var cards = new List<int>(board.Cards.Count);
            for (int i = 0; i < board.Cards.Count; i++)
            {
                if (!held.Contains(i)) cards.Add(i);
            }

            return cards;
        }

        /// <summary>
        /// Whether the player may play card <paramref name="card"/> of their hand now at <paramref name="target"/> with
        /// <paramref name="option"/> (Faz 30). One card a turn, on the player's own turn: before the roll (in jail too, but
        /// for the chauffeur and the lookout, which are about moving), the lawyer only in jail, the rent shield only when rent
        /// waits on it.
        /// </summary>
        public static RejectionReason CanPlayCard(BoardDefinition board, GameState state, PlayerState player, int card, int target, int option)
        {
            if (state.CurrentPlayer.Id != player.Id) return RejectionReason.NotYourTurn;
            if (!Holds(player, card) || card >= board.Cards.Count || !board.Cards[card].IsKept) return RejectionReason.CardNotHeld;
            if (state.CardPlayedThisTurn) return RejectionReason.CardAlreadyPlayed;

            CardDefinition definition = board.Cards[card];
            bool beforeRoll = state.Phase == GamePhase.WaitingForRoll && state.LastDie1 == 0 && !state.RodeThisTurn;
            bool inJail = state.Phase == GamePhase.AwaitingJailDecision;
            switch (definition.Kind)
            {
                case CardKind.RentShield:
                    if (state.Phase != GamePhase.AwaitingRentShield) return RejectionReason.WrongPhase;
                    break;
                case CardKind.Lawyer:
                    if (!inJail) return RejectionReason.WrongPhase;
                    break;
                case CardKind.Chauffeur:
                case CardKind.Lookout:
                    if (!beforeRoll) return RejectionReason.WrongPhase;
                    break;
                default:
                    if (!beforeRoll && !inJail) return RejectionReason.WrongPhase;
                    break;
            }

            return CardTargetAllowed(board, state, player, definition, target, option);
        }

        /// <summary>Whether <paramref name="target"/> and <paramref name="option"/> fit the card, money included; the phase aside.</summary>
        public static RejectionReason CardTargetAllowed(BoardDefinition board, GameState state, PlayerState player, CardDefinition card, int target, int option)
        {
            if (card.Kind == CardKind.Fund)
            {
                if (option == 0) return RejectionReason.None;
                if (option != 1 || player.FundPayout > 0) return RejectionReason.InvalidChoice;
                return player.Money >= card.Cost ? RejectionReason.None : RejectionReason.InsufficientFunds;
            }

            if (option != 0) return RejectionReason.InvalidChoice;
            switch (card.Aim)
            {
                case CardTarget.None:
                    if (target != -1) return RejectionReason.InvalidTarget;
                    return card.Kind == CardKind.Lookout && player.HasLookout ? RejectionReason.InvalidChoice : RejectionReason.None;

                case CardTarget.Square:
                    if (target < 0 || target >= board.Count || target == player.Position) return RejectionReason.InvalidTarget;
                    return board[target].Type == SquareType.Jail || board[target].Type == SquareType.Tax ? RejectionReason.InvalidTarget : RejectionReason.None;

                case CardTarget.Rival:
                {
                    PlayerState rival = state.FindPlayer(target);
                    return rival == null || rival.Id == player.Id || rival.IsBankrupt ? RejectionReason.InvalidTarget : RejectionReason.None;
                }

                case CardTarget.FreeProperty:
                    if (target < 0 || target >= board.Count || board[target].Type != SquareType.Property || state.GetOwner(target) != GameState.NoOwner)
                    {
                        return RejectionReason.InvalidTarget;
                    }

                    return player.Money >= LandDealCost(board, card, target) ? RejectionReason.None : RejectionReason.InsufficientFunds;

                case CardTarget.OwnTown:
                    if (!CanHoldCup(board, target) || state.GetOwner(target) != player.Id) return RejectionReason.InvalidTarget;
                    switch (card.Kind)
                    {
                        case CardKind.CupTransfer:
                            return target == state.CupSquare ? RejectionReason.InvalidTarget : RejectionReason.None;
                        case CardKind.Shield:
                            return state.HasShield(target) ? RejectionReason.InvalidTarget : RejectionReason.None;
                        case CardKind.Renewal:
                            if (!CanRenew(board, state, player, target)) return RejectionReason.CannotBuild;
                            return player.Money >= RenewalCost(board, card, target) ? RejectionReason.None : RejectionReason.InsufficientFunds;
                        default:
                            return RejectionReason.None;
                    }

                case CardTarget.RivalTown:
                {
                    if (!CanHoldCup(board, target)) return RejectionReason.InvalidTarget;
                    int owner = state.GetOwner(target);
                    if (owner == GameState.NoOwner || owner == player.Id) return RejectionReason.InvalidTarget;
                    int level = state.GetBuildingLevel(target);
                    switch (card.Kind)
                    {
                        case CardKind.Strike:
                            return state.IsStruck(target) ? RejectionReason.InvalidTarget : RejectionReason.None;
                        case CardKind.Demolish:
                            return level >= SquareDefinition.MaxBuildingLevel ? RejectionReason.InvalidTarget : RejectionReason.None;
                        case CardKind.IllegalFloor:
                            return level < 1 || level >= SquareDefinition.MaxBuildingLevel ? RejectionReason.InvalidTarget : RejectionReason.None;
                        default:
                            return RejectionReason.None;
                    }
                }

                default:
                    return RejectionReason.InvalidTarget;
            }
        }

        /// <summary>
        /// Every target the card may be played at now, money included (squares or player ids), for the screens and the
        /// computer players; a card that aims at nothing gives -1 when it may be played. Empty when it cannot be played.
        /// </summary>
        public static List<int> CardTargets(BoardDefinition board, GameState state, PlayerState player, int card, int option = 0)
        {
            var targets = new List<int>();
            if (player == null || card < 0 || card >= board.Cards.Count) return targets;
            CardDefinition definition = board.Cards[card];
            switch (definition.Aim)
            {
                case CardTarget.None:
                    if (CanPlayCard(board, state, player, card, -1, option) == RejectionReason.None) targets.Add(-1);
                    break;
                case CardTarget.Rival:
                    foreach (PlayerState rival in state.Players)
                    {
                        if (CanPlayCard(board, state, player, card, rival.Id, option) == RejectionReason.None) targets.Add(rival.Id);
                    }

                    break;
                default:
                    for (int square = 0; square < board.Count; square++)
                    {
                        if (CanPlayCard(board, state, player, card, square, option) == RejectionReason.None) targets.Add(square);
                    }

                    break;
            }

            return targets;
        }

        /// <summary>What a land deal card makes a free town or resort cost: its price less the card's per cent (rounded down).</summary>
        public static int LandDealCost(BoardDefinition board, CardDefinition card, int squareIndex) =>
            (int)((long)board[squareIndex].Price * (100 - card.Amount) / 100);

        /// <summary>What a renewal card makes the next building level of a town cost: the card's per cent of its cost.</summary>
        public static int RenewalCost(BoardDefinition board, CardDefinition card, int squareIndex) =>
            (int)((long)board[squareIndex].BuildingCost * card.Amount / 100);

        /// <summary>
        /// Whether a renewal card may add a level to the player's town wherever they stand: the usual limits hold (one house
        /// until the first pass of Start, the hotel only on four houses), only the visit is not needed.
        /// </summary>
        public static bool CanRenew(BoardDefinition board, GameState state, PlayerState player, int squareIndex)
        {
            if (!CanHoldCup(board, squareIndex) || !board[squareIndex].IsDevelopable || state.GetOwner(squareIndex) != player.Id) return false;
            int level = state.GetBuildingLevel(squareIndex);
            if (level >= SquareDefinition.MaxBuildingLevel) return false;
            return level == MaxHouses || level < (player.Laps == 0 ? FirstLapHouseLimit : MaxHouses);
        }

        /// <summary>What a tax audit card takes from a player: the card's per cent of their net worth, at most the cash they hold.</summary>
        public static int AuditDue(BoardDefinition board, GameState state, CardDefinition card, int playerId)
        {
            PlayerState player = state.FindPlayer(playerId);
            if (player == null || player.IsBankrupt) return 0;
            long due = (long)NetWorth(board, state, playerId) * card.Amount / 100;
            return (int)Math.Max(0, Math.Min(due, player.Money));
        }

        /// <summary>Rent the owner would collect on a property right now, the cup's raise included (rounded down).</summary>
        public static int CalculateRent(BoardDefinition board, RulesConfig rules, GameState state, int squareIndex)
        {
            return WithCup(state, squareIndex, RentWithoutCup(board, rules, state, squareIndex));
        }

        /// <summary><paramref name="rent"/> with the cup's raise when the cup stands on the square (rounded down).</summary>
        public static int WithCup(GameState state, int squareIndex, int rent) =>
            squareIndex == state.CupSquare ? rent * (100 + state.CupPercent) / 100 : rent;

        /// <summary>A town's printed rent at a building level: the land's at 0, then each house's and the hotel's.</summary>
        public static int LevelRent(SquareDefinition square, int level) =>
            level > 0 ? square.DevelopedRents[level - 1] : square.BaseRent;

        /// <summary>Rent the owner would collect on a property right now if the cup were not there.</summary>
        public static int RentWithoutCup(BoardDefinition board, RulesConfig rules, GameState state, int squareIndex)
        {
            SquareDefinition square = board[squareIndex];
            if (square.Type != SquareType.Property) return 0;

            int ownerId = state.GetOwner(squareIndex);
            if (ownerId == GameState.NoOwner) return 0;

            // A strike card (Faz 30): the town collects nothing while it lasts.
            if (state.IsStruck(squareIndex)) return 0;

            if (square.IsResort) return ResortRent(square.BaseRent, ResortsOwned(board, state, ownerId));

            // The whole colour group in one hand multiplies the rent at every level, houses and hotel too (the owner's wish, Faz 26).
            int rent = LevelRent(square, state.GetBuildingLevel(squareIndex));
            return OwnsWholeGroup(board, state, ownerId, square.GroupId) ? rent * rules.FullGroupRentMultiplier : rent;
        }

        /// <summary>Whether the champions' cup may stand on a square: towns only, never a holiday resort.</summary>
        public static bool CanHoldCup(BoardDefinition board, int squareIndex) =>
            squareIndex >= 0 && squareIndex < board.Count && board[squareIndex].Type == SquareType.Property && !board[squareIndex].IsResort;

        /// <summary>
        /// The raise, in percent, the next pick on the cup square at <paramref name="cupSquare"/> gives its town: the square's
        /// first raise plus its step for every pick made so far in the game, whoever made it (50, 100, 150... on the shipped
        /// board, the owner's rule, Faz 29), up to its limit (500). 0 off a cup square.
        /// </summary>
        public static int NextCupPercent(BoardDefinition board, GameState state, int cupSquare)
        {
            SquareDefinition square = board[cupSquare];
            if (square.Type != SquareType.Cup) return 0;
            long percent = square.BoostPercent + (long)square.BoostStep * state.CupPicks;
            if (square.BoostLimit > 0 && percent > square.BoostLimit) percent = square.BoostLimit;
            return (int)Math.Min(percent, int.MaxValue);
        }

        /// <summary>Whether a player on the cup square is offered it: they own a town to bring it to and can pay the fee.</summary>
        public static bool CanBringCup(BoardDefinition board, GameState state, PlayerState player)
        {
            if (player == null || board[player.Position].Type != SquareType.Cup || player.Money < board[player.Position].Fee) return false;
            for (int i = 0; i < board.Count; i++)
            {
                if (CanHoldCup(board, i) && state.GetOwner(i) == player.Id) return true;
            }

            return false;
        }

        /// <summary>
        /// A holiday resort's rent when its owner holds <paramref name="held"/> of them: its base rent, doubled for every
        /// further resort (25K, 50K, 100K, 200K; the owner's wish, Faz 27). 0 when none is held.
        /// </summary>
        public static int ResortRent(int baseRent, int held) => held <= 0 ? 0 : baseRent << Math.Min(held - 1, 20);

        /// <summary>How many holiday resorts <paramref name="ownerId"/> holds; each one's rent doubles with every further one (<see cref="ResortRent"/>).</summary>
        public static int ResortsOwned(BoardDefinition board, GameState state, int ownerId)
        {
            int owned = 0;
            for (int i = 0; i < board.Count; i++)
            {
                if (board[i].IsResort && state.GetOwner(i) == ownerId) owned++;
            }

            return owned;
        }

        public static bool OwnsWholeGroup(BoardDefinition board, GameState state, int ownerId, int groupId)
        {
            int groupSize = board.CountInGroup(groupId);
            if (groupSize == 0) return false;

            int owned = 0;
            foreach (int square in GroupSquares(board, groupId))
            {
                if (state.GetOwner(square) == ownerId) owned++;
            }

            return owned == groupSize;
        }

        /// <summary>The property squares of a group, in board order. Group 0 has none.</summary>
        public static IEnumerable<int> GroupSquares(BoardDefinition board, int groupId)
        {
            if (groupId == 0) yield break;
            for (int i = 0; i < board.Count; i++)
            {
                if (board[i].Type == SquareType.Property && board[i].GroupId == groupId) yield return i;
            }
        }

        /// <summary>Whether the square carries houses or a hotel; such a town cannot be traded.</summary>
        public static bool HasBuildings(GameState state, int squareIndex) => state.GetBuildingLevel(squareIndex) > 0;

        /// <summary>Whether the table's game time has run out (never at a table without a game clock).</summary>
        public static bool TimeIsUp(RulesConfig rules, GameState state) =>
            rules.TimeLimitMinutes > 0 && state.PlayedSeconds >= rules.TimeLimitSeconds;

        /// <summary>
        /// Whether a player whose turn starts now picks a ride before rolling (the owner's rule, Faz 27): they stand at a bus
        /// stop they came to on an earlier turn, are not in jail and can pay its fare. Otherwise they simply roll from there.
        /// </summary>
        public static bool WaitsForRide(BoardDefinition board, PlayerState player) =>
            player != null && !player.InJail && board[player.Position].Type == SquareType.Travel && player.Money >= board[player.Position].Fee;

        /// <summary>
        /// Where a ride from the bus station may go: any other square on the board but a town another
        /// player owns (the ride is not a way to pay someone's rent on purpose, nor to land on their
        /// buildings). Free towns, one's own towns and the special squares are all fine.
        /// </summary>
        public static bool IsRideDestination(BoardDefinition board, GameState state, PlayerState player, int squareIndex)
        {
            if (squareIndex < 0 || squareIndex >= board.Count || squareIndex == player.Position) return false;
            // Nor does it go to the champions' cup (the owner's rule, Faz 31).
            if (board[squareIndex].Type == SquareType.Cup) return false;
            int owner = state.GetOwner(squareIndex);
            return board[squareIndex].Type != SquareType.Property || owner == GameState.NoOwner || owner == player.Id;
        }

        /// <summary>A rival's town is taken over at this share of its worth (the owner's rule, Faz 31).</summary>
        public const int TakeoverPercent = 150;

        /// <summary>What a town is worth for a takeover: its price and each building level at its cost.</summary>
        public static long TakeoverWorth(BoardDefinition board, GameState state, int squareIndex) =>
            PurchaseCost(board, squareIndex, state.GetBuildingLevel(squareIndex));

        /// <summary>What taking a town over costs: <see cref="TakeoverPercent"/> of <see cref="TakeoverWorth"/>, rounded down.</summary>
        public static long TakeoverPrice(BoardDefinition board, GameState state, int squareIndex) =>
            TakeoverWorth(board, state, squareIndex) * TakeoverPercent / 100;

        /// <summary>
        /// Whether the player may take over the town they stand on, right after paying its rent (Faz 31): a city (not a resort)
        /// of another player, without a hotel or a protection shield, and the player holds the whole takeover price.
        /// </summary>
        public static bool CanTakeOver(BoardDefinition board, GameState state, PlayerState player)
        {
            if (player == null || player.IsBankrupt) return false;
            int square = player.Position;
            SquareDefinition definition = board[square];
            if (definition.Type != SquareType.Property || definition.IsResort || !definition.IsDevelopable) return false;
            int owner = state.GetOwner(square);
            if (owner == GameState.NoOwner || owner == player.Id) return false;
            PlayerState holder = state.FindPlayer(owner);
            if (holder == null || holder.IsBankrupt) return false;
            if (state.GetBuildingLevel(square) >= SquareDefinition.MaxBuildingLevel || state.HasShield(square)) return false;
            return player.Money >= TakeoverPrice(board, state, square);
        }

        /// <summary>
        /// What a player is worth when a round-limited game is scored: money, plus each property at
        /// its price and each building level at its cost.
        /// </summary>
        public static int NetWorth(BoardDefinition board, GameState state, int playerId)
        {
            PlayerState player = state.FindPlayer(playerId);
            if (player == null || player.IsBankrupt) return 0;
            return player.Money + PropertyValue(board, state, playerId);
        }

        /// <summary>What a player's properties are worth, cash left out: each property at its price and each building level at its cost.</summary>
        public static int PropertyValue(BoardDefinition board, GameState state, int playerId)
        {
            int total = 0;
            for (int i = 0; i < board.Count; i++)
            {
                if (state.GetOwner(i) != playerId) continue;
                total += board[i].Price;
                total += state.GetBuildingLevel(i) * board[i].BuildingCost;
            }

            return total;
        }

        /// <summary>
        /// What a tax square takes from a player: its fixed amount, or its share of the player's
        /// <see cref="PropertyValue"/> rounded down (someone with no property pays nothing).
        /// </summary>
        public static int TaxDue(BoardDefinition board, GameState state, int squareIndex, int playerId)
        {
            SquareDefinition square = board[squareIndex];
            if (square.Type != SquareType.Tax) return 0;
            return square.TaxPercent > 0 ? PropertyValue(board, state, playerId) * square.TaxPercent / 100 : square.TaxAmount;
        }

        /// <summary>The solvent player with the highest <see cref="NetWorth"/>; a tie goes to the earlier seat.</summary>
        public static int Richest(BoardDefinition board, GameState state)
        {
            int best = GameState.NoWinner;
            int bestWorth = int.MinValue;
            foreach (PlayerState player in state.Players)
            {
                if (player.IsBankrupt) continue;
                int worth = NetWorth(board, state, player.Id);
                if (worth <= bestWorth) continue;
                best = player.Id;
                bestWorth = worth;
            }

            return best;
        }

        /// <summary>What leaving the board's jail early costs; 0 when the board has no jail.</summary>
        public static int JailFee(BoardDefinition board) => board.HasJail ? board[board.JailIndex].Fee : 0;

        /// <summary>Where taxes and card fines go: the pot when the board has one, otherwise the bank.</summary>
        public static int FineCollector(BoardDefinition board) => board.HasPot ? GameState.PotId : GameState.NoOwner;

        /// <summary>The two rolls a lookout offers as (die1, die2); null when no choice is open.</summary>
        public static int[] DiceOption(GameState state, int option)
        {
            int[] dice = state.DiceOptions;
            if (dice == null || option < 0 || option > 1) return null;
            return new[] { dice[option * 2], dice[option * 2 + 1] };
        }

        /// <summary>Money a player could hold after selling every property with its buildings back to the bank.</summary>
        public static int MaxRaisableMoney(BoardDefinition board, RulesConfig rules, GameState state, int playerId)
        {
            PlayerState player = state.FindPlayer(playerId);
            if (player == null) return 0;

            int total = player.Money;
            for (int i = 0; i < board.Count; i++)
            {
                if (state.GetOwner(i) != playerId) continue;
                total += PropertySaleProceeds(board, rules, state, i);
            }

            return total;
        }

        /// <summary>What the bank pays for a property sold back with everything on it: its whole price plus each building at its sell-back value.</summary>
        public static int PropertySaleProceeds(BoardDefinition board, RulesConfig rules, GameState state, int squareIndex) =>
            board[squareIndex].SaleValue + state.GetBuildingLevel(squareIndex) * rules.SellbackValue(board[squareIndex].BuildingCost);

        /// <summary>Houses a square may carry; the level above is the hotel.</summary>
        public const int MaxHouses = SquareDefinition.MaxBuildingLevel - 1;

        /// <summary>Until a player has passed the start square, each of their towns takes a single house; afterwards up to four.</summary>
        public const int FirstLapHouseLimit = 1;

        /// <summary>
        /// Building happens where the player arrived this turn: you own the town, you are on it, and
        /// Before their first pass of the start square a player builds one house per
        /// town, afterwards up to four; the hotel needs a later visit to a town that already has its
        /// four houses. Colour groups play no part.
        /// </summary>
        private static RejectionReason CanBuild(BoardDefinition board, GameState state, PlayerState player, int squareIndex, int levels)
        {
            RejectionReason owned = RequireOwnProperty(board, state, player, squareIndex);
            if (owned != RejectionReason.None) return owned;
            if (levels < 1 || levels > BuildableNow(board, state, player, squareIndex)) return RejectionReason.CannotBuild;
            return player.Money >= (long)board[squareIndex].BuildingCost * levels ? RejectionReason.None : RejectionReason.InsufficientFunds;
        }

        /// <summary>
        /// How many buildings the player may still add to this square on the current visit, money
        /// aside: the houses up to the player's lap limit, or one (the hotel) on a four-house town they
        /// arrived at. Zero when they do not own it or have not arrived on it this turn.
        /// </summary>
        public static int BuildableNow(BoardDefinition board, GameState state, PlayerState player, int squareIndex)
        {
            if (player == null || squareIndex < 0 || squareIndex >= board.Count) return 0;
            SquareDefinition square = board[squareIndex];
            if (square.Type != SquareType.Property || !square.IsDevelopable) return 0;
            if (state.GetOwner(squareIndex) != player.Id || player.Position != squareIndex) return 0;
            if (state.BuiltThisVisit == GameState.NotArrived || state.CurrentPlayer.Id != player.Id) return 0;

            int level = state.GetBuildingLevel(squareIndex);
            if (level >= SquareDefinition.MaxBuildingLevel) return 0;
            if (level == MaxHouses) return state.BuiltThisVisit == 0 ? 1 : 0;
            int limit = player.Laps == 0 ? FirstLapHouseLimit : MaxHouses;
            return Math.Max(0, limit - level);
        }

        /// <summary>
        /// How many houses the player could put up on a town right after buying it, on the same
        /// visit: the first-lap limit until they have passed the start square, otherwise all four.
        /// The property card shows the levels this allows on top of the purchase. Zero for a
        /// square that takes no buildings.
        /// </summary>
        public static int BuildableAfterPurchase(BoardDefinition board, PlayerState player, int squareIndex)
        {
            if (player == null || squareIndex < 0 || squareIndex >= board.Count) return 0;
            SquareDefinition square = board[squareIndex];
            if (square.Type != SquareType.Property || !square.IsDevelopable) return 0;
            return player.Laps == 0 ? FirstLapHouseLimit : MaxHouses;
        }

        /// <summary>What buying a square with <paramref name="levels"/> building levels on it costs in all: its price and the buildings.</summary>
        public static long PurchaseCost(BoardDefinition board, int squareIndex, int levels) =>
            (long)board[squareIndex].Price + (long)board[squareIndex].BuildingCost * Math.Max(0, levels);

        /// <summary>True when the next building on this square would be the hotel.</summary>
        public static bool NextIsHotel(GameState state, int squareIndex) => state.GetBuildingLevel(squareIndex) == MaxHouses;

        private static RejectionReason CanSell(BoardDefinition board, GameState state, PlayerState player, int squareIndex)
        {
            RejectionReason owned = RequireOwnProperty(board, state, player, squareIndex);
            if (owned != RejectionReason.None) return owned;
            return state.GetBuildingLevel(squareIndex) == 0 ? RejectionReason.CannotSell : RejectionReason.None;
        }

        private static RejectionReason ValidateTrade(BoardDefinition board, GameState state, TradeOffer offer)
        {
            PlayerState from = state.FindPlayer(offer.FromPlayerId);
            PlayerState to = state.FindPlayer(offer.ToPlayerId);
            if (from == null || to == null || from.Id == to.Id || from.IsBankrupt || to.IsBankrupt) return RejectionReason.InvalidTrade;
            if (offer.IsEmpty || offer.GiveMoney < 0 || offer.TakeMoney < 0) return RejectionReason.InvalidTrade;
            if (from.Money < offer.GiveMoney || to.Money < offer.TakeMoney) return RejectionReason.InsufficientFunds;

            var seen = new HashSet<int>();
            if (!SquaresTradable(board, state, offer.GiveSquares, from.Id, seen)) return RejectionReason.InvalidTrade;
            if (!SquaresTradable(board, state, offer.TakeSquares, to.Id, seen)) return RejectionReason.InvalidTrade;
            return RejectionReason.None;
        }

        private static bool SquaresTradable(BoardDefinition board, GameState state, IReadOnlyList<int> squares, int ownerId, HashSet<int> seen)
        {
            foreach (int square in squares)
            {
                if (square < 0 || square >= board.Count || !seen.Add(square)) return false;
                if (board[square].Type != SquareType.Property || state.GetOwner(square) != ownerId) return false;
                if (HasBuildings(state, square)) return false; // Buildings must be sold first.
            }

            return true;
        }

        private static RejectionReason RequireOwnProperty(BoardDefinition board, GameState state, PlayerState player, int squareIndex)
        {
            if (squareIndex < 0 || squareIndex >= board.Count || board[squareIndex].Type != SquareType.Property)
            {
                return RejectionReason.InvalidSquare;
            }

            return state.GetOwner(squareIndex) == player.Id ? RejectionReason.None : RejectionReason.NotOwner;
        }

        private static RejectionReason OnTurn(GameState state, PlayerState player, params GamePhase[] phases)
        {
            if (state.CurrentPlayer.Id != player.Id) return RejectionReason.NotYourTurn;
            return Array.IndexOf(phases, state.Phase) >= 0 ? RejectionReason.None : RejectionReason.WrongPhase;
        }

        private static RejectionReason Then(this RejectionReason first, Func<RejectionReason> next) =>
            first != RejectionReason.None ? first : next();
    }
}
