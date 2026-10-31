using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>One distinct colour per seat (up to 8 players).</summary>
    public static class PlayerPalette
    {
        private static readonly Color[] Colors =
        {
            new Color(0.90f, 0.22f, 0.21f), // red
            new Color(0.12f, 0.53f, 0.90f), // blue
            new Color(0.26f, 0.63f, 0.28f), // green
            new Color(0.98f, 0.75f, 0.18f), // yellow
            new Color(0.56f, 0.27f, 0.68f), // purple
            new Color(1.00f, 0.44f, 0.00f), // orange
            new Color(0.00f, 0.67f, 0.76f), // teal
            new Color(0.93f, 0.25f, 0.48f), // pink
        };

        public static Color ForSeat(int seatIndex) => Colors[((seatIndex % Colors.Length) + Colors.Length) % Colors.Length];
    }
}
