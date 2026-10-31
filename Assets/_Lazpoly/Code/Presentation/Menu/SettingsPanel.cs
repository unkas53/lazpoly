using System;
using Lazpoly.Infra;
using Lazpoly.Infra.Settings;
using Lazpoly.Presentation.UI;
using UnityEngine;

namespace Lazpoly.Presentation.Menu
{
    /// <summary>
    /// Settings controls, shown in a window from the main menu and from the game. Every change is
    /// saved at once by <see cref="SettingsService"/>.
    /// </summary>
    public sealed class SettingsPanel
    {
        private static readonly string[] AiSpeedNames = { "Yavaş", "Normal", "Hızlı" };

        private readonly SettingsService _settings;

        public SettingsPanel()
        {
            GameServices.Registry?.TryGet(out _settings);
        }

        public SettingsPanel(SettingsService settings)
        {
            _settings = settings;
        }

        public void Render(UiBuilder ui, Action close)
        {
            ui.Label("Ayarlar", UiTextStyle.Heading);
            if (_settings == null)
            {
                ui.Label("Ayarlar kullanılamıyor.", UiTextStyle.Small);
            }
            else
            {
                GameSettings current = _settings.Current;

                var volumeLabel = ui.Label(VolumeText(current.MasterVolume));
                ui.Slider(current.MasterVolume, 0f, 1f, value =>
                {
                    float rounded = Mathf.Round(value * 20f) / 20f;
                    volumeLabel.text = VolumeText(rounded);
                    if (!Mathf.Approximately(rounded, _settings.Current.MasterVolume)) Change(_settings.Current.WithMasterVolume(rounded));
                });

                ui.Toggle("Tam ekran", current.Fullscreen, on => Change(_settings.Current.WithFullscreen(on)));
                ui.Toggle("Dikey eşitleme (VSync)", current.VSync, on => Change(_settings.Current.WithVSync(on)));
                ui.Toggle("Hızlı animasyonlar", current.FastAnimations, on => Change(_settings.Current.WithFastAnimations(on)));

                ui.Label("Bilgisayar hamle hızı");
                UiBuilder speeds = ui.Row();
                for (int i = 0; i < AiSpeedNames.Length; i++)
                {
                    var speed = (AiSpeed)i;
                    speeds.Button(AiSpeedNames[i], () => Change(_settings.Current.WithAiSpeed(speed)),
                        style: current.AiSpeed == speed ? UiButtonStyle.Selected : UiButtonStyle.Secondary, height: UiTheme.SmallButtonHeight);
                }
            }

            ui.Space(8f);
            ui.Button("Kapat", close, style: UiButtonStyle.Primary);
        }

        private void Change(GameSettings next)
        {
            if (!next.SameAs(_settings.Current)) _settings.Change(next);
        }

        private static string VolumeText(float volume) => $"Ses: %{Mathf.RoundToInt(volume * 100f)}";
    }
}
