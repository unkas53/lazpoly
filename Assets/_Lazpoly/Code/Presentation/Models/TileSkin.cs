using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The printed look of a square: a soft paper grain and a thin frame line a little inside the
    /// edge, drawn once into a small texture that every tile shares. The tile's own colour tints it,
    /// so one texture serves pastel properties and special squares alike.
    /// </summary>
    public static class TileSkin
    {
        public const int Size = 128;

        /// <summary>How far the paper's brightness wanders: a few percent, a grain rather than a pattern.</summary>
        public const float Grain = 0.045f;

        /// <summary>Where the frame line runs, as a fraction of the tile's size in from each edge.</summary>
        public const float FrameInset = 0.07f;

        /// <summary>Half the frame line's thickness, as a fraction of the tile's size.</summary>
        public const float FrameWidth = 0.008f;

        /// <summary>How much darker the frame line is than the paper around it.</summary>
        public const float FrameShade = 0.85f;

        private static Texture2D _paper;

        /// <summary>The shared paper texture, made on first use.</summary>
        public static Texture2D Paper()
        {
            if (_paper == null) _paper = Make(Size);
            return _paper;
        }

        public static Texture2D Make(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "tile-paper",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    byte value = (byte)Mathf.RoundToInt(255f * Value((x + 0.5f) / size, (y + 0.5f) / size));
                    pixels[y * size + x] = new Color32(value, value, value, 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            return texture;
        }

        /// <summary>The paper's brightness (0-1) at a point of the tile, u along and v across, both 0-1.</summary>
        public static float Value(float u, float v)
        {
            float paper = 1f - Grain * (0.6f * Noise(u * 9f, v * 9f) + 0.4f * Noise(u * 23f, v * 23f));
            float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            if (Mathf.Abs(edge - FrameInset) <= FrameWidth) paper *= FrameShade;
            return paper;
        }

        /// <summary>Smooth value noise in 0-1: a random value at every whole number, blended between.</summary>
        private static float Noise(float x, float y)
        {
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            float fx = Mathf.SmoothStep(0f, 1f, x - ix);
            float fy = Mathf.SmoothStep(0f, 1f, y - iy);
            float top = Mathf.Lerp(Hash(ix, iy + 1), Hash(ix + 1, iy + 1), fx);
            float bottom = Mathf.Lerp(Hash(ix, iy), Hash(ix + 1, iy), fx);
            return Mathf.Lerp(bottom, top, fy);
        }

        private static float Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393) + (uint)(y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / 4294967295f;
        }
    }
}
