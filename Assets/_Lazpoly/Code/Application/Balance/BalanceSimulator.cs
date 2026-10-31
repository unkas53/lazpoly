using System;
using System.Collections.Generic;
using Lazpoly.App.AI;
using Lazpoly.App.Session;
using Lazpoly.Core.Board;
using Lazpoly.Core.Events;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;

namespace Lazpoly.App.Balance
{
    /// <summary>What to simulate: how many computer-only games, with how many seats, from which seed.</summary>
    public sealed class BalanceSettings
    {
        public BalanceSettings(int games, int players, ulong firstSeed = 1UL, int commandLimit = 40000)
        {
            if (games < 1) throw new ArgumentOutOfRangeException(nameof(games));
            if (players < RulesConfig.AbsoluteMinPlayers || players > RulesConfig.AbsoluteMaxPlayers) throw new ArgumentOutOfRangeException(nameof(players));
            if (commandLimit < 1) throw new ArgumentOutOfRangeException(nameof(commandLimit));
            Games = games;
            Players = players;
            FirstSeed = firstSeed;
            CommandLimit = commandLimit;
        }

        public int Games { get; }
        public int Players { get; }
        public ulong FirstSeed { get; }

        /// <summary>A game that needs more moves than this counts as stalled and stops.</summary>
        public int CommandLimit { get; }

        /// <summary>Seats cycle through the difficulties, shifted every game, so no seat always gets the strong player.</summary>
        public static AiDifficulty DifficultyOf(int game, int seat) => (AiDifficulty)(1 + (game + seat) % 3);
    }

    /// <summary>Totals over many computer games, for tuning prices and rents.</summary>
    public sealed class BalanceResult
    {
        public BalanceResult(int squares, int players)
        {
            Landings = new int[squares];
            RentBySquare = new long[squares];
            PurchasesBySquare = new int[squares];
            WinsBySeat = new int[players];
        }

        public int Games { get; internal set; }
        public int Stalled { get; internal set; }
        public int EndedByRoundLimit { get; internal set; }
        public List<int> Rounds { get; } = new List<int>();
        public int[] WinsBySeat { get; }
        public Dictionary<AiDifficulty, int> WinsByDifficulty { get; } = new Dictionary<AiDifficulty, int>();
        public Dictionary<AiDifficulty, int> SeatsByDifficulty { get; } = new Dictionary<AiDifficulty, int>();
        public int[] Landings { get; }
        public long[] RentBySquare { get; }
        public int[] PurchasesBySquare { get; }

        /// <summary>Games in which some colour group was first completed by one owner; the round it happened.</summary>
        public Dictionary<int, List<int>> GroupCompletedRounds { get; } = new Dictionary<int, List<int>>();

        public int GamesWithEarlyDebt { get; internal set; }
        public List<int> FirstBankruptcyRounds { get; } = new List<int>();
        public int Bankruptcies { get; internal set; }
        public long CardDraws { get; internal set; }
        public long PotPaidOut { get; internal set; }
        public long Rides { get; internal set; }

        /// <summary>Rounds up to this count as "early" for cash crises.</summary>
        public const int EarlyRounds = 5;
    }

    /// <summary>
    /// Plays whole games with computer players only and records what the board's numbers lead to:
    /// game length, which seats and difficulties win, where players land, which squares earn rent.
    /// Deterministic for the same settings, so a change to the board can be compared fairly.
    /// </summary>
    public static class BalanceSimulator
    {
        public static BalanceResult Run(BoardDefinition board, RulesConfig rules, BalanceSettings settings)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            var result = new BalanceResult(board.Count, settings.Players);
            for (int game = 0; game < settings.Games; game++) Play(board, rules, settings, game, result);
            return result;
        }

        private static void Play(BoardDefinition board, RulesConfig rules, BalanceSettings settings, int game, BalanceResult result)
        {
            var seats = new PlayerSeat[settings.Players];
            var ai = new Dictionary<int, AiDifficulty>();
            for (int seat = 0; seat < seats.Length; seat++)
            {
                seats[seat] = new PlayerSeat(seat + 1, $"AI {seat + 1}");
                AiDifficulty difficulty = BalanceSettings.DifficultyOf(game, seat);
                ai.Add(seat + 1, difficulty);
                result.SeatsByDifficulty[difficulty] = Count(result.SeatsByDifficulty, difficulty) + 1;
            }

            LocalGame local = LocalGame.Create(board, rules, seats, ai, settings.FirstSeed + (ulong)game);
            LocalGameSession session = local.Session;
            var watcher = new GameWatcher(board, session.State, result);
            session.EventsPublished += watcher.OnEvents;

            int commands = 0;
            while (session.State.Phase != GamePhase.GameOver && commands < settings.CommandLimit && session.StepAi()) commands++;
            session.EventsPublished -= watcher.OnEvents;

            result.Games++;
            GameState state = session.State;
            if (state.Phase != GamePhase.GameOver)
            {
                result.Stalled++;
                return;
            }

            result.Rounds.Add(state.Round);
            if (watcher.EndedByRoundLimit) result.EndedByRoundLimit++;
            int winnerSeat = state.WinnerId - 1;
            if (winnerSeat >= 0 && winnerSeat < settings.Players)
            {
                result.WinsBySeat[winnerSeat]++;
                AiDifficulty difficulty = ai[state.WinnerId];
                result.WinsByDifficulty[difficulty] = Count(result.WinsByDifficulty, difficulty) + 1;
            }

            if (watcher.HadEarlyDebt) result.GamesWithEarlyDebt++;
        }

