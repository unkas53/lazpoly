using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Menu
{
    /// <summary>
    /// Where the main menu's parts stand, in the owner's kit's 1920x1080 design (x and y from the top left;
    /// Layout.json, with the ways in the game has that the kit leaves out). The design is one frame scaled to fit the
    /// screen whole (<see cref="FrameScale"/>), over the city background that covers the screen. Left: the profile card,
    /// the logo, the tagline and the buttons; top right: sound and settings; bottom: characters, how to play, settings,
    /// and the line of the board's dearest towns.
    /// </summary>
    public static class MenuLayout
    {
        public static readonly Vector2 Design = new Vector2(1920f, 1080f);

        public static readonly Rect Profile = new Rect(100f, 40f, 310f, 76f);
        public static readonly Rect ProfileDisc = new Rect(115f, 48f, 56f, 56f);

        /// <summary>The profile picture inside the disc's brass rim.</summary>
        public static readonly Rect ProfilePhoto = new Rect(119f, 52f, 48f, 48f);
        public static readonly Rect ProfileName = new Rect(188f, 48f, 204f, 28f);
        public static readonly Rect ProfileCaption = new Rect(188f, 79f, 204f, 20f);

        public static readonly Rect Sound = new Rect(1668f, 40f, 70f, 76f);
        public static readonly Rect SettingsTop = new Rect(1750f, 40f, 70f, 76f);

        /// <summary>The logo, about square: from under the profile card down to the tagline.</summary>
        public static readonly Rect Logo = new Rect(110f, 120f, 610f, 304f);
        public static readonly Rect Tagline = new Rect(140f, 428f, 650f, 46f);
        public static readonly Rect Subtitle = new Rect(140f, 474f, 640f, 32f);

        /// <summary>The big dark button: straight to a table against the computer.</summary>
        public static readonly Rect Play = new Rect(140f, 526f, 580f, 104f);
        public static readonly Rect Host = new Rect(140f, 648f, 280f, 84f);
        public static readonly Rect Join = new Rect(440f, 648f, 280f, 84f);
        public static readonly Rect Single = new Rect(140f, 750f, 280f, 84f);
        public static readonly Rect Local = new Rect(440f, 750f, 280f, 84f);

        /// <summary>Under the buttons: the saved game to go on with, the room still open, or the kit's motto.</summary>
        public static readonly Rect Info = new Rect(140f, 852f, 740f, 72f);

        public static readonly Rect Characters = new Rect(100f, 950f, 250f, 80f);
        public static readonly Rect HowToPlay = new Rect(366f, 950f, 280f, 80f);
        public static readonly Rect SettingsBottom = new Rect(662f, 950f, 210f, 80f);

        public static readonly Rect WorldIcon = new Rect(1250f, 959f, 46f, 46f);
        public static readonly Rect WorldTitle = new Rect(1310f, 948f, 560f, 30f);
        public static readonly Rect WorldTowns = new Rect(1310f, 982f, 560f, 26f);

        /// <summary>Steam's state and the build's version, small under the towns.</summary>
        public static readonly Rect Status = new Rect(1310f, 1012f, 560f, 22f);

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

        /// <summary>The home screen's parts by name, for the test that none overlaps another or leaves the design.</summary>
        public static IReadOnlyDictionary<string, Rect> HomeParts => new Dictionary<string, Rect>
        {
            { nameof(Profile), Profile }, { nameof(Sound), Sound }, { nameof(SettingsTop), SettingsTop },
            { nameof(Logo), Logo }, { nameof(Tagline), Tagline }, { nameof(Subtitle), Subtitle },
            { nameof(Play), Play }, { nameof(Host), Host }, { nameof(Join), Join }, { nameof(Single), Single }, { nameof(Local), Local },
            { nameof(Info), Info }, { nameof(Characters), Characters }, { nameof(HowToPlay), HowToPlay }, { nameof(SettingsBottom), SettingsBottom },
            { nameof(WorldIcon), WorldIcon }, { nameof(WorldTitle), WorldTitle }, { nameof(WorldTowns), WorldTowns }, { nameof(Status), Status },
        };
    }
}
