using System;

namespace Lazpoly.App.Session
{
    /// <summary>
    /// The game clock where the rules run (the owner's rule, Faz 28): it adds up the time played, frame by frame, and says
    /// when to tell the game about it (<see cref="Core.Engine.GameEngine.AdvanceClock"/>): every <see cref="ReportSeconds"/>
    /// seconds, and at once when the table's time limit is reached. The caller feeds it only while the game runs (a paused
    /// local game does not count). Pure, so it is tested without waiting.
    /// </summary>
    public sealed class GameTimer
    {
        /// <summary>How often the time played goes into the game: often enough for saves and the other machines, rarely enough to stay cheap.</summary>
        public const int ReportSeconds = 10;

        /// <summary>The most one frame may add: a long hitch (a loading stall, the window being dragged) is not play time.</summary>
        public const double MaxStepSeconds = 1.0;

        private double _played;
        private int _reported;

        /// <param name="playedSeconds">Time already played, as the state says (a resumed game goes on from there).</param>
        public GameTimer(int playedSeconds)
        {
            if (playedSeconds < 0) throw new ArgumentOutOfRangeException(nameof(playedSeconds));
            _played = playedSeconds;
            _reported = playedSeconds;
        }

        /// <summary>Seconds played so far, with the fraction not reported yet.</summary>
        public double Played => _played;

        /// <summary>
        /// Adds <paramref name="seconds"/> of play. Returns the whole seconds played when the game should hear of them now,
        /// otherwise -1. <paramref name="limitSeconds"/> is the table's time limit (0: none), reported the moment it is reached.
        /// </summary>
        public int Add(double seconds, int limitSeconds)
        {
            if (!(seconds > 0)) return -1;
            _played += Math.Min(seconds, MaxStepSeconds);
            int whole = (int)Math.Floor(_played);
            bool limitReached = limitSeconds > 0 && whole >= limitSeconds && _reported < limitSeconds;
            if (whole - _reported < ReportSeconds && !limitReached) return -1;
            _reported = whole;
            return whole;
        }
    }
}
