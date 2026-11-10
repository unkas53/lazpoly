using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The factory that stands on a factory city its owner holds (the squares that were the holiday resorts; the owner's
    /// wish, 2026-11-11), in four stages that follow how many factory cities the owner has (the rent grows the same way):
    /// a workshop with one chimney; a factory hall with a taller chimney and a tank; two halls with two chimneys; and a
    /// big works with an office block and three chimneys. The halls' saw-tooth roofs, the office's roof and a band round
    /// each chimney are the owner's (<see cref="PaintOwner"/>); brick, concrete, glass and smoke keep their own colours.
    /// Built from the game's own meshes like the corner scenes: base at y = 0, front (towards the camera) at +Z, inside
    /// <see cref="Width"/> by <see cref="Depth"/> so the board can scale it like a building. (The class keeps the resorts'
    /// name, as the code keeps "resort" for these squares.)
    /// </summary>
    public static class ResortParts
    {
        /// <summary>The stages, one per factory city held.</summary>
        public const int Stages = 4;

        /// <summary>The footprint every stage keeps to: along the edge (x) and across it (z).</summary>
        public const float Width = 1.24f;
        public const float Depth = 0.72f;

        /// <summary>The tallest thing, the top of a chimney's smoke, from the base.</summary>
        public const float Height = 0.8f;

        /// <summary>The parts painted in the owner's colour are named this.</summary>
        public const string OwnerPart = "Owner";

        /// <summary>The owner's parts before anyone owns the city.</summary>
        public static readonly Color Unowned = new Color(0.92f, 0.92f, 0.92f);

        public static readonly Color Brick = new Color(0.64f, 0.35f, 0.27f);
        public static readonly Color DarkBrick = new Color(0.42f, 0.24f, 0.2f);
        public static readonly Color Concrete = new Color(0.74f, 0.73f, 0.7f);
        public static readonly Color Steel = new Color(0.6f, 0.63f, 0.67f);
        public static readonly Color Glass = new Color(0.62f, 0.8f, 0.86f);
        public static readonly Color Door = new Color(0.27f, 0.28f, 0.3f);
        public static readonly Color Smoke = new Color(0.86f, 0.86f, 0.84f);
        public static readonly Color Crate = BuildingParts.Timber;

        /// <summary>
        /// The stage for an owner holding <paramref name="held"/> factory cities (1 to <see cref="Stages"/>; outside that it
        /// is clamped), ready for the board: the pieces of <see cref="Layout"/> merged into one mesh per colour, the owner's
        /// parts into one named <see cref="OwnerPart"/>, so a works draws in a handful of calls.
        /// </summary>
        public static GameObject Stage(int held, string name, Transform parent)
        {
            int stage = Mathf.Clamp(held, 1, Stages);
            return Merge(Layout(stage, name, null), $"factory:{stage}", name, parent);
        }

        // The pieces of loose merged into one mesh per colour, the owner's parts into one named OwnerPart; loose is destroyed.
        private static GameObject Merge(GameObject loose, string id, string name, Transform parent)
        {
            GameObject root = Root(name, parent);
            var order = new List<KeyValuePair<bool, Color>>();
            var groups = new Dictionary<KeyValuePair<bool, Color>, List<KeyValuePair<Mesh, Matrix4x4>>>();
            Matrix4x4 toRoot = loose.transform.worldToLocalMatrix;
            foreach (MeshRenderer part in loose.GetComponentsInChildren<MeshRenderer>(true))
            {
                bool owner = part.name == OwnerPart;
                Color color = part.sharedMaterial != null ? part.sharedMaterial.color : Color.white;
                var key = new KeyValuePair<bool, Color>(owner, owner ? Unowned : color);
                if (!groups.TryGetValue(key, out List<KeyValuePair<Mesh, Matrix4x4>> meshes))
                {
                    meshes = new List<KeyValuePair<Mesh, Matrix4x4>>();
                    groups[key] = meshes;
                    order.Add(key);
                }

                meshes.Add(new KeyValuePair<Mesh, Matrix4x4>(part.GetComponent<MeshFilter>().sharedMesh, toRoot * part.transform.localToWorldMatrix));
            }

            foreach (KeyValuePair<bool, Color> key in order)
            {
                string colour = key.Key ? "owner" : ColorUtility.ToHtmlStringRGB(key.Value);
                Mesh merged = MeshFactory.Combine($"{id}:{colour}", groups[key]);
                ModelParts.Create(key.Key ? OwnerPart : "Works", root.transform, merged, key.Value, key.Key ? 0.35f : 0.2f);
            }

            Object.DestroyImmediate(loose);
            return root;
        }

        /// <summary>
        /// The stage's pieces one by one (halls, chimneys, the office...), each its own object under the root: what
        /// <see cref="Stage"/> merges, and what the tests count.
        /// </summary>
        public static GameObject Layout(int held, string name, Transform parent)
        {
            int stage = Mathf.Clamp(held, 1, Stages);
            GameObject root = Root(name, parent);
            switch (stage)
            {
                case 1:
                    // A workshop: a small hall with two roof teeth, one chimney, crates by the door.
                    Hall(root, new Vector3(-0.06f, 0f, -0.04f), 0.5f, 0.34f, 0.15f, 2);
                    Chimney(root, new Vector3(0.33f, 0f, -0.12f), 0.32f, 0.035f);
                    Crates(root, new Vector3(0.32f, 0f, 0.14f));
                    break;
                case 2:
                    // A factory: a bigger hall, a taller chimney and a tank.
                    Hall(root, new Vector3(-0.14f, 0f, -0.05f), 0.64f, 0.38f, 0.17f, 3);
                    Chimney(root, new Vector3(0.36f, 0f, -0.16f), 0.42f, 0.042f);
                    Tank(root, new Vector3(0.37f, 0f, 0.12f), 0.07f, 0.14f);
                    Crates(root, new Vector3(-0.36f, 0f, 0.24f));
                    break;
                case 3:
                    // Two halls and two chimneys.
                    Hall(root, new Vector3(-0.24f, 0f, -0.07f), 0.56f, 0.38f, 0.19f, 3);
                    Hall(root, new Vector3(0.36f, 0f, 0.04f), 0.3f, 0.28f, 0.15f, 2);
                    Chimney(root, new Vector3(0.13f, 0f, -0.2f), 0.46f, 0.042f);
                    Chimney(root, new Vector3(0.42f, 0f, -0.24f), 0.4f, 0.04f);
                    Crates(root, new Vector3(-0.1f, 0f, 0.24f));
                    break;
                default:
                    // The big works: a long hall, an office block and three chimneys.
                    Hall(root, new Vector3(-0.22f, 0f, -0.08f), 0.7f, 0.42f, 0.21f, 4);
                    Office(root, new Vector3(0.36f, 0f, 0.11f), 0.3f, 0.22f, 0.24f);
                    Chimney(root, new Vector3(0.2f, 0f, -0.22f), 0.52f, 0.042f);
                    Chimney(root, new Vector3(0.34f, 0f, -0.25f), 0.58f, 0.045f);
                    Chimney(root, new Vector3(0.48f, 0f, -0.21f), 0.48f, 0.04f);
                    break;
            }

            return root;
        }

        /// <summary>Paints a stage's owner parts: <paramref name="owner"/>'s colour, or <see cref="Unowned"/> for null.</summary>
        public static void PaintOwner(GameObject stage, Color? owner)
        {
            Color color = owner ?? Unowned;
            foreach (Renderer part in stage.GetComponentsInChildren<Renderer>(true))
            {
                if (part.name == OwnerPart) ModelParts.Paint(part.gameObject, color, 0.35f);
            }
        }

        /// <summary>
        /// A factory hall, its front to +Z: brick walls on a concrete plinth, a band of windows and a loading door in front,
        /// and a saw-tooth roof of <paramref name="teeth"/> pitched rows running front to back, in the owner's colour.
        /// </summary>
        private static void Hall(GameObject root, Vector3 at, float width, float depth, float wall, int teeth)
        {
            GameObject hall = Group(root, "Hall", at, 0f);
            const float plinth = 0.02f;
            Part(hall, "Plinth", MeshFactory.RoundedBox(new Vector3(width + 0.03f, plinth, depth + 0.03f), 0.004f, 1), Concrete,
                new Vector3(0f, plinth / 2f, 0f), Vector3.one);
            Part(hall, "Walls", MeshFactory.RoundedBox(new Vector3(width, wall, depth), 0.005f, 2), Brick, new Vector3(0f, plinth + wall / 2f, 0f), Vector3.one);
            float front = depth / 2f + 0.003f;
            Part(hall, "Windows", MeshFactory.RoundedBox(new Vector3(width * 0.78f, 0.035f, 0.008f), 0.003f, 1), Glass,
                new Vector3(0f, plinth + wall * 0.7f, front), Vector3.one);
            Part(hall, "Door", MeshFactory.RoundedBox(new Vector3(Mathf.Min(0.1f, width * 0.25f), wall * 0.52f, 0.01f), 0.003f, 1), Door,
                new Vector3(-width * 0.22f, plinth + wall * 0.26f, front), Vector3.one);

            // Each tooth a prism with its ridge front to back (the prism's ridge runs along X, so it is turned a quarter).
            float tooth = width / teeth;
            for (int i = 0; i < teeth; i++)
            {
                float x = -width / 2f + (i + 0.5f) * tooth;
                Part(hall, OwnerPart, MeshFactory.Prism(), Unowned, new Vector3(x, plinth + wall, 0f),
                    new Vector3(depth + 0.02f, 0.08f, tooth), Quaternion.Euler(0f, 90f, 0f));
            }
        }

        /// <summary>A brick chimney with a band in the owner's colour, a dark rim and three puffs of smoke drifting off its top.</summary>
        private static void Chimney(GameObject root, Vector3 at, float height, float radius)
        {
            GameObject chimney = Group(root, "Chimney", at, 0f);
            Part(chimney, "Stack", MeshFactory.Cylinder(10), Brick, Vector3.zero, new Vector3(radius, height, radius));
            Part(chimney, OwnerPart, MeshFactory.Cylinder(10), Unowned, new Vector3(0f, height * 0.76f, 0f), new Vector3(radius * 1.08f, 0.035f, radius * 1.08f));
            Part(chimney, "Rim", MeshFactory.Cylinder(10), DarkBrick, new Vector3(0f, height - 0.025f, 0f), new Vector3(radius * 1.18f, 0.025f, radius * 1.18f));
            Part(chimney, "Smoke", MeshFactory.Sphere(10), Smoke, new Vector3(0f, height + 0.035f, 0f), Vector3.one * 0.03f);
            Part(chimney, "Smoke", MeshFactory.Sphere(10), Smoke, new Vector3(0.012f, height + 0.085f, -0.012f), Vector3.one * 0.04f);
            Part(chimney, "Smoke", MeshFactory.Sphere(10), Smoke, new Vector3(0.03f, height + 0.14f, -0.028f), Vector3.one * 0.048f);
        }

        /// <summary>A steel tank with a domed top.</summary>
        private static void Tank(GameObject root, Vector3 at, float radius, float height)
        {
            GameObject tank = Group(root, "Tank", at, 0f);
            Part(tank, "Shell", MeshFactory.Cylinder(16), Steel, Vector3.zero, new Vector3(radius, height, radius));
            Part(tank, "Dome", MeshFactory.Sphere(12), Steel, new Vector3(0f, height, 0f), new Vector3(radius, radius * 0.45f, radius));
        }

        /// <summary>An office block: concrete, two rows of windows and a door in front, a flat roof in the owner's colour.</summary>
        private static void Office(GameObject root, Vector3 at, float width, float depth, float height)
        {
            GameObject office = Group(root, "Office", at, 0f);
            Part(office, "Block", MeshFactory.RoundedBox(new Vector3(width, height, depth), 0.005f, 2), Concrete, new Vector3(0f, height / 2f, 0f), Vector3.one);
            Part(office, OwnerPart, MeshFactory.RoundedBox(new Vector3(width + 0.02f, 0.02f, depth + 0.02f), 0.004f, 1), Unowned,
                new Vector3(0f, height + 0.01f, 0f), Vector3.one);
            float front = depth / 2f + 0.003f;
            foreach (float y in new[] { height * 0.5f, height * 0.78f })
            {
                foreach (float x in new[] { -width * 0.3f, 0f, width * 0.3f })
                {
                    Part(office, "Window", MeshFactory.RoundedBox(new Vector3(0.05f, 0.035f, 0.008f), 0.003f, 1), Glass, new Vector3(x, y, front), Vector3.one);
                }
            }

            Part(office, "Door", MeshFactory.RoundedBox(new Vector3(0.05f, 0.075f, 0.01f), 0.003f, 1), Door, new Vector3(0f, 0.0375f, front), Vector3.one);
        }

        /// <summary>Two timber crates, one turned a little.</summary>
        private static void Crates(GameObject root, Vector3 at)
        {
            Part(root, "Crate", MeshFactory.RoundedBox(Vector3.one * 0.06f, 0.004f, 1), Crate, at + new Vector3(0f, 0.03f, 0f), Vector3.one);
            Part(root, "Crate", MeshFactory.RoundedBox(Vector3.one * 0.05f, 0.004f, 1), Crate, at + new Vector3(0.065f, 0.025f, 0.01f), Vector3.one,
                Quaternion.Euler(0f, 20f, 0f));
        }

        private static GameObject Root(string name, Transform parent)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            return root;
        }

        private static GameObject Group(GameObject parent, string name, Vector3 position, float yaw)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent.transform, false);
            group.transform.localPosition = position;
            group.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return group;
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
