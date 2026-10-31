using System;
using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Rules;

namespace Lazpoly.Core.State
{
    /// <summary>
    /// The single source of truth for a game. It is never edited directly: the host's engine and
    /// every client change it only by applying events through <see cref="GameStateReducer"/>,
    /// so all copies stay identical.
    /// </summary>
    public sealed class GameState
    {
        public const int NoOwner = -1;
        public const int NoWinner = -1;

        private readonly List<PlayerState> _players;
        private readonly int[] _owners;
        private readonly int[] _buildingLevels;
        private readonly bool[] _mortgaged;

        private GameState(List<PlayerState> players, int squareCount)
        {
            _players = players;
            _owners = new int[squareCount];
            _buildingLevels = new int[squareCount];
            _mortgaged = new bool[squareCount];
            for (int i = 0; i < _owners.Length; i++) _owners[i] = NoOwner;
            WinnerId = NoWinner;
        }

        public IReadOnlyList<PlayerState> Players => _players;
        public int SquareCount => _owners.Length;
        public int CurrentPlayerIndex { get; internal set; }
        public PlayerState CurrentPlayer => _players[CurrentPlayerIndex];
        public GamePhase Phase { get; internal set; }

        /// <summary>1-based turn counter, increased each time a turn starts.</summary>
        public int TurnNumber { get; internal set; }

        /// <summary>Faces of the last roll in the current turn; 0 before the first roll.</summary>
        public int LastDie1 { get; internal set; }
        public int LastDie2 { get; internal set; }
        public bool LastRollWasDouble => LastDie1 != 0 && LastDie1 == LastDie2;

        /// <summary>Doubles rolled so far in the current turn.</summary>
        public int ConsecutiveDoubles { get; internal set; }

        public int WinnerId { get; internal set; }

        /// <summary>The running auction; null unless <see cref="Phase"/> is <see cref="GamePhase.AuctionInProgress"/>.</summary>
        public AuctionState Auction { get; internal set; }

        /// <summary>The open trade offer; null unless <see cref="Phase"/> is <see cref="GamePhase.AwaitingTradeResponse"/>.</summary>
        public TradeOffer PendingTrade { get; internal set; }

        /// <summary>Phase to return to once the open trade is answered.</summary>
        public GamePhase TradeReturnPhase { get; internal set; }

        /// <summary>The unpaid debt; null unless <see cref="Phase"/> is <see cref="GamePhase.AwaitingDebtPayment"/>.</summary>
        public DebtState Debt { get; internal set; }

        public static GameState CreateInitial(BoardDefinition board, RulesConfig rules, IReadOnlyList<PlayerSeat> seats)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (seats == null) throw new ArgumentNullException(nameof(seats));
            if (seats.Count < RulesConfig.AbsoluteMinPlayers || seats.Count > RulesConfig.AbsoluteMaxPlayers)
            {
                throw new ArgumentException(
                    $"A game needs {RulesConfig.AbsoluteMinPlayers}-{RulesConfig.AbsoluteMaxPlayers} players, got {seats.Count}.",
                    nameof(seats));
            }

            var ids = new HashSet<int>();
            var players = new List<PlayerState>(seats.Count);
            foreach (PlayerSeat seat in seats)
            {
                if (seat == null) throw new ArgumentException("Seats cannot contain null.", nameof(seats));
                if (!ids.Add(seat.Id)) throw new ArgumentException($"Duplicate player id {seat.Id}.", nameof(seats));
                players.Add(new PlayerState(seat.Id, seat.Name, rules.StartingMoney));
            }

            return new GameState(players, board.Count)
            {
                CurrentPlayerIndex = 0,
                Phase = GamePhase.WaitingForRoll,
                TurnNumber = 1,
            };
        }

        internal static GameState FromSnapshot(GameStateSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.Players == null || snapshot.Players.Count < RulesConfig.AbsoluteMinPlayers ||
                snapshot.Players.Count > RulesConfig.AbsoluteMaxPlayers)
            {
                throw new ArgumentException("Snapshot has an invalid player list.", nameof(snapshot));
            }

