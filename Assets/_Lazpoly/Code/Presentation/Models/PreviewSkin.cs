using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The sky behind a property's picture on the cards (<see cref="BuildingPreview"/>), drawn in code from the table's
    /// own sky (<see cref="SkySkin"/>): its blue rising paler towards the top, a soft warm glow behind the building so
    /// it stands out, and the near cloud bank along the bottom, so the little island floats over the clouds as the
    /// table does.
    /// </summary>
    public static class PreviewSkin
    {
        /// <summary>The sky at the picture's bottom and at its top.</summary>
        public static readonly Color Low = Color.Lerp(SkySkin.Deep, SkySkin.Mid, 0.4f);
        public static readonly Color High = Color.Lerp(SkySkin.High, Color.white, 0.2f);

        /// <summary>The glow behind the building: its colour, how strong at its middle, where (u across, v up) and how far it reaches.</summary>
        public static readonly Color Glow = new Color(1f, 0.98f, 0.93f);
        public const float GlowStrength = 0.45f;
        public static readonly Vector2 GlowCentre = new Vector2(0.5f, 0.54f);
        public static readonly Vector2 GlowReach = new Vector2(0.42f, 0.36f);

        /// <summary>How high up the picture the cloud bank reaches, and the stretch of the table's cloud picture it is cut from.</summary>
        public const float CloudReach = 0.24f;
        public const float CloudFrom = 0.18f;
        public const float CloudSpan = 0.3f;

        public const int Width = 192;
        public const int Height = 256;

        private static Texture2D _backdrop;

        /// <summary>The backdrop's colour at a point: u across, v from the bottom (0) to the top (1).</summary>
        public static Color ColourAt(float u, float v)
        {
            u = Mathf.Clamp01(u);
            v = Mathf.Clamp01(v);
            Color colour = Color.Lerp(Low, High, Mathf.SmoothStep(0f, 1f, v));
            float du = (u - GlowCentre.x) / GlowReach.x, dv = (v - GlowCentre.y) / GlowReach.y;
            colour = Color.Lerp(colour, Glow, GlowStrength * Mathf.Exp(-(du * du + dv * dv)));
            Color cloud = SkySkin.CloudAt(true, CloudFrom + u * CloudSpan, v * SkySkin.NearReach / CloudReach);
            colour = Color.Lerp(colour, cloud, cloud.a);
            colour.a = 1f;
            return colour;
        }

        /// <summary>The backdrop's picture.</summary>
        public static Texture2D Backdrop()
        {
            if (_backdrop != null) return _backdrop;
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = "preview-sky", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[Width * Height];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++) pixels[y * Width + x] = ColourAt((x + 0.5f) / Width, (y + 0.5f) / Height);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return _backdrop = texture;
        }
    }
}
