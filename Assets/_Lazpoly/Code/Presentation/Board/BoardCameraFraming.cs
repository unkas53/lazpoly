using System;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// How far the camera must sit from the board centre, along its view direction, so the whole
    /// board fits between the HUD side panels and inside the screen's height.
    /// </summary>
    public static class BoardCameraFraming
    {
        private const float Margin = 1.05f;

        /// <param name="boardHalfExtent">Half the board's width (it is square).</param>
        /// <param name="verticalFovDegrees">Camera vertical field of view.</param>
        /// <param name="pitchDegrees">Camera tilt below the horizon.</param>
        /// <param name="screenWidth">Screen width in pixels.</param>
        /// <param name="screenHeight">Screen height in pixels.</param>
        /// <param name="reservedSideWidth">Pixels covered by panels on each side.</param>
        public static float Distance(float boardHalfExtent, float verticalFovDegrees, float pitchDegrees,
            float screenWidth, float screenHeight, float reservedSideWidth)
        {
            if (screenWidth <= 0f || screenHeight <= 0f) throw new ArgumentOutOfRangeException(nameof(screenWidth));

            double halfFov = Math.Tan(verticalFovDegrees * Math.PI / 360.0);
            double pitch = pitchDegrees * Math.PI / 180.0;
            double freeFraction = Math.Max(0.2, 1.0 - 2.0 * reservedSideWidth / screenWidth);
            double halfHorizontal = halfFov * (screenWidth / screenHeight) * freeFraction;

            // The board's near edge is closest to the camera, so it is the widest part on screen.
            double nearEdgeDepth = boardHalfExtent * Math.Cos(pitch);
            double forWidth = boardHalfExtent / halfHorizontal + nearEdgeDepth;
            double forHeight = boardHalfExtent * Math.Sin(pitch) / halfFov + nearEdgeDepth;
            return (float)(Math.Max(forWidth, forHeight) * Margin);
        }
    }
}
