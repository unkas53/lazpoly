using Lazpoly.Core.Board;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The little scenes that stand on the four corner squares and say what the corner is, in the
    /// town's own words: a pier with its lighthouse at the start, a tea garden at the tea break, a
    /// newspaper kiosk for the street news and a minibus stop at the bus station. Each is a parent
    /// object with its base at y = 0, one unit wide (x) and deep (z), built from the game's own meshes
    /// so the board can scale and place it. The board turns a scene's +Z outward; the far corners' scenes
    /// (kiosk, bus stop) keep their open side at -Z, towards the camera.
    /// </summary>
    public static class CornerParts
    {
        public static readonly Color Timber = BuildingParts.Timber;
        public static readonly Color DarkTimber = BuildingParts.DarkTimber;
        public static readonly Color Whitewash = new Color(0.96f, 0.95f, 0.90f);
        public static readonly Color Red = new Color(0.80f, 0.26f, 0.22f);
        public static readonly Color Brass = new Color(0.82f, 0.66f, 0.30f);
        public static readonly Color Sea = new Color(0.30f, 0.55f, 0.80f);
        public static readonly Color Leaf = new Color(0.24f, 0.48f, 0.36f);
        public static readonly Color Slate = new Color(0.36f, 0.40f, 0.46f);
        public static readonly Color Glass = new Color(0.72f, 0.84f, 0.92f);
        public static readonly Color Orange = new Color(0.93f, 0.55f, 0.18f);
        public static readonly Color Tea = new Color(0.72f, 0.30f, 0.16f);

        /// <summary>Whether a corner of this type carries a scene.</summary>
        public static bool Has(SquareType type) =>
            type == SquareType.Start || type == SquareType.Pot || type == SquareType.Card || type == SquareType.Travel;

        /// <summary>The scene for a corner square's type, or null when the type has none.</summary>
        public static GameObject For(SquareType type, string name, Transform parent)
        {
            switch (type)
            {
                case SquareType.Start: return Pier(name, parent);
                case SquareType.Pot: return TeaGarden(name, parent);
                case SquareType.Card: return Kiosk(name, parent);
                case SquareType.Travel: return BusStop(name, parent);
                default: return null;
            }
        }

        /// <summary>A timber pier on posts with the lighthouse at its end and a rowing boat moored beside it.</summary>
        public static GameObject Pier(string name, Transform parent)
        {
            GameObject root = Root(name, parent);
            Part(root, "Deck", MeshFactory.RoundedBox(Vector3.one, 0.03f, 2), Timber, new Vector3(0f, 0.15f, 0.12f), new Vector3(0.96f, 0.06f, 0.5f));
            foreach (float x in new[] { -0.42f, 0.42f })
            {
                foreach (float z in new[] { -0.08f, 0.32f })
                {
                    Part(root, "Post", MeshFactory.Cylinder(8), DarkTimber, new Vector3(x, 0f, z), new Vector3(0.035f, 0.16f, 0.035f));
                }
            }

            // The pawn-sized lighthouse is one mesh; its red band and the lamp room are laid over it.
            Part(root, "Tower", PawnShapes.Lighthouse(), Whitewash, new Vector3(-0.2f, 0.18f, 0.12f), Vector3.one * 1.4f);
            Part(root, "Band", MeshFactory.Cylinder(16), Red, new Vector3(-0.2f, 0.46f, 0.12f), new Vector3(0.17f, 0.11f, 0.17f));
            Part(root, "Lamp", MeshFactory.Cylinder(12), Brass, new Vector3(-0.2f, 1f, 0.12f), new Vector3(0.1f, 0.1f, 0.1f));
            Part(root, "Cap", MeshFactory.Cone(12), Red, new Vector3(-0.2f, 1.1f, 0.12f), new Vector3(0.13f, 0.12f, 0.13f));
            Part(root, "Boat", PawnShapes.Boat(), Sea, new Vector3(0.22f, 0f, -0.28f), Vector3.one * 0.9f, Quaternion.Euler(0f, 70f, 0f));
            return root;
        }

        /// <summary>A round table under a red parasol with three stools, a samovar and a glass of tea on it.</summary>
        public static GameObject TeaGarden(string name, Transform parent)
        {
            GameObject root = Root(name, parent);
            Part(root, "Pole", MeshFactory.Cylinder(8), Slate, Vector3.zero, new Vector3(0.02f, 0.92f, 0.02f));
            Part(root, "Parasol", MeshFactory.Cone(16), Red, new Vector3(0f, 0.74f, 0f), new Vector3(0.47f, 0.2f, 0.47f));
            Part(root, "Leg", MeshFactory.Cylinder(8), Slate, Vector3.zero, new Vector3(0.03f, 0.32f, 0.03f));
            Part(root, "Table", MeshFactory.Disc(0.12f, 24), Whitewash, new Vector3(0f, 0.3f, 0f), new Vector3(0.24f, 0.25f, 0.24f));
            for (int i = 0; i < 3; i++)
            {
                float angle = (i * 120f + 60f) * Mathf.Deg2Rad;
                Part(root, "Stool", MeshFactory.Cylinder(10), Timber, new Vector3(Mathf.Cos(angle) * 0.36f, 0f, Mathf.Sin(angle) * 0.36f), new Vector3(0.075f, 0.2f, 0.075f));
            }

            Part(root, "Samovar", Samovar(), Brass, new Vector3(-0.06f, 0.33f, -0.02f), Vector3.one * 0.3f);
            Part(root, "Tea glass", PawnShapes.TeaGlass(), Tea, new Vector3(0.11f, 0.33f, 0.07f), Vector3.one * 0.22f);
            return root;
        }

        /// <summary>A green kiosk with a red awning over its counter, newspapers stacked on it and a sign on the roof; the counter faces -Z.</summary>
        public static GameObject Kiosk(string name, Transform parent)
        {
            GameObject root = Root(name, parent);
            Part(root, "Body", MeshFactory.RoundedBox(Vector3.one, 0.04f, 3), Leaf, new Vector3(0f, 0.37f, 0.08f), new Vector3(0.7f, 0.74f, 0.52f));
            Part(root, "Roof", MeshFactory.RoundedBox(Vector3.one, 0.03f, 2), DarkTimber, new Vector3(0f, 0.77f, 0.08f), new Vector3(0.8f, 0.06f, 0.62f));
            Part(root, "Sign", MeshFactory.RoundedBox(Vector3.one, 0.01f, 2), Whitewash, new Vector3(0f, 0.86f, -0.1f), new Vector3(0.5f, 0.12f, 0.04f));
            Part(root, "Awning", MeshFactory.RoundedBox(Vector3.one, 0.01f, 2), Red, new Vector3(0f, 0.64f, -0.32f), new Vector3(0.8f, 0.03f, 0.32f), Quaternion.Euler(-14f, 0f, 0f));
            Part(root, "Counter", MeshFactory.RoundedBox(Vector3.one, 0.01f, 2), Timber, new Vector3(0f, 0.42f, -0.27f), new Vector3(0.74f, 0.05f, 0.2f));
            Part(root, "Papers", MeshFactory.RoundedBox(Vector3.one, 0.01f, 2), Whitewash, new Vector3(-0.18f, 0.49f, -0.26f), new Vector3(0.2f, 0.09f, 0.14f), Quaternion.Euler(0f, -8f, 0f));
            Part(root, "Papers", MeshFactory.RoundedBox(Vector3.one, 0.01f, 2), Whitewash, new Vector3(0.14f, 0.47f, -0.28f), new Vector3(0.2f, 0.05f, 0.14f), Quaternion.Euler(0f, 10f, 0f));
            return root;
        }

        /// <summary>A shelter with a bench and a glass back at +Z, and a minibus pulled up in front of it along the edge.</summary>
        public static GameObject BusStop(string name, Transform parent)
        {
            GameObject root = Root(name, parent);
            foreach (float x in new[] { -0.3f, 0.3f })
            {
                Part(root, "Post", MeshFactory.Cylinder(8), Slate, new Vector3(x, 0f, 0.26f), new Vector3(0.025f, 0.7f, 0.025f));
            }

            Part(root, "Back", MeshFactory.RoundedBox(Vector3.one, 0.01f, 2), Glass, new Vector3(0f, 0.4f, 0.4f), new Vector3(0.72f, 0.56f, 0.03f));
            Part(root, "Roof", MeshFactory.RoundedBox(Vector3.one, 0.02f, 2), Sea, new Vector3(0f, 0.72f, 0.24f), new Vector3(0.78f, 0.05f, 0.4f));
            Part(root, "Bench", MeshFactory.RoundedBox(Vector3.one, 0.01f, 2), Timber, new Vector3(0f, 0.26f, 0.32f), new Vector3(0.6f, 0.05f, 0.18f));
            foreach (float x in new[] { -0.22f, 0.22f })
            {
                Part(root, "Bench leg", MeshFactory.Cylinder(8), Slate, new Vector3(x, 0f, 0.32f), new Vector3(0.02f, 0.26f, 0.02f));
            }

            // The minibus from the bus-station sign, parked along the edge in front of the shelter.
            Part(root, "Minibus", SquareIcons.Bus(), Orange, new Vector3(0f, 0f, -0.22f), Vector3.one * 0.5f, Quaternion.Euler(0f, 90f, 0f));
            return root;
        }

        /// <summary>A samovar: a round-bellied urn on a foot with a chimney, base at y = 0, one unit tall.</summary>
        public static Mesh Samovar() => MeshFactory.Lathe("corner:samovar", new[]
        {
            new Vector2(0f, 0f), new Vector2(0.3f, 0f), new Vector2(0.3f, 0.06f), new Vector2(0.14f, 0.1f), new Vector2(0.32f, 0.22f),
            new Vector2(0.36f, 0.45f), new Vector2(0.3f, 0.66f), new Vector2(0.16f, 0.72f), new Vector2(0.1f, 0.74f), new Vector2(0.1f, 1f), new Vector2(0f, 1f),
        }, 16);

        private static GameObject Root(string name, Transform parent)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            return root;
        }

        private static void Part(GameObject root, string name, Mesh mesh, Color color, Vector3 position, Vector3 scale, Quaternion? rotation = null)
        {
            GameObject part = ModelParts.Create(name, root.transform, mesh, color, 0.2f);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            if (rotation.HasValue) part.transform.localRotation = rotation.Value;
        }
    }
}
