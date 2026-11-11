using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// A little floating island for a player's money safe to stand on (the owner's wish, 2026-11-11): a meadow top with a
    /// lip of deeper grass and a few tufts at its edge, a band of soil with roots hanging out of it, and under it rock in
    /// layers that narrow, each a little askew, down to a point, with a few loose stones sticking out. The top is the size
    /// it is given, at y 0, so the safe stands on it as it stood on its shelf; everything else hangs below. A seed makes
    /// each island a little different.
    /// </summary>
    public static class IslandParts
    {
        public static readonly Color Grass = LawnSkin.Light;
        public static readonly Color GrassDeep = LawnSkin.Deep;
        public static readonly Color Soil = new Color(0.40f, 0.29f, 0.19f);
        public static readonly Color Root = new Color(0.30f, 0.22f, 0.14f);
        public static readonly Color Rock = new Color(0.50f, 0.47f, 0.43f);
        public static readonly Color RockDark = new Color(0.38f, 0.36f, 0.33f);

        /// <summary>The meadow's and the soil's thickness.</summary>
        public const float GrassThickness = 0.07f;
        public const float SoilThickness = 0.16f;

        /// <summary>The rock layers under the soil: each one's share of the top's size, and its height.</summary>
        public static readonly float[] LayerShares = { 0.82f, 0.6f, 0.38f };
        public static readonly float[] LayerHeights = { 0.2f, 0.2f, 0.18f };

        /// <summary>The point under the last layer: its height, and its radius as a share of the top's smaller side.</summary>
        public const float SpikeHeight = 0.38f;
        public const float SpikeShare = 0.2f;

        /// <summary>How deep the whole island hangs under its top.</summary>
        public static float Depth
        {
            get
            {
                // The meadow, the soil and each layer reach a little into the one above; the point sets in under the last layer.
                float depth = GrassThickness - Overlap / 2f + SoilThickness - Overlap - Overlap + SpikeHeight;
                foreach (float height in LayerHeights) depth += height - Overlap;
                return depth;
            }
        }

        /// <summary>How far each layer reaches up into the one above, so no seam shows.</summary>
        private const float Overlap = 0.02f;

        /// <summary>Builds an island under <paramref name="parent"/> whose top is <paramref name="top"/> (x across, z deep) at y 0.</summary>
        public static GameObject Create(Transform parent, string name, Vector2 top, int seed)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            Transform at = root.transform;
            var random = new System.Random(seed * 7919 + 17);
            float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);

            // The meadow, a lip of deeper grass round it, and the soil under it.
            Part(at, "Meadow", MeshFactory.RoundedBox(new Vector3(top.x, GrassThickness, top.y), 0.03f, 2), Grass, new Vector3(0f, -GrassThickness / 2f, 0f), Quaternion.identity, 0.05f);
            Part(at, "Meadow lip", MeshFactory.RoundedBox(new Vector3(top.x * 0.995f, 0.05f, top.y * 0.99f), 0.025f, 2), GrassDeep,
                new Vector3(0f, -GrassThickness + 0.01f, 0f), Quaternion.identity, 0.05f);
            float y = -GrassThickness + Overlap / 2f;
            Part(at, "Soil", MeshFactory.RoundedBox(new Vector3(top.x * 0.96f, SoilThickness, top.y * 0.93f), 0.06f, 2), Soil,
                new Vector3(0f, y - SoilThickness / 2f, 0f), Quaternion.identity, 0.05f);
            y -= SoilThickness - Overlap;

            // Rock in layers, narrowing and a little askew, down to a point.
            Vector2 drift = Vector2.zero;
            for (int layer = 0; layer < LayerShares.Length; layer++)
            {
                float share = LayerShares[layer], height = LayerHeights[layer];
                drift += new Vector2(Range(-0.05f, 0.05f) * top.x, Range(-0.05f, 0.05f) * top.y) * share;
                Quaternion turn = Quaternion.Euler(0f, Range(-4f, 4f), 0f);
                Part(at, $"Rock {layer + 1}", MeshFactory.RoundedBox(new Vector3(top.x * share, height, top.y * share * Range(0.9f, 1f)), 0.07f, 2),
                    layer % 2 == 0 ? Rock : RockDark, new Vector3(drift.x, y - height / 2f, drift.y), turn, 0.15f);
                y -= height - Overlap;
            }

            float spike = Mathf.Min(top.x, top.y) * SpikeShare;
            Part(at, "Spike", MeshFactory.Cone(10), RockDark, new Vector3(drift.x, y + Overlap, drift.y), Quaternion.Euler(180f, Range(0f, 360f), 0f), 0.15f,
                new Vector3(spike, SpikeHeight, spike * Range(0.8f, 1f)));

            // Loose stones sticking out of the soil and the first layer, kept within the top's outline.
            var stones = new List<KeyValuePair<Mesh, Matrix4x4>>();
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f + Range(-20f, 20f);
                Vector3 way = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                float size = Range(0.1f, 0.18f);
                var spot = new Vector3(way.x * (top.x * 0.44f - size / 2f), Range(-0.34f, -0.18f), way.z * (top.y * 0.42f - size / 2f));
                stones.Add(new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.RoundedBox(Vector3.one * size, size * 0.3f, 2),
                    Matrix4x4.TRS(spot, Quaternion.Euler(Range(0f, 40f), Range(0f, 360f), Range(0f, 40f)), new Vector3(1f, Range(0.6f, 0.9f), Range(0.8f, 1f)))));
            }

            Part(at, "Stones", MeshFactory.Combine($"island-stones:{seed}:{top.x:F2}:{top.y:F2}", stones), Rock, Vector3.zero, Quaternion.identity, 0.15f);

            // Roots hanging out of the soil near the edge.
            var roots = new List<KeyValuePair<Mesh, Matrix4x4>>();
            for (int i = 0; i < 7; i++)
            {
                float angle = i * (360f / 7f) + Range(-15f, 15f);
                Vector3 way = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                float length = Range(0.12f, 0.26f);
                var spot = new Vector3(way.x * top.x * 0.43f, -GrassThickness - SoilThickness + 0.04f, way.z * top.y * 0.4f);
                roots.Add(new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Cylinder(6),
                    Matrix4x4.TRS(spot, Quaternion.Euler(180f + Range(-15f, 15f), 0f, Range(-15f, 15f)), new Vector3(0.012f, length, 0.012f))));
            }

            Part(at, "Roots", MeshFactory.Combine($"island-roots:{seed}:{top.x:F2}:{top.y:F2}", roots), Root, Vector3.zero, Quaternion.identity, 0.05f);

            // Tufts of grass round the meadow's edge, clear of the middle where the safe stands.
            var tufts = new List<KeyValuePair<Mesh, Matrix4x4>>();
            for (int i = 0; i < 10; i++)
            {
                float u = Range(-0.47f, 0.47f), v = i % 2 == 0 ? -0.46f : 0.46f;
                Vector3 spot = i < 6 ? new Vector3(u * top.x, 0f, v * top.y) : new Vector3((i % 2 == 0 ? -0.47f : 0.47f) * top.x, 0f, Range(-0.4f, 0.4f) * top.y);
                for (int blade = 0; blade < 3; blade++)
                {
                    tufts.Add(new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Cone(5),
                        Matrix4x4.TRS(spot + new Vector3(Range(-0.02f, 0.02f), 0f, Range(-0.02f, 0.02f)), Quaternion.Euler(Range(-20f, 20f), 0f, Range(-20f, 20f)),
                            new Vector3(0.012f, Range(0.05f, 0.09f), 0.012f))));
                }
            }

            Part(at, "Tufts", MeshFactory.Combine($"island-tufts:{seed}:{top.x:F2}:{top.y:F2}", tufts), GrassDeep, Vector3.zero, Quaternion.identity, 0.05f);
            return root;
        }

        private static void Part(Transform parent, string name, Mesh mesh, Color color, Vector3 at, Quaternion turn, float smoothness, Vector3? scale = null)
        {
            GameObject part = ModelParts.Create(name, parent, mesh, color, smoothness);
            part.transform.localPosition = at;
            part.transform.localRotation = turn;
            if (scale.HasValue) part.transform.localScale = scale.Value;
        }
    }
}
