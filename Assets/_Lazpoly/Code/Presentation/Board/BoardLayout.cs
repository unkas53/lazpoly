using System;
using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// Places squares around the edge of a square board, starting at the near-right corner and
    /// running clockwise when seen from above (right to left along the near edge). The four corner
    /// squares are square; the squares between them are <see cref="Width"/> along the edge and
    /// <see cref="Depth"/> from the inner edge to the outer, the way a printed board has them.
    /// </summary>
    public sealed class BoardLayout
    {
        /// <param name="squareCount">How many squares; a multiple of four, one in each corner.</param>
        /// <param name="halfSize">Distance from the board's centre to the line the square centres lie on.</param>
        /// <param name="depthRatio">How much deeper than wide an edge square is; 1 makes every square the same size.</param>
        public BoardLayout(int squareCount, float halfSize, float depthRatio = 1f)
        {
            if (squareCount < 4) throw new ArgumentOutOfRangeException(nameof(squareCount));
            if (halfSize <= 0f) throw new ArgumentOutOfRangeException(nameof(halfSize));
            if (depthRatio < 1f) throw new ArgumentOutOfRangeException(nameof(depthRatio), "An edge square is at least as deep as it is wide.");

            SquareCount = squareCount;
            HalfSize = halfSize;
            HasCorners = squareCount % 4 == 0;
            if (HasCorners)
            {
                PerSide = squareCount / 4 - 1;
                // A side runs from corner centre to corner centre: half a corner, the edge squares, half a corner.
                Width = 2f * halfSize / (PerSide + depthRatio);
                Depth = depthRatio * Width;
            }
            else
            {
                // A count that does not share out over four sides gets equal squares spaced evenly round the edge.
                PerSide = 0;
                Width = Depth = 8f * halfSize / squareCount;
            }
        }

        /// <summary>Whether the count shares out as four corners with the same number of squares between them.</summary>
        public bool HasCorners { get; }

        public int SquareCount { get; }
        public float HalfSize { get; }

        /// <summary>Edge squares between two corners.</summary>
        public int PerSide { get; }

        /// <summary>An edge square's size along the edge; also the pitch between neighbouring edge squares.</summary>
        public float Width { get; }

        /// <summary>An edge square's size from its inner edge to its outer edge; also a corner square's side.</summary>
        public float Depth { get; }

        /// <summary>Half the board's full width, to the squares' outer edge.</summary>
        public float OuterHalf => HalfSize + Depth / 2f;

        public bool IsCorner(int index) => HasCorners && Wrap(index) % (PerSide + 1) == 0;

        /// <summary>A square's size: x along the edge, y from the inner edge to the outer edge.</summary>
        public Vector2 SquareSize(int index) => IsCorner(index) ? new Vector2(Depth, Depth) : new Vector2(Width, Depth);

        public Vector3 SquareCenter(int index)
        {
            int wrapped = Wrap(index);
            float h = HalfSize;
            int side;
            float distance;
            if (HasCorners)
            {
                side = wrapped / (PerSide + 1);
                int step = wrapped % (PerSide + 1);
                // Along the side from its first corner: the corner itself, then the edge squares centre to centre.
                distance = step == 0 ? 0f : Depth / 2f + Width / 2f + (step - 1) * Width;
            }
            else
            {
                float along = wrapped * Width;
                side = Mathf.Min(3, (int)(along / (2f * h)));
                distance = along - side * 2f * h;
            }

            switch (side)
            {
                case 0: return new Vector3(h - distance, 0f, -h);
                case 1: return new Vector3(-h, 0f, -h + distance);
                case 2: return new Vector3(-h + distance, 0f, h);
                default: return new Vector3(h, 0f, h - distance);
            }
        }

        /// <summary>Direction from a square towards the outside of the board; a corner points along the edge it starts.</summary>
        public Vector3 Outward(int index) => Outward(SquareCenter(index));

        /// <summary>Direction from a point on the board's edge towards the outside; on a corner, towards the nearer or the z side.</summary>
        public static Vector3 Outward(Vector3 center) =>
            Mathf.Abs(center.z) >= Mathf.Abs(center.x)
                ? new Vector3(0f, 0f, Mathf.Sign(center.z))
                : new Vector3(Mathf.Sign(center.x), 0f, 0f);

        /// <summary>On a corner square, the way along its outer edge towards the board's own corner; zero on an edge square.</summary>
        public Vector3 Cornerward(int index)
        {
            if (!IsCorner(index)) return Vector3.zero;
            Vector3 center = SquareCenter(index);
            return new Vector3(Mathf.Sign(center.x), 0f, 0f);
        }

        private int Wrap(int index) => ((index % SquareCount) + SquareCount) % SquareCount;
    }
}
