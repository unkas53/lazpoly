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

        private GameState(List<PlayerState> players, int squareCount)
        {
            _players = players;
            _owners = new int[squareCount];
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

        public int GetOwner(int squareIndex) => _owners[squareIndex];

        internal void SetOwner(int squareIndex, int ownerId) => _owners[squareIndex] = ownerId;

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

            return hash;
        }
    }
}
