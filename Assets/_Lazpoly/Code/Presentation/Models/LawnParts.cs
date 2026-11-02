using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// What dresses the lawn in the middle of the board: trees and bushes of the coast, posts with
    /// bunting strung between them, and the glass jar the tea money collects in with its coins. Each
    /// piece is a parent object with its base at y = 0, built from the game's own meshes at a size that
    /// suits the board (a tree stands about as tall as a pawn and a half).
    /// </summary>
    public static class LawnParts
    {
        public static readonly Color Trunk = BuildingParts.DarkTimber;
        public static readonly Color Leaf = new Color(0.26f, 0.52f, 0.30f);
        public static readonly Color LightLeaf = new Color(0.40f, 0.64f, 0.34f);
        public static readonly Color Pine = new Color(0.20f, 0.42f, 0.30f);
        public static readonly Color Post = new Color(0.93f, 0.90f, 0.82f);
        public static readonly Color Twine = new Color(0.40f, 0.36f, 0.32f);
        public static readonly Color Glass = new Color(0.78f, 0.88f, 0.92f);
        public static readonly Color Lid = new Color(0.72f, 0.30f, 0.22f);
        public static readonly Color Coin = new Color(0.86f, 0.70f, 0.32f);

        /// <summary>The bunting's colours, in the order they repeat along a string.</summary>
        public static readonly Color[] PennantColors =
        {
            new Color(0.80f, 0.26f, 0.22f), new Color(0.96f, 0.95f, 0.90f), new Color(0.30f, 0.55f, 0.80f), new Color(0.93f, 0.55f, 0.18f),
        };

        /// <summary>How many kinds of tree there are.</summary>
        public const int TreeKinds = 3;

        /// <summary>The tallest tree, from its base; the lawn keeps that much room up the screen above every tree.</summary>
        public const float TreeHeight = 0.72f;

        /// <summary>A bunting post's height; the string ties on a little below the top.</summary>
        public const float PostHeight = 0.5f;
        public const float StringHeight = 0.46f;

        /// <summary>How far the string hangs below its ends in the middle.</summary>
        public const float Sag = 0.06f;

        /// <summary>A pennant's width along the string and its drop below it.</summary>
        public const float PennantWidth = 0.13f;
        public const float PennantDrop = 0.17f;

        /// <summary>A coin's radius and thickness, and how many sit in a stack before the next one starts.</summary>
        public const float CoinRadius = 0.085f;
        public const float CoinThickness = 0.024f;
        public const int CoinsPerStack = 6;
        public const int MaxStacks = 5;

        /// <summary>A tree of one of the <see cref="TreeKinds"/>: a round crown, a pine, or a double crown; up to <see cref="TreeHeight"/> tall.</summary>
        public static GameObject Tree(string name, Transform parent, int kind)
        {
            GameObject root = Root(name, parent);
            Part(root, "Trunk", MeshFactory.Cylinder(8), Trunk, Vector3.zero, new Vector3(0.045f, 0.34f, 0.045f));
            switch (((kind % TreeKinds) + TreeKinds) % TreeKinds)
            {
                case 0:
                    Part(root, "Crown", MeshFactory.Sphere(10), Leaf, new Vector3(0f, 0.48f, 0f), new Vector3(0.24f, 0.22f, 0.24f));
                    break;
                case 1:
                    Part(root, "Crown", MeshFactory.Cone(12), Pine, new Vector3(0f, 0.2f, 0f), new Vector3(0.2f, 0.52f, 0.2f));
                    Part(root, "Skirt", MeshFactory.Cone(12), Pine, new Vector3(0f, 0.12f, 0f), new Vector3(0.24f, 0.3f, 0.24f));
                    break;
                default:
                    Part(root, "Crown", MeshFactory.Sphere(10), Leaf, new Vector3(0.05f, 0.44f, 0.03f), new Vector3(0.21f, 0.2f, 0.21f));
                    Part(root, "Top", MeshFactory.Sphere(10), LightLeaf, new Vector3(-0.06f, 0.56f, -0.04f), new Vector3(0.15f, 0.14f, 0.15f));
                    break;
            }

            return root;
        }

        /// <summary>A low rounded bush.</summary>
        public static GameObject Bush(string name, Transform parent)
        {
            GameObject root = Root(name, parent);
            Part(root, "Bush", MeshFactory.Sphere(10), LightLeaf, new Vector3(0f, 0.1f, 0f), new Vector3(0.19f, 0.1f, 0.17f));
            Part(root, "Bush", MeshFactory.Sphere(10), Leaf, new Vector3(0.1f, 0.07f, 0.06f), new Vector3(0.12f, 0.08f, 0.11f));
            return root;
        }

        /// <summary>A bunting post: a pale pole with a small cap, <see cref="PostHeight"/> tall.</summary>
        public static GameObject BuntingPost(string name, Transform parent)
        {
            GameObject root = Root(name, parent);
            Part(root, "Pole", MeshFactory.Cylinder(8), Post, Vector3.zero, new Vector3(0.025f, PostHeight, 0.025f));
            Part(root, "Cap", MeshFactory.Sphere(8), Lid, new Vector3(0f, PostHeight, 0f), Vector3.one * 0.04f);
            return root;
        }

        /// <summary>
        /// A string from <paramref name="from"/> to <paramref name="to"/> (both at string height, in the
        /// parent's space) that sags in the middle, with <paramref name="pennants"/> pennants hung along it
        /// in the bunting colours. The pennants hang in the string's own vertical plane.
        /// </summary>
        public static GameObject Bunting(string name, Transform parent, Vector3 from, Vector3 to, int pennants)
        {
            GameObject root = Root(name, parent);
            Vector3 middle = (from + to) / 2f - Vector3.up * Sag;
            Strand(root, from, middle);
            Strand(root, middle, to);
            Vector3 along = to - from;
            along.y = 0f;
            Quaternion facing = Quaternion.LookRotation(Vector3.Cross(Vector3.up, along.normalized), Vector3.up);
            for (int i = 0; i < pennants; i++)
            {
                float t = (i + 0.5f) / pennants;
                Vector3 point = t < 0.5f ? Vector3.Lerp(from, middle, t * 2f) : Vector3.Lerp(middle, to, (t - 0.5f) * 2f);
                Part(root, "Pennant", MeshFactory.Pennant(), PennantColors[i % PennantColors.Length],
                    point - Vector3.up * PennantDrop, new Vector3(PennantWidth, PennantDrop, 1f), facing);
            }

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

        private static void Strand(GameObject root, Vector3 from, Vector3 to)
        {
            Vector3 run = to - from;
            Part(root, "String", MeshFactory.Cylinder(6), Twine, from, new Vector3(0.008f, run.magnitude, 0.008f), Quaternion.FromToRotation(Vector3.up, run));
        }

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
