using System;
using System.Collections.Generic;

namespace Lazpoly.Core.State
{
    /// <summary>
    /// An immutable trade proposal: the proposer gives money and squares, and asks for money and
    /// squares in return. Used in the command, the event and the state alike.
    /// </summary>
    public sealed class TradeOffer
    {
        private readonly int[] _giveSquares;
        private readonly int[] _takeSquares;

        public TradeOffer(int fromPlayerId, int toPlayerId, int giveMoney, int takeMoney,
            IReadOnlyList<int> giveSquares, IReadOnlyList<int> takeSquares)
        {
            FromPlayerId = fromPlayerId;
            ToPlayerId = toPlayerId;
            GiveMoney = giveMoney;
            TakeMoney = takeMoney;
            _giveSquares = Copy(giveSquares);
            _takeSquares = Copy(takeSquares);
        }

        public int FromPlayerId { get; }
        public int ToPlayerId { get; }

        /// <summary>Money the proposer pays.</summary>
        public int GiveMoney { get; }

        /// <summary>Money the proposer asks for.</summary>
        public int TakeMoney { get; }

        /// <summary>Squares the proposer hands over.</summary>
        public IReadOnlyList<int> GiveSquares => _giveSquares;

        /// <summary>Squares the proposer asks for.</summary>
        public IReadOnlyList<int> TakeSquares => _takeSquares;

        public bool IsEmpty => GiveMoney == 0 && TakeMoney == 0 && _giveSquares.Length == 0 && _takeSquares.Length == 0;

        private static int[] Copy(IReadOnlyList<int> source)
        {
            if (source == null) return Array.Empty<int>();
            var copy = new int[source.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = source[i];
            return copy;
        }
    }
}
