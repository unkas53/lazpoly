using Lazpoly.Presentation.Models;
using Lazpoly.Presentation.Tabletop;
using UnityEngine;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// One piece of paid money on its way, the owner's wish (Faz 25): a payment travels as the owner's banded money stacks
    /// and, for the big sums, gold bars (<see cref="MoneyBundle"/>). The pieces leave the payer's money case exactly as they
    /// lie in it, at its size and turned its way, and never grow or turn on the way (the owner's note). One after another they
    /// go to the payer's hatch at their corner of the table (<see cref="MoneyHatch"/>), which opens, and sink into it; the
    /// hatch shuts. Money for the bank ends there. Money for another player comes up through that player's hatch, which opens
    /// in turn, and goes on to their case, turned as their case is; their hatch shuts behind it. The game waits for it all.
    /// The timeline is pure (<see cref="PositionAt"/>, <see cref="Seconds"/>) so it can be checked without a scene.
    /// </summary>
    public sealed class MoneyFlight : MonoBehaviour
    {
        /// <summary>
        /// A gold bar for every full <see cref="GoldValue"/> (at most <see cref="MaxGold"/>), a money stack for every hundred
        /// started of the rest; at least one piece and at most <see cref="MaxPieces"/>.
        /// </summary>
        public const int GoldValue = 500;
        public const int MaxGold = 2;
        public const int BundleValue = 100;
        public const int MaxPieces = 4;

        /// <summary>From the case to over the hatch (and from over the other hatch to the other case), at normal speed.</summary>
        public const float TravelSeconds = 0.8f;

        /// <summary>Going down into a hatch or coming up out of one.</summary>
        public const float DropSeconds = 0.35f;

        /// <summary>Each piece sets off this long after the one before it, so they go into the hatch one by one.</summary>
        public const float Stagger = 0.15f;

        /// <summary>The short wait under the table between the payer's hatch shutting and the other one opening.</summary>
        public const float PassSeconds = 0.15f;

        /// <summary>How high over the hatch a piece comes before it goes down, how deep it goes, and how high its way arcs.</summary>
        public const float Hover = 0.35f;
        public const float SinkDepth = 0.6f;
        public const float Arc = 0.5f;

        private Vector3 _from;
        private Vector3 _overA;
        private Vector3 _underA;
        private Vector3 _underB;
        private Vector3 _overB;
        private Vector3 _to;
        private Quaternion _restA;
        private Quaternion _restB;
        private Vector3 _size;
        private int _index;
        private int _count;
        private bool _toPlayer;
        private float _speed;
        private float _age;

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

        /// <summary>When the <paramref name="index"/>th piece is over the payer's hatch and starts down.</summary>
        public static float SinkAt(int index) => index * Stagger + TravelSeconds;

        /// <summary>When it is gone under the table.</summary>
        public static float GoneAt(int index) => SinkAt(index) + DropSeconds;

        /// <summary>The payer's hatch is open just as the first piece gets there, and shuts once the last is gone.</summary>
        public static float PayerOpensAt => TravelSeconds - MoneyHatch.SwingSeconds;
        public static float PayerShutsAt(int count) => GoneAt(count - 1);

        /// <summary>The other player's hatch opens after a short pass under the table.</summary>
        public static float ReceiverOpensAt(int count) => PayerShutsAt(count) + PassSeconds;

        /// <summary>When the <paramref name="index"/>th piece starts up out of the other hatch, is over it, and lands in the case.</summary>
        public static float RiseAt(int index, int count) => ReceiverOpensAt(count) + MoneyHatch.SwingSeconds + index * Stagger;
        public static float UpAt(int index, int count) => RiseAt(index, count) + DropSeconds;
        public static float LandAt(int index, int count) => UpAt(index, count) + TravelSeconds;

        /// <summary>The other hatch shuts once the last piece is out of it.</summary>
        public static float ReceiverShutsAt(int count) => UpAt(count - 1, count);

        /// <summary>The whole payment of <paramref name="count"/> pieces at normal speed: to the bank it ends as the payer's hatch shuts.</summary>
        public static float Seconds(int count, bool toPlayer) =>
            count <= 0 ? 0f : toPlayer ? LandAt(count - 1, count) : PayerShutsAt(count) + MoneyHatch.SwingSeconds;

        /// <summary>Whether the piece is under the table at <paramref name="t"/>, between going down one hatch and coming up the other (or for good).</summary>
        public static bool Hidden(float t, int index, int count, bool toPlayer) =>
            t >= GoneAt(index) && (!toPlayer || t < RiseAt(index, count));

        /// <summary>
        /// Where the <paramref name="index"/>th of <paramref name="count"/> pieces is <paramref name="t"/> seconds in (normal
        /// speed): from its place in the payer's case <paramref name="from"/> in an arc to <paramref name="overA"/> over the
        /// payer's hatch, straight down to <paramref name="underA"/>; then, for a player, from <paramref name="underB"/> straight
        /// up to <paramref name="overB"/> over their hatch and in an arc to its place in their case <paramref name="to"/>.
        /// </summary>
        public static Vector3 PositionAt(float t, int index, int count, bool toPlayer,
            Vector3 from, Vector3 overA, Vector3 underA, Vector3 underB, Vector3 overB, Vector3 to)
        {
            float start = index * Stagger;
            if (t <= start) return from;
            if (t < SinkAt(index)) return Arced(from, overA, (t - start) / TravelSeconds);
            if (t < GoneAt(index)) return Vector3.Lerp(overA, underA, Ease.InOutCubic((t - SinkAt(index)) / DropSeconds));
            if (!toPlayer || t < RiseAt(index, count)) return underA;
            if (t < UpAt(index, count)) return Vector3.Lerp(underB, overB, Ease.InOutCubic((t - RiseAt(index, count)) / DropSeconds));
            if (t < LandAt(index, count)) return Arced(overB, to, (t - UpAt(index, count)) / TravelSeconds);
            return to;
        }

        private static Vector3 Arced(Vector3 a, Vector3 b, float share)
        {
            float s = Mathf.Clamp01(share);
            return Vector3.Lerp(a, b, Ease.InOutCubic(s)) + Vector3.up * (Arc * Mathf.Sin(Mathf.PI * s));
        }

        /// <summary>
        /// Starts a payment of <paramref name="amount"/> out of <paramref name="payer"/>'s case down through
        /// <paramref name="payerHatch"/>; for a player, up through <paramref name="receiverHatch"/> into
        /// <paramref name="receiver"/>'s case (both null for the bank). Returns how long it takes at
        /// <paramref name="speed"/>, for the game to wait on.
        /// </summary>
        public static float Fly(Transform parent, int amount, MoneyPile payer, MoneyHatch payerHatch,
            MoneyPile receiver, MoneyHatch receiverHatch, float speed)
        {
            MoneyPiece[] pieces = Pieces(amount);
            if (pieces.Length == 0 || payer == null || payerHatch == null) return 0f;
            bool toPlayer = receiver != null && receiverHatch != null;
            speed = Mathf.Max(speed, 0.1f);
            int count = pieces.Length;
            payerHatch.Swing(PayerOpensAt, PayerShutsAt(count), speed);
            if (toPlayer) receiverHatch.Swing(ReceiverOpensAt(count), ReceiverShutsAt(count), speed);

            for (int i = 0; i < count; i++)
            {
                GameObject piece = MoneyBundle.Create(pieces[i], $"Money {pieces[i]}", parent);
                var flight = piece.AddComponent<MoneyFlight>();
                flight._index = i;
                flight._count = count;
                flight._toPlayer = toPlayer;
                flight._speed = speed;
                flight._from = payer.Root.TransformPoint(MoneyPile.SlotOffset(i, count));
                flight._overA = payerHatch.Mouth + Vector3.up * Hover;
                flight._underA = payerHatch.Mouth - Vector3.up * SinkDepth;
                flight._restA = payer.Root.rotation;
                flight._restB = toPlayer ? receiver.Root.rotation : payer.Root.rotation;
                if (toPlayer)
                {
                    flight._underB = receiverHatch.Mouth - Vector3.up * SinkDepth;
                    flight._overB = receiverHatch.Mouth + Vector3.up * Hover;
                    flight._to = receiver.Root.TransformPoint(MoneyPile.SlotOffset(i, count));
                }

                // Its size in the case, whatever the parent's scale.
                float scale = MoneyPile.PieceScale * payer.Root.lossyScale.x / Mathf.Max(parent.lossyScale.x, 1e-4f);
                flight._size = Vector3.one * scale;
                piece.transform.localScale = flight._size;
                piece.transform.position = flight._from;
                piece.transform.rotation = flight._restA;
            }

            return Seconds(count, toPlayer) / speed;
        }

        /// <summary>
        /// The bank hands a player <paramref name="amount"/> at the game's start (the owner's wish): it comes up through the
        /// player's hatch, which opens for it, and goes into their case, the second half of a payment. Returns how long it
        /// takes at <paramref name="speed"/>.
        /// </summary>
        public static float Deal(Transform parent, int amount, MoneyPile receiver, MoneyHatch hatch, float speed)
        {
            MoneyPiece[] pieces = Pieces(amount);
            if (pieces.Length == 0 || receiver == null || hatch == null) return 0f;
            speed = Mathf.Max(speed, 0.1f);
            int count = pieces.Length;
            float start = DealStartsAt(count);
            hatch.Swing(ReceiverOpensAt(count) - start, ReceiverShutsAt(count) - start, speed);
            for (int i = 0; i < count; i++)
            {
                GameObject piece = MoneyBundle.Create(pieces[i], $"Money {pieces[i]}", parent);
                var flight = piece.AddComponent<MoneyFlight>();
                flight._index = i;
                flight._count = count;
                flight._toPlayer = true;
                flight._speed = speed;
                flight._age = start;
                flight._underB = hatch.Mouth - Vector3.up * SinkDepth;
                flight._underA = flight._underB;
                flight._overB = hatch.Mouth + Vector3.up * Hover;
                flight._to = receiver.Root.TransformPoint(MoneyPile.SlotOffset(i, count));
                flight._restA = receiver.Root.rotation;
                flight._restB = receiver.Root.rotation;
                flight._size = Vector3.one * (MoneyPile.PieceScale * receiver.Root.lossyScale.x / Mathf.Max(parent.lossyScale.x, 1e-4f));
                piece.transform.position = flight._underB;
                piece.transform.rotation = flight._restB;
                piece.transform.localScale = Vector3.zero;
            }

            return (Seconds(count, true) - start) / speed;
        }

        /// <summary>Where a deal starts on a payment's timeline: every piece already under the table, the hatch about to open.</summary>
        public static float DealStartsAt(int count) => ReceiverOpensAt(count);

        private void Update()
        {
            _age += Time.deltaTime * _speed;
            if (_age >= Seconds(_count, _toPlayer))
            {
                Destroy(gameObject);
                return;
            }

            transform.position = PositionAt(_age, _index, _count, _toPlayer, _from, _overA, _underA, _underB, _overB, _to);
            transform.rotation = _toPlayer && _age >= RiseAt(_index, _count) ? _restB : _restA;
            transform.localScale = Hidden(_age, _index, _count, _toPlayer) ? Vector3.zero : _size;
        }
    }
}
