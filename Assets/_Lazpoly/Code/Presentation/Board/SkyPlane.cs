using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// When and where the little aeroplane crosses the sky behind the table (the owner's wish, 2026-11-12: "now and
    /// then"): pure arithmetic on the clock, so it is the same every game and can be tested. A plane sets out every
    /// <see cref="EverySeconds"/> and takes <see cref="CrossSeconds"/> to cross the view in a straight, level line high
    /// in the sky, one pass to the right, the next to the left, each at its own height; in between the sky is empty.
    /// Slow and small, so it is noticed rather than watched: the eye belongs on the board.
    /// </summary>
    public static class SkyPlane
    {
        /// <summary>When the first plane sets out, how often one does after that, and how long a crossing takes, in seconds.</summary>
        public const float FirstAt = 25f;
        public const float EverySeconds = 120f;
        public const float CrossSeconds = 40f;

        /// <summary>The plane's length as a share of the view's height.</summary>
        public const float Size = 0.045f;

        /// <summary>
        /// How far the plane is banked away from the camera, in degrees: seen from the game's slanted view a level plane
        /// shows mostly its top, a cross of wings and body; banked a little, its side and fin show and it reads as a plane.
        /// </summary>
        public const float Bank = 18f;

        /// <summary>How far outside the view's sides a crossing starts and ends, as a share of the view's width: more than half the plane's length, so it never pops in.</summary>
        public const float Margin = 0.04f;

        /// <summary>How high up the view each pass in turn flies (0 bottom, 1 top): in the upper sky, over the far islands and under the screen's top.</summary>
        private static readonly float[] Heights = { 0.83f, 0.77f, 0.87f };

        /// <summary>Which pass the clock is in or after: -1 before the first plane sets out.</summary>
        public static int PassAt(float seconds) => seconds < FirstAt ? -1 : Mathf.FloorToInt((seconds - FirstAt) / EverySeconds);

        /// <summary>How far across the plane is at a time, 0 to 1; -1 while no plane is in the sky.</summary>
        public static float ProgressAt(float seconds)
        {
            int pass = PassAt(seconds);
            if (pass < 0) return -1f;
            float into = seconds - FirstAt - pass * EverySeconds;
            return into < CrossSeconds ? into / CrossSeconds : -1f;
        }

        /// <summary>Whether a pass flies from the left of the view to the right; the next one comes back the other way.</summary>
        public static bool Rightward(int pass) => pass % 2 == 0;

        /// <summary>How high up the view a pass flies.</summary>
        public static float HeightOf(int pass) => Heights[((pass % Heights.Length) + Heights.Length) % Heights.Length];

        /// <summary>Where the plane is on the view (u across, v up) at a point of a pass: from just outside one side to just outside the other.</summary>
        public static Vector2 SpotAt(int pass, float progress)
        {
            float along = Mathf.Lerp(-Margin, 1f + Margin, Mathf.Clamp01(progress));
            return new Vector2(Rightward(pass) ? along : 1f - along, HeightOf(pass));
        }
    }
}