            if (snapshot.Owners == null || snapshot.Owners.Length < 2)
            {
                throw new ArgumentException("Snapshot has no board.", nameof(snapshot));
            }

            if (snapshot.CurrentPlayerIndex < 0 || snapshot.CurrentPlayerIndex >= snapshot.Players.Count)
            {
                throw new ArgumentException("Snapshot current player is out of range.", nameof(snapshot));
            }

            var ids = new HashSet<int>();
            var players = new List<PlayerState>(snapshot.Players.Count);
            foreach (GameStateSnapshot.PlayerEntry entry in snapshot.Players)
            {
                if (entry == null || !ids.Add(entry.Id)) throw new ArgumentException("Snapshot has duplicate or empty players.", nameof(snapshot));
                if (entry.Position < 0 || entry.Position >= snapshot.Owners.Length)
                {
                    throw new ArgumentException($"Player {entry.Id} stands outside the board.", nameof(snapshot));
                }

                players.Add(new PlayerState(entry.Id, entry.Name ?? string.Empty, entry.Money)
                {
                    Position = entry.Position,
                    IsBankrupt = entry.IsBankrupt,
                });
            }

            var state = new GameState(players, snapshot.Owners.Length)
            {
                Phase = snapshot.Phase,
                CurrentPlayerIndex = snapshot.CurrentPlayerIndex,
                TurnNumber = snapshot.TurnNumber,
                LastDie1 = snapshot.LastDie1,
                LastDie2 = snapshot.LastDie2,
                ConsecutiveDoubles = snapshot.ConsecutiveDoubles,
                WinnerId = snapshot.WinnerId,
            };

            for (int i = 0; i < snapshot.Owners.Length; i++)
            {
                int owner = snapshot.Owners[i];
                if (owner != NoOwner && !ids.Contains(owner))
                {
                    throw new ArgumentException($"Square {i} is owned by unknown player {owner}.", nameof(snapshot));
                }

                state.SetOwner(i, owner);
            }

            if (snapshot.BuildingLevels == null || snapshot.BuildingLevels.Length != snapshot.Owners.Length ||
                snapshot.Mortgaged == null || snapshot.Mortgaged.Length != snapshot.Owners.Length)
            {
                throw new ArgumentException("Snapshot building or mortgage data does not match the board.", nameof(snapshot));
            }

            for (int i = 0; i < snapshot.Owners.Length; i++)
            {
                int level = snapshot.BuildingLevels[i];
                bool unowned = snapshot.Owners[i] == NoOwner;
                if (level < 0 || level > SquareDefinition.MaxBuildingLevel || (unowned && (level != 0 || snapshot.Mortgaged[i])))
                {
                    throw new ArgumentException($"Square {i} has impossible building or mortgage data.", nameof(snapshot));
                }

                state.SetBuildingLevel(i, level);
                state.SetMortgaged(i, snapshot.Mortgaged[i]);
            }

            if (snapshot.Auction != null)
            {
                AuctionState auction = snapshot.Auction;
                if (auction.SquareIndex < 0 || auction.SquareIndex >= snapshot.Owners.Length ||
                    (auction.HighestBidderId != NoOwner && !ids.Contains(auction.HighestBidderId)))
                {
                    throw new ArgumentException("Snapshot auction is inconsistent.", nameof(snapshot));
                }

                foreach (int passed in auction.PassedPlayerIds)
                {
                    if (!ids.Contains(passed)) throw new ArgumentException("Snapshot auction names an unknown player.", nameof(snapshot));
                }

                state.Auction = auction.Copy();
            }

            if (snapshot.PendingTrade != null)
            {
                TradeOffer trade = snapshot.PendingTrade;
                if (!ids.Contains(trade.FromPlayerId) || !ids.Contains(trade.ToPlayerId))
                {
                    throw new ArgumentException("Snapshot trade names an unknown player.", nameof(snapshot));
                }

                foreach (int square in trade.GiveSquares) RequireSquare(square, snapshot.Owners.Length);
                foreach (int square in trade.TakeSquares) RequireSquare(square, snapshot.Owners.Length);
                state.PendingTrade = trade;
                state.TradeReturnPhase = snapshot.TradeReturnPhase;
            }

