using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The aeroplane that crosses the sky behind the table now and then (the owner's wishes: a plane in the background,
    /// 2026-11-12; a jet, 2026-11-13): a small twin-engined airliner of a few plain shapes, pale like everything far off
    /// in the haze: a slim body with a rounded nose, swept wings with a jet engine hung under each, a swept fin and
    /// tailplane. Its fin, its wing tips and its engines' cowls are a soft red; the cockpit's glass, the line of
    /// windows and the engines' mouths are dark. It is <see cref="Length"/> long from tail to nose along its +z, its
    /// wings across x, and is sized and flown by the sky backdrop, which also draws the trail it leaves
    /// (<see cref="Exhaust"/>). Nothing about it is loud: no sound.
    /// </summary>
    public static class PlaneParts
    {
        /// <summary>The aeroplane from tail to nose, and its wings from tip to tip.</summary>
        public const float Length = 1f;
        public const float Span = 0.97f;

        /// <summary>Where each engine hangs: this far out along the wing, this far under the body's middle line, its own middle this far ahead of the plane's.</summary>
        public const float EngineOut = 0.175f;
        public const float EngineDrop = 0.07f;
        public const float EngineAhead = 0.09f;

        /// <summary>An engine's cowl at its widest, and from its mouth to its nozzle.</summary>
        public const float EngineRadius = 0.041f;
        public const float EngineLength = 0.18f;

        /// <summary>Where the engines' jets leave the plane, midway between the two: the head of the trail.</summary>
        public static readonly Vector3 Exhaust = new Vector3(0f, -EngineDrop, EngineAhead - 0.135f);

        public const string BodyName = "Body";
        public const string TrimName = "Trim";
        public const string GlassName = "Glass";

        /// <summary>Its colours before the haze takes them: cream paint, a soft red, and the dark of its glass and its engines' mouths.</summary>
        public static readonly Color Paint = new Color(0.96f, 0.94f, 0.88f);
        public static readonly Color Trim = new Color(0.80f, 0.34f, 0.28f);
        public static readonly Color Glass = new Color(0.24f, 0.33f, 0.42f);

        // The body's radius, half the wings' reach before they rise, how far they rise towards their tips (degrees), and
        // how far the wing tips' fins lean outwards.
        private const float BodyRadius = 0.056f;
        private const float WingReach = 0.47f;
        private const float WingRise = 5f;
        private const float TipLean = 15f;
        private const float WingHeight = -0.022f;

        /// <summary>Builds the aeroplane under <paramref name="parent"/>, its middle at the origin, casting and taking no shadows.</summary>
        public static GameObject Create(Transform parent, string name)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            ModelParts.Create(BodyName, root.transform, BodyMesh(), SkySkin.Hazed(Paint), 0.2f);
            ModelParts.Create(TrimName, root.transform, TrimMesh(), SkySkin.Hazed(Trim), 0.2f);
            ModelParts.Create(GlassName, root.transform, GlassMesh(), SkySkin.Hazed(Glass), 0.5f);
            foreach (MeshRenderer part in root.GetComponentsInChildren<MeshRenderer>())
            {
                part.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                part.receiveShadows = false;
            }

            return root;
        }

        // A lathe turns its profile round y; laid over, that axis becomes the aeroplane's length (+z).
        private static readonly Quaternion Along = Quaternion.Euler(90f, 0f, 0f);

        // A plate lies flat; stood up, its x becomes the aeroplane's height and its z stays its length.
        private static Quaternion Upright(float lean) => Quaternion.Euler(0f, 0f, 90f - lean);

        /// <summary>The cream parts: the body from its tail's point to its nose, the swept wings, the tailplane, and the engines' pylons.</summary>
        private static Mesh BodyMesh()
        {
            Mesh body = MeshFactory.Lathe("jet-body", new[]
            {
                new Vector2(0f, -0.5f), new Vector2(0.016f, -0.495f), new Vector2(0.034f, -0.36f), new Vector2(0.05f, -0.2f), new Vector2(BodyRadius, -0.05f),
                new Vector2(BodyRadius, 0.33f), new Vector2(0.05f, 0.41f), new Vector2(0.036f, 0.465f), new Vector2(0.018f, 0.492f), new Vector2(0f, 0.5f),
            }, 16);
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>
            {
                new KeyValuePair<Mesh, Matrix4x4>(body, Matrix4x4.Rotate(Along)),
            };
            foreach (float side in new[] { -1f, 1f })
            {
                // A wing: broad at the body, swept back and narrowing to its tip, rising a little on the way.
                Mesh wing = MeshFactory.Plate(side < 0f ? "jet-wing-left" : "jet-wing-right", new[]
                {
                    new Vector2(side * 0.03f, 0.17f), new Vector2(side * 0.03f, -0.07f), new Vector2(side * WingReach, -0.215f), new Vector2(side * WingReach, -0.125f),
                }, 0.016f);
                parts.Add(new KeyValuePair<Mesh, Matrix4x4>(wing, Matrix4x4.TRS(new Vector3(0f, WingHeight, 0f), Quaternion.Euler(0f, 0f, side * WingRise), Vector3.one)));
                Mesh tail = MeshFactory.Plate(side < 0f ? "jet-tail-left" : "jet-tail-right", new[]
                {
                    new Vector2(side * 0.015f, -0.35f), new Vector2(side * 0.015f, -0.455f), new Vector2(side * 0.19f, -0.5f), new Vector2(side * 0.19f, -0.455f),
                }, 0.012f);
                parts.Add(new KeyValuePair<Mesh, Matrix4x4>(tail, Matrix4x4.Translate(new Vector3(0f, 0.012f, 0f))));
                Box(parts, new Vector3(0.012f, 0.04f, 0.11f), new Vector3(side * EngineOut, -0.028f, EngineAhead - 0.03f), 0.004f);
            }

            return MeshFactory.Combine("jet-body", parts);
        }

        /// <summary>The red parts: the fin at the tail, the little fins at the wings' tips, and the engines' cowls.</summary>
        private static Mesh TrimMesh()
        {
            // The fin, drawn lying down: (height, length).
            Mesh fin = MeshFactory.Plate("jet-fin", new[]
            {
                new Vector2(0.03f, -0.29f), new Vector2(0.03f, -0.465f), new Vector2(0.215f, -0.5f), new Vector2(0.215f, -0.43f),
            }, 0.014f);
            Mesh tip = MeshFactory.Plate("jet-wing-tip", new[]
            {
                new Vector2(0f, -0.125f), new Vector2(0f, -0.215f), new Vector2(0.06f, -0.235f), new Vector2(0.06f, -0.2f),
            }, 0.012f);
            // A cowl from its nozzle to the lip of its mouth, closed at both ends.
            Mesh cowl = MeshFactory.Lathe("jet-cowl", new[]
            {
                new Vector2(0f, -0.095f), new Vector2(0.024f, -0.095f), new Vector2(0.036f, -0.06f), new Vector2(EngineRadius, 0.02f), new Vector2(0.039f, 0.07f),
                new Vector2(0.032f, EngineLength - 0.095f), new Vector2(0f, EngineLength - 0.095f),
            }, 14);
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>
            {
                new KeyValuePair<Mesh, Matrix4x4>(fin, Matrix4x4.Rotate(Upright(0f))),
            };
            float rise = WingRise * Mathf.Deg2Rad;
            foreach (float side in new[] { -1f, 1f })
            {
                var wingTip = new Vector3(side * (WingReach * Mathf.Cos(rise) - 0.004f), WingHeight + WingReach * Mathf.Sin(rise), 0f);
                parts.Add(new KeyValuePair<Mesh, Matrix4x4>(tip, Matrix4x4.TRS(wingTip, Upright(side * TipLean), Vector3.one)));
                parts.Add(new KeyValuePair<Mesh, Matrix4x4>(cowl, Matrix4x4.TRS(new Vector3(side * EngineOut, -EngineDrop, EngineAhead), Along, Vector3.one)));
            }

            return MeshFactory.Combine("jet-trim", parts);
        }

        /// <summary>The dark parts: the cockpit's glass, a line of windows along each side, and each engine's mouth and nozzle.</summary>
        private static Mesh GlassMesh()
        {
            Mesh mouth = MeshFactory.Lathe("jet-mouth", new[]
            {
                new Vector2(0f, EngineLength - 0.097f), new Vector2(0.027f, EngineLength - 0.097f), new Vector2(0.027f, EngineLength - 0.092f), new Vector2(0f, EngineLength - 0.092f),
            }, 14);
            Mesh nozzle = MeshFactory.Lathe("jet-nozzle", new[]
            {
                new Vector2(0f, -0.135f), new Vector2(0.02f, -0.096f), new Vector2(0f, -0.096f),
            }, 10);
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>
            {
                new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Sphere(8), Matrix4x4.TRS(new Vector3(0f, 0.024f, 0.425f), Quaternion.identity, new Vector3(0.036f, 0.026f, 0.04f))),
            };
            foreach (float side in new[] { -1f, 1f })
            {
                Matrix4x4 engine = Matrix4x4.TRS(new Vector3(side * EngineOut, -EngineDrop, EngineAhead), Along, Vector3.one);
                parts.Add(new KeyValuePair<Mesh, Matrix4x4>(mouth, engine));
                parts.Add(new KeyValuePair<Mesh, Matrix4x4>(nozzle, engine));
                Box(parts, new Vector3(0.005f, 0.013f, 0.5f), new Vector3(side * (BodyRadius - 0.0023f), 0.014f, 0.07f), 0.002f);
            }

            return MeshFactory.Combine("jet-glass", parts);
        }

        private static void Box(List<KeyValuePair<Mesh, Matrix4x4>> parts, Vector3 size, Vector3 at, float radius) =>
            parts.Add(new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.RoundedBox(size, Mathf.Min(radius, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) / 2f), 2), Matrix4x4.Translate(at)));
    }
}
