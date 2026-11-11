using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The meshes of a floating island (<see cref="IslandParts"/>), cut in code: a meadow whose outline is a soft
    /// rounded rectangle wandering a little, with grass hanging over its edge in uneven tongues; under it a body of soil
    /// and rock that steps in ledge by ledge, growing rounder and leaning a little, down to a point, with smaller points
    /// hanging beside it and boulders bedded in its face; vines hanging from under the grass; tufts and flowers at the
    /// meadow's edge; and loose stones adrift under it. Everything is cut in flat faces, so the table's light picks out
    /// each one, like chiselled stone. The island's top is at y 0 and is exactly as wide and deep as asked; all the rest
    /// hangs below, inside that outline. A seed makes each island its own.
    /// </summary>
    public static class IslandShape
    {
        /// <summary>How deep the island hangs under its top.</summary>
        public const float Depth = 2.05f;

        /// <summary>How deep the soil reaches under the top, and where each ledge of rock begins below it.</summary>
        public const float SoilDepth = 0.3f;
        public static readonly float[] Ledges = { 0.31f, 0.77f, 1.20f, 1.63f };

        /// <summary>How round the meadow's corners are (of its half size), and how far its edge may wander in.</summary>
        public const float Corner = 0.5f;
        public const float Wander = 0.035f;

        /// <summary>The grass hanging over the edge: its shortest and its longest tongue.</summary>
        public const float SkirtShort = 0.1f;
        public const float SkirtLong = 0.42f;

        /// <summary>How much of the top's size the body under the grass takes at its widest.</summary>
        public const float BodyShare = 0.94f;

        public const int MeadowSegments = 56;
        public const int RockSegments = 20;

        // The body's rings from the top down: how deep, what share of the top's outline, how round (0 the meadow's
        // rounded rectangle, 1 an oval), and how much each point strays. A ledge is a ring set in under the one above.
        private static readonly float[] RingDepth = { 0.02f, 0.16f, 0.30f, 0.31f, 0.62f, 0.75f, 0.77f, 1.05f, 1.18f, 1.20f, 1.45f, 1.60f, 1.63f, 1.85f };
        private static readonly float[] RingShare = { 0.94f, 0.925f, 0.875f, 0.92f, 0.895f, 0.73f, 0.70f, 0.655f, 0.50f, 0.47f, 0.42f, 0.28f, 0.25f, 0.12f };
        private static readonly float[] RingRound = { 0f, 0.05f, 0.1f, 0.1f, 0.22f, 0.32f, 0.36f, 0.5f, 0.6f, 0.64f, 0.78f, 0.88f, 0.9f, 1f };
        private static readonly float[] RingStray = { 0f, 0.015f, 0.02f, 0.03f, 0.045f, 0.06f, 0.05f, 0.065f, 0.08f, 0.07f, 0.09f, 0.12f, 0.1f, 0.14f };

        private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();

        /// <summary>How much of the top's outline the body takes at a depth under the top (0 at the point).</summary>
        public static float ShareAt(float depth)
        {
            if (depth <= RingDepth[0]) return RingShare[0];
            for (int ring = 1; ring < RingDepth.Length; ring++)
            {
                if (depth > RingDepth[ring]) continue;
                return Mathf.Lerp(RingShare[ring - 1], RingShare[ring], (depth - RingDepth[ring - 1]) / (RingDepth[ring] - RingDepth[ring - 1]));
            }

            int last = RingDepth.Length - 1;
            return Mathf.Lerp(RingShare[last], 0f, (depth - RingDepth[last]) / (Depth - RingDepth[last]));
        }

        /// <summary>
        /// A point of the plain outline for a top of <paramref name="half"/> (half its width, half its depth):
        /// <paramref name="angle"/> round it in radians, <paramref name="round"/> from the rounded rectangle (0) to an oval (1).
        /// </summary>
        public static Vector2 Outline(Vector2 half, float angle, float round)
        {
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            float reach = Mathf.Lerp(Square(angle), 0.94f, round);
            return new Vector2(c * reach * half.x, s * reach * half.y);
        }

        /// <summary>The meadow's edge, once round (<see cref="MeadowSegments"/> points): the outline wandering in a little, touching the top's four sides exactly.</summary>
        public static Vector2[] MeadowEdge(Vector2 top, int seed)
        {
            var random = new System.Random(seed * 7919 + 17);
            float a = (float)random.NextDouble() * 6.283f, b = (float)random.NextDouble() * 6.283f, c = (float)random.NextDouble() * 6.283f;
            var half = new Vector2(top.x / 2f, top.y / 2f);
            var edge = new Vector2[MeadowSegments];
            float right = 0f, left = 0f, far = 0f, near = 0f;
            for (int i = 0; i < MeadowSegments; i++)
            {
                float angle = i * 2f * Mathf.PI / MeadowSegments;
                float wave = 0.5f + 0.25f * Mathf.Sin(3f * angle + a) + 0.15f * Mathf.Sin(5f * angle + b) + 0.1f * Mathf.Sin(9f * angle + c);
                Vector2 point = Outline(half, angle, 0f) * (1f - Wander * wave);
                edge[i] = point;
                right = Mathf.Max(right, point.x);
                left = Mathf.Max(left, -point.x);
                far = Mathf.Max(far, point.y);
                near = Mathf.Max(near, -point.y);
            }

            // Stretched back, each side by itself, so the meadow is exactly as wide and deep as asked.
            for (int i = 0; i < MeadowSegments; i++)
            {
                Vector2 point = edge[i];
                edge[i] = new Vector2(point.x * half.x / (point.x >= 0f ? right : left), point.y * half.y / (point.y >= 0f ? far : near));
            }

            return edge;
        }

        /// <summary>How far the body's middle has leant sideways at a depth: nothing under the grass, the most at the point.</summary>
        public static Vector2 LeanAt(Vector2 top, int seed, float depth)
        {
            var random = new System.Random(seed * 104729 + 5);
            float x = ((float)random.NextDouble() - 0.5f) * 0.3f * top.x, z = ((float)random.NextDouble() - 0.5f) * 0.3f * top.y;
            float t = Mathf.Clamp01(depth / Depth);
            return new Vector2(x, z) * (t * t);
        }

        /// <summary>The meadow: its flat top at y 0, a soft shoulder, and the grass hanging over the edge in tongues.</summary>
        public static Mesh Meadow(Vector2 top, int seed)
        {
            string key = Key("meadow", top, seed);
            if (Meshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;
            var random = new System.Random(seed * 31 + 101);
            float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);
            Vector2[] edge = MeadowEdge(top, seed);
            int count = edge.Length;
            // The tongues: a few lobes round the island, each segment a little its own.
            float lobes = 7 + seed % 3, start = Range(0f, 6.283f), second = Range(0f, 6.283f);
            var hang = new float[count];
            for (int i = 0; i < count; i++)
            {
                float angle = i * 2f * Mathf.PI / count;
                float lobe = Mathf.Abs(Mathf.Sin(lobes * 0.5f * angle + start + 0.6f * Mathf.Sin(3f * angle + second)));
                float slow = 0.5f + 0.5f * Mathf.Sin(2f * angle + second);
                hang[i] = Mathf.Lerp(SkirtShort, SkirtLong, Mathf.Clamp01(lobe * lobe * lobe * (0.4f + 0.6f * slow) + Range(-0.05f, 0.07f)));
            }

            var build = new Builder();
            var middle = new Vector3(0f, 0f, 0f);
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                Vector3 a = Flat(edge[i] * 0.965f, 0f), b = Flat(edge[next] * 0.965f, 0f);
                Vector3 a1 = Flat(edge[i], -0.035f), b1 = Flat(edge[next], -0.035f);
                Vector3 a2 = Flat(edge[i] * 0.985f, -hang[i]), b2 = Flat(edge[next] * 0.985f, -hang[next]);
                Vector3 inside = new Vector3(0f, -0.2f, 0f);
                // The top: light in the middle, a little deeper towards the edge.
                build.Triangle(middle, a, b, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.86f), new Vector2(0.5f, 0.86f), new Vector3(0f, -1f, 0f));
                // The shoulder and the hanging grass, deepening to the tongues' tips.
                build.Quad(a, b, b1, a1, new Vector2(0.5f, 0.86f), new Vector2(0.5f, 0.86f), new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.7f), inside);
                float va = 0.7f - 0.7f * Mathf.Clamp01((hang[i] - 0.035f) / (SkirtLong - 0.035f)), vb = 0.7f - 0.7f * Mathf.Clamp01((hang[next] - 0.035f) / (SkirtLong - 0.035f));
                build.Quad(a1, b1, b2, a2, new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.7f), new Vector2(0.5f, vb), new Vector2(0.5f, va), inside);
            }

            return Meshes[key] = build.Mesh(key);
        }

        /// <summary>
        /// The body: soil, then rock in ledges down to a leaning point, smaller points hanging under the ledges and
        /// boulders bedded in the face. Its picture runs round it (u) and from the point (v 0) up to the top (v 1).
        /// </summary>
        public static Mesh Rock(Vector2 top, int seed)
        {
            string key = Key("rock", top, seed);
            if (Meshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;
            var random = new System.Random(seed * 53 + 211);
            float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);
            var half = new Vector2(top.x / 2f, top.y / 2f);
            int rings = RingDepth.Length, count = RockSegments;
            float turn = Range(0f, 6.283f);
            var points = new Vector3[rings][];
            for (int ring = 0; ring < rings; ring++)
            {
                points[ring] = new Vector3[count];
                Vector2 lean = LeanAt(top, seed, RingDepth[ring]);
                for (int i = 0; i < count; i++)
                {
                    float angle = turn + i * 2f * Mathf.PI / count;
                    // Never wider than the body's share of the top, so nothing shows past the grass.
                    float stray = 1f - RingStray[ring] * Range(0f, 2f);
                    Vector2 at = Outline(half, angle, RingRound[ring]) * (RingShare[ring] * stray) + lean * (1f - RingShare[ring]);
                    points[ring][i] = new Vector3(at.x, -RingDepth[ring] + (ring == 0 ? 0f : Range(-0.02f, 0.02f)), at.y);
                }
            }

            var build = new Builder();
            Vector2 tipLean = LeanAt(top, seed, Depth);
            var tip = new Vector3(tipLean.x, -Depth, tipLean.y);
            for (int ring = 0; ring < rings; ring++)
            {
                // A face looks out from the body's middle, and down where the body narrows below it, up where a ledge juts out.
                bool juts = ring < rings - 1 && RingShare[ring + 1] > RingShare[ring];
                Vector2 lean = LeanAt(top, seed, RingDepth[ring]) * (1f - RingShare[ring]);
                for (int i = 0; i < count; i++)
                {
                    int next = (i + 1) % count;
                    float u0 = (float)i / count, u1 = (float)(i + 1) / count;
                    Vector3 a = points[ring][i], b = points[ring][next];
                    var out2 = new Vector2((a.x + b.x) / 2f - lean.x, (a.z + b.z) / 2f - lean.y);
                    float away = Mathf.Max(1e-4f, out2.magnitude);
                    var facing = new Vector3(out2.x / away, juts ? 0.7f : -0.7f, out2.y / away);
                    if (ring == rings - 1)
                    {
                        build.Facing(a, b, tip, new Vector2(u0, V(a.y)), new Vector2(u1, V(b.y)), new Vector2((u0 + u1) / 2f, 0f), facing);
                        continue;
                    }

                    Vector3 c = points[ring + 1][next], d = points[ring + 1][i];
                    build.Facing(a, b, c, new Vector2(u0, V(a.y)), new Vector2(u1, V(b.y)), new Vector2(u1, V(c.y)), facing);
                    build.Facing(a, c, d, new Vector2(u0, V(a.y)), new Vector2(u1, V(c.y)), new Vector2(u0, V(d.y)), facing);
                }
            }

            // Smaller points hanging under the first two ledges.
            for (int i = 0; i < 5; i++)
            {
                int ledge = i % 2;
                float depth = Ledges[ledge + 1] - 0.05f, share = ShareAt(depth + 0.1f) * Range(0.72f, 0.92f);
                float angle = turn + (i * 72f + Range(-22f, 22f)) * Mathf.Deg2Rad;
                Vector2 lean = LeanAt(top, seed, depth);
                Vector2 at = Outline(half, angle, RingRound[ledge == 0 ? 6 : 9]) * share + lean * (1f - share);
                float length = Range(0.24f, 0.46f), radius = Range(0.07f, 0.11f);
                var from = new Vector3(at.x, -depth, at.y);
                var to = new Vector3(at.x + Range(-0.05f, 0.05f), Mathf.Max(-Depth, -depth - length), at.y + Range(-0.05f, 0.05f));
                Spike(build, from, to, radius, 6, Range(0f, 1f), random);
            }

            // Boulders bedded in the soil's and the first ledge's face, half out of it.
            for (int i = 0; i < 7; i++)
            {
                float depth = i % 2 == 0 ? Range(0.12f, 0.26f) : Range(0.36f, 0.6f);
                float angle = turn + (i * 51.4f + Range(-16f, 16f)) * Mathf.Deg2Rad;
                float size = Range(0.07f, 0.11f);
                Vector2 face = Outline(half, angle, 0.1f) * ShareAt(depth);
                float away = face.magnitude;
                Vector2 at = away > 1e-4f ? face * ((away - size * 0.9f) / away) : face;
                Boulder(build, new Vector3(at.x, -depth, at.y), size, Range(0f, 1f), V(-depth), random);
            }

            return Meshes[key] = build.Mesh(key);
        }

        /// <summary>Vines hanging from under the grass, each a thin strand tapering down with a few leaves on it.</summary>
        public static Mesh Vines(Vector2 top, int seed)
        {
            string key = Key("vines", top, seed);
            if (Meshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;
            var random = new System.Random(seed * 71 + 307);
            float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);
            Vector2[] edge = MeadowEdge(top, seed);
            var build = new Builder();
            const int vines = 8;
            for (int vine = 0; vine < vines; vine++)
            {
                int at = (int)((vine + Range(0.15f, 0.85f)) * edge.Length / vines) % edge.Length;
                Vector2 foot = edge[at] * 0.93f;
                float length = Range(0.4f, 0.85f), sway = Range(0f, 6.283f);
                Vector2 inward = foot * (-0.06f / Mathf.Max(0.01f, foot.magnitude));
                Vector3 previous = Flat(foot, -0.06f);
                const int joints = 4;
                for (int joint = 1; joint <= joints; joint++)
                {
                    float t = (float)joint / joints;
                    // Hanging straight, drawn in a little towards the rock, swaying a touch from joint to joint.
                    Vector3 here = Flat(foot + inward * t, -0.06f - length * t) + new Vector3(Mathf.Sin(sway + joint * 1.7f), 0f, Mathf.Cos(sway + joint * 1.3f)) * (0.02f * t);
                    Tube(build, previous, here, Mathf.Lerp(0.03f, 0.012f, (t - 1f / joints)), Mathf.Lerp(0.03f, 0.012f, t), 5, 0.3f);
                    if (joint < joints || length > 0.6f)
                    {
                        Vector3 leaf = here + new Vector3(Range(-0.02f, 0.02f), Range(-0.01f, 0.03f), Range(-0.02f, 0.02f));
                        Gem(build, leaf, new Vector3(0.05f, 0.03f, 0.05f) * Range(0.8f, 1.15f), Range(0f, 6.283f), new Vector2(0.5f, Range(0.55f, 0.95f)), 4, random, 0f);
                    }

                    previous = here;
                }
            }

            return Meshes[key] = build.Mesh(key);
        }

        /// <summary>Tufts of grass standing at the meadow's edge, clear of the middle where the safe stands.</summary>
        public static Mesh Tufts(Vector2 top, int seed)
        {
            string key = Key("tufts", top, seed);
            if (Meshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;
            var random = new System.Random(seed * 89 + 401);
            float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);
            Vector2[] edge = MeadowEdge(top, seed);
            var build = new Builder();
            const int tufts = 16;
            for (int tuft = 0; tuft < tufts; tuft++)
            {
                int at = (int)((tuft + Range(0.1f, 0.9f)) * edge.Length / tufts) % edge.Length;
                Vector2 spot = edge[at] * Range(0.93f, 0.965f);
                int blades = 3 + tuft % 2;
                for (int blade = 0; blade < blades; blade++)
                {
                    float height = Range(0.09f, 0.17f), angle = Range(0f, 6.283f), tilt = Range(0.01f, 0.045f);
                    Vector3 from = Flat(spot, -0.005f) + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.012f;
                    // Leaning inwards rather than out, so no blade shows past the island's outline.
                    Vector2 lean = spot * (-tilt * 0.5f / Mathf.Max(0.01f, spot.magnitude));
                    Vector3 to = from + new Vector3(Mathf.Cos(angle) * tilt * 0.5f + lean.x, height, Mathf.Sin(angle) * tilt * 0.5f + lean.y);
                    Tube(build, from, to, 0.02f, 0.002f, 4, Range(0.5f, 1f));
                }
            }

            return Meshes[key] = build.Mesh(key);
        }

        /// <summary>A few small flowers at the meadow's edge.</summary>
        public static Mesh Flowers(Vector2 top, int seed)
        {
            string key = Key("flowers", top, seed);
            if (Meshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;
            var random = new System.Random(seed * 97 + 503);
            float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);
            Vector2[] edge = MeadowEdge(top, seed);
            var build = new Builder();
            const int flowers = 9;
            for (int flower = 0; flower < flowers; flower++)
            {
                int at = (int)((flower + Range(0.1f, 0.9f)) * edge.Length / flowers) % edge.Length;
                Vector2 spot = edge[at] * Range(0.93f, 0.955f);
                float height = Range(0.05f, 0.09f);
                Gem(build, Flat(spot, height), new Vector3(0.04f, 0.025f, 0.04f) * Range(0.85f, 1.2f), Range(0f, 6.283f), new Vector2(0.5f, 0.5f), 6, random, 0f);
            }

            return Meshes[key] = build.Mesh(key);
        }

        /// <summary>Where the loose stones adrift under the island are (x, y, z; y under the top), and how big each is.</summary>
        public static Vector4[] Drifters(Vector2 top, int seed)
        {
            var random = new System.Random(seed * 113 + 601);
            float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);
            var stones = new Vector4[4];
            float turn = Range(0f, 6.283f);
            for (int i = 0; i < stones.Length; i++)
            {
                float depth = Range(1.2f, 1.82f), size = Range(0.06f, 0.115f);
                float angle = turn + i * Mathf.PI / 2f + Range(-0.5f, 0.5f);
                // Out from the rock at this depth, well inside the top's outline.
                Vector2 lean = LeanAt(top, seed, depth);
                float share = Mathf.Min(0.8f, ShareAt(depth) + Range(0.22f, 0.34f));
                Vector2 at = Outline(new Vector2(top.x / 2f, top.y / 2f), angle, 1f) * share + lean * (1f - share);
                stones[i] = new Vector4(at.x, -depth, at.y, size);
            }

            return stones;
        }

        /// <summary>One loose stone of size 1 about its own middle, to be placed and sized by <see cref="Drifters"/>.</summary>
        public static Mesh Drifter(int seed)
        {
            string key = $"island-drifter:{seed}";
            if (Meshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;
            var random = new System.Random(seed * 127 + 701);
            var build = new Builder();
            Boulder(build, Vector3.zero, 1f, (float)random.NextDouble(), 0.45f, random);
            return Meshes[key] = build.Mesh(key);
        }

        private static string Key(string part, Vector2 top, int seed) => $"island-{part}:{seed}:{top.x:F3}:{top.y:F3}";

        private static Vector3 Flat(Vector2 point, float y) => new Vector3(point.x, y, point.y);

        /// <summary>The body's picture runs from the point (0) up to the top (1).</summary>
        private static float V(float y) => Mathf.Clamp01(1f + y / Depth);

        /// <summary>How far a square with corners rounded by <see cref="Corner"/> reaches from its middle along an angle (1 at the middle of a side).</summary>
        private static float Square(float angle)
        {
            float c = Mathf.Abs(Mathf.Cos(angle)), s = Mathf.Abs(Mathf.Sin(angle));
            float big = Mathf.Max(c, s), small = Mathf.Min(c, s);
            float flat = 1f / Mathf.Max(big, 1e-6f);
            float inner = 1f - Corner;
            if (small * flat <= inner) return flat;
            // The ray meets the corner's circle, whose middle lies at (inner, inner).
            float along = (big + small) * inner;
            return along + Mathf.Sqrt(Mathf.Max(0f, along * along - (2f * inner * inner - Corner * Corner)));
        }

        /// <summary>A hanging point of rock: a ring at <paramref name="from"/> narrowing to <paramref name="to"/>.</summary>
        private static void Spike(Builder build, Vector3 from, Vector3 to, float radius, int sides, float u, System.Random random)
        {
            var ring = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float angle = i * 2f * Mathf.PI / sides, stray = 0.8f + 0.4f * (float)random.NextDouble();
                ring[i] = from + new Vector3(Mathf.Cos(angle) * radius * stray, 0f, Mathf.Sin(angle) * radius * stray);
            }

            Vector3 inside = Vector3.Lerp(from, to, 0.3f);
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                float u0 = u + 0.12f * i / sides, u1 = u + 0.12f * (i + 1) / sides;
                build.Triangle(ring[i], ring[next], to, new Vector2(u0, V(from.y)), new Vector2(u1, V(from.y)), new Vector2((u0 + u1) / 2f, V(to.y)), inside);
                build.Triangle(from + new Vector3(0f, 0.03f, 0f), ring[next], ring[i], new Vector2(u0, V(from.y)), new Vector2(u1, V(from.y)), new Vector2(u0, V(from.y)), inside);
            }
        }

        /// <summary>A boulder: a rough stone of chiselled faces about <paramref name="at"/>.</summary>
        private static void Boulder(Builder build, Vector3 at, float size, float u, float v, System.Random random)
        {
            float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);
            Gem(build, at, new Vector3(size * Range(0.9f, 1.2f), size * Range(0.65f, 0.9f), size * Range(0.85f, 1.1f)), Range(0f, 6.283f), new Vector2(u, v), 6, random, 0.18f);
        }

        /// <summary>
        /// A small faceted solid about <paramref name="at"/>: a point above, a ring round the middle, a point below.
        /// <paramref name="size"/> is its half size, <paramref name="rough"/> how much each ring point strays.
        /// </summary>
        private static void Gem(Builder build, Vector3 at, Vector3 size, float turn, Vector2 uv, int sides, System.Random random, float rough)
        {
            var ring = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float angle = turn + i * 2f * Mathf.PI / sides;
                float stray = 1f + rough * ((float)random.NextDouble() * 2f - 1f);
                ring[i] = at + new Vector3(Mathf.Cos(angle) * size.x * stray, rough * size.y * ((float)random.NextDouble() - 0.5f), Mathf.Sin(angle) * size.z * stray);
            }

            Vector3 above = at + new Vector3(0f, size.y, 0f), below = at - new Vector3(0f, size.y, 0f);
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                build.Triangle(above, ring[i], ring[next], uv, uv, uv, at);
                build.Triangle(below, ring[next], ring[i], uv, uv, uv, at);
            }
        }

        /// <summary>A strand from one point to another, a ring of <paramref name="sides"/> at each end; its picture's v is <paramref name="v"/> all along.</summary>
        private static void Tube(Builder build, Vector3 from, Vector3 to, float fromRadius, float toRadius, int sides, float v)
        {
            Vector3 middle = (from + to) * 0.5f;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * 2f * Mathf.PI / sides, a1 = (i + 1) * 2f * Mathf.PI / sides;
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                var uv = new Vector2(0.5f, v);
                build.Quad(from + d0 * fromRadius, from + d1 * fromRadius, to + d1 * toRadius, to + d0 * toRadius, uv, uv, uv, uv, middle);
            }
        }

        /// <summary>
        /// Gathers flat faces: every face has its own corners and its own facing, so the light shows each as cut.
        /// Faces are turned to look away from a point inside the solid they belong to.
        /// </summary>
        private sealed class Builder
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<Vector2> _uvs = new List<Vector2>();
            private readonly List<int> _triangles = new List<int>();

            /// <summary>A face turned to look away from <paramref name="inside"/>, a point inside its solid.</summary>
            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, Vector3 inside) =>
                Facing(a, b, c, ua, ub, uc, (a + b + c) * (1f / 3f) - inside);

            /// <summary>A face turned to look along <paramref name="facing"/> rather than against it.</summary>
            public void Facing(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, Vector3 facing)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a);
                float length = normal.magnitude;
                if (length < 1e-9f) return;
                normal = normal * (1f / length);
                // Unity's faces look along Cross(b - a, c - a); turn the face if that looks the wrong way.
                if (Vector3.Dot(normal, facing) < 0f)
                {
                    Vector3 swap = b;
                    b = c;
                    c = swap;
                    Vector2 swapUv = ub;
                    ub = uc;
                    uc = swapUv;
                    normal = normal * -1f;
                }

                int start = _vertices.Count;
                _vertices.Add(a);
                _vertices.Add(b);
                _vertices.Add(c);
                _normals.Add(normal);
                _normals.Add(normal);
                _normals.Add(normal);
                _uvs.Add(ua);
                _uvs.Add(ub);
                _uvs.Add(uc);
                _triangles.Add(start);
                _triangles.Add(start + 1);
                _triangles.Add(start + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 inside)
            {
                Triangle(a, b, c, ua, ub, uc, inside);
                Triangle(a, c, d, ua, uc, ud, inside);
            }

            public Mesh Mesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(_vertices);
                mesh.SetNormals(_normals);
                mesh.SetUVs(0, _uvs);
                mesh.SetTriangles(_triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
