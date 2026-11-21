using UnityEngine;

namespace Lazpoly.Presentation.UI
{
    /// <summary>
    /// One set of interface colours. Every screen uses <see cref="Paper"/>, the game's ivory, navy and gold;
    /// <see cref="Dusk"/>, the dark seaside-at-dusk look the HUD was first
    /// drawn in, stays as a second palette. <see cref="UiTheme"/> reads whichever is in force.
    /// </summary>
    public sealed class UiPalette
    {
        public string Name { get; private set; }

        /// <summary>Behind everything: a scene's backdrop when there is no 3D view.</summary>
        public Color Backdrop { get; private set; }

        /// <summary>The dimming over a screen while a window is open.</summary>
        public Color Dim { get; private set; }

        /// <summary>A thinner veil than <see cref="Dim"/>, over the 3D scene behind the main menu.</summary>
        public Color Veil { get; private set; }

        public Color Panel { get; private set; }
        public Color Card { get; private set; }
        public Color Field { get; private set; }
        public Color Text { get; private set; }
        public Color Muted { get; private set; }
        public Color Accent { get; private set; }
        public Color Warning { get; private set; }
        public Color Gain { get; private set; }

        /// <summary>A player's money at rest: warm gold, so it reads as wealth rather than as a loss (<see cref="Warning"/>) or a gain.</summary>
        public Color Money { get; private set; }
        public Color ShadowColor { get; private set; }

        /// <summary>The tinted card of a warning notice.</summary>
        public Color NoticeFill { get; private set; }

        /// <summary>The track of a scrollbar, barely there until the handle moves along it.</summary>
        public Color ScrollTrack { get; private set; }

        public Color PrimaryFill { get; private set; }
        public Color SecondaryFill { get; private set; }
        public Color DangerFill { get; private set; }
        public Color SelectedFill { get; private set; }

        /// <summary>A disc that cannot be pressed right now: a step up from the panel, so the button keeps its shape.</summary>
        public Color RestingFill { get; private set; }

        /// <summary>The label on a primary button.</summary>
        public Color PrimaryLabel { get; private set; }

        /// <summary>The label on a secondary button; the other styles use <see cref="Text"/> or white.</summary>
        public Color SecondaryLabel { get; private set; }

        /// <summary>The label on a selected or danger button.</summary>
        public Color StrongLabel { get; private set; }

        /// <summary>Deep sea blues for the panels, sand for the text, sunset amber as the accent and a teal for anything selected.</summary>
        public static readonly UiPalette Dusk = new UiPalette
        {
            Name = "Dusk",
            Backdrop = new Color(0.05f, 0.09f, 0.13f, 1f),
            Dim = new Color(0.01f, 0.03f, 0.06f, 0.62f),
            Veil = new Color(0.02f, 0.04f, 0.07f, 0.38f),
            Panel = new Color(0.09f, 0.15f, 0.21f, 0.94f),
            Card = new Color(0.14f, 0.22f, 0.30f, 1f),
            Field = new Color(0.06f, 0.11f, 0.16f, 1f),
            Text = new Color(0.96f, 0.93f, 0.86f),
            Muted = new Color(0.66f, 0.72f, 0.76f),
            Accent = new Color(0.98f, 0.70f, 0.24f),
            Warning = new Color(0.96f, 0.47f, 0.40f),
            Gain = new Color(0.48f, 0.84f, 0.56f),
            Money = new Color(0.98f, 0.70f, 0.24f),
            ShadowColor = new Color(0f, 0f, 0f, 0.38f),
            NoticeFill = new Color(0.34f, 0.19f, 0.16f),
            ScrollTrack = new Color(1f, 1f, 1f, 0.07f),
            PrimaryFill = new Color(0.98f, 0.70f, 0.24f),
            SecondaryFill = new Color(0.22f, 0.33f, 0.42f),
            DangerFill = new Color(0.70f, 0.26f, 0.24f),
            SelectedFill = new Color(0.16f, 0.58f, 0.60f),
            RestingFill = new Color(0.18f, 0.26f, 0.33f),
            PrimaryLabel = new Color(0.12f, 0.08f, 0.03f),
            SecondaryLabel = new Color(0.96f, 0.93f, 0.86f),
            StrongLabel = new Color(0.96f, 0.93f, 0.86f),
        };

