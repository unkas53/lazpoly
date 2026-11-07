using Lazpoly.Core.Board;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The little scenes that stand on the corner squares and say what the corner is, in the town's own words: a barred
    /// cell at the jail (a tea garden on boards that still have a tea break), a newspaper kiosk on a card corner, the
    /// champions' cup on its plinth and a minibus stop at the bus station. The start has none: an arrow is printed on it (Faz 24). Each is a parent
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
        public static readonly Color Copper = new Color(0.70f, 0.40f, 0.26f);
        public static readonly Color Sea = new Color(0.30f, 0.55f, 0.80f);
        public static readonly Color Leaf = new Color(0.24f, 0.48f, 0.36f);
        public static readonly Color Slate = new Color(0.36f, 0.40f, 0.46f);
        public static readonly Color Glass = new Color(0.72f, 0.84f, 0.92f);
        public static readonly Color Orange = new Color(0.93f, 0.55f, 0.18f);
        public static readonly Color Tea = new Color(0.72f, 0.30f, 0.16f);
        public static readonly Color Stone = new Color(0.68f, 0.66f, 0.62f);
        public static readonly Color Iron = new Color(0.22f, 0.24f, 0.28f);

        /// <summary>The cup's gold: deeper and shinier than <see cref="Brass"/>, so it does not read as the yellow pawn.</summary>
        public static readonly Color Gold = new Color(0.93f, 0.68f, 0.16f);

        /// <summary>Whether a corner of this type carries a scene.</summary>
        public static bool Has(SquareType type) =>
            type == SquareType.Pot || type == SquareType.Jail || type == SquareType.Card || type == SquareType.Travel || type == SquareType.Cup;

        /// <summary>The scene for a corner square's type, or null when the type has none.</summary>
        public static GameObject For(SquareType type, string name, Transform parent)
        {
            switch (type)
            {
                case SquareType.Pot: return TeaGarden(name, parent);
                case SquareType.Jail: return Cell(name, parent);
                case SquareType.Card: return Kiosk(name, parent);
                case SquareType.Cup: return Trophy(name, parent);
                case SquareType.Travel: return Station(name, parent) ?? BusStop(name, parent);
                default: return null;
            }
        }

        /// <summary>
        /// The owner's bus station diorama (a bus at a stop, made with Meshy AI; <see cref="FigureModels.StationId"/>):
        /// one unit wide, its base at y = 0, its front (the bus) at +Z. Null when its files are not in the build,
        /// and the drawn <see cref="BusStop"/> stands in.
        /// </summary>
        public static GameObject Station(string name, Transform parent) =>
            FigureModels.IsAvailable(FigureModels.StationId) ? FigureModels.Create(FigureModels.StationId, name, parent) : null;

        /// <summary>Whether a corner scene is a textured model that should show its front to the camera, not face out over the edge.</summary>
        public static bool ShowsFront(GameObject scene) => scene != null && scene.transform.Find(FigureModels.BodyPart) != null;

        /// <summary>A round table under a red parasol with three stools, a samovar and a glass of tea on it.</summary>
        public static GameObject TeaGarden(string name, Transform parent)
        {
            GameObject root = Root(name, parent);
            // The parasol stands across the screen from the table, to its right (-X, -Z here: the scene's -X is the
            // side away from the pawns and its -Z the side towards the lawn), and the table a little the other way.
            // A piece's height falls up the screen, so the canopy shows up beyond the table towards the lawn, not
            // over it, and never over the pawns on the corner's other side.
            var parasol = new Vector3(-0.22f, 0f, -0.22f);
            Part(root, "Pole", MeshFactory.Cylinder(8), Slate, parasol, new Vector3(0.02f, 0.68f, 0.02f));
            Part(root, "Parasol", MeshFactory.Cone(16), Red, parasol + Vector3.up * 0.52f, new Vector3(0.26f, 0.18f, 0.26f));
            var table = new Vector3(0.1f, 0f, 0.1f);
            Part(root, "Leg", MeshFactory.Cylinder(8), Slate, table, new Vector3(0.03f, 0.32f, 0.03f));
            Part(root, "Table", MeshFactory.Disc(0.12f, 24), Whitewash, table + Vector3.up * 0.3f, new Vector3(0.24f, 0.25f, 0.24f));
            for (int i = 0; i < 3; i++)
            {
                float angle = (i * 120f + 60f) * Mathf.Deg2Rad;
                Part(root, "Stool", MeshFactory.Cylinder(10), Timber, table + new Vector3(Mathf.Cos(angle) * 0.28f, 0f, Mathf.Sin(angle) * 0.28f), new Vector3(0.075f, 0.2f, 0.075f));
            }

            // Copper, not brass: brass reads as the yellow pawn from across the board.
            Part(root, "Samovar", Samovar(), Copper, table + new Vector3(-0.06f, 0.33f, -0.02f), Vector3.one * 0.3f);
            Part(root, "Tea glass", PawnShapes.TeaGlass(), Tea, table + new Vector3(0.1f, 0.33f, 0.07f), Vector3.one * 0.22f);
            return root;
        }

        /// <summary>
        /// A small stone cell: a solid wall at -Z (towards the lawn) under a slate roof, and iron bars on its other
        /// three sides with a brass lock on the front, so the bars face the camera from the near corner.
        /// </summary>
        public static GameObject Cell(string name, Transform parent)
        {
            GameObject root = Root(name, parent);
            const float halfWidth = 0.36f, front = 0.27f, back = -0.27f, barHeight = 0.6f, floor = 0.06f;
            Part(root, "Floor", MeshFactory.RoundedBox(Vector3.one, 0.02f, 2), Stone, new Vector3(0f, floor / 2f, 0f), new Vector3(0.8f, floor, 0.66f));
            Part(root, "Wall", MeshFactory.RoundedBox(Vector3.one, 0.02f, 2), Stone, new Vector3(0f, 0.36f, back), new Vector3(0.8f, 0.6f, 0.1f));
            Part(root, "Roof", MeshFactory.RoundedBox(Vector3.one, 0.02f, 2), Slate, new Vector3(0f, 0.7f, -0.01f), new Vector3(0.88f, 0.07f, 0.74f));
            foreach (float x in new[] { -halfWidth, halfWidth })
            {
                Part(root, "Pillar", MeshFactory.RoundedBox(Vector3.one, 0.01f, 2), Stone, new Vector3(x, 0.36f, front), new Vector3(0.08f, 0.6f, 0.08f));
                foreach (float z in new[] { -0.12f, 0.01f, 0.14f })
                {
                    Part(root, "Bar", MeshFactory.Cylinder(8), Iron, new Vector3(x, floor, z), new Vector3(0.018f, barHeight, 0.018f));
                }
            }

            for (int i = -3; i <= 3; i++)
            {
                Part(root, "Bar", MeshFactory.Cylinder(8), Iron, new Vector3(i * 0.09f, floor, front), new Vector3(0.018f, barHeight, 0.018f));
            }

            Part(root, "Crossbar", MeshFactory.RoundedBox(Vector3.one, 0.005f, 2), Iron, new Vector3(0f, 0.38f, front), new Vector3(0.7f, 0.035f, 0.035f));
            Part(root, "Lock", MeshFactory.RoundedBox(Vector3.one, 0.01f, 2), Brass, new Vector3(0.1f, 0.33f, front + 0.03f), new Vector3(0.08f, 0.09f, 0.04f));
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

        /// <summary>
        /// The champions' cup: a gold cup with two handles (along x) on a two-step dark plinth, base at y = 0, about one unit tall
        /// and 0.8 wide across its handles. It stands on the cup corner, and small beside the buildings of the town that holds it.
        /// </summary>
        public static GameObject Trophy(string name, Transform parent)
        {
            GameObject root = Root(name, parent);
            Part(root, "Plinth", MeshFactory.RoundedBox(Vector3.one, 0.02f, 2), DarkTimber, new Vector3(0f, 0.08f, 0f), new Vector3(0.5f, 0.16f, 0.5f));
            Part(root, "Step", MeshFactory.RoundedBox(Vector3.one, 0.02f, 2), DarkTimber, new Vector3(0f, 0.2f, 0f), new Vector3(0.38f, 0.08f, 0.38f));
            Part(root, "Plate", MeshFactory.RoundedBox(Vector3.one, 0.005f, 2), Gold, new Vector3(0f, 0.08f, -0.252f), new Vector3(0.3f, 0.07f, 0.01f), smoothness: 0.6f);
            Part(root, "Cup", Cup(), Gold, new Vector3(0f, 0.24f, 0f), Vector3.one, smoothness: 0.6f);
            foreach (float side in new[] { -1f, 1f })
            {
                Part(root, "Handle", MeshFactory.Ring(0.7f, 1f, 24), Gold, new Vector3(side * 0.27f, 0.78f, 0.02f), new Vector3(0.13f, 0.04f, 0.13f),
                    Quaternion.Euler(90f, 0f, 0f), 0.6f);
            }

            return root;
        }

        /// <summary>The cup itself, turned on a lathe: a round foot, a stem with a knot and a deep bowl with a rim, base at y = 0, 0.72 tall.</summary>
        public static Mesh Cup() => MeshFactory.Lathe("corner:cup", new[]
        {
            new Vector2(0f, 0f), new Vector2(0.16f, 0f), new Vector2(0.16f, 0.03f), new Vector2(0.05f, 0.08f), new Vector2(0.04f, 0.2f),
            new Vector2(0.08f, 0.23f), new Vector2(0.04f, 0.27f), new Vector2(0.08f, 0.32f), new Vector2(0.21f, 0.42f), new Vector2(0.27f, 0.56f),
            new Vector2(0.28f, 0.72f), new Vector2(0.24f, 0.72f), new Vector2(0f, 0.64f),
        }, 24);

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

            // The minibus from the bus-station sign, parked along the edge in front of the shelter, with a window strip so it reads as a bus.
            Part(root, "Minibus", SquareIcons.Bus(), Orange, new Vector3(0f, 0f, -0.22f), new Vector3(0.5f, 0.5f, 0.78f), Quaternion.Euler(0f, 90f, 0f));
            Part(root, "Windows", MeshFactory.RoundedBox(Vector3.one, 0.01f, 2), Slate, new Vector3(0f, 0.25f, -0.22f), new Vector3(0.62f, 0.07f, 0.32f));
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

        private static void Part(GameObject root, string name, Mesh mesh, Color color, Vector3 position, Vector3 scale, Quaternion? rotation = null,
            float smoothness = 0.2f)
        {
            GameObject part = ModelParts.Create(name, root.transform, mesh, color, smoothness);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            if (rotation.HasValue) part.transform.localRotation = rotation.Value;
        }
    }
}
