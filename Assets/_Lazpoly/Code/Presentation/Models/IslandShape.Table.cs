using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The great island the table itself rests on (the owner's wish, 2026-11-12: a floating island under the table too,
    /// suiting the atmosphere), cut in the same manner as the little ones under the safes: under the table's stone a band
    /// of soil with grass hanging over its edge in tongues, then rock stepping in ledge by ledge, growing rounder and
    /// leaning, down to a point, with teeth of rock hanging from its face, boulders bedded in it, long vines and loose
    /// stones adrift beside it. Its top is a square exactly as wide as asked, at y 0, with sharp corners like the table's;
    /// nothing reaches past that outline, so the table's edge stays the widest thing (the owner's wish for the plate).
    /// </summary>
    public static partial class IslandShape
    {
        /// <summary>How deep the table's island hangs under the table.</summary>
        public const float TableDepth = 6f;

        /// <summary>
        /// How far under the table's near corner the island may show in the game's slanted view, in world units up the
        /// screen: the interface's bar lies just under that corner, and the island must end above it. So the body is
        /// drawn round quickly under the table's corners, and what hangs (grass, vines, teeth) is short there.
        /// </summary>
        public const float TableShow = 0.45f;

        /// <summary>How deep its soil reaches, and where each ledge of rock begins below the top.</summary>
        public const float TableSoil = 0.75f;
        public static readonly float[] TableLedges = { 0.78f, 2.20f, 3.70f, 5.10f };

        /// <summary>How much of the table's outline the island takes at its widest: a hair inside, so the stone's edge shows whole.</summary>
        public const float TableShare = 0.985f;

        /// <summary>The grass hanging from under the table's edge: its shortest and its longest tongue.</summary>
        public const float TableSkirtShort = 0.16f;
        public const float TableSkirtLong = 0.78f;

        public const int TableSegments = 96;
        public const int TableSkirtSegments = 192;

        /// <summary>How many times the rock's picture repeats round the island, so its grain is as fine as on the little ones.</summary>
        public const int TableTurns = 6;

        /// <summary>How many vines hang from under the table's edge, and how many loose stones drift beside the rock.</summary>
        public const int TableVines = 14;
        public const int TableStones = 7;

        // The body's rings from the top down, as for the little islands: how deep, what share of the table's outline,
        // how round (0 the table's square, 1 a circle), and how much each point strays.
        private static readonly float[] TableRingDepth = { 0.03f, 0.40f, 0.75f, 0.78f, 1.60f, 2.10f, 2.20f, 3.10f, 3.60f, 3.70f, 4.50f, 5.00f, 5.10f, 5.60f };
        private static readonly float[] TableRingShare = { 0.985f, 0.975f, 0.955f, 0.982f, 0.970f, 0.92f, 0.90f, 0.84f, 0.70f, 0.67f, 0.56f, 0.40f, 0.37f, 0.20f };
        private static readonly float[] TableRingRound = { 0f, 0.03f, 0.12f, 0.15f, 0.60f, 0.72f, 0.75f, 0.85f, 0.90f, 0.92f, 0.96f, 1f, 1f, 1f };
        private static readonly float[] TableRingStray = { 0f, 0.004f, 0.008f, 0.010f, 0.015f, 0.025f, 0.020f, 0.030f, 0.040f, 0.035f, 0.050f, 0.070f, 0.060f, 0.090f };

        /// <summary>How much of the table's outline the island takes at a depth under the table (0 at the point).</summary>
        public static float TableShareAt(float depth)
        {
            if (depth <= TableRingDepth[0]) return TableRingShare[0];
            for (int ring = 1; ring < TableRingDepth.Length; ring++)
            {
                if (depth > TableRingDepth[ring]) continue;
                return Mathf.Lerp(TableRingShare[ring - 1], TableRingShare[ring], (depth - TableRingDepth[ring - 1]) / (TableRingDepth[ring] - TableRingDepth[ring - 1]));
            }

            int last = TableRingDepth.Length - 1;
            return Mathf.Lerp(TableRingShare[last], 0f, (depth - TableRingDepth[last]) / (TableDepth - TableRingDepth[last]));
        }

        /// <summary>
        /// A point of the plain outline for a table <paramref name="half"/> its width: <paramref name="angle"/> round it in
        /// radians, <paramref name="round"/> from the table's sharp square (0) to a circle (1).
        /// </summary>
        public static Vector2 TableOutline(float half, float angle, float round)
        {
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            float square = 1f / Mathf.Max(Mathf.Max(Mathf.Abs(c), Mathf.Abs(s)), 1e-6f);
            float reach = Mathf.Lerp(square, 1f, round);
            return new Vector2(c * reach * half, s * reach * half);
        }

        /// <summary>How far the island's middle has leant sideways at a depth: nothing under the table, the most at the point.</summary>
        public static Vector2 TableLeanAt(float width, int seed, float depth)
        {
            var random = new System.Random(seed * 104729 + 9);
            float x = ((float)random.NextDouble() - 0.5f) * 0.24f * width, z = ((float)random.NextDouble() - 0.5f) * 0.24f * width;
            float t = Mathf.Clamp01(depth / TableDepth);
            return new Vector2(x, z) * (t * t);
        }

        /// <summary>
        /// Where a depth under the table lies on the rock's picture (<see cref="IslandSkin.RockAt"/>, drawn for the little
        /// islands): soil on soil and each ledge on its ledge, so the big island wears the same layers.
        /// </summary>
        public static float TableV(float depth)
        {
            float[] from = { 0f, TableSoil, TableLedges[0], TableLedges[1], TableLedges[2], TableLedges[3], TableDepth };
            float[] to = { 0f, SoilDepth, Ledges[0], Ledges[1], Ledges[2], Ledges[3], Depth };
            depth = Mathf.Clamp(depth, 0f, TableDepth);
            for (int i = 1; i < from.Length; i++)
            {
                if (depth > from[i]) continue;
                return 1f - Mathf.Lerp(to[i - 1], to[i], (depth - from[i - 1]) / (from[i] - from[i - 1])) / Depth;
            }

            return 0f;
        }

        /// <summary>A point of the table's edge: <paramref name="along"/> once round it (0-1), from the middle of one side, by the way round rather than by angle.</summary>
        public static Vector2 TableEdge(float half, float along)
        {
            along = Mathf.Repeat(along, 1f) * 4f;
            int side = Mathf.Min(3, (int)along);
            float t = (along - side) * 2f - 1f; // -1 to 1 along the side
            switch (side)
            {
                case 0: return new Vector2(half, t * half);
                case 1: return new Vector2(-t * half, half);
                case 2: return new Vector2(-half, -t * half);
                default: return new Vector2(t * half, -half);
            }
        }

        /// <summary>
        /// How far along its side a point of the table's edge is from the nearest corner: 0 at a corner, 1 at the middle
        /// of a side. What hangs from the edge is shortest at the corners (<see cref="TableShow"/>).
        /// </summary>
        public static float TableFromCorner(float along)
        {
            float side = Mathf.Repeat(along, 1f) * 4f;
            return 1f - Mathf.Abs((side - Mathf.Floor(side)) * 2f - 1f);
        }

        /// <summary>The grass hanging from under the table's edge, all the way round: a short fringe with longer tongues here and there.</summary>
        public static Mesh TableTurf(float width, int seed)
        {
            string key = TableKey("turf", width, seed);
            if (Meshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;
            var random = new System.Random(seed * 31 + 131);
            float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);
            float half = width / 2f;
            int count = TableSkirtSegments;
            float start = Range(0f, 6.283f), second = Range(0f, 6.283f);
            var hang = new float[count];
            for (int i = 0; i < count; i++)
            {
                float angle = i * 2f * Mathf.PI / count;
                float lobe = Mathf.Abs(Mathf.Sin(19f * angle + start + 0.8f * Mathf.Sin(5f * angle + second)));
                float slow = 0.5f + 0.5f * Mathf.Sin(3f * angle + second);
                float tongue = Mathf.Lerp(TableSkirtShort, TableSkirtLong, Mathf.Clamp01(lobe * lobe * lobe * (0.35f + 0.65f * slow) + Range(-0.04f, 0.06f)));
                hang[i] = Mathf.Max(TableSkirtShort, tongue * Mathf.Lerp(0.5f, 1f, TableFromCorner((float)i / count)));
            }

            var build = new Builder();
            var inside = new Vector3(0f, -0.4f, 0f);
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                Vector2 e0 = TableEdge(half, (float)i / count), e1 = TableEdge(half, (float)next / count);
                Vector3 a = Flat(e0 * 0.975f, 0f), b = Flat(e1 * 0.975f, 0f);
                Vector3 a1 = Flat(e0 * 0.993f, -0.07f), b1 = Flat(e1 * 0.993f, -0.07f);
                Vector3 a2 = Flat(e0 * 0.982f, -hang[i]), b2 = Flat(e1 * 0.982f, -hang[next]);
                build.Quad(a, b, b1, a1, new Vector2(0.5f, 0.8f), new Vector2(0.5f, 0.8f), new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.7f), inside);
                float va = 0.7f - 0.7f * Mathf.Clamp01((hang[i] - 0.07f) / (TableSkirtLong - 0.07f)), vb = 0.7f - 0.7f * Mathf.Clamp01((hang[next] - 0.07f) / (TableSkirtLong - 0.07f));
                build.Quad(a1, b1, b2, a2, new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.7f), new Vector2(0.5f, vb), new Vector2(0.5f, va), inside);
            }

            build.Soften();
            return Meshes[key] = build.Mesh(key);
        }

        /// <summary>
        /// The body under the table: soil, then rock in ledges down to a leaning point, teeth of rock hanging from its face
        /// and boulders bedded in it. Its picture runs round it (u, <see cref="TableTurns"/> times) and down it by <see cref="TableV"/>.
        /// </summary>
        public static Mesh TableRock(float width, int seed)
        {
            string key = TableKey("rock", width, seed);
            if (Meshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;
            var random = new System.Random(seed * 53 + 223);
            float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);
            float half = width / 2f;
            int rings = TableRingDepth.Length, count = TableSegments;
            // The body is not a turned shape: it bulges and draws in round its waist, more the deeper it hangs.
            float p1 = Range(0f, 6.283f), p2 = Range(0f, 6.283f), p3 = Range(0f, 6.283f);
            float Waist(float angle, float depth)
            {
                float wave = 0.5f + 0.25f * Mathf.Sin(3f * angle + p1) + 0.15f * Mathf.Sin(5f * angle + p2) + 0.1f * Mathf.Sin(8f * angle + p3);
                return 1f - 0.16f * Mathf.Clamp01((depth - TableSoil) / (TableDepth - TableSoil)) * wave;
            }

            var points = new Vector3[rings][];
            for (int ring = 0; ring < rings; ring++)
            {
                points[ring] = new Vector3[count];
                Vector2 lean = TableLeanAt(width, seed, TableRingDepth[ring]);
                for (int i = 0; i < count; i++)
                {
                    float angle = i * 2f * Mathf.PI / count;
                    // Never wider than the body's share of the table, so nothing shows past its edge.
                    float stray = 1f - TableRingStray[ring] * Range(0f, 2f);
                    Vector2 at = TableOutline(half, angle, TableRingRound[ring]) * (TableRingShare[ring] * stray * Waist(angle, TableRingDepth[ring])) + lean * (1f - TableRingShare[ring]);
                    points[ring][i] = new Vector3(at.x, -TableRingDepth[ring] + (ring == 0 ? 0f : Range(-0.05f, 0.05f)), at.y);
                }
            }

            var build = new Builder();
            Vector2 tipLean = TableLeanAt(width, seed, TableDepth);
            var tip = new Vector3(tipLean.x, -TableDepth, tipLean.y);
            for (int ring = 0; ring < rings; ring++)
            {
                bool juts = ring < rings - 1 && TableRingShare[ring + 1] > TableRingShare[ring];
                Vector2 lean = TableLeanAt(width, seed, TableRingDepth[ring]) * (1f - TableRingShare[ring]);
                for (int i = 0; i < count; i++)
                {
                    int next = (i + 1) % count;
                    float u0 = (float)i * TableTurns / count, u1 = (float)(i + 1) * TableTurns / count;
                    Vector3 a = points[ring][i], b = points[ring][next];
                    var out2 = new Vector2((a.x + b.x) / 2f - lean.x, (a.z + b.z) / 2f - lean.y);
                    float away = Mathf.Max(1e-4f, out2.magnitude);
                    var facing = new Vector3(out2.x / away, juts ? 0.7f : -0.7f, out2.y / away);
                    if (ring == rings - 1)
                    {
                        build.Facing(a, b, tip, new Vector2(u0, TableV(-a.y)), new Vector2(u1, TableV(-b.y)), new Vector2((u0 + u1) / 2f, 0f), facing);
                        continue;
                    }

                    Vector3 c = points[ring + 1][next], d = points[ring + 1][i];
                    build.Facing(a, b, c, new Vector2(u0, TableV(-a.y)), new Vector2(u1, TableV(-b.y)), new Vector2(u1, TableV(-c.y)), facing);
                    build.Facing(a, c, d, new Vector2(u0, TableV(-a.y)), new Vector2(u1, TableV(-c.y)), new Vector2(u0, TableV(-d.y)), facing);
                }
            }

            // Teeth of rock hanging from the face under the first three ledges, half bedded in it.
            const int teeth = 13;
            for (int i = 0; i < teeth; i++)
            {
                int ledge = i % 3;
                float depth = Mathf.Lerp(TableLedges[ledge], TableLedges[ledge + 1], Range(0.45f, 0.85f));
                float angle = (i * 360f / teeth + Range(-9f, 9f)) * Mathf.Deg2Rad;
                float share = TableShareAt(depth) * Waist(angle, depth) * Range(0.9f, 0.97f);
                Vector2 lean = TableLeanAt(width, seed, depth);
                Vector2 at = TableOutline(half, angle, 1f) * share + lean * (1f - share);
                float length = Range(0.5f, 1.1f), radius = Range(0.3f, 0.5f);
                var from = new Vector3(at.x, -depth, at.y);
                var to = new Vector3(at.x * 0.9f + Range(-0.1f, 0.1f), Mathf.Max(-TableDepth, -depth - length), at.y * 0.9f + Range(-0.1f, 0.1f));
                TableTooth(build, from, to, radius, 7, Range(0f, 1f), random);
            }

            // Boulders bedded in the soil's and the first ledge's face, half out of it.
            for (int i = 0; i < 26; i++)
            {
                float depth = i % 2 == 0 ? Range(0.42f, 0.66f) : Range(1.0f, 1.8f);
                float angle = (i * 360f / 26f + Range(-5f, 5f)) * Mathf.Deg2Rad;
                float size = Range(0.2f, 0.36f);
                Vector2 face = TableOutline(half, angle, depth < TableLedges[0] ? 0.12f : 0.5f) * TableShareAt(depth);
                float away = face.magnitude;
                Vector2 at = away > 1e-4f ? face * ((away - size * 1.25f) / away) : face;
                float Jitter(float a, float b) => a + (float)random.NextDouble() * (b - a);
                Gem(build, new Vector3(at.x, -depth, at.y), new Vector3(size * Jitter(0.9f, 1.2f), size * Jitter(0.65f, 0.9f), size * Jitter(0.85f, 1.1f)), Jitter(0f, 6.283f),
                    new Vector2(Jitter(0f, 1f), TableV(depth)), 6, random, 0.18f);
            }

            return Meshes[key] = build.Mesh(key);
        }

        /// <summary>A hanging tooth of rock: a ring at <paramref name="from"/> narrowing to <paramref name="to"/>, painted by its depth.</summary>
        private static void TableTooth(Builder build, Vector3 from, Vector3 to, float radius, int sides, float u, System.Random random)
        {
            var ring = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float angle = i * 2f * Mathf.PI / sides, stray = 0.8f + 0.4f * (float)random.NextDouble();
                ring[i] = from + new Vector3(Mathf.Cos(angle) * radius * stray, 0f, Mathf.Sin(angle) * radius * stray);
            }

            Vector3 inside = Vector3.Lerp(from, to, 0.3f);
            Vector3 crown = from + new Vector3(0f, radius * 0.5f, 0f);
            float vFrom = TableV(-from.y), vTo = TableV(-to.y);
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                float u0 = u + 0.3f * i / sides, u1 = u + 0.3f * (i + 1) / sides;
                build.Triangle(ring[i], ring[next], to, new Vector2(u0, vFrom), new Vector2(u1, vFrom), new Vector2((u0 + u1) / 2f, vTo), inside);
                build.Triangle(crown, ring[next], ring[i], new Vector2(u0, vFrom), new Vector2(u1, vFrom), new Vector2(u0, vFrom), inside);
            }
        }

        /// <summary>How far round the table's edge (0-1) a vine is rooted.</summary>
        public static float TableVineAlong(int seed, int vine)
        {
            var random = new System.Random(seed * 71 + 331 + vine * 1013);
            return (vine + 0.15f + 0.7f * (float)random.NextDouble()) / TableVines;
        }

        /// <summary>Where a vine is rooted, under the grass at the table's edge.</summary>
        public static Vector3 TableVineFoot(float width, int seed, int vine)
        {
            return Flat(TableEdge(width / 2f, TableVineAlong(seed, vine)) * 0.972f, -0.12f);
        }

        /// <summary>A long vine hanging from its foot (<see cref="TableVineFoot"/>, here the origin, so it can sway about it), with leaves down its length.</summary>
        public static Mesh TableVine(float width, int seed, int vine)
        {
            string key = TableKey($"vine{vine}", width, seed);
            if (Meshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;
            var random = new System.Random(seed * 71 + 337 + vine * 1013);
            float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);
            Vector3 foot = TableVineFoot(width, seed, vine);
            var out2 = new Vector2(foot.x, foot.z);
            Vector2 inward = out2 * (-0.1f / Mathf.Max(0.01f, out2.magnitude));
            // Long by the middle of a side, short towards a corner.
            float length = Range(1.1f, 2.6f) * Mathf.Lerp(0.12f, 1f, TableFromCorner(TableVineAlong(seed, vine))), sway = Range(0f, 6.283f);
            var build = new Builder();
            Vector3 previous = Vector3.zero;
            const int joints = 7;
            for (int joint = 1; joint <= joints; joint++)
            {
                float t = (float)joint / joints;
                Vector3 here = Flat(inward * t, -length * t) + new Vector3(Mathf.Sin(sway + joint * 1.7f), 0f, Mathf.Cos(sway + joint * 1.3f)) * (0.05f * t);
                Tube(build, previous, here, Mathf.Lerp(0.055f, 0.02f, t - 1f / joints), Mathf.Lerp(0.055f, 0.02f, t), 5, 0.3f);
                Vector3 leaf = here + new Vector3(Range(-0.05f, 0.05f), Range(-0.02f, 0.06f), Range(-0.05f, 0.05f));
                Gem(build, leaf, new Vector3(0.11f, 0.06f, 0.11f) * Range(0.8f, 1.2f), Range(0f, 6.283f), new Vector2(0.5f, Range(0.55f, 0.95f)), 4, random, 0f);
                previous = here;
            }

            build.Soften();
            return Meshes[key] = build.Mesh(key);
        }

        /// <summary>Where the loose stones adrift beside the table's island are (x, y, z; y under the table), and how big each is.</summary>
        public static Vector4[] TableDrifters(float width, int seed)
        {
            var random = new System.Random(seed * 113 + 617);
            float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);
            var stones = new Vector4[TableStones];
            float turn = Range(0f, 6.283f), half = width / 2f;
            for (int i = 0; i < stones.Length; i++)
            {
                float depth = Range(3.3f, 5.3f), size = Range(0.18f, 0.4f);
                float angle = turn + i * 2f * Mathf.PI / stones.Length + Range(-0.3f, 0.3f);
                // Out from the rock at this depth, well inside the table's outline.
                Vector2 lean = TableLeanAt(width, seed, depth);
                float share = Mathf.Min(0.8f, TableShareAt(depth) + Range(0.06f, 0.14f));
                Vector2 at = TableOutline(half, angle, 1f) * share + lean * (1f - share);
                stones[i] = new Vector4(at.x, -depth, at.y, size);
            }

            return stones;
        }

        private static string TableKey(string part, float width, int seed) => $"table-island-{part}:{seed}:{width:F3}";
    }
}
