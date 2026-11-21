using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>The shapes an effect is drawn with (<see cref="SoftSprites"/>).</summary>
    public enum SoftSprite
    {
        /// <summary>A cartoon cloud: a lumpy ball with a clean edge. Dust, and the puff a sold building goes in.</summary>
        Cloud,

        /// <summary>The lit side of a <see cref="Cloud"/>: the same ball a size smaller, up and to the left, drawn over it in a lighter tone.</summary>
        CloudLight,

        /// <summary>A ring with soft sides: a wave spreading over the ground.</summary>
        Ring,

        /// <summary>A round glow, brightest in the middle.</summary>
        Dot,

        /// <summary>A bold dollar sign with a clean edge (the owner's wish, 2026-11-18).</summary>
        Dollar,

        /// <summary>The dark rim under a <see cref="Dollar"/>: the same sign a stroke thicker.</summary>
        DollarEdge,

        /// <summary>A cartoon burst: a star of many sharp points with a clean edge, the heart of an explosion (the owner's missile card, 2026-11-21).</summary>
        Star,
    }

    /// <summary>
    /// The pictures behind the game's effects (the owner's wishes, 2026-11-17 and 18: the leaving of a building should
    /// look professional and suit the game, with dollar signs for the gold glints): see-through shapes drawn in code on
    /// one sheet. The clouds and the dollar signs have clean cartoon edges, as the game's models do; the ring and the
    /// glow are soft. They are drawn with the board's print shader, which blends over what is behind, tests depth (a
    /// house hides the dust behind it) and takes its colour from the quad's corners; a shape of two tones is two quads.
    /// </summary>
    public static class SoftSprites
    {
        /// <summary>Each shape's square on the sheet, in pixels, and how many stand in a row and how many rows.</summary>
        public const int Cell = 128;
        public const int Columns = 4;
        public const int Rows = 2;

        /// <summary>The effects draw after the board's own print and its lettering.</summary>
        public const int Layer = 40;

        /// <summary>Half the width of a clean edge's feather, in a shape's own units: a pixel or so on the sheet.</summary>
        public const float Feather = 0.02f;

        /// <summary>The dollar sign: its two bows' radius, their stroke's and the bar's half widths, how far the bar reaches, and how much thicker the rim is.</summary>
        public const float Bow = 0.25f;
        public const float Stroke = 0.095f;
        public const float Bar = 0.055f;
        public const float BarReach = 0.7f;
        public const float Rim = 0.075f;

        /// <summary>The burst star: how many points it has, how far they reach and how deep the notches between them go.</summary>
        public const int StarPoints = 10;
        public const float StarReach = 0.9f;
        public const float StarNotch = 0.52f;

        private static Texture2D _sheet;

        /// <summary>
        /// How solid <paramref name="sprite"/> is at a point of its square, (-1,-1) to (1,1) with the middle at 0: 0 clear
        /// to 1 solid. Every shape is clear at the edge of its square.
        /// </summary>
        public static float Alpha(SoftSprite sprite, float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            // Nothing reaches the edge, so neighbours on the sheet never bleed into each other.
            float edge = Mathf.Clamp01((1f - Mathf.Max(Mathf.Abs(x), Mathf.Abs(y))) / 0.08f);
            float alpha;
            switch (sprite)
            {
                case SoftSprite.Cloud:
                    alpha = Solid(-CloudDistance(x, y));
                    break;
                case SoftSprite.CloudLight:
                    alpha = Solid(-CloudDistance((x + 0.09f) / 0.74f, (y - 0.12f) / 0.74f) * 0.74f);
                    break;
                case SoftSprite.Ring:
                {
                    float off = (r - 0.78f) / 0.1f;
                    alpha = Mathf.Exp(-off * off);
                    break;
                }

                case SoftSprite.Dollar:
                    alpha = Solid(-DollarDistance(x, y));
                    break;
                case SoftSprite.DollarEdge:
                    alpha = Solid(Rim - DollarDistance(x, y));
                    break;
                case SoftSprite.Star:
                    alpha = Solid((StarRadius(Mathf.Atan2(y, x)) - r) * 0.7f);
                    break;
                default:
                    alpha = Mathf.Exp(-r * r / 0.16f) * Mathf.Clamp01((1f - r) / 0.25f);
                    break;
            }

            return Mathf.Clamp01(alpha) * edge;
        }

        /// <summary>A clean edge: solid <paramref name="inside"/> the shape, clear outside, with a feather a pixel wide between.</summary>
        private static float Solid(float inside) => Mathf.Clamp01(inside / (2f * Feather) + 0.5f);

        /// <summary>How far the burst star reaches at <paramref name="angle"/> (radians): out to a point, in to a notch, <see cref="StarPoints"/> times round, every other point a little shorter.</summary>
        public static float StarRadius(float angle)
        {
            float turn = angle / (2f * Mathf.PI) * StarPoints;
            int point = Mathf.RoundToInt(turn);
            float off = Mathf.Abs(turn - point) * 2f; // 0 on a point, 1 in a notch
            float reach = (point % 2 + 2) % 2 == 0 ? StarReach : StarReach * 0.84f;
            return Mathf.Lerp(reach, StarNotch, off);
        }

        /// <summary>How far a point is outside the cloud (below 0 inside it): a heart and six lobes round it, each a ball, so its outline is lumpy.</summary>
        public static float CloudDistance(float x, float y)
        {
            float distance = Mathf.Sqrt(x * x + y * y) - 0.5f;
            for (int i = 0; i < 6; i++)
            {
                float angle = 0.5f + i * (Mathf.PI / 3f);
                float reach = i % 2 == 0 ? 0.42f : 0.36f;
                float radius = i % 2 == 0 ? 0.34f : 0.4f;
                float dx = x - Mathf.Cos(angle) * reach, dy = y - Mathf.Sin(angle) * reach;
                distance = Mathf.Min(distance, Mathf.Sqrt(dx * dx + dy * dy) - radius);
            }

            return distance;
        }

        /// <summary>
        /// How far a point is outside the dollar sign (below 0 inside it): an S of two bows, the upper open to the
        /// right below its top, the lower open to the left above its foot, and an upright bar through both.
        /// </summary>
        public static float DollarDistance(float x, float y)
        {
            // The upper bow runs from its tip at the right, over the top and down the left to the middle; the lower one
            // is the same turned half round.
            float upper = BowDistance(x, y - Bow);
            float lower = BowDistance(-x, -y - Bow);
            float bar = Mathf.Sqrt(x * x + Mathf.Pow(Mathf.Max(0f, Mathf.Abs(y) - BarReach), 2f)) - Bar;
            return Mathf.Min(Mathf.Min(upper, lower) - Stroke, bar);
        }

        /// <summary>The distance to a bow's middle line: three quarters and a little of a circle about the origin, from 35 degrees round to 270.</summary>
        private static float BowDistance(float x, float y)
        {
            const float first = 35f * Mathf.Deg2Rad, last = 270f * Mathf.Deg2Rad;
            float angle = Mathf.Atan2(y, x);
            if (angle < 0f) angle += 2f * Mathf.PI;
            if (angle >= first && angle <= last) return Mathf.Abs(Mathf.Sqrt(x * x + y * y) - Bow);
            float ax = x - Mathf.Cos(first) * Bow, ay = y - Mathf.Sin(first) * Bow;
            float bx = x - Mathf.Cos(last) * Bow, by = y - Mathf.Sin(last) * Bow;
            return Mathf.Sqrt(Mathf.Min(ax * ax + ay * ay, bx * bx + by * by));
        }

        /// <summary>Where <paramref name="sprite"/> lies on the sheet: its lower left corner and its size, in texture coordinates.</summary>
        public static Rect Uv(SoftSprite sprite)
        {
            int index = (int)sprite;
            return new Rect((index % Columns) / (float)Columns, (index / Columns) / (float)Rows, 1f / Columns, 1f / Rows);
        }

        /// <summary>The sheet itself: white, with the shapes in its alpha. Drawn once and kept.</summary>
        public static Texture2D Sheet()
        {
            if (_sheet != null) return _sheet;
            int wide = Cell * Columns, high = Cell * Rows;
            var pixels = new Color32[wide * high];
            int shapes = System.Enum.GetValues(typeof(SoftSprite)).Length;
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 0);
            for (int index = 0; index < shapes; index++)
            {
                int left = (index % Columns) * Cell, bottom = (index / Columns) * Cell;
                for (int py = 0; py < Cell; py++)
                {
                    for (int px = 0; px < Cell; px++)
                    {
                        float x = (px + 0.5f) / Cell * 2f - 1f;
                        float y = (py + 0.5f) / Cell * 2f - 1f;
                        byte alpha = (byte)Mathf.RoundToInt(Alpha((SoftSprite)index, x, y) * 255f);
                        pixels[(bottom + py) * wide + left + px] = new Color32(255, 255, 255, alpha);
                    }
                }
            }

            _sheet = new Texture2D(wide, high, TextureFormat.RGBA32, false, true)
            {
                name = "Soft sprites",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            _sheet.SetPixels32(pixels);
            _sheet.Apply(false, true);
            return _sheet;
        }

        /// <summary>The one material every effect shares (its colours ride on the quads); null when the print shader is not in the build.</summary>
        public static Material Material() => PrintMaterial.ForPicture(Sheet(), Layer);
    }
}
