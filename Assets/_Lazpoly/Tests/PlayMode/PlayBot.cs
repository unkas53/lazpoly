using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;

namespace Lazpoly.PlayTests
{
    /// <summary>
    /// Picks a legal command for whoever has to act next, so scene tests can drive a game
    /// through every phase: buys when it can, lets trades lapse, settles debts.
    /// </summary>
    internal static class PlayBot
    {
        public static GameCommand Next(BoardDefinition board, RulesConfig rules, GameState state)
        {
            int me = state.CurrentPlayer.Id;
            bool Legal(GameCommand command) => GameRules.Validate(board, rules, state, command) == RejectionReason.None;

            switch (state.Phase)
            {
                case GamePhase.WaitingForRoll:
                    return new RollDiceCommand(me);
                case GamePhase.AwaitingBuyDecision:
                    var buy = new BuyPropertyCommand(me);
                    return Legal(buy) ? buy : (GameCommand)new DeclinePurchaseCommand(me);
                case GamePhase.AwaitingTradeResponse:
                    return new RejectTradeCommand(state.PendingTrade.ToPlayerId);
                case GamePhase.AwaitingTravelDecision:
                    return new DeclineTravelCommand(me);
                case GamePhase.AwaitingDiceChoice:
                    return new ChooseDiceCommand(me, 1);
                case GamePhase.AwaitingDebtPayment:
                    if (Legal(new PayDebtCommand(me))) return new PayDebtCommand(me);
                    for (int square = 0; square < board.Count; square++)
                    {
                        var sell = new SellBuildingCommand(me, square);
                        if (Legal(sell)) return sell;
                    }

                    for (int square = 0; square < board.Count; square++)
                    {
                        var sale = new SellPropertyCommand(me, square);
                        if (Legal(sale)) return sale;
                    }

                    return new DeclareBankruptcyCommand(me);
            }

            return new EndTurnCommand(me);
        }
    }
}
