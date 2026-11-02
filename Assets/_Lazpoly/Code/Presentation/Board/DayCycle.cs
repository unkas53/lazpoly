using System;
using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>The light over the table at one moment of the day: sun, fill and ambient, and how far into the evening it is.</summary>
    public readonly struct DayLight
    {
        public DayLight(Color sunColor, float sunIntensity, float sunPitch, float sunYaw, Color fillColor, float fillIntensity, Color ambient, float evening)
        {
            SunColor = sunColor;
            SunIntensity = sunIntensity;
            SunPitch = sunPitch;
            SunYaw = sunYaw;
            FillColor = fillColor;
            FillIntensity = fillIntensity;
            Ambient = ambient;
            Evening = evening;
        }

        public Color SunColor { get; }
        public float SunIntensity { get; }

        /// <summary>Sun height above the horizon, in degrees; low in the evening, so shadows grow long.</summary>
        public float SunPitch { get; }
        public float SunYaw { get; }
        public Color FillColor { get; }
        public float FillIntensity { get; }
        public Color Ambient { get; }

        /// <summary>0 by day, 1 at dusk: the sky warms along it.</summary>
        public float Evening { get; }

        public static DayLight Lerp(in DayLight a, in DayLight b, float t) => new DayLight(
            Color.Lerp(a.SunColor, b.SunColor, t), Mathf.Lerp(a.SunIntensity, b.SunIntensity, t),
            Mathf.Lerp(a.SunPitch, b.SunPitch, t), Mathf.Lerp(a.SunYaw, b.SunYaw, t),
            Color.Lerp(a.FillColor, b.FillColor, t), Mathf.Lerp(a.FillIntensity, b.FillIntensity, t),
            Color.Lerp(a.Ambient, b.Ambient, t), Mathf.Lerp(a.Evening, b.Evening, t));
    }

    /// <summary>
    /// The day over the board follows the game: it starts at noon and slides into evening as the
    /// rounds go by, reaching dusk at the round limit (or after <see cref="RoundsToEvening"/> rounds
    /// when there is none) and at the end of the game. Pure arithmetic; the scene applies the result.
    /// </summary>
    public static class DayCycle
    {
        /// <summary>Rounds from noon to dusk when the table has no round limit (a typical game ends sooner).</summary>
        public const int RoundsToEvening = 24;

        public static readonly DayLight Noon = new DayLight(
            new Color(1f, 0.93f, 0.82f), 1.25f, 52f, -35f, new Color(0.55f, 0.70f, 0.90f), 0.35f, new Color(0.42f, 0.44f, 0.50f), 0f);

        public static readonly DayLight Dusk = new DayLight(
            new Color(1f, 0.62f, 0.38f), 0.95f, 21f, -78f, new Color(0.32f, 0.42f, 0.74f), 0.5f, new Color(0.34f, 0.28f, 0.36f), 1f);

        /// <summary>How far the day has gone, 0 (noon) to 1 (dusk).</summary>
        public static float Progress(int round, int roundLimit, bool gameOver)
        {
            if (gameOver) return 1f;
            int span = Math.Max((roundLimit > 0 ? roundLimit : RoundsToEvening) - 1, 1);
            return Mathf.Clamp01((round - 1) / (float)span);
        }

        /// <summary>The light at <paramref name="progress"/>; the evening itself only sets in during the second half.</summary>
        public static DayLight At(float progress)
        {
            float t = Mathf.Clamp01(progress);
            DayLight light = DayLight.Lerp(Noon, Dusk, t);
            float evening = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, t));
            return new DayLight(light.SunColor, light.SunIntensity, light.SunPitch, light.SunYaw, light.FillColor, light.FillIntensity, light.Ambient, evening);
        }
    }
}
