using System;
using System.Diagnostics;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;

namespace Lazpoly.App.Session
{
    /// <summary>
    /// How long an online game has waited since the last accepted move. The table picks the turn
    /// time (<see cref="TableRules.TurnSeconds"/>; 0 means no clock at all); answers to a trade
    /// offer get at most <see cref="ResponseSeconds"/>. The time source is injected so tests
    /// need no waiting.
    /// </summary>
    public sealed class TurnClock
    {
        /// <summary>The turn time when the table picks none.</summary>
        public const double TurnSeconds = TableRules.DefaultTurnSeconds;

        public const double ResponseSeconds = 30;

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

        /// <summary>Seconds the game allows in a phase; infinite when the table runs no clock.</summary>
        public static double LimitFor(GamePhase phase, int turnSeconds = TableRules.DefaultTurnSeconds)
        {
            if (turnSeconds <= 0) return double.PositiveInfinity;
            bool answer = phase == GamePhase.AwaitingTradeResponse;
            return answer ? Math.Min(ResponseSeconds, turnSeconds) : turnSeconds;
        }

        /// <summary>The clock's reading in seconds, for anything else the session times.</summary>
        public double Now => _now();

        /// <summary>Called whenever the game moves on.</summary>
        public void Restart() => _since = _now();

        public double Remaining(GamePhase phase, int turnSeconds = TableRules.DefaultTurnSeconds) => LimitFor(phase, turnSeconds) - (_now() - _since);

        public bool HasExpired(GamePhase phase, int turnSeconds = TableRules.DefaultTurnSeconds) => Remaining(phase, turnSeconds) <= 0;
    }
}
