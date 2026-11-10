using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The printed look of a square: a soft paper grain and a thin frame line along the
    /// edge, drawn once into a small texture that every tile shares. The tile's own colour tints it,
    /// so one texture serves pastel properties and special squares alike. A town's tile has its own
    /// texture with its group's colour printed into it (<see cref="Town"/>).
    /// </summary>
    public static class TileSkin
    {
        public const int Size = 128;

        /// <summary>How far the paper's brightness wanders: a few percent, a grain rather than a pattern.</summary>
        public const float Grain = 0.1f;

        /// <summary>A print screen of soft dots over the paper: how many across the tile, each dot's radius (of the tile) and how dark.</summary>
        public const int DotsAcross = 12;
        public const float DotRadius = 0.014f;
        public const float DotShade = 0.06f;

        /// <summary>
        /// Where the frame line runs, as a fraction of the tile's size in from each edge: on the edge itself since the squares
        /// lie side by side (Faz 25, the owner's wish), so two neighbours' lines meet in one line along their seam.
        /// </summary>
        public const float FrameInset = FrameWidth;

        /// <summary>Half the frame line's thickness, as a fraction of the tile's size.</summary>
        public const float FrameWidth = 0.008f;

        /// <summary>How much darker the frame line is than the paper around it.</summary>
        public const float FrameShade = 0.6f;

        private static Texture2D _paper;

        /// <summary>The pattern in a town's colour (<see cref="Town"/>): soft patches a little darker and lighter, and fine specks, like worn stone or a lawn.</summary>
        public const float PatchShade = 0.14f;
        public const float SpeckShade = 0.16f;
        public const int SpecksAcross = 36;
        public const float SpeckChance = 0.18f;

        /// <summary>The pattern's brightness (0-1) at a point, u and v both 0-1: patches of noise under scattered specks.</summary>
        public static float PatternValue(float u, float v)
        {
            // Soft patches: the noise pushed towards its ends, so the patches have shape instead of a uniform wash.
            float patch = Mathf.SmoothStep(0f, 1f, Noise(u * 5f + 11f, v * 5f + 3f));
            float value = 1f - PatchShade * patch + 0.03f * Noise(u * 23f, v * 23f);
            // Specks: in a few cells of a fine grid, a small round dot, some dark and some light.
            float cx = u * SpecksAcross, cy = v * SpecksAcross;
            int ix = Mathf.FloorToInt(cx), iy = Mathf.FloorToInt(cy);
            float pick = Hash(ix + 101, iy + 37);
            if (pick < SpeckChance)
            {
                float dx = cx - ix - 0.5f, dy = cy - iy - 0.5f;
                float dot = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / 0.32f);
                value += (pick < SpeckChance * 0.6f ? -SpeckShade : SpeckShade * 0.5f) * dot;
            }

            return Mathf.Clamp(value, 0.75f, 1f);
        }

        /// <summary>
        /// The paving stones under a town's colour (the owner's wish, Faz 31: "hafiften belli olacak şekilde parke taşı"):
        /// rows of 2:1 stones laid in a running bond, <see cref="StonesAcross"/> to a tile's width, each a touch lighter or
        /// darker than the next, with thin soft joints between them. Kept faint so the colour and the name still lead.
        /// </summary>
        public const int StonesAcross = 7;
        public const float JointWidth = 0.1f;
        public const float JointShade = 0.12f;
        public const float StoneShade = 0.045f;

        /// <summary>
        /// The paving's brightness (about 0.85-1) at a point measured in tile widths (<paramref name="x"/> along the tile,
        /// <paramref name="y"/> across it), so the stones keep their shape on a deep tile.
        /// </summary>
        public static float StoneValue(float x, float y)
        {
            float width = 1f / StonesAcross, height = width * 0.5f;
            float rowAt = Mathf.Max(0f, y) / height;
            int row = Mathf.FloorToInt(rowAt);
            float colAt = Mathf.Max(0f, x) / width + ((row & 1) == 1 ? 0.5f : 0f);
            int col = Mathf.FloorToInt(colAt);
            float fx = colAt - col, fy = rowAt - row;
            // The distance to the nearest joint, in stone heights: the long sides count twice as far across.
            float edge = Mathf.Min(Mathf.Min(fx, 1f - fx) * (width / height), Mathf.Min(fy, 1f - fy));
            float joint = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((edge - JointWidth * 0.5f) / JointWidth));
            float stone = 1f + StoneShade * (2f * Hash(col * 7 + 3, row * 13 + 5) - 1f);
            // A soft light on each stone's upper edge, so the stones read as raised a little.
            stone += 0.02f * (1f - Mathf.Clamp01(fy / 0.3f));
            return stone * (1f - JointShade * joint);
        }

        /// <summary>The shared paper texture, made on first use.</summary>
        public static Texture2D Paper()
        {
            if (_paper == null) _paper = Make(Size);
            return _paper;
        }

        public static Texture2D Make(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "tile-paper",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8, // the tiles lie at a slant to the camera
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    byte value = (byte)Mathf.RoundToInt(255f * Value((x + 0.5f) / size, (y + 0.5f) / size));
                    pixels[y * size + x] = new Color32(value, value, value, 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            return texture;
        }

        /// <summary>The paper's brightness (0-1) at a point of the tile, u along and v across, both 0-1.</summary>
        public static float Value(float u, float v) => PaperGrain(u, v) * (OnFrame(u, v) ? FrameShade : 1f);

        /// <summary>The paper without its frame line: soft clouds under a fine grain, and the print screen's dots.</summary>
        private static float PaperGrain(float u, float v)
        {
            // Three scales of noise: soft clouds under a fine grain, so it reads as paper rather than wash.
            float paper = 1f - Grain * (0.35f * Noise(u * 9f, v * 9f) + 0.3f * Noise(u * 29f, v * 29f) + 0.35f * Noise(u * 67f, v * 67f));
            // The dot screen sits on the half-steps, so no dot falls on the frame line or the edge.
            float du = Mathf.Repeat(u * DotsAcross, 1f) - 0.5f;
            float dv = Mathf.Repeat(v * DotsAcross, 1f) - 0.5f;
            float dot = Mathf.Sqrt(du * du + dv * dv) / DotsAcross;
            paper *= 1f - DotShade * Mathf.Clamp01(1f - dot / DotRadius);
            return paper;
        }

        /// <summary>Whether a point of the tile (u, v both 0-1) lies on the frame line.</summary>
        public static bool OnFrame(float u, float v) => Mathf.Abs(FrameDistance(u, v)) <= FrameWidth;

        /// <summary>
        /// How much the frame's corners are rounded (of the tile's size): not at all since the squares are square-cornered
        /// and lie side by side (Faz 25, the owner's wish); the lines of neighbours meet in straight seams.
        /// </summary>
        public const float FrameCorner = 0f;

        /// <summary>
        /// How far a point of the tile lies outside the frame's rounded rectangle (negative inside it), in the tile's
        /// size: zero on the frame line's middle.
        /// </summary>
        public static float FrameDistance(float u, float v)
        {
            float reach = 0.5f - FrameInset - FrameCorner;
            float qx = Mathf.Abs(u - 0.5f) - reach, qy = Mathf.Abs(v - 0.5f) - reach;
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - FrameCorner;
        }

        /// <summary>
        /// Whether a point lies on the line between a town's two parts, at <paramref name="split"/> (v): as thick and as
        /// dark as the frame line, and running from one side of the frame to the other, so it joins the frame.
        /// </summary>
        public static bool OnDivider(float u, float v, float split)
        {
            bool inside = u > FrameInset + FrameWidth && u < 1f - FrameInset - FrameWidth;
            return inside && Mathf.Abs(v - split) <= FrameWidth;
        }

        /// <summary>The width in pixels of a town tile's skin (<see cref="Town"/>); its height follows the tile's shape.</summary>
        public const int TownWidth = 192;

        private static readonly System.Collections.Generic.Dictionary<string, Texture2D> Towns = new System.Collections.Generic.Dictionary<string, Texture2D>();

        /// <summary>
        /// A town tile's whole skin, printed flat into the tile's own face: the group's colour with the pattern from
        /// <paramref name="bandFrom"/> to <paramref name="bandTo"/>, inside the frame, (v, 0-1, across the tile), the paper in
        /// <paramref name="paving"/> elsewhere, and the frame line running round the tile over both and across it between
        /// them (<see cref="OnDivider"/>), in the paper's frame tone; the colour stays inside the frame. The tile is drawn
        /// untinted (white), since the colours are in the texture. <paramref name="depthOverWidth"/> is the tile's depth
        /// over its width, so the pixels stay square. Towns of one colour and shape share one texture.
        /// </summary>
        public static Texture2D Town(Color band, Color paving, float bandFrom, float bandTo, float depthOverWidth)
        {
            int width = TownWidth;
            int height = Mathf.Max(1, Mathf.RoundToInt(width * depthOverWidth));
            string key = $"{ColorUtility.ToHtmlStringRGB(band)}:{ColorUtility.ToHtmlStringRGB(paving)}:{bandFrom:F3}:{bandTo:F3}:{height}";
            if (Towns.TryGetValue(key, out Texture2D cached) && cached != null) return cached;

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
            {
                name = $"tile-town-{key}",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8, // the tiles lie at a slant to the camera
            };
            // The paper and the pattern are the same for every colour, so they are worked out once per shape and reused.
            float[] paper = Grid($"grain:{width}:{height}", width, height, PaperGrain);
            float span = Mathf.Max(1e-4f, bandTo - bandFrom);
            // The worn-stone pattern over paving stones (Faz 31), the stones measured in tile widths so they stay 2:1.
            float[] pattern = Grid($"pattern:{width}:{height}:{bandFrom:F3}:{bandTo:F3}", width, height,
                (u, v) => Mathf.Clamp01(PatternValue(u, Mathf.Clamp01((v - bandFrom) / span)) * StoneValue(u, v * depthOverWidth)));
            // The line between the two parts lies on the band's inner end: the end that is not at the tile's edge.
            float split = bandFrom <= 0f ? bandTo : bandFrom;
            // The colour stays inside the frame: from the frame line on the tile's edge side to the line between the parts.
            float low = Mathf.Max(bandFrom, FrameInset), high = Mathf.Min(bandTo, 1f - FrameInset);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f) / height;
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    float u = (x + 0.5f) / width;
                    // The frame and the line between the parts are in the paper's own frame tone, as on every other
                    // square; they lie over the colour's edges, so those need no smoothing.
                    bool coloured = FrameDistance(u, v) < 0f && v > low && v < high;
                    Color colour = coloured ? band * pattern[i] : paving * paper[i];
                    if (OnFrame(u, v) || OnDivider(u, v, split)) colour = paving * paper[i] * FrameShade;
                    colour.a = 1f;
                    pixels[i] = colour;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            Towns[key] = texture;
            return texture;
        }

        /// <summary>
        /// A factory city's paving (the owner's wish, 2026-11-11: dark grey, with larger paving stones than a town's): rows
        /// of setts with rounded corners in a running bond, <see cref="FactoryStonesAcross"/> to a tile's width (a town's are
        /// seven), each its own shade of the grey, with plain dark joints and a little grit, so it reads as an industrial yard.
        /// </summary>
        public const int FactoryStonesAcross = 4;
        public const float FactoryJointWidth = 0.07f;
        public const float FactoryJointShade = 0.42f;
        public const float FactoryStoneShade = 0.09f;
        public const float FactoryStoneHeight = 0.7f; // of its width
        public const float FactoryStoneRound = 0.2f;  // of its height

        /// <summary>The factory paving's brightness (about 0.55-1.1) at a point measured in tile widths, like <see cref="StoneValue"/>.</summary>
        public static float FactoryStoneValue(float x, float y)
        {
            float width = 1f / FactoryStonesAcross, height = width * FactoryStoneHeight;
            float rowAt = Mathf.Max(0f, y) / height;
            int row = Mathf.FloorToInt(rowAt);
            float colAt = Mathf.Max(0f, x) / width + ((row & 1) == 1 ? 0.5f : 0f);
            int col = Mathf.FloorToInt(colAt);
            float fx = colAt - col, fy = rowAt - row;
            // The distance to the nearest joint, in stone heights, round at the corners.
            float ex = Mathf.Min(fx, 1f - fx) * (width / height), ey = Mathf.Min(fy, 1f - fy);
            const float round = FactoryStoneRound;
            float edge = ex < round && ey < round ? round - Mathf.Sqrt((round - ex) * (round - ex) + (round - ey) * (round - ey)) : Mathf.Min(ex, ey);
            float joint = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((edge - FactoryJointWidth * 0.5f) / (FactoryJointWidth * 0.6f)));
            float stone = 1f + FactoryStoneShade * (2f * Hash(col * 11 + 5, row * 17 + 3) - 1f);
            // Each stone's upper edge catches a little light, its lower edge a little shade: the setts read as raised.
            stone += 0.05f * (1f - Mathf.Clamp01(fy / 0.25f)) - 0.04f * (1f - Mathf.Clamp01((1f - fy) / 0.25f));
            // Grit: fine noise over the stone.
            stone *= 0.96f + 0.08f * Noise(x * 90f + 7f, y * 90f + 13f);
            return stone * (1f - FactoryJointShade * joint);
        }

        /// <summary>
        /// A factory city tile's whole skin, printed flat into the tile's own face like a town's: <paramref name="stone"/>
        /// paving (<see cref="FactoryStoneValue"/>) inside the frame and the frame line round it. The tile is drawn white.
        /// <paramref name="depthOverWidth"/> is the tile's depth over its width, so the stones keep their shape.
        /// </summary>
        public static Texture2D Factory(Color stone, float depthOverWidth)
        {
            int width = TownWidth;
            int height = Mathf.Max(1, Mathf.RoundToInt(width * depthOverWidth));
            string key = $"factory:{ColorUtility.ToHtmlStringRGB(stone)}:{height}";
            if (Towns.TryGetValue(key, out Texture2D cached) && cached != null) return cached;

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
            {
                name = $"tile-{key}",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8, // the tiles lie at a slant to the camera
            };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f) / height;
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    Color colour = stone * FactoryStoneValue(u, v * depthOverWidth);
                    if (OnFrame(u, v)) colour = stone * FrameShade;
                    colour.a = 1f;
                    pixels[y * width + x] = colour;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            Towns[key] = texture;
            return texture;
        }

        private static readonly System.Collections.Generic.Dictionary<string, float[]> Grids = new System.Collections.Generic.Dictionary<string, float[]>();

        /// <summary>A brightness at every pixel's centre of a width x height texture, worked out once per key.</summary>
        private static float[] Grid(string key, int width, int height, System.Func<float, float, float> value)
        {
            if (Grids.TryGetValue(key, out float[] grid)) return grid;
            grid = new float[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++) grid[y * width + x] = value((x + 0.5f) / width, (y + 0.5f) / height);
            }

            Grids[key] = grid;
            return grid;
        }

        /// <summary>Smooth value noise in 0-1: a random value at every whole number, blended between.</summary>
        private static float Noise(float x, float y)
        {
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            float fx = Mathf.SmoothStep(0f, 1f, x - ix);
            float fy = Mathf.SmoothStep(0f, 1f, y - iy);
            float top = Mathf.Lerp(Hash(ix, iy + 1), Hash(ix + 1, iy + 1), fx);
            float bottom = Mathf.Lerp(Hash(ix, iy), Hash(ix + 1, iy), fx);
            return Mathf.Lerp(bottom, top, fy);
        }

        private static float Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393) + (uint)(y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / 4294967295f;
        }
    }
}
