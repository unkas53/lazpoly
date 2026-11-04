using System;
using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;

namespace Lazpoly.Presentation.Game
{
    /// <summary>
    /// The towns a player in debt has ticked on the board to sell, and what selling them sends: a sale
    /// for each ticked town that is still theirs and, when the money then covers the debt, the payment.
    /// Pure; the host still validates every command.
    /// </summary>
    public sealed class DebtSale
    {
        private readonly List<int> _picked = new List<int>();

        /// <summary>The ticked towns, in the order they were ticked.</summary>
        public IReadOnlyList<int> Picked => _picked;

        /// <summary>Goes up on every change, so a view knows when to redraw.</summary>
        public int Version { get; private set; }

        public bool IsPicked(int square) => _picked.Contains(square);

        /// <summary>Ticks a town, or unticks it when it was ticked.</summary>
        public void Toggle(int square)
        {
            if (!_picked.Remove(square)) _picked.Add(square);
            Version++;
        }

        public void Set(int square, bool picked)
        {
            if (picked != IsPicked(square)) Toggle(square);
        }

        public void Clear()
        {
            if (_picked.Count == 0) return;
            _picked.Clear();
            Version++;
        }

        /// <summary>Drops the ticks on towns that are no longer a choice (sold, or the debt is over).</summary>
        public void Keep(Predicate<int> stillChoice)
        {
            if (stillChoice == null) throw new ArgumentNullException(nameof(stillChoice));
            if (_picked.RemoveAll(square => !stillChoice(square)) > 0) Version++;
        }

        /// <summary>What the bank pays for the ticked towns that are still the player's (their buildings included).</summary>
        public int Proceeds(BoardDefinition board, RulesConfig rules, GameState state, int playerId)
        {
            int total = 0;
            foreach (int square in _picked)
            {
                if (Sellable(board, state, playerId, square)) total += GameRules.PropertySaleProceeds(board, rules, state, square);
            }

            return total;
        }

        /// <summary>
        /// The sales of the ticked towns, then the payment when the money after them covers the debt.
        /// Empty when nothing is ticked or the player owes nothing now.
        /// </summary>
        public IReadOnlyList<GameCommand> Commands(BoardDefinition board, RulesConfig rules, GameState state, int playerId)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (state == null) throw new ArgumentNullException(nameof(state));

            var commands = new List<GameCommand>();
            DebtState debt = state.Debt;
            PlayerState player = state.FindPlayer(playerId);
            if (state.Phase != GamePhase.AwaitingDebtPayment || debt == null || player == null || debt.PlayerId != playerId) return commands;

            foreach (int square in _picked)
            {
                if (Sellable(board, state, playerId, square)) commands.Add(new SellPropertyCommand(playerId, square));
            }

            if (commands.Count > 0 && player.Money + Proceeds(board, rules, state, playerId) >= debt.Amount)
            {
                commands.Add(new PayDebtCommand(playerId));
            }

            return commands;
        }

        private static bool Sellable(BoardDefinition board, GameState state, int playerId, int square) =>
            square >= 0 && square < board.Count && board[square].Type == SquareType.Property && state.GetOwner(square) == playerId;
    }
}
