using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// The timeline of the fireworks over a trade agreement's towns (the owner's wish, Faz 27): each town sends up
    /// <see cref="RocketsPerTown"/> rockets, the towns a little after each other, every rocket climbing for
    /// <see cref="RiseSeconds"/> and bursting into sparks that fall and fade for <see cref="BurstSeconds"/>. The board
    /// shows the towns' new rents once <see cref="Seconds"/> are over. Pure, so it is tested without a scene.
    /// </summary>
    public static class FireworkShow
    {
        public const int RocketsPerTown = 2;
        public const float TownGap = 0.18f;
        public const float RocketGap = 0.45f;
        public const float RiseSeconds = 0.5f;
        public const float BurstSeconds = 1.2f;
        public const int SparksPerBurst = 36;

        /// <summary>How high a rocket bursts over its town, in board units (the board is about 16 across); the camera keeps little room above the far side.</summary>
        public const float BurstHeight = 1.6f;

        /// <summary>How far a rocket drifts towards the board's middle on its way up, so the far side's bursts stay on screen.</summary>
        public const float InwardDrift = 0.7f;

        /// <summary>A spark's launch speed, how fast the air slows it, and how fast it falls: a burst about two units across.</summary>
        public const float SparkSpeed = 2.6f;
        public const float SparkDrag = 2.8f;
        public const float SparkGravity = 1.2f;

        /// <summary>When rocket <paramref name="rocket"/> of the <paramref name="town"/>-th town leaves its tile.</summary>
        public static float LaunchAt(int town, int rocket) => Math.Max(0, town) * TownGap + Math.Max(0, rocket) * RocketGap;

        /// <summary>When that rocket bursts.</summary>
        public static float BurstAt(int town, int rocket) => LaunchAt(town, rocket) + RiseSeconds;

        /// <summary>How long the whole show lasts for <paramref name="towns"/> towns: until the last sparks are gone; 0 for none.</summary>
        public static float Seconds(int towns) =>
            towns <= 0 ? 0f : LaunchAt(towns - 1, RocketsPerTown - 1) + RiseSeconds + BurstSeconds;

        /// <summary>A rocket's height over its tile <paramref name="t"/> seconds after launch: fast at first, slowing to the burst.</summary>
        public static float RocketHeight(float t) => BurstHeight * Ease.OutCubic(Mathf.Clamp01(t / RiseSeconds));

        /// <summary>
        /// Where a rocket is <paramref name="t"/> seconds after leaving <paramref name="tile"/>, <paramref name="inward"/> being the
        /// flat direction to the board's middle: up, drifting inwards.
        /// </summary>
        public static Vector3 RocketPosition(Vector3 tile, Vector3 inward, float t)
        {
            float rise = Mathf.Clamp01(t / RiseSeconds);
            return tile + Vector3.up * RocketHeight(t) + inward * (InwardDrift * rise);
        }

        /// <summary>
        /// Where a spark is <paramref name="age"/> seconds after its burst, from the burst point: out along
        /// <paramref name="direction"/> at <paramref name="speed"/>, slowed by the air, and falling.
        /// </summary>
        public static Vector3 SparkOffset(Vector3 direction, float speed, float age)
        {
            age = Mathf.Max(0f, age);
            float travelled = speed * (1f - Mathf.Exp(-SparkDrag * age)) / SparkDrag;
            return direction * travelled + Vector3.down * (0.5f * SparkGravity * age * age);
        }

        /// <summary>Each rocket's own burst size, so the bursts over a group are not all alike.</summary>
        public static float BurstSpeed(int town, int rocket) => SparkSpeed * (0.85f + 0.3f * ((town * 3 + rocket * 7) % 5) / 4f);

        /// <summary>
        /// The size of a spark <paramref name="age"/> seconds after its burst, from 1 down to 0 at <see cref="BurstSeconds"/>:
        /// whole for the first part, then shrinking away, with a twinkle near the end.
        /// </summary>
        public static float SparkSize(float age, int spark)
        {
            if (age < 0f || age >= BurstSeconds) return 0f;
            float t = age / BurstSeconds;
            float size = t < 0.45f ? 1f : 1f - Ease.InCubic((t - 0.45f) / 0.55f);
            if (t > 0.6f && ((int)(age * 18f) + spark) % 3 == 0) size *= 0.45f; // The twinkle of dying sparks.
            return Mathf.Clamp01(size);
        }

        /// <summary>
        /// <paramref name="count"/> directions spread evenly over a ball (a golden-angle spiral), tilted a little upwards
        /// so a burst reads as a round flower from the table's high camera.
        /// </summary>
        public static List<Vector3> SparkDirections(int count)
        {
            var directions = new List<Vector3>(Math.Max(0, count));
            float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < count; i++)
            {
                float y = count == 1 ? 1f : 1f - 2f * i / (count - 1);
                float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                float angle = golden * i;
                directions.Add(new Vector3(Mathf.Cos(angle) * ring, y * 0.85f + 0.15f, Mathf.Sin(angle) * ring).normalized);
            }

            return directions;
        }
    }
}
