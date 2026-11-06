using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The beach that stands on a holiday resort its owner holds, in four stages that follow how many
    /// resorts the owner has (the rent grows the same way): one sun lounger under a parasol; two loungers
    /// with a towel and a beach ball; three loungers under two parasols beside a palm; and the full resort
    /// with a pool and a beach bar on a timber deck. The cushions, towels, parasols, ball and the bar's roof
    /// are the owner's (<see cref="PaintOwner"/>); the timber, sea water and palms keep their own colours.
    /// Built from the game's own meshes like the corner scenes: base at y = 0, front (towards the camera)
    /// at +Z, inside <see cref="Width"/> by <see cref="Depth"/> so the board can scale it like a building.
    /// </summary>
    public static class ResortParts
    {
        /// <summary>The stages, one per resort held.</summary>
        public const int Stages = 4;

        /// <summary>The footprint every stage keeps to: along the edge (x) and across it (z).</summary>
        public const float Width = 1.24f;
        public const float Depth = 0.72f;

        /// <summary>The tallest piece, a palm, from the base.</summary>
        public const float Height = 0.66f;

        /// <summary>The parts painted in the owner's colour are named this.</summary>
        public const string OwnerPart = "Owner";

        /// <summary>The owner's parts before anyone owns the resort.</summary>
        public static readonly Color Unowned = new Color(0.92f, 0.92f, 0.92f);

        public static readonly Color Timber = BuildingParts.Timber;
        public static readonly Color DarkTimber = BuildingParts.DarkTimber;
        public static readonly Color White = new Color(0.97f, 0.96f, 0.93f);
        public static readonly Color Pole = new Color(0.90f, 0.88f, 0.84f);
        public static readonly Color Water = new Color(0.36f, 0.76f, 0.86f);
        public static readonly Color Frond = new Color(0.28f, 0.60f, 0.30f);
        public static readonly Color Bark = new Color(0.62f, 0.46f, 0.30f);

        /// <summary>The stage for an owner holding <paramref name="held"/> resorts (1 to <see cref="Stages"/>; outside that it is clamped).</summary>
        public static GameObject Stage(int held, string name, Transform parent)
        {
            int stage = Mathf.Clamp(held, 1, Stages);
            GameObject root = Root(name, parent);
            switch (stage)
            {
                case 1:
                    Lounger(root, new Vector3(0.08f, 0f, 0.04f));
                    Parasol(root, new Vector3(-0.14f, 0f, -0.08f));
                    break;
                case 2:
                    Lounger(root, new Vector3(-0.12f, 0f, 0.04f));
                    Lounger(root, new Vector3(0.16f, 0f, 0.04f));
                    Parasol(root, new Vector3(0.02f, 0f, -0.1f));
                    SideTable(root, new Vector3(0.02f, 0f, 0.15f));
                    Towel(root, new Vector3(-0.42f, 0f, 0.06f), 14f);
                    Ball(root, new Vector3(0.4f, 0f, 0.18f));
                    break;
                case 3:
                    for (int i = 0; i < 3; i++) Lounger(root, new Vector3(-0.32f + i * 0.26f, 0f, 0.06f));
                    Parasol(root, new Vector3(-0.19f, 0f, -0.1f));
                    Parasol(root, new Vector3(0.07f, 0f, -0.1f));
                    Palm(root, new Vector3(0.44f, 0f, -0.1f), 170f);
                    Ball(root, new Vector3(0.42f, 0f, 0.2f));
                    Towel(root, new Vector3(-0.5f, 0f, 0.1f), -10f);
                    break;
                default:
                    Part(root, "Deck", MeshFactory.RoundedBox(new Vector3(1.04f, 0.024f, 0.36f), 0.01f, 2), Timber, new Vector3(-0.04f, 0.012f, -0.17f), Vector3.one);
                    Pool(root, new Vector3(-0.22f, 0.024f, -0.17f));
                    Bar(root, new Vector3(0.32f, 0.024f, -0.17f));
                    for (int i = 0; i < 3; i++) Lounger(root, new Vector3(-0.38f + i * 0.25f, 0f, 0.18f));
                    Parasol(root, new Vector3(-0.255f, 0f, 0.12f));
                    Parasol(root, new Vector3(-0.005f, 0f, 0.12f));
                    Palm(root, new Vector3(0.44f, 0f, 0.14f), 160f);
                    Palm(root, new Vector3(-0.48f, 0f, -0.06f), -20f);
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

        /// <summary>A sun lounger along z: a timber frame on four legs, the backrest raised at the back (-Z), cushions in the owner's colour.</summary>
        private static void Lounger(GameObject root, Vector3 at)
        {
            GameObject lounger = Group(root, "Lounger", at, 0f);
            foreach (float x in new[] { -0.05f, 0.05f })
            {
                foreach (float z in new[] { -0.1f, 0.12f })
                {
                    Part(lounger, "Leg", MeshFactory.Cylinder(6), DarkTimber, new Vector3(x, 0f, z), new Vector3(0.008f, 0.06f, 0.008f));
                }
            }

            Part(lounger, "Frame", MeshFactory.RoundedBox(new Vector3(0.12f, 0.018f, 0.24f), 0.006f, 2), Timber, new Vector3(0f, 0.066f, 0.04f), Vector3.one);
            Part(lounger, OwnerPart, MeshFactory.RoundedBox(new Vector3(0.105f, 0.02f, 0.22f), 0.009f, 2), Unowned, new Vector3(0f, 0.084f, 0.045f), Vector3.one);
            // The backrest leans back over the frame's far end.
            GameObject back = Group(lounger, "Backrest", new Vector3(0f, 0.075f, -0.07f), 0f);
            back.transform.localRotation = Quaternion.Euler(48f, 0f, 0f);
            Part(back, "Frame", MeshFactory.RoundedBox(new Vector3(0.12f, 0.018f, 0.12f), 0.006f, 2), Timber, new Vector3(0f, 0f, -0.06f), Vector3.one);
            Part(back, OwnerPart, MeshFactory.RoundedBox(new Vector3(0.105f, 0.02f, 0.11f), 0.009f, 2), Unowned, new Vector3(0f, 0.016f, -0.058f), Vector3.one);
        }

        /// <summary>A beach parasol: a pale pole with a domed canopy in the owner's colour, a white rim and a white tip.</summary>
        private static void Parasol(GameObject root, Vector3 at)
        {
            GameObject parasol = Group(root, "Parasol", at, 0f);
            Part(parasol, "Pole", MeshFactory.Cylinder(8), Pole, Vector3.zero, new Vector3(0.009f, 0.42f, 0.009f));
            Part(parasol, OwnerPart, Canopy(), Unowned, new Vector3(0f, 0.34f, 0f), new Vector3(0.17f, 0.12f, 0.17f));
            Part(parasol, "Rim", MeshFactory.Ring(0.93f, 0.08f, 24), White, new Vector3(0f, 0.336f, 0f), new Vector3(0.172f, 0.12f, 0.172f));
            Part(parasol, "Tip", MeshFactory.Sphere(8), White, new Vector3(0f, 0.42f, 0f), Vector3.one * 0.014f);
        }

        /// <summary>A round white table on one leg with a glass on it.</summary>
        private static void SideTable(GameObject root, Vector3 at)
        {
            Part(root, "Table leg", MeshFactory.Cylinder(8), Pole, at, new Vector3(0.008f, 0.07f, 0.008f));
            Part(root, "Table", MeshFactory.Disc(0.12f, 20), White, at + Vector3.up * 0.068f, new Vector3(0.045f, 0.06f, 0.045f));
            Part(root, "Glass", MeshFactory.Cylinder(10), Water, at + new Vector3(0.012f, 0.076f, 0f), new Vector3(0.01f, 0.026f, 0.01f));
        }

        /// <summary>A beach towel laid on the sand in the owner's colour with a white stripe across it.</summary>
        private static void Towel(GameObject root, Vector3 at, float yaw)
        {
            GameObject towel = Group(root, "Towel", at, yaw);
            Part(towel, OwnerPart, MeshFactory.RoundedBox(new Vector3(0.12f, 0.008f, 0.24f), 0.003f, 1), Unowned, new Vector3(0f, 0.004f, 0f), Vector3.one);
            Part(towel, "Stripe", MeshFactory.RoundedBox(new Vector3(0.122f, 0.009f, 0.03f), 0.003f, 1), White, new Vector3(0f, 0.0045f, 0.06f), Vector3.one);
        }

        /// <summary>A beach ball in the owner's colour with a white band.</summary>
        private static void Ball(GameObject root, Vector3 at)
        {
            Part(root, OwnerPart, MeshFactory.Sphere(10), Unowned, at + Vector3.up * 0.035f, Vector3.one * 0.035f);
            Part(root, "Band", MeshFactory.Cylinder(16), White, at + Vector3.up * 0.026f, new Vector3(0.0365f, 0.018f, 0.0365f));
        }

        /// <summary>A palm: a trunk that bends towards its local +X as it rises (the yaw turns it in over the beach), a crown of drooping fronds and three coconuts.</summary>
        private static void Palm(GameObject root, Vector3 at, float yaw)
        {
            GameObject palm = Group(root, "Palm", at, yaw);
            const int pieces = 5;
            const float piece = 0.11f;
            // The first ring leans already; lifted by its lean over its radius, its low edge stays on the sand.
            Vector3 top = Vector3.up * 0.002f;
            for (int i = 0; i < pieces; i++)
            {
                // Each ring of the trunk leans a little more towards +X, narrowing as it goes.
                float lean = 4f + i * 4f;
                Quaternion tilt = Quaternion.Euler(0f, 0f, -lean);
                float radius = 0.024f - i * 0.0025f;
                Part(palm, "Trunk", MeshFactory.Cylinder(8), i % 2 == 0 ? Bark : DarkTimber, top, new Vector3(radius, piece, radius), tilt);
                top += tilt * Vector3.up * piece;
            }

            const int fronds = 7;
            for (int i = 0; i < fronds; i++)
            {
                GameObject frond = Group(palm, "Frond", top, i * 360f / fronds + 10f);
                // A long flat leaf that leaves the crown and droops towards its tip.
                Part(frond, "Leaf", MeshFactory.RoundedBox(new Vector3(0.05f, 0.01f, 0.12f), 0.004f, 1), Frond, new Vector3(0f, 0.012f, 0.055f), Vector3.one, Quaternion.Euler(-12f, 0f, 0f));
                Part(frond, "Leaf", MeshFactory.RoundedBox(new Vector3(0.04f, 0.01f, 0.1f), 0.004f, 1), Frond, new Vector3(0f, -0.012f, 0.15f), Vector3.one, Quaternion.Euler(22f, 0f, 0f));
            }

            for (int i = 0; i < 3; i++)
            {
                float angle = (i * 120f + 30f) * Mathf.Deg2Rad;
                Part(palm, "Coconut", MeshFactory.Sphere(8), DarkTimber, top + new Vector3(Mathf.Cos(angle) * 0.022f, -0.022f, Mathf.Sin(angle) * 0.022f), Vector3.one * 0.016f);
            }
        }

        /// <summary>A pool with a white edge round clear water.</summary>
        private static void Pool(GameObject root, Vector3 at)
        {
            const float width = 0.46f;
            const float depth = 0.26f;
            const float edge = 0.03f;
            const float height = 0.03f;
            Part(root, "Water", MeshFactory.RoundedBox(new Vector3(width - edge, height * 0.7f, depth - edge), 0.004f, 1), Water, at + Vector3.up * (height * 0.35f), Vector3.one);
            foreach (float z in new[] { -1f, 1f })
            {
                Part(root, "Pool edge", MeshFactory.RoundedBox(new Vector3(width, height, edge), 0.006f, 1), White, at + new Vector3(0f, height / 2f, z * (depth - edge) / 2f), Vector3.one);
            }

            foreach (float x in new[] { -1f, 1f })
            {
                Part(root, "Pool edge", MeshFactory.RoundedBox(new Vector3(edge, height, depth), 0.006f, 1), White, at + new Vector3(x * (width - edge) / 2f, height / 2f, 0f), Vector3.one);
            }
        }

        /// <summary>A beach bar: a timber counter under a roof in the owner's colour on four posts, with two stools in front.</summary>
        private static void Bar(GameObject root, Vector3 at)
        {
            GameObject bar = Group(root, "Bar", at, 0f);
            foreach (float x in new[] { -0.1f, 0.1f })
            {
                foreach (float z in new[] { -0.1f, 0.1f })
                {
                    Part(bar, "Post", MeshFactory.Cylinder(6), DarkTimber, new Vector3(x, 0f, z), new Vector3(0.01f, 0.24f, 0.01f));
                }
            }

            Part(bar, "Counter", MeshFactory.RoundedBox(new Vector3(0.22f, 0.1f, 0.06f), 0.008f, 2), Timber, new Vector3(0f, 0.05f, 0.08f), Vector3.one);
            Part(bar, "Counter top", MeshFactory.RoundedBox(new Vector3(0.24f, 0.012f, 0.075f), 0.004f, 1), White, new Vector3(0f, 0.105f, 0.08f), Vector3.one);
            Part(bar, OwnerPart, MeshFactory.Cone(4), Unowned, new Vector3(0f, 0.235f, 0f), new Vector3(0.2f, 0.11f, 0.2f), Quaternion.Euler(0f, 45f, 0f));
            foreach (float x in new[] { -0.05f, 0.05f })
            {
                Part(bar, "Stool", MeshFactory.Cylinder(10), Timber, new Vector3(x, 0f, 0.15f), new Vector3(0.016f, 0.065f, 0.016f));
            }
        }

        /// <summary>A parasol's canopy: a shallow dome over a slightly hollow underside, radius and height 1.</summary>
        public static Mesh Canopy() => MeshFactory.Lathe("resort:canopy", new List<Vector2>
        {
            new Vector2(0f, 0.18f), new Vector2(1f, 0f), new Vector2(0.82f, 0.42f), new Vector2(0.5f, 0.78f), new Vector2(0f, 1f),
        }, 20);

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
