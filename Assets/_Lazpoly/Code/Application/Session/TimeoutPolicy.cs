using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;

namespace Lazpoly.App.Session
{
    /// <summary>
    /// What the game does for a person whose decision time ran out: the dice are rolled for them, a
    /// town on sale is left to the bank, the turn ends without building, a jailed player rolls for a double for free, a ride or a trade is turned
    /// down, and a debt is covered by the automatic sale (<see cref="DebtSettlement"/>). Local games
    /// and the online host use the same moves.
    /// </summary>
    public static class TimeoutPolicy
    {
        /// <returns>The commands to send in order; empty when nothing waits on the player.</returns>
        public static IReadOnlyList<GameCommand> CommandsFor(BoardDefinition board, RulesConfig rules, GameState state, int playerId)
        {
            switch (state.Phase)
            {
                case GamePhase.AwaitingDebtPayment: return DebtSettlement.Plan(board, rules, state, playerId);
                default:
                    GameCommand command = CommandFor(state, playerId);
                    return command == null ? new GameCommand[0] : new[] { command };
            }
        }

        /// <summary>The single move for every phase but a debt (which takes several sales); null when there is none.</summary>
        public static GameCommand CommandFor(GameState state, int playerId)
        {
            switch (state.Phase)
            {
                case GamePhase.WaitingForRoll: return new RollDiceCommand(playerId);
                case GamePhase.AwaitingBuyDecision: return new DeclinePurchaseCommand(playerId);
                case GamePhase.TurnEnd: return new EndTurnCommand(playerId);
                case GamePhase.AwaitingTradeResponse: return new RejectTradeCommand(playerId);
                case GamePhase.AwaitingTravelDecision: return new DeclineTravelCommand(playerId);
                case GamePhase.AwaitingDiceChoice: return new ChooseDiceCommand(playerId, 0);
                case GamePhase.AwaitingJailDecision: return new RollInJailCommand(playerId);
                default: return null;
            }
        }
    }
}
