using System;
using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// Where the camera must sit so the whole board fills the screen area between the HUD's side
    /// panels: as close as it can while every corner stays visible, and with the board centred
    /// vertically (the near edge looks wider than the far edge, so aiming at the centre would waste space).
    /// </summary>
    public static class BoardCameraFraming
    {
        /// <summary>Fraction of the free area the board may fill.</summary>
        public const float Fill = 0.96f;

        private const int Iterations = 60;

        /// <param name="boardCenter">World position of the board's centre.</param>
        /// <param name="boardHalfExtent">Half the board's width (it is square and flat).</param>
        /// <param name="verticalFovDegrees">Camera vertical field of view.</param>
        /// <param name="pitchDegrees">Camera tilt below the horizon; the camera does not turn sideways.</param>
        /// <param name="screenWidth">Screen width in pixels.</param>
        /// <param name="screenHeight">Screen height in pixels.</param>
        /// <param name="reservedSidePixels">Pixels covered by panels on each side.</param>
        public static Vector3 CameraPosition(Vector3 boardCenter, float boardHalfExtent, float verticalFovDegrees, float pitchDegrees,
            float screenWidth, float screenHeight, float reservedSidePixels)
        {
            if (screenWidth <= 0f || screenHeight <= 0f) throw new ArgumentOutOfRangeException(nameof(screenWidth));
            if (boardHalfExtent <= 0f) throw new ArgumentOutOfRangeException(nameof(boardHalfExtent));

            float pitch = pitchDegrees * Mathf.Deg2Rad;
            var forward = new Vector3(0f, -Mathf.Sin(pitch), Mathf.Cos(pitch));
            float tanV = Mathf.Tan(verticalFovDegrees * Mathf.Deg2Rad / 2f);
            float tanH = tanV * (screenWidth / screenHeight) * FreeFraction(screenWidth, reservedSidePixels);

            float distance = boardHalfExtent * 3f;
            float targetZ = 0f;
            for (int i = 0; i < Iterations; i++)
            {
                Vector3 camera = boardCenter + new Vector3(0f, 0f, targetZ) - forward * distance;
                Measure(camera, boardCenter, boardHalfExtent, pitch, tanH, tanV, out float maxX, out float minY, out float maxY);

                // Centre vertically: moving the aim point along z shifts every corner up or down.
                float middle = (minY + maxY) / 2f;
                targetZ += middle * distance * tanV / Mathf.Sin(pitch) * 0.8f;

                // On-screen size shrinks roughly in proportion to distance.
                float extent = Mathf.Max(maxX, (maxY - minY) / 2f);
                distance *= 1f + (extent / Fill - 1f) * 0.8f;
            }

            return boardCenter + new Vector3(0f, 0f, targetZ) - forward * distance;
        }

        /// <summary>
        /// Where the board's corners land on screen, in the camera's normalised view: x is -1..1
        /// across the free area between the panels and y is -1..1 bottom to top.
        /// </summary>
        public static void Measure(Vector3 camera, Vector3 boardCenter, float halfExtent, float pitch, float tanH, float tanV,
            out float maxAbsX, out float minY, out float maxY)
        {
            var forward = new Vector3(0f, -Mathf.Sin(pitch), Mathf.Cos(pitch));
            var up = new Vector3(0f, Mathf.Cos(pitch), Mathf.Sin(pitch));
            maxAbsX = 0f;
            minY = float.MaxValue;
            maxY = float.MinValue;
            for (int corner = 0; corner < 4; corner++)
            {
                var point = boardCenter + new Vector3(corner % 2 == 0 ? -halfExtent : halfExtent, 0f, corner < 2 ? -halfExtent : halfExtent);
                Vector3 relative = point - camera;
                float depth = Vector3.Dot(relative, forward);
                maxAbsX = Mathf.Max(maxAbsX, Mathf.Abs(relative.x / depth / tanH));
                float y = Vector3.Dot(relative, up) / depth / tanV;
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }
        }

        public static float FreeFraction(float screenWidth, float reservedSidePixels) =>
            Mathf.Max(0.2f, 1f - 2f * reservedSidePixels / screenWidth);
    }
}
