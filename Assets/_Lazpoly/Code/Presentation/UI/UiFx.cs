using System;
using UnityEngine;

namespace Lazpoly.Presentation.UI
{
    /// <summary>
    /// The interface's lights, made at runtime as white pictures that take any colour: a round glow, a fan of rays (the
    /// sunburst behind the logo), a band of light (the sheen that crosses a button), a soft mote of dust and a fade from
    /// one side to the other. Like <see cref="UiSprites"/>, they need no art files. What each picture holds is a plain
    /// function of the place in it, so the tests read the same numbers the pictures are drawn from.
    /// </summary>
    public static class UiFx
    {
        public const int GlowSize = 128;
        public const int RaysSize = 512;
        public const int BandSize = 64;
        public const int DotSize = 32;

        /// <summary>How many rays the sunburst has; each is as wide as the gap beside it.</summary>
        public const int RayCount = 22;

        public const int EdgeSize = 16;

        private static Sprite _glow, _rays, _band, _dot, _fade, _edge;

        /// <summary>A round light (u, v from 0 to 1): full in the middle, falling off smoothly to nothing at the rim.</summary>
        public static float GlowAt(float u, float v)
        {
            float x = u - 0.5f, y = v - 0.5f;
            float reach = Mathf.Clamp01(1f - 2f * Mathf.Sqrt(x * x + y * y));
            return reach * reach * (3f - 2f * reach);
        }

        /// <summary>
        /// The sunburst: rays fanning out of the middle, soft-edged, nothing in the very middle (the logo stands there),
        /// strongest a little way out and fading to nothing at the rim.
        /// </summary>
        public static float RaysAt(float u, float v)
        {
            float x = u - 0.5f, y = v - 0.5f;
            float out01 = 2f * Mathf.Sqrt(x * x + y * y);
            if (out01 >= 1f) return 0f;
            float ray = Mathf.Clamp01(Mathf.Cos(Mathf.Atan2(y, x) * RayCount) * 6f);
            return ray * Mathf.Pow(1f - out01, 1.3f) * Mathf.Clamp01(out01 / 0.16f);
        }

        /// <summary>A band of light across (u from 0 to 1): nothing at both sides, full along its middle.</summary>
        public static float BandAt(float u)
        {
            float s = Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI);
            return s * s;
        }

        /// <summary>A mote: a small bright middle in a wide soft halo.</summary>
        public static float DotAt(float u, float v)
        {
            float x = u - 0.5f, y = v - 0.5f;
            float reach = Mathf.Clamp01(1f - 2f * Mathf.Sqrt(x * x + y * y));
            return Mathf.Clamp01(0.35f * reach + Mathf.Pow(reach, 3f));
        }

        /// <summary>
        /// A line with soft sides (u from 0 to 1 across it): full along its middle, fading out over its outermost
        /// texels, so a leaning line drawn with it has smooth edges where a plain rectangle would show steps.
        /// </summary>
        public static float EdgeAt(float u) => Mathf.Clamp01(Mathf.Min(u, 1f - u) * EdgeSize / 2f);

        public static Sprite Glow => _glow != null ? _glow : _glow = Make("Lazpoly Glow", GlowSize, GlowSize, GlowAt);

        public static Sprite Rays => _rays != null ? _rays : _rays = Make("Lazpoly Rays", RaysSize, RaysSize, RaysAt);

        public static Sprite Band => _band != null ? _band : _band = Make("Lazpoly Band", BandSize, 4, (u, v) => BandAt(u));

        public static Sprite Dot => _dot != null ? _dot : _dot = Make("Lazpoly Dot", DotSize, DotSize, DotAt);

        /// <summary>A fade across: full at the left, nothing at the right (a shadow cast to the right of an edge).</summary>
        public static Sprite Fade => _fade != null ? _fade : _fade = Make("Lazpoly Fade", BandSize, 4, (u, v) => (1f - u) * (1f - u));

        /// <summary>A line with soft sides, for an edge that leans (see <see cref="EdgeAt"/>).</summary>
        public static Sprite Edge => _edge != null ? _edge : _edge = Make("Lazpoly Edge", EdgeSize, 4, (u, v) => EdgeAt(u));

        /// <summary>
        /// A picture one pixel wide that runs from <paramref name="bottom"/> to <paramref name="top"/> between two heights
        /// of it (shares of its height) and holds the end colours beyond them: a ground of two shades for a tall shape of
        /// which only the middle shows.
        /// </summary>
        public static Sprite Shaded(string name, Color bottom, Color top, float from = 0f, float to = 1f)
        {
            const int height = 64;
            var texture = new Texture2D(1, height, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color[height];
            for (int y = 0; y < height; y++) pixels[y] = ShadeAt((y + 0.5f) / height, bottom, top, from, to);
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        /// <summary>The colour of <see cref="Shaded"/> at a height of it (0 the bottom, 1 the top).</summary>
        public static Color ShadeAt(float v, Color bottom, Color top, float from, float to) =>
            Color.Lerp(bottom, top, to > from ? Mathf.Clamp01((v - from) / (to - from)) : (v >= to ? 1f : 0f));

        private static Sprite Make(string name, int width, int height, Func<float, float, float> light)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };

            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float alpha = Mathf.Clamp01(light((x + 0.5f) / width, (y + 0.5f) / height));
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _glow = null;
            _rays = null;
            _band = null;
            _dot = null;
            _fade = null;
            _edge = null;
        }
    }
}
