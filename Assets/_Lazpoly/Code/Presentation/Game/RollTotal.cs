using Lazpoly.Presentation.Effects;
using UnityEngine;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Game
{
    /// <summary>
    /// The roll's total in large figures in the middle of the screen once the dice settle, with "ÇİFT" under it for a
    /// double (the owner's wish, Faz 28; the lawn no longer prints the two dice). It pops in, stays and fades over
    /// <see cref="Seconds"/>, and the table waits that long before the pawn walks. Pure, so it is tested without a scene.
    /// </summary>
    public static class RollTotal
    {
        /// <summary>How long the total stays up at normal speed; the table waits the same.</summary>
        public const float Seconds = 1.1f;

        public const float PopSeconds = 0.25f;
        public const float FadeInSeconds = 0.1f;
        public const float FadeOutSeconds = 0.3f;

        /// <summary>The figures for a roll: "7"; nothing before the first roll.</summary>
        public static string Text(int die1, int die2) => die1 <= 0 || die2 <= 0 ? "" : (die1 + die2).ToString();

        /// <summary>The word under the figures: "ÇİFT" for a double, else nothing.</summary>
        public static string Note(int die1, int die2) => die1 > 0 && die1 == die2 ? L("ÇİFT") : null;

        /// <summary>Whether the total is still up <paramref name="age"/> seconds after it went up, for a total that lasts <paramref name="seconds"/>.</summary>
        public static bool Showing(float age, float seconds = Seconds) => age >= 0f && age < seconds;

        /// <summary>How opaque the total is: a quick fade in, fully there, then fading over the last <see cref="FadeOutSeconds"/>.</summary>
        public static float Alpha(float age, float seconds = Seconds)
        {
            if (!Showing(age, seconds)) return 0f;
            float fadeOut = Mathf.Min(FadeOutSeconds, seconds / 3f);
            return Mathf.Clamp01(age / FadeInSeconds) * Mathf.Clamp01((seconds - age) / fadeOut);
        }

        /// <summary>Its size: popping up from 0.6 with a little overshoot, then whole.</summary>
        public static float Scale(float age) => Mathf.LerpUnclamped(0.6f, 1f, Ease.OutBack(Mathf.Clamp01(age / PopSeconds)));
    }
}
