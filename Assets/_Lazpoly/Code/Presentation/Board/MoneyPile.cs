using System.Collections.Generic;
using Lazpoly.Presentation.Effects;
using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// A player's money at the table's edge in front of their corner card, the owner's wish (Faz 25): since 2026-11-11 a
    /// steel money safe in the player's colour (the owner gave up the open case for it), shut, its lid hinged along its back
    /// (away from the camera, so the open lid never hides the money). The lid opens when money goes in or comes out and
    /// shuts after it (<see cref="Swing"/>). Inside lie the owner's money stacks and gold bars, two rows of two. It grows
    /// and shrinks with the money the table shows; payments leave from it and arrive in it. One gold bar stands for every
    /// full <see cref="GoldValue"/> (at most <see cref="MaxGold"/>), one stack for every <see cref="StackValue"/> started of
    /// the rest, <see cref="MaxPieces"/> in all. The layout is pure (<see cref="Pieces"/>, <see cref="SlotOffset"/>).
    /// </summary>
    public sealed class MoneyPile
    {
        public const int GoldValue = 1000000;
        public const int MaxGold = 2;
        public const int StackValue = 300000;
        public const int MaxPieces = 4;

        /// <summary>The pieces lie at this share of their flying size, in two rows of two with this gap between them.</summary>
        public const float PieceScale = 0.55f;
        public const float Gap = 0.06f;
        public const int PerRow = 2;

        /// <summary>
        /// The safe: this much room round the money inside its walls, steel walls this thick, a floor this thick with a
        /// lining on it, room this high over the tallest piece, and a lid this thick.
        /// </summary>
        public const float SafeMargin = 0.08f;
        public const float SafeWall = 0.08f;
        public const float SafeFloor = 0.06f;
        public const float LiningThickness = 0.015f;
        public const float Headroom = 0.05f;
        public const float LidThickness = 0.08f;

        /// <summary>How far the lid swings open (a little past upright, leaning back) and how long a swing takes at normal speed.</summary>
        public const float OpenAngle = 105f;
        public const float SwingSeconds = 0.25f;

        /// <summary>Steel for the corner guards, the hinges and the dial's ring; chrome for the dial and the lever.</summary>
        public static readonly Color Steel = new Color(0.20f, 0.21f, 0.23f);
        public static readonly Color Chrome = new Color(0.82f, 0.83f, 0.85f);
        public static readonly Color Brass = new Color(0.93f, 0.76f, 0.33f);

        private readonly List<GameObject> _pieces = new List<GameObject>();
        private readonly SafeLid _swing;
        private MoneyPiece[] _shown = new MoneyPiece[0];
        private int _money = int.MinValue;

        public MoneyPile(Transform parent, string name, Color color)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            Root = root.transform;
            var shape = new GameObject("Player safe"); // not "Safe": the bank's safe on the lawn is gone and a test keeps it so
            shape.transform.SetParent(Root, false);
            Safe = shape.transform;

            // In the pile's frame -z faces the camera (BoardView turns it so): the dial and the lever on that front, the hinge at the back.
            Color paint = Color.Lerp(color, Steel, 0.12f);
            Color lining = Color.Lerp(color, Color.black, 0.65f);
            ModelParts.Create("Body", Safe, BodyMesh(), paint, 0.55f);
            ModelParts.Create("Lining", Safe, LiningMesh(), lining, 0.05f);
            ModelParts.Create("Guards", Safe, GuardMesh(), Steel, 0.6f);
            ModelParts.Create("Dial ring", Safe, DialRingMesh(), Steel, 0.6f);
            ModelParts.Create("Dial", Safe, DialMesh(), Chrome, 0.85f);
            ModelParts.Create("Lever", Safe, LeverMesh(), Chrome, 0.85f);
            ModelParts.Create("Plate", Safe, PlateMesh(), Brass, 0.75f);
            ModelParts.Create("Hinges", Safe, HingeMesh(), Steel, 0.6f);

            Lid = new GameObject("Lid").transform;
            Lid.SetParent(Safe, false);
            Lid.localPosition = new Vector3(0f, BodyHeight, SafeSize.z / 2f);
            ModelParts.Create("Lid top", Lid, LidMesh(), paint, 0.55f);
            ModelParts.Create("Lid rim", Lid, LidRimMesh(), Steel, 0.6f);
            ModelParts.Create("Lid lining", Lid, LidLiningMesh(), lining, 0.05f);
            _swing = root.AddComponent<SafeLid>();
            _swing.Hinge = Lid;
            _swing.Show(0f);
        }

        public Transform Root { get; }

        /// <summary>The safe drawn round the money; the money itself lies under <see cref="Root"/>.</summary>
        public Transform Safe { get; }

        /// <summary>The lid, turning on its hinge along the safe's back top edge.</summary>
        public Transform Lid { get; }

        /// <summary>How open the lid is now, from 0 (shut) to 1 (open).</summary>
        public float Openness => _swing != null ? _swing.Openness : 0f;

        /// <summary>
        /// Opens the lid <paramref name="openAt"/> seconds from now and shuts it <paramref name="closeAt"/> seconds from now
        /// (normal-speed seconds, played at <paramref name="speed"/>).
        /// </summary>
        public void Swing(float openAt, float closeAt, float speed)
        {
            if (_swing != null) _swing.Swing(openAt, closeAt, speed);
        }

        /// <summary>Sets the lid for an openness from 0 to 1 (kept public so a test can check the swing).</summary>
        public void ShowLid(float openness)
        {
            if (_swing != null) _swing.Show(openness);
        }

        /// <summary>How open a lid is <paramref name="t"/> seconds in: it swings open from <paramref name="openAt"/>, stays, and swings shut from <paramref name="closeAt"/>.</summary>
        public static float OpennessAt(float t, float openAt, float closeAt)
        {
            if (t <= openAt) return 0f;
            if (t < openAt + SwingSeconds) return Ease.InOutCubic((t - openAt) / SwingSeconds);
            if (t <= closeAt) return 1f;
            if (t < closeAt + SwingSeconds) return 1f - Ease.InOutCubic((t - closeAt) / SwingSeconds);
            return 0f;
        }

        /// <summary>The widest piece across, and the tallest.</summary>
        private static float PieceWidth => Mathf.Max(MoneyBundle.Width(MoneyPiece.Stack), MoneyBundle.Width(MoneyPiece.Gold)) * PieceScale;
        private static float PieceHeight => Mathf.Max(MoneyBundle.Height(MoneyPiece.Stack), MoneyBundle.Height(MoneyPiece.Gold)) * PieceScale;

        /// <summary>The room inside the walls: two pieces along, two rows across, the margin round them; as high as the tallest piece and the headroom.</summary>
        public static Vector3 Inside => new Vector3(
            PerRow * Slot - Gap + 2f * SafeMargin,
            LiningThickness + PieceHeight + Headroom,
            2f * PieceWidth + Gap + 2f * SafeMargin);

        /// <summary>The body's height, to the lid's underside.</summary>
        public static float BodyHeight => SafeFloor + Inside.y;

        /// <summary>The whole safe's outer size, its lid shut.</summary>
        public static Vector3 SafeSize => new Vector3(Inside.x + 2f * SafeWall, BodyHeight + LidThickness, Inside.z + 2f * SafeWall);

        /// <summary>Where the money lies: on the safe's lining.</summary>
        public static float MoneyFloor => SafeFloor + LiningThickness;

        /// <summary>The pieces lying on the tray now.</summary>
        public IReadOnlyList<MoneyPiece> Shown => _shown;


        /// <summary>The room one piece takes in the row: a stack's length and the gap.</summary>
        private static float Slot => MoneyBundle.Length(MoneyPiece.Stack) * PieceScale + Gap;

        /// <summary>The pieces that show <paramref name="money"/>: the gold bars first, then the stacks; none for nothing.</summary>
        public static MoneyPiece[] Pieces(int money)
        {
            if (money <= 0) return new MoneyPiece[0];
            int gold = Mathf.Min(money / GoldValue, MaxGold);
            int rest = money - gold * GoldValue;
            int stacks = Mathf.Min((rest + StackValue - 1) / StackValue, MaxPieces - gold);
            if (gold + stacks == 0) stacks = 1;
            var pieces = new MoneyPiece[gold + stacks];
            for (int i = 0; i < pieces.Length; i++) pieces[i] = i < gold ? MoneyPiece.Gold : MoneyPiece.Stack;
            return pieces;
        }

        /// <summary>
        /// Where the <paramref name="index"/>th of <paramref name="count"/> pieces lies in the safe, in the pile's frame (x
        /// along the board's edge, -z towards the camera): up to two side by side in the front row, the rest in the back row,
        /// each row centred; one or two pieces lie in one row across the middle.
        /// </summary>
        public static Vector3 SlotOffset(int index, int count)
        {
            count = Mathf.Max(count, 1);
            int rows = count > PerRow ? 2 : 1;
            int row = rows == 1 ? 0 : index / PerRow;
            int inRow = rows == 1 ? count : (row == 0 ? PerRow : count - PerRow);
            int column = rows == 1 ? index : index % PerRow;
            float z = rows == 1 ? 0f : (row == 0 ? -1f : 1f) * (PieceWidth + Gap) / 2f;
            return new Vector3((column - (inRow - 1) / 2f) * Slot, MoneyFloor, z);
        }

        /// <summary>Shows <paramref name="money"/>: the pieces change only when the money calls for other pieces.</summary>
        public void Show(int money)
        {
            if (money == _money) return;
            _money = money;
            MoneyPiece[] wanted = Pieces(money);
            if (Same(wanted, _shown)) return;

            foreach (GameObject piece in _pieces)
            {
                if (Application.isPlaying) Object.Destroy(piece);
                else Object.DestroyImmediate(piece);
            }

            _pieces.Clear();
            for (int i = 0; i < wanted.Length; i++)
            {
                GameObject piece = MoneyBundle.Create(wanted[i], $"Money {wanted[i]}", Root);
                piece.transform.localPosition = SlotOffset(i, wanted.Length);
                piece.transform.localScale = Vector3.one * PieceScale;
                _pieces.Add(piece);
            }

            _shown = wanted;
        }

        /// <summary>The body: a floor and four thick walls, its edges only just rounded.</summary>
        private static Mesh BodyMesh()
        {
            Vector3 size = SafeSize;
            float height = BodyHeight;
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>();
            Add(parts, new Vector3(size.x, SafeFloor, size.z), new Vector3(0f, SafeFloor / 2f, 0f));
            foreach (float side in new[] { -1f, 1f })
            {
                Add(parts, new Vector3(size.x, height, SafeWall), new Vector3(0f, height / 2f, side * (size.z - SafeWall) / 2f));
                Add(parts, new Vector3(SafeWall, height, size.z - 2f * SafeWall), new Vector3(side * (size.x - SafeWall) / 2f, height / 2f, 0f));
            }

            return MeshFactory.Combine("money-safe-body", parts);
        }

        /// <summary>The dark lining on the floor and up the inside of the walls, so the open safe reads deep.</summary>
        private static Mesh LiningMesh()
        {
            Vector3 inside = Inside;
            const float skin = 0.01f;
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>();
            Add(parts, new Vector3(inside.x, LiningThickness, inside.z), new Vector3(0f, SafeFloor + LiningThickness / 2f, 0f));
            float y = SafeFloor + inside.y / 2f;
            foreach (float side in new[] { -1f, 1f })
            {
                Add(parts, new Vector3(inside.x, inside.y, skin), new Vector3(0f, y, side * (inside.z - skin) / 2f));
                Add(parts, new Vector3(skin, inside.y, inside.z - 2f * skin), new Vector3(side * (inside.x - skin) / 2f, y, 0f));
            }

            return MeshFactory.Combine("money-safe-lining", parts);
        }

        /// <summary>Steel guards down the four upright edges and a steel band round the foot.</summary>
        private static Mesh GuardMesh()
        {
            Vector3 size = SafeSize;
            const float guard = 0.1f, stand = 0.012f, foot = 0.07f;
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>();
            foreach (float x in new[] { -1f, 1f })
            foreach (float z in new[] { -1f, 1f })
            {
                Add(parts, new Vector3(guard, BodyHeight, guard),
                    new Vector3(x * (size.x / 2f - guard / 2f + stand), BodyHeight / 2f, z * (size.z / 2f - guard / 2f + stand)));
            }

            Add(parts, new Vector3(size.x + 2f * stand, foot, size.z + 2f * stand), new Vector3(0f, foot / 2f, 0f));
            return MeshFactory.Combine("money-safe-guards", parts);
        }

        /// <summary>Where the dial sits on the front, and how big it is.</summary>
        private static Vector3 DialAt => new Vector3(-SafeSize.x * 0.12f, BodyHeight * 0.52f, -SafeSize.z / 2f);
        public const float DialRadius = 0.11f;

        /// <summary>A dark ring round the dial, standing a little off the front.</summary>
        private static Mesh DialRingMesh() => Disc("money-safe-dial-ring", DialAt, DialRadius * 1.35f, 0.02f);

        /// <summary>The chrome dial: a knob on the ring with a grip on it.</summary>
        private static Mesh DialMesh()
        {
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>
            {
                new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Cylinder(24),
                    Matrix4x4.TRS(DialAt + new Vector3(0f, 0f, -0.02f), Quaternion.Euler(-90f, 0f, 0f), new Vector3(DialRadius, 0.035f, DialRadius))),
                new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Cylinder(16),
                    Matrix4x4.TRS(DialAt + new Vector3(0f, 0f, -0.055f), Quaternion.Euler(-90f, 0f, 0f), new Vector3(DialRadius * 0.35f, 0.04f, DialRadius * 0.35f))),
            };
            return MeshFactory.Combine("money-safe-dial", parts);
        }

        /// <summary>The chrome lever beside the dial: a hub and three spokes, as on a vault door.</summary>
        private static Mesh LeverMesh()
        {
            Vector3 hub = DialAt + new Vector3(SafeSize.x * 0.3f, 0f, -0.02f);
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>
            {
                new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Cylinder(16),
                    Matrix4x4.TRS(hub, Quaternion.Euler(-90f, 0f, 0f), new Vector3(0.045f, 0.05f, 0.045f))),
            };
            for (int spoke = 0; spoke < 3; spoke++)
            {
                Quaternion turn = Quaternion.Euler(0f, 0f, 90f + spoke * 120f);
                Vector3 way = turn * Vector3.right;
                parts.Add(new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.RoundedBox(new Vector3(0.16f, 0.025f, 0.025f), 0.012f, 2),
                    Matrix4x4.TRS(hub + way * 0.08f + new Vector3(0f, 0f, -0.04f), turn, Vector3.one)));
                parts.Add(new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.RoundedBox(new Vector3(0.04f, 0.04f, 0.04f), 0.02f, 2),
                    Matrix4x4.TRS(hub + way * 0.16f + new Vector3(0f, 0f, -0.04f), turn, Vector3.one)));
            }

            return MeshFactory.Combine("money-safe-lever", parts);
        }

        /// <summary>A small brass name plate under the dial and the lever.</summary>
        private static Mesh PlateMesh()
        {
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>();
            Add(parts, new Vector3(SafeSize.x * 0.32f, 0.06f, 0.012f), new Vector3(0f, BodyHeight * 0.2f, -SafeSize.z / 2f - 0.006f));
            return MeshFactory.Combine("money-safe-plate", parts);
        }

        /// <summary>Two steel hinges along the back top edge, where the lid turns.</summary>
        private static Mesh HingeMesh()
        {
            Vector3 size = SafeSize;
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>();
            foreach (float side in new[] { -1f, 1f })
            {
                parts.Add(new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Cylinder(12),
                    Matrix4x4.TRS(new Vector3(side * size.x * 0.3f - 0.12f, BodyHeight, size.z / 2f + 0.01f), Quaternion.Euler(0f, 0f, -90f), new Vector3(0.04f, 0.24f, 0.04f))));
            }

            return MeshFactory.Combine("money-safe-hinges", parts);
        }

        /// <summary>The lid in its hinge's frame: lying forward of the hinge (-z), a little over the body all round.</summary>
        private static Mesh LidMesh()
        {
            Vector3 size = SafeSize;
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>();
            Add(parts, new Vector3(size.x + 0.02f, LidThickness * 0.7f, size.z + 0.02f), new Vector3(0f, LidThickness * 0.65f, -size.z / 2f));
            // A raised panel on top.
            Add(parts, new Vector3(size.x * 0.8f, 0.02f, size.z * 0.7f), new Vector3(0f, LidThickness + 0.005f, -size.z / 2f));
            return MeshFactory.Combine("money-safe-lid", parts);
        }

        /// <summary>The lid's steel rim under its edge, and its grip at the front.</summary>
        private static Mesh LidRimMesh()
        {
            Vector3 size = SafeSize;
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>();
            Add(parts, new Vector3(size.x + 0.03f, LidThickness * 0.32f, size.z + 0.03f), new Vector3(0f, LidThickness * 0.16f, -size.z / 2f));
            Add(parts, new Vector3(0.36f, 0.035f, 0.05f), new Vector3(0f, LidThickness * 0.5f, -size.z - 0.035f));
            return MeshFactory.Combine("money-safe-lid-rim", parts);
        }

        /// <summary>The dark lining under the lid, seen when it stands open.</summary>
        private static Mesh LidLiningMesh()
        {
            Vector3 inside = Inside;
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>();
            Add(parts, new Vector3(inside.x, 0.01f, inside.z), new Vector3(0f, -0.004f, -SafeSize.z / 2f));
            return MeshFactory.Combine("money-safe-lid-lining", parts);
        }

        /// <summary>A flat disc on the front, facing the camera (-z).</summary>
        private static Mesh Disc(string name, Vector3 at, float radius, float depth)
        {
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>
            {
                new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Cylinder(24), Matrix4x4.TRS(at, Quaternion.Euler(-90f, 0f, 0f), new Vector3(radius, depth, radius))),
            };
            return MeshFactory.Combine(name, parts);
        }

        private static void Add(List<KeyValuePair<Mesh, Matrix4x4>> parts, Vector3 size, Vector3 at) =>
            parts.Add(new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.RoundedBox(size, Mathf.Min(0.02f, size.y / 2f), 2), Matrix4x4.Translate(at)));

        private static bool Same(MoneyPiece[] a, MoneyPiece[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }

            return true;
        }
    }
}