        private static int Count(Dictionary<AiDifficulty, int> counts, AiDifficulty key) => counts.TryGetValue(key, out int value) ? value : 0;

        /// <summary>Follows one game's events; keeps its own copy of where each player stands while a batch is read.</summary>
        private sealed class GameWatcher
        {
            private readonly BoardDefinition _board;
            private readonly GameState _state;
            private readonly BalanceResult _result;
            private readonly Dictionary<int, int> _positions = new Dictionary<int, int>();
            private readonly HashSet<int> _completedGroups = new HashSet<int>();
            private readonly Dictionary<int, int> _owners = new Dictionary<int, int>();
            private bool _anyBankruptcy;

            public GameWatcher(BoardDefinition board, GameState state, BalanceResult result)
            {
                _board = board;
                _state = state;
                _result = result;
                foreach (PlayerState player in state.Players) _positions[player.Id] = player.Position;
            }

            public bool EndedByRoundLimit { get; private set; }
            public bool HadEarlyDebt { get; private set; }

            public void OnEvents(IReadOnlyList<GameEvent> events)
            {
                foreach (GameEvent gameEvent in events)
                {
                    switch (gameEvent)
                    {
                        case PlayerMovedEvent moved:
                            _positions[moved.PlayerId] = moved.ToSquare;
                            _result.Landings[moved.ToSquare]++;
                            if (moved.Kind == MoveKind.Jump) _result.Rides++;
                            break;

                        case MoneyChangedEvent money when money.Reason == MoneyReason.Rent && money.Delta < 0:
                            _result.RentBySquare[_positions[money.PlayerId]] -= money.Delta;
                            break;

                        case MoneyChangedEvent money when money.Reason == MoneyReason.Pot && money.Delta > 0:
                            _result.PotPaidOut += money.Delta;
                            break;

                        case PropertyOwnerChangedEvent owner:
                            if (owner.PreviousOwnerId == GameState.NoOwner && GameState.IsPlayerId(owner.NewOwnerId)) _result.PurchasesBySquare[owner.SquareIndex]++;
                            _owners[owner.SquareIndex] = owner.NewOwnerId;
                            CheckGroup(_board[owner.SquareIndex].GroupId);
                            break;

                        case CardDrawnEvent _:
                            _result.CardDraws++;
                            break;

                        case DebtIncurredEvent _:
                            if (_state.Round <= BalanceResult.EarlyRounds) HadEarlyDebt = true;
                            break;

                        case PlayerBankruptedEvent _:
                            _result.Bankruptcies++;
                            if (!_anyBankruptcy) _result.FirstBankruptcyRounds.Add(_state.Round);
                            _anyBankruptcy = true;
                            break;

                        case GameEndedEvent ended:
                            EndedByRoundLimit = ended.Reason == GameEndReason.RoundLimit;
                            break;
                    }
                }
            }

            private void CheckGroup(int groupId)
            {
                if (groupId <= 0 || _completedGroups.Contains(groupId)) return;
                int owner = GameState.NoOwner;
                for (int square = 0; square < _board.Count; square++)
                {
                    if (_board[square].Type != SquareType.Property || _board[square].GroupId != groupId) continue;
                    if (!_owners.TryGetValue(square, out int squareOwner) || !GameState.IsPlayerId(squareOwner)) return;
                    if (owner != GameState.NoOwner && owner != squareOwner) return;
                    owner = squareOwner;
                }

                if (owner == GameState.NoOwner) return;
                _completedGroups.Add(groupId);
                if (!_result.GroupCompletedRounds.TryGetValue(groupId, out List<int> rounds)) _result.GroupCompletedRounds[groupId] = rounds = new List<int>();
                rounds.Add(_state.Round);
            }
        }
    }
}
