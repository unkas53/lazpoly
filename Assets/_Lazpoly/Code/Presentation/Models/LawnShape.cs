using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The lawn's outline (the owner's wish, 2026-11-10: its edges not dead straight, natural): a square with well
    /// rounded corners whose edge wanders in and out like a real lawn's, a handful of slow waves and a fine ripple laid
    /// over each other. Everything is in half-lawns: 1 is the distance from the middle to the middle of a straight
    /// edge. The same outline cuts the mesh (<see cref="Build"/>) and tells the texture where the grass ends
    /// (<see cref="LawnSkin"/>). The waves are fixed numbers, so every table has the same lawn.
    /// </summary>
    public static class LawnShape
    {
        /// <summary>How round the corners are.</summary>
        public const float Corner = 0.26f;

        /// <summary>How far the edge may wander in from the straight edge and out over the road (short of the road's painted line).</summary>
        public const float Inward = 0.055f;
        public const float Outward = 0.028f;

        /// <summary>The fine ripple on top of the waves.</summary>
        public const float Fine = 0.003f;

        /// <summary>How wide the bank is that falls from the flat top to the edge.</summary>
        public const float Bevel = 0.008f;

        /// <summary>How far the texture reaches from the middle: past the furthest the edge can wander.</summary>
        public const float Reach = 1.05f;

        public const int Segments = 576;

        // Waves round the outline: how many times each runs round, and how strong it is; then their starts.
        private static readonly int[] WaveCount = { 3, 5, 7, 11, 16, 23, 34 };
        private static readonly float[] WaveStrength = { 0.5f, 0.7f, 1f, 0.9f, 0.7f, 0.45f, 0.25f };
        private static readonly float[] WaveStart = { 2.035f, 0.948f, 4.09f, 0.455f, 3.367f, 2.298f, 0.364f };
        private static readonly int[] RippleCount = { 55, 89 };
        private static readonly float[] RippleStrength = { 0.6f, 0.4f };
        private static readonly float[] RippleStart = { 3.188f, 0.236f };

        private static float _waveScale, _rippleScale;
        private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();

        /// <summary>How far the straight-edged, round-cornered square reaches from the middle along <paramref name="angle"/> (radians).</summary>
        public static float BaseRadius(float angle)
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

        /// <summary>How far the edge has wandered from the straight one along <paramref name="angle"/>: between -(Inward + Fine) and Outward + Fine.</summary>
        public static float Wander(float angle)
        {
            if (_waveScale <= 0f) Measure();
            return (Outward - Inward) / 2f + (Outward + Inward) / 2f * Sum(angle, WaveCount, WaveStrength, WaveStart) / _waveScale
                + Fine * Sum(angle, RippleCount, RippleStrength, RippleStart) / _rippleScale;
        }

        /// <summary>How far the lawn reaches from the middle along <paramref name="angle"/>.</summary>
        public static float Radius(float angle) => BaseRadius(angle) + Wander(angle);

        /// <summary>Whether a point (in half-lawns from the middle) lies on the grass, at least <paramref name="margin"/> in from its edge.</summary>
        public static bool Covers(Vector2 point, float margin = 0f) =>
            point.magnitude <= Radius(Mathf.Atan2(point.y, point.x)) - margin;

        /// <summary>
        /// The lawn as a mesh, <paramref name="half"/> from its middle to the middle of a straight edge and
        /// <paramref name="height"/> tall, standing on y = 0: a flat top, a short bank falling to the wandering edge and a
        /// skirt down to the ground. The texture lies flat over all of it (u, v from the x and z, <see cref="Reach"/> wide).
        /// </summary>
        public static Mesh Build(float half, float height)
        {
            string key = $"{half:F3}:{height:F3}";
            if (Meshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;

            var vertices = new List<Vector3>(3 * Segments + 1);
            var normals = new List<Vector3>(3 * Segments + 1);
            var uvs = new List<Vector2>(3 * Segments + 1);
            var triangles = new List<int>(15 * Segments);
            void Add(Vector3 at, Vector3 normal)
            {
                vertices.Add(at);
                normals.Add(normal.normalized);
                uvs.Add(new Vector2(at.x / (half * Reach) / 2f + 0.5f, at.z / (half * Reach) / 2f + 0.5f));
            }

            Add(new Vector3(0f, height, 0f), Vector3.up);
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * 2f * Mathf.PI / Segments;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float radius = Radius(angle) * half;
                Add(outward * (radius - Bevel * half) + Vector3.up * height, Vector3.up);           // The top's rim.
                Add(outward * radius + Vector3.up * (height * BankFoot), outward * 0.75f + Vector3.up * 0.65f); // The bank's foot.
                Add(outward * radius, outward);                                                     // On the ground.
            }

            for (int i = 0; i < Segments; i++)
            {
                int a = 1 + 3 * i, b = 1 + 3 * ((i + 1) % Segments);
                triangles.Add(0); triangles.Add(b); triangles.Add(a);
                for (int ring = 0; ring < 2; ring++)
                {
                    int p = a + ring, pNext = b + ring, q = p + 1, qNext = pNext + 1;
                    triangles.Add(p); triangles.Add(pNext); triangles.Add(q);
                    triangles.Add(pNext); triangles.Add(qNext); triangles.Add(q);
                }
            }

            var mesh = new Mesh { name = "lawn:" + key };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            Meshes[key] = mesh;
            return mesh;
        }

        /// <summary>How high the bank's foot stands, as a share of the lawn's height.</summary>
        public const float BankFoot = 0.45f;

        private static float Sum(float angle, int[] counts, float[] strengths, float[] starts)
        {
            float sum = 0f;
            for (int i = 0; i < counts.Length; i++) sum += strengths[i] * Mathf.Sin(counts[i] * angle + starts[i]);
            return sum;
        }

        /// <summary>The largest swing of the waves and of the ripple, so each can be scaled to its allowance.</summary>
        private static void Measure()
        {
            const int Steps = 8192;
            float waves = 0f, ripple = 0f;
            for (int i = 0; i < Steps; i++)
            {
                float angle = i * 2f * Mathf.PI / Steps;
                waves = Mathf.Max(waves, Mathf.Abs(Sum(angle, WaveCount, WaveStrength, WaveStart)));
                ripple = Mathf.Max(ripple, Mathf.Abs(Sum(angle, RippleCount, RippleStrength, RippleStart)));
            }

            _waveScale = waves;
            _rippleScale = ripple;
        }
    }
}
