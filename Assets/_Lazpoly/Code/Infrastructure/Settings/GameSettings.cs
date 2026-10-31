using System;

namespace Lazpoly.Infra.Settings
{
    /// <summary>How long the computer waits between its moves, so people can follow them.</summary>
    public enum AiSpeed
    {
        Slow = 0,
        Normal = 1,
        Fast = 2,
    }

    /// <summary>The player's choices, kept between sessions. Immutable; change with the <c>With</c> methods.</summary>
    public sealed class GameSettings
    {
        public static readonly GameSettings Default = new GameSettings(0.8f, fullscreen: true, vSync: true, AiSpeed.Normal, fastAnimations: false);

        public GameSettings(float masterVolume, bool fullscreen, bool vSync, AiSpeed aiSpeed, bool fastAnimations)
        {
            MasterVolume = float.IsNaN(masterVolume) ? Default.MasterVolume : Math.Max(0f, Math.Min(1f, masterVolume));
            Fullscreen = fullscreen;
            VSync = vSync;
            AiSpeed = Enum.IsDefined(typeof(AiSpeed), aiSpeed) ? aiSpeed : AiSpeed.Normal;
            FastAnimations = fastAnimations;
        }

        /// <summary>0 (silent) to 1 (full).</summary>
        public float MasterVolume { get; }

        public bool Fullscreen { get; }
        public bool VSync { get; }
        public AiSpeed AiSpeed { get; }

        /// <summary>Doubles the speed of pawn walks.</summary>
        public bool FastAnimations { get; }

        /// <summary>Pause before each computer move.</summary>
        public float AiMoveDelaySeconds => AiSpeed == AiSpeed.Slow ? 1.2f : AiSpeed == AiSpeed.Fast ? 0.2f : 0.6f;

        /// <summary>Multiplier for animation playback speed.</summary>
        public float AnimationSpeed => FastAnimations ? 2f : 1f;

        public GameSettings WithMasterVolume(float value) => new GameSettings(value, Fullscreen, VSync, AiSpeed, FastAnimations);
        public GameSettings WithFullscreen(bool value) => new GameSettings(MasterVolume, value, VSync, AiSpeed, FastAnimations);
        public GameSettings WithVSync(bool value) => new GameSettings(MasterVolume, Fullscreen, value, AiSpeed, FastAnimations);
        public GameSettings WithAiSpeed(AiSpeed value) => new GameSettings(MasterVolume, Fullscreen, VSync, value, FastAnimations);
        public GameSettings WithFastAnimations(bool value) => new GameSettings(MasterVolume, Fullscreen, VSync, AiSpeed, value);

        public bool SameAs(GameSettings other) =>
            other != null && MasterVolume.Equals(other.MasterVolume) && Fullscreen == other.Fullscreen && VSync == other.VSync &&
            AiSpeed == other.AiSpeed && FastAnimations == other.FastAnimations;
    }
}
