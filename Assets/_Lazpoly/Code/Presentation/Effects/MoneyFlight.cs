using System.Collections.Generic;
using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// One bundle of paid money on its way, the owner's wish (Faz 25): a payment travels as the owner's banded money stacks and,
    /// for the big sums, gold bars (<see cref="MoneyBundle"/>), two side by side and piled on each other, the gold at the
    /// bottom. It comes out of the payer's
    /// figure, goes straight to the middle of the table, waits there a moment, then goes straight on to whoever gets it (a
    /// figure, or the bank's safe) and sinks into it. It lies flat and never turns on the way. The whole payment takes
    /// <see cref="Seconds"/> at normal speed, and the game waits for it. The timeline and the pile are pure
    /// (<see cref="PositionAt"/>, <see cref="ScaleAt"/>, <see cref="StackOffset"/>) so they can be checked without a scene.
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

        /// <summary>How many bundles lie side by side in a layer of the pile, and the gaps between them and between the layers.</summary>
        public const int PerLayer = 2;
        public const float SideGap = 0.06f;
        public const float LayerGap = 0.01f;

        /// <summary>The bundles are squared up loosely: each lies up to this far off the pile's line, in board units.</summary>
        public const float Jitter = 0.04f;

        private Vector3 _from;
        private Vector3 _middle;
        private Vector3 _to;
        private Quaternion _rest;
        private float _speed;
        private float _age;
        private Vector3 _size;

        /// <summary>The pieces that show a payment of <paramref name="amount"/>, the gold bars first: none for nothing.</summary>
        public static MoneyPiece[] Pieces(int amount)
        {
            if (amount <= 0) return new MoneyPiece[0];
            int gold = Mathf.Min(amount / GoldValue, MaxGold);
            int rest = amount - gold * GoldValue;
            int stacks = Mathf.Min((rest + BundleValue - 1) / BundleValue, MaxPieces - gold);
            if (gold + stacks == 0) stacks = 1;
            var pieces = new MoneyPiece[gold + stacks];
            for (int i = 0; i < pieces.Length; i++) pieces[i] = i < gold ? MoneyPiece.Gold : MoneyPiece.Stack;
            return pieces;
        }

        /// <summary>
        /// Where the <paramref name="index"/>th of the <paramref name="pieces"/> lies in the pile, in the pile's own frame
        /// (x along the pieces, y up, z across): two side by side in each layer, each layer on the tallest piece of the one
        /// below, a lone piece in the middle of its layer, each a little off the line. The pile is centred across.
        /// </summary>
        public static Vector3 StackOffset(int index, IReadOnlyList<MoneyPiece> pieces)
        {
            int layer = index / PerLayer;
            float y = 0f;
            for (int below = 0; below < layer; below++) y += LayerHeight(pieces, below) + LayerGap;

            int first = layer * PerLayer;
            int inLayer = Mathf.Min(PerLayer, pieces.Count - first);
            float z = 0f;
            if (inLayer > 1)
            {
                float left = MoneyBundle.Width(pieces[first]), right = MoneyBundle.Width(pieces[first + 1]);
                float span = left / 2f + SideGap + right / 2f;
                z = index == first ? -span / 2f : span / 2f;
            }

            float x = ((index * 37) % 5 - 2) / 2f * Jitter;
            return new Vector3(x, y, z);
        }

        private static float LayerHeight(IReadOnlyList<MoneyPiece> pieces, int layer)
        {
            float tallest = 0f;
            for (int i = layer * PerLayer; i < Mathf.Min(pieces.Count, (layer + 1) * PerLayer); i++) tallest = Mathf.Max(tallest, MoneyBundle.Height(pieces[i]));
            return tallest;
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
                Vector3 offset = rest * StackOffset(i, pieces);
                var flight = piece.AddComponent<MoneyFlight>();
                flight._from = from + offset;
                flight._middle = middle + offset;
                flight._to = to + offset;
                flight._rest = rest * Quaternion.Euler(0f, ((i * 29) % 5 - 2) * 1.5f, 0f);
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
