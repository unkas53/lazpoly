using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The road round the lawn (Faz 25: asphalt with a dashed white line; the owner's wish, 2026-11-10, once the lawn's
    /// edge wandered: its edges mixed with sand, and the line set right for that). The ground between the squares and
    /// the grass is sand with small stones. On it lies a lane of dark asphalt that follows the lawn round its corners;
    /// both its edges break up into the sand grain by grain, and where the grass comes out it pushes a sandy fringe
    /// ahead of itself, so no asphalt ever touches the grass. The dashed line runs along the lane's middle and bends
    /// round the corners with it. Drawn once into a texture over the road's whole top (u, v both 0-1).
    /// Lengths here are in half-roads: 1 is from the road's middle to its edge, where the squares begin.
    /// </summary>
    public static class RoadSkin
    {
        public const int Size = 1024;

        /// <summary>The asphalt, before the light: dark, a little cool.</summary>
        public static readonly Color Asphalt = new Color(0.25f, 0.26f, 0.28f);

        /// <summary>The road's painted line.</summary>
        public static readonly Color Paint = new Color(0.96f, 0.95f, 0.90f);

        /// <summary>The sand, a lighter and a deeper tone, both matt and warm, and the small stones in it.</summary>
        public static readonly Color Sand = new Color32(0xA3, 0x90, 0x6C, 0xFF);
        public static readonly Color SandDeep = new Color32(0x86, 0x76, 0x58, 0xFF);
        public static readonly Color Stone = new Color32(0x6F, 0x6A, 0x60, 0xFF);

        /// <summary>How far the brightness wanders, grain by grain: of the asphalt, and of the sand.</summary>
        public const float Grain = 0.08f;
        public const float SandGrain = 0.14f;

        /// <summary>
        /// Where the lane lies across the strip between the lawn's straight edge (0) and the squares (1): where its
        /// asphalt begins, where its painted line runs, and where the asphalt ends.
        /// </summary>
        public const float LaneInner = 0.08f;
        public const float LineAt = 0.58f;
        public const float LaneOuter = 0.94f;

        /// <summary>The painted line's width, as a share of the strip's width.</summary>
        public const float LineShare = 0.12f;

        /// <summary>How long a dash and its gap are together, about (the count is rounded so the dashes meet up round the road), and how much of that is painted.</summary>
        public const float DashStep = 1f / 14f;
        public const float DashShare = 0.55f;

        /// <summary>The sandy fringe the grass keeps between itself and the asphalt: its least and greatest width, in strip widths.</summary>
        public const float FringeMin = 0.07f;
        public const float FringeMax = 0.20f;

        /// <summary>Over how wide a band (in strip widths) an edge of the asphalt breaks up into the sand, and how far that edge wanders in and out.</summary>
        public const float Feather = 0.14f;
        public const float Ragged = 0.10f;

        /// <summary>How many of the sand's grains are small stones.</summary>
        public const float Stones = 0.035f;

        /// <summary>How far from the road's middle the painted line runs along a straight side, for a strip that is <paramref name="strip"/> of the road's size wide.</summary>
        public static float LineReach(float strip) => 1f - 2f * strip * (1f - LineAt);

        /// <summary>The radius of the line's bend at a corner: the lawn's own corner, as far out as the line runs from the lawn.</summary>
        public static float LineBend(float strip) => (1f - 2f * strip) * LawnShape.Corner + 2f * strip * LineAt;

        /// <summary>How long the line is over a quarter of the way round: a straight side and a bend.</summary>
        public static float QuarterLength(float strip) => 2f * (LineReach(strip) - LineBend(strip)) + Mathf.PI / 2f * LineBend(strip);

        /// <summary>
        /// Where a point of the road lies by the painted line: <paramref name="across"/> is how far it is from the line
        /// (positive towards the squares, negative towards the lawn), <paramref name="along"/> how far along the line
        /// from the middle of the nearest side, round the bend up to the corner's diagonal.
        /// </summary>
        public static void Lane(Vector2 point, float strip, out float across, out float along)
        {
            float x = Mathf.Abs(point.x), y = Mathf.Abs(point.y);
            float far = Mathf.Max(x, y), near = Mathf.Min(x, y);
            float reach = LineReach(strip), bend = LineBend(strip), straight = reach - bend;
            if (near <= straight)
            {
                across = far - reach;
                along = near;
                return;
            }

            // Round the corner the line is an arc whose middle lies where the straight parts end.
            float dx = far - straight, dy = near - straight;
            across = Mathf.Sqrt(dx * dx + dy * dy) - bend;
            along = straight + bend * Mathf.Atan2(dy, dx);
        }

        /// <summary>Whether a point of the road (in half-roads from its middle) lies on a painted dash of the line.</summary>
        public static bool OnLine(Vector2 point, float strip) => Line(point, strip, 0f) > 0.5f;

        /// <summary>
        /// How much of the painted line covers a point (0-1); <paramref name="soft"/> is the width its edges fade over,
        /// so the texture's dashes have no jagged edges.
        /// </summary>
        public static float Line(Vector2 point, float strip, float soft)
        {
            Lane(point, strip, out float across, out float along);
            float halfLine = strip * LineShare;
            float quarter = QuarterLength(strip);
            float step = quarter / Mathf.Max(1f, Mathf.Round(quarter / DashStep));
            // A dash sits on the middle of each side, and they repeat from there round the bend.
            float phase = along / step;
            float fromDash = Mathf.Abs(phase - Mathf.Round(phase)) * step - step * DashShare / 2f;
            float fromLine = Mathf.Max(Mathf.Abs(across) - halfLine, fromDash);
            return soft <= 0f ? (fromLine <= 0f ? 1f : 0f) : Mathf.Clamp01(0.5f - fromLine / soft);
        }

        /// <summary>
        /// How much of a point is asphalt (0 sand, 1 asphalt), before the grains are thrown: inside the lane, short of
        /// the squares, and beyond the sandy fringe of the grass. <paramref name="rough"/> (0-1) is the place's noise:
        /// it widens the fringe and moves the lane's edges in and out.
        /// </summary>
        public static float Cover(Vector2 point, float strip, float rough)
        {
            float width = 2f * strip;
            Lane(point, strip, out float across, out _);
            float fromInner = across + (LineAt - LaneInner) * width;
            float fromOuter = (LaneOuter - LineAt) * width - across;
            float lawnHalf = 1f - width;
            float beyondGrass = point.magnitude - lawnHalf * LawnShape.QuickRadius(Mathf.Atan2(point.y, point.x));
            float fromFringe = beyondGrass - width * Mathf.Lerp(FringeMin, FringeMax, rough);
            float depth = Mathf.Min(Mathf.Min(fromInner, fromOuter), fromFringe) + width * Ragged * (rough - 0.5f);
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(depth / (width * Feather)));
        }

        /// <summary>
        /// The road's texture for a strip <paramref name="strip"/> wide (a share of the road's size) between the road's
        /// edge and the lawn's straight edge.
        /// </summary>
        public static Texture2D Create(float strip)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                name = "road", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 8, hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[Size * Size];
            var random = new System.Random(11);
            var drift = new GridNoise(31, 31, 12);
            var ragged = new GridNoise(113, 113, 13);
            var tone = new GridNoise(47, 47, 14);
            // Nearer the middle than this the lawn covers the road whichever way its edge wanders: plain sand, never seen.
            float hidden = (1f - 2f * strip) * (1f - LawnShape.Corner * 0.3f - LawnShape.Inward - LawnShape.Fine) - 0.01f;
            float soft = 2.5f / Size;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
                    float grain = (float)random.NextDouble(), chance = (float)random.NextDouble();
                    var point = new Vector2(2f * u - 1f, 2f * v - 1f);
                    Color colour;
                    if (Mathf.Max(Mathf.Abs(point.x), Mathf.Abs(point.y)) < hidden)
                    {
                        colour = SandDeep;
                    }
                    else
                    {
                        float cover = Cover(point, strip, 0.55f * drift.At(u, v) + 0.45f * ragged.At(u, v));
                        // Grain by grain: the nearer the asphalt's edge, the more of its grains are sand.
                        float sandy = 1f - cover;
                        if (chance < sandy * Mathf.Sqrt(sandy))
                        {
                            colour = Color.Lerp(SandDeep, Sand, tone.At(u, v)) * (1f + SandGrain * (grain - 0.5f));
                            if (grain < Stones) colour = Color.Lerp(colour, Stone, 0.75f);
                            else if (grain > 1f - Stones) colour = Color.Lerp(colour, Color.white, 0.25f);
                        }
                        else
                        {
                            // Dust lightens the asphalt a little where the sand is near.
                            colour = Color.Lerp(Asphalt, Sand, 0.3f * sandy) * (1f + Grain * (grain - 0.5f));
                            colour = Color.Lerp(colour, Paint, Line(point, strip, soft) * (0.9f + 0.1f * grain));
                        }
                    }

                    colour.a = 1f;
                    pixels[y * Size + x] = colour;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }
    }
}
