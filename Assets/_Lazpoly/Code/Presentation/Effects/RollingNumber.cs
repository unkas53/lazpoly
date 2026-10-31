using System;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// A number on screen that counts towards its real value instead of jumping, so a payment can be
    /// seen happening. It always arrives within <see cref="Seconds"/>, whatever the size of the change.
    /// </summary>
    public sealed class RollingNumber
    {
        public const float Seconds = 0.6f;

        private float _shown;
        private float _from;
        private float _elapsed;

        public RollingNumber(int value)
        {
            Target = value;
            _shown = value;
            _from = value;
            _elapsed = Seconds;
        }

        /// <summary>The real value.</summary>
        public int Target { get; private set; }

        /// <summary>The value to display now.</summary>
        public int Shown => (int)Math.Round(_shown);

        /// <summary>+1 while counting up, -1 while counting down, 0 at rest.</summary>
        public int Direction => Shown == Target ? 0 : Target > Shown ? 1 : -1;

        public void SetTarget(int value)
        {
            if (value == Target) return;
            _from = _shown;
            Target = value;
            _elapsed = 0f;
        }

        /// <summary>Moves on by <paramref name="deltaSeconds"/>; returns true when <see cref="Shown"/> changed.</summary>
        public bool Step(float deltaSeconds)
        {
            int before = Shown;
            _elapsed = Math.Min(Seconds, _elapsed + Math.Max(0f, deltaSeconds));
            _shown = _from + (Target - _from) * Ease.OutCubic(_elapsed / Seconds);
            if (_elapsed >= Seconds) _shown = Target;
            return Shown != before;
        }
    }
}
