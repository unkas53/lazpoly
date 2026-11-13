using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// When and where the aeroplane crosses the sky behind the table (the owner's wishes: "now and then", 2026-11-12;
    /// flying at the game's isometric angle, a jet with a trail behind it, 2026-11-13): pure arithmetic on the clock, so
    /// it is the same every game and can be tested. A plane sets out every <see cref="EverySeconds"/> and is on its way
    /// for <see cref="CrossSeconds"/>; in between the sky is empty. It flies level in the world along one of the
    /// board's own two directions, a half right angle off the camera's (<see cref="TurnOf"/>), so the slanted view
    /// shows it three-quarters on and its path is a slope on the screen, alongside the board's far edges
    /// (<see cref="WayOf"/>): up one upper corner of the sky away from the camera, or down it towards the camera, the
    /// left corner and the right by turns. Each pass meets the side of the view at its own height
    /// (<see cref="EdgeHeightOf"/>), over the far islands. Behind it lies its trail (<see cref="TrailLength"/>), so a
    /// pass lasts until the trail too has left the view. Slow and small, so it is noticed rather than watched: the
    /// eye belongs on the board.
    /// </summary>
    public static class SkyPlane
    {
        /// <summary>When the first plane sets out, how often one does after that, and how long one is on its way, in seconds.</summary>
        public const float FirstAt = 25f;
        public const float EverySeconds = 120f;
        public const float CrossSeconds = 32f;

        /// <summary>The plane's length as a share of the view's height.</summary>
        public const float Size = 0.06f;

        /// <summary>How far outside the view a pass starts, and how far outside it the trail's end is when the pass is over, as a share of the view's height: more than half the plane's length, so nothing pops in or out.</summary>
        public const float Margin = 0.04f;

        /// <summary>The trail behind the plane, in plane lengths: how far back it reaches before it has faded away, and how wide its picture is.</summary>
        public const float TrailLength = 7f;
        public const float TrailWidth = 0.9f;

        /// <summary>How many different passes there are before they come round again.</summary>
        public const int Passes = 4;

        // Each pass in turn: which way the plane heads, in degrees from the camera's own heading (see TurnOf), and how
        // high up the view's side its path meets it (0 bottom, 1 top).
        private static readonly float[] Turns = { 45f, -45f, -135f, 135f };
        private static readonly float[] EdgeHeights = { 0.74f, 0.72f, 0.76f, 0.73f };

        /// <summary>Which pass the clock is in or after: -1 before the first plane sets out.</summary>
        public static int PassAt(float seconds) => seconds < FirstAt ? -1 : Mathf.FloorToInt((seconds - FirstAt) / EverySeconds);

        /// <summary>How far along its way the plane is at a time, 0 to 1; -1 while no plane is in the sky.</summary>
        public static float ProgressAt(float seconds)
        {
            int pass = PassAt(seconds);
            if (pass < 0) return -1f;
            float into = seconds - FirstAt - pass * EverySeconds;
            return into < CrossSeconds ? into / CrossSeconds : -1f;
        }

        private static int Kind(int pass) => ((pass % Passes) + Passes) % Passes;

        /// <summary>
        /// Which way a pass heads in the world, in degrees to the right of where the camera looks: always a half right
        /// angle off it, which is along the board's edges. 45 and -45 fly away from the camera, up the screen to the
        /// right and to the left; 135 and -135 come towards it, down the screen.
        /// </summary>
        public static float TurnOf(int pass) => Turns[Kind(pass)];

        /// <summary>Whether a pass flies away from the camera, up the screen, rather than towards it and down.</summary>
        public static bool Climbing(int pass) => Mathf.Abs(TurnOf(pass)) < 90f;

        /// <summary>Whether a pass keeps to the left upper corner of the sky rather than the right.</summary>
        public static bool OnTheLeft(int pass) => Climbing(pass) == TurnOf(pass) > 0f;

        /// <summary>How high up the side of the view a pass's path meets it.</summary>
        public static float EdgeHeightOf(int pass) => EdgeHeights[Kind(pass)];

        /// <summary>
        /// Which way a pass moves on the screen (x to the right, y up; of length 1) for a camera looking down by
        /// <paramref name="pitch"/> degrees: level flight a half right angle off the camera's heading shows as a slope,
        /// the same slope as the board's edges.
        /// </summary>
        public static Vector2 WayOf(int pass, float pitch = BoardCameraRig.Pitch)
        {
            float turn = TurnOf(pass) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(turn), Mathf.Cos(turn) * Mathf.Sin(pitch * Mathf.Deg2Rad)).normalized;
        }

        /// <summary>
        /// Where a pass's path enters the view and where it leaves it, in view heights from the view's middle, for a
        /// view <paramref name="aspect"/> wide for its height: one end on the view's side, the other on its top.
        /// </summary>
        public static void Ends(int pass, float aspect, out Vector2 from, out Vector2 to)
        {
            Vector2 way = WayOf(pass);
            Vector2 up = Climbing(pass) ? way : -way;
            var side = new Vector2((OnTheLeft(pass) ? -0.5f : 0.5f) * aspect, EdgeHeightOf(pass) - 0.5f);
            Vector2 top = side + up * ((0.5f - side.y) / up.y);
            from = Climbing(pass) ? side : top;
            to = Climbing(pass) ? top : side;
        }

        /// <summary>
        /// Where the plane is on the view (u across, v up) at a point of a pass: from just outside the view where its
        /// path enters it, to so far outside where it leaves that the trail's end has gone too.
        /// </summary>
        public static Vector2 SpotAt(int pass, float progress, float aspect)
        {
            Ends(pass, aspect, out Vector2 from, out Vector2 to);
            Vector2 way = WayOf(pass);
            Vector2 spot = Vector2.Lerp(from - way * Margin, to + way * (Margin + TrailLength * Size), Mathf.Clamp01(progress));
            return new Vector2(spot.x / aspect + 0.5f, spot.y + 0.5f);
        }

        /// <summary>How far the plane flies in a pass, in view heights.</summary>
        public static float Distance(int pass, float aspect)
        {
            Ends(pass, aspect, out Vector2 from, out Vector2 to);
            return (to - from).magnitude + 2f * Margin + TrailLength * Size;
        }
    }
}
