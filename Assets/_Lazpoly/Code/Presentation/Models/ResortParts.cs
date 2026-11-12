using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The works that stand on a factory city its owner holds (the squares that were the holiday resorts; the owner's
    /// wish, 2026-11-11), in four stages that follow how many factory cities the owner has (the rent grows the same way):
    /// a workshop with one chimney; a factory with a taller chimney and a water tower; two halls with two chimneys and
    /// the tower; and a great works with an office block, three chimneys and two silos. Redrawn on 2026-11-12 (the
    /// owner's wish: a better model that suits the table and pleases the eye) as an old brick mill in the table's own
    /// materials: brick halls on a limestone course with pilasters between tall windows and a limestone cornice, and
    /// on them a true saw-tooth roof, each tooth an upright row of glass and one slope; tapering chimneys on stone
    /// feet with a corbelled crown; a steel tank on iron legs. The roofs' slopes, the office's roof and a band round
    /// each chimney are the owner's (<see cref="PaintOwner"/>); brick, stone, glass, iron and smoke keep their own.
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

        /// <summary>
        /// Few colours, the table's own: a warm brick and a darker one for pilasters, the jail's limestone for courses,
        /// sills and feet, its iron for doors, legs and hoops, a quiet glass, the tank's steel.
        /// </summary>
        public static readonly Color Brick = new Color(0.66f, 0.40f, 0.31f);
        public static readonly Color DarkBrick = new Color(0.50f, 0.29f, 0.23f);
        public static readonly Color Stone = CornerParts.Limestone;
        public static readonly Color Iron = CornerParts.Iron;
        public static readonly Color Steel = new Color(0.58f, 0.61f, 0.65f);
        public static readonly Color Glass = new Color(0.50f, 0.68f, 0.76f);
        public static readonly Color Smoke = new Color(0.90f, 0.89f, 0.86f);
        public static readonly Color Crate = BuildingParts.Timber;

        /// <summary>The stone course a hall or the office stands on, and the cornice that closes its walls.</summary>
        public const float Course = 0.024f;
        public const float Cornice = 0.014f;

        /// <summary>How high a roof tooth stands over the cornice.</summary>
        public const float Tooth = 0.085f;

        /// <summary>A chimney's stone foot, and how far its smoke reaches over its top.</summary>
        public const float ChimneyFoot = 0.06f;
        public const float SmokeReach = 0.175f;

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
                    Hall(root, new Vector3(-0.10f, 0f, -0.03f), 0.56f, 0.38f, 0.17f, 2, Tooth);
                    Chimney(root, new Vector3(0.36f, 0f, -0.14f), 0.40f, 0.04f);
                    Crates(root, new Vector3(0.30f, 0f, 0.17f));
                    break;
                case 2:
                    // A factory: a bigger hall, a taller chimney and a water tower.
                    Hall(root, new Vector3(-0.16f, 0f, -0.04f), 0.72f, 0.42f, 0.19f, 3, Tooth);
                    Chimney(root, new Vector3(0.34f, 0f, -0.19f), 0.50f, 0.043f);
                    Tower(root, new Vector3(0.44f, 0f, 0.12f), 0.075f, 0.12f, 0.16f);
                    Crates(root, new Vector3(0.26f, 0f, 0.25f));
                    break;
                case 3:
                    // Two halls, two chimneys and the tower behind them.
                    Hall(root, new Vector3(-0.24f, 0f, -0.06f), 0.66f, 0.44f, 0.20f, 3, Tooth);
                    Hall(root, new Vector3(0.36f, 0f, 0.10f), 0.34f, 0.30f, 0.15f, 2, 0.07f);
                    Chimney(root, new Vector3(0.20f, 0f, -0.24f), 0.56f, 0.044f);
                    Chimney(root, new Vector3(0.40f, 0f, -0.24f), 0.47f, 0.04f);
                    Tower(root, new Vector3(0.54f, 0f, -0.20f), 0.062f, 0.11f, 0.2f);
                    break;
                default:
                    // The great works: a long hall, an office block, three chimneys and two silos.
                    Hall(root, new Vector3(-0.21f, 0f, -0.07f), 0.78f, 0.46f, 0.22f, 4, Tooth);
                    Office(root, new Vector3(0.40f, 0f, 0.14f), 0.30f, 0.24f, 0.27f);
                    Chimney(root, new Vector3(0.24f, 0f, -0.25f), 0.54f, 0.044f);
                    Chimney(root, new Vector3(0.37f, 0f, -0.27f), 0.62f, 0.047f);
                    Chimney(root, new Vector3(0.50f, 0f, -0.24f), 0.48f, 0.04f);
                    Silo(root, new Vector3(-0.56f, 0f, 0.27f), 0.05f, 0.15f);
                    Silo(root, new Vector3(-0.455f, 0f, 0.27f), 0.05f, 0.15f);
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
        /// A mill hall, its front to +Z: brick walls on a stone course, closed by a stone cornice; pilasters divide the
        /// front into bays, each with a tall window over a stone sill but one, which holds the iron door under a stone
        /// lintel; windows down both sides. On it a saw-tooth roof of <paramref name="teeth"/> teeth, their ridges running
        /// front to back so the front shows the saw's outline: each a brick wedge <paramref name="tooth"/> high with a row of
        /// glass in its upright face and its slope covered in the owner's colour.
        /// </summary>
        private static void Hall(GameObject root, Vector3 at, float width, float depth, float wall, int teeth, float tooth)
        {
            GameObject hall = Group(root, "Hall", at, 0f);
            Part(hall, "Course", Box(width + 0.03f, Course, depth + 0.03f), Stone, new Vector3(0f, Course / 2f, 0f), Vector3.one);
            Part(hall, "Walls", MeshFactory.RoundedBox(new Vector3(width, wall, depth), 0.004f, 1), Brick, new Vector3(0f, Course + wall / 2f, 0f), Vector3.one);
            float top = Course + wall, front = depth / 2f + 0.003f, middle = Course + wall / 2f;
            Part(hall, "Cornice", Box(width + 0.018f, Cornice, depth + 0.018f), Stone, new Vector3(0f, top + Cornice / 2f, 0f), Vector3.one);

            int bays = Mathf.Max(3, Mathf.RoundToInt(width / 0.115f));
            float bay = width / bays;
            for (int i = 0; i <= bays; i++)
            {
                Part(hall, "Pilaster", Box(0.02f, wall, 0.008f), DarkBrick, new Vector3(-width / 2f + i * bay, middle, front), Vector3.one);
            }

            for (int i = 0; i < bays; i++)
            {
                float x = -width / 2f + (i + 0.5f) * bay;
                if (i == 1)
                {
                    Part(hall, "Door", Box(bay * 0.62f, wall * 0.62f, 0.008f), Iron, new Vector3(x, Course + wall * 0.31f, front), Vector3.one);
                    Part(hall, "Lintel", Box(bay * 0.72f, 0.012f, 0.01f), Stone, new Vector3(x, Course + wall * 0.64f, front + 0.001f), Vector3.one);
                }
                else
                {
                    Part(hall, "Window", Box(bay * 0.46f, wall * 0.46f, 0.007f), Glass, new Vector3(x, Course + wall * 0.58f, front), Vector3.one);
                    Part(hall, "Sill", Box(bay * 0.56f, 0.01f, 0.012f), Stone, new Vector3(x, Course + wall * 0.33f, front + 0.001f), Vector3.one);
                }
            }

            int sideWindows = Mathf.Max(2, Mathf.RoundToInt(depth / 0.13f));
            float sideBay = depth / sideWindows;
            foreach (float side in new[] { -1f, 1f })
            {
                for (int i = 0; i < sideWindows; i++)
                {
                    Part(hall, "Window", Box(0.007f, wall * 0.46f, sideBay * 0.46f), Glass,
                        new Vector3(side * (width / 2f + 0.003f), Course + wall * 0.58f, -depth / 2f + (i + 0.5f) * sideBay), Vector3.one);
                }
            }

            // The teeth: the upright face of each at its left, the slope falling to the right.
            float span = width / teeth, foot = top + Cornice;
            float slope = Mathf.Sqrt(span * span + tooth * tooth), pitch = -Mathf.Atan2(tooth, span) * Mathf.Rad2Deg;
            for (int i = 0; i < teeth; i++)
            {
                float left = -width / 2f + i * span;
                Part(hall, "Tooth", MeshFactory.Wedge(), Brick, new Vector3(left + span / 2f, foot, 0f), new Vector3(span, tooth, depth));
                Part(hall, "Roof light", Box(0.006f, tooth * 0.6f, depth * 0.84f), Glass, new Vector3(left - 0.002f, foot + tooth * 0.5f, 0f), Vector3.one);
                Part(hall, OwnerPart, Box(slope + 0.012f, 0.012f, depth + 0.03f), Unowned,
                    new Vector3(left + span / 2f + 0.004f, foot + tooth / 2f + 0.006f, 0f), Vector3.one, Quaternion.Euler(0f, 0f, pitch));
            }
        }

        /// <summary>
        /// A brick chimney <paramref name="height"/> high on a square stone foot: it tapers upwards to a corbelled crown
        /// with a dark mouth, wears a band in the owner's colour below the crown, and three puffs of smoke drift off its top.
        /// </summary>
        private static void Chimney(GameObject root, Vector3 at, float height, float radius)
        {
            GameObject chimney = Group(root, "Chimney", at, 0f);
            Part(chimney, "Foot", Box(radius * 2.9f, ChimneyFoot, radius * 2.9f), Stone, new Vector3(0f, ChimneyFoot / 2f, 0f), Vector3.one);
            float below = radius * 1.18f, above = radius * 0.8f, stack = height - ChimneyFoot, neck = stack - 0.05f;
            Mesh shaft = MeshFactory.Lathe($"factory-chimney:{height:F3}:{radius:F3}", Sharp(
                new Vector2(0f, 0f), new Vector2(below, 0f), new Vector2(above, neck), new Vector2(above * 1.22f, neck + 0.01f),
                new Vector2(above * 1.22f, neck + 0.038f), new Vector2(above * 1.05f, stack), new Vector2(0f, stack)), 12);
            Part(chimney, "Stack", shaft, Brick, new Vector3(0f, ChimneyFoot, 0f), Vector3.one);
            float bandAt = height * 0.7f, bandRadius = Mathf.Lerp(below, above, (bandAt - ChimneyFoot) / neck) * 1.07f;
            Part(chimney, OwnerPart, MeshFactory.Cylinder(12), Unowned, new Vector3(0f, bandAt, 0f), new Vector3(bandRadius, 0.04f, bandRadius));
            Part(chimney, "Mouth", MeshFactory.Cylinder(12), Iron, new Vector3(0f, height - 0.014f, 0f), new Vector3(above * 0.7f, 0.015f, above * 0.7f));
            Part(chimney, "Smoke", MeshFactory.Sphere(8), Smoke, new Vector3(0f, height + 0.03f, 0f), Vector3.one * 0.03f);
            Part(chimney, "Smoke", MeshFactory.Sphere(8), Smoke, new Vector3(0.022f, height + 0.075f, -0.012f), Vector3.one * 0.04f);
            Part(chimney, "Smoke", MeshFactory.Sphere(8), Smoke, new Vector3(0.055f, height + SmokeReach - 0.05f, -0.03f), Vector3.one * 0.05f);
        }

        /// <summary>A water tower: a steel tank with an iron hoop and a low iron cone for a roof, on four braced iron legs.</summary>
        private static void Tower(GameObject root, Vector3 at, float radius, float height, float legs)
        {
            GameObject tower = Group(root, "Tower", at, 0f);
            foreach (float x in new[] { -1f, 1f })
            {
                foreach (float z in new[] { -1f, 1f })
                {
                    Part(tower, "Leg", Box(0.012f, legs, 0.012f), Iron, new Vector3(x * radius * 0.62f, legs / 2f, z * radius * 0.62f), Vector3.one);
                }
            }

            Part(tower, "Brace", Box(radius * 1.24f + 0.012f, 0.008f, 0.008f), Iron, new Vector3(0f, legs * 0.5f, 0f), Vector3.one);
            Part(tower, "Brace", Box(0.008f, 0.008f, radius * 1.24f + 0.012f), Iron, new Vector3(0f, legs * 0.5f, 0f), Vector3.one);
            Part(tower, "Tank", MeshFactory.Cylinder(14), Steel, new Vector3(0f, legs, 0f), new Vector3(radius, height, radius));
            Part(tower, "Hoop", MeshFactory.Cylinder(14), Iron, new Vector3(0f, legs + height * 0.5f, 0f), new Vector3(radius * 1.04f, 0.012f, radius * 1.04f));
            Part(tower, "Cap", MeshFactory.Cone(14), Iron, new Vector3(0f, legs + height, 0f), new Vector3(radius * 1.12f, radius * 0.7f, radius * 1.12f));
        }

        /// <summary>A steel silo with a domed top and an iron hoop.</summary>
        private static void Silo(GameObject root, Vector3 at, float radius, float height)
        {
            GameObject silo = Group(root, "Silo", at, 0f);
            Part(silo, "Shell", MeshFactory.Cylinder(14), Steel, Vector3.zero, new Vector3(radius, height, radius));
            Part(silo, "Dome", MeshFactory.Sphere(8), Steel, new Vector3(0f, height, 0f), new Vector3(radius, radius * 0.42f, radius));
            Part(silo, "Hoop", MeshFactory.Cylinder(14), Iron, new Vector3(0f, height * 0.45f, 0f), new Vector3(radius * 1.04f, 0.01f, radius * 1.04f));
        }

        /// <summary>
        /// The office block: brick on a stone course between stone corner piers, a stone band under each of its two rows of
        /// windows, an iron door in front, windows on both sides, a stone cornice and a flat roof in the owner's colour.
        /// </summary>
        private static void Office(GameObject root, Vector3 at, float width, float depth, float height)
        {
            GameObject office = Group(root, "Office", at, 0f);
            Part(office, "Course", Box(width + 0.024f, Course, depth + 0.024f), Stone, new Vector3(0f, Course / 2f, 0f), Vector3.one);
            Part(office, "Block", MeshFactory.RoundedBox(new Vector3(width, height, depth), 0.004f, 1), Brick, new Vector3(0f, Course + height / 2f, 0f), Vector3.one);
            float top = Course + height, front = depth / 2f + 0.003f;
            Part(office, "Cornice", Box(width + 0.02f, 0.016f, depth + 0.02f), Stone, new Vector3(0f, top + 0.008f, 0f), Vector3.one);
            Part(office, OwnerPart, Box(width - 0.01f, 0.02f, depth - 0.01f), Unowned, new Vector3(0f, top + 0.026f, 0f), Vector3.one);
            foreach (float side in new[] { -1f, 1f })
            {
                Part(office, "Pier", Box(0.024f, height, 0.008f), Stone, new Vector3(side * (width / 2f - 0.012f), Course + height / 2f, front), Vector3.one);
            }

            foreach (float floor in new[] { 0.36f, 0.74f })
            {
                Part(office, "Band", Box(width * 0.8f, 0.008f, 0.01f), Stone, new Vector3(0f, Course + height * (floor - 0.105f), front + 0.001f), Vector3.one);
                foreach (float across in new[] { -0.27f, 0f, 0.27f })
                {
                    // The door takes the ground floor's middle window.
                    if (floor < 0.5f && across == 0f) continue;
                    Part(office, "Window", Box(width * 0.16f, height * 0.19f, 0.007f), Glass, new Vector3(across * width, Course + height * floor, front), Vector3.one);
                }

                foreach (float side in new[] { -1f, 1f })
                {
                    foreach (float along in new[] { -0.22f, 0.22f })
                    {
                        Part(office, "Window", Box(0.007f, height * 0.19f, depth * 0.2f), Glass,
                            new Vector3(side * (width / 2f + 0.003f), Course + height * floor, along * depth), Vector3.one);
                    }
                }
            }

            Part(office, "Door", Box(width * 0.17f, height * 0.36f, 0.009f), Iron, new Vector3(0f, Course + height * 0.18f, front + 0.001f), Vector3.one);
        }

        /// <summary>Three timber crates, two on the ground and one on them, each turned a little.</summary>
        private static void Crates(GameObject root, Vector3 at)
        {
            Part(root, "Crate", Box(0.06f, 0.06f, 0.06f), Crate, at + new Vector3(0f, 0.03f, 0f), Vector3.one);
            Part(root, "Crate", Box(0.05f, 0.05f, 0.05f), Crate, at + new Vector3(0.066f, 0.025f, 0.01f), Vector3.one, Quaternion.Euler(0f, 20f, 0f));
            Part(root, "Crate", Box(0.045f, 0.045f, 0.045f), Crate, at + new Vector3(0.012f, 0.0825f, -0.004f), Vector3.one, Quaternion.Euler(0f, -12f, 0f));
        }

        /// <summary>A small crisp box: the trim's pieces are too fine for a rounder edge to show.</summary>
        private static Mesh Box(float x, float y, float z) => MeshFactory.RoundedBox(new Vector3(x, y, z), 0.002f, 1);

        /// <summary>A lathe's profile with every point doubled, so each ring between two points keeps its own flat shading.</summary>
        private static List<Vector2> Sharp(params Vector2[] points)
        {
            var profile = new List<Vector2>(points.Length * 2);
            foreach (Vector2 point in points)
            {
                profile.Add(point);
                profile.Add(point);
            }

            return profile;
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
