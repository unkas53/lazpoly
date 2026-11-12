using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// What shows down a money hatch when its lid is open (the owner's wish, 2026-11-12: hatches that please the eye
    /// and suit the atmosphere): darkness, and deep in its middle a faint glimmer of gold, fading softly to black well
    /// before the walls, so the hole reads as deep rather than as a painted patch.
    /// </summary>
    public static class HatchSkin
    {
        public const int Width = 64;
        public const int Height = 48;

        /// <summary>The dark of the hole.</summary>
        public static readonly Color Dark = new Color(0.02f, 0.02f, 0.025f);

        /// <summary>The glimmer at its strongest: a dim, warm gold.</summary>
        public static readonly Color Ember = new Color(0.36f, 0.23f, 0.07f);

        /// <summary>How far from the middle the glimmer reaches, of the way to the hole's edge.</summary>
        public const float Reach = 0.7f;

        private static Texture2D _hole;

        /// <summary>The hole's colour at a point of its picture (0-1 across and along).</summary>
        public static Color ColourAt(float u, float v)
        {
            float x = (Mathf.Clamp01(u) - 0.5f) * 2f, y = (Mathf.Clamp01(v) - 0.5f) * 2f;
            float away = Mathf.Sqrt(x * x + y * y) / Reach;
            float glow = 1f - Mathf.SmoothStep(0f, 1f, away);
            Color colour = Color.Lerp(Dark, Ember, glow * glow);
            colour.a = 1f;
            return colour;
        }

        /// <summary>The hole's picture.</summary>
        public static Texture2D Hole()
        {
            if (_hole != null) return _hole;
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, true)
            {
                name = "hatch-hole",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
            };

            var pixels = new Color32[Width * Height];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++) pixels[y * Width + x] = ColourAt((x + 0.5f) / Width, (y + 0.5f) / Height);
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            _hole = texture;
            return texture;
        }
    }
}
