using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The sky the table floats in, drawn in code (the owner's wishes, 2026-11-11: a backdrop that suits the game's
    /// atmosphere, then calm and natural rather than drawn, since the eye belongs on the board in the middle). The sky
    /// itself (<see cref="Sky"/>) is a deep blue at the bottom of the screen rising to a pale, faintly warm haze at the
    /// top, with a soft glow behind the board. Over it lie two layers of soft cloud (<see cref="Clouds"/>): a bank along
    /// the bottom of the screen and thin wisps along the top, clear of the middle; the far layer hazier and reaching a
    /// little higher than the near one. Each repeats sideways, so the two can drift past at their own slow speeds. And
    /// far off, two small floating islands gone into the haze (<see cref="FarIslandPicture"/>), kin of the islands the
    /// money safes stand on. The same numbers every time, so every table has the same sky.
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

        /// <summary>A cloud's lit side and its shaded side: close to each other, so the clouds stay soft.</summary>
        public static readonly Color CloudLight = new Color32(0xFD, 0xFD, 0xFA, 0xFF);
        public static readonly Color CloudShade = new Color32(0xCC, 0xDE, 0xF0, 0xFF);

        /// <summary>How solid the near and the far clouds are at their thickest, and how far the far ones have gone into the sky's colour.</summary>
        public const float NearSolid = 0.9f;
        public const float FarSolid = 0.62f;
        public const float FarFaded = 0.35f;

        /// <summary>How high up the screen the near and the far bank of cloud reach, and how solid the wisps along the top are.</summary>
        public const float NearReach = 0.25f;
        public const float FarReach = 0.34f;
        public const float WispSolid = 0.3f;

        /// <summary>The far islands' grass and rock before the haze takes them, the haze's own blue, and how much of them it leaves.</summary>
        public static readonly Color FarGrass = new Color32(0x6C, 0x8E, 0x55, 0xFF);
        public static readonly Color FarRock = new Color32(0x9C, 0x8B, 0x77, 0xFF);
        public static readonly Color FarHaze = new Color32(0xA9, 0xD3, 0xF0, 0xFF);
        public const float FarIslandSolid = 0.6f;

        /// <summary>The sky picture's size (16:9), and a cloud layer's (twice as wide as high; it repeats sideways). Small: all of it is soft.</summary>
        public const int Width = 512;
        public const int Height = 288;
        public const int CloudWidth = 512;
        public const int CloudHeight = 256;

        /// <summary>A far island's picture.</summary>
        public const int IslandWidth = 96;
        public const int IslandHeight = 128;

        /// <summary>The bands of the screen's height the clouds keep to: under the first, above the second.</summary>
        public const float CloudsBelow = 0.36f;
        public const float CloudsAbove = 0.8f;

        // How many cells of noise run along a cloud layer before it repeats, for the near and the far layer.
        private const int NearCells = 5;
        private const int FarCells = 8;

        private static Texture2D _sky, _near, _far;
        private static readonly Texture2D[] IslandPictures = new Texture2D[2];

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
            colour = Color.Lerp(colour, Deep * 0.93f, 0.5f * corner);
            colour.a = 1f;
            return colour;
        }

        /// <summary>The sky picture: the gradient and the glow, nothing else.</summary>
        public static Texture2D Sky()
        {
            if (_sky != null) return _sky;
            var canvas = new Color[Width * Height];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++) canvas[y * Width + x] = ColourAt((x + 0.5f) / Width, (y + 0.5f) / Height);
            }

            return _sky = Picture("sky-backdrop", canvas, Width, Height, TextureWrapMode.Clamp, 7);
        }

        /// <summary>A layer of cloud, the near one or the far: see-through between the clouds, repeating sideways.</summary>
        public static Texture2D Clouds(bool near)
        {
            Texture2D made = near ? _near : _far;
            if (made != null) return made;
            var canvas = new Color[CloudWidth * CloudHeight];
            for (int y = 0; y < CloudHeight; y++)
            {
                for (int x = 0; x < CloudWidth; x++) canvas[y * CloudWidth + x] = CloudAt(near, (x + 0.5f) / CloudWidth, (y + 0.5f) / CloudHeight);
            }

            made = Picture(near ? "sky-clouds-near" : "sky-clouds-far", canvas, CloudWidth, CloudHeight, TextureWrapMode.Repeat, 0);
            if (near) _near = made;
            else _far = made;
            return made;
        }

        /// <summary>
        /// A cloud layer's colour at a point of it (u along, wrapping; v up the screen), its alpha how solid the cloud is
        /// there. Soft noise makes the clouds: thick along the bottom, thinning out upwards to nothing at the layer's
        /// reach; the far layer also has thin streaks along the top. Each cloud is a little lighter on the side towards
        /// the light, upper left.
        /// </summary>
        public static Color CloudAt(bool near, float u, float v)
        {
            Color colour = CloudLight;
            colour.a = 0f;
            int cells = near ? NearCells : FarCells;
            float reach = near ? NearReach : FarReach;
            float x = (u - Mathf.Floor(u)) * cells, y = v * cells * 1.15f + (near ? 0f : 7.3f);
            float cover = 0f, tone = 0.5f;
            if (v < reach)
            {
                float density = Billows(x, y, cells, near ? 11 : 23);
                // Thick at the bottom of the screen, thinning to scattered tops, then nothing.
                float bank = 1f - v / reach;
                float soft = near ? 0.055f : 0.1f;
                float body = density + 0.62f * (bank - 0.5f);
                cover = Mathf.SmoothStep(0f, 1f, (body - (0.5f - soft)) / (2f * soft)) * Mathf.SmoothStep(0f, 1f, bank / 0.22f);
                if (cover > 0f)
                {
                    float towardsLight = Billows(x - 0.16f, y + 0.22f, cells, near ? 11 : 23);
                    tone = Mathf.Clamp01(0.5f + 3.2f * (density - towardsLight) + 0.3f * (body - 0.5f));
                }
            }
            else if (!near && v > CloudsAbove)
            {
                // Wisps: long thin streaks, never solid.
                float streak = Billows(x * 0.5f, v * 46f, cells / 2, 37);
                float band = Mathf.SmoothStep(0f, 1f, (v - CloudsAbove) / 0.08f);
                cover = Mathf.SmoothStep(0f, 1f, (streak - 0.5f) / 0.3f) * band * (WispSolid / FarSolid);
                tone = 0.8f;
            }

            if (cover <= 0f) return colour;
            colour = Color.Lerp(CloudShade, CloudLight, tone);
            if (!near) colour = Color.Lerp(colour, ColourAt(0.5f, v), FarFaded);
            colour.a = cover * (near ? NearSolid : FarSolid);
            return colour;
        }

        /// <summary>How much of the sky the two cloud layers hide at a point (u along, wrapping; v up): 0 clear sky, 1 solid cloud.</summary>
        public static float CloudCover(float u, float v) => 1f - (1f - CloudAt(true, u, v).a) * (1f - CloudAt(false, u, v).a);

        /// <summary>A far island: its top's middle on the screen (u across, v up), how high its picture stands (in screen heights), its seed.</summary>
        public readonly struct FarIsland
        {
            public readonly float U, V, Size;
            public readonly int Seed;

            public FarIsland(float u, float v, float size, int seed)
            {
                U = u;
                V = v;
                Size = size;
                Seed = seed;
            }
        }

        /// <summary>The far islands: one at each side of the screen, the second smaller and higher.</summary>
        public static readonly IReadOnlyList<FarIsland> FarIslands = new[]
        {
            new FarIsland(0.085f, 0.60f, 0.15f, 51),
            new FarIsland(0.915f, 0.68f, 0.11f, 52),
        };

        /// <summary>A far island's picture, see-through round it: a grass cap with a tree or two, and rock narrowing to a point, all gone pale in the haze.</summary>
        public static Texture2D FarIslandPicture(int index)
        {
            if (IslandPictures[index] != null) return IslandPictures[index];
            var canvas = new Color[IslandWidth * IslandHeight];
            Color clear = FarHaze;
            clear.a = 0f;
            for (int i = 0; i < canvas.Length; i++) canvas[i] = clear;
            Draw(canvas, FarIslands[index].Seed);
            return IslandPictures[index] = Picture($"sky-island-{index + 1}", canvas, IslandWidth, IslandHeight, TextureWrapMode.Clamp, 0);
        }

        /// <summary>Soft billowing noise in 0-1, repeating every <paramref name="period"/> along x: five layers, each twice as fine and half as strong.</summary>
        private static float Billows(float x, float y, int period, int seed)
        {
            float sum = 0f, strength = 0.5f, total = 0f;
            for (int layer = 0; layer < 5; layer++)
            {
                sum += strength * Noise(x, y, period, seed + layer * 17);
                total += strength;
                x *= 2f;
                y *= 2f;
                period *= 2;
                strength *= 0.5f;
            }

            // Stretched about its middle: the layers average each other out.
            return Mathf.Clamp01(0.5f + (sum / total - 0.5f) * 1.7f);
        }

        private static float Noise(float x, float y, int period, int seed)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int x0 = ((ix % period) + period) % period, x1 = (x0 + 1) % period;
            float low = Hash(x0, iy, seed) + (Hash(x1, iy, seed) - Hash(x0, iy, seed)) * fx;
            float high = Hash(x0, iy + 1, seed) + (Hash(x1, iy + 1, seed) - Hash(x0, iy + 1, seed)) * fx;
            return low + (high - low) * fy;
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        private static void Draw(Color[] canvas, int seed)
        {
            var random = new System.Random(seed * 977 + 3);
            float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);
            float half = IslandWidth * 0.4f, cx = IslandWidth / 2f, top = IslandHeight * 0.74f;
            float depth = half * Range(1.9f, 2.3f), lean = Range(-0.25f, 0.25f) * half;
            float waveA = Range(0f, 6.28f), waveB = Range(0f, 6.28f);
            // Two or three trees on top.
            int trees = 2 + seed % 2;
            var crowns = new Vector3[trees];
            for (int i = 0; i < trees; i++) crowns[i] = new Vector3(Range(-0.6f, 0.6f) * half, Range(0.2f, 0.34f) * half, Range(0.16f, 0.24f) * half);
            int low = Mathf.Max(0, Mathf.FloorToInt(top - depth - 2f)), high = Mathf.Min(IslandHeight - 1, Mathf.CeilToInt(top + half * 0.7f));
            int left = Mathf.Max(0, Mathf.FloorToInt(cx - half * 1.2f)), right = Mathf.Min(IslandWidth - 1, Mathf.CeilToInt(cx + half * 1.2f));
            for (int y = low; y <= high; y++)
            {
                for (int x = left; x <= right; x++)
                {
                    float px = x + 0.5f - cx, py = y + 0.5f - top;
                    Color colour = FarHaze;
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
                    // The haze takes its colour and lets the sky show through it.
                    colour = Color.Lerp(colour, FarHaze, 0.3f);
                    colour.a = cover * FarIslandSolid;
                    canvas[y * IslandWidth + x] = colour;
                }
            }
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
