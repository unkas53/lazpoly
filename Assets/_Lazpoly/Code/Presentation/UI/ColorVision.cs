using UnityEngine;

namespace Lazpoly.Presentation.UI
{
    /// <summary>
    /// Colours chosen for people who do not see red and green apart. When the setting is on, the
    /// colour groups and the players take palettes built for that (Paul Tol's bright set for the
    /// groups, Okabe and Ito's set for the players) and every group mark carries a letter, so no
    /// two groups are told apart by hue alone. The choice is read once per table, when the board
    /// and the pawns are built; <see cref="Apply"/> is called at scene start, not on every change.
    /// </summary>
    public static class ColorVision
    {
        private static readonly Color[] GroupPalette =
        {
            Hex(0x4477AA), // blue
            Hex(0xEE6677), // red
            Hex(0x228833), // green
            Hex(0xCCBB44), // yellow
            Hex(0x66CCEE), // cyan
            Hex(0xAA3377), // purple
            Hex(0xBBBBBB), // grey
            Hex(0x332288), // indigo
        };

        private static readonly Color[] PlayerPalette =
        {
            Hex(0xE69F00), // orange
            Hex(0x56B4E9), // sky blue
            Hex(0x009E73), // bluish green
            Hex(0xF0E442), // yellow
            Hex(0x0072B2), // blue
            Hex(0xD55E00), // vermillion
            Hex(0xCC79A7), // reddish purple
            Hex(0x333333), // near black
        };

        /// <summary>Whether the friendly palettes and the group letters are in force.</summary>
        public static bool Friendly { get; private set; }

        public static void Apply(bool friendly) => Friendly = friendly;

        /// <summary>The colour a group is drawn in: its own from the board file, or the friendly one for its position in the file.</summary>
        public static Color Group(int ordinal, Color normal) => Friendly ? Pick(GroupPalette, ordinal) : normal;

        /// <summary>The colour a seat is drawn in: the usual one, or the friendly one.</summary>
        public static Color Player(int seat, Color normal) => Friendly ? Pick(PlayerPalette, seat) : normal;

        /// <summary>The letter a group carries on its marks ("A" for the first group in the file), or null while colours alone tell them apart.</summary>
        public static string GroupMark(int ordinal) => Friendly && ordinal >= 0 ? ((char)('A' + ordinal % 26)).ToString() : null;

        /// <summary>Dark text on light swatches, light text on dark ones.</summary>
        public static Color TextOn(Color background) => Luminance(background) > 0.45f ? new Color(0.08f, 0.08f, 0.1f) : new Color(0.97f, 0.96f, 0.93f);

        public static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        /// <summary>How many friendly colours there are before they repeat.</summary>
        public static int PaletteSize => GroupPalette.Length;

        private static Color Pick(Color[] palette, int index) => palette[((index % palette.Length) + palette.Length) % palette.Length];

        private static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
