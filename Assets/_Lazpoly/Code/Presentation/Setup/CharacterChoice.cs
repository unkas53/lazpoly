using System;
using System.Collections.Generic;
using Lazpoly.Infra.Settings;
using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Setup
{
    /// <summary>One of the characters: its figure, its name and accent colour, and its two pictures.</summary>
    public sealed class CharacterOption
    {
        public CharacterOption(string id, string name, Color color)
        {
            Id = id;
            Name = name;
            Color = color;
        }

        /// <summary>The figure's id (<see cref="FigureModels.Ids"/>); what the lobby and the game state carry.</summary>
        public string Id { get; }

        public string Name { get; }

        /// <summary>The "SEÇ" line under the portrait.</summary>
        public Color Color { get; }

        /// <summary>The whole figure, for the character cards (Tools/Art/make_figure_portraits.py).</summary>
        public string Portrait => "Figure_" + Id;

        /// <summary>Head and shoulders, for the round avatar on a player card and a lobby seat.</summary>
        public string Face => "Face_" + Id;
    }

    /// <summary>
    /// The characters a player can be, one per figure, and the one the person at this computer likes best (picked
    /// in the menu's "Karakterler" window, kept on this computer). That one is their first pick in every lobby; the
    /// lobby can change it, and no two players at a table play the same character (<see cref="App.Lobby.FigurePicks"/>).
    /// </summary>
    public sealed class CharacterChoice
    {
        public const string StorageKey = "Lazpoly.Character";

        /// <summary>In the order free characters are handed out (to computers, and to seats nobody picked for).</summary>
        public static readonly IReadOnlyList<CharacterOption> All = new[]
        {
            new CharacterOption("hip", "Ece", Hex(0x923C51)),
            new CharacterOption("purple", "Aras", Hex(0x775C83)),
            new CharacterOption("teal", "Ada", Hex(0x39818A)),
            new CharacterOption("navy", "Deniz", Hex(0x274B70)),
            new CharacterOption("mob", "Leyla", Hex(0x6B2235)),
            new CharacterOption("suit", "Kaan", Hex(0x34558F)),
            new CharacterOption("boss", "Baron", Hex(0x8A2A2A)),
            new CharacterOption("cane", "Necdet Bey", Hex(0x5B5148)),
        };

        /// <summary>The characters' ids, in <see cref="All"/>'s order.</summary>
        public static readonly IReadOnlyList<string> Ids = IdsOf(All);

        /// <summary>The menu's characters before Faz 21 by their old ids, so a stored pick carries over.</summary>
        private static readonly Dictionary<string, string> Renamed = new Dictionary<string, string>
        {
            ["Woman01"] = "hip", ["Woman02"] = "teal", ["Man01"] = "purple", ["Man02"] = "navy",
        };

        private readonly ISettingsStorage _storage;

        public CharacterChoice(ISettingsStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        /// <summary>The picked character; the first one until something is picked (or when the stored id is unknown).</summary>
        public CharacterOption Selected => Find(_storage.GetString(StorageKey)) ?? All[0];

        public void Select(string id)
        {
            CharacterOption option = Find(id) ?? throw new ArgumentException($"No character '{id}'.", nameof(id));
            _storage.SetString(StorageKey, option.Id);
            _storage.Save();
        }

        /// <summary>The character with this id (or its old menu id); null when there is none.</summary>
        public static CharacterOption Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (Renamed.TryGetValue(id, out string renamed)) id = renamed;
            foreach (CharacterOption option in All)
            {
                if (option.Id == id) return option;
            }

            return null;
        }

        /// <summary>The character a seat plays when it has none of its own (a game saved before Faz 21): they take turns.</summary>
        public static CharacterOption ForSeat(int seat) => All[((seat % All.Count) + All.Count) % All.Count];

        private static IReadOnlyList<string> IdsOf(IReadOnlyList<CharacterOption> options)
        {
            var ids = new string[options.Count];
            for (int i = 0; i < options.Count; i++) ids[i] = options[i].Id;
            return ids;
        }

        private static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
