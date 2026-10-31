using System.Collections.Generic;
using Lazpoly.App.AI;
using Lazpoly.App.Lobby;
using Lazpoly.App.Presence;
using Lazpoly.App.Save;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;
using Lazpoly.Infra;
using Lazpoly.Net.Transport;
using Lazpoly.Presentation.Audio;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Setup;
using Lazpoly.Presentation.Tabletop;
using Lazpoly.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Lazpoly.Presentation.Menu
{
    /// <summary>
    /// Main menu: a local game for 2-8 seats on this machine, each played by a person or the
    /// computer, a saved local game to continue, or an online room.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        /// <summary>What sits in a local seat: a person (0) or the computer at a difficulty.</summary>
        private static readonly string[] SeatKinds = { "İnsan", "AI: Kolay", "AI: Orta", "AI: Zor" };

        private readonly List<string> _names = new List<string>();
        private readonly List<int> _seatKinds = new List<int>();
        private int _playerCount = 2;
        private UiView _titleView;
        private UiView _localView;
        private UiView _onlineView;
        private UiView _footerView;
        private UiView _settingsView;
        private GameObject _settingsLayer;
        private LobbyPanel _lobbyPanel;
        private SavedGame _savedGame;
        private SettingsPanel _settings;
        private ILobbyService _lobby;
        private IPresenceService _presence;

        private void Awake()
        {
            AudioPlayer.Ensure();
            for (int i = 0; i < RulesConfig.AbsoluteMaxPlayers; i++)
            {
                _names.Add($"Oyuncu {i + 1}");
                _seatKinds.Add(0);
            }

            FindSavedGame();

            ITransport transport = null;
            GameServices.Registry?.TryGet(out _lobby);
            GameServices.Registry?.TryGet(out transport);
            GameServices.Registry?.TryGet(out _presence);
            _lobbyPanel = new LobbyPanel(_lobby, transport, () => SceneManager.LoadScene("Game"));
            _settings = new SettingsPanel();
        }

        private void OnDestroy()
        {
            _lobbyPanel?.Dispose();
        }

        private void Update()
        {
            _lobbyPanel.Update();
            if (_presence == null) return;
            bool inRoom = _lobby != null && _lobby.Status == LobbyStatus.InLobby;
            _presence.Show(inRoom ? PresenceText.InRoom(_lobby.Members.Count, RulesConfig.AbsoluteMaxPlayers) : PresenceText.MainMenu());
        }

        private void Start()
        {
            Canvas canvas = UiRoot.CreateCanvas(transform, "Main Menu UI", 0);
            UiRoot.Background(UiRoot.Stretch(canvas.transform, "Backdrop"), UiTheme.Backdrop, rounded: false);

            RectTransform title = UiRoot.Rect(canvas.transform, "Title", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-600f, -150f), new Vector2(600f, -40f));
            _titleView = new UiView(new UiBuilder(title), RenderTitle);
            UiBuilder.AddColumnLayout(title.gameObject, 0, 4f);

            RectTransform local = UiRoot.Rect(canvas.transform, "Local Game", new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-600f, 120f), new Vector2(-12f, -170f));
            UiRoot.Background(local, UiTheme.Panel);
            _localView = new UiView(UiRoot.ScrollPanel(local, 24), RenderLocalGame);

            RectTransform online = UiRoot.Rect(canvas.transform, "Online Game", new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(12f, 120f), new Vector2(600f, -170f));
            UiRoot.Background(online, UiTheme.Panel);
            _onlineView = new UiView(UiRoot.ScrollPanel(online, 24), _lobbyPanel.Render);

            RectTransform footer = UiRoot.Rect(canvas.transform, "Footer", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-600f, 40f), new Vector2(600f, 96f));
            _footerView = new UiView(new UiBuilder(footer), RenderFooter);
            var row = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = UiTheme.Spacing * 2f;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;

            _settingsView = new UiView(UiRoot.Modal(canvas.transform, "Settings", 520f, out _settingsLayer), ui => _settings.Render(ui, CloseSettings));
            _settingsLayer.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_localView == null) return;
            _titleView.Refresh(_savedGame != null ? 1 : 0);
            _localView.Refresh(_playerCount);
            _onlineView.Refresh(_lobbyPanel.ViewKey);
            _footerView.Refresh(0);
            if (_settingsLayer.activeSelf) _settingsView.Refresh(0);
        }

        private void RenderTitle(UiBuilder ui)
        {
            ui.Label("LAZPOLY", UiTextStyle.Title, UiTheme.Accent, TextAnchor.MiddleCenter);
            ui.Label("Arkadaşlarınla ya da bilgisayara karşı mülk topla, kirayı topla, son kalan sen ol.", UiTextStyle.Small, alignment: TextAnchor.MiddleCenter);
        }

        private void RenderLocalGame(UiBuilder ui)
        {
            if (_savedGame != null)
            {
                ui.Button("Kayıtlı Oyuna Devam Et", ResumeSavedGame, style: UiButtonStyle.Primary, height: 52f);
                ui.Space(8f);
            }

            ui.Label("Yerel oyun (aynı bilgisayar)", UiTextStyle.Heading);
            ui.Button("Tek Oyunculu: sen + 3 bilgisayar", UseSinglePlayerPreset);

            UiBuilder count = ui.Row();
            count.Label($"Oyuncu sayısı: {_playerCount}");
            UiBuilder.Fix(count.Button("-", () => _playerCount--, _playerCount > RulesConfig.AbsoluteMinPlayers), width: 52f);
            UiBuilder.Fix(count.Button("+", () => _playerCount++, _playerCount < RulesConfig.AbsoluteMaxPlayers), width: 52f);

            for (int i = 0; i < _playerCount; i++)
            {
                int seat = i;
                UiBuilder row = ui.Row();
                row.Swatch(PlayerPalette.ForSeat(seat), 22f);
                row.Input(_names[seat], text => _names[seat] = text, 24);
                UiBuilder.Fix(row.Button(SeatKinds[_seatKinds[seat]], () => _seatKinds[seat] = (_seatKinds[seat] + 1) % SeatKinds.Length,
                    style: _seatKinds[seat] == 0 ? UiButtonStyle.Secondary : UiButtonStyle.Selected, height: UiTheme.FieldHeight), width: 130f);
            }

            ui.Space(8f);
            ui.Button("Oyunu Başlat", StartLocalGame, style: UiButtonStyle.Primary, height: 52f);
        }

        private void RenderFooter(UiBuilder ui)
        {
            ui.Button("Ayarlar", OpenSettings);
            ui.Button("Çıkış", Application.Quit, style: UiButtonStyle.Danger);
        }

        private void OpenSettings()
        {
            _settingsLayer.SetActive(true);
            _settingsView.Invalidate();
        }

        private void CloseSettings() => _settingsLayer.SetActive(false);

        private void StartLocalGame()
        {
            var seats = new PlayerSeat[_playerCount];
            var ai = new Dictionary<int, AiDifficulty>();
            for (int i = 0; i < _playerCount; i++)
            {
                string name = string.IsNullOrWhiteSpace(_names[i]) ? $"Oyuncu {i + 1}" : _names[i].Trim();
                seats[i] = new PlayerSeat(i + 1, name);
                if (_seatKinds[i] != 0) ai.Add(i + 1, (AiDifficulty)_seatKinds[i]);
            }

            LocalGameSetup.SetSeats(seats, ai);
            SceneManager.LoadScene("Game");
        }

        private void UseSinglePlayerPreset()
        {
            _playerCount = 4;
            _seatKinds[0] = 0;
            if (_names[0].StartsWith("Bilgisayar")) _names[0] = "Oyuncu 1";
            for (int i = 1; i < _playerCount; i++)
            {
                _seatKinds[i] = (int)AiDifficulty.Normal;
                _names[i] = $"Bilgisayar {i}";
            }
        }

        /// <summary>Offers the save only if it was made with the board data this build ships.</summary>
        private void FindSavedGame()
        {
            if (!SaveGameStore.TryRead(out SavedGame save)) return;
            BoardData data = BoardDataLoader.LoadFromResources();
            if (save.BoardChecksum == data.Checksum) _savedGame = save;
        }

        private void ResumeSavedGame()
        {
            LocalGameSetup.SetResume(_savedGame);
            SceneManager.LoadScene("Game");
        }
    }
}
