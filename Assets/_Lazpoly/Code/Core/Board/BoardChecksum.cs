using System;
using Lazpoly.Core.Rules;

namespace Lazpoly.Core.Board
{
    /// <summary>
    /// 64-bit FNV-1a digest of a board and its rules. Host and clients compare it before a game so
    /// that two builds with different game data never play together.
    /// </summary>
    public static class BoardChecksum
    {
        public static ulong Compute(BoardDefinition board, RulesConfig rules)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (rules == null) throw new ArgumentNullException(nameof(rules));

            ulong hash = 14695981039346656037UL;

            void MixByte(byte value)
            {
                unchecked
                {
                    hash ^= value;
                    hash *= 1099511628211UL;
                }
            }

            void Mix(int value)
            {
                for (int shift = 0; shift < 32; shift += 8) MixByte((byte)(value >> shift));
            }

            void MixText(string text)
            {
                Mix(text.Length);
                foreach (char c in text) Mix(c);
            }

            Mix(rules.StartingMoney);
            Mix(rules.PassStartBonus);
            Mix(rules.DiceSides);
            Mix(rules.MaxConsecutiveDoubles);
            Mix(rules.FullGroupRentMultiplier);
            Mix(rules.BuildingSellbackPercent);

            Mix(board.Count);
            for (int i = 0; i < board.Count; i++)
            {
                SquareDefinition square = board[i];
                MixText(square.Name);
                Mix((int)square.Type);
                Mix(square.Price);
                Mix(square.BaseRent);
                Mix(square.GroupId);
                Mix(square.TaxAmount);
                Mix(square.BuildingCost);
                Mix(square.DevelopedRents.Count);
                foreach (int rent in square.DevelopedRents) Mix(rent);
                Mix(square.Fee);
                Mix(square.IsResort ? 1 : 0);
            }

            Mix(board.Cards.Count);
            foreach (CardDefinition card in board.Cards)
            {
                MixText(card.Text);
                Mix((int)card.Kind);
                Mix(card.Amount);
                Mix(card.Target);
                Mix(card.HotelAmount);
            }

            return hash;
        }
    }
}
