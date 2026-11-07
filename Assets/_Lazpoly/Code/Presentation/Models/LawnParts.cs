using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// What stands on the lawn in the middle of the board (the owner wants it plain grass otherwise): the bank's
    /// safe (the kasa) that money paid to the bank flies into, and the glass jar the tea money collects in, with its
    /// coins. Each is a parent object with its base at y = 0, built from the game's own meshes.
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

        /// <summary>The safe's steel, its door a shade lighter, and its brass dial, handle and slot frame.</summary>
        public static readonly Color SafeSteel = new Color(0.17f, 0.32f, 0.37f);
        public static readonly Color SafeDoor = new Color(0.24f, 0.42f, 0.47f);
        public static readonly Color Brass = new Color(0.88f, 0.69f, 0.28f);
        public static readonly Color SlotDark = new Color(0.06f, 0.08f, 0.10f);

        /// <summary>Where the money goes in: the slot on the safe's top, in its own units.</summary>
        public static readonly Vector3 SafeSlot = new Vector3(0f, 0.95f, -0.04f);

        /// <summary>
        /// The bank's safe (the kasa), one unit tall with its slot, base at y = 0 and its door at +Z: a steel box on four
        /// feet with a lighter door, a brass dial and handle, two hinges and a money slot on top (<see cref="SafeSlot"/>).
        /// </summary>
        public static GameObject Safe(string name, Transform parent)
        {
            GameObject root = Root(name, parent);
            foreach (float x in new[] { -0.36f, 0.36f })
            {
                foreach (float z in new[] { -0.28f, 0.28f })
                {
                    Part(root, "Foot", MeshFactory.RoundedBox(Vector3.one, 0.01f, 2), SlotDark, new Vector3(x, 0.03f, z), new Vector3(0.1f, 0.06f, 0.1f));
                }
            }

            Part(root, "Body", MeshFactory.RoundedBox(Vector3.one, 0.06f, 3), SafeSteel, new Vector3(0f, 0.5f, 0f), new Vector3(0.92f, 0.88f, 0.76f), smoothness: 0.45f);
            Part(root, "Door", MeshFactory.RoundedBox(Vector3.one, 0.03f, 2), SafeDoor, new Vector3(0f, 0.5f, 0.39f), new Vector3(0.74f, 0.7f, 0.04f), smoothness: 0.45f);
            Part(root, "Dial", MeshFactory.Disc(1f, 24), Brass, new Vector3(-0.1f, 0.56f, 0.41f), new Vector3(0.13f, 0.04f, 0.13f), Quaternion.Euler(90f, 0f, 0f), 0.7f);
            Part(root, "Dial knob", MeshFactory.Disc(1f, 16), SlotDark, new Vector3(-0.1f, 0.56f, 0.45f), new Vector3(0.05f, 0.03f, 0.05f), Quaternion.Euler(90f, 0f, 0f));
            Part(root, "Handle", MeshFactory.RoundedBox(Vector3.one, 0.01f, 2), Brass, new Vector3(0.2f, 0.42f, 0.43f), new Vector3(0.05f, 0.22f, 0.04f), smoothness: 0.7f);
            foreach (float y in new[] { 0.3f, 0.7f })
            {
                Part(root, "Hinge", MeshFactory.Cylinder(12), SlotDark, new Vector3(-0.39f, y - 0.06f, 0.4f), new Vector3(0.025f, 0.12f, 0.025f));
            }

            Part(root, "Slot frame", MeshFactory.RoundedBox(Vector3.one, 0.01f, 2), Brass, new Vector3(0f, 0.945f, -0.04f), new Vector3(0.44f, 0.02f, 0.14f), smoothness: 0.7f);
            Part(root, "Slot", MeshFactory.RoundedBox(Vector3.one, 0.005f, 2), SlotDark, SafeSlot, new Vector3(0.36f, 0.012f, 0.05f));
            return root;
        }

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

        private static void Part(GameObject root, string name, Mesh mesh, Color color, Vector3 position, Vector3 scale, Quaternion? rotation = null, float smoothness = 0.15f)
        {
            GameObject part = ModelParts.Create(name, root.transform, mesh, color, smoothness);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            if (rotation.HasValue) part.transform.localRotation = rotation.Value;
        }
    }
}
