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
                Sprite sprite = ToSprite(picture);
                if (sprite == null) continue;
                _photos[playerId] = sprite;
                arrived = true;
            }

            if (now >= _giveUpAt) _waiting.Clear(); // No picture (or no Steam): the portrait stays.
            if (arrived) Version++;
            return arrived;
        }

        /// <summary>The rows of a top-row-first picture turned over, as a texture (bottom row first) wants them.</summary>
        public static byte[] FlipRows(byte[] rgba, int width, int height)
        {
            if (rgba == null) throw new ArgumentNullException(nameof(rgba));
            int row = width * 4;
            if (width <= 0 || height <= 0 || rgba.Length != row * height) throw new ArgumentException("Four bytes for every pixel.", nameof(rgba));
            var flipped = new byte[rgba.Length];
            for (int y = 0; y < height; y++) Buffer.BlockCopy(rgba, y * row, flipped, (height - 1 - y) * row, row);
            return flipped;
        }

        /// <summary>A profile picture as a sprite, mipmapped so it stays smooth at the card's small size.</summary>
        public static Sprite ToSprite(AvatarPicture picture)
        {
            if (picture == null) return null;
            var texture = new Texture2D(picture.Width, picture.Height, TextureFormat.RGBA32, true)
            {
                name = "Profile Picture",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
            };
            texture.SetPixelData(FlipRows(picture.Rgba, picture.Width, picture.Height), 0);
            texture.Apply(true, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, picture.Width, picture.Height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
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
