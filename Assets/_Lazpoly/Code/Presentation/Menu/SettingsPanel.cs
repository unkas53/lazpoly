using Lazpoly.Infra;
using Lazpoly.Infra.Settings;
using UnityEngine;

namespace Lazpoly.Presentation.Menu
{
    /// <summary>
    /// Prototype settings controls (IMGUI), shown in the main menu and in the game. Every change is
    /// saved at once by <see cref="SettingsService"/>. Replaced with the final interface in Phase 6b.
    /// </summary>
    public sealed class SettingsPanel
    {
        private static readonly string[] AiSpeedNames = { "Yavaş", "Normal", "Hızlı" };

        private readonly SettingsService _settings;

        public SettingsPanel()
        {
            GameServices.Registry?.TryGet(out _settings);
        }

        public bool IsOpen { get; set; }

        public void Draw()
        {
            if (!IsOpen) return;
            if (_settings == null)
            {
                GUILayout.Label("Ayarlar kullanılamıyor.");
                return;
            }

            GameSettings current = _settings.Current;
            GameSettings next = current;

            GUILayout.Label($"Ses: %{Mathf.RoundToInt(current.MasterVolume * 100f)}");
            float volume = GUILayout.HorizontalSlider(current.MasterVolume, 0f, 1f);
            if (!Mathf.Approximately(volume, current.MasterVolume)) next = next.WithMasterVolume(Mathf.Round(volume * 20f) / 20f);

            bool fullscreen = GUILayout.Toggle(current.Fullscreen, " Tam ekran");
            if (fullscreen != current.Fullscreen) next = next.WithFullscreen(fullscreen);

            bool vSync = GUILayout.Toggle(current.VSync, " Dikey eşitleme (VSync)");
            if (vSync != current.VSync) next = next.WithVSync(vSync);

            bool fast = GUILayout.Toggle(current.FastAnimations, " Hızlı animasyonlar");
            if (fast != current.FastAnimations) next = next.WithFastAnimations(fast);

            GUILayout.Label("Bilgisayar hamle hızı");
            int speed = GUILayout.Toolbar((int)current.AiSpeed, AiSpeedNames);
            if (speed != (int)current.AiSpeed) next = next.WithAiSpeed((AiSpeed)speed);

            if (!next.SameAs(current)) _settings.Change(next);
        }
    }
}
