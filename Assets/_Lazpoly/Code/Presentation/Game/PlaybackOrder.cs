using System.Collections.Generic;
using Lazpoly.Core.Events;

namespace Lazpoly.Presentation.Game
{
    /// <summary>
    /// The order the table shows a batch of events in, where it differs from the order the rules applied them (the state is
    /// already final either way). An accepted trade's result card comes before its money and towns change hands (the
    /// owner's wish, Faz 29), though the rules settle the trade first and announce the result last. Pure, so it is tested
    /// without a scene.
    /// </summary>
    public static class PlaybackOrder
    {
        /// <summary><paramref name="events"/> with each accepted trade's result moved in front of the trade's own steps.</summary>
        public static List<GameEvent> For(IReadOnlyList<GameEvent> events)
        {
            var order = new List<GameEvent>(events ?? new GameEvent[0]);
            for (int i = 0; i < order.Count; i++)
            {
                if (!(order[i] is TradeResolvedEvent resolved) || resolved.Outcome != TradeOutcome.Accepted) continue;
                int start = FirstStepOfTrade(order, i);
                if (start == i) continue;
                order.RemoveAt(i);
                order.Insert(start, resolved);
            }

            return order;
        }

        /// <summary>
        /// Where the trade settled just before <paramref name="result"/> starts: the run of trade money and owner changes
        /// right before it, between the same two players.
        /// </summary>
        private static int FirstStepOfTrade(List<GameEvent> order, int result)
        {
            var players = new HashSet<int>();
            int start = result;
            while (start > 0 && IsTradeStep(order[start - 1], players)) start--;
            return start;
        }

        private static bool IsTradeStep(GameEvent gameEvent, HashSet<int> players)
        {
            switch (gameEvent)
            {
                case MoneyChangedEvent money when money.Reason == MoneyReason.Trade:
                    return Between(players, money.PlayerId);
                case PropertyOwnerChangedEvent owner:
                    return Between(players, owner.PreviousOwnerId, owner.NewOwnerId);
                default:
                    return false;
            }
        }

        /// <summary>True while every player seen in the run is one of at most two; adds the new ones.</summary>
        private static bool Between(HashSet<int> players, params int[] ids)
        {
            var together = new HashSet<int>(players);
            foreach (int id in ids) together.Add(id);
            if (together.Count > 2) return false;
            players.UnionWith(together);
            return true;
        }
    }
}
