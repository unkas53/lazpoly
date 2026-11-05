using System;
using Lazpoly.App.Avatars;
using UnityEngine;

namespace Lazpoly.Presentation.UI
{
    /// <summary>A platform profile picture (<see cref="AvatarPicture"/>) as a sprite for the interface: a player's card at the table, the menu's profile card.</summary>
    public static class AvatarSprites
    {
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

        /// <summary>A profile picture as a sprite, mipmapped so it stays smooth at a small size; null for no picture.</summary>
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
    }
}
