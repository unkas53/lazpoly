using System;
using System.Collections;
using Lazpoly.Presentation.Effects;
using Lazpoly.Presentation.Models;
using Lazpoly.Presentation.Tabletop;
using UnityEngine;

namespace Lazpoly.Presentation.Pawns
{
    /// <summary>A player's piece: one of the town's shapes from <see cref="PawnShapes"/>, in the player's colour.</summary>
    public sealed class PawnView : MonoBehaviour
    {
        private const float StepSeconds = 0.18f;
        private const float HopHeight = 0.35f;
        private const float SquashSeconds = 0.07f;
        private const float ToppleSeconds = 0.55f;

        /// <summary>A walk never takes longer than this at normal speed; long moves hop faster instead.</summary>
        public const float MaxWalkSeconds = 2f;

        private BoardView _board;
        private PawnCrowd _crowd;
        private int _playerId;
        private int _slot;
        private int _square;

        /// <summary>Animation speed multiplier from the settings (1 = normal).</summary>
        public float Speed { get; set; } = 1f;

        /// <summary>Raised each time the pawn lands on a square while walking; the scene plays a step sound.</summary>
        public event Action<int> Stepped;

        /// <summary>Raised with the spot the pawn leaves at the start of every hop; the scene leaves a trail there.</summary>
        public event Action<Vector3> Departed;

        /// <summary>The square the pawn stands on (or is walking from).</summary>
        public int Square => _square;

        /// <param name="crowd">Shared by every pawn on the board, so two never take the same slot of a square.</param>
        /// <param name="playerId">The pawn's key in <paramref name="crowd"/>.</param>
        public static PawnView Create(Transform parent, BoardView board, int seatIndex, Color color, PawnCrowd crowd, int playerId)
        {
            if (crowd == null) throw new ArgumentNullException(nameof(crowd));
            GameObject body = ModelParts.Create($"Pawn {seatIndex + 1} {PawnShapes.NameOf(seatIndex)}", parent, PawnShapes.For(seatIndex), color, 0.55f);

            var pawn = body.AddComponent<PawnView>();
            pawn._board = board;
            pawn._crowd = crowd;
            pawn._playerId = playerId;
            pawn.PlaceOn(0);
            return pawn;
        }

        public void PlaceOn(int square)
        {
            transform.position = Reserve(square);
            _square = square;
        }

        /// <summary>Takes a slot on <paramref name="square"/> and says where the pawn stands there.</summary>
        private Vector3 Reserve(int square)
        {
            _slot = _crowd.Place(_playerId, square);
            return Target(square);
        }

        /// <summary>Walks square by square from the current square to <paramref name="destination"/>, forward or back.</summary>
        public IEnumerator WalkTo(int destination, bool backward = false)
        {
            int count = _board.Layout.SquareCount;
            int steps = backward ? (_square - destination + count) % count : (destination - _square + count) % count;
            float speed = Mathf.Max(Speed, 0.1f) * Mathf.Max(1f, steps * (StepSeconds + SquashSeconds) / MaxWalkSeconds);
            while (_square != destination)
            {
                int next = (_square + (backward ? count - 1 : 1)) % count;
                Vector3 from = transform.position;
                Departed?.Invoke(from);
                Vector3 to = Reserve(next);
                for (float t = 0f; t < 1f; t += Time.deltaTime * speed / StepSeconds)
                {
                    Vector3 position = Vector3.Lerp(from, to, t);
                    position.y += Mathf.Sin(t * Mathf.PI) * HopHeight;
                    transform.position = position;
                    // Stretches a little in the air.
                    SetStretch(1f + 0.08f * Ease.Bump(t));
                    yield return null;
                }

                transform.position = to;
                _square = next;
                Stepped?.Invoke(next);
                // A short squash on landing, back to shape before the next hop.
                for (float t = 0f; t < 1f; t += Time.deltaTime * speed / SquashSeconds)
                {
                    SetStretch(1f - 0.16f * Ease.Bump(t));
                    yield return null;
                }

                SetStretch(1f);
            }
        }

        /// <summary>One high arc straight to <paramref name="destination"/> (a ride from a travel square).</summary>
        public IEnumerator JumpTo(int destination)
        {
            const float seconds = 0.8f;
            float speed = Mathf.Max(Speed, 0.1f);
            Vector3 from = transform.position;
            Departed?.Invoke(from);
            Vector3 to = Reserve(destination);
            for (float t = 0f; t < 1f; t += Time.deltaTime * speed / seconds)
            {
                Vector3 position = Vector3.Lerp(from, to, Ease.OutCubic(t));
                position.y += Mathf.Sin(t * Mathf.PI) * HopHeight * 5f;
                transform.position = position;
                SetStretch(1f + 0.12f * Ease.Bump(t));
                yield return null;
            }

            transform.position = to;
            _square = destination;
            SetStretch(1f);
            Stepped?.Invoke(destination);
        }

        /// <summary>A small hop in place, to say "this one is on turn".</summary>
        public IEnumerator Bob()
        {
            if (!gameObject.activeSelf) yield break;
            float speed = Mathf.Max(Speed, 0.1f);
            Vector3 rest = Target(_square);
            for (float t = 0f; t < 1f; t += Time.deltaTime * speed / 0.32f)
            {
                transform.position = rest + Vector3.up * (Mathf.Sin(t * Mathf.PI) * HopHeight * 0.6f);
                SetStretch(1f + 0.1f * Ease.Bump(t));
                yield return null;
            }

            transform.position = rest;
            SetStretch(1f);
        }

        /// <summary>Hides a bankrupt player's pawn at once (used when the board is rebuilt from the state).</summary>
        public void SetEliminated()
        {
            _crowd.Remove(_playerId);
            gameObject.SetActive(false);
        }

        /// <summary>A bankrupt pawn tips over, sinks into the table and disappears.</summary>
        public IEnumerator Topple()
        {
            if (!gameObject.activeSelf) yield break;
            float speed = Mathf.Max(Speed, 0.1f);
            Vector3 start = transform.position;
            Vector3 axis = Vector3.Cross(Vector3.up, (start - _board.transform.position).normalized);
            if (axis.sqrMagnitude < 0.01f) axis = Vector3.right;
            Quaternion upright = transform.rotation;
            for (float t = 0f; t < 1f; t += Time.deltaTime * speed / ToppleSeconds)
            {
                transform.rotation = Quaternion.AngleAxis(88f * Ease.InCubic(t), axis) * upright;
                yield return null;
            }

            for (float t = 0f; t < 1f; t += Time.deltaTime * speed / (ToppleSeconds * 0.6f))
            {
                transform.position = start + Vector3.down * (0.25f * t);
                transform.localScale = Vector3.one * (1f - t);
                yield return null;
            }

            SetEliminated();
        }

        // Keeps the pawn's volume: taller and thinner, or shorter and wider.
        private void SetStretch(float height)
        {
            float width = 1f / Mathf.Sqrt(height);
            transform.localScale = new Vector3(width, height, width);
        }

        // The slot grid is turned to the square: its first row lies towards the board's middle, so the pawns fill the square from the inside out.
        private Vector3 Target(int square) =>
            _board.PawnSpot(square) + Quaternion.LookRotation(_board.Layout.Outward(square)) * PawnSlots.Offset(_slot, _crowd.Capacity, _board.Layout.Width);
    }
}
