using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The pictures laid over a floating island (<see cref="IslandShape"/>), drawn in code. The body's picture
    /// (<see cref="Rock"/>) runs round the island and from its point up to its top: warm brown soil under the grass,
    /// shaded where the grass hangs over it, then limestone of the board's own stone in layers, each ledge with a lit lip
    /// and a dark seam under it, darkening on the way down and taking a little of the sky's blue at the point, as far
    /// things do in haze. The grass's picture (<see cref="Grass"/>) is a strip from the lawn's light green at the top to
    /// a deep green at the tips of the hanging tongues.
    /// </summary>
    public static class IslandSkin
    {
        /// <summary>The soil under the grass.</summary>
        public static readonly Color Soil = new Color(0.47f, 0.34f, 0.23f);

        /// <summary>The rock: the board's stone, a little warmer, and the darker stone of every other layer.</summary>
        public static readonly Color Stone = new Color(PlateSkin.Stone.r * 1.1f, PlateSkin.Stone.g * 1.04f, PlateSkin.Stone.b * 0.96f);
        public static readonly Color StoneDark = new Color(0.52f, 0.47f, 0.42f);

        /// <summary>The sky's haze the point takes on, and how much of it at the very point.</summary>
        public static readonly Color Haze = new Color(0.64f, 0.79f, 0.92f);
        public const float HazeStrength = 0.24f;

        /// <summary>How much darker the rock is at the point than under the soil.</summary>
        public const float FootShade = 0.24f;

        /// <summary>The grass at the tips of the hanging tongues.</summary>
        public static readonly Color GrassTip = new Color(LawnSkin.Deep.r * 0.74f, LawnSkin.Deep.g * 0.78f, LawnSkin.Deep.b * 0.74f);

        public const int RockSize = 256;
        public const int GrassHeight = 64;

        private static Texture2D _rock, _grass;

        /// <summary>The body's colour at a point of its picture: u round the island (0-1, wrapping), v from the point (0) to the top (1).</summary>
        public static Color RockAt(float u, float v)
        {
            float depth = (1f - Mathf.Clamp01(v)) * IslandShape.Depth;
            float grain = Noise(u * 96f, v * 96f, 96);
            if (depth < IslandShape.SoilDepth)
            {
                // Soil: shaded right under the grass, crumbly, with a few pale pebbles and dark root threads.
                float shade = 0.72f + 0.28f * Mathf.SmoothStep(0f, 1f, depth / 0.14f);
                float crumbs = 0.9f + 0.2f * Noise(u * 40f, v * 60f, 40);
                Color soil = Soil * (shade * crumbs);
                float pebble = Noise(u * 64f + 9f, v * 90f, 64);
                if (pebble > 0.8f) soil = Color.Lerp(soil, Stone * 0.95f, Mathf.Clamp01((pebble - 0.8f) / 0.1f) * 0.8f);
                float thread = Noise(u * 56f + 3f, v * 6f, 56);
                if (thread > 0.82f) soil = soil * (1f - 0.35f * Mathf.Clamp01((thread - 0.82f) / 0.08f));
                // Darker where it meets the rock.
                soil = soil * (1f - 0.25f * Mathf.SmoothStep(0f, 1f, (depth - (IslandShape.SoilDepth - 0.05f)) / 0.05f));
                soil.a = 1f;
                return soil;
            }

            // Which layer of rock, and how far down it.
            int layer = 0;
            while (layer < IslandShape.Ledges.Length - 1 && depth >= IslandShape.Ledges[layer + 1]) layer++;
            float from = IslandShape.Ledges[layer], to = layer + 1 < IslandShape.Ledges.Length ? IslandShape.Ledges[layer + 1] : IslandShape.Depth;
            float within = Mathf.Clamp01((depth - from) / (to - from));
            Color rock = Color.Lerp(layer % 2 == 0 ? Stone : StoneDark, layer % 2 == 0 ? StoneDark : Stone, 0.25f * Noise(u * 5f + layer, v * 3f, 5));
            // Big soft patches of a warmer and a cooler stone, and thin beds laid in it, gently wavy round the island.
            float patch = Noise(u * 7f, v * 5f + 1.7f, 7);
            rock = new Color(rock.r * (0.97f + 0.07f * patch), rock.g, rock.b * (1.03f - 0.07f * patch));
            float beds = Noise(u * 6f, v * 34f + 2.5f * Noise(u * 4f, 0.5f, 4), 6);
            float value = 1f + 0.09f * (beds - 0.5f) * 2f;
            value *= 0.96f + 0.08f * grain;
            // The ledge's lit lip, and the dark seam where it is set in under the layer above.
            value *= 1f + 0.1f * (1f - Mathf.SmoothStep(0f, 1f, within / 0.22f));
            if (layer > 0) value *= 1f - 0.25f * (1f - Mathf.SmoothStep(0f, 1f, within / 0.06f));
            value *= 1f - 0.2f * Mathf.SmoothStep(0f, 1f, (within - 0.72f) / 0.28f);
            // A crack here and there, running down the face.
            float crack = Noise(u * 22f + 5f, v * 4f, 22);
            if (crack > 0.86f) value *= 1f - 0.3f * Mathf.Clamp01((crack - 0.86f) / 0.06f);
            // Darker towards the point, and a little of the sky's haze on it.
            float down = Mathf.Clamp01((depth - IslandShape.SoilDepth) / (IslandShape.Depth - IslandShape.SoilDepth));
            value *= 1f - FootShade * Mathf.SmoothStep(0f, 1f, down);
            rock = rock * value;
            rock = Color.Lerp(rock, Haze, HazeStrength * Mathf.SmoothStep(0f, 1f, (down - 0.35f) / 0.65f));
            rock.a = 1f;
            return rock;
        }

        /// <summary>The grass's colour up its strip: v 1 the meadow's middle, 0.7 its edge, 0 the tips of the hanging tongues.</summary>
        public static Color GrassAt(float v)
        {
            v = Mathf.Clamp01(v);
            Color colour = v >= 0.7f
                ? Color.Lerp(Color.Lerp(LawnSkin.Light, LawnSkin.Deep, 0.35f), LawnSkin.Light, Mathf.SmoothStep(0f, 1f, (v - 0.7f) / 0.3f))
                : Color.Lerp(GrassTip, Color.Lerp(LawnSkin.Light, LawnSkin.Deep, 0.35f), Mathf.SmoothStep(0f, 1f, v / 0.7f));
            colour.a = 1f;
            return colour;
        }

        /// <summary>The body's picture; it repeats round the island.</summary>
        public static Texture2D Rock()
        {
            if (_rock != null) return _rock;
            var texture = new Texture2D(RockSize, RockSize, TextureFormat.RGBA32, true)
            {
                name = "island-rock", wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear,
                anisoLevel = 4, hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[RockSize * RockSize];
            for (int y = 0; y < RockSize; y++)
            {
                for (int x = 0; x < RockSize; x++) pixels[y * RockSize + x] = RockAt((x + 0.5f) / RockSize, (y + 0.5f) / RockSize);
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            return _rock = texture;
        }

        /// <summary>The grass's strip.</summary>
        public static Texture2D Grass()
        {
            if (_grass != null) return _grass;
            var texture = new Texture2D(4, GrassHeight, TextureFormat.RGBA32, false)
            {
                name = "island-grass", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[4 * GrassHeight];
            for (int y = 0; y < GrassHeight; y++)
            {
                Color32 colour = GrassAt((y + 0.5f) / GrassHeight);
                for (int x = 0; x < 4; x++) pixels[y * 4 + x] = colour;
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return _grass = texture;
        }

        /// <summary>Smooth value noise in 0-1 that repeats every <paramref name="period"/> along x, so the picture wraps round the island.</summary>
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
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
