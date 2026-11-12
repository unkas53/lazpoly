using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The little aeroplane that crosses the sky behind the table now and then (the owner's wish, 2026-11-12): a plain
    /// single-engined machine of a few rounded shapes, pale like everything far off in the haze, with soft red wing tips,
    /// fin and nose, a dark canopy and a turning propeller. It is <see cref="Length"/> long from tail to nose along its
    /// +z, its wings across x, and is sized and flown by the sky backdrop. Nothing about it is loud: no trail, no sound.
    /// </summary>
    public static class PlaneParts
    {
        /// <summary>The aeroplane from tail to spinner, and its wings from tip to tip.</summary>
        public const float Length = 1f;
        public const float Span = 0.96f;

        /// <summary>How many turns the propeller makes a second: slow enough to be seen turning rather than flickering.</summary>
        public const float PropellerTurns = 2.5f;

        public const string BodyName = "Body";
        public const string TrimName = "Trim";
        public const string GlassName = "Canopy";
        public const string PropellerName = "Propeller";

        /// <summary>Its colours before the haze takes them: cream paint, a soft red, the canopy's glass, the propeller.</summary>
        public static readonly Color Paint = new Color(0.96f, 0.94f, 0.88f);
        public static readonly Color Trim = new Color(0.80f, 0.34f, 0.28f);
        public static readonly Color Glass = new Color(0.24f, 0.33f, 0.42f);
        public static readonly Color Blade = new Color(0.30f, 0.29f, 0.28f);

        /// <summary>Builds the aeroplane under <paramref name="parent"/>, its middle at the origin, casting and taking no shadows.</summary>
        public static GameObject Create(Transform parent, string name)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            ModelParts.Create(BodyName, root.transform, BodyMesh(), SkySkin.Hazed(Paint), 0.2f);
            ModelParts.Create(TrimName, root.transform, TrimMesh(), SkySkin.Hazed(Trim), 0.2f);
            ModelParts.Create(GlassName, root.transform, GlassMesh(), SkySkin.Hazed(Glass), 0.5f);
            GameObject propeller = ModelParts.Create(PropellerName, root.transform, PropellerMesh(), SkySkin.Hazed(Blade), 0.2f);
            propeller.transform.localPosition = new Vector3(0f, 0f, Length / 2f - 0.035f);
            foreach (MeshRenderer part in root.GetComponentsInChildren<MeshRenderer>())
            {
                part.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                part.receiveShadows = false;
            }

            return root;
        }

        /// <summary>How far the propeller has turned at a time, in degrees.</summary>
        public static float PropellerAt(float seconds) => Mathf.Repeat(seconds * PropellerTurns, 1f) * 360f;

        /// <summary>Turns the propeller of an aeroplane made by <see cref="Create"/> to where it is at a time.</summary>
        public static void Spin(Transform plane, float seconds)
        {
            Transform propeller = plane != null ? plane.Find(PropellerName) : null;
            if (propeller != null) propeller.localRotation = Quaternion.Euler(0f, 0f, PropellerAt(seconds));
        }

        // A lathe turns its profile round y; laid over, that axis becomes the aeroplane's length (+z).
        private static readonly Quaternion Along = Quaternion.Euler(90f, 0f, 0f);

        /// <summary>The cream parts: the fuselage from its tail's point to the cowl, the wing, and the tailplane.</summary>
        private static Mesh BodyMesh()
        {
            Mesh fuselage = MeshFactory.Lathe("plane-fuselage", new[]
            {
                new Vector2(0f, -0.5f), new Vector2(0.03f, -0.46f), new Vector2(0.055f, -0.28f), new Vector2(0.08f, -0.05f), new Vector2(0.088f, 0.15f),
                new Vector2(0.084f, 0.34f), new Vector2(0f, 0.34f),
            }, 12);
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>
            {
                new KeyValuePair<Mesh, Matrix4x4>(fuselage, Matrix4x4.Rotate(Along)),
            };
            Box(parts, new Vector3(Span - 0.2f, 0.02f, 0.19f), new Vector3(0f, -0.03f, 0.1f), 0.009f);
            Box(parts, new Vector3(0.36f, 0.016f, 0.11f), new Vector3(0f, 0.02f, -0.41f), 0.007f);
            return MeshFactory.Combine("plane-body", parts);
        }

        /// <summary>The red parts: the cowl and the spinner at the nose, the wing's tips, and the fin.</summary>
        private static Mesh TrimMesh()
        {
            Mesh nose = MeshFactory.Lathe("plane-nose", new[]
            {
                new Vector2(0f, 0.34f), new Vector2(0.086f, 0.34f), new Vector2(0.08f, 0.43f), new Vector2(0.05f, 0.455f), new Vector2(0.03f, 0.47f), new Vector2(0f, 0.5f),
            }, 12);
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>
            {
                new KeyValuePair<Mesh, Matrix4x4>(nose, Matrix4x4.Rotate(Along)),
            };
            foreach (float side in new[] { -1f, 1f })
                Box(parts, new Vector3(0.11f, 0.022f, 0.192f), new Vector3(side * (Span - 0.11f) / 2f, -0.03f, 0.1f), 0.01f);
            Box(parts, new Vector3(0.016f, 0.15f, 0.13f), new Vector3(0f, 0.09f, -0.42f), 0.007f);
            return MeshFactory.Combine("plane-trim", parts);
        }

        /// <summary>The canopy: a long glass bubble over the wing.</summary>
        private static Mesh GlassMesh()
        {
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>
            {
                new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Sphere(8), Matrix4x4.TRS(new Vector3(0f, 0.07f, 0.08f), Quaternion.identity, new Vector3(0.052f, 0.05f, 0.13f))),
            };
            return MeshFactory.Combine("plane-canopy", parts);
        }

        /// <summary>The propeller about its own middle: two blades across, turning round z.</summary>
        private static Mesh PropellerMesh()
        {
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>();
            Box(parts, new Vector3(0.3f, 0.034f, 0.008f), Vector3.zero, 0.004f);
            return MeshFactory.Combine("plane-propeller", parts);
        }

        private static void Box(List<KeyValuePair<Mesh, Matrix4x4>> parts, Vector3 size, Vector3 at, float radius) =>
            parts.Add(new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.RoundedBox(size, Mathf.Min(radius, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) / 2f), 2), Matrix4x4.Translate(at)));
    }
}
