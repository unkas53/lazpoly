using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Random;
using Lazpoly.Core.State;

namespace Lazpoly.Core.Tests
{
    /// <summary>
    /// Picks a random but legal command for whoever must act, touching every rule: buying,
    /// building, property sales, trades and debts. Test-only; the real AI comes in Phase 5.
    /// </summary>
    internal static class TestBot
    {
        public static GameCommand Next(GameEngine engine, IRandomSource dice)
        {
            GameState state = engine.State;
            BoardDefinition board = engine.Board;
            int me = state.CurrentPlayer.Id;

            switch (state.Phase)
            {
                case GamePhase.AwaitingTradeResponse:
                    int answerer = state.PendingTrade.ToPlayerId;
                    var accept = new AcceptTradeCommand(answerer);
                    if (dice.NextInt(0, 2) == 0 && Legal(engine, accept)) return accept;
                    return dice.NextInt(0, 4) == 0 ? new CancelTradeCommand(state.PendingTrade.FromPlayerId) : (GameCommand)new RejectTradeCommand(answerer);

                case GamePhase.AwaitingDebtPayment:
                    if (Legal(engine, new PayDebtCommand(me))) return new PayDebtCommand(me);
                    GameCommand raise = FirstLegal(engine, square => new SellBuildingCommand(me, square))
                                        ?? FirstLegal(engine, square => new SellPropertyCommand(me, square));
                    return raise ?? new DeclareBankruptcyCommand(me);

                case GamePhase.AwaitingTravelDecision:
                    var ride = new TravelCommand(me, dice.NextInt(0, board.Count));
                    return dice.NextInt(0, 2) == 0 && Legal(engine, ride) ? ride : (GameCommand)new DeclineTravelCommand(me);

                case GamePhase.AwaitingDiceChoice:
                    return new ChooseDiceCommand(me, dice.NextInt(0, 2));

                case GamePhase.AwaitingCupChoice:
                    GameCommand host = dice.NextInt(0, 2) == 0 ? FirstLegal(engine, square => new HostCupCommand(me, square)) : null;
                    return host ?? new DeclineCupCommand(me);

                case GamePhase.AwaitingTakeoverDecision:
                    var take = new TakeOverCommand(me);
                    return dice.NextInt(0, 2) == 0 && Legal(engine, take) ? take : (GameCommand)new DeclineTakeoverCommand(me);

                case GamePhase.AwaitingJailDecision:
                    var pay = new PayJailFeeCommand(me);
                    return dice.NextInt(0, 2) == 0 && Legal(engine, pay) ? pay : (GameCommand)new RollInJailCommand(me);

                case GamePhase.AwaitingBuyDecision:
                    if (Legal(engine, new BuyPropertyCommand(me)) && dice.NextInt(0, 4) != 0) return new BuyPropertyCommand(me);
                    return new DeclinePurchaseCommand(me);

                case GamePhase.WaitingForRoll:
                case GamePhase.TurnEnd:
                    int roll = dice.NextInt(0, 20);
                    GameCommand extra = null;
                    if (roll < 6) extra = FirstLegal(engine, square => new BuildCommand(me, square));
                    else if (roll < 8) extra = FirstLegal(engine, square => new SellPropertyCommand(me, square));
                    else if (roll < 10) extra = FirstLegal(engine, square => new SellBuildingCommand(me, square));
                    else if (roll < 11) extra = ProposeSwap(engine, me);
                    if (extra != null) return extra;
                    return state.Phase == GamePhase.WaitingForRoll ? new RollDiceCommand(me) : (GameCommand)new EndTurnCommand(me);
            }

            return new EndTurnCommand(me);
        }

        private static GameCommand ProposeSwap(GameEngine engine, int me)
        {
            GameState state = engine.State;
            int partner = GameState.NoOwner;
            for (int step = 1; step < state.Players.Count && partner == GameState.NoOwner; step++)
            {
                PlayerState candidate = state.Players[(state.CurrentPlayerIndex + step) % state.Players.Count];
                if (!candidate.IsBankrupt) partner = candidate.Id;
            }

            if (partner == GameState.NoOwner) return null;

            int mine = -1;
            int theirs = -1;
            for (int square = 0; square < engine.Board.Count; square++)
            {
                if (GameRules.HasBuildings(state, square)) continue;
                if (mine < 0 && state.GetOwner(square) == me) mine = square;
                if (theirs < 0 && state.GetOwner(square) == partner) theirs = square;
            }

            var offer = new TradeOffer(me, partner, mine < 0 ? 20 : 0, 0,
                mine < 0 ? new int[0] : new[] { mine }, theirs < 0 ? new int[0] : new[] { theirs });
            var command = new ProposeTradeCommand(offer);
            return Legal(engine, command) ? command : null;
        }

        private static GameCommand FirstLegal(GameEngine engine, System.Func<int, GameCommand> make)
        {
            for (int square = 0; square < engine.Board.Count; square++)
            {
                GameCommand command = make(square);
                if (Legal(engine, command)) return command;
            }

            return null;
        }

        private static bool Legal(GameEngine engine, GameCommand command) => engine.Validate(command) == RejectionReason.None;
    }
}
