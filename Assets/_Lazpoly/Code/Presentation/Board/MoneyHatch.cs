using System.Collections.Generic;
using Lazpoly.Presentation.Effects;
using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// A player's money hatch on the table, the owner's wish (Faz 25): paid money goes down through the payer's hatch, and
    /// money a player gets comes up through theirs. Since 2026-11-12 (the owner's wish: plainer and more elegant, "it could
    /// even be a manhole cover") it is a round cast-iron cover set in the meadow: a low iron collar with a lip, and in it a
    /// lid that is almost black (one colour for every player, the owner's wish) with nothing on it but the casting's own
    /// relief: a rim, a ring, a boss in the middle and short bars between rim and ring. The lid is hinged at its back (away
    /// from the camera, so the open lid never hides the hole) and shows its ribbed underside when it stands open; down the
    /// hole is darkness with a glimmer of gold (<see cref="HatchSkin"/>). <see cref="Swing"/> opens it at one moment and
    /// closes it at another; the swing itself is pure (<see cref="OpennessAt"/>).
    /// </summary>
    public sealed class MoneyHatch : MonoBehaviour
    {
        /// <summary>The round hole: wide enough for a money stack lying flat to pass whichever way it is turned.</summary>
        public const float HoleRadius = 0.46f;
        public const float HoleLength = 2f * HoleRadius;
        public const float HoleWidth = 2f * HoleRadius;

        /// <summary>The collar round the hole: its width, how high the lid's seat lies over the grass, and the lid's thickness (the lip stands that much over the seat).</summary>
        public const float Rim = 0.07f;
        public const float FrameHeight = 0.03f;
        public const float LidThickness = 0.03f;

        /// <summary>How far the lid lies over the seat all round, and the gap between its edge and the collar's lip.</summary>
        public const float LidSeat = 0.03f;
        public const float LidGap = 0.006f;

        /// <summary>The casting's relief on the lid: how far it stands proud, and how many bars lie between the rim and the ring.</summary>
        public const float Relief = 0.008f;
        public const int Bars = 12;

        /// <summary>The relief's rings, as shares of the lid's radius: the rim's inner edge, the ring's two edges, the boss.</summary>
        public const float RimFrom = 0.85f;
        public const float RingTo = 0.62f;
        public const float RingFrom = 0.55f;
        public const float Boss = 0.24f;

        /// <summary>How far the hole's floor lies over the grass.</summary>
        public const float HoleLift = 0.003f;

        /// <summary>How far the lid swings open (a little past upright, leaning back) and how long a swing takes at normal speed.</summary>
        public const float OpenAngle = 105f;
        public const float SwingSeconds = 0.25f;

        public static readonly Color HoleDark = HatchSkin.Dark;

        /// <summary>Every hatch's lid: nearly black, a shade lighter than the hole so the open lid still reads.</summary>
        public static readonly Color LidColor = new Color(0.11f, 0.11f, 0.12f);

        /// <summary>The relief cast on the lid, a shade lighter than the lid so it reads as raised; and the lid's underside.</summary>
        public static readonly Color ReliefColor = new Color(0.165f, 0.165f, 0.18f);
        public static readonly Color Underside = new Color(0.15f, 0.155f, 0.17f);

        /// <summary>The collar: the safes' dark steel, a little lighter than the lid it holds.</summary>
        public static readonly Color Iron = MoneyPile.Steel;

        private Transform _lid;
        private float _openAt = -1f;
        private float _closeAt = -1f;
        private float _age;
        private float _speed = 1f;

        /// <summary>The lid's radius, and the collar's outer one.</summary>
        public const float LidRadius = HoleRadius + LidSeat;
        public const float OuterRadius = HoleRadius + Rim;

        /// <summary>The whole hatch's size on the table: the collar's width both ways, and its lip's height.</summary>
        public static Vector3 Size => new Vector3(2f * OuterRadius, FrameHeight + LidThickness, 2f * OuterRadius);

        /// <summary>The lid's size: the hole and its seat on the collar.</summary>
        public static Vector3 LidSize => new Vector3(2f * LidRadius, LidThickness, 2f * LidRadius);

        /// <summary>How open the lid is now, from 0 (shut) to 1 (open).</summary>
        public float Openness { get; private set; }

        /// <summary>The lid, turning on its hinge.</summary>
        public Transform Lid => _lid;

        /// <summary>The middle of the hole at the lid's seat (world space): money goes down and comes up here.</summary>
        public Vector3 Mouth => transform.TransformPoint(new Vector3(0f, FrameHeight, 0f));

        public static MoneyHatch Create(Transform parent, string name)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            ModelParts.Create("Collar", root.transform, CollarMesh(), Iron, 0.45f);
            GameObject hole = ModelParts.Create("Hole", root.transform, HoleMesh(), Color.white, 0.05f);
            ModelParts.Skin(hole, HatchSkin.Hole());
            hole.transform.localPosition = new Vector3(0f, HoleLift, 0f);

            // The hinge lies at the back of the lid's top, level with the lip; in its frame the lid lies forward (-z) of it
            // and under it when shut, so the open lid stands on the lip instead of cutting through it.
            var hinge = new GameObject("Lid");
            hinge.transform.SetParent(root.transform, false);
            hinge.transform.localPosition = new Vector3(0f, FrameHeight + LidThickness, LidRadius + LidGap);
            ModelParts.Create("Lid plate", hinge.transform, LidPlateMesh(), LidColor, 0.4f); // first: the lid's colour is read from it
            ModelParts.Create("Lid relief", hinge.transform, LidReliefMesh(), ReliefColor, 0.4f);
            ModelParts.Create("Lid underside", hinge.transform, LidUndersideMesh(), Underside, 0.35f);

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

        /// <summary>
        /// The collar, turned round the hole: up its outer side, in over a chamfered lip, down to the seat the lid lies
        /// on, in to the hole and down its wall; and the knuckle of the lid's hinge on the lip at the back.
        /// </summary>
        private static Mesh CollarMesh()
        {
            const float chamfer = 0.008f, knuckle = 0.12f, radius = 0.02f;
            float top = FrameHeight + LidThickness, seat = LidRadius + LidGap;
            // The knuckle lies on the lip behind the lid, inside the collar's own round.
            Mesh ring = MeshFactory.Lathe("money-hatch-collar", Sharp(
                new Vector2(HoleRadius, 0f), new Vector2(OuterRadius, 0f), new Vector2(OuterRadius, top - chamfer), new Vector2(OuterRadius - chamfer, top),
                new Vector2(seat, top), new Vector2(seat, FrameHeight), new Vector2(HoleRadius, FrameHeight), new Vector2(HoleRadius, 0f)), 48);
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>
            {
                new KeyValuePair<Mesh, Matrix4x4>(ring, Matrix4x4.identity),
                // A cylinder's axis is its y: laid along the hinge, it runs from its foot towards -x.
                new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Cylinder(12),
                    Matrix4x4.TRS(new Vector3(knuckle / 2f, top, OuterRadius - radius - 0.004f), Quaternion.Euler(0f, 0f, 90f), new Vector3(radius, knuckle, radius))),
            };
            return MeshFactory.Combine("money-hatch-collar", parts);
        }

        private static Mesh _hole;

        /// <summary>The hole's floor: a round sheet facing up, its picture laid over all of it.</summary>
        private static Mesh HoleMesh()
        {
            if (_hole != null) return _hole;
            const int segments = 40;
            var vertices = new List<Vector3> { Vector3.zero };
            var normals = new List<Vector3> { Vector3.up };
            var uvs = new List<Vector2> { new Vector2(0.5f, 0.5f) };
            var triangles = new List<int>();
            for (int i = 0; i < segments; i++)
            {
                float angle = i * 2f * Mathf.PI / segments;
                var way = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vertices.Add(new Vector3(way.x, 0f, way.y) * HoleRadius);
                normals.Add(Vector3.up);
                uvs.Add(new Vector2(0.5f, 0.5f) + way * 0.5f);
                triangles.Add(0);
                triangles.Add(1 + (i + 1) % segments);
                triangles.Add(1 + i);
            }

            var mesh = new Mesh { name = "money-hatch-hole" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return _hole = mesh;
        }

        // The lid's parts, in the hinge's frame: the hinge along x at the origin, the lid lying forward (-z) of it, its top at y 0.

        /// <summary>The middle of the lid's top in the hinge's frame.</summary>
        private static Vector3 LidMiddle => new Vector3(0f, 0f, -(LidRadius + LidGap));

        private static Mesh LidPlateMesh()
        {
            const float chamfer = 0.006f;
            Mesh disc = MeshFactory.Lathe("money-hatch-lid", Sharp(
                new Vector2(0f, 0f), new Vector2(LidRadius, 0f), new Vector2(LidRadius, LidThickness - chamfer), new Vector2(LidRadius - chamfer, LidThickness),
                new Vector2(0f, LidThickness)), 48);
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>
            {
                new KeyValuePair<Mesh, Matrix4x4>(disc, Matrix4x4.Translate(LidMiddle + Vector3.down * LidThickness)),
            };
            return MeshFactory.Combine("money-hatch-lid", parts);
        }

        /// <summary>The casting's relief on the lid's top: the rim, the ring, the boss, and the bars between rim and ring.</summary>
        private static Mesh LidReliefMesh()
        {
            const float sunk = 0.002f, barWidth = 0.034f;
            float height = Relief + sunk;
            Vector3 foot = LidMiddle + Vector3.down * sunk;
            float rim = LidRadius - 0.03f;
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>
            {
                new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Ring(RimFrom, 1f), Matrix4x4.TRS(foot, Quaternion.identity, new Vector3(rim, height, rim))),
                new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Ring(RingFrom / RingTo, 1f),
                    Matrix4x4.TRS(foot, Quaternion.identity, new Vector3(LidRadius * RingTo, height, LidRadius * RingTo))),
                new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Disc(1f), Matrix4x4.TRS(foot, Quaternion.identity, new Vector3(LidRadius * Boss, height, LidRadius * Boss))),
            };

            // The bars run from the ring out to the rim, set between the hinge's line and the cross's, so none lies under the knuckle.
            float from = LidRadius * RingTo - 0.004f, to = rim * RimFrom + 0.004f;
            Mesh bar = MeshFactory.RoundedBox(new Vector3(to - from, height, barWidth), 0.003f, 2);
            for (int i = 0; i < Bars; i++)
            {
                Quaternion turn = Quaternion.Euler(0f, (i + 0.5f) * 360f / Bars, 0f);
                parts.Add(new KeyValuePair<Mesh, Matrix4x4>(bar, Matrix4x4.TRS(foot + turn * new Vector3((from + to) / 2f, height / 2f, 0f), turn, Vector3.one)));
            }

            return MeshFactory.Combine("money-hatch-lid-relief", parts);
        }

        /// <summary>
        /// The lid's underside, which faces the table when the lid stands open: a plate that fits down the hole when the
        /// lid is shut, stiffened by a rib ring and a boss.
        /// </summary>
        private static Mesh LidUndersideMesh()
        {
            const float plate = 0.008f, rib = 0.008f;
            float radius = HoleRadius - 0.03f;
            Vector3 under = LidMiddle + Vector3.down * (LidThickness + plate);
            Vector3 ribs = under + Vector3.down * rib;
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>
            {
                new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Disc(1f), Matrix4x4.TRS(under, Quaternion.identity, new Vector3(radius, plate, radius))),
                new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Ring(0.88f, 1f), Matrix4x4.TRS(ribs, Quaternion.identity, new Vector3(radius * 0.8f, rib, radius * 0.8f))),
                new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Disc(1f), Matrix4x4.TRS(ribs, Quaternion.identity, new Vector3(radius * 0.22f, rib, radius * 0.22f))),
            };
            return MeshFactory.Combine("money-hatch-lid-underside", parts);
        }

        /// <summary>A profile for <see cref="MeshFactory.Lathe"/> with every point doubled, so each face keeps its own flat shading.</summary>
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
    }
}
