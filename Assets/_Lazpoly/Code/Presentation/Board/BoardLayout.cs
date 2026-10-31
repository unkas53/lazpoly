using System;
using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// Places squares evenly around the edge of a square board, starting at the near-right
    /// corner and running clockwise when seen from above (right to left along the near edge).
    /// </summary>
    public sealed class BoardLayout
    {
        public BoardLayout(int squareCount, float halfSize)
        {
            if (squareCount < 4) throw new ArgumentOutOfRangeException(nameof(squareCount));
            if (halfSize <= 0f) throw new ArgumentOutOfRangeException(nameof(halfSize));

            SquareCount = squareCount;
            HalfSize = halfSize;
        }

        public int SquareCount { get; }
        public float HalfSize { get; }

        /// <summary>Distance between neighbouring square centres along the edge.</summary>
        public float Spacing => 8f * HalfSize / SquareCount;

        public Vector3 SquareCenter(int index)
        {
            int wrapped = ((index % SquareCount) + SquareCount) % SquareCount;
            float side = 2f * HalfSize;
            float distance = wrapped * Spacing;
            float h = HalfSize;

            if (distance < side) return new Vector3(h - distance, 0f, -h);
            distance -= side;
            if (distance < side) return new Vector3(-h, 0f, -h + distance);
            distance -= side;
            if (distance < side) return new Vector3(-h + distance, 0f, h);
            distance -= side;
            return new Vector3(h, 0f, h - distance);
        }
    }
}
