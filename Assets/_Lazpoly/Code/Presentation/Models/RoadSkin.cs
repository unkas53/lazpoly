using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The asphalt road round the lawn (Faz 25, the owner's wish; it was a plain grey strip of gravel): the road's top
    /// face is dark asphalt with a fine grain, and a dashed white line runs round the middle of the strip that shows
    /// between the lawn and the squares. Drawn once into a texture over the road's whole top (u, v both 0-1).
    /// </summary>
    public static class RoadSkin
    {
        public const int Size = 1024;

        /// <summary>The asphalt, before the light: dark, a little cool.</summary>
        public static readonly Color Asphalt = new Color(0.25f, 0.26f, 0.28f);

        /// <summary>The road's painted line.</summary>
        public static readonly Color Paint = new Color(0.96f, 0.95f, 0.90f);

        /// <summary>How far the asphalt's brightness wanders, grain by grain.</summary>
        public const float Grain = 0.08f;

        /// <summary>The dashes along each side of the road, and how much of each step is painted.</summary>
        public const int DashesPerSide = 28;
        public const float DashShare = 0.55f;

        /// <summary>The painted line's width, as a share of the strip's width.</summary>
        public const float LineShare = 0.12f;

        /// <summary>
        /// The road's texture for a strip <paramref name="strip"/> wide (a share of the road's size) between the road's
        /// edge and the lawn: its middle line runs <paramref name="strip"/>/2 in from each edge.
        /// </summary>
        public static Texture2D Create(float strip)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                name = "road", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 8, hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[Size * Size];
            var random = new System.Random(11);
            float middle = strip / 2f, halfLine = strip * LineShare / 2f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
                    float grain = 1f + Grain * ((float)random.NextDouble() - 0.5f);
                    Color colour = OnLine(u, v, middle, halfLine) ? Paint : Asphalt * grain;
                    colour.a = 1f;
                    pixels[y * Size + x] = colour;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        /// <summary>
        /// Whether a point of the road's top lies on the dashed middle line: <paramref name="middle"/> in from the nearest
        /// edge (within <paramref name="halfLine"/>), on a painted part of the dashes running along that side.
        /// </summary>
        public static bool OnLine(float u, float v, float middle, float halfLine)
        {
            float fromEdge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            if (Mathf.Abs(fromEdge - middle) > halfLine) return false;
            // Along the side the point is nearest: across it the line, along it the dashes.
            bool onSideU = Mathf.Min(u, 1f - u) <= Mathf.Min(v, 1f - v);
            float along = onSideU ? v : u;
            float step = along * DashesPerSide;
            return step - Mathf.Floor(step) < DashShare;
        }
    }
}
