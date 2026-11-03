namespace Lazpoly.Presentation.Game
{
    /// <summary>
    /// The decision clock of a local game, ticked by the frame: it starts again whenever the moment it
    /// times changes (a new key: the game moved on, or someone else is to act), stands still while held
    /// (the board animates, the pause menu is open) and reports the moment the time runs out, once.
    /// Online the host keeps the real clock instead.
    /// </summary>
    public sealed class DecisionClock
    {
        private int _key;
        private bool _started;
        private bool _fired;

        /// <summary>Seconds left; infinite when nothing is timed.</summary>
        public double Left { get; private set; } = double.PositiveInfinity;

        /// <param name="seconds">Real seconds since the last tick.</param>
        /// <param name="key">What is being timed; a different key starts the clock again.</param>
        /// <param name="limit">The time allowed; infinite times nothing.</param>
        /// <param name="hold">True stops the clock without resetting it.</param>
        /// <returns>True on the one tick at which the time runs out.</returns>
        public bool Tick(double seconds, int key, double limit, bool hold)
        {
            if (!_started || key != _key)
            {
                _started = true;
                _key = key;
                _fired = false;
                Left = limit;
            }

            if (double.IsInfinity(limit))
            {
                Left = limit;
                return false;
            }

            if (hold || _fired) return false;
            Left -= seconds;
            if (Left > 0) return false;
            Left = 0;
            _fired = true;
            return true;
        }
    }
}
