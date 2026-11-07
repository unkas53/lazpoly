using System.Collections.Generic;
using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// A player's money lying at the table's edge in front of their corner card, the owner's wish (Faz 25): a tray in the
    /// player's colour along the board's edge with the owner's money stacks and gold bars on it, neatly side by side in one
    /// row (two rows hid the back one behind the front one from the low camera). It grows and shrinks with the
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

        /// <summary>The tray under the money: its size around the row and its thickness.</summary>
        public const float TrayMargin = 0.15f;
        public const float TrayThickness = 0.06f;

        private readonly List<GameObject> _pieces = new List<GameObject>();
        private MoneyPiece[] _shown = new MoneyPiece[0];
        private int _money = int.MinValue;

        public MoneyPile(Transform parent, string name, Color color)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            Root = root.transform;
            Vector3 tray = TraySize;
            GameObject plate = ModelParts.Create("Tray", Root, MeshFactory.RoundedBox(tray, 0.05f, 2), color, 0.35f);
            plate.transform.localPosition = new Vector3(0f, tray.y / 2f, 0f);
        }

        public Transform Root { get; }

        /// <summary>The pieces lying on the tray now.</summary>
        public IReadOnlyList<MoneyPiece> Shown => _shown;

        /// <summary>The tray's size: room for a row of <see cref="MaxPieces"/> stacks.</summary>
        public static Vector3 TraySize => new Vector3(
            MaxPieces * Slot - Gap + 2f * TrayMargin,
            TrayThickness,
            MoneyBundle.Width(MoneyPiece.Stack) * PieceScale + 2f * TrayMargin);

        /// <summary>The room one piece takes in the row: a stack's length and the gap.</summary>
        private static float Slot => MoneyBundle.Length(MoneyPiece.Stack) * PieceScale + Gap;

        /// <summary>Where money leaves the pile and arrives in it: just over the tray's middle (world space).</summary>
        public Vector3 Top => Root.TransformPoint(new Vector3(0f, TrayThickness + MoneyBundle.Height(MoneyPiece.Stack) * PieceScale, 0f));

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
        /// Where the <paramref name="index"/>th of <paramref name="count"/> pieces lies on the tray, in the tray's frame (x
        /// along the board's edge): side by side in one row with even gaps, the row centred on the tray.
        /// </summary>
        public static Vector3 SlotOffset(int index, int count) => new Vector3((index - (count - 1) / 2f) * Slot, TrayThickness, 0f);

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
