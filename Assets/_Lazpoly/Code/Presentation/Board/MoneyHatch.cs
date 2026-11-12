using System.Collections.Generic;
using Lazpoly.Presentation.Effects;
using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// A player's money hatch on the table, the owner's wish (Faz 25): paid money goes down through the payer's hatch, and
    /// money a player gets comes up through theirs. Since 2026-11-12 (the owner's wish: more professional, pleasing, suiting
    /// the atmosphere) it is a little vault mouth set in the meadow: a curb of the table's own limestone with corner stones,
    /// a steel sleeve down the hole, and a lid in the safes' manner, almost black (one colour for every player, the owner's
    /// wish), with a raised panel, two brass straps running back to barrel hinges, chrome studs, a brass coin in its middle
    /// and a pull ring at its front. The lid is hinged on its back edge (away from the camera, so the open lid never hides
    /// the hole) and shows its lined underside when it stands open; down the hole is darkness with a glimmer of gold
    /// (<see cref="HatchSkin"/>). <see cref="Swing"/> opens it at one moment and closes it at another; the swing itself is
    /// pure (<see cref="OpennessAt"/>).
    /// </summary>
    public sealed class MoneyHatch : MonoBehaviour
    {
        /// <summary>The hole: long enough for a money stack lying as it lies in the case, along the hatch's x.</summary>
        public const float HoleLength = 0.9f;
        public const float HoleWidth = 0.6f;

        /// <summary>The stone curb round the hole, how high it stands on the grass, and the lid's thickness.</summary>
        public const float Rim = 0.13f;
        public const float FrameHeight = 0.07f;
        public const float LidThickness = 0.04f;

        /// <summary>How far the lid lies over the curb on every side, and how thick the steel sleeve down the hole is.</summary>
        public const float LidSeat = 0.045f;
        public const float Sleeve = 0.022f;

        /// <summary>The brass straps: how far each lies from the lid's middle, and how wide it is.</summary>
        public const float StrapOffset = 0.3f;
        public const float StrapWidth = 0.075f;

        /// <summary>The brass coin in the lid's middle: its radius.</summary>
        public const float CoinRadius = 0.115f;

        /// <summary>How far the corner stones stand over the curb, and how far the hole's floor lies over the grass.</summary>
        public const float CornerRise = 0.014f;
        public const float HoleLift = 0.003f;

        /// <summary>How far the lid swings open (a little past upright, leaning back) and how long a swing takes at normal speed.</summary>
        public const float OpenAngle = 105f;
        public const float SwingSeconds = 0.25f;

        /// <summary>The curb: the table's limestone, a little lighter in the sun on the grass; its corner stones a shade deeper.</summary>
        public static readonly Color Stone = Color.Lerp(PlateSkin.Stone, Color.white, 0.18f);
        public static readonly Color CornerStone = Color.Lerp(PlateSkin.Stone, Color.white, 0.06f);

        public static readonly Color HoleDark = HatchSkin.Dark;

        /// <summary>Every hatch's lid: nearly black, a shade lighter than the hole so the open lid still reads.</summary>
        public static readonly Color LidColor = new Color(0.11f, 0.11f, 0.12f);

        /// <summary>The lid's raised panel, a shade lighter than the lid, and the steel that lines its underside.</summary>
        public static readonly Color PanelColor = new Color(0.165f, 0.165f, 0.18f);
        public static readonly Color Lining = new Color(0.26f, 0.27f, 0.30f);

        private Transform _lid;
        private float _openAt = -1f;
        private float _closeAt = -1f;
        private float _age;
        private float _speed = 1f;

        /// <summary>The whole hatch's size on the table: the hole and its curb.</summary>
        public static Vector3 Size => new Vector3(HoleLength + 2f * Rim, FrameHeight, HoleWidth + 2f * Rim);

        /// <summary>The lid's size: the hole and its seat on the curb.</summary>
        public static Vector3 LidSize => new Vector3(HoleLength + 2f * LidSeat, LidThickness, HoleWidth + 2f * LidSeat);

        /// <summary>How open the lid is now, from 0 (shut) to 1 (open).</summary>
        public float Openness { get; private set; }

        /// <summary>The lid, turning on its hinge.</summary>
        public Transform Lid => _lid;

        /// <summary>The middle of the hole at the curb's top (world space): money goes down and comes up here.</summary>
        public Vector3 Mouth => transform.TransformPoint(new Vector3(0f, FrameHeight, 0f));

        public static MoneyHatch Create(Transform parent, string name)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            ModelParts.Create("Curb", root.transform, CurbMesh(), Stone, 0.12f);
            ModelParts.Create("Corner stones", root.transform, CornerMesh(), CornerStone, 0.12f);
            ModelParts.Create("Frame", root.transform, FrameMesh(), MoneyPile.Steel, 0.6f);
            GameObject hole = ModelParts.Create("Hole", root.transform, HoleMesh(), Color.white, 0.05f);
            ModelParts.Skin(hole, HatchSkin.Hole());
            hole.transform.localPosition = new Vector3(0f, HoleLift, 0f);

            // The hinge runs along the lid's back edge; in its frame the lid lies forward (-z) of it over the hole when shut.
            var hinge = new GameObject("Lid");
            hinge.transform.SetParent(root.transform, false);
            hinge.transform.localPosition = new Vector3(0f, FrameHeight, LidSize.z / 2f);
            ModelParts.Create("Lid plate", hinge.transform, LidPlateMesh(), LidColor, 0.45f); // first: the lid's colour is read from it
            ModelParts.Create("Lid panel", hinge.transform, LidPanelMesh(), PanelColor, 0.35f);
            ModelParts.Create("Lid brass", hinge.transform, LidBrassMesh(), MoneyPile.Brass, 0.75f);
            ModelParts.Create("Lid chrome", hinge.transform, LidChromeMesh(), MoneyPile.Chrome, 0.85f);
            ModelParts.Create("Lid lining", hinge.transform, LidLiningMesh(), Lining, 0.5f);

            var hatch = root.AddComponent<MoneyHatch>();
            hatch._lid = hinge.transform;
            return hatch;
        }

        /// <summary>
        /// Opens the lid <paramref name="openAt"/> seconds from now and shuts it <paramref name="closeAt"/> seconds from now
        /// (normal-speed seconds, played at <paramref name="speed"/>).
        /// </summary>
        public void Swing(float openAt, float closeAt, float speed)
        {
            _openAt = openAt;
            _closeAt = Mathf.Max(closeAt, openAt + SwingSeconds);
            _speed = Mathf.Max(speed, 0.1f);
            _age = 0f;
        }

        /// <summary>How open the lid is <paramref name="t"/> seconds in: it swings open from <paramref name="openAt"/>, stays, and swings shut from <paramref name="closeAt"/>.</summary>
        public static float OpennessAt(float t, float openAt, float closeAt)
        {
            if (t <= openAt) return 0f;
            if (t < openAt + SwingSeconds) return Ease.InOutCubic((t - openAt) / SwingSeconds);
            if (t <= closeAt) return 1f;
            if (t < closeAt + SwingSeconds) return 1f - Ease.InOutCubic((t - closeAt) / SwingSeconds);
            return 0f;
        }

        /// <summary>Sets the lid's angle for an openness from 0 to 1 (kept public so a test can check the swing).</summary>
        public void Show(float openness)
        {
            Openness = Mathf.Clamp01(openness);
            if (_lid != null) _lid.localRotation = Quaternion.Euler(OpenAngle * Openness, 0f, 0f);
        }

        private void Update()
        {
            if (_openAt < 0f) return;
            _age += Time.deltaTime * _speed;
            Show(OpennessAt(_age, _openAt, _closeAt));
            if (_age >= _closeAt + SwingSeconds) _openAt = -1f;
        }

        /// <summary>The curb: a stone along each side of the hole.</summary>
        private static Mesh CurbMesh()
        {
            Vector3 size = Size;
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>();
            foreach (float side in new[] { -1f, 1f })
            {
                Box(parts, new Vector3(HoleLength, FrameHeight, Rim), new Vector3(0f, FrameHeight / 2f, side * (size.z - Rim) / 2f), 0.012f);
                Box(parts, new Vector3(Rim, FrameHeight, HoleWidth), new Vector3(side * (size.x - Rim) / 2f, FrameHeight / 2f, 0f), 0.012f);
            }

            return MeshFactory.Combine("money-hatch-curb", parts);
        }

        /// <summary>The four corner stones, a little larger and taller than the curb they join.</summary>
        private static Mesh CornerMesh()
        {
            Vector3 size = Size;
            float height = FrameHeight + CornerRise, side = Rim + 0.012f;
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>();
            foreach (float x in new[] { -1f, 1f })
            foreach (float z in new[] { -1f, 1f })
                Box(parts, new Vector3(side, height, side), new Vector3(x * (size.x - Rim) / 2f, height / 2f, z * (size.z - Rim) / 2f), 0.012f);
            return MeshFactory.Combine("money-hatch-corners", parts);
        }

        /// <summary>The steel: the sleeve lining the hole's four walls, and the two barrels of the lid's hinges on the back curb.</summary>
        private static Mesh FrameMesh()
        {
            float height = FrameHeight + 0.004f;
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>();
            foreach (float side in new[] { -1f, 1f })
            {
                Box(parts, new Vector3(HoleLength, height, Sleeve), new Vector3(0f, height / 2f, side * (HoleWidth - Sleeve) / 2f), 0.004f);
                Box(parts, new Vector3(Sleeve, height, HoleWidth - 2f * Sleeve), new Vector3(side * (HoleLength - Sleeve) / 2f, height / 2f, 0f), 0.004f);
                // A barrel lying along the hinge, behind each strap.
                const float barrel = 0.13f, radius = 0.022f;
                parts.Add(new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Cylinder(12),
                    Matrix4x4.TRS(new Vector3(side * StrapOffset + barrel / 2f, FrameHeight, LidSize.z / 2f), Quaternion.Euler(0f, 0f, 90f), new Vector3(radius, barrel, radius))));
            }

            return MeshFactory.Combine("money-hatch-frame", parts);
        }

        private static Mesh _hole;

        /// <summary>The hole's floor: a flat sheet facing up, its picture laid over all of it.</summary>
        private static Mesh HoleMesh()
        {
            if (_hole != null) return _hole;
            float x = HoleLength / 2f, z = HoleWidth / 2f;
            var mesh = new Mesh { name = "money-hatch-hole" };
            mesh.SetVertices(new List<Vector3> { new Vector3(-x, 0f, -z), new Vector3(-x, 0f, z), new Vector3(x, 0f, z), new Vector3(x, 0f, -z) });
            mesh.SetNormals(new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) });
            mesh.SetTriangles(new List<int> { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
            return _hole = mesh;
        }

        // The lid's parts, in the hinge's frame: the hinge along x at the origin, the lid lying forward (-z) of it, its underside at y 0.

        private static Mesh LidPlateMesh()
        {
            Vector3 lid = LidSize;
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>();
            Box(parts, lid, new Vector3(0f, LidThickness / 2f, -lid.z / 2f), 0.012f);
            return MeshFactory.Combine("money-hatch-lid", parts);
        }

        /// <summary>The raised panel on the lid's top, in from its edge.</summary>
        private static Mesh LidPanelMesh()
        {
            Vector3 lid = LidSize;
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>();
            Box(parts, new Vector3(lid.x - 0.14f, 0.012f, lid.z - 0.14f), new Vector3(0f, LidThickness + 0.004f, -lid.z / 2f), 0.005f);
            return MeshFactory.Combine("money-hatch-lid-panel", parts);
        }

        /// <summary>The brass: two straps over the lid from front to hinge (and under it, on the lining), and the coin in the lid's middle.</summary>
        private static Mesh LidBrassMesh()
        {
            Vector3 lid = LidSize;
            float middle = -lid.z / 2f;
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>();
            foreach (float side in new[] { -1f, 1f })
            {
                Box(parts, new Vector3(StrapWidth, 0.02f, lid.z + 0.004f), new Vector3(side * StrapOffset, LidThickness + 0.008f, middle), 0.006f);
                Box(parts, new Vector3(StrapWidth, 0.008f, HoleWidth - 0.1f), new Vector3(side * StrapOffset, -0.014f, middle), 0.003f);
            }

            // The coin: a raised rim round a flat face.
            var coin = new Vector3(0f, LidThickness + 0.008f, middle + 0.02f);
            parts.Add(new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Ring(0.72f, 0.016f, 24), Matrix4x4.TRS(coin, Quaternion.identity, new Vector3(CoinRadius, 1f, CoinRadius))));
            parts.Add(new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Disc(0.009f, 24), Matrix4x4.TRS(coin, Quaternion.identity, new Vector3(CoinRadius * 0.74f, 1f, CoinRadius * 0.74f))));
            return MeshFactory.Combine("money-hatch-lid-brass", parts);
        }

        /// <summary>The chrome: three studs down each strap and the pull ring at the lid's front.</summary>
        private static Mesh LidChromeMesh()
        {
            Vector3 lid = LidSize;
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>();
            foreach (float side in new[] { -1f, 1f })
            foreach (float z in new[] { -0.07f, -lid.z / 2f, -lid.z + 0.07f })
            {
                parts.Add(new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Sphere(8),
                    Matrix4x4.TRS(new Vector3(side * StrapOffset, LidThickness + 0.018f, z), Quaternion.identity, new Vector3(0.02f, 0.012f, 0.02f))));
            }

            parts.Add(new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Ring(0.6f, 0.014f, 20),
                Matrix4x4.TRS(new Vector3(0f, LidThickness + 0.01f, -lid.z + 0.09f), Quaternion.identity, new Vector3(0.05f, 1f, 0.05f))));
            return MeshFactory.Combine("money-hatch-lid-chrome", parts);
        }

        /// <summary>The steel plate lining the lid's underside, which faces the table when the lid stands open; it fits down the hole when shut.</summary>
        private static Mesh LidLiningMesh()
        {
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>();
            Box(parts, new Vector3(HoleLength - 0.08f, 0.012f, HoleWidth - 0.08f), new Vector3(0f, -0.006f, -LidSize.z / 2f), 0.004f);
            return MeshFactory.Combine("money-hatch-lid-lining", parts);
        }

        private static void Box(List<KeyValuePair<Mesh, Matrix4x4>> parts, Vector3 size, Vector3 at, float radius) =>
            parts.Add(new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.RoundedBox(size, Mathf.Min(radius, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) / 2f), 2), Matrix4x4.Translate(at)));
    }
}
