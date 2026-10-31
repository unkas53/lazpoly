using System.Collections;
using Lazpoly.Presentation.Models;
using Lazpoly.Presentation.Tabletop;
using UnityEngine;

namespace Lazpoly.Presentation.Pawns
{
    /// <summary>A player's piece: a turned pawn shape built in code until the Blender pawn model arrives.</summary>
    public sealed class PawnView : MonoBehaviour
    {
        private const float StepSeconds = 0.18f;
        private const float HopHeight = 0.35f;

        /// <summary>Pawn outline as (radius, height) from the base up; turned around the vertical axis.</summary>
        private static readonly Vector2[] Profile =
        {
            new Vector2(0f, 0f), new Vector2(0.17f, 0f), new Vector2(0.17f, 0.04f), new Vector2(0.13f, 0.07f),
            new Vector2(0.08f, 0.13f), new Vector2(0.06f, 0.30f), new Vector2(0.10f, 0.34f), new Vector2(0.05f, 0.37f),
            new Vector2(0.07f, 0.395f), new Vector2(0.09f, 0.44f), new Vector2(0.085f, 0.49f), new Vector2(0.06f, 0.53f),
            new Vector2(0f, 0.55f),
        };

        private BoardView _board;
        private Vector3 _offset;
        private int _square;

        /// <summary>Animation speed multiplier from the settings (1 = normal).</summary>
        public float Speed { get; set; } = 1f;

        public static PawnView Create(Transform parent, BoardView board, int seatIndex, int seatCount, Color color)
        {
            GameObject body = ModelParts.Create($"Pawn {seatIndex + 1}", parent, MeshFactory.Lathe("pawn", Profile), color, 0.55f);

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
                for (float t = 0f; t < 1f; t += Time.deltaTime * Mathf.Max(Speed, 0.1f) / StepSeconds)
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

        private Vector3 Target(int square) => _board.PawnSpot(square) + _offset;

        // Spreads up to 8 pawns on a small grid inside the tile so they never overlap.
        private static Vector3 SeatOffset(int seatIndex, int seatCount, float spacing)
        {
            int columns = seatCount <= 4 ? 2 : 3;
            int row = seatIndex / columns;
            int column = seatIndex % columns;
            int rows = (seatCount + columns - 1) / columns;
            float step = spacing * 0.18f;
            return new Vector3((column - (columns - 1) / 2f) * step, 0f, (row - (rows - 1) / 2f) * step);
        }
    }
}
