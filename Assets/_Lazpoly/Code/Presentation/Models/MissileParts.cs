using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The missile of the owner's card (2026-11-21: "bir şehre füze at"): a toy rocket in the game's own manner, a few
    /// plain round shapes: an ivory body, a rounded nose, a band and four swept fins in its player's colour, a dark
    /// nozzle, and a short flame of two tones behind it that shines by itself. It is <see cref="Length"/> long from
    /// its nozzle's end to its nose's tip along its +y, small enough to come up through a money hatch
    /// (<see cref="Span"/> across its fins), and casts a shadow as it flies. It is flown by <c>MissileStrike</c>.
    /// </summary>
    public static class MissileParts
    {
        /// <summary>The missile from tail to nose, its body's radius and its fins from tip to tip, in its own lengths.</summary>
        public const float Length = 1f;
        public const float Radius = 0.135f;
        public const float Span = 0.6f;

        /// <summary>The flame from the nozzle to its tip at its longest, and its radius at the nozzle.</summary>
        public const float FlameLength = 0.55f;
        public const float FlameRadius = 0.11f;

        public const string BodyName = "Body";
        public const string TrimName = "Trim";
        public const string NozzleName = "Nozzle";
        public const string FlameName = "Flame";
        public const string FlameCoreName = "Flame core";

        /// <summary>Its colours: ivory paint as the buildings' walls, dark iron as the hatches, and the flame's orange and yellow.</summary>
        public static readonly Color Paint = new Color(0.94f, 0.92f, 0.86f);
        public static readonly Color Iron = new Color(0.2f, 0.2f, 0.22f);
        public static readonly Color FlameOuter = new Color(1f, 0.5f, 0.12f);
        public static readonly Color FlameInner = new Color(1f, 0.88f, 0.36f);

        // Heights along the missile, tail at -0.5 and nose at 0.5: where the nozzle ends, the body's shoulder, the band.
        private const float NozzleTop = -0.42f;
        private const float Shoulder = 0.16f;
        private const float BandFrom = -0.14f;
        private const float BandTo = -0.04f;

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        /// <summary>
        /// Builds the missile under <paramref name="parent"/>, its middle at the origin and its nose up, its trim in
        /// <paramref name="color"/>. The flame is the child named <see cref="FlameName"/>, its root at the nozzle: scale
        /// it along y to make it flicker.
        /// </summary>
        public static GameObject Create(Transform parent, string name, Color color)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            ModelParts.Create(BodyName, root.transform, BodyMesh(), Paint, 0.35f);
            ModelParts.Create(TrimName, root.transform, TrimMesh(), color, 0.3f);
            ModelParts.Create(NozzleName, root.transform, NozzleMesh(), Iron, 0.4f);

            var flame = new GameObject(FlameName);
            flame.transform.SetParent(root.transform, false);
            flame.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            Glow(ModelParts.Create("Flame outer", flame.transform, FlameMesh("missile-flame", FlameRadius, FlameLength), FlameOuter), FlameOuter);
            Glow(ModelParts.Create(FlameCoreName, flame.transform, FlameMesh("missile-flame-core", FlameRadius * 0.55f, FlameLength * 0.6f), FlameInner), FlameInner);
            return root;
        }

        /// <summary>A part that shines by itself and neither casts nor takes a shadow: fire looks the same in the sun and in the shade.</summary>
        private static void Glow(GameObject part, Color color)
        {
            var drawn = part.GetComponent<MeshRenderer>();
            drawn.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            drawn.receiveShadows = false;
            Material material = drawn.sharedMaterial;
            if (material == null || !material.HasProperty(EmissionColor)) return;
            material.color = Color.black;
            material.EnableKeyword("_EMISSION");
            material.SetColor(EmissionColor, color);
        }

        /// <summary>The ivory body: a tube from above the nozzle to its shoulder, closed at both ends.</summary>
        private static Mesh BodyMesh() => MeshFactory.Lathe("missile-body", new[]
        {
            new Vector2(0f, NozzleTop), new Vector2(Radius * 0.8f, NozzleTop), new Vector2(Radius, NozzleTop + 0.05f),
            new Vector2(Radius, Shoulder), new Vector2(0f, Shoulder),
        }, 20);

        /// <summary>What wears the player's colour: the rounded nose, a band round the body and the four swept fins.</summary>
        private static Mesh TrimMesh()
        {
            Mesh nose = MeshFactory.Lathe("missile-nose", new[]
            {
                new Vector2(0f, Shoulder), new Vector2(Radius * 1.04f, Shoulder), new Vector2(Radius * 1.04f, Shoulder + 0.03f), new Vector2(Radius * 0.9f, 0.3f),
                new Vector2(Radius * 0.62f, 0.41f), new Vector2(Radius * 0.3f, 0.48f), new Vector2(0f, 0.5f),
            }, 20);
            Mesh band = MeshFactory.Lathe("missile-band", new[]
            {
                new Vector2(0f, BandFrom), new Vector2(Radius * 1.05f, BandFrom), new Vector2(Radius * 1.05f, BandTo), new Vector2(0f, BandTo),
            }, 20);
            // A fin, drawn lying down as (out from the body, along it): swept back from the body to its tip.
            Mesh fin = MeshFactory.Plate("missile-fin", new[]
            {
                new Vector2(Radius * 0.7f, -0.16f), new Vector2(Radius * 0.7f, -0.44f), new Vector2(Span * 0.5f, -0.5f), new Vector2(Span * 0.5f, -0.38f),
            }, 0.03f);
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>
            {
                new KeyValuePair<Mesh, Matrix4x4>(nose, Matrix4x4.identity),
                new KeyValuePair<Mesh, Matrix4x4>(band, Matrix4x4.identity),
            };
            // The plate lies in x and z with its length along z; stood up, that length runs along the missile (y).
            Quaternion upright = Quaternion.Euler(-90f, 0f, 0f);
            for (int i = 0; i < 4; i++)
            {
                parts.Add(new KeyValuePair<Mesh, Matrix4x4>(fin, Matrix4x4.Rotate(Quaternion.Euler(0f, 90f * i, 0f) * upright)));
            }

            return MeshFactory.Combine("missile-trim", parts);
        }

        /// <summary>The dark nozzle under the body, widening to its mouth at the missile's tail.</summary>
        private static Mesh NozzleMesh() => MeshFactory.Lathe("missile-nozzle", new[]
        {
            new Vector2(0f, -0.5f), new Vector2(Radius * 0.85f, -0.5f), new Vector2(Radius * 0.6f, NozzleTop), new Vector2(0f, NozzleTop),
        }, 16);

        /// <summary>A flame hanging down from its root: round at the nozzle, drawn out to a point.</summary>
        private static Mesh FlameMesh(string name, float radius, float length) => MeshFactory.Lathe(name, new[]
        {
            new Vector2(0f, -length), new Vector2(radius * 0.45f, -length * 0.62f), new Vector2(radius * 0.9f, -length * 0.3f),
            new Vector2(radius, -length * 0.1f), new Vector2(radius * 0.7f, 0f), new Vector2(0f, 0f),
        }, 14);
    }
}
