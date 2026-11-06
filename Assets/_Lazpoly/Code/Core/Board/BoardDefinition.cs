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
        private static readonly CardDefinition[] NoCards = new CardDefinition[0];

        private readonly SquareDefinition[] _squares;
        private readonly CardDefinition[] _cards;

        /// <param name="cards">The deck card squares draw from; required (and only allowed) when the board has a card square.</param>
        public BoardDefinition(IReadOnlyList<SquareDefinition> squares, IReadOnlyList<CardDefinition> cards = null)
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

            HasPot = Array.Exists(_squares, square => square.Type == SquareType.Pot);
            JailIndex = Array.FindIndex(_squares, square => square.Type == SquareType.Jail);
            if (JailIndex >= 0 && Array.FindLastIndex(_squares, square => square.Type == SquareType.Jail) != JailIndex)
            {
                throw new ArgumentException("A board has at most one jail.", nameof(squares));
            }
            bool hasCardSquare = Array.Exists(_squares, square => square.Type == SquareType.Card);
            _cards = NoCards;
            if (cards != null && cards.Count > 0)
            {
                if (!hasCardSquare) throw new ArgumentException("Cards need a card square to be drawn on.", nameof(cards));
                _cards = new CardDefinition[cards.Count];
                for (int i = 0; i < cards.Count; i++)
                {
                    CardDefinition card = cards[i] ?? throw new ArgumentException($"Card {i} is null.", nameof(cards));
                    ValidateCard(card, i);
                    _cards[i] = card;
                }
            }
            else if (hasCardSquare)
            {
                throw new ArgumentException("A board with a card square needs cards.", nameof(cards));
            }
        }

        /// <summary>The deck of the card squares, in data order; empty when the board has none.</summary>
        public IReadOnlyList<CardDefinition> Cards => _cards;

        /// <summary>Whether taxes and card fines collect in a pot (the board has a pot square) instead of going to the bank.</summary>
        public bool HasPot { get; }

        /// <summary>The jail square, where three doubles in a row send a player; -1 when the board has none.</summary>
        public int JailIndex { get; }

        public bool HasJail => JailIndex >= 0;

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

        private static void ValidateDevelopment(SquareDefinition square, int index)
        {
            if (square.BuildingCost < 0) throw new ArgumentException($"Square {index} '{square.Name}': building cost cannot be negative.");
            if (!square.IsDevelopable)
            {
                if (square.DevelopedRents.Count != 0)
                {
                    throw new ArgumentException($"Square {index} '{square.Name}': building rents need a building cost.");
                }

                return;
            }

            if (square.GroupId == 0) throw new ArgumentException($"Square {index} '{square.Name}': only grouped properties can be developed.");
            if (square.DevelopedRents.Count != SquareDefinition.MaxBuildingLevel)
            {
                throw new ArgumentException(
                    $"Square {index} '{square.Name}': needs {SquareDefinition.MaxBuildingLevel} building rents, got {square.DevelopedRents.Count}.");
            }

            int previous = square.BaseRent;
            foreach (int rent in square.DevelopedRents)
            {
                if (rent < previous) throw new ArgumentException($"Square {index} '{square.Name}': building rents must not decrease.");
                previous = rent;
            }
        }

        private void ValidateCard(CardDefinition card, int index)
        {
            string name = $"Card {index} '{card.Text}'";
            switch (card.Kind)
            {
                case CardKind.Collect:
                case CardKind.Pay:
                    if (card.Amount <= 0) throw new ArgumentException($"{name}: amount must be positive.");
                    break;
                case CardKind.MoveTo:
                    if (card.Target < 0 || card.Target >= _squares.Length) throw new ArgumentException($"{name}: target is outside the board.");
                    // A card never leads to another card, so one landing draws at most one card.
                    if (_squares[card.Target].Type == SquareType.Card) throw new ArgumentException($"{name}: cannot send a player to a card square.");
                    break;
                case CardKind.MoveBy:
                    if (card.Amount == 0 || Math.Abs(card.Amount) >= _squares.Length) throw new ArgumentException($"{name}: steps must be 1 to {_squares.Length - 1} either way.");
                    break;
                case CardKind.PayPerBuilding:
                    if (card.Amount < 0 || card.HotelAmount < 0 || card.Amount + card.HotelAmount == 0)
                    {
                        throw new ArgumentException($"{name}: needs a house or hotel cost.");
                    }

                    break;
                case CardKind.Lookout:
                    break;
                default:
                    throw new ArgumentException($"{name}: unknown kind {card.Kind}.");
            }
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
                    ValidateDevelopment(square, index);
                    if (square.IsResort && square.IsDevelopable) throw new ArgumentException($"Square {index} '{square.Name}': a resort takes no buildings.");
                    break;
                case SquareType.Tax:
                    if (square.TaxAmount <= 0) throw new ArgumentException($"Square {index} '{square.Name}': tax must be positive.");
                    break;
                case SquareType.Travel:
                    if (square.Fee < 0) throw new ArgumentException($"Square {index} '{square.Name}': fee cannot be negative.");
                    break;
                case SquareType.Jail:
                    if (square.Fee <= 0) throw new ArgumentException($"Square {index} '{square.Name}': leaving jail early needs a fee.");
                    break;
            }
        }
    }
}
