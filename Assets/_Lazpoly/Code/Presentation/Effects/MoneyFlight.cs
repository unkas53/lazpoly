using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// One bundle of paid money on its way, the owner's wish (Faz 25): a payment travels as the owner's thick dollar stacks
    /// (<see cref="MoneyBundle"/>), two side by side and piled on each other. It comes out of the payer's
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

        /// <summary>One bundle for every hundred started, at least one and at most <see cref="MaxBundles"/>.</summary>
        public const int BundleValue = 100;
        public const int MaxBundles = 4;

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

        /// <summary>How many thick bundles show a payment of <paramref name="amount"/>: none for nothing, else one per hundred started.</summary>
        public static int Bundles(int amount)
        {
            if (amount <= 0) return 0;
            return Mathf.Clamp((amount + BundleValue - 1) / BundleValue, 1, MaxBundles);
        }

        /// <summary>
        /// Where the <paramref name="index"/>th of <paramref name="count"/> bundles lies in the pile, in the pile's own frame
        /// (x along the bundles, y up, z across): two side by side in each layer, each layer on the one below, a lone bundle
        /// in the middle of its layer, each a little off the line. The pile is centred across.
        /// </summary>
        public static Vector3 StackOffset(int index, int count)
        {
            int layer = index / PerLayer;
            int inLayer = Mathf.Min(PerLayer, count - layer * PerLayer);
            float across = MoneyBundle.Width + SideGap;
            float z = inLayer > 1 ? (index % PerLayer - (inLayer - 1) / 2f) * across : 0f;
            float x = ((index * 37) % 5 - 2) / 2f * Jitter;
            return new Vector3(x, layer * (MoneyBundle.Height + LayerGap), z);
        }

        /// <summary>
        /// Starts a payment of <paramref name="amount"/> from <paramref name="from"/> through <paramref name="middle"/> to
        /// <paramref name="to"/> as thick bundles, lying turned by <paramref name="rest"/> (its lettering to the camera).
        /// Returns how long it takes at <paramref name="speed"/>, for the game to wait on.
        /// </summary>
        public static float Fly(Transform parent, Vector3 from, Vector3 middle, Vector3 to, int amount, Quaternion rest, float speed)
        {
            int count = Bundles(amount);
            for (int i = 0; i < count; i++)
            {
                GameObject piece = MoneyBundle.Create("Money Bundle", parent);
                Vector3 offset = rest * StackOffset(i, count);
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
