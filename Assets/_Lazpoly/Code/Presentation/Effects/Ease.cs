using System;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>Easing curves for animations: each maps 0..1 time to 0..1 progress (some overshoot on purpose).</summary>
    public static class Ease
    {
        private static float Clamp(float t) => t < 0f ? 0f : t > 1f ? 1f : t;

        public static float OutCubic(float t)
        {
            float u = 1f - Clamp(t);
            return 1f - u * u * u;
        }

        public static float InCubic(float t)
        {
            t = Clamp(t);
            return t * t * t;
        }

        /// <summary>Slow at both ends, quick in the middle.</summary>
        public static float InOutCubic(float t)
        {
            t = Clamp(t);
            return t < 0.5f ? 4f * t * t * t : 1f - 4f * (1f - t) * (1f - t) * (1f - t);
        }

        /// <summary>Goes a little past 1 and settles back, like something pressed into place.</summary>
        public static float OutBack(float t)
        {
            const float s = 1.70158f;
            float u = Clamp(t) - 1f;
            return 1f + u * u * ((s + 1f) * u + s);
        }

        /// <summary>A falling object hitting the table and bouncing twice before it rests.</summary>
        public static float OutBounce(float t)
        {
            t = Clamp(t);
            const float n = 7.5625f;
            const float d = 2.75f;
            if (t < 1f / d) return n * t * t;
            if (t < 2f / d)
            {
                t -= 1.5f / d;
                return n * t * t + 0.75f;
            }

            if (t < 2.5f / d)
            {
                t -= 2.25f / d;
                return n * t * t + 0.9375f;
            }

            t -= 2.625f / d;
            return n * t * t + 0.984375f;
        }

        /// <summary>0 at both ends and 1 in the middle: for a swell that comes back, such as a pulse.</summary>
        public static float Bump(float t) => (float)Math.Sin(Clamp(t) * Math.PI);
    }
}
