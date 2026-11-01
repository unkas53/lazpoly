using System;
using System.Diagnostics;
using Lazpoly.Core.State;

namespace Lazpoly.App.Session
{
    /// <summary>
    /// How long an online game has waited since the last accepted move. Answers to an auction or a
    /// trade get less time than a turn. The time source is injected so tests need no waiting.
    /// </summary>
    public sealed class TurnClock
    {
        public const double TurnSeconds = 90;
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

        public static double LimitFor(GamePhase phase) =>
            phase == GamePhase.AuctionInProgress || phase == GamePhase.AwaitingTradeResponse ? ResponseSeconds : TurnSeconds;

        /// <summary>The clock's reading in seconds, for anything else the session times.</summary>
        public double Now => _now();

        /// <summary>Called whenever the game moves on.</summary>
        public void Restart() => _since = _now();

        public double Remaining(GamePhase phase) => LimitFor(phase) - (_now() - _since);

        public bool HasExpired(GamePhase phase) => Remaining(phase) <= 0;
    }
}
