using System;
using System.Collections.Generic;
using Lazpoly.Infra;
using Lazpoly.Infra.Logging;
using Lazpoly.Infra.Settings;
using Lazpoly.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Menu
{
    /// <summary>
    /// Settings controls, shown in a window from the main menu and from the game, in four groups:
    /// sound, picture, game and help. Every change is saved at once by <see cref="SettingsService"/>.
    /// </summary>
    public sealed class SettingsPanel
    {
        private static readonly string[] AiSpeedNames = { "Yavaş", "Normal", "Hızlı" };
        private static readonly string[] QualityNames = { "Düşük", "Orta", "Yüksek" };
        private static readonly string[] LanguageNames = { "Otomatik", "Türkçe", "English" };
        private static readonly string[] UiScaleNames = { "Küçük", "Normal", "Büyük" };

        /// <summary>The groups scroll inside the window, so the window fits a small screen.</summary>
        private const float BodyHeight = 400f;

        private readonly SettingsService _settings;
        private readonly ErrorLogService _errorLog;

        public SettingsPanel()
        {
            GameServices.Registry?.TryGet(out _settings);
            GameServices.Registry?.TryGet(out _errorLog);
        }

        public SettingsPanel(SettingsService settings, ErrorLogService errorLog = null)
        {
            _settings = settings;
            _errorLog = errorLog;
        }

        public void Render(UiBuilder ui, Action close)
        {
            ui.Title(UiIcon.Sliders, L("Ayarlar"));
            if (_settings == null)
            {
                ui.Label(L("Ayarlar kullanılamıyor."), UiTextStyle.Small);
            }
            else
            {
                GameSettings current = _settings.Current;
                // Language sits above the scrolling groups, so someone who cannot read the rest finds it first.
                RenderLanguage(ui, current);
                UiBuilder body = ui.Scroll(BodyHeight);
                RenderSound(body, current);
                RenderPicture(body, current);
                RenderGame(body, current);
                RenderAccess(body, current);
                RenderHelp(body);
            }

            ui.Space(8f);
            ui.Button(L("Kapat"), close, style: UiButtonStyle.Primary);
        }

        private void RenderLanguage(UiBuilder ui, GameSettings current)
        {
            UiBuilder row = ui.Row();
            UiBuilder.Fix(row.Label(L("Dil") + " / Language"), width: 150f);
            for (int i = 0; i < LanguageNames.Length; i++)
            {
                var language = (Language)i;
                row.Button(L(LanguageNames[i]), () => Change(_settings.Current.WithLanguage(language)),
                    style: current.Language == language ? UiButtonStyle.Selected : UiButtonStyle.Secondary, height: UiTheme.SmallButtonHeight);
            }
        }

        private void RenderSound(UiBuilder ui, GameSettings current)
        {
            ui.Label(L("Ses"), UiTextStyle.Small, UiTheme.Muted);
            VolumeSlider(ui, L("Genel"), current.MasterVolume, v => _settings.Current.WithMasterVolume(v));
            VolumeSlider(ui, L("Efektler"), current.EffectsVolume, v => _settings.Current.WithEffectsVolume(v));
            VolumeSlider(ui, L("Müzik"), current.MusicVolume, v => _settings.Current.WithMusicVolume(v));
            ui.Space(6f);
        }

        private void RenderPicture(UiBuilder ui, GameSettings current)
        {
            ui.Label(L("Görüntü"), UiTextStyle.Small, UiTheme.Muted);
            ui.Label(L("Kalite"));
            UiBuilder qualities = ui.Row();
            for (int i = 0; i < QualityNames.Length; i++)
            {
                var quality = (GraphicsQuality)i;
                qualities.Button(L(QualityNames[i]), () => Change(_settings.Current.WithQuality(quality)),
                    style: current.Quality == quality ? UiButtonStyle.Selected : UiButtonStyle.Secondary, height: UiTheme.SmallButtonHeight);
            }

            ui.Label(L("Arayüz boyutu"));
            UiBuilder sizes = ui.Row();
            for (int i = 0; i < UiScaleNames.Length; i++)
            {
                var scale = (UiScale)i;
                sizes.Button(L(UiScaleNames[i]), () => Change(_settings.Current.WithUiScale(scale)),
                    style: current.UiScale == scale ? UiButtonStyle.Selected : UiButtonStyle.Secondary, height: UiTheme.SmallButtonHeight);
            }

            ui.Toggle(L("Gün ışığı akışı (tur ilerledikçe akşam olur)"), current.DayCycle, on => Change(_settings.Current.WithDayCycle(on)));
            ui.Toggle(L("Tam ekran"), current.Fullscreen, on => Change(_settings.Current.WithFullscreen(on)));
            ui.Toggle(L("Dikey eşitleme (VSync)"), current.VSync, on => Change(_settings.Current.WithVSync(on)));
            RenderResolution(ui, current);
            ui.Space(6f);
        }

        /// <summary>A stepper through the screen's sizes; the choice is applied in builds, the editor keeps its own window.</summary>
        private void RenderResolution(UiBuilder ui, GameSettings current)
        {
            List<Vector2Int> sizes = ScreenSizes();
            if (sizes.Count == 0) return;
            int chosen = IndexOf(sizes, current.HasScreenSize ? new Vector2Int(current.ScreenWidth, current.ScreenHeight) : new Vector2Int(Screen.width, Screen.height));

            ui.Label(L("Çözünürlük"));
            UiBuilder row = ui.Row();
            row.Button("<", () => PickSize(sizes[(chosen + sizes.Count - 1) % sizes.Count]), height: UiTheme.SmallButtonHeight);
            Text label = row.Label(SizeText(sizes[chosen]), alignment: TextAnchor.MiddleCenter);
            UiBuilder.Fix(label, width: 160f);
            row.Button(">", () => PickSize(sizes[(chosen + 1) % sizes.Count]), height: UiTheme.SmallButtonHeight);
            if (Application.isEditor) ui.Label(L("Editörde pencere boyutu değişmez; derlemede uygulanır."), UiTextStyle.Small, UiTheme.Muted);
        }

        private void RenderGame(UiBuilder ui, GameSettings current)
        {
            ui.Label(L("Oyun"), UiTextStyle.Small, UiTheme.Muted);

            ui.Toggle(L("Hızlı animasyonlar"), current.FastAnimations, on => Change(_settings.Current.WithFastAnimations(on)));
            ui.Label(L("Bilgisayar hamle hızı"));
            UiBuilder speeds = ui.Row();
            for (int i = 0; i < AiSpeedNames.Length; i++)
            {
                var speed = (AiSpeed)i;
                speeds.Button(L(AiSpeedNames[i]), () => Change(_settings.Current.WithAiSpeed(speed)),
                    style: current.AiSpeed == speed ? UiButtonStyle.Selected : UiButtonStyle.Secondary, height: UiTheme.SmallButtonHeight);
            }

            ui.Space(6f);
        }

        private void RenderAccess(UiBuilder ui, GameSettings current)
        {
            ui.Label(L("Erişilebilirlik"), UiTextStyle.Small, UiTheme.Muted);
            ui.Toggle(L("Renk körlüğü dostu renkler (gruplara harf eklenir)"), current.ColorBlind, on => Change(_settings.Current.WithColorBlind(on)));
            if (current.ColorBlind != ColorVision.Friendly) ui.Label(L("Tahta ve piyonlar bir sonraki masada yeni renkleri alır."), UiTextStyle.Small, UiTheme.Muted);
            ui.Label(L("Klavye: Tab ile düğmeler arasında gez, Enter ile bas, Esc ile kapat. Oyunda Space/Enter zar atar ve sırayı bitirir."), UiTextStyle.Small, UiTheme.Muted);
            ui.Space(6f);
        }

        private void RenderHelp(UiBuilder ui)
        {
            if (_errorLog == null) return;
            ui.Label(L("Sorun bildirme"), UiTextStyle.Small, UiTheme.Muted);
            ui.Label(L("Hatalar şu dosyaya yazılır; bir sorun bildirirken bu dosyayı ekle:"), UiTextStyle.Small);
            ui.Label(_errorLog.FilePath, UiTextStyle.Small, UiTheme.Muted);
            UiBuilder row = ui.Row();
            row.Button(L("Günlük klasörünü aç"), () => Application.OpenURL("file://" + _errorLog.Folder), height: UiTheme.SmallButtonHeight);
            row.Button(L("Sorun Bildir"), () =>
            {
                _errorLog.Report("Oyuncu sorun bildirdi (menü)", new[]
                {
                    $"sürüm {Application.version}, {Application.platform}, Unity {Application.unityVersion}",
                    $"ekran {Screen.width}x{Screen.height}, tam ekran {Screen.fullScreen}, kalite {QualitySettings.GetQualityLevel()}",
                    $"dil {Current}",
                });
                Application.OpenURL("file://" + _errorLog.Folder);
            }, height: UiTheme.SmallButtonHeight);
        }

        private void VolumeSlider(UiBuilder ui, string name, float value, Func<float, GameSettings> with)
        {
            Text label = ui.Label(VolumeText(name, value));
            ui.Slider(value, 0f, 1f, raw =>
            {
                float rounded = Mathf.Round(raw * 20f) / 20f;
                label.text = VolumeText(name, rounded);
                Change(with(rounded));
            });
        }

        private void PickSize(Vector2Int size) => Change(_settings.Current.WithScreenSize(size.x, size.y));

        private void Change(GameSettings next)
        {
            if (!next.SameAs(_settings.Current)) _settings.Change(next);
        }

        /// <summary>Distinct sizes the screen supports, smallest first; the current size is always among them.</summary>
        public static List<Vector2Int> ScreenSizes()
        {
            var sizes = new List<Vector2Int>();
            foreach (Resolution resolution in Screen.resolutions)
            {
                var size = new Vector2Int(resolution.width, resolution.height);
                if (size.x >= 640 && size.y >= 480 && !sizes.Contains(size)) sizes.Add(size);
            }

            var now = new Vector2Int(Screen.width, Screen.height);
            if (now.x >= 640 && now.y >= 480 && !sizes.Contains(now)) sizes.Add(now);
            sizes.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return sizes;
        }

        private static int IndexOf(List<Vector2Int> sizes, Vector2Int wanted)
        {
            int index = sizes.IndexOf(wanted);
            return index >= 0 ? index : sizes.Count - 1;
        }

        private static string SizeText(Vector2Int size) => $"{size.x} × {size.y}";

        private static string VolumeText(string name, float volume) => $"{name}: %{Mathf.RoundToInt(volume * 100f)}";
    }
}
