using System;
using System.Collections;
using Lazpoly.Presentation.Effects;
using Lazpoly.Presentation.Models;
using Lazpoly.Presentation.Tabletop;
using UnityEngine;

namespace Lazpoly.Presentation.Pawns
{
    /// <summary>
    /// A player's piece: the business man from <see cref="CharacterParts"/> with his jacket in the
    /// player's colour, standing on the square and facing the camera; or, when the character model is
    /// not in the build, one of the town's shapes from <see cref="PawnShapes"/> painted whole. The
    /// model is a child of the pawn so the hop's squash and stretch scale the pawn, not the model's size.
    /// </summary>
    public sealed class PawnView : MonoBehaviour
    {
        /// <summary>
        /// The character's height on the board; the shapes are about 0.55. A person is slim, so he
        /// stands taller than they did and still takes less of the square; at 0.7 his face and tablet
        /// were too small to make out at 1080p.
        /// </summary>
        public const float CharacterHeight = 0.9f;

        /// <summary>The child that holds the character or the shape.</summary>
        public const string ModelName = "Model";

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
        private Transform _model;

        /// <summary>True when the pawn is the character; false when it is one of the shapes.</summary>
        public bool IsCharacter { get; private set; }

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
            GameObject model = CharacterParts.Character(ModelName, null);
            bool character = model != null;
            var root = new GameObject($"Pawn {seatIndex + 1} {(character ? CharacterParts.Name : PawnShapes.NameOf(seatIndex))}");
            root.transform.SetParent(parent, false);
            if (character)
            {
                model.transform.SetParent(root.transform, false);
                model.transform.localScale = Vector3.one * CharacterHeight;
                model.transform.localRotation = Facing(BoardCameraRig.BaseYaw);
                CharacterParts.Paint(model, color);
            }
            else
            {
                model = ModelParts.Create(ModelName, root.transform, PawnShapes.For(seatIndex), color, 0.55f);
            }

            var pawn = root.AddComponent<PawnView>();
            pawn._model = model.transform;
            pawn.IsCharacter = character;
            pawn._board = board;
            pawn._crowd = crowd;
            pawn._playerId = playerId;
            pawn.PlaceOn(0);
            return pawn;
        }

        /// <summary>The character's turn to look at a camera at <paramref name="yaw"/>: straight at it, across the board.</summary>
        public static Quaternion Facing(float yaw) => Quaternion.LookRotation(-BoardView.ScreenUp(yaw));

        /// <summary>Turns the character to a camera at <paramref name="yaw"/> (the shapes look the same from every side).</summary>
        public void FaceCamera(float yaw)
        {
            if (IsCharacter && _model != null) _model.localRotation = Facing(yaw);
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
