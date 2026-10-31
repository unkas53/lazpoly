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

        /// <summary>Null unless a lookout's two rolls wait for a choice.</summary>
        public int[] DiceOptions;
        public List<PlayerEntry> Players = new List<PlayerEntry>();
        public int[] Owners = Array.Empty<int>();
        public int[] BuildingLevels = Array.Empty<int>();
        public bool[] Mortgaged = Array.Empty<bool>();
        public AuctionState Auction;
        public TradeOffer PendingTrade;
        public GamePhase TradeReturnPhase;
        public DebtState Debt;

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
                DiceOptions = state.DiceOptions == null ? null : (int[])state.DiceOptions.Clone(),
                Owners = new int[state.SquareCount],
                BuildingLevels = new int[state.SquareCount],
                Mortgaged = new bool[state.SquareCount],
                Auction = state.Auction?.Copy(),
                PendingTrade = state.PendingTrade,
                TradeReturnPhase = state.TradeReturnPhase,
                Debt = state.Debt,
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
                });
            }

            for (int i = 0; i < state.SquareCount; i++)
            {
                snapshot.Owners[i] = state.GetOwner(i);
                snapshot.BuildingLevels[i] = state.GetBuildingLevel(i);
                snapshot.Mortgaged[i] = state.IsMortgaged(i);
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
