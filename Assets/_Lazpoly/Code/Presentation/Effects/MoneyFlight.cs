using System.Collections.Generic;
using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// One piece of paid money on its way, the owner's wish (Faz 25): a payment travels as the owner's banded money stacks
    /// and, for the big sums, gold bars (<see cref="MoneyBundle"/>), all side by side in one neat row. It leaves the
    /// payer's money at the table's edge (MoneyPile), goes straight to the middle of the table, waits
    /// there a moment, then goes straight on to whoever gets it (their money at the edge, or the bank's safe) and sinks
    /// into it. It lies flat and never turns on the way. The whole payment takes <see cref="Seconds"/> at normal speed,
    /// and the game waits for it. The timeline and the row are pure (<see cref="PositionAt"/>, <see cref="ScaleAt"/>,
    /// <see cref="RowOffset"/>) so they can be checked without a scene.
    /// </summary>
    public sealed class MoneyFlight : MonoBehaviour
    {
        /// <summary>The whole payment, from the bundle leaving to it being gone, at normal speed (the owner's three seconds).</summary>
        public const float Seconds = 3f;

        /// <summary>When the bundle reaches the middle and when it leaves it again, at normal speed.</summary>
        public const float GatherAt = 1.0f;
        public const float LeaveAt = 1.5f;

        /// <summary>How long the way from the middle to whoever gets the money takes, and the shrink into them at the end.</summary>
        public const float LegSeconds = 1.2f;
        public const float SinkSeconds = 0.3f;

        /// <summary>When the bundle reaches whoever gets it.</summary>
        public const float ArriveAt = LeaveAt + LegSeconds;

        /// <summary>How long the bundle takes to grow to its size as it leaves the payer.</summary>
        public const float PopSeconds = 0.15f;

        /// <summary>
        /// A gold bar for every full <see cref="GoldValue"/> (at most <see cref="MaxGold"/>), a money stack for every hundred
        /// started of the rest; at least one piece and at most <see cref="MaxPieces"/>.
        /// </summary>
        public const int GoldValue = 500;
        public const int MaxGold = 2;
        public const int BundleValue = 100;
        public const int MaxPieces = 4;

        /// <summary>The gap between neighbouring pieces in the row, in board units.</summary>
        public const float RowGap = 0.08f;

        private Vector3 _from;
        private Vector3 _middle;
        private Vector3 _to;
        private Quaternion _rest;
        private float _speed;
        private float _age;
        private Vector3 _size;

        /// <summary>The pieces that show a payment of <paramref name="amount"/>, the stacks first and then the gold bars: none for nothing.</summary>
        public static MoneyPiece[] Pieces(int amount)
        {
            if (amount <= 0) return new MoneyPiece[0];
            int gold = Mathf.Min(amount / GoldValue, MaxGold);
            int rest = amount - gold * GoldValue;
            int stacks = Mathf.Min((rest + BundleValue - 1) / BundleValue, MaxPieces - gold);
            if (gold + stacks == 0) stacks = 1;
            var pieces = new MoneyPiece[gold + stacks];
            for (int i = 0; i < pieces.Length; i++) pieces[i] = i < stacks ? MoneyPiece.Stack : MoneyPiece.Gold;
            return pieces;
        }

        /// <summary>
        /// Where the <paramref name="index"/>th of the <paramref name="pieces"/> lies, in the row's own frame (x along the
        /// pieces, y up, z across): all side by side in one straight row along x, squared up on one line with even gaps,
        /// the row centred on its middle (the owner's wish: neatly in a row).
        /// </summary>
        public static Vector3 RowOffset(int index, IReadOnlyList<MoneyPiece> pieces)
        {
            float total = RowLength(pieces);
            float x = -total / 2f;
            for (int i = 0; i < index; i++) x += MoneyBundle.Length(pieces[i]) + RowGap;
            return new Vector3(x + MoneyBundle.Length(pieces[index]) / 2f, 0f, 0f);
        }

        /// <summary>The whole row's length: the pieces end to end with the gaps between them.</summary>
        public static float RowLength(IReadOnlyList<MoneyPiece> pieces)
        {
            float total = 0f;
            for (int i = 0; i < pieces.Count; i++) total += MoneyBundle.Length(pieces[i]) + (i > 0 ? RowGap : 0f);
            return total;
        }

        /// <summary>
        /// Starts a payment of <paramref name="amount"/> from <paramref name="from"/> through <paramref name="middle"/> to
        /// <paramref name="to"/> as thick bundles, lying turned by <paramref name="rest"/> (its lettering to the camera).
        /// Returns how long it takes at <paramref name="speed"/>, for the game to wait on.
        /// </summary>
        public static float Fly(Transform parent, Vector3 from, Vector3 middle, Vector3 to, int amount, Quaternion rest, float speed)
        {
            MoneyPiece[] pieces = Pieces(amount);
            for (int i = 0; i < pieces.Length; i++)
            {
                GameObject piece = MoneyBundle.Create(pieces[i], $"Money {pieces[i]}", parent);
                Vector3 offset = rest * RowOffset(i, pieces);
                var flight = piece.AddComponent<MoneyFlight>();
                flight._from = from + offset;
                flight._middle = middle + offset;
                flight._to = to + offset;
                flight._rest = rest;
                flight._speed = Mathf.Max(speed, 0.1f);
                flight._size = piece.transform.localScale;
                piece.transform.position = flight._from;
                piece.transform.rotation = flight._rest;
                piece.transform.localScale = Vector3.zero;
            }

            return Seconds / Mathf.Max(speed, 0.1f);
        }

        /// <summary>Where a bundle is <paramref name="t"/> seconds into the payment (normal speed): straight lines, a stop in the middle.</summary>
        public static Vector3 PositionAt(float t, Vector3 from, Vector3 middle, Vector3 to)
        {
            if (t <= 0f) return from;
            if (t < GatherAt) return Vector3.Lerp(from, middle, Ease.InOutCubic(t / GatherAt));
            if (t < LeaveAt) return middle;
            if (t >= ArriveAt) return to;
            return Vector3.Lerp(middle, to, Ease.InOutCubic((t - LeaveAt) / LegSeconds));
        }

        /// <summary>A bundle's size as a share of its own: it pops up as it leaves and shrinks into whoever gets it.</summary>
        public static float ScaleAt(float t)
        {
            if (t <= 0f) return 0f;
            float grow = Mathf.Clamp01(t / PopSeconds);
            float sink = 1f - Mathf.Clamp01((t - ArriveAt) / SinkSeconds);
            return Mathf.Min(grow, sink);
        }

        private void Update()
        {
            _age += Time.deltaTime * _speed;
            if (_age >= ArriveAt + SinkSeconds)
            {
                Destroy(gameObject);
                return;
            }

            transform.position = PositionAt(_age, _from, _middle, _to);
            transform.localScale = _size * ScaleAt(_age);
            transform.rotation = _rest;
        }
    }
}
