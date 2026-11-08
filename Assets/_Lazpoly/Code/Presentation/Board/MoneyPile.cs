using System.Collections.Generic;
using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// A player's money lying at the table's edge in front of their corner card, the owner's wish (Faz 25): an open money
    /// case in the player's colour along the board's edge (the owner asked for a case instead of the first plain tray), its
    /// lid opened flat away from the board so it hides nothing, the owner's money stacks and gold bars in its bottom half,
    /// neatly side by side in one row (two rows hid the back one behind the front one from the low camera), rising over
    /// its rim. It grows and shrinks with the
    /// money the table shows; payments leave from it and arrive in it. One gold bar stands for every full
    /// <see cref="GoldValue"/> (at most <see cref="MaxGold"/>), one stack for every <see cref="StackValue"/> started of
    /// the rest, <see cref="MaxPieces"/> in all. The layout is pure (<see cref="Pieces"/>, <see cref="SlotOffset"/>).
    /// </summary>
    public sealed class MoneyPile
    {
        public const int GoldValue = 1000;
        public const int MaxGold = 2;
        public const int StackValue = 300;
        public const int MaxPieces = 4;

        /// <summary>The pieces lie at this share of their flying size, side by side in one row with this gap between them.</summary>
        public const float PieceScale = 0.55f;
        public const float Gap = 0.06f;

        /// <summary>
        /// The case's halves: this much room round the row inside the walls, walls this thick and this high, a floor this
        /// thick with a lining of this thickness on it. Each half is <see cref="CaseDepth"/> deep.
        /// </summary>
        public const float CaseMargin = 0.1f;
        public const float CaseWall = 0.05f;
        public const float CaseHeight = 0.22f;
        public const float CaseFloor = 0.04f;
        public const float LiningThickness = 0.015f;
        public const float CaseDepth = 0.95f;

        /// <summary>The handle stands this far out of the bottom half's front wall (the side away from the hinge).</summary>
        public const float HandleReach = 0.17f;

        /// <summary>Brass for the clasps and the hinges.</summary>
        public static readonly Color Brass = new Color(0.93f, 0.76f, 0.33f);

        private readonly List<GameObject> _pieces = new List<GameObject>();
        private MoneyPiece[] _shown = new MoneyPiece[0];
        private int _money = int.MinValue;

        public MoneyPile(Transform parent, string name, Color color)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            Root = root.transform;
            var shape = new GameObject("Case");
            shape.transform.SetParent(Root, false);
            Case = shape.transform;

            // In the case's own frame the lid lies at +z; OpenAway turns the whole case round when the lid must go the other way.
            Color lining = Color.Lerp(color, Color.black, 0.45f);
            Color handle = Color.Lerp(color, Color.black, 0.6f);
            ModelParts.Create("Shell", Case, ShellMesh(), color, 0.4f);
            ModelParts.Create("Lining", Case, LiningMesh(), lining, 0.1f);
            ModelParts.Create("Handle", Case, HandleMesh(), handle, 0.45f);
            ModelParts.Create("Brass", Case, BrassMesh(), Brass, 0.75f);
            Lid = new GameObject("Lid").transform;
            Lid.SetParent(Case, false);
            Lid.localPosition = new Vector3(0f, 0f, CaseDepth);
        }

        public Transform Root { get; }

        /// <summary>The case drawn round the money; the money itself lies under <see cref="Root"/>.</summary>
        public Transform Case { get; }

        /// <summary>The middle of the open lid's floor, where the lid lies beside the bottom half.</summary>
        public Transform Lid { get; }

        /// <summary>
        /// Opens the lid towards <paramref name="away"/> (in the pile's parent's frame, flat on the table): the side away from
        /// the board, so the lid never covers a square.
        /// </summary>
        public void OpenAway(Vector3 away)
        {
            Vector3 forward = Root.localRotation * Vector3.forward;
            Case.localRotation = Vector3.Dot(forward, away) >= 0f ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
        }

        /// <summary>The pieces lying on the tray now.</summary>
        public IReadOnlyList<MoneyPiece> Shown => _shown;

        /// <summary>The bottom half's outer size: room inside its walls for a row of <see cref="MaxPieces"/> stacks.</summary>
        public static Vector3 CaseSize => new Vector3(
            MaxPieces * Slot - Gap + 2f * (CaseMargin + CaseWall),
            CaseHeight,
            CaseDepth);

        /// <summary>Where the money lies: on the bottom half's lining.</summary>
        public static float MoneyFloor => CaseFloor + LiningThickness;

        /// <summary>The room one piece takes in the row: a stack's length and the gap.</summary>
        private static float Slot => MoneyBundle.Length(MoneyPiece.Stack) * PieceScale + Gap;

        /// <summary>Where money leaves the pile and arrives in it: just over the case's middle (world space).</summary>
        public Vector3 Top => Root.TransformPoint(new Vector3(0f, MoneyFloor + MoneyBundle.Height(MoneyPiece.Stack) * PieceScale, 0f));

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
        /// Where the <paramref name="index"/>th of <paramref name="count"/> pieces lies in the case, in the pile's frame (x
        /// along the board's edge): side by side in one row with even gaps, the row centred in the bottom half.
        /// </summary>
        public static Vector3 SlotOffset(int index, int count) => new Vector3((index - (count - 1) / 2f) * Slot, MoneyFloor, 0f);

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

        /// <summary>The two halves' shells, floor and four walls each, the lid's lying open beside the bottom one.</summary>
        private static Mesh ShellMesh()
        {
            Vector3 size = CaseSize;
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>();
            foreach (float z in new[] { 0f, CaseDepth })
            {
                Add(parts, new Vector3(size.x, CaseFloor, size.z), new Vector3(0f, CaseFloor / 2f, z));
                foreach (float side in new[] { -1f, 1f })
                {
                    Add(parts, new Vector3(size.x, CaseHeight, CaseWall), new Vector3(0f, CaseHeight / 2f, z + side * (size.z - CaseWall) / 2f));
                    Add(parts, new Vector3(CaseWall, CaseHeight, size.z - 2f * CaseWall), new Vector3(side * (size.x - CaseWall) / 2f, CaseHeight / 2f, z));
                }
            }

            return MeshFactory.Combine("money-case-shell", parts);
        }

        /// <summary>The lining on both floors, and a pocket on the lid's.</summary>
        private static Mesh LiningMesh()
        {
            Vector3 size = CaseSize;
            var inside = new Vector3(size.x - 2f * CaseWall, LiningThickness, size.z - 2f * CaseWall);
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>();
            foreach (float z in new[] { 0f, CaseDepth }) Add(parts, inside, new Vector3(0f, CaseFloor + LiningThickness / 2f, z));
            var pocket = new Vector3(inside.x - 0.2f, 0.03f, inside.z * 0.45f);
            Add(parts, pocket, new Vector3(0f, MoneyFloor + pocket.y / 2f, CaseDepth + (inside.z - pocket.z) / 2f - 0.05f));
            return MeshFactory.Combine("money-case-lining", parts);
        }

        /// <summary>The handle on the bottom half's front wall (the side away from the lid): a grip on two posts.</summary>
        private static Mesh HandleMesh()
        {
            float front = -CaseDepth / 2f, height = CaseHeight * 0.55f;
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>();
            Add(parts, new Vector3(0.95f, 0.1f, 0.1f), new Vector3(0f, height, front - HandleReach + 0.05f));
            foreach (float side in new[] { -1f, 1f }) Add(parts, new Vector3(0.09f, 0.08f, HandleReach), new Vector3(side * 0.4f, height, front - HandleReach / 2f));
            return MeshFactory.Combine("money-case-handle", parts);
        }

        /// <summary>Brass clasps on both halves' front edges and hinges between the halves.</summary>
        private static Mesh BrassMesh()
        {
            Vector3 size = CaseSize;
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>();
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * size.x * 0.3f;
                Add(parts, new Vector3(0.16f, 0.07f, 0.03f), new Vector3(x, CaseHeight - 0.035f, -size.z / 2f - 0.015f));
                Add(parts, new Vector3(0.16f, 0.07f, 0.03f), new Vector3(x, CaseHeight - 0.035f, CaseDepth + size.z / 2f + 0.015f));
                Add(parts, new Vector3(0.3f, 0.05f, 0.06f), new Vector3(side * size.x * 0.35f, CaseHeight - 0.01f, size.z / 2f));
            }

            return MeshFactory.Combine("money-case-brass", parts);
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
