using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The sky the table floats in, drawn in code (the owner's wish, 2026-11-11: a backdrop that suits the game's
    /// atmosphere). Two pictures: the sky itself (<see cref="Sky"/>), a deep blue at the bottom of the screen rising to a
    /// pale, faintly warm haze at the top, with a soft glow behind the board, hazy clouds far off and a few small floating
    /// islands in the distance, kin of the islands the money safes stand on; and a layer of nearer clouds
    /// (<see cref="Clouds"/>), see-through between them, that repeats sideways so it can drift slowly past. The clouds
    /// are round puffs on a flat foot, lit warm from the upper left and shaded a cool blue underneath, and they lie along
    /// the bottom and the top of the screen, clear of the middle where the board is. The same numbers every time, so
    /// every table has the same sky.
    /// </summary>
    public static class SkySkin
    {
        /// <summary>The sky at the bottom of the screen, half way up (the approved sky blue), and at the top.</summary>
        public static readonly Color Deep = new Color32(0x7C, 0xBB, 0xE8, 0xFF);
        public static readonly Color Mid = new Color32(0xA2, 0xD7, 0xF7, 0xFF);
        public static readonly Color High = new Color32(0xCF, 0xEA, 0xF8, 0xFF);

        /// <summary>The warm haze towards the top, and how much of it there is at the very top.</summary>
        public static readonly Color Haze = new Color32(0xFF, 0xEC, 0xE0, 0xFF);
        public const float HazeStrength = 0.32f;

        /// <summary>The glow behind the board, and where it sits on the screen (0-1 across and up).</summary>
        public static readonly Color Glow = new Color32(0xEC, 0xF6, 0xFB, 0xFF);
        public static readonly Vector2 GlowCentre = new Vector2(0.5f, 0.56f);

        /// <summary>A cloud's lit top and its shaded foot.</summary>
        public static readonly Color CloudLight = new Color32(0xFF, 0xFD, 0xF7, 0xFF);
        public static readonly Color CloudShade = new Color32(0xBF, 0xD6, 0xEE, 0xFF);

        /// <summary>The far islands' grass and rock before the haze takes them.</summary>
        public static readonly Color FarGrass = new Color32(0x6C, 0x8E, 0x55, 0xFF);
        public static readonly Color FarRock = new Color32(0x9C, 0x8B, 0x77, 0xFF);

        /// <summary>The sky picture's size (16:9), and the cloud layer's (twice as wide as high; it repeats sideways).</summary>
        public const int Width = 1024;
        public const int Height = 576;
        public const int CloudWidth = 1024;
        public const int CloudHeight = 512;

        /// <summary>The bands of the screen's height the near clouds keep to: under the first, above the second.</summary>
        public const float CloudsBelow = 0.35f;
        public const float CloudsAbove = 0.8f;

        // The light the clouds are shaded by: from the upper left, a little towards the eye.
        private static readonly Vector3 Light = new Vector3(-0.38f, 0.74f, 0.55f);

        private static Texture2D _sky, _clouds;

        /// <summary>The sky's colour at a point of the screen (u across, v up, both 0-1) before clouds and islands.</summary>
        public static Color ColourAt(float u, float v)
        {
            Color colour = v < 0.5f ? Color.Lerp(Deep, Mid, Mathf.SmoothStep(0f, 1f, v / 0.5f)) : Color.Lerp(Mid, High, Mathf.SmoothStep(0f, 1f, (v - 0.5f) / 0.5f));
            colour = Color.Lerp(colour, Haze, HazeStrength * Mathf.SmoothStep(0f, 1f, (v - 0.62f) / 0.38f));
            // The glow behind the board, the screen's width counting a little more (the view is wide).
            float dx = (u - GlowCentre.x) * 1.25f, dy = v - GlowCentre.y;
            float distance = Mathf.Sqrt(dx * dx + dy * dy) / 0.7f;
            colour = Color.Lerp(Glow, colour, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distance)));
            // The bottom corners a touch deeper, so the eye is led to the middle.
            float corner = Mathf.Clamp01((Mathf.Abs(u - 0.5f) - 0.25f) / 0.25f) * Mathf.Clamp01((0.4f - v) / 0.4f);
            return Color.Lerp(colour, Deep * 0.93f, 0.5f * corner);
        }

        /// <summary>The sky picture: the gradient and glow, hazy clouds far off, and small islands in the distance.</summary>
        public static Texture2D Sky()
        {
            if (_sky != null) return _sky;
            Color[] canvas = Gradient();
            // Far clouds at the sides, small and nearly gone into the haze.
            foreach (Cloud cloud in FarClouds) Draw(canvas, Width, Height, false, cloud);
            foreach (FarIsland island in FarIslands) Draw(canvas, island);
            return _sky = Picture("sky-backdrop", canvas, Width, Height, TextureWrapMode.Clamp, 7);
        }

        /// <summary>
        /// The sky without clouds or islands, picture sized. It is smooth, so it is worked out on a coarse grid and eased
        /// between the grid's points, which is many times quicker than asking for every pixel.
        /// </summary>
        private static Color[] Gradient()
        {
            const int step = 8;
            int across = Width / step + 2, up = Height / step + 2;
            var coarse = new Color[across * up];
            for (int j = 0; j < up; j++)
            {
                for (int i = 0; i < across; i++) coarse[j * across + i] = ColourAt((float)(i * step) / Width, (float)(j * step) / Height);
            }

            var canvas = new Color[Width * Height];
            for (int y = 0; y < Height; y++)
            {
                float fy = (y + 0.5f) / step;
                int j = (int)fy;
                fy -= j;
                for (int x = 0; x < Width; x++)
                {
                    float fx = (x + 0.5f) / step;
                    int i = (int)fx;
                    fx -= i;
                    Color a = coarse[j * across + i], b = coarse[j * across + i + 1], c = coarse[(j + 1) * across + i], d = coarse[(j + 1) * across + i + 1];
                    float low = 1f - fy, left = 1f - fx;
                    canvas[y * Width + x] = new Color(
                        (a.r * left + b.r * fx) * low + (c.r * left + d.r * fx) * fy,
                        (a.g * left + b.g * fx) * low + (c.g * left + d.g * fx) * fy,
                        (a.b * left + b.b * fx) * low + (c.b * left + d.b * fx) * fy, 1f);
                }
            }

            return canvas;
        }

        /// <summary>The near clouds: see-through between them, repeating sideways.</summary>
        public static Texture2D Clouds()
        {
            if (_clouds != null) return _clouds;
            var canvas = new Color[CloudWidth * CloudHeight];
            Color clear = CloudLight;
            clear.a = 0f;
            for (int i = 0; i < canvas.Length; i++) canvas[i] = clear;
            foreach (Cloud cloud in NearClouds) Draw(canvas, CloudWidth, CloudHeight, true, cloud);
            var picture = Picture("sky-clouds", canvas, CloudWidth, CloudHeight, TextureWrapMode.Repeat, 0);
            return _clouds = picture;
        }

        /// <summary>How much of the cloud layer is cloud at a point of it (u along, wrapping; v up): 0 clear sky, 1 solid cloud.</summary>
        public static float CloudCover(float u, float v)
        {
            float x = (u - Mathf.Floor(u)) * CloudWidth, y = v * CloudHeight;
            float clear = 1f;
            foreach (Cloud cloud in NearClouds)
            {
                Vector3[] puffs = Placed(cloud, CloudHeight);
                for (int turn = -1; turn <= 1; turn++) clear *= 1f - Sample(cloud, puffs, x - turn * CloudWidth, y, CloudHeight, out _);
            }

            return 1f - clear;
        }

        /// <summary>
        /// A cloud: where the middle of its foot is and how wide it is (in picture heights; x along, y up), how solid it
        /// is, how far it has gone into the haze, how soft its edge is (in picture heights), and the seed of its puffs.
        /// </summary>
        public readonly struct Cloud
        {
            public readonly float X, Foot, Size, Solid, Faded, Soft;
            public readonly int Seed;

            public Cloud(float x, float foot, float size, float solid, float faded, float soft, int seed)
            {
                X = x;
                Foot = foot;
                Size = size;
                Solid = solid;
                Faded = faded;
                Soft = soft;
                Seed = seed;
            }
        }

        /// <summary>A far island: its top's middle (u, v of the sky picture), its half width (in picture heights), its haze, its seed.</summary>
        public readonly struct FarIsland
        {
            public readonly float U, V, Half, Faded;
            public readonly int Seed;

            public FarIsland(float u, float v, float half, float faded, int seed)
            {
                U = u;
                V = v;
                Half = half;
                Faded = faded;
                Seed = seed;
            }
        }

        /// <summary>
        /// The near clouds, back to front (x over the layer's two heights of width): a row of small ones along the top, a
        /// middle row behind, and the big ones along the bottom, their feet under the screen's edge.
        /// </summary>
        public static readonly IReadOnlyList<Cloud> NearClouds = new[]
        {
            new Cloud(0.22f, 0.905f, 0.30f, 0.80f, 0.30f, 0.012f, 11),
            new Cloud(0.83f, 0.865f, 0.24f, 0.75f, 0.36f, 0.012f, 12),
            new Cloud(1.34f, 0.915f, 0.34f, 0.80f, 0.30f, 0.012f, 13),
            new Cloud(1.81f, 0.875f, 0.22f, 0.72f, 0.38f, 0.012f, 14),

            new Cloud(0.10f, 0.165f, 0.36f, 0.85f, 0.26f, 0.010f, 21),
            new Cloud(0.62f, 0.205f, 0.30f, 0.82f, 0.30f, 0.010f, 22),
            new Cloud(1.08f, 0.150f, 0.40f, 0.85f, 0.26f, 0.010f, 23),
            new Cloud(1.56f, 0.215f, 0.28f, 0.82f, 0.30f, 0.010f, 24),

            new Cloud(0.36f, -0.050f, 0.62f, 0.96f, 0.06f, 0.007f, 31),
            new Cloud(0.90f, -0.060f, 0.50f, 0.96f, 0.08f, 0.007f, 32),
            new Cloud(1.37f, -0.055f, 0.66f, 0.96f, 0.06f, 0.007f, 33),
            new Cloud(1.88f, -0.065f, 0.52f, 0.96f, 0.08f, 0.007f, 34),
        };

        /// <summary>The far clouds painted into the sky itself (x over the sky picture's 16:9 of width, in heights).</summary>
        public static readonly IReadOnlyList<Cloud> FarClouds = new[]
        {
            new Cloud(0.10f, 0.660f, 0.20f, 0.70f, 0.55f, 0.016f, 41),
            new Cloud(0.36f, 0.745f, 0.13f, 0.60f, 0.62f, 0.016f, 42),
            new Cloud(1.46f, 0.725f, 0.15f, 0.60f, 0.60f, 0.016f, 43),
            new Cloud(1.70f, 0.630f, 0.21f, 0.70f, 0.55f, 0.016f, 44),
            new Cloud(0.20f, 0.430f, 0.16f, 0.60f, 0.58f, 0.016f, 45),
            new Cloud(1.62f, 0.400f, 0.14f, 0.60f, 0.60f, 0.016f, 46),
        };

        /// <summary>The far islands, at the sides of the screen, each smaller and hazier than the one before.</summary>
        public static readonly IReadOnlyList<FarIsland> FarIslands = new[]
        {
            new FarIsland(0.075f, 0.565f, 0.050f, 0.50f, 51),
            new FarIsland(0.925f, 0.520f, 0.040f, 0.56f, 52),
            new FarIsland(0.215f, 0.800f, 0.024f, 0.68f, 53),
            new FarIsland(0.800f, 0.835f, 0.020f, 0.72f, 54),
        };

        /// <summary>The puffs of a cloud, in the cloud's own measure (x across from its middle, y up from its foot, both in widths; then the radius).</summary>
        public static Vector3[] Puffs(int seed)
        {
            var random = new System.Random(seed * 131 + 7);
            float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);
            int along = 6 + seed % 3;
            var puffs = new List<Vector3>();
            for (int i = 0; i < along; i++)
            {
                // A row along the foot, bigger towards the middle.
                float t = along == 1 ? 0.5f : (float)i / (along - 1);
                float bell = Mathf.Sin(t * Mathf.PI);
                float radius = (0.1f + 0.07f * bell) * Range(0.9f, 1.12f);
                puffs.Add(new Vector3((t - 0.5f) * 0.78f + Range(-0.02f, 0.02f), radius * (0.15f + 0.4f * bell) * Range(0.85f, 1.15f), radius));
            }

            // Two or three heaped on top, off the middle, so no two clouds have the same crown.
            int crown = 2 + seed % 2;
            for (int i = 0; i < crown; i++)
            {
                float radius = Range(0.11f, 0.15f);
                puffs.Add(new Vector3(Range(-0.2f, 0.2f), Range(0.17f, 0.25f), radius));
            }

            return puffs.ToArray();
        }

        /// <summary>A cloud's puffs in the pixels of a picture <paramref name="height"/> high (x, y, then the radius).</summary>
        private static Vector3[] Placed(Cloud cloud, int height)
        {
            Vector3[] puffs = Puffs(cloud.Seed);
            float size = cloud.Size * height;
            for (int i = 0; i < puffs.Length; i++) puffs[i] = new Vector3(cloud.X * height + puffs[i].x * size, cloud.Foot * height + puffs[i].y * size, puffs[i].z * size);
            return puffs;
        }

        /// <summary>How solid a cloud is at a point of its picture (pixels), and by <paramref name="tone"/> how lit (0 shade, 1 light).</summary>
        private static float Sample(Cloud cloud, Vector3[] puffs, float x, float y, int height, out float tone)
        {
            tone = 0f;
            float size = cloud.Size * height, soft = Mathf.Max(1f, cloud.Soft * height);
            float foot = cloud.Foot * height;
            if (y < foot - soft) return 0f;
            float clear = 1f, weight = 0f;
            Vector3 normal = Vector3.zero;
            foreach (Vector3 puff in puffs)
            {
                float radius = puff.z;
                float dx = x - puff.x, dy = y - puff.y;
                float reach = radius + soft;
                if (dx >= reach || dx <= -reach || dy >= reach || dy <= -reach) continue;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                if (distance >= reach) continue;
                clear *= 1f - Mathf.SmoothStep(0f, 1f, (radius + soft - distance) / (2f * soft));
                // The puff as a ball: its height here weighs how much its facing counts.
                float inside = 1f - Mathf.Min(1f, distance / radius);
                float w = inside * inside + 1e-4f;
                float nx = dx / radius, ny = dy / radius;
                normal += new Vector3(nx, ny, Mathf.Sqrt(Mathf.Max(0f, 1f - nx * nx - ny * ny))) * w;
                weight += w;
            }

            float cover = 1f - clear;
            if (cover <= 0f) return 0f;
            // The flat foot.
            cover *= Mathf.SmoothStep(0f, 1f, (y - foot + soft) / (2f * soft));
            if (weight > 0f)
            {
                normal = normal * (1f / weight);
                float length = Mathf.Sqrt(Vector3.Dot(normal, normal));
                float lit = length > 1e-5f ? Vector3.Dot(normal, Light) / (length * LightLength) : 0f;
                float rise = Mathf.Clamp01((y - foot) / (size * 0.3f));
                tone = 0.5f * Mathf.SmoothStep(0f, 1f, (lit + 0.25f) / 1.05f) + 0.5f * Mathf.SmoothStep(0f, 1f, rise);
            }

            return cover * cloud.Solid;
        }

        private static readonly float LightLength = Mathf.Sqrt(Vector3.Dot(Light, Light));

        private static void Draw(Color[] canvas, int width, int height, bool wraps, Cloud cloud)
        {
            float size = cloud.Size * height;
            Vector3[] puffs = Placed(cloud, height);
            int left = Mathf.FloorToInt(cloud.X * height - size * 0.65f), right = Mathf.CeilToInt(cloud.X * height + size * 0.65f);
            int low = Mathf.Max(0, Mathf.FloorToInt(cloud.Foot * height - size * 0.05f)), high = Mathf.Min(height - 1, Mathf.CeilToInt(cloud.Foot * height + size * 0.5f));
            for (int y = low; y <= high; y++)
            {
                // Gone a little into the haze: towards the sky behind it at this height.
                Color haze = ColourAt(0.5f, (y + 0.5f) / height);
                for (int column = left; column <= right; column++)
                {
                    int x = column;
                    if (wraps) x = ((column % width) + width) % width;
                    else if (column < 0 || column >= width) continue;
                    float cover = Sample(cloud, puffs, column + 0.5f, y + 0.5f, height, out float tone);
                    if (cover <= 0f) continue;
                    Color colour = Color.Lerp(Color.Lerp(CloudShade, CloudLight, tone), haze, cloud.Faded);
                    int at = y * width + x;
                    canvas[at] = Over(canvas[at], colour, cover);
                }
            }
        }

        private static void Draw(Color[] canvas, FarIsland island)
        {
            var random = new System.Random(island.Seed * 977 + 3);
            float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);
            float half = island.Half * Height, cx = island.U * Width, top = island.V * Height;
            float depth = half * Range(1.5f, 1.9f), lean = Range(-0.25f, 0.25f) * half;
            float waveA = Range(0f, 6.28f), waveB = Range(0f, 6.28f);
            // Two or three trees on top.
            int trees = 2 + island.Seed % 2;
            var crowns = new Vector3[trees];
            for (int i = 0; i < trees; i++) crowns[i] = new Vector3(Range(-0.6f, 0.6f) * half, Range(0.2f, 0.34f) * half, Range(0.16f, 0.24f) * half);
            int low = Mathf.Max(0, Mathf.FloorToInt(top - depth - 2f)), high = Mathf.Min(Height - 1, Mathf.CeilToInt(top + half * 0.7f));
            int left = Mathf.Max(0, Mathf.FloorToInt(cx - half * 1.2f)), right = Mathf.Min(Width - 1, Mathf.CeilToInt(cx + half * 1.2f));
            for (int y = low; y <= high; y++)
            {
                for (int x = left; x <= right; x++)
                {
                    float px = x + 0.5f - cx, py = y + 0.5f - top;
                    Color sky = canvas[y * Width + x];
                    Color colour = sky;
                    float cover = 0f;
                    if (py <= 0f)
                    {
                        // The rock: narrowing to an off-centre point, its edge a little ragged, lit from the left.
                        float t = -py / depth;
                        float narrow = Mathf.Pow(Mathf.Clamp01(1f - t), 0.75f) * (0.93f - 0.1f * t);
                        float ragged = 1f + 0.1f * Mathf.Sin(t * 13f + waveA) + 0.06f * Mathf.Sin(t * 29f + waveB);
                        float reach = half * narrow * ragged, middle = lean * t;
                        float edge = reach - Mathf.Abs(px - middle);
                        cover = Mathf.Clamp01(edge + 0.5f) * Mathf.Clamp01((depth + py) + 0.5f);
                        float side = reach > 0.5f ? Mathf.Clamp01(0.5f + 0.5f * (px - middle) / reach) : 0.5f;
                        float layers = 0.94f + 0.06f * Mathf.Sin(t * 21f + waveB);
                        colour = FarRock * ((1.12f - 0.38f * side) * (1f - 0.3f * t) * layers);
                        // The soil right under the grass.
                        if (t < 0.1f) colour = Color.Lerp(new Color(0.48f, 0.36f, 0.25f) * (1.1f - 0.3f * side), colour, t / 0.1f);
                    }

                    // The grass: a thin cap wider than the rock, drooping a little over its edge.
                    float cap = half * 1.04f - Mathf.Abs(px);
                    float droop = half * (0.07f + 0.04f * Mathf.Sin(px / half * 9f + waveA));
                    if (cap > -0.5f && py > -droop && py <= half * 0.06f)
                    {
                        float grass = Mathf.Clamp01(cap + 0.5f) * Mathf.Clamp01(py + droop + 0.5f) * Mathf.Clamp01(half * 0.06f - py + 0.5f);
                        colour = Color.Lerp(colour, FarGrass * (py > 0f ? 1.12f : 0.95f), grass);
                        cover = Mathf.Max(cover, grass);
                    }

                    foreach (Vector3 crown in crowns)
                    {
                        float dx = px - crown.x, dy = py - crown.y;
                        float inside = crown.z - Mathf.Sqrt(dx * dx + dy * dy);
                        if (inside <= -0.5f) continue;
                        float leaf = Mathf.Clamp01(inside + 0.5f);
                        float lit = Mathf.Clamp01(0.5f + 0.5f * (-dx * 0.5f + dy * 0.8f) / crown.z);
                        colour = Color.Lerp(colour, FarGrass * (0.72f + 0.36f * lit), leaf);
                        cover = Mathf.Max(cover, leaf);
                    }

                    if (cover <= 0f) continue;
                    colour = Color.Lerp(colour, sky, island.Faded);
                    colour.a = 1f;
                    canvas[y * Width + x] = Color.Lerp(sky, colour, cover);
                }
            }
        }

        /// <summary>Lays <paramref name="colour"/> over a pixel by <paramref name="cover"/>, keeping the pixel's own see-through.</summary>
        private static Color Over(Color under, Color colour, float cover)
        {
            float alpha = cover + under.a * (1f - cover);
            if (alpha <= 1e-5f) return under;
            float keep = under.a * (1f - cover) / alpha;
            return new Color(colour.r + (under.r - colour.r) * keep, colour.g + (under.g - colour.g) * keep, colour.b + (under.b - colour.b) * keep, alpha);
        }

        private static Texture2D Picture(string name, Color[] canvas, int width, int height, TextureWrapMode wrap, int ditherSeed)
        {
            var picture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name, wrapMode = wrap, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[canvas.Length];
            // A faint dither on the sky, so its wide gradient shows no bands.
            System.Random random = ditherSeed != 0 ? new System.Random(ditherSeed) : null;
            for (int i = 0; i < canvas.Length; i++)
            {
                Color colour = canvas[i];
                if (random != null)
                {
                    float shift = ((float)random.NextDouble() - 0.5f) / 255f;
                    colour = new Color(colour.r + shift, colour.g + shift, colour.b + shift, colour.a);
                }

                pixels[i] = colour;
            }

            picture.SetPixels32(pixels);
            picture.Apply(false, false);
            return picture;
        }
    }
}
