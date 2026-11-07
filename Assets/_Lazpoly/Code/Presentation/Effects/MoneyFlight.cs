using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// One piece of paid money (the owner's 1 L coin or 100 L note, <see cref="MoneyModels"/>) on its way, the owner's wish
    /// (Faz 25): it rises out of the payer's figure, arcs to the middle of the table, waits there with the rest of the
    /// payment, then arcs on to whoever gets it (a figure, or the bank's safe) and sinks into it. The whole payment takes
    /// <see cref="Seconds"/> at normal speed, and the game waits for it. The timeline is pure (<see cref="PositionAt"/>,
    /// <see cref="ScaleAt"/>) so it can be checked without a scene.
    /// </summary>
    public sealed class MoneyFlight : MonoBehaviour
    {
        /// <summary>The whole payment, from the first piece leaving to the last one gone, at normal speed.</summary>
        public const float Seconds = 5f;

        /// <summary>When the pieces reach the middle, and when the first leaves it again; each next piece starts and leaves <see cref="Stagger"/> later.</summary>
        public const float GatherAt = 1.8f;
        public const float LeaveAt = 2.6f;
        public const float Stagger = 0.05f;

        /// <summary>How long the way from the middle to whoever gets the money takes, and the shrink into them at the end.</summary>
        public const float LegSeconds = 1.7f;
        public const float SinkSeconds = 0.3f;

        /// <summary>How long a piece takes to grow to its size as it leaves the payer.</summary>
        public const float PopSeconds = 0.2f;

        /// <summary>How high each leg arcs over the straight line, in board units.</summary>
        public const float Arc = 1.1f;

        /// <summary>Notes for the hundreds (at most <see cref="MaxNotes"/>), coins for the rest (one per <see cref="CoinValue"/>, at most <see cref="MaxCoins"/>).</summary>
        public const int NoteValue = 100;
        public const int CoinValue = 25;
        public const int MaxNotes = 5;
        public const int MaxCoins = 3;

        /// <summary>A piece's size on the table in board units: the coin across, the note along.</summary>
        public const float CoinWidth = 0.45f;
        public const float NoteLength = 1.0f;

        /// <summary>How far apart the pieces wait in the middle, along the screen, and how much higher each next one lies.</summary>
        public const float SpreadStep = 0.22f;
        public const float LayerStep = 0.05f;

        private static readonly Color DrawnGold = new Color(0.93f, 0.72f, 0.26f);

        private Vector3 _from;
        private Vector3 _middle;
        private Vector3 _to;
        private Quaternion _rest;
        private int _index;
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
        /// Starts a payment of <paramref name="amount"/> from <paramref name="from"/> through <paramref name="middle"/> to
        /// <paramref name="to"/>; the pieces lie turned by <paramref name="rest"/> while they wait (their lettering to the
        /// camera). Returns how long it takes at <paramref name="speed"/>, for the game to wait on.
        /// </summary>
        public static float Fly(Transform parent, Vector3 from, Vector3 middle, Vector3 to, int amount, Quaternion rest, float speed)
        {
            (int notes, int coins) = Pieces(amount);
            int count = notes + coins;
            Vector3 along = rest * Vector3.right;
            for (int i = 0; i < count; i++)
            {
                MoneyKind kind = i < notes ? MoneyKind.Note : MoneyKind.Coin;
                Vector3 spread = along * ((i - (count - 1) / 2f) * SpreadStep) + Vector3.up * (i * LayerStep);
                GameObject piece = Create(kind, parent);
                var flight = piece.AddComponent<MoneyFlight>();
                flight._from = from;
                flight._middle = middle + spread;
                flight._to = to;
                flight._rest = rest * Quaternion.Euler(0f, (i % 2 == 0 ? 1f : -1f) * 8f * i, 0f);
                flight._index = i;
                flight._speed = Mathf.Max(speed, 0.1f);
                flight._size = piece.transform.localScale;
                piece.transform.position = from;
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
                return piece;
            }

            GameObject coin = ModelParts.Create("Money Coin", parent, MeshFactory.Disc(0.12f), DrawnGold, 0.6f);
            coin.transform.localScale = new Vector3(CoinWidth / 2f, 0.5f, CoinWidth / 2f);
            return coin;
        }

        /// <summary>When the <paramref name="index"/>th piece leaves the payer, leaves the middle and reaches whoever gets it, at normal speed.</summary>
        public static float DepartAt(int index) => index * Stagger;
        public static float LeaveMiddleAt(int index) => LeaveAt + index * Stagger;
        public static float ArriveAt(int index) => LeaveMiddleAt(index) + LegSeconds;

        /// <summary>Where the <paramref name="index"/>th piece is <paramref name="t"/> seconds into the payment (normal speed).</summary>
        public static Vector3 PositionAt(float t, int index, Vector3 from, Vector3 middle, Vector3 to)
        {
            float depart = DepartAt(index);
            if (t <= depart) return from;
            if (t < GatherAt)
            {
                float u = (t - depart) / (GatherAt - depart);
                return Vector3.Lerp(from, middle, Ease.InOutCubic(u)) + Vector3.up * (Arc * Ease.Bump(u));
            }

            float leave = LeaveMiddleAt(index);
            if (t < leave) return middle + Vector3.up * (0.06f * Mathf.Sin((t - GatherAt) * 6f + index));
            float arrive = ArriveAt(index);
            if (t >= arrive) return to;
            float v = (t - leave) / LegSeconds;
            return Vector3.Lerp(middle, to, Ease.InOutCubic(v)) + Vector3.up * (Arc * Ease.Bump(v));
        }

        /// <summary>The <paramref name="index"/>th piece's size as a share of its own: it pops up as it leaves and shrinks into whoever gets it.</summary>
        public static float ScaleAt(float t, int index)
        {
            float depart = DepartAt(index);
            if (t <= depart) return 0f;
            float grow = Mathf.Clamp01((t - depart) / PopSeconds);
            float sink = 1f - Mathf.Clamp01((t - ArriveAt(index)) / SinkSeconds);
            return Mathf.Min(grow, sink);
        }

        private void Update()
        {
            _age += Time.deltaTime * _speed;
            if (_age >= ArriveAt(_index) + SinkSeconds)
            {
                Destroy(gameObject);
                return;
            }

            transform.position = PositionAt(_age, _index, _from, _middle, _to);
            transform.localScale = _size * ScaleAt(_age, _index);
            transform.rotation = RotationAt(_age);
        }

        /// <summary>Tumbling on the way, lying still with a slow sway while it waits in the middle.</summary>
        private Quaternion RotationAt(float t)
        {
            float leave = LeaveMiddleAt(_index);
            if (t >= GatherAt && t < leave) return _rest * Quaternion.Euler(0f, 10f * Mathf.Sin((t - GatherAt) * 3f + _index), 0f);
            float u = t < GatherAt ? (t - DepartAt(_index)) / (GatherAt - DepartAt(_index)) : (t - leave) / LegSeconds;
            u = Mathf.Clamp01(u);
            // One full turn over the leg, so the piece lands flat and the wait starts from its resting turn.
            return _rest * Quaternion.Euler(360f * Ease.InOutCubic(u), 0f, 25f * Ease.Bump(u));
        }
    }
}
