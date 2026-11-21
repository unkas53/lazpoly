using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Menu
{
    /// <summary>
    /// Where the main menu's parts stand, in a 1920x1080 design (x and y from the top left). The design is a frame
    /// scaled to fit the screen whole (<see cref="FrameScale"/>): the left one, kept at the screen's left edge, holds the
    /// navy rail (<see cref="RailEdge"/>) with the profile, the logo, the tagline and the ways into a game; the right
    /// one, kept at the right edge, holds the small buttons at the top and the line of the board's dearest towns at
    /// the bottom. Between them the game's own table floats in its sky.
    /// </summary>
    public static class MenuLayout
    {
        public static readonly Vector2 Design = new Vector2(1920f, 1080f);

        /// <summary>The rail's right edge leans: this far from the left at the top of the design, and at its bottom.</summary>
        public const float RailTop = 790f;
        public const float RailBottom = 650f;

        /// <summary>How wide the rail's gold edge is, and how far inside it the hairline runs.</summary>
        public const float RailEdgeWidth = 6f;
        public const float RailHairline = 20f;

        /// <summary>Where the rail's right edge is at a height of the design (y from the top).</summary>
        public static float RailEdge(float y) => Mathf.LerpUnclamped(RailTop, RailBottom, y / Design.y);

        /// <summary>How far the rail's edge leans from upright, in degrees (its top to the right).</summary>
        public static float RailLean => Mathf.Atan2(RailTop - RailBottom, Design.y) * Mathf.Rad2Deg;

        /// <summary>The share of a screen's width the rail takes at the middle of its height, for a canvas of a size.</summary>
        public static float RailShare(Vector2 canvas) =>
            canvas.x <= 0f ? 0f : Mathf.Clamp01(RailEdge(Design.y / 2f) * FrameScale(canvas) / canvas.x);

        public static readonly Rect Profile = new Rect(64f, 36f, 330f, 76f);
        public static readonly Rect ProfileDisc = new Rect(74f, 46f, 56f, 56f);

        /// <summary>The profile picture inside the disc's gold rim.</summary>
        public static readonly Rect ProfilePhoto = new Rect(78f, 50f, 48f, 48f);
        public static readonly Rect ProfileName = new Rect(144f, 45f, 238f, 30f);
        public static readonly Rect ProfileCaption = new Rect(144f, 76f, 238f, 22f);

        /// <summary>The logo, about square, with the light behind it; the tagline under it.</summary>
        public static readonly Rect Logo = new Rect(150f, 114f, 420f, 420f);
        public static readonly Rect Tagline = new Rect(60f, 538f, 600f, 40f);
        public static readonly Rect Subtitle = new Rect(60f, 578f, 600f, 30f);

        /// <summary>The big gold button: straight to a table against the computer.</summary>
        public static readonly Rect Play = new Rect(100f, 628f, 520f, 108f);
        public static readonly Rect Host = new Rect(100f, 752f, 252f, 80f);
        public static readonly Rect Join = new Rect(368f, 752f, 252f, 80f);
        public static readonly Rect Single = new Rect(100f, 844f, 252f, 80f);
        public static readonly Rect Local = new Rect(368f, 844f, 252f, 80f);

        /// <summary>Under the buttons: the saved game to go on with, the room still open, or the motto.</summary>
        public static readonly Rect Info = new Rect(100f, 938f, 520f, 84f);

        /// <summary>Steam's state and the build's version, small at the rail's foot.</summary>
        public static readonly Rect Status = new Rect(100f, 1034f, 520f, 24f);

        /// <summary>The top right: the three windows and the sound.</summary>
        public static readonly Rect Characters = new Rect(1148f, 36f, 212f, 68f);
        public static readonly Rect HowToPlay = new Rect(1372f, 36f, 236f, 68f);
        public static readonly Rect Settings = new Rect(1620f, 36f, 160f, 68f);
        public static readonly Rect Sound = new Rect(1792f, 36f, 68f, 68f);

        /// <summary>The bottom right, on the sky: the line of the board's dearest towns.</summary>
        public static readonly Rect WorldTitle = new Rect(1300f, 978f, 560f, 30f);
        public static readonly Rect WorldTowns = new Rect(1300f, 1010f, 560f, 26f);

        /// <summary>The room's lobby on a wide paper sheet over the menu: its columns need the width.</summary>
        public static readonly Rect Sheet = new Rect(90f, 60f, 1740f, 960f);

        /// <summary>A mode's setup (one player, one computer, joining or hosting a room) on a window-sized sheet in the middle, like the kit's windows.</summary>
        public static readonly Rect SetupSheet = new Rect(490f, 90f, 940f, 900f);

        /// <summary>Gap between the lobby's columns on the sheet.</summary>
        public const float CardGap = 24f;

        /// <summary>The character window and its parts: eight cards in two rows of four.</summary>
        public static readonly Rect CharacterPanel = new Rect(410f, 150f, 1100f, 770f);
        public const int CharacterCardsPerRow = 4;
        public const float CharacterCardWidth = 230f;
        public const float CharacterCardHeight = 268f;
        public const float CharacterCardGap = 20f;
        public const float CharacterRowGap = 16f;
        public const float CharacterCardsTop = 140f;

        /// <summary>Where the card of the character at <paramref name="index"/> sits in the window.</summary>
        public static Vector2 CharacterCard(int index) => new Vector2(
            40f + index % CharacterCardsPerRow * (CharacterCardWidth + CharacterCardGap),
            CharacterCardsTop + index / CharacterCardsPerRow * (CharacterCardHeight + CharacterRowGap));

        /// <summary>The design frame's scale on a canvas of this size: the whole design shows, centred, never cropped.</summary>
        public static float FrameScale(Vector2 canvas) => Mathf.Min(canvas.x / Design.x, canvas.y / Design.y);

        /// <summary>The parts of the home screen that stand on the rail: each must stay left of its leaning edge.</summary>
        public static IReadOnlyDictionary<string, Rect> RailParts => new Dictionary<string, Rect>
        {
            { nameof(Profile), Profile }, { nameof(Logo), Logo }, { nameof(Tagline), Tagline }, { nameof(Subtitle), Subtitle },
            { nameof(Play), Play }, { nameof(Host), Host }, { nameof(Join), Join }, { nameof(Single), Single }, { nameof(Local), Local },
            { nameof(Info), Info }, { nameof(Status), Status },
        };

        /// <summary>The home screen's parts by name, for the test that none overlaps another or leaves the design.</summary>
        public static IReadOnlyDictionary<string, Rect> HomeParts
        {
            get
            {
                var parts = new Dictionary<string, Rect>
                {
                    { nameof(Characters), Characters }, { nameof(HowToPlay), HowToPlay }, { nameof(Settings), Settings }, { nameof(Sound), Sound },
                    { nameof(WorldTitle), WorldTitle }, { nameof(WorldTowns), WorldTowns },
                };
                foreach (KeyValuePair<string, Rect> part in RailParts) parts.Add(part.Key, part.Value);
                return parts;
            }
        }
    }
}
