using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// What a far island in the sky has beyond the little islands' meadow and rock (the owner's wish, 2026-11-12: the
    /// islands in the background three-dimensional too): its own size, and a few round-crowned trees on its meadow.
    /// Kept plain, since it is seen small and far off: no vines, tufts or flowers, which would only be specks.
    /// </summary>
    public static partial class IslandShape
    {
        /// <summary>A far island's top (x across, z deep), in its own units; it is sized on the screen by <see cref="FarSpan"/>.</summary>
        public static readonly Vector2 FarTop = new Vector2(3.2f, 2.4f);

        /// <summary>How many trees stand on it, how tall the shortest and the tallest may be, and a crown's radius as a share of its tree's height.</summary>
        public const int FarTrees = 3;
        public const float FarTreeLow = 0.8f;
        public const float FarTreeHigh = 1.15f;
        public const float FarCrown = 0.36f;

        /// <summary>How far under its top a far island's middle lies: half way between the tallest tree's top and the rock's point.</summary>
        public const float FarMiddle = (Depth - FarTreeHigh) / 2f;

        /// <summary>
        /// How high a far island stands on the screen in the game's slanted view, in its own units: from the trees' tops
        /// to the rock's point as the 35° camera sees them, and the top's depth besides.
        /// </summary>
        public const float FarSpan = 4f;

        /// <summary>A far island's tree: where it stands on the top (x, z), how tall it is (y) and its crown's radius (w). The trees stand apart, well in from the edge.</summary>
        public static Vector4 FarTree(int seed, int tree)
        {
            var random = new System.Random(seed * 151 + tree * 37 + 907);
            float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);
            float angle = seed * 0.7f + tree * 2f * Mathf.PI / FarTrees + Range(-0.4f, 0.4f);
            Vector2 at = Outline(FarTop / 2f, angle, 1f) * Range(0.25f, 0.55f);
            float height = Range(FarTreeLow, FarTreeHigh);
            return new Vector4(at.x, height, at.y, height * FarCrown);
        }

        /// <summary>The trees' trunks, standing on the top at y 0.</summary>
        public static Mesh FarTrunks(int seed)
        {
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>();
            for (int i = 0; i < FarTrees; i++)
            {
                Vector4 tree = FarTree(seed, i);
                parts.Add(new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Cylinder(8),
                    Matrix4x4.TRS(new Vector3(tree.x, 0f, tree.z), Quaternion.identity, new Vector3(tree.y * 0.06f, tree.y * 0.6f, tree.y * 0.06f))));
            }

            return MeshFactory.Combine($"far-island-trunks:{seed}", parts);
        }

        /// <summary>The trees' crowns: a round head reaching the tree's height, and two smaller lobes under it at either side.</summary>
        public static Mesh FarCrowns(int seed)
        {
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>();
            for (int i = 0; i < FarTrees; i++)
            {
                Vector4 tree = FarTree(seed, i);
                float radius = tree.w, turn = seed * 1.3f + i * 2.1f;
                var head = new Vector3(tree.x, tree.y - radius * 1.05f, tree.z);
                var side = new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn)) * (radius * 0.6f);
                Ball(parts, head, new Vector3(radius, radius * 1.05f, radius));
                Ball(parts, head + side + Vector3.down * (radius * 0.45f), Vector3.one * (radius * 0.7f));
                Ball(parts, head - side + Vector3.down * (radius * 0.25f), Vector3.one * (radius * 0.6f));
            }

            return MeshFactory.Combine($"far-island-crowns:{seed}", parts);
        }

        private static void Ball(List<KeyValuePair<Mesh, Matrix4x4>> parts, Vector3 at, Vector3 size) =>
            parts.Add(new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Sphere(8), Matrix4x4.TRS(at, Quaternion.identity, size)));
    }
}
