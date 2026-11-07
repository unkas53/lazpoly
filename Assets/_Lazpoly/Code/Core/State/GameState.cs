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

        /// <summary>Creditor id of the pot: taxes and card fines are owed to it instead of the bank when the board has one.</summary>
        public const int PotId = -2;

        private readonly List<PlayerState> _players;
        private readonly int[] _owners;
        private readonly int[] _buildingLevels;

        private GameState(List<PlayerState> players, int squareCount)
        {
            _players = players;
            _owners = new int[squareCount];
            _buildingLevels = new int[squareCount];
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

        /// <summary>1-based round counter: goes up each time the turn passes the first seat again.</summary>
        public int Round { get; internal set; }

        /// <summary>Value of <see cref="CupSquare"/> before anyone has brought the cup to a town.</summary>
        public const int NoCup = -1;

        /// <summary>The town the champions' cup stands in, whose rent is <see cref="CupPercent"/> per cent higher; <see cref="NoCup"/> before the first pick.</summary>
        public int CupSquare { get; internal set; } = NoCup;

        /// <summary>How much, in percent, the cup raises its town's rent; 0 while no town has it.</summary>
        public int CupPercent { get; internal set; }

        /// <summary>How many times the cup has been brought to a town in this game, by anyone; each pick raises the next one's share.</summary>
        public int CupPicks { get; internal set; }

        /// <summary>Money waiting in the pot for the next player who lands on the pot square.</summary>
        public int Pot { get; internal set; }

        /// <summary>Value of <see cref="BuiltThisVisit"/> while the current player has not moved this turn.</summary>
        public const int NotArrived = -1;

        /// <summary>
        /// Buildings the current player added since they arrived on their square this turn, or
        /// <see cref="NotArrived"/> before they move. Building needs an arrival, and a hotel may only
        /// go up on a visit that started with four houses already standing, so it needs zero here.
        /// </summary>
        public int BuiltThisVisit { get; internal set; } = NotArrived;

        /// <summary>The two rolls to choose from (die1, die2, die1, die2); null unless <see cref="Phase"/> is <see cref="GamePhase.AwaitingDiceChoice"/>.</summary>
        public int[] DiceOptions { get; internal set; }

        /// <summary>True for a real player's id, false for the bank (<see cref="NoOwner"/>) and the pot (<see cref="PotId"/>).</summary>
        public static bool IsPlayerId(int id) => id != NoOwner && id != PotId;

        /// <summary>Doubles rolled so far in the current turn.</summary>
        public int ConsecutiveDoubles { get; internal set; }

        /// <summary>The player on turn left jail with a double this turn: they moved by it, but it earns no second roll.</summary>
        public bool RolledOutOfJail { get; internal set; }

        public int WinnerId { get; internal set; }

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
                players.Add(new PlayerState(seat.Id, seat.Name, rules.StartingMoney, seat.Figure));
            }

            return new GameState(players, board.Count)
            {
                CurrentPlayerIndex = 0,
                Phase = GamePhase.WaitingForRoll,
                TurnNumber = 1,
                Round = 1,
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

                if (entry.Laps < 0) throw new ArgumentException($"Player {entry.Id} has a negative lap count.", nameof(snapshot));
                if (entry.JailTurns < 0) throw new ArgumentException($"Player {entry.Id} has negative jail turns.", nameof(snapshot));
                players.Add(new PlayerState(entry.Id, entry.Name ?? string.Empty, entry.Money, entry.Figure)
                {
                    Position = entry.Position,
                    IsBankrupt = entry.IsBankrupt,
                    HasLookout = entry.HasLookout,
                    Laps = entry.Laps,
                    JailTurns = entry.JailTurns,
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
                Round = snapshot.Round,
                Pot = snapshot.Pot,
                BuiltThisVisit = snapshot.BuiltThisVisit,
                RolledOutOfJail = snapshot.RolledOutOfJail,
                CupSquare = snapshot.CupSquare,
                CupPercent = snapshot.CupPercent,
                CupPicks = snapshot.CupPicks,
            };

            bool noCup = snapshot.CupSquare == NoCup;
            if (snapshot.CupPicks < 0 || (noCup ? snapshot.CupPercent != 0 || snapshot.CupPicks != 0
                    : snapshot.CupSquare < 0 || snapshot.CupSquare >= snapshot.Owners.Length || snapshot.CupPercent <= 0 || snapshot.CupPicks == 0))
            {
                throw new ArgumentException("Snapshot cup data is inconsistent.", nameof(snapshot));
            }

            if (snapshot.BuiltThisVisit < NotArrived) throw new ArgumentException("Snapshot build count is out of range.", nameof(snapshot));
            if (snapshot.Round < 1 || snapshot.Pot < 0) throw new ArgumentException("Snapshot round or pot is out of range.", nameof(snapshot));
            if (snapshot.DiceOptions != null)
            {
                if (snapshot.DiceOptions.Length != 4 || Array.Exists(snapshot.DiceOptions, die => die < 1))
                {
                    throw new ArgumentException("Snapshot dice options are malformed.", nameof(snapshot));
                }

                state.DiceOptions = (int[])snapshot.DiceOptions.Clone();
            }

            for (int i = 0; i < snapshot.Owners.Length; i++)
            {
                int owner = snapshot.Owners[i];
                if (owner != NoOwner && !ids.Contains(owner))
                {
                    throw new ArgumentException($"Square {i} is owned by unknown player {owner}.", nameof(snapshot));
                }

                state.SetOwner(i, owner);
            }

            if (snapshot.BuildingLevels == null || snapshot.BuildingLevels.Length != snapshot.Owners.Length)
            {
                throw new ArgumentException("Snapshot building data does not match the board.", nameof(snapshot));
            }

            for (int i = 0; i < snapshot.Owners.Length; i++)
            {
                int level = snapshot.BuildingLevels[i];
                bool unowned = snapshot.Owners[i] == NoOwner;
                if (level < 0 || level > SquareDefinition.MaxBuildingLevel || (unowned && level != 0))
                {
                    throw new ArgumentException($"Square {i} has impossible building data.", nameof(snapshot));
                }

                state.SetBuildingLevel(i, level);
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
                if (!ids.Contains(debt.PlayerId) || (IsPlayerId(debt.CreditorId) && !ids.Contains(debt.CreditorId)) || debt.Amount <= 0)
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
            Mix(Round);
            Mix(Pot);
            Mix(BuiltThisVisit);
            Mix(RolledOutOfJail ? 1 : 0);
            Mix(CupSquare);
            Mix(CupPercent);
            Mix(CupPicks);
            Mix(DiceOptions == null ? 0 : 1);
            if (DiceOptions != null)
            {
                foreach (int die in DiceOptions) Mix(die);
            }

            foreach (PlayerState player in _players)
            {
                Mix(player.Id);
                Mix(player.Money);
                Mix(player.Position);
                Mix(player.IsBankrupt ? 1 : 0);
                Mix(player.HasLookout ? 1 : 0);
                Mix(player.Laps);
                Mix(player.JailTurns);
            }

            foreach (int owner in _owners)
            {
                Mix(owner);
            }

            foreach (int level in _buildingLevels) Mix(level);

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
