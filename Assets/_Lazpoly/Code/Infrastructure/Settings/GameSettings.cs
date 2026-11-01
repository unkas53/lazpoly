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

    /// <summary>How much the picture costs: Low drops post-processing and shadows for weak machines.</summary>
    public enum GraphicsQuality
    {
        Low = 0,
        Medium = 1,
        High = 2,
    }

    /// <summary>How big the interface is drawn: Large for a far-away screen or weak eyes, Small for more board.</summary>
    public enum UiScale
    {
        Small = 0,
        Normal = 1,
        Large = 2,
    }

    /// <summary>Interface language; Auto follows the system language.</summary>
    public enum Language
    {
        Auto = 0,
        Turkish = 1,
        English = 2,
    }

    /// <summary>The player's choices, kept between sessions. Immutable; change with the <c>With</c> methods.</summary>
    public sealed class GameSettings
    {
        private const float DefaultMaster = 0.8f;
        private const float DefaultEffects = 1f;
        private const float DefaultMusic = 0.6f;

        public static readonly GameSettings Default = new GameSettings(DefaultMaster, DefaultEffects, DefaultMusic, fullscreen: true, vSync: true,
            AiSpeed.Normal, fastAnimations: false, GraphicsQuality.High, screenWidth: 0, screenHeight: 0, Language.Auto, UiScale.Normal, dayCycle: true);

        public GameSettings(float masterVolume, float effectsVolume, float musicVolume, bool fullscreen, bool vSync, AiSpeed aiSpeed,
            bool fastAnimations, GraphicsQuality quality, int screenWidth, int screenHeight, Language language, UiScale uiScale, bool dayCycle = true)
        {
            DayCycle = dayCycle;
            Language = Enum.IsDefined(typeof(Language), language) ? language : Language.Auto;
            UiScale = Enum.IsDefined(typeof(UiScale), uiScale) ? uiScale : UiScale.Normal;
            MasterVolume = Volume(masterVolume, DefaultMaster);
            EffectsVolume = Volume(effectsVolume, DefaultEffects);
            MusicVolume = Volume(musicVolume, DefaultMusic);
            Fullscreen = fullscreen;
            VSync = vSync;
            AiSpeed = Enum.IsDefined(typeof(AiSpeed), aiSpeed) ? aiSpeed : AiSpeed.Normal;
            FastAnimations = fastAnimations;
            Quality = Enum.IsDefined(typeof(GraphicsQuality), quality) ? quality : GraphicsQuality.High;
            // A size that makes no sense means "whatever the screen is".
            bool sized = screenWidth >= 640 && screenHeight >= 480;
            ScreenWidth = sized ? screenWidth : 0;
            ScreenHeight = sized ? screenHeight : 0;
        }

        /// <summary>Everything: 0 (silent) to 1 (full).</summary>
        public float MasterVolume { get; }

        /// <summary>Sound effects, under the master volume.</summary>
        public float EffectsVolume { get; }

        /// <summary>Background music, under the master volume.</summary>
        public float MusicVolume { get; }

        public bool Fullscreen { get; }
        public bool VSync { get; }
        public AiSpeed AiSpeed { get; }

        /// <summary>Doubles the speed of pawn walks.</summary>
        public bool FastAnimations { get; }

        public GraphicsQuality Quality { get; }

        public Language Language { get; }

        public UiScale UiScale { get; }

        /// <summary>The light over the board slides from noon to evening as the rounds go by; off keeps it at noon.</summary>
        public bool DayCycle { get; }

        /// <summary>How much bigger than the 1920x1080 reference the interface is drawn.</summary>
        public float UiScaleFactor => UiScale == UiScale.Small ? 0.88f : UiScale == UiScale.Large ? 1.18f : 1f;

        /// <summary>Chosen window size in pixels; 0 keeps the screen's own size.</summary>
        public int ScreenWidth { get; }

        public int ScreenHeight { get; }

        /// <summary>True when the player picked a size instead of leaving the screen as it is.</summary>
        public bool HasScreenSize => ScreenWidth > 0 && ScreenHeight > 0;

        /// <summary>Pause before each computer move.</summary>
        public float AiMoveDelaySeconds => AiSpeed == AiSpeed.Slow ? 1.2f : AiSpeed == AiSpeed.Fast ? 0.2f : 0.6f;

        /// <summary>Multiplier for animation playback speed.</summary>
        public float AnimationSpeed => FastAnimations ? 2f : 1f;

        public GameSettings WithMasterVolume(float value) => Copy(master: value);
        public GameSettings WithEffectsVolume(float value) => Copy(effects: value);
        public GameSettings WithMusicVolume(float value) => Copy(music: value);
        public GameSettings WithFullscreen(bool value) => Copy(fullscreen: value);
        public GameSettings WithVSync(bool value) => Copy(vSync: value);
        public GameSettings WithAiSpeed(AiSpeed value) => Copy(aiSpeed: value);
        public GameSettings WithFastAnimations(bool value) => Copy(fastAnimations: value);
        public GameSettings WithQuality(GraphicsQuality value) => Copy(quality: value);
        public GameSettings WithScreenSize(int width, int height) => Copy(width: width, height: height);
        public GameSettings WithLanguage(Language value) => Copy(language: value);
        public GameSettings WithUiScale(UiScale value) => Copy(uiScale: value);
        public GameSettings WithDayCycle(bool value) => Copy(dayCycle: value);

        public bool SameAs(GameSettings other) =>
            other != null && MasterVolume.Equals(other.MasterVolume) && EffectsVolume.Equals(other.EffectsVolume) &&
            MusicVolume.Equals(other.MusicVolume) && Fullscreen == other.Fullscreen && VSync == other.VSync &&
            AiSpeed == other.AiSpeed && FastAnimations == other.FastAnimations && Quality == other.Quality &&
            ScreenWidth == other.ScreenWidth && ScreenHeight == other.ScreenHeight && Language == other.Language && UiScale == other.UiScale &&
            DayCycle == other.DayCycle;

        private GameSettings Copy(float? master = null, float? effects = null, float? music = null, bool? fullscreen = null, bool? vSync = null,
            AiSpeed? aiSpeed = null, bool? fastAnimations = null, GraphicsQuality? quality = null, int? width = null, int? height = null,
            Language? language = null, UiScale? uiScale = null, bool? dayCycle = null) =>
            new GameSettings(master ?? MasterVolume, effects ?? EffectsVolume, music ?? MusicVolume, fullscreen ?? Fullscreen, vSync ?? VSync,
                aiSpeed ?? AiSpeed, fastAnimations ?? FastAnimations, quality ?? Quality, width ?? ScreenWidth, height ?? ScreenHeight,
                language ?? Language, uiScale ?? UiScale, dayCycle ?? DayCycle);

        private static float Volume(float value, float fallback) => float.IsNaN(value) ? fallback : Math.Max(0f, Math.Min(1f, value));
    }
}
