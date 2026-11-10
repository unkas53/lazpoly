using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The beach that stands on a holiday resort its owner holds, in four stages that follow how many
    /// resorts the owner has (the rent grows the same way), the owner's wish (2026-11-11): sun loungers under
    /// a parasol; then a small beach hut beside them; two huts; and one big hut with a porch. Every resort, held
    /// or not, has its two palms (<see cref="Palms"/>), which stand at the ends of the beach, clear of the stages.
    /// The cushions, parasols, towel, ball and the huts' roofs are the owner's (<see cref="PaintOwner"/>); the
    /// timber, the windows and the palms keep their own colours.
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
        public static readonly Color Bush = new Color(0.36f, 0.62f, 0.36f);
        public static readonly Color Window = new Color(0.62f, 0.84f, 0.88f);

        /// <summary>Where the palms stand: at both ends of the beach (x), towards the sea (z).</summary>
        public const float PalmSpread = 0.51f;
        public const float PalmBack = -0.14f;

        /// <summary>The stages keep within this half width, between the palms.</summary>
        public const float StageHalfWidth = 0.47f;

        /// <summary>
        /// The stage for an owner holding <paramref name="held"/> resorts (1 to <see cref="Stages"/>; outside that it is clamped),
        /// ready for the board: the pieces of <see cref="Layout"/> merged into one mesh per colour, the owner's parts into one
        /// named <see cref="OwnerPart"/>, so a full beach draws in a handful of calls instead of about ninety.
        /// </summary>
        public static GameObject Stage(int held, string name, Transform parent)
        {
            int stage = Mathf.Clamp(held, 1, Stages);
            return Merge(Layout(stage, name, null), $"resort:{stage}", name, parent);
        }

        /// <summary>
        /// A resort's two palms, with a bush at each foot, merged like a stage: they stand on every resort, held or not,
        /// at the ends of the beach (<see cref="PalmSpread"/>), leaning in over it. Same space as <see cref="Stage"/>.
        /// </summary>
        public static GameObject Palms(string name, Transform parent) => Merge(PalmLayout(name, null), "resort:palms", name, parent);

        /// <summary>The palms' pieces one by one, as <see cref="Layout"/> gives a stage's.</summary>
        public static GameObject PalmLayout(string name, Transform parent)
        {
            GameObject root = Root(name, parent);
            Palm(root, new Vector3(-PalmSpread, 0f, PalmBack), 0f);
            Palm(root, new Vector3(PalmSpread, 0f, PalmBack + 0.04f), 180f);
            // A bush at each palm's foot, on the outer side, clear of the huts.
            Part(root, "Bush", MeshFactory.Sphere(10), Bush, new Vector3(-PalmSpread - 0.045f, 0.035f, PalmBack - 0.06f), new Vector3(0.05f, 0.035f, 0.045f));
            Part(root, "Bush", MeshFactory.Sphere(10), Bush, new Vector3(PalmSpread + 0.045f, 0.03f, PalmBack + 0.08f), new Vector3(0.045f, 0.03f, 0.04f));
            return root;
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
                ModelParts.Create(key.Key ? OwnerPart : "Beach", root.transform, merged, key.Value, key.Key ? 0.35f : 0.2f);
            }

            Object.DestroyImmediate(loose);
            return root;
        }

        /// <summary>
        /// The stage's pieces one by one (loungers, parasols, huts...), each its own object under the root: what
        /// <see cref="Stage"/> merges, and what the tests count.
        /// </summary>
        public static GameObject Layout(int held, string name, Transform parent)
        {
            int stage = Mathf.Clamp(held, 1, Stages);
            GameObject root = Root(name, parent);
            switch (stage)
            {
                case 1:
                    // Sun loungers under a parasol, a little table between them.
                    Lounger(root, new Vector3(-0.13f, 0f, 0.06f));
                    Lounger(root, new Vector3(0.13f, 0f, 0.06f));
                    Parasol(root, new Vector3(0f, 0f, -0.12f));
                    SideTable(root, new Vector3(0f, 0f, 0.16f));
                    break;
                case 2:
                    // A small hut beside the loungers.
                    Hut(root, new Vector3(-0.25f, 0f, -0.1f), false);
                    Lounger(root, new Vector3(0.09f, 0f, 0.08f));
                    Lounger(root, new Vector3(0.31f, 0f, 0.08f));
                    Parasol(root, new Vector3(0.2f, 0f, -0.12f));
                    Towel(root, new Vector3(-0.25f, 0f, 0.24f), 82f);
                    break;
                case 3:
                    // Two huts, the loungers between them.
                    Hut(root, new Vector3(-0.31f, 0f, -0.1f), false);
                    Hut(root, new Vector3(0.31f, 0f, -0.1f), false);
                    Lounger(root, new Vector3(-0.1f, 0f, 0.1f));
                    Lounger(root, new Vector3(0.1f, 0f, 0.1f));
                    Parasol(root, new Vector3(0f, 0f, -0.14f));
                    Towel(root, new Vector3(-0.31f, 0f, 0.24f), 82f);
                    Ball(root, new Vector3(0.33f, 0f, 0.22f));
                    break;
                default:
                    // One big hut with its porch, the loungers beside it.
                    Hut(root, new Vector3(-0.17f, 0f, -0.1f), true);
                    Lounger(root, new Vector3(0.22f, 0f, 0.1f));
                    Lounger(root, new Vector3(0.4f, 0f, 0.1f));
                    Parasol(root, new Vector3(0.29f, 0f, -0.14f));
                    SideTable(root, new Vector3(0.31f, 0f, 0.27f));
                    Towel(root, new Vector3(-0.3f, 0f, 0.27f), 82f);
                    Ball(root, new Vector3(-0.04f, 0f, 0.27f));
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

        /// <summary>
        /// A beach hut, its door to the front (+Z): timber walls on a low deck, a dark door, light windows and a hipped roof
        /// in the owner's colour. The big one is wider, with a window either side of the door and a porch under the roof
        /// on two posts, with a step down to the sand.
        /// </summary>
        private static void Hut(GameObject root, Vector3 at, bool big)
        {
            GameObject hut = Group(root, big ? "Big hut" : "Hut", at, 0f);
            float width = big ? 0.42f : 0.22f, depth = big ? 0.2f : 0.17f, wall = big ? 0.17f : 0.14f;
            float porch = big ? 0.1f : 0f;
            float deck = 0.022f;
            // The deck runs out in front of the walls for the porch.
            Part(hut, "Deck", MeshFactory.RoundedBox(new Vector3(width + 0.04f, deck, depth + 0.04f + porch), 0.004f, 1), DarkTimber,
                new Vector3(0f, deck / 2f, porch / 2f), Vector3.one);
            Part(hut, "Walls", MeshFactory.RoundedBox(new Vector3(width, wall, depth), 0.006f, 2), Timber, new Vector3(0f, deck + wall / 2f, 0f), Vector3.one);
            float front = depth / 2f + 0.002f;
            Part(hut, "Door", MeshFactory.RoundedBox(new Vector3(0.05f, wall * 0.68f, 0.008f), 0.003f, 1), DarkTimber,
                new Vector3(big ? 0f : -0.045f, deck + wall * 0.34f, front), Vector3.one);
            foreach (float x in big ? new[] { -0.13f, 0.13f } : new[] { 0.05f })
            {
                Part(hut, "Window", MeshFactory.RoundedBox(new Vector3(0.055f, 0.045f, 0.008f), 0.003f, 1), Window,
                    new Vector3(x, deck + wall * 0.58f, front), Vector3.one);
            }

            if (big)
            {
                foreach (float x in new[] { -width / 2f + 0.015f, width / 2f - 0.015f })
                {
                    Part(hut, "Post", MeshFactory.Cylinder(6), DarkTimber, new Vector3(x, deck, depth / 2f + porch - 0.015f), new Vector3(0.009f, wall, 0.009f));
                }

                Part(hut, "Step", MeshFactory.RoundedBox(new Vector3(0.1f, deck * 0.6f, 0.04f), 0.003f, 1), Timber,
                    new Vector3(0f, deck * 0.3f, depth / 2f + porch + 0.04f), Vector3.one);
            }

            // A hipped roof: a four-sided cone turned to lie square, stretched over the walls (and the porch) with an overhang.
            float roofDepth = depth + porch + 0.06f;
            GameObject roof = Group(hut, "Roof", new Vector3(0f, deck + wall, porch / 2f), 0f);
            roof.transform.localScale = new Vector3((width + 0.06f) * Mathf.Sqrt(2f) / 2f, big ? 0.15f : 0.12f, roofDepth * Mathf.Sqrt(2f) / 2f);
            Part(roof, OwnerPart, MeshFactory.Cone(4), Unowned, Vector3.zero, Vector3.one, Quaternion.Euler(0f, 45f, 0f));
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
