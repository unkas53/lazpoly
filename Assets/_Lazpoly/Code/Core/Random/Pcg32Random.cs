using System;

namespace Lazpoly.Core.Random
{
    /// <summary>
    /// PCG32 (XSH-RR) generator by Melissa O'Neill. Small, fast and fully deterministic:
    /// the same seed and stream always yield the same sequence on every platform,
    /// which makes seeded game-scenario tests and state snapshots possible.
    /// </summary>
    public sealed class Pcg32Random : IRandomSource
    {
        private const ulong Multiplier = 6364136223846793005UL;
        private const ulong DefaultStream = 54UL;

        private ulong _state;
        private readonly ulong _increment;

        /// <summary>Creates a generator from a seed and an optional stream selector.</summary>
        public Pcg32Random(ulong seed, ulong stream = DefaultStream)
        {
            _state = 0UL;
            _increment = (stream << 1) | 1UL;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        private Pcg32Random(ulong state, ulong increment, bool _)
        {
            _state = state;
            _increment = increment;
        }

        /// <summary>Internal state, for saving the generator inside a game snapshot.</summary>
        public ulong State => _state;

        /// <summary>Stream increment, for saving the generator inside a game snapshot.</summary>
        public ulong Increment => _increment;

        /// <summary>Restores a generator from values previously read from <see cref="State"/> and <see cref="Increment"/>.</summary>
        public static Pcg32Random FromState(ulong state, ulong increment)
        {
            if ((increment & 1UL) == 0UL)
            {
                throw new ArgumentException("PCG32 increment must be odd.", nameof(increment));
            }

            return new Pcg32Random(state, increment, true);
        }

        public uint NextUInt()
        {
            ulong oldState = _state;
            _state = unchecked(oldState * Multiplier + _increment);
            uint xorShifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
            int rotation = (int)(oldState >> 59);
            return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive),
                    "maxExclusive must be greater than minInclusive.");
            }

            uint range = (uint)((long)maxExclusive - minInclusive);

            // Rejection sampling removes modulo bias so every face of a die is equally likely.
            uint threshold = unchecked(0u - range) % range;
            while (true)
            {
                uint value = NextUInt();
                if (value >= threshold)
                {
                    return (int)(minInclusive + (long)(value % range));
                }
            }
        }
    }
}
