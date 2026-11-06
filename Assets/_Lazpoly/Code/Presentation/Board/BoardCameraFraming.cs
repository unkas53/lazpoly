using System;
using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// How large the orthographic view must be so the whole board plate fills the screen area between
    /// the HUD's side panels and above its bottom panel. Seen corner-on the square board is a diamond;
    /// its width is the diagonal and its height the diagonal foreshortened by the camera's pitch, plus
    /// the plate's edge below it. When the bottom panel is up the view also aims lower, so the board
    /// sits in the middle of what is left above the panel.
    /// </summary>
    public static class BoardCameraFraming
    {
        /// <summary>Fraction of the free area the board may fill; the owner had the camera stand a little further back (Faz 22, was 0.94).</summary>
        public const float Fill = 0.88f;

        /// <summary>Room kept above the far corner for what stands on it (a corner scene, a pawn with its bubble), in world units.</summary>
        public const float Headroom = 0.8f;

        /// <param name="boardHalfExtent">Half the plate's width (it is square).</param>
        /// <param name="plateThickness">How far the plate hangs below the board's surface.</param>
        /// <param name="pitchDegrees">Camera tilt below the horizon.</param>
        /// <param name="screenWidth">Screen width in pixels.</param>
        /// <param name="screenHeight">Screen height in pixels.</param>
        /// <param name="reservedSidePixels">Pixels covered by panels on each side.</param>
        /// <param name="reservedBottomPixels">Pixels covered by the panel along the bottom.</param>
        /// <returns>The camera's orthographic size: half the view's height in world units.</returns>
        public static float OrthographicSize(float boardHalfExtent, float plateThickness, float pitchDegrees,
            float screenWidth, float screenHeight, float reservedSidePixels, float reservedBottomPixels = 0f)
        {
            if (screenWidth <= 0f || screenHeight <= 0f) throw new ArgumentOutOfRangeException(nameof(screenWidth));
            if (boardHalfExtent <= 0f) throw new ArgumentOutOfRangeException(nameof(boardHalfExtent));

            float pitch = pitchDegrees * Mathf.Deg2Rad;
            float halfDiagonal = boardHalfExtent * Mathf.Sqrt(2f);
            // Top to bottom on the screen: the headroom over the far corner, the diamond, the plate's edge under the near corner.
            float halfHeight = halfDiagonal * Mathf.Sin(pitch) + (plateThickness + Headroom) * Mathf.Cos(pitch) / 2f;
            float freeAspect = FreeAspect(screenWidth, screenHeight, reservedSidePixels, reservedBottomPixels);
            float freeHeight = FreeHeightFraction(screenHeight, reservedBottomPixels);
            return Mathf.Max(halfHeight, halfDiagonal / freeAspect) / (freeHeight * Fill);
        }

        /// <summary>Where the camera looks: between the plate's edge below the surface and the headroom above it, so both count in the centring.</summary>
        public static Vector3 Aim(Vector3 boardCenter, float plateThickness) => boardCenter + Vector3.up * ((Headroom - plateThickness) / 2f);

        /// <summary>
        /// Where the camera looks when a panel covers the bottom of the screen: below the board's aim by
        /// half the panel, so the board is centred in the free area above it. Moving the aim down the
        /// camera's own up direction moves the picture straight up the screen.
        /// </summary>
        public static Vector3 AimAbovePanel(Vector3 aim, float pitchDegrees, float yawDegrees, float orthographicSize, float screenHeight, float reservedBottomPixels)
        {
            if (reservedBottomPixels <= 0f || screenHeight <= 0f) return aim;
            Vector3 up = Quaternion.Euler(pitchDegrees, yawDegrees, 0f) * Vector3.up;
            // The view is 2 * size tall over screenHeight pixels; the free area's middle is half the panel above the screen's.
            return aim - up * (orthographicSize * reservedBottomPixels / screenHeight);
        }

        /// <summary>Width over height of the free area between the panels and above the bottom one.</summary>
        public static float FreeAspect(float screenWidth, float screenHeight, float reservedSidePixels, float reservedBottomPixels = 0f) =>
            screenWidth / screenHeight * FreeFraction(screenWidth, reservedSidePixels) / FreeHeightFraction(screenHeight, reservedBottomPixels);

        public static float FreeFraction(float screenWidth, float reservedSidePixels) =>
            Mathf.Max(0.2f, 1f - 2f * reservedSidePixels / screenWidth);

        /// <summary>The share of the screen's height above the bottom panel.</summary>
        public static float FreeHeightFraction(float screenHeight, float reservedBottomPixels) =>
            Mathf.Max(0.2f, 1f - reservedBottomPixels / screenHeight);

        /// <summary>
        /// Where a world point lands in the orthographic view: x is -1..1 across a free width of
        /// <paramref name="freeAspect"/> times the view's half height, and y is -1..1 over the whole screen,
        /// bottom to top. Depth plays no part, which is what makes the view isometric.
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
