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

                case BuyPropertyCommand _:
                {
                    RejectionReason turn = OnTurn(state, player, GamePhase.AwaitingBuyDecision);
                    if (turn != RejectionReason.None) return turn;
                    return player.Money >= board[player.Position].Price ? RejectionReason.None : RejectionReason.InsufficientFunds;
                }

                case DeclinePurchaseCommand _:
                    return OnTurn(state, player, GamePhase.AwaitingBuyDecision);

                case EndTurnCommand _:
                    return OnTurn(state, player, GamePhase.TurnEnd);

                case BuildCommand c:
                    return OnTurn(state, player, GamePhase.WaitingForRoll, GamePhase.TurnEnd)
                        .Then(() => CanBuild(board, state, player, c.SquareIndex));

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

                default:
                    return RejectionReason.UnknownCommand;
            }
        }

        /// <summary>Rent the owner would collect on a property right now.</summary>
        public static int CalculateRent(BoardDefinition board, RulesConfig rules, GameState state, int squareIndex)
        {
            SquareDefinition square = board[squareIndex];
            if (square.Type != SquareType.Property) return 0;

            int ownerId = state.GetOwner(squareIndex);
            if (ownerId == GameState.NoOwner) return 0;

            if (square.IsResort) return square.BaseRent * ResortsOwned(board, state, ownerId);

            int level = state.GetBuildingLevel(squareIndex);
            if (level > 0) return square.DevelopedRents[level - 1];

            return OwnsWholeGroup(board, state, ownerId, square.GroupId)
                ? square.BaseRent * rules.FullGroupRentMultiplier
                : square.BaseRent;
        }

        /// <summary>How many holiday resorts <paramref name="ownerId"/> holds; each one's rent is its base rent times this.</summary>
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

        /// <summary>
        /// Where a ride from the bus station may go: any other square on the board but a town another
        /// player owns (the ride is not a way to pay someone's rent on purpose, nor to land on their
        /// buildings). Free towns, one's own towns and the special squares are all fine.
        /// </summary>
        public static bool IsRideDestination(BoardDefinition board, GameState state, PlayerState player, int squareIndex)
        {
            if (squareIndex < 0 || squareIndex >= board.Count || squareIndex == player.Position) return false;
            int owner = state.GetOwner(squareIndex);
            return board[squareIndex].Type != SquareType.Property || owner == GameState.NoOwner || owner == player.Id;
        }

        /// <summary>
        /// What a player is worth when a round-limited game is scored: money, plus each property at
        /// its price and each building level at its cost.
        /// </summary>
        public static int NetWorth(BoardDefinition board, GameState state, int playerId)
        {
            PlayerState player = state.FindPlayer(playerId);
            if (player == null || player.IsBankrupt) return 0;

            int total = player.Money;
            for (int i = 0; i < board.Count; i++)
            {
                if (state.GetOwner(i) != playerId) continue;
                total += board[i].Price;
                total += state.GetBuildingLevel(i) * board[i].BuildingCost;
            }

            return total;
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

        /// <summary>What the bank pays for a property sold back with everything on it: half its price plus each building at its sell-back value.</summary>
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
        private static RejectionReason CanBuild(BoardDefinition board, GameState state, PlayerState player, int squareIndex)
        {
            RejectionReason owned = RequireOwnProperty(board, state, player, squareIndex);
            if (owned != RejectionReason.None) return owned;
            if (BuildableNow(board, state, player, squareIndex) == 0) return RejectionReason.CannotBuild;
            return player.Money >= board[squareIndex].BuildingCost ? RejectionReason.None : RejectionReason.InsufficientFunds;
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
