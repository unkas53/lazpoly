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
    /// far off, two small floating islands gone into the haze (<see cref="FarIslands"/>), kin of the islands the money
    /// safes stand on; since 2026-11-12 (the owner's wish: three-dimensional) they are models
    /// (<see cref="IslandParts.Far"/>) wearing the little islands' pictures gone pale (<see cref="FarRockPicture"/>,
    /// <see cref="FarGrassPicture"/>). The same numbers every time, so every table has the same sky.
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

        /// <summary>The haze's own blue, and how far the far islands (and the plane that passes now and then) have gone into it.</summary>
        public static readonly Color FarHaze = new Color32(0xA9, 0xD3, 0xF0, 0xFF);
        public const float FarIslandHaze = 0.36f;

        /// <summary>
        /// How far the owner's far-island model has gone into the haze (<see cref="IslandParts.Haze"/>): less than the
        /// islands cut in code, whose pictures are paled before the light falls on them; the model's haze is laid over
        /// its lit colours, where the same share takes more.
        /// </summary>
        public const float FarModelHaze = 0.2f;

        /// <summary>The far islands' trees before the haze takes them: their leaves and their trunks.</summary>
        public static readonly Color FarLeaf = new Color32(0x5C, 0x86, 0x4A, 0xFF);
        public static readonly Color FarTrunk = new Color32(0x7C, 0x60, 0x48, 0xFF);

        /// <summary>The far islands' rock picture: smaller than the near islands', being seen from far.</summary>
        public const int FarRockSize = 128;

        /// <summary>The sky picture's size (16:9), and a cloud layer's (twice as wide as high; it repeats sideways). Small: all of it is soft.</summary>
        public const int Width = 512;
        public const int Height = 288;
        public const int CloudWidth = 512;
        public const int CloudHeight = 256;

        /// <summary>The bands of the screen's height the clouds keep to: under the first, above the second.</summary>
        public const float CloudsBelow = 0.36f;
        public const float CloudsAbove = 0.8f;

        // How many cells of noise run along a cloud layer before it repeats, for the near and the far layer.
        private const int NearCells = 5;
        private const int FarCells = 8;

        private static Texture2D _sky, _near, _far, _farRock, _farGrass, _trail;

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

        /// <summary>
        /// The picture of the trail an aeroplane leaves (the owner's wish, 2026-11-13): its size, its white, how solid
        /// it is at its thickest, and how far each of its two streaks (one from each engine) lies from the picture's
        /// middle line, as a share of the picture's height.
        /// </summary>
        public const int TrailWidth = 256;
        public const int TrailHeight = 48;
        public static readonly Color TrailColour = new Color32(0xFB, 0xFC, 0xFC, 0xFF);
        public const float TrailSolid = 0.9f;
        public const float TrailGap = 0.137f;

        /// <summary>The trail's picture: the plane at its right end (u = 1), the trail fading away to the left.</summary>
        public static Texture2D Trail()
        {
            if (_trail != null) return _trail;
            var canvas = new Color[TrailWidth * TrailHeight];
            for (int y = 0; y < TrailHeight; y++)
            {
                for (int x = 0; x < TrailWidth; x++) canvas[y * TrailWidth + x] = TrailAt((x + 0.5f) / TrailWidth, (y + 0.5f) / TrailHeight);
            }

            return _trail = Picture("sky-plane-trail", canvas, TrailWidth, TrailHeight, TextureWrapMode.Clamp, 0);
        }

        /// <summary>
        /// The trail's colour at a point of its picture (u along, 1 at the plane; v across), its alpha how solid it is
        /// there: two thin streaks that start a little behind the engines, widen and soften as they grow older until
        /// they run into one band, and thin away to nothing at the far end. A slow unevenness along it keeps it from
        /// looking ruled; its picture's edges are clear all round.
        /// </summary>
        public static Color TrailAt(float u, float v)
        {
            float age = 1f - Mathf.Clamp01(u);
            float spread = 0.03f + 0.15f * Mathf.Pow(age, 0.75f);
            float one = (v - 0.5f - TrailGap) / spread, other = (v - 0.5f + TrailGap) / spread;
            float body = Mathf.Min(1f, (Mathf.Exp(-0.5f * one * one) + Mathf.Exp(-0.5f * other * other)) * Mathf.Pow(0.03f / spread, 0.35f));
            float fade = Mathf.Clamp01(age / 0.03f) * Mathf.Pow(1f - age, 1.4f);
            float wisp = 0.84f + 0.16f * Mathf.Sin(u * 47f + 1.3f) * Mathf.Sin(u * 19f + 0.4f);
            float edge = Mathf.Clamp01((0.5f - Mathf.Abs(v - 0.5f)) / 0.1f);
            Color colour = TrailColour;
            colour.a = Mathf.Clamp01(TrailSolid * body * fade * wisp * edge);
            return colour;
        }

        /// <summary>How much of the sky the two cloud layers hide at a point (u along, wrapping; v up): 0 clear sky, 1 solid cloud.</summary>
        public static float CloudCover(float u, float v) => 1f - (1f - CloudAt(true, u, v).a) * (1f - CloudAt(false, u, v).a);

        /// <summary>
        /// A far island: its middle on the screen (u across, v up), how high it stands there (in screen heights), its
        /// seed, and, for the owner's model, how far its front is turned from facing the camera (degrees about its
        /// upright), so the same model shows a different side at each place.
        /// </summary>
        public readonly struct FarIsland
        {
            public readonly float U, V, Size, Turn;
            public readonly int Seed;

            public FarIsland(float u, float v, float size, int seed, float turn)
            {
                U = u;
                V = v;
                Size = size;
                Seed = seed;
                Turn = turn;
            }
        }

        /// <summary>The far islands: one at each side of the screen, the second smaller and higher, and seen more from its side; the aeroplane's paths pass over both.</summary>
        public static readonly IReadOnlyList<FarIsland> FarIslands = new[]
        {
            new FarIsland(0.074f, 0.60f, 0.19f, 51, 14f),
            new FarIsland(0.915f, 0.66f, 0.14f, 52, 62f),
        };

        /// <summary>A colour as it shows far off: part of the way to the haze's blue.</summary>
        public static Color Hazed(Color colour)
        {
            Color hazed = Color.Lerp(colour, FarHaze, FarIslandHaze);
            hazed.a = 1f;
            return hazed;
        }

        /// <summary>The far islands' rock: the little islands' own picture (<see cref="IslandSkin.RockAt"/>) gone pale in the haze. It repeats round the island.</summary>
        public static Texture2D FarRockPicture()
        {
            if (_farRock != null) return _farRock;
            var texture = new Texture2D(FarRockSize, FarRockSize, TextureFormat.RGBA32, true)
            {
                name = "sky-island-rock", wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[FarRockSize * FarRockSize];
            for (int y = 0; y < FarRockSize; y++)
            {
                for (int x = 0; x < FarRockSize; x++) pixels[y * FarRockSize + x] = Hazed(IslandSkin.RockAt((x + 0.5f) / FarRockSize, (y + 0.5f) / FarRockSize));
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            return _farRock = texture;
        }

        /// <summary>The far islands' grass: the little islands' strip (<see cref="IslandSkin.GrassAt"/>) gone pale in the haze.</summary>
        public static Texture2D FarGrassPicture()
        {
            if (_farGrass != null) return _farGrass;
            var texture = new Texture2D(4, IslandSkin.GrassHeight, TextureFormat.RGBA32, false)
            {
                name = "sky-island-grass", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[4 * IslandSkin.GrassHeight];
            for (int y = 0; y < IslandSkin.GrassHeight; y++)
            {
                Color32 colour = Hazed(IslandSkin.GrassAt((y + 0.5f) / IslandSkin.GrassHeight));
                for (int x = 0; x < 4; x++) pixels[y * 4 + x] = colour;
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return _farGrass = texture;
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
