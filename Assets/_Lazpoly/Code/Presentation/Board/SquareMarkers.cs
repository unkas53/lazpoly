using System.Collections.Generic;
using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// Marks on the board's squares, drawn flat on the tiles: the square a pawn is about to walk to (a
    /// bold ring in the mover's colour, shown from the moment the dice settle), and
    /// the squares the person here may click now (rings that breathe: where a ride may go, which towns
    /// may be sold for a debt). Only looks; the HUD decides what is marked.
    /// </summary>
    public sealed class SquareMarkers : MonoBehaviour
    {
        /// <summary>
        /// A ring's radius as a share of a tile's shorter side (its width along the edge): the ring stays
        /// inside its square and off its neighbours and most of the name.
        /// </summary>
        public const float RingRadius = 0.34f;

        /// <summary>The colour of the rings on the squares that can be clicked.</summary>
        public static readonly Color ChoiceColor = new Color(1f, 0.84f, 0.22f);

        private const float RingInner = 0.74f;
        private const float RingThickness = 0.035f;

        /// <summary>
        /// The target ring is wider and bolder than a choice ring. It lies flat on the tile: anything
        /// standing above the square (an arrow was tried) falls on a neighbouring square on screen,
        /// because the camera looks down at a slant.
        /// </summary>
        public const float TargetScale = 1.1f;
        private const float TargetInner = 0.55f;

        private BoardView _board;
        private float _radius;
        private GameObject _target;
        private GameObject _targetRing;
        private readonly List<GameObject> _choices = new List<GameObject>();
        private readonly List<int> _choiceSquares = new List<int>();

        /// <summary>The square marked as a pawn's destination; -1 when none is.</summary>
        public int Target { get; private set; } = -1;

        /// <summary>The squares marked as choices, in the order given.</summary>
        public IReadOnlyList<int> Choices => _choiceSquares;

        public static SquareMarkers Create(BoardView board)
        {
            var root = new GameObject("Square markers");
            root.transform.SetParent(board.transform, false);
            var markers = root.AddComponent<SquareMarkers>();
            markers._board = board;
            markers._radius = Mathf.Min(board.Layout.Width, board.Layout.Depth) * RingRadius;
            return markers;
        }

        /// <summary>Marks where a pawn will stop, in its player's colour; a new target replaces the old one.</summary>
        public void ShowTarget(int square, Color color)
        {
            if (_target == null)
            {
                _target = new GameObject("Target");
                _target.transform.SetParent(transform, false);
                _targetRing = ModelParts.Create("Target ring", _target.transform, MeshFactory.Ring(TargetInner, RingThickness * 1.5f), color, 0.4f);
            }

            ModelParts.Paint(_targetRing, color, 0.4f);
            Place(_target, square);
            _target.SetActive(true);
            Target = square;
        }

        public void HideTarget()
        {
            if (_target != null) _target.SetActive(false);
            Target = -1;
        }

        /// <summary>Rings the squares that may be clicked now; null or empty takes the rings away.</summary>
        public void ShowChoices(ICollection<int> squares)
        {
            _choiceSquares.Clear();
            if (squares != null) _choiceSquares.AddRange(squares);
            while (_choices.Count < _choiceSquares.Count) _choices.Add(Ring("Choice ring", ChoiceColor));
            for (int i = 0; i < _choices.Count; i++)
            {
                bool used = i < _choiceSquares.Count;
                if (used) Place(_choices[i], _choiceSquares[i]);
                _choices[i].SetActive(used);
            }
        }

        private void Update()
        {
            float time = Time.unscaledTime;
            if (_target != null && _target.activeSelf)
            {
                float size = _radius * TargetScale * (1f + 0.05f * Mathf.Sin(time * 5f));
                _targetRing.transform.localScale = new Vector3(size, 1f, size);
            }

            float breath = 1f + 0.06f * Mathf.Sin(time * 3.5f);
            foreach (GameObject ring in _choices)
            {
                if (ring.activeSelf) ring.transform.localScale = new Vector3(_radius * breath, 1f, _radius * breath);
            }
        }

        private GameObject Ring(string name, Color color)
        {
            GameObject ring = ModelParts.Create(name, transform, MeshFactory.Ring(RingInner, RingThickness), color, 0.4f);
            ring.transform.localScale = new Vector3(_radius, 1f, _radius);
            ring.SetActive(false);
            return ring;
        }

        /// <summary>On the square's pawn spot, just over the tile so it never flickers with it.</summary>
        private void Place(GameObject ring, int square)
        {
            Vector3 spot = transform.InverseTransformPoint(_board.PawnSpot(square));
            ring.transform.localPosition = spot + Vector3.up * 0.01f;
        }
    }
}
