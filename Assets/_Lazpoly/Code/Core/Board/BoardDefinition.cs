using System;
using System.Collections.Generic;

namespace Lazpoly.Core.Board
{
    /// <summary>
    /// The ordered ring of squares. Validated once on creation so the rules never have to
    /// defend against a malformed board.
    /// </summary>
    public sealed class BoardDefinition
    {
        private readonly SquareDefinition[] _squares;

        public BoardDefinition(IReadOnlyList<SquareDefinition> squares)
        {
            if (squares == null) throw new ArgumentNullException(nameof(squares));
            if (squares.Count < 2) throw new ArgumentException("A board needs at least two squares.", nameof(squares));

            _squares = new SquareDefinition[squares.Count];
            for (int i = 0; i < squares.Count; i++)
            {
                SquareDefinition square = squares[i] ?? throw new ArgumentException($"Square {i} is null.", nameof(squares));
                Validate(square, i);
                _squares[i] = square;
            }

            if (_squares[0].Type != SquareType.Start)
            {
                throw new ArgumentException("Square 0 must be the start square.", nameof(squares));
            }
        }

        public int Count => _squares.Length;

        public SquareDefinition this[int index] => _squares[index];

        /// <summary>Number of properties in a group. Returns 0 for group 0 (no group).</summary>
        public int CountInGroup(int groupId)
        {
            if (groupId == 0) return 0;
            int count = 0;
            foreach (SquareDefinition square in _squares)
            {
                if (square.Type == SquareType.Property && square.GroupId == groupId) count++;
            }

            return count;
        }

        private static void Validate(SquareDefinition square, int index)
        {
            if (index > 0 && square.Type == SquareType.Start)
            {
                throw new ArgumentException($"Square {index}: only square 0 may be the start square.");
            }

            switch (square.Type)
            {
                case SquareType.Property:
                    if (square.Price <= 0) throw new ArgumentException($"Square {index} '{square.Name}': price must be positive.");
                    if (square.BaseRent < 0) throw new ArgumentException($"Square {index} '{square.Name}': rent cannot be negative.");
                    if (square.GroupId < 0) throw new ArgumentException($"Square {index} '{square.Name}': group id cannot be negative.");
                    break;
                case SquareType.Tax:
                    if (square.TaxAmount <= 0) throw new ArgumentException($"Square {index} '{square.Name}': tax must be positive.");
                    break;
            }
        }
    }
}
