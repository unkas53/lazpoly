using System;
using System.Collections.Generic;

namespace Lazpoly.Core.State
{
    /// <summary>A running auction for one property. Changed only by <see cref="GameStateReducer"/>.</summary>
    public sealed class AuctionState
    {
        private readonly List<int> _passed;

        public AuctionState(int squareIndex, int highestBid, int highestBidderId, IEnumerable<int> passedPlayerIds)
        {
            if (highestBid < 0) throw new ArgumentOutOfRangeException(nameof(highestBid));
            SquareIndex = squareIndex;
            HighestBid = highestBid;
            HighestBidderId = highestBidderId;
            _passed = new List<int>(passedPlayerIds ?? Array.Empty<int>());
        }

        public int SquareIndex { get; }

        /// <summary>0 while nobody has bid.</summary>
        public int HighestBid { get; internal set; }

        /// <summary><see cref="GameState.NoOwner"/> while nobody has bid.</summary>
        public int HighestBidderId { get; internal set; }

        /// <summary>Players who left the auction, in the order they passed.</summary>
        public IReadOnlyList<int> PassedPlayerIds => _passed;

        public bool HasPassed(int playerId) => _passed.Contains(playerId);

        internal void AddPassed(int playerId) => _passed.Add(playerId);

        public AuctionState Copy() => new AuctionState(SquareIndex, HighestBid, HighestBidderId, _passed);
    }
}
