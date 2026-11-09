using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The lawn in the middle of the board (the owner's wish, Faz 31: a nice pattern on the ground and a kinder green): a
    /// soft sage green mowed in large diamonds, two greens a few shades apart with soft edges, a fine grain, and a thin
    /// cream garden border with a finer line inside it. Drawn once into a texture over the lawn's whole top (u, v 0-1).
    /// </summary>
    public static class LawnSkin
    {
        public const int Size = 1024;

        /// <summary>The two greens of the mowed diamonds, kin to the towns' palette: a light sage and a slightly deeper one.</summary>
        public static readonly Color Light = new Color32(0x99, 0xCF, 0x8E, 0xFF);
        public static readonly Color Deep = new Color32(0x87, 0xC1, 0x83, 0xFF);

        /// <summary>The border's cream paint.</summary>
        public static readonly Color Paint = new Color(0.97f, 0.96f, 0.90f);

        /// <summary>How many diamonds run along each side.</summary>
        public const int DiamondsAcross = 9;

        /// <summary>How soft a diamond's edge is, as a share of a diamond.</summary>
        public const float EdgeSoftness = 0.06f;

        /// <summary>How far the grass's brightness wanders, blade by blade.</summary>
        public const float Grain = 0.05f;

        /// <summary>Where the border runs in from the lawn's edge, how round its corners are, and how strongly it is painted.</summary>
        public const float BorderInset = 0.045f;
        public const float BorderCorner = 0.03f;
        public const float BorderGap = 0.018f;
        public const float BorderStrength = 0.55f;

        private static Texture2D _texture;

        /// <summary>The lawn's texture, made on first use and shared.</summary>
        public static Texture2D Texture()
        {
            if (_texture != null) return _texture;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                name = "lawn", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 8, hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[Size * Size];
            var random = new System.Random(5);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
                    Color colour = Color.Lerp(Deep, Light, MowValue(u, v)) * (1f + Grain * ((float)random.NextDouble() - 0.5f));
                    colour = Color.Lerp(colour, Paint, BorderStrength * BorderValue(u, v));
                    colour.a = 1f;
                    pixels[y * Size + x] = colour;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            _texture = texture;
            return texture;
        }

        /// <summary>How much of the light green a point shows (0-1): 1 on a light diamond, 0 on a deep one, a soft blend at their edges.</summary>
        public static float MowValue(float u, float v)
        {
            float a = (u + v) * DiamondsAcross / 2f, b = (u - v) * DiamondsAcross / 2f;
            int ia = Mathf.FloorToInt(a), ib = Mathf.FloorToInt(b);
            float fa = a - ia, fb = b - ib;
            float edge = Mathf.Min(Mathf.Min(fa, 1f - fa), Mathf.Min(fb, 1f - fb));
            float sharp = ((ia + ib) & 1) == 1 ? 1f : 0f;
            return Mathf.Lerp(0.5f, sharp, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / EdgeSoftness)));
        }

        /// <summary>How much of the border's paint a point shows (0-1): the main line, and a fainter, finer one inside it.</summary>
        public static float BorderValue(float u, float v)
        {
            float d = RoundedDistance(u, v, BorderInset, BorderCorner);
            float main = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.Abs(d) - 0.003f) / 0.003f));
            float inner = 0.6f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.Abs(d + BorderGap) - 0.0015f) / 0.002f)));
            return Mathf.Max(main, inner);
        }

        /// <summary>How far a point lies outside a rounded square <paramref name="inset"/> in from the edges (negative inside).</summary>
        private static float RoundedDistance(float u, float v, float inset, float corner)
        {
            float reach = 0.5f - inset - corner;
            float qx = Mathf.Abs(u - 0.5f) - reach, qy = Mathf.Abs(v - 0.5f) - reach;
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - corner;
        }
    }
}