            if (snapshot.Debt != null)
            {
                DebtState debt = snapshot.Debt;
                if (!ids.Contains(debt.PlayerId) || (debt.CreditorId != NoOwner && !ids.Contains(debt.CreditorId)) || debt.Amount <= 0)
                {
                    throw new ArgumentException("Snapshot debt is inconsistent.", nameof(snapshot));
                }

                state.Debt = debt;
            }

            return state;
        }

        private static void RequireSquare(int square, int count)
        {
            if (square < 0 || square >= count) throw new ArgumentException($"Square {square} is outside the board.");
        }

        public int GetOwner(int squareIndex) => _owners[squareIndex];

        internal void SetOwner(int squareIndex, int ownerId) => _owners[squareIndex] = ownerId;

        /// <summary>0 = no buildings, 1-4 = houses, 5 = hotel.</summary>
        public int GetBuildingLevel(int squareIndex) => _buildingLevels[squareIndex];

        internal void SetBuildingLevel(int squareIndex, int level) => _buildingLevels[squareIndex] = level;

        public bool IsMortgaged(int squareIndex) => _mortgaged[squareIndex];

        internal void SetMortgaged(int squareIndex, bool mortgaged) => _mortgaged[squareIndex] = mortgaged;

        public PlayerState FindPlayer(int playerId)
        {
            foreach (PlayerState player in _players)
            {
                if (player.Id == playerId) return player;
            }

            return null;
        }

        public int IndexOfPlayer(int playerId)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].Id == playerId) return i;
            }

            return -1;
        }

        public int ActivePlayerCount()
        {
            int count = 0;
            foreach (PlayerState player in _players)
            {
                if (!player.IsBankrupt) count++;
            }

            return count;
        }

        /// <summary>
        /// Deterministic 64-bit FNV-1a digest of everything that matters for the game. Host and clients
        /// compare it to detect a copy that drifted. Player names are excluded on purpose.
        /// </summary>
        public ulong ComputeHash()
        {
            ulong hash = 14695981039346656037UL;
            void Mix(int value)
            {
                unchecked
                {
                    for (int shift = 0; shift < 32; shift += 8)
                    {
                        hash ^= (byte)(value >> shift);
                        hash *= 1099511628211UL;
                    }
                }
            }

            Mix((int)Phase);
            Mix(CurrentPlayerIndex);
            Mix(TurnNumber);
            Mix(LastDie1);
            Mix(LastDie2);
            Mix(ConsecutiveDoubles);
            Mix(WinnerId);
            foreach (PlayerState player in _players)
            {
                Mix(player.Id);
                Mix(player.Money);
                Mix(player.Position);
                Mix(player.IsBankrupt ? 1 : 0);
            }

            foreach (int owner in _owners)
            {
                Mix(owner);
            }

            foreach (int level in _buildingLevels) Mix(level);
            foreach (bool mortgaged in _mortgaged) Mix(mortgaged ? 1 : 0);

            Mix(Auction == null ? 0 : 1);
            if (Auction != null)
            {
                Mix(Auction.SquareIndex);
                Mix(Auction.HighestBid);
                Mix(Auction.HighestBidderId);
                Mix(Auction.PassedPlayerIds.Count);
                foreach (int id in Auction.PassedPlayerIds) Mix(id);
            }

            Mix(PendingTrade == null ? 0 : 1);
            if (PendingTrade != null)
            {
                Mix(PendingTrade.FromPlayerId);
                Mix(PendingTrade.ToPlayerId);
                Mix(PendingTrade.GiveMoney);
                Mix(PendingTrade.TakeMoney);
                Mix(PendingTrade.GiveSquares.Count);
                foreach (int square in PendingTrade.GiveSquares) Mix(square);
                Mix(PendingTrade.TakeSquares.Count);
                foreach (int square in PendingTrade.TakeSquares) Mix(square);
                Mix((int)TradeReturnPhase);
            }

            Mix(Debt == null ? 0 : 1);
            if (Debt != null)
            {
                Mix(Debt.PlayerId);
                Mix(Debt.CreditorId);
                Mix(Debt.Amount);
                Mix((int)Debt.Reason);
            }

            return hash;
        }
    }
}
