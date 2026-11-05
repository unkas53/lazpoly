using System;

namespace Lazpoly.App.Avatars
{
    /// <summary>A profile picture as plain pixels: <see cref="Width"/> by <see cref="Height"/>, four bytes (red, green, blue, alpha) each, top row first.</summary>
    public sealed class AvatarPicture
    {
        public AvatarPicture(int width, int height, byte[] rgba)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "A picture has a size.");
            if (rgba == null || rgba.Length != width * height * 4) throw new ArgumentException("Four bytes for every pixel.", nameof(rgba));
            Width = width;
            Height = height;
            Rgba = rgba;
        }

        public int Width { get; }
        public int Height { get; }
        public byte[] Rgba { get; }
    }

    /// <summary>
    /// The profile pictures of real players on the platform (Steam), by their platform id, so a person's card at
    /// the table shows their own face rather than a character's.
    /// </summary>
    public interface IPlayerAvatars
    {
        /// <summary>
        /// The player's picture, or null while it is still on its way or when they have none (or the platform is
        /// not running). Asking again later is how the caller learns that it arrived; the platform is asked to
        /// fetch it on the first ask.
        /// </summary>
        AvatarPicture Get(ulong platformId);
    }
}
