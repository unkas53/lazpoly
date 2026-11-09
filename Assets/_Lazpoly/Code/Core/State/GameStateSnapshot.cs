using System;
using System.Collections.Generic;

namespace Lazpoly.Core.State
{
    /// <summary>
    /// A plain copy of every field of <see cref="GameState"/>. The host sends it to a client that
    /// joins, rejoins or drifts, and the client rebuilds an identical state from it.
    /// </summary>
    public sealed class GameStateSnapshot
    {
        public sealed class PlayerEntry
        {
            public int Id;
            public string Name;
            public int Money;
            public int Position;
            public bool IsBankrupt;
            public bool HasLookout;
            public int Laps;

            /// <summary>Turns still to wait in jail; 0 when free.</summary>
            public int JailTurns;

            /// <summary>The character the player plays as; empty when none was picked (older games).</summary>
            public string Figure = string.Empty;

            /// <summary>The cards kept in hand, as deck indexes (Faz 30); null or empty when none.</summary>
            public List<int> Hand = new List<int>();

            /// <summary>What the player's investment fund pays at Start; 0 when none (Faz 30).</summary>
            public int FundPayout;
        }

        public GamePhase Phase;
        public int CurrentPlayerIndex;
        public int TurnNumber;
        public int LastDie1;
        public int LastDie2;
        public int ConsecutiveDoubles;
        public int WinnerId;
        public int Round = 1;
        public int Pot;
        public int BuiltThisVisit = GameState.NotArrived;

        /// <summary>The player on turn left jail with a double this turn, which earns no second roll.</summary>
        public bool RolledOutOfJail;

        /// <summary>The town holding the champions' cup (<see cref="GameState.NoCup"/> when none), its raise in percent and the game's pick count.</summary>
        public int CupSquare = GameState.NoCup;
        public int CupPercent;
        public int CupPicks;

        /// <summary>Game time played so far, in whole seconds (the game clock, Faz 28).</summary>
        public int PlayedSeconds;

        /// <summary>Null unless a lookout's two rolls wait for a choice.</summary>
        public int[] DiceOptions;
        public List<PlayerEntry> Players = new List<PlayerEntry>();
        public int[] Owners = Array.Empty<int>();
        public int[] BuildingLevels = Array.Empty<int>();
        public TradeOffer PendingTrade;
        public GamePhase TradeReturnPhase;
        public DebtState Debt;

        /// <summary>The cards' marks on the board (Faz 30): shields, and each strike's player and turns left; null in older snapshots.</summary>
        public bool[] Shields;
        public int[] Strikers;
        public int[] StrikeTurns;

        /// <summary>One card a turn: the player on turn has played theirs; and their move was a chauffeur's ride.</summary>
        public bool CardPlayedThisTurn;
        public bool RodeThisTurn;

        /// <summary>Null unless rent waits on the current player's rent-shield choice.</summary>
        public DebtState RentDue;

        public static GameStateSnapshot Capture(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            var snapshot = new GameStateSnapshot
            {
                Phase = state.Phase,
                CurrentPlayerIndex = state.CurrentPlayerIndex,
                TurnNumber = state.TurnNumber,
                LastDie1 = state.LastDie1,
                LastDie2 = state.LastDie2,
                ConsecutiveDoubles = state.ConsecutiveDoubles,
                WinnerId = state.WinnerId,
                Round = state.Round,
                Pot = state.Pot,
                BuiltThisVisit = state.BuiltThisVisit,
                RolledOutOfJail = state.RolledOutOfJail,
                CupSquare = state.CupSquare,
                CupPercent = state.CupPercent,
                CupPicks = state.CupPicks,
                PlayedSeconds = state.PlayedSeconds,
                DiceOptions = state.DiceOptions == null ? null : (int[])state.DiceOptions.Clone(),
                Owners = new int[state.SquareCount],
                BuildingLevels = new int[state.SquareCount],
                PendingTrade = state.PendingTrade,
                TradeReturnPhase = state.TradeReturnPhase,
                Debt = state.Debt,
                Shields = new bool[state.SquareCount],
                Strikers = new int[state.SquareCount],
                StrikeTurns = new int[state.SquareCount],
                CardPlayedThisTurn = state.CardPlayedThisTurn,
                RodeThisTurn = state.RodeThisTurn,
                RentDue = state.RentDue,
            };

            foreach (PlayerState player in state.Players)
            {
                snapshot.Players.Add(new PlayerEntry
                {
                    Id = player.Id,
                    Name = player.Name,
                    Money = player.Money,
                    Position = player.Position,
                    IsBankrupt = player.IsBankrupt,
                    HasLookout = player.HasLookout,
                    Laps = player.Laps,
                    JailTurns = player.JailTurns,
                    Figure = player.Figure,
                    Hand = new List<int>(player.Hand),
                    FundPayout = player.FundPayout,
                });
            }

            for (int i = 0; i < state.SquareCount; i++)
            {
                snapshot.Owners[i] = state.GetOwner(i);
                snapshot.BuildingLevels[i] = state.GetBuildingLevel(i);
                snapshot.Shields[i] = state.HasShield(i);
                snapshot.Strikers[i] = state.StrikerOf(i);
                snapshot.StrikeTurns[i] = state.StrikeTurnsLeft(i);
            }

            return snapshot;
        }

        /// <summary>Builds a new state from the snapshot. Throws if the snapshot is inconsistent.</summary>
        public GameState Restore()
        {
            return GameState.FromSnapshot(this);
        }
    }
}
