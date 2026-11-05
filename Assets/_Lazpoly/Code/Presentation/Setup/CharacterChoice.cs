using System;
using System.Collections.Generic;
using Lazpoly.Infra.Settings;
using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Setup
{
    /// <summary>One of the menu's characters: its kit id, name, accent colour, portrait and the figure it plays with.</summary>
    public sealed class CharacterOption
    {
        public CharacterOption(string id, string name, Color color, CharacterKind figure)
        {
            Id = id;
            Name = name;
            Color = color;
            Figure = figure;
        }

        public string Id { get; }
        public string Name { get; }

        /// <summary>The "SEÇ" line under the portrait.</summary>
        public Color Color { get; }

        /// <summary>The 3D figure that stands for this character on the board.</summary>
        public CharacterKind Figure { get; }

        /// <summary>The portrait's picture in the menu kit.</summary>
        public string Portrait => "Portrait_" + Id;
    }

    /// <summary>
    /// The character the person at this computer picked in the menu (Ece, Ada, Aras or Deniz), kept on this
    /// computer. It names them in the menu's profile card and decides their figure on the board in a game played
    /// on this computer: the women play the business woman, the men the business man.
    /// </summary>
    public sealed class CharacterChoice
    {
        public const string StorageKey = "Lazpoly.Character";

        public static readonly IReadOnlyList<CharacterOption> All = new[]
        {
            new CharacterOption("Woman01", "Ece", new Color(0x92 / 255f, 0x3C / 255f, 0x51 / 255f), CharacterKind.Woman),
            new CharacterOption("Woman02", "Ada", new Color(0x39 / 255f, 0x81 / 255f, 0x8A / 255f), CharacterKind.Woman),
            new CharacterOption("Man01", "Aras", new Color(0x77 / 255f, 0x5C / 255f, 0x83 / 255f), CharacterKind.Man),
            new CharacterOption("Man02", "Deniz", new Color(0x27 / 255f, 0x4B / 255f, 0x70 / 255f), CharacterKind.Man),
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

        public static CharacterOption Find(string id)
        {
            foreach (CharacterOption option in All)
            {
                if (option.Id == id) return option;
            }

            return null;
        }
    }
}
