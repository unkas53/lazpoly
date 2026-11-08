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
    /// What the scene's lights are set to for one <see cref="DayLight"/>: the colours as Unity takes them (sRGB, it turns
    /// them linear itself in linear colour space), the intensities, and how dark the sun's shadows are.
    /// </summary>
    public readonly struct LightRig
    {
        public LightRig(Color sunColor, float sunIntensity, Color fillColor, float fillIntensity, Color ambient, float shadowStrength)
        {
            SunColor = sunColor;
            SunIntensity = sunIntensity;
            FillColor = fillColor;
            FillIntensity = fillIntensity;
            Ambient = ambient;
            ShadowStrength = shadowStrength;
        }

        public Color SunColor { get; }
        public float SunIntensity { get; }
        public Color FillColor { get; }
        public float FillIntensity { get; }
        public Color Ambient { get; }
        public float ShadowStrength { get; }
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
            new Color(1f, 0.93f, 0.82f), 1.1f, 52f, -35f, new Color(0.55f, 0.70f, 0.90f), 0.35f, new Color(0.36f, 0.38f, 0.44f), 0f);

        public static readonly DayLight Dusk = new DayLight(
            new Color(1f, 0.62f, 0.38f), 0.9f, 21f, -78f, new Color(0.32f, 0.42f, 0.74f), 0.5f, new Color(0.30f, 0.25f, 0.33f), 1f);

        /// <summary>How dark the sun's shadows were tuned in gamma colour space (the look the owner approved).</summary>
        public const float GammaShadowStrength = 0.45f;

        /// <summary>The fill light's height above the horizon, in degrees.</summary>
        public const float FillPitch = 35f;

        /// <summary>The power that turns a gamma-space value into a linear one, near enough for the sRGB curve's middle.</summary>
        public const float Gamma = 2.2f;

        /// <summary>
        /// The lights for <paramref name="light"/> in gamma colour space: the values as they were tuned, unchanged.
        /// </summary>
        public static LightRig GammaRig(in DayLight light) => new LightRig(
            light.SunColor, light.SunIntensity, light.FillColor, light.FillIntensity, light.Ambient, GammaShadowStrength);

        /// <summary>
        /// The lights for <paramref name="light"/> in linear colour space, so the table looks as it did in gamma, where the
        /// light was tuned. Gamma shades a surface with albedo times light (both as stored), linear with their linear values
        /// and turns the sum back at the end; so the same lights in linear left the board about a quarter darker and
        /// yellower. Here every light is scaled, channel by channel, so the light falling on the board's top (the tiles, the
        /// grass) is the gamma light raised to <see cref="Gamma"/>, which gives back the gamma colour exactly; the shadows
        /// are deepened the same way, so a shadow keeps the darkness it had. Linear's softer fall-off would leave the faces turned
        /// away from the sun lighter than in gamma, so part of the ambient is handed to the sun (<see cref="LinearAmbientShare"/>).
        /// </summary>
        public static LightRig LinearRig(in DayLight light)
        {
            float sunUp = Mathf.Max(Mathf.Sin(light.SunPitch * Mathf.Deg2Rad), 0f);
            float fillUp = Mathf.Sin(FillPitch * Mathf.Deg2Rad);
            Color fill = light.FillColor * light.FillIntensity;
            Color top = light.Ambient + light.SunColor * (light.SunIntensity * sunUp) + fill * fillUp;
            Color lift = new Color(Lift(top.r), Lift(top.g), Lift(top.b), 1f);

            // Part of the ambient moves into the sun, which lights the board's top just as much but the sides less.
            Color ambientLeft = light.Ambient * LinearAmbientShare;
            Color sun = light.SunColor * light.SunIntensity;
            if (sunUp > 0.05f) sun += (light.Ambient - ambientLeft) / sunUp;
            else ambientLeft = light.Ambient;

            (Color sunColor, float sunIntensity) = AsLight(Scale(sun, lift));
            (Color fillColor, float fillIntensity) = AsLight(Scale(fill, lift));
            Color ambient = Scale(ambientLeft, lift).gamma;

            // In gamma a shadow on the board's top lets through (1 - strength) of the sun: keep its share of the top's light.
            float topLuma = Luma(top);
            float gammaSunLuma = Luma(light.SunColor * light.SunIntensity) * sunUp;
            float sunLuma = Luma(sun) * sunUp;
            float shadowStrength = GammaShadowStrength;
            if (sunLuma > 0.0001f)
            {
                float gammaShadow = topLuma - gammaSunLuma * GammaShadowStrength;
                float wanted = Mathf.Pow(gammaShadow / topLuma, Gamma) * topLuma;
                shadowStrength = Mathf.Clamp01((topLuma - wanted) / sunLuma);
            }

            return new LightRig(sunColor, sunIntensity, fillColor, fillIntensity, ambient, shadowStrength);
        }

        /// <summary>
        /// How much of the ambient stays ambient in linear colour space. With all of it, faces turned from the sun (the
        /// board's sides, the safe's front) came out about a tenth lighter than in gamma; the rest goes into the sun.
        /// </summary>
        public const float LinearAmbientShare = 0.35f;

        private static float Lift(float topLight) => Mathf.Pow(Mathf.Max(topLight, 0.0001f), Gamma - 1f);

        private static Color Scale(Color color, Color by) => new Color(color.r * by.r, color.g * by.g, color.b * by.b, 1f);

        private static float Luma(Color color) => 0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b;

        /// <summary>A linear light as Unity takes it: an sRGB colour no brighter than white, and the rest as intensity.</summary>
        private static (Color color, float intensity) AsLight(Color linear)
        {
            float intensity = Mathf.Max(linear.r, Mathf.Max(linear.g, linear.b));
            if (intensity <= 0f) return (Color.black, 0f);
            return (new Color(linear.r / intensity, linear.g / intensity, linear.b / intensity, 1f).gamma, intensity);
        }

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
