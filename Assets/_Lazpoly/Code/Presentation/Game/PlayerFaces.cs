using System;
using System.Collections.Generic;
using Lazpoly.App.Avatars;
using Lazpoly.Presentation.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Lazpoly.Presentation.Game
{
    /// <summary>
    /// The face on each player's card. A real person whose platform account is known (<see cref="SeatAccounts"/>)
    /// shows their Steam profile picture once Steam has it; until then, and for everyone else, a character
    /// portrait from the player card kit stands in: the person who set the game up gets the character they picked
    /// in the menu, the other seats take the remaining portraits in turn. <see cref="Version"/> goes up when a
    /// picture arrives, so the cards redraw.
    /// </summary>
    public sealed class PlayerFaces : IDisposable
    {
        /// <summary>The player card kit's four portraits, in the order the seats take them.</summary>
        public static readonly string[] Portraits =
        {
            "Portrait_Woman_01_Bordo", "Portrait_Man_02_Lacivert", "Portrait_Woman_02_Turkuaz", "Portrait_Man_01_Mor",
        };

        /// <summary>How often a picture still on its way is asked for again, and for how long before giving up.</summary>
        public const float PollSeconds = 0.5f;
        public const float GiveUpSeconds = 60f;

        private readonly IPlayerAvatars _avatars;
        private readonly IReadOnlyDictionary<int, ulong> _accounts;
        private readonly int _chooser;
        private readonly string _chooserFace;
        private readonly Dictionary<int, Sprite> _photos = new Dictionary<int, Sprite>();
        private readonly HashSet<int> _waiting = new HashSet<int>();
        private float _nextPoll;
        private float _giveUpAt = float.NaN;

        /// <param name="avatars">The platform's profile pictures; null when there is no platform (a picture is never shown).</param>
        /// <param name="accounts">The account behind each person's seat.</param>
        /// <param name="chooser">The player who plays the character picked in the menu, or GameState.NoOwner.</param>
        /// <param name="chooserFace">That character's portrait.</param>
        public PlayerFaces(IPlayerAvatars avatars, IReadOnlyDictionary<int, ulong> accounts, int chooser, string chooserFace)
        {
            _avatars = avatars;
            _accounts = accounts ?? new Dictionary<int, ulong>();
            _chooser = chooser;
            _chooserFace = chooserFace;
            if (_avatars != null)
            {
                foreach (int playerId in _accounts.Keys) _waiting.Add(playerId);
            }
        }

        /// <summary>Goes up each time a profile picture arrives.</summary>
        public int Version { get; private set; }

        /// <summary>Whether the player's card shows their own profile picture now.</summary>
        public bool HasPhoto(int playerId) => _photos.ContainsKey(playerId);

        /// <summary>The player's face: their profile picture, else their portrait; null only when the kit is missing.</summary>
        public Sprite For(int playerId, int seat) =>
            _photos.TryGetValue(playerId, out Sprite photo) ? photo : KitArt.Get(PortraitFor(playerId, seat, _chooser, _chooserFace));

        /// <summary>
        /// The portrait for a seat (0-based lobby seat): the chooser's own character, else the portraits the chooser
        /// did not take, in turn, so two cards side by side rarely share a face.
        /// </summary>
        public static string PortraitFor(int playerId, int seat, int chooser, string chooserFace)
        {
            bool known = Array.IndexOf(Portraits, chooserFace) >= 0;
            if (playerId == chooser && known) return chooserFace;
            var rest = new List<string>(Portraits);
            if (known) rest.Remove(chooserFace);
            return rest[((seat % rest.Count) + rest.Count) % rest.Count];
        }

        /// <summary>Asks for the pictures still on their way, every <see cref="PollSeconds"/>; true when one arrived.</summary>
        public bool Update(float now)
        {
            if (_waiting.Count == 0 || now < _nextPoll) return false;
            if (float.IsNaN(_giveUpAt)) _giveUpAt = now + GiveUpSeconds;
            _nextPoll = now + PollSeconds;
            bool arrived = false;
            foreach (int playerId in new List<int>(_waiting))
            {
                AvatarPicture picture = _avatars.Get(_accounts[playerId]);
                if (picture == null) continue;
                _waiting.Remove(playerId);
                Sprite sprite = AvatarSprites.ToSprite(picture);
                if (sprite == null) continue;
                _photos[playerId] = sprite;
                arrived = true;
            }

            if (now >= _giveUpAt) _waiting.Clear(); // No picture (or no Steam): the portrait stays.
            if (arrived) Version++;
            return arrived;
        }

        public void Dispose()
        {
            foreach (Sprite sprite in _photos.Values)
            {
                if (sprite == null) continue;
                Destroy(sprite.texture);
                Destroy(sprite);
            }

            _photos.Clear();
            _waiting.Clear();
        }

        private static void Destroy(Object thing)
        {
            if (thing == null) return;
            if (Application.isPlaying) Object.Destroy(thing);
            else Object.DestroyImmediate(thing);
        }
    }
}
