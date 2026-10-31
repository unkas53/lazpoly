using System;
using UnityEngine;

namespace Lazpoly.Infra.Settings
{
    /// <summary>Loads the settings at start, applies them to the engine and saves every change.</summary>
    public sealed class SettingsService : IGameService
    {
        private readonly ISettingsStorage _storage;

        public SettingsService(ISettingsStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public GameSettings Current { get; private set; } = GameSettings.Default;

        public event Action<GameSettings> Changed;

        public void Initialize()
        {
            Current = SettingsSerializer.Read(_storage);
            Apply(Current);
        }

        public void Tick()
        {
        }

        public void Shutdown()
        {
        }

        public void Change(GameSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (settings.SameAs(Current)) return;

            Current = settings;
            SettingsSerializer.Write(_storage, settings);
            Apply(settings);
            Changed?.Invoke(settings);
        }

        private static void Apply(GameSettings settings)
        {
            AudioListener.volume = settings.MasterVolume;
            QualitySettings.vSyncCount = settings.VSync ? 1 : 0;
            if (Application.isEditor) return;

            FullScreenMode mode = settings.Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (settings.HasScreenSize && (Screen.width != settings.ScreenWidth || Screen.height != settings.ScreenHeight || Screen.fullScreenMode != mode))
            {
                Screen.SetResolution(settings.ScreenWidth, settings.ScreenHeight, mode);
            }
            else
            {
                Screen.fullScreenMode = mode;
            }
        }
    }
}
