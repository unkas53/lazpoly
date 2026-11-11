using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The stone of the plate the squares lie on (the owner's wish, 2026-11-11: a stone colour with a professional shading
    /// that suits the table): a warm grey limestone with faint layers and grain, lit along its top lip, a fine seam under
    /// the squares and a soft shade growing towards its foot. Drawn once for a side of the slab
    /// (<see cref="MeshFactory.Slab"/>): u runs along the side over <see cref="Repeat"/> world units and wraps, v up it.
    /// </summary>
    public static class PlateSkin
    {
        /// <summary>The stone, a warm grey that sits with the creams of the squares.</summary>
        public static readonly Color Stone = new Color(0.62f, 0.60f, 0.56f);

        /// <summary>How long a stretch of side the picture covers before it repeats.</summary>
        public const float Repeat = 3f;

        public const int Width = 768;
        public const int Height = 96;

        /// <summary>How dark the foot of the side is against its middle: a soft shade, as if the light came from above.</summary>
        public const float FootShade = 0.26f;

        /// <summary>The lit lip at the top, its height (of the side) and its lift.</summary>
        public const float LipHeight = 0.12f;
        public const float LipLight = 0.07f;

        /// <summary>The thin seam where the squares meet the stone, its height (of the side) and how dark.</summary>
        public const float SeamHeight = 0.035f;
        public const float SeamShade = 0.35f;

        private static Texture2D _cached;

        public static Texture2D Side()
        {
            if (_cached != null) return _cached;
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, true)
            {
                name = "plate-stone",
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8, // the sides are seen at a slant
            };

            var pixels = new Color32[Width * Height];
            for (int y = 0; y < Height; y++)
            {
                float v = (y + 0.5f) / Height;
                for (int x = 0; x < Width; x++)
                {
                    Color colour = ColourAt((x + 0.5f) / Width, v);
                    colour.a = 1f;
                    pixels[y * Width + x] = colour;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            _cached = texture;
            return texture;
        }

        /// <summary>The stone's colour at a point of the picture: u along (0-1, wrapping), v up the side (0 foot, 1 top).</summary>
        public static Color ColourAt(float u, float v)
        {
            Color warm = new Color(Stone.r * 1.04f, Stone.g * 1.005f, Stone.b * 0.94f);
            Color cool = new Color(Stone.r * 0.97f, Stone.g * 0.995f, Stone.b * 1.03f);
            // Big soft clouds of a warmer and a cooler stone.
            float cloud = Noise(u * 6f, v * 1.2f, 6);
            Color colour = Color.Lerp(cool, warm, cloud);
            return colour * Shade(u, v);
        }

        /// <summary>The stone's brightness at a point of the picture, about 1 in the middle of the side.</summary>
        public static float Shade(float u, float v)
        {
            // Faint layers laid in the stone, gently wavy along the side.
            float layer = Noise(u * 4f, v * 7f + 3f * Noise(u * 3f, 0.5f, 3), 4);
            float value = 1f + 0.05f * (layer - 0.5f) * 2f;
            // Grain: fine speckle and the odd darker fleck.
            value *= 0.97f + 0.06f * Noise(u * 270f, v * 30f, 270);
            float fleck = Noise(u * 400f, v * 50f, 400);
            if (fleck > 0.86f) value *= 1f - 0.5f * (fleck - 0.86f);
            // Light from above: a soft shade growing towards the foot.
            float foot = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(v / 0.75f));
            value *= 1f - FootShade * foot;
            // The lit lip under the squares, and the thin seam where they rest on the stone.
            float lip = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((v - (1f - SeamHeight - LipHeight)) / LipHeight));
            value *= 1f + LipLight * lip;
            float seam = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((v - (1f - SeamHeight)) / SeamHeight));
            value *= 1f - SeamShade * seam;
            // A hint of a worn edge at the foot.
            value *= 1f - 0.12f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(v / 0.04f)));
            return value;
        }

        /// <summary>Smooth value noise in 0-1 that repeats every <paramref name="period"/> along x, so the side wraps.</summary>
        private static float Noise(float x, float y, int period)
        {
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            float fx = Mathf.SmoothStep(0f, 1f, x - ix);
            float fy = Mathf.SmoothStep(0f, 1f, y - iy);
            int x0 = Wrap(ix, period), x1 = Wrap(ix + 1, period);
            float top = Mathf.Lerp(Hash(x0, iy + 1), Hash(x1, iy + 1), fx);
            float bottom = Mathf.Lerp(Hash(x0, iy), Hash(x1, iy), fx);
            return Mathf.Lerp(bottom, top, fy);
        }

        private static int Wrap(int value, int period) => period <= 0 ? value : ((value % period) + period) % period;

        private static float Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393) + (uint)(y * 668265263) + 97u;
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / 4294967295f;
        }
    }
}
