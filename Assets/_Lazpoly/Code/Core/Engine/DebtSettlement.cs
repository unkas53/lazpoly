using System;
using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;

namespace Lazpoly.Core.Engine
{
    /// <summary>
    /// The automatic sale on the debt screen ("Otomatik Sat", and what happens when its time runs out):
    /// the towns to sell back to the bank and the payment, as the commands a player would send. Pure;
    /// the host still validates each command.
    /// </summary>
    public static class DebtSettlement
    {
        /// <summary>
        /// Sells as little as it can, keeping what is worth most: unbuilt towns before built ones, and of
        /// those the cheapest sale that covers what is still missing, else the cheapest sale, until the
        /// debt is covered; then pays. A debt nothing could cover is given up (bankruptcy).
        /// Empty when the player owes nothing now.
        /// </summary>
        public static IReadOnlyList<GameCommand> Plan(BoardDefinition board, RulesConfig rules, GameState state, int playerId)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (state == null) throw new ArgumentNullException(nameof(state));

            var commands = new List<GameCommand>();
            DebtState debt = state.Debt;
            PlayerState player = state.FindPlayer(playerId);
            if (state.Phase != GamePhase.AwaitingDebtPayment || debt == null || player == null || debt.PlayerId != playerId) return commands;

            if (GameRules.MaxRaisableMoney(board, rules, state, playerId) < debt.Amount)
            {
                commands.Add(new DeclareBankruptcyCommand(playerId));
                return commands;
            }

            var towns = new List<int>();
            for (int i = 0; i < board.Count; i++)
            {
                if (board[i].Type == SquareType.Property && state.GetOwner(i) == playerId) towns.Add(i);
            }

            // Unbuilt first, then by what the bank pays; the board order settles ties.
            towns.Sort((a, b) =>
            {
                int built = GameRules.HasBuildings(state, a).CompareTo(GameRules.HasBuildings(state, b));
                if (built != 0) return built;
                int value = Proceeds(board, rules, state, a).CompareTo(Proceeds(board, rules, state, b));
                return value != 0 ? value : a.CompareTo(b);
            });

            int money = player.Money;
            while (money < debt.Amount && towns.Count > 0)
            {
                int missing = debt.Amount - money;
                int pick = towns.FindIndex(square => Proceeds(board, rules, state, square) >= missing);
                if (pick < 0) pick = 0;
                int sold = towns[pick];
                towns.RemoveAt(pick);
                money += Proceeds(board, rules, state, sold);
                commands.Add(new SellPropertyCommand(playerId, sold));
            }

            commands.Add(new PayDebtCommand(playerId));
            return commands;
        }

        /// <summary>The towns the plan sells, in order; for the debt screen's summary.</summary>
        public static IReadOnlyList<int> SquaresSold(IReadOnlyList<GameCommand> plan)
        {
            var squares = new List<int>();
            foreach (GameCommand command in plan)
            {
                if (command is SellPropertyCommand sale) squares.Add(sale.SquareIndex);
            }

            return squares;
        }

        private static int Proceeds(BoardDefinition board, RulesConfig rules, GameState state, int square) =>
            GameRules.PropertySaleProceeds(board, rules, state, square);
    }
}
