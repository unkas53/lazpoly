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
    /// shows their Steam profile picture once Steam has it; until then, and for everyone else, the face of the
    /// character they play (Setup.CharacterOption.Face) stands in. <see cref="Version"/> goes up when a
    /// picture arrives, so the cards redraw.
    /// </summary>
    public sealed class PlayerFaces : IDisposable
    {
        /// <summary>How often a picture still on its way is asked for again, and for how long before giving up.</summary>
        public const float PollSeconds = 0.5f;
        public const float GiveUpSeconds = 60f;

        private readonly IPlayerAvatars _avatars;
        private readonly IReadOnlyDictionary<int, ulong> _accounts;
        private readonly IReadOnlyDictionary<int, string> _portraits;
        private readonly Dictionary<int, Sprite> _photos = new Dictionary<int, Sprite>();
        private readonly HashSet<int> _waiting = new HashSet<int>();
        private float _nextPoll;
        private float _giveUpAt = float.NaN;

        /// <param name="avatars">The platform's profile pictures; null when there is no platform (a picture is never shown).</param>
        /// <param name="accounts">The account behind each person's seat.</param>
        /// <param name="portraits">The picture of each player's character (a KitArt name), by player id.</param>
        public PlayerFaces(IPlayerAvatars avatars, IReadOnlyDictionary<int, ulong> accounts, IReadOnlyDictionary<int, string> portraits)
        {
            _avatars = avatars;
            _accounts = accounts ?? new Dictionary<int, ulong>();
            _portraits = portraits ?? new Dictionary<int, string>();
            if (_avatars != null)
            {
                foreach (int playerId in _accounts.Keys) _waiting.Add(playerId);
            }
        }

        /// <summary>Goes up each time a profile picture arrives.</summary>
        public int Version { get; private set; }

        /// <summary>Whether the player's card shows their own profile picture now.</summary>
        public bool HasPhoto(int playerId) => _photos.ContainsKey(playerId);

        /// <summary>The player's face: their profile picture, else their character's; null when neither is there.</summary>
        public Sprite For(int playerId) =>
            _photos.TryGetValue(playerId, out Sprite photo) ? photo
            : _portraits.TryGetValue(playerId, out string portrait) ? KitArt.Get(portrait) : null;

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