        /// <summary>
        /// The game's own look (the owner's wish, 2026-11-21: menus and buttons that suit Cash Father), in the logo's
        /// colours: ivory paper windows and cards with deep navy ink, navy for the main action and anything selected,
        /// gold for the trims and the one button a screen is about, a wine red for leaving, all over a navy dimming.
        /// Tools/Art/make_ui_skins.py draws the skins in the same colours.
        /// </summary>
        public static readonly UiPalette Paper = new UiPalette
        {
            Name = "Paper",
            Backdrop = new Color(0.635f, 0.843f, 0.969f, 1f),
            Dim = new Color(0.031f, 0.063f, 0.165f, 0.74f),
            Veil = new Color(0.859f, 0.902f, 0.957f, 0.35f),
            Panel = new Color(1f, 0.9882353f, 0.9529412f, 1f),
            Card = new Color(1f, 0.992f, 0.969f, 1f),
            Field = new Color(0.961f, 0.941f, 0.886f, 1f),
            Text = new Color(0.078f, 0.129f, 0.247f),
            Muted = new Color(0.353f, 0.400f, 0.518f),
            Accent = new Color(0.122f, 0.239f, 0.510f),
            Warning = new Color(0.659f, 0.188f, 0.235f),
            Gain = new Color(0.137f, 0.467f, 0.310f),
            Money = new Color(0.500f, 0.340f, 0.070f),
            ShadowColor = new Color(0.039f, 0.075f, 0.188f, 0.24f),
            NoticeFill = new Color(0.984f, 0.914f, 0.910f),
            ScrollTrack = new Color(0.078f, 0.129f, 0.247f, 0.08f),
            PrimaryFill = new Color(0.106f, 0.196f, 0.439f),
            SecondaryFill = new Color(1f, 0.9882353f, 0.9529412f),
            DangerFill = new Color(0.659f, 0.188f, 0.235f),
            SelectedFill = new Color(0.153f, 0.267f, 0.549f),
            RestingFill = new Color(0.902f, 0.890f, 0.851f),
            PrimaryLabel = new Color(1f, 0.9882353f, 0.9529412f),
            SecondaryLabel = new Color(0.078f, 0.129f, 0.247f),
            StrongLabel = Color.white,
        };

        /// <summary>The design's own named colours, for the parts of the menu that go beyond the theme.</summary>
        public static class Ink
        {
            /// <summary>The masthead and banners: the game's navy, a lighter and a deeper shade.</summary>
            public static readonly Color Masthead = new Color(0.153f, 0.267f, 0.549f);
            public static readonly Color MastheadDeep = new Color(0.082f, 0.165f, 0.369f);

            /// <summary>The deepest navy: the menu's rail and the loading screen's edges.</summary>
            public static readonly Color Night = new Color(0.039f, 0.075f, 0.188f);

            /// <summary>Text on the masthead: ivory, and a grey-blue for the quieter lines.</summary>
            public static readonly Color OnMasthead = new Color(1f, 0.9882353f, 0.9529412f);
            public static readonly Color OnMastheadMuted = new Color(0.761f, 0.800f, 0.894f);

            /// <summary>The game's gold, on its navy buttons' icons and trims; and the gold of the button a screen is about, with the navy written on it.</summary>
            public static readonly Color Gold = new Color(0.949f, 0.780f, 0.361f);
            public static readonly Color HeroFill = new Color(0.976f, 0.745f, 0.251f);
            public static readonly Color HeroLabel = new Color(0.078f, 0.129f, 0.247f);

            /// <summary>The six menu cards' edge stripes: coral, sea blue, lilac, gold, leaf green, steel blue.</summary>
            public static readonly Color[] Stripes =
            {
                new Color(0.933f, 0.439f, 0.333f), new Color(0.318f, 0.659f, 0.812f), new Color(0.659f, 0.545f, 0.769f),
                new Color(0.906f, 0.725f, 0.302f), new Color(0.365f, 0.627f, 0.443f), new Color(0.298f, 0.584f, 0.714f),
            };

            /// <summary>A light wash of a colour, for a chip or a disc behind an icon.</summary>
            public static Color Wash(Color color, float amount = 0.82f)
            {
                Color wash = Color.Lerp(color, Color.white, amount);
                wash.a = 1f;
                return wash;
            }

            /// <summary>A readable ink of a colour: the same hue, dark enough for text on its own wash (4.5:1 even for the gold).</summary>
            public static Color Deep(Color color, float amount = 0.5f)
            {
                Color deep = Color.Lerp(color, Color.black, amount);
                deep.a = 1f;
                return deep;
            }
        }
    }
}
