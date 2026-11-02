using System;
using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// How large the orthographic view must be so the whole board plate fills the screen area between
    /// the HUD's side panels. Seen corner-on the square board is a diamond; its width is the diagonal
    /// and its height the diagonal foreshortened by the camera's pitch, plus the plate's edge below it.
    /// </summary>
    public static class BoardCameraFraming
    {
        /// <summary>Fraction of the free area the board may fill.</summary>
        public const float Fill = 0.94f;

        /// <param name="boardHalfExtent">Half the plate's width (it is square).</param>
        /// <param name="plateThickness">How far the plate hangs below the board's surface.</param>
        /// <param name="pitchDegrees">Camera tilt below the horizon.</param>
        /// <param name="screenWidth">Screen width in pixels.</param>
        /// <param name="screenHeight">Screen height in pixels.</param>
        /// <param name="reservedSidePixels">Pixels covered by panels on each side.</param>
        /// <returns>The camera's orthographic size: half the view's height in world units.</returns>
        public static float OrthographicSize(float boardHalfExtent, float plateThickness, float pitchDegrees,
            float screenWidth, float screenHeight, float reservedSidePixels)
        {
            if (screenWidth <= 0f || screenHeight <= 0f) throw new ArgumentOutOfRangeException(nameof(screenWidth));
            if (boardHalfExtent <= 0f) throw new ArgumentOutOfRangeException(nameof(boardHalfExtent));

            float pitch = pitchDegrees * Mathf.Deg2Rad;
            float halfDiagonal = boardHalfExtent * Mathf.Sqrt(2f);
            float halfHeight = halfDiagonal * Mathf.Sin(pitch) + plateThickness * Mathf.Cos(pitch) / 2f;
            float freeAspect = FreeAspect(screenWidth, screenHeight, reservedSidePixels);
            return Mathf.Max(halfHeight, halfDiagonal / freeAspect) / Fill;
        }

        /// <summary>Where the camera looks: a little below the board's surface, so the plate's edge counts in the centring.</summary>
        public static Vector3 Aim(Vector3 boardCenter, float plateThickness) => boardCenter - Vector3.up * (plateThickness / 2f);

        /// <summary>Width over height of the free area between the panels.</summary>
        public static float FreeAspect(float screenWidth, float screenHeight, float reservedSidePixels) =>
            screenWidth / screenHeight * FreeFraction(screenWidth, reservedSidePixels);

        public static float FreeFraction(float screenWidth, float reservedSidePixels) =>
            Mathf.Max(0.2f, 1f - 2f * reservedSidePixels / screenWidth);

        /// <summary>
        /// Where a world point lands in the orthographic view: x is -1..1 across the free area between
        /// the panels and y is -1..1 bottom to top. Depth plays no part, which is what makes the view isometric.
        /// </summary>
        public static Vector2 Project(Vector3 point, Vector3 aim, float pitchDegrees, float yawDegrees, float orthographicSize, float freeAspect)
        {
            Quaternion look = Quaternion.Euler(pitchDegrees, yawDegrees, 0f);
            Vector3 relative = point - aim;
            return new Vector2(
                Vector3.Dot(relative, look * Vector3.right) / (orthographicSize * freeAspect),
                Vector3.Dot(relative, look * Vector3.up) / orthographicSize);
        }
    }
}
