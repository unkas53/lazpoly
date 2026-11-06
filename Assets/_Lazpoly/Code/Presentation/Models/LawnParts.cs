using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// What stands on the lawn in the middle of the board (the owner wants it plain grass otherwise): the
    /// glass jar the tea money collects in, with its coins. The jar is a parent object with its base at
    /// y = 0, built from the game's own meshes.
    /// </summary>
    public static class LawnParts
    {
        public static readonly Color Glass = new Color(0.78f, 0.88f, 0.92f);
        public static readonly Color Lid = new Color(0.72f, 0.30f, 0.22f);
        public static readonly Color Coin = new Color(0.86f, 0.70f, 0.32f);

        /// <summary>A coin's radius and thickness, and how many sit in a stack before the next one starts.</summary>
        public const float CoinRadius = 0.085f;
        public const float CoinThickness = 0.024f;
        public const int CoinsPerStack = 6;
        public const int MaxStacks = 5;

        /// <summary>A glass jar with a red lid, one unit tall, base at y = 0: the tea money's box.</summary>
        public static GameObject Jar(string name, Transform parent)
        {
            GameObject root = Root(name, parent);
            Part(root, "Jar", JarShape(), Glass, Vector3.zero, Vector3.one);
            Part(root, "Lid", MeshFactory.Cylinder(16), Lid, new Vector3(0f, 0.86f, 0f), new Vector3(0.24f, 0.14f, 0.24f));
            return root;
        }

        /// <summary>The jar's turned shape: a round-shouldered body on a flat foot with a short neck.</summary>
        public static Mesh JarShape() => MeshFactory.Lathe("lawn:jar", new[]
        {
            new Vector2(0f, 0f), new Vector2(0.3f, 0f), new Vector2(0.34f, 0.08f), new Vector2(0.36f, 0.4f), new Vector2(0.34f, 0.7f),
            new Vector2(0.24f, 0.82f), new Vector2(0.22f, 0.88f), new Vector2(0f, 0.88f),
        }, 20);

        /// <summary>One coin, lying flat with its base at y = 0.</summary>
        public static GameObject CoinPiece(string name, Transform parent)
        {
            GameObject coin = ModelParts.Create(name, parent, MeshFactory.Cylinder(16), Coin, 0.45f);
            coin.transform.localScale = new Vector3(CoinRadius, CoinThickness, CoinRadius);
            return coin;
        }

        /// <summary>Where the <paramref name="index"/>th coin of a pile lies: stacks of <see cref="CoinsPerStack"/> set round the jar.</summary>
        public static Vector3 CoinSpot(int index, float jarRadius)
        {
            int stack = (index / CoinsPerStack) % MaxStacks;
            int layer = index % CoinsPerStack;
            // Stacks sit round the jar's foot, a little apart, with a slight turn so they do not line up like a grid.
            float angle = (stack * 137f + 20f) * Mathf.Deg2Rad;
            float radius = jarRadius + CoinRadius * 1.3f;
            return new Vector3(Mathf.Cos(angle) * radius, layer * CoinThickness, Mathf.Sin(angle) * radius);
        }

        /// <summary>How many coins a pot shows: one per <paramref name="coinValue"/>, never more than the stacks hold.</summary>
        public static int CoinsFor(int pot, int coinValue) =>
            Mathf.Clamp(pot <= 0 ? 0 : (pot + coinValue - 1) / coinValue, 0, CoinsPerStack * MaxStacks);

        private static GameObject Root(string name, Transform parent)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            return root;
        }

        private static void Part(GameObject root, string name, Mesh mesh, Color color, Vector3 position, Vector3 scale, Quaternion? rotation = null)
        {
            GameObject part = ModelParts.Create(name, root.transform, mesh, color, 0.15f);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            if (rotation.HasValue) part.transform.localRotation = rotation.Value;
        }
    }
}
