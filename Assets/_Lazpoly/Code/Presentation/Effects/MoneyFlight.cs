using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// One piece of paid money (the owner's 1 L coin or 100 L note, <see cref="MoneyModels"/>) on its way, the owner's wish
    /// (Faz 25): a payment travels as one bundle, the notes in a stack (a deste) with the coins piled beside it. It comes
    /// out of the payer's figure, goes straight to the middle of the table, waits there a moment, then goes straight on to
    /// whoever gets it (a figure, or the bank's safe) and sinks into it. It lies flat and never turns on the way. The whole payment takes
    /// <see cref="Seconds"/> at normal speed, and the game waits for it. The timeline and the stack are pure
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

        /// <summary>Notes for the hundreds (at most <see cref="MaxNotes"/> in the stack), coins for the rest (one per <see cref="CoinValue"/>, at most <see cref="MaxCoins"/>).</summary>
        public const int NoteValue = 100;
        public const int CoinValue = 25;
        public const int MaxNotes = 8;
        public const int MaxCoins = 4;

        /// <summary>A piece's size on the table in board units: the coin across, the note along.</summary>
        public const float CoinWidth = 0.8f;
        public const float NoteLength = 1.8f;

        /// <summary>How much higher each next note and coin lies in its stack, a little over its own thickness.</summary>
        public const float NoteLayer = 0.035f;
        public const float CoinLayer = 0.1f;

        /// <summary>The notes are squared up loosely: each lies up to this far off the stack's line, in board units.</summary>
        public const float Jitter = 0.03f;

        private static readonly Color DrawnGold = new Color(0.93f, 0.72f, 0.26f);

        private Vector3 _from;
        private Vector3 _middle;
        private Vector3 _to;
        private Quaternion _rest;
        private float _speed;
        private float _age;
        private Vector3 _size;

        /// <summary>How many notes and coins show a payment of <paramref name="amount"/>: at least one piece.</summary>
        public static (int Notes, int Coins) Pieces(int amount)
        {
            if (amount <= 0) return (0, 0);
            int notes = Mathf.Min(amount / NoteValue, MaxNotes);
            int rest = amount % NoteValue; // Past the notes' limit the hundreds stay notes; coins show only the odd part.
            int coins = rest > 0 ? Mathf.Clamp((rest + CoinValue - 1) / CoinValue, 1, MaxCoins) : 0;
            return (notes, coins);
        }

        /// <summary>
        /// Where the <paramref name="index"/>th piece lies in the bundle, in the bundle's own frame (x along the notes, y up):
        /// the notes stacked on each other, each a little off the line, and the coins in a pile at the stack's right end; the
        /// whole bundle centred on its middle.
        /// </summary>
        public static Vector3 StackOffset(int index, int notes, int coins)
        {
            float coinColumn = notes > 0 && coins > 0 ? NoteLength / 2f + CoinWidth * 0.6f : 0f;
            float centre = coinColumn / 2f;
            if (index < notes)
            {
                float x = ((index * 37) % 7 - 3) / 3f * Jitter;
                float z = ((index * 53) % 5 - 2) / 2f * Jitter;
                return new Vector3(x - centre, index * NoteLayer, z);
            }

            int coin = index - notes;
            return new Vector3(coinColumn - centre, coin * CoinLayer, 0f);
        }

        /// <summary>
        /// Starts a payment of <paramref name="amount"/> from <paramref name="from"/> through <paramref name="middle"/> to
        /// <paramref name="to"/> as one bundle, lying turned by <paramref name="rest"/> (its lettering to the camera).
        /// Returns how long it takes at <paramref name="speed"/>, for the game to wait on.
        /// </summary>
        public static float Fly(Transform parent, Vector3 from, Vector3 middle, Vector3 to, int amount, Quaternion rest, float speed)
        {
            (int notes, int coins) = Pieces(amount);
            for (int i = 0; i < notes + coins; i++)
            {
                GameObject piece = Create(i < notes ? MoneyKind.Note : MoneyKind.Coin, parent);
                Vector3 offset = rest * StackOffset(i, notes, coins);
                var flight = piece.AddComponent<MoneyFlight>();
                flight._from = from + offset;
                flight._middle = middle + offset;
                flight._to = to + offset;
                flight._rest = rest * Quaternion.Euler(0f, i < notes ? ((i * 29) % 5 - 2) * 1.5f : 0f, 0f);
                flight._speed = Mathf.Max(speed, 0.1f);
                flight._size = piece.transform.localScale;
                piece.transform.position = flight._from;
                piece.transform.rotation = flight._rest;
                piece.transform.localScale = Vector3.zero;
            }

            return Seconds / Mathf.Max(speed, 0.1f);
        }

        /// <summary>The owner's piece at its size on the table, or a drawn gold coin when the money set is missing.</summary>
        private static GameObject Create(MoneyKind kind, Transform parent)
        {
            GameObject piece = MoneyModels.Create(kind, $"Money {kind}", parent);
            if (piece != null)
            {
                float size = (kind == MoneyKind.Coin ? CoinWidth : NoteLength) / MoneyModels.Length(kind);
                piece.transform.localScale = Vector3.one * size;
                // Flying money casts no shadow: a lighter frame while it is in the air.
                foreach (Renderer part in piece.GetComponentsInChildren<Renderer>()) part.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                return piece;
            }

            GameObject coin = ModelParts.Create("Money Coin", parent, MeshFactory.Disc(0.12f), DrawnGold, 0.6f);
            coin.transform.localScale = new Vector3(CoinWidth / 2f, 0.5f, CoinWidth / 2f);
            return coin;
        }

        /// <summary>Where a piece of the bundle is <paramref name="t"/> seconds into the payment (normal speed): straight lines, a stop in the middle.</summary>
        public static Vector3 PositionAt(float t, Vector3 from, Vector3 middle, Vector3 to)
        {
            if (t <= 0f) return from;
            if (t < GatherAt) return Vector3.Lerp(from, middle, Ease.InOutCubic(t / GatherAt));
            if (t < LeaveAt) return middle;
            if (t >= ArriveAt) return to;
            return Vector3.Lerp(middle, to, Ease.InOutCubic((t - LeaveAt) / LegSeconds));
        }

        /// <summary>The bundle's size as a share of its own: it pops up as it leaves and shrinks into whoever gets it.</summary>
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
