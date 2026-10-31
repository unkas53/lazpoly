using System.Collections;
using Lazpoly.Presentation.Tabletop;
using UnityEngine;

namespace Lazpoly.Presentation.Pawns
{
    /// <summary>A player's piece. Placeholder capsule until the Blender pawn models arrive.</summary>
    public sealed class PawnView : MonoBehaviour
    {
        private const float StepSeconds = 0.18f;
        private const float HopHeight = 0.35f;

        private BoardView _board;
        private Vector3 _offset;
        private int _square;

        public static PawnView Create(Transform parent, BoardView board, int seatIndex, int seatCount, Color color)
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = $"Pawn {seatIndex + 1}";
            body.transform.SetParent(parent, false);
            body.transform.localScale = new Vector3(0.28f, 0.28f, 0.28f);
            Object.Destroy(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().material.color = color;

            var pawn = body.AddComponent<PawnView>();
            pawn._board = board;
            pawn._offset = SeatOffset(seatIndex, seatCount, board.Layout.Spacing);
            pawn.PlaceOn(0);
            return pawn;
        }

        public void PlaceOn(int square)
        {
            _square = square;
            transform.position = Target(square);
        }

        /// <summary>Walks square by square from the current square to <paramref name="destination"/>.</summary>
        public IEnumerator WalkTo(int destination)
        {
            int count = _board.Layout.SquareCount;
            while (_square != destination)
            {
                int next = (_square + 1) % count;
                Vector3 from = transform.position;
                Vector3 to = Target(next);
                for (float t = 0f; t < 1f; t += Time.deltaTime / StepSeconds)
                {
                    Vector3 position = Vector3.Lerp(from, to, t);
                    position.y += Mathf.Sin(t * Mathf.PI) * HopHeight;
                    transform.position = position;
                    yield return null;
                }

                PlaceOn(next);
            }
        }

        public void SetEliminated()
        {
            gameObject.SetActive(false);
        }

        private Vector3 Target(int square) => _board.SquarePosition(square) + _offset + Vector3.up * 0.28f;

        // Spreads up to 8 pawns on a small grid inside the tile so they never overlap.
        private static Vector3 SeatOffset(int seatIndex, int seatCount, float spacing)
        {
            int columns = seatCount <= 4 ? 2 : 3;
            int row = seatIndex / columns;
            int column = seatIndex % columns;
            int rows = (seatCount + columns - 1) / columns;
            float step = spacing * 0.22f;
            return new Vector3((column - (columns - 1) / 2f) * step, 0f, (row - (rows - 1) / 2f) * step);
        }
    }
}
