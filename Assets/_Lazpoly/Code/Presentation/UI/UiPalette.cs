using UnityEngine;

namespace Lazpoly.Presentation.UI
{
    /// <summary>
    /// One set of interface colours. Every screen uses <see cref="Paper"/>, the light "paper window on a
    /// sky" look of the menu design; <see cref="Dusk"/>, the dark seaside-at-dusk look the HUD was first
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
        /// The menu design: cream paper panels with dark green ink, a forest-green masthead, leaf green for
        /// the main action, a deep coral for leaving and sea blue for anything selected, all on a morning sky.
        /// </summary>
        public static readonly UiPalette Paper = new UiPalette
        {
            Name = "Paper",
            Backdrop = new Color(0.62f, 0.82f, 0.91f, 1f),
            Dim = new Color(0.10f, 0.22f, 0.20f, 0.55f),
            Veil = new Color(0.62f, 0.82f, 0.91f, 0.35f),
            Panel = new Color(0.984f, 0.973f, 0.925f, 1f),
            Card = new Color(1f, 0.992f, 0.969f, 1f),
            Field = new Color(0.945f, 0.929f, 0.875f, 1f),
            Text = new Color(0.153f, 0.294f, 0.263f),
            Muted = new Color(0.459f, 0.533f, 0.486f),
            Accent = new Color(0.871f, 0.380f, 0.286f),
            Warning = new Color(0.871f, 0.380f, 0.286f),
            Gain = new Color(0.302f, 0.604f, 0.380f),
            ShadowColor = new Color(0.19f, 0.33f, 0.32f, 0.20f),
            NoticeFill = new Color(0.992f, 0.898f, 0.851f),
            ScrollTrack = new Color(0.153f, 0.294f, 0.263f, 0.08f),
            PrimaryFill = new Color(0.380f, 0.620f, 0.380f),
            SecondaryFill = new Color(0.945f, 0.929f, 0.875f),
            DangerFill = new Color(0.871f, 0.380f, 0.286f),
            SelectedFill = new Color(0.196f, 0.451f, 0.412f),
            RestingFill = new Color(0.90f, 0.89f, 0.84f),
            PrimaryLabel = Color.white,
            SecondaryLabel = new Color(0.153f, 0.294f, 0.263f),
            StrongLabel = Color.white,
        };

        /// <summary>The design's own named colours, for the parts of the menu that go beyond the theme.</summary>
        public static class Ink
        {
            /// <summary>The masthead: forest green, lighter at the top.</summary>
            public static readonly Color Masthead = new Color(0.212f, 0.475f, 0.412f);
            public static readonly Color MastheadDeep = new Color(0.137f, 0.337f, 0.306f);

            /// <summary>Text on the masthead.</summary>
            public static readonly Color OnMasthead = new Color(1f, 0.976f, 0.898f);
            public static readonly Color OnMastheadMuted = new Color(0.741f, 0.859f, 0.769f);

            /// <summary>The brand mark's gold.</summary>
            public static readonly Color Gold = new Color(0.961f, 0.816f, 0.431f);

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
