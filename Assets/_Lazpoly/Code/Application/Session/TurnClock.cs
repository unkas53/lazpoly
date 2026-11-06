using System;
using System.Diagnostics;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;

namespace Lazpoly.App.Session
{
    /// <summary>
    /// How long the game waits on a person's decision. The table picks the decision time
    /// (<see cref="TableRules.TurnSeconds"/>; 0 means no clock at all): rolling, buying, building,
    /// riding, picking a lookout roll and a turn in jail get that long; selling to pay a debt and answering a trade
    /// get <see cref="LongDecisionFactor"/> times as long. The time source is injected so tests need no waiting.
    /// </summary>
    public sealed class TurnClock
    {
        /// <summary>The decision time when the table picks none.</summary>
        public const int DecisionSeconds = TableRules.DefaultTurnSeconds;

        /// <summary>
        /// The longest decision time used. Tables from before the decision clock stored a whole move's
        /// time (60-120 s) in the same place; they count as this.
        /// </summary>
        public const int MaxDecisionSeconds = 30;

        /// <summary>Selling towns to cover a debt and weighing a trade take longer than a roll.</summary>
        public const int LongDecisionFactor = 2;

        private readonly Func<double> _now;
        private double _since;

        /// <param name="now">Seconds from any fixed point; null uses <see cref="SystemClock"/>.</param>
        public TurnClock(Func<double> now = null)
        {
            _now = now ?? SystemClock();
            _since = _now();
        }

        /// <summary>A monotonic clock that starts at zero now; unaffected by the system time being changed.</summary>
        public static Func<double> SystemClock()
        {
            Stopwatch watch = Stopwatch.StartNew();
            return () => watch.Elapsed.TotalSeconds;
        }

        /// <summary>Seconds the game allows for a decision in a phase; infinite when the table runs no clock.</summary>
        public static double LimitFor(GamePhase phase, int turnSeconds = TableRules.DefaultTurnSeconds)
        {
            if (turnSeconds <= 0) return double.PositiveInfinity;
            int decision = Math.Min(turnSeconds, MaxDecisionSeconds);
            bool long_ = phase == GamePhase.AwaitingDebtPayment || phase == GamePhase.AwaitingTradeResponse;
            return long_ ? decision * LongDecisionFactor : decision;
        }

        /// <summary>The clock's reading in seconds, for anything else the session times.</summary>
        public double Now => _now();

        /// <summary>Called whenever the game moves on.</summary>
        public void Restart() => _since = _now();

        public double Remaining(GamePhase phase, int turnSeconds = TableRules.DefaultTurnSeconds) => LimitFor(phase, turnSeconds) - (_now() - _since);

        /// <param name="grace">Extra seconds past the limit before it counts as run out.</param>
        public bool HasExpired(GamePhase phase, int turnSeconds = TableRules.DefaultTurnSeconds, double grace = 0) =>
            Remaining(phase, turnSeconds) + grace <= 0;
    }
}
