using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The lawn in the middle of the board, painted like a real one (the owner's wish, 2026-11-10: a nicer design, a
    /// more natural and matt green, edges that are not dead straight; the mowed diamonds and the cream border are gone).
    /// A muted meadow green that drifts between a deeper and a lighter tone in soft patches, a few drier, yellower
    /// spots, fine blades and grain, a darker earthy rim where the grass ends (<see cref="LawnShape"/> says where), and
    /// a few loose patches of small daisies away from the middle, where the dice total and the pot are read.
    /// Drawn once into a texture over the lawn's whole top (u, v 0-1 span <see cref="LawnShape.Reach"/> either way).
    /// </summary>
    public static class LawnSkin
    {
        public const int Size = 1024;

        /// <summary>The meadow's greens before the light: a deeper and a lighter one, both muted, and the drier spots' yellow-green.</summary>
        public static readonly Color Deep = new Color32(0x5A, 0x7D, 0x4A, 0xFF);
        public static readonly Color Light = new Color32(0x75, 0x96, 0x59, 0xFF);
        public static readonly Color Dry = new Color32(0x8A, 0x9C, 0x60, 0xFF);

        /// <summary>The earth that shows where the grass ends.</summary>
        public static readonly Color Earth = new Color32(0x5B, 0x57, 0x3C, 0xFF);

        /// <summary>A daisy's petals and its eye.</summary>
        public static readonly Color Petal = new Color(0.96f, 0.95f, 0.88f);
        public static readonly Color Eye = new Color(0.95f, 0.78f, 0.25f);

        /// <summary>How far the brightness wanders grain by grain, and blade by blade.</summary>
        public const float Grain = 0.05f;
        public const float Blades = 0.06f;

        /// <summary>How much of the drier colour its spots take at most.</summary>
        public const float DryShare = 0.35f;

        /// <summary>The earthy rim: how wide it is at most (in half-lawns) and how much of the earth it shows at the very edge.</summary>
        public const float RimWidth = 0.031f;
        public const float RimStrength = 0.8f;

        /// <summary>The grass darkens a little towards its edge: over this width, by this much.</summary>
        public const float ShadeWidth = 0.05f;
        public const float ShadeStrength = 0.12f;

        /// <summary>The daisies: how many patches, between which distances from the middle (in half-lawns), and how loose a patch is.</summary>
        public const int FlowerPatches = 15;
        public const float FlowerInner = 0.38f;
        public const float FlowerOuter = 0.86f;
        public const float PatchSpread = 0.035f;

        private static Texture2D _texture;
        private static List<Vector3> _flowers;

        /// <summary>The lawn's texture, made on first use and shared.</summary>
        public static Texture2D Texture()
        {
            if (_texture != null) return _texture;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                name = "lawn", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 8, hideFlags = HideFlags.DontSave,
            };

            var broad = new Noise(5, 5, 1);
            var patches = new Noise(17, 17, 2);
            var tufts = new Noise(61, 61, 3);
            var dry = new Noise(7, 7, 4);
            var ragged = new Noise(140, 140, 6);
            var blades = new Noise(400, 90, 8);
            float[] radius = RadiusTable();
            var grain = new System.Random(5);
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
                    float tone = 0.55f * broad.At(u, v) + 0.3f * patches.At(u, v) + 0.15f * tufts.At(u, v);
                    Color colour = Color.Lerp(Deep, Light, Mathf.Clamp01((tone - 0.5f) * 1.6f + 0.5f));
                    colour = Color.Lerp(colour, Dry, DryShare * Mathf.Clamp01((dry.At(u, v) - 0.58f) / 0.2f));
                    // Blades: fine across one diagonal, long along the other.
                    float shine = 1f + Blades * (blades.At((u - v) / 2f + 0.5f, (u + v) / 2f) - 0.5f) + Grain * ((float)grain.NextDouble() - 0.5f);

                    float px = (2f * u - 1f) * LawnShape.Reach, py = (2f * v - 1f) * LawnShape.Reach;
                    float fromMiddle = Mathf.Sqrt(px * px + py * py);
                    if (fromMiddle > NearEdge)
                    {
                        float inFromEdge = Lookup(radius, Mathf.Atan2(py, px)) - fromMiddle;
                        shine *= 1f - ShadeStrength * Shade(inFromEdge);
                        colour = Color.Lerp(colour * shine, Earth, RimStrength * Rim(inFromEdge, ragged.At(u, v)));
                    }
                    else
                    {
                        colour *= shine;
                    }

                    colour.a = 1f;
                    pixels[y * Size + x] = colour;
                }
            }

            foreach (Vector3 flower in Flowers) PaintFlower(pixels, flower);
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            _texture = texture;
            return texture;
        }

        /// <summary>Closer to the middle than this (in half-lawns) no edge is near, whichever way the edge wanders.</summary>
        private const float NearEdge = 0.86f;

        /// <summary>
        /// How much of the earthy rim shows (0-1) this far in from the grass's edge (negative: past it). The rim's width
        /// changes with <paramref name="roughness"/> (0-1), so its inner side is ragged like grass, not a drawn line.
        /// </summary>
        public static float Rim(float inFromEdge, float roughness)
        {
            float width = RimWidth * (0.3f + 0.7f * Mathf.Clamp01(roughness));
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - inFromEdge / width));
        }

        /// <summary>How much darker the grass is (0-1) this far in from its edge.</summary>
        public static float Shade(float inFromEdge)
        {
            float t = Mathf.Clamp01(1f - inFromEdge / ShadeWidth);
            return t * t;
        }

        /// <summary>
        /// The daisies: x and y where each stands (in half-lawns from the middle), z its size in texels. A few are small
        /// yellow ones (z negative). The same on every table.
        /// </summary>
        public static IReadOnlyList<Vector3> Flowers
        {
            get
            {
                if (_flowers != null) return _flowers;
                var random = new System.Random(21);
                var flowers = new List<Vector3>();
                for (int patch = 0; patch < FlowerPatches; patch++)
                {
                    Vector2 middle;
                    do
                    {
                        float away = Mathf.Lerp(FlowerInner, FlowerOuter, (float)random.NextDouble());
                        float angle = (float)random.NextDouble() * 2f * Mathf.PI;
                        middle = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * away;
                    }
                    while (Mathf.Max(Mathf.Abs(middle.x), Mathf.Abs(middle.y)) >= FlowerOuter);

                    int count = 4 + random.Next(5);
                    for (int i = 0; i < count; i++)
                    {
                        Vector2 at = middle + new Vector2(Bell(random), Bell(random)) * PatchSpread;
                        float size = Mathf.Lerp(2.8f, 4f, (float)random.NextDouble());
                        bool yellow = random.NextDouble() < 0.22;
                        // Never in the calm middle, never on the rim.
                        if (at.magnitude < FlowerInner - PatchSpread || !LawnShape.Covers(at, 2f * RimWidth)) continue;
                        flowers.Add(new Vector3(at.x, at.y, yellow ? -size * 0.75f : size));
                    }
                }

                _flowers = flowers;
                return flowers;
            }
        }

        /// <summary>A number around 0, mostly within 1 either way (two throws averaged out to a bell).</summary>
        private static float Bell(System.Random random)
        {
            double a = 1.0 - random.NextDouble(), b = random.NextDouble();
            return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(a)) * System.Math.Cos(2.0 * System.Math.PI * b));
        }

        private static void PaintFlower(Color32[] pixels, Vector3 flower)
        {
            float cx = (flower.x / LawnShape.Reach + 1f) / 2f * Size, cy = (flower.y / LawnShape.Reach + 1f) / 2f * Size;
            if (flower.z < 0f)
            {
                Dot(pixels, cx, cy, -flower.z, Eye, 0.9f);
                return;
            }

            Dot(pixels, cx, cy, flower.z, Petal, 0.85f);
            Dot(pixels, cx, cy, flower.z * 0.36f, Eye, 1f);
        }

        private static void Dot(Color32[] pixels, float cx, float cy, float radius, Color colour, float strength)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - radius - 1f)), x1 = Mathf.Min(Size - 1, Mathf.CeilToInt(cx + radius + 1f));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - radius - 1f)), y1 = Mathf.Min(Size - 1, Mathf.CeilToInt(cy + radius + 1f));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float cover = Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy)) * strength;
                    if (cover <= 0f) continue;
                    Color mixed = Color.Lerp(pixels[y * Size + x], colour, cover);
                    mixed.a = 1f;
                    pixels[y * Size + x] = mixed;
                }
            }
        }

        private const int RadiusSteps = 4096;

        /// <summary>The outline's reach at evenly spaced angles from -pi to pi, so each pixel only looks it up.</summary>
        private static float[] RadiusTable()
        {
            var table = new float[RadiusSteps + 1];
            for (int i = 0; i <= RadiusSteps; i++) table[i] = LawnShape.Radius(-Mathf.PI + i * 2f * Mathf.PI / RadiusSteps);
            return table;
        }

        private static float Lookup(float[] table, float angle)
        {
            float at = Mathf.Clamp01((angle + Mathf.PI) / (2f * Mathf.PI)) * RadiusSteps;
            int i = Mathf.Min(RadiusSteps - 1, (int)at);
            return Mathf.Lerp(table[i], table[i + 1], at - i);
        }

        /// <summary>Soft noise over the unit square: random numbers on a grid, blended smoothly between its points.</summary>
        private sealed class Noise
        {
            private readonly float[] _values;
            private readonly int _across, _along;

            public Noise(int across, int along, int seed)
            {
                _across = across;
                _along = along;
                _values = new float[(across + 2) * (along + 2)];
                var random = new System.Random(seed);
                for (int i = 0; i < _values.Length; i++) _values[i] = (float)random.NextDouble();
            }

            public float At(float u, float v)
            {
                float x = Mathf.Clamp01(u) * _across, y = Mathf.Clamp01(v) * _along;
                int ix = Mathf.Min(_across, (int)x), iy = Mathf.Min(_along, (int)y);
                float fx = x - ix, fy = y - iy;
                fx = fx * fx * (3f - 2f * fx);
                fy = fy * fy * (3f - 2f * fy);
                int row = iy * (_across + 2) + ix, next = row + _across + 2;
                float low = _values[row] + (_values[row + 1] - _values[row]) * fx;
                float high = _values[next] + (_values[next + 1] - _values[next]) * fx;
                return low + (high - low) * fy;
            }
        }
    }
}
