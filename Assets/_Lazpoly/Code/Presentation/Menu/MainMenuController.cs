using System.Collections.Generic;
using Lazpoly.App.AI;
using Lazpoly.App.Lobby;
using Lazpoly.App.Presence;
using Lazpoly.App.Save;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;
using Lazpoly.Infra;
using Lazpoly.Net.Transport;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Setup;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lazpoly.Presentation.Menu
{
    /// <summary>
    /// Prototype main menu (IMGUI): a local game for 2-8 seats on this machine, each played by a
    /// person or the computer, a saved local game to continue, or an online room.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        /// <summary>What sits in a local seat: a person (0) or the computer at a difficulty.</summary>
        private static readonly string[] SeatKinds = { "İnsan", "AI: Kolay", "AI: Orta", "AI: Zor" };

        private readonly List<string> _names = new List<string>();
        private readonly List<int> _seatKinds = new List<int>();
        private int _playerCount = 2;
        private GUIStyle _title;
        private LobbyPanel _lobbyPanel;
        private SavedGame _savedGame;
        private SettingsPanel _settings;
        private ILobbyService _lobby;
        private IPresenceService _presence;

        private void Awake()
        {
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

        private void OnGUI()
        {
            _title ??= new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            const float width = 360f;
            GUILayout.BeginArea(new Rect((Screen.width - width) / 2f, 40f, width, Screen.height - 80f), GUI.skin.box);
            GUILayout.Label("Lazpoly", _title, GUILayout.Height(56));

            if (_savedGame != null)
            {
                if (GUILayout.Button("Kayıtlı Oyuna Devam Et", GUILayout.Height(40))) ResumeSavedGame();
                GUILayout.Space(12);
            }

            GUILayout.Label("Yerel oyun (aynı bilgisayar)");
            if (GUILayout.Button("Tek Oyunculu: sen + 3 bilgisayar", GUILayout.Height(30))) UseSinglePlayerPreset();
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Oyuncu sayısı: {_playerCount}");
            if (GUILayout.Button("-", GUILayout.Width(32)) && _playerCount > RulesConfig.AbsoluteMinPlayers) _playerCount--;
            if (GUILayout.Button("+", GUILayout.Width(32)) && _playerCount < RulesConfig.AbsoluteMaxPlayers) _playerCount++;
            GUILayout.EndHorizontal();

            for (int i = 0; i < _playerCount; i++)
            {
                GUILayout.BeginHorizontal();
                _names[i] = GUILayout.TextField(_names[i], 24);
                if (GUILayout.Button(SeatKinds[_seatKinds[i]], GUILayout.Width(96))) _seatKinds[i] = (_seatKinds[i] + 1) % SeatKinds.Length;
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(12);
            if (GUILayout.Button("Oyunu Başlat", GUILayout.Height(40))) StartLocalGame();

            GUILayout.Space(16);
            _lobbyPanel.Draw();
            GUILayout.Space(16);

            if (GUILayout.Button(_settings.IsOpen ? "Ayarları Kapat" : "Ayarlar", GUILayout.Height(32))) _settings.IsOpen = !_settings.IsOpen;
            _settings.Draw();
            GUILayout.Space(8);
            if (GUILayout.Button("Çıkış", GUILayout.Height(32))) Application.Quit();
            GUILayout.EndArea();
        }

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
