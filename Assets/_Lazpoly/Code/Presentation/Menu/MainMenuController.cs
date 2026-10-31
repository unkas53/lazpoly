using System.Collections.Generic;
using Lazpoly.App.Lobby;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;
using Lazpoly.Infra;
using Lazpoly.Net.Transport;
using Lazpoly.Presentation.Setup;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lazpoly.Presentation.Menu
{
    /// <summary>Prototype main menu (IMGUI): a local game for 2-8 players on this machine, or an online room.</summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        private readonly List<string> _names = new List<string>();
        private int _playerCount = 2;
        private GUIStyle _title;
        private LobbyPanel _lobbyPanel;

        private void Awake()
        {
            for (int i = 0; i < RulesConfig.AbsoluteMaxPlayers; i++) _names.Add($"Oyuncu {i + 1}");

            ILobbyService lobby = null;
            ITransport transport = null;
            GameServices.Registry?.TryGet(out lobby);
            GameServices.Registry?.TryGet(out transport);
            _lobbyPanel = new LobbyPanel(lobby, transport, () => SceneManager.LoadScene("Game"));
        }

        private void OnDestroy()
        {
            _lobbyPanel?.Dispose();
        }

        private void Update()
        {
            _lobbyPanel.Update();
        }

        private void OnGUI()
        {
            _title ??= new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            const float width = 360f;
            GUILayout.BeginArea(new Rect((Screen.width - width) / 2f, 40f, width, Screen.height - 80f), GUI.skin.box);
            GUILayout.Label("Lazpoly", _title, GUILayout.Height(56));

            GUILayout.Label("Yerel oyun (aynı bilgisayar)");
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Oyuncu sayısı: {_playerCount}");
            if (GUILayout.Button("-", GUILayout.Width(32)) && _playerCount > RulesConfig.AbsoluteMinPlayers) _playerCount--;
            if (GUILayout.Button("+", GUILayout.Width(32)) && _playerCount < RulesConfig.AbsoluteMaxPlayers) _playerCount++;
            GUILayout.EndHorizontal();

            for (int i = 0; i < _playerCount; i++)
            {
                _names[i] = GUILayout.TextField(_names[i], 24);
            }

            GUILayout.Space(12);
            if (GUILayout.Button("Oyunu Başlat", GUILayout.Height(40))) StartLocalGame();

            GUILayout.Space(16);
            _lobbyPanel.Draw();
            GUILayout.Space(16);

            if (GUILayout.Button("Çıkış", GUILayout.Height(32))) Application.Quit();
            GUILayout.EndArea();
        }

        private void StartLocalGame()
        {
            var seats = new PlayerSeat[_playerCount];
            for (int i = 0; i < _playerCount; i++)
            {
                string name = string.IsNullOrWhiteSpace(_names[i]) ? $"Oyuncu {i + 1}" : _names[i].Trim();
                seats[i] = new PlayerSeat(i + 1, name);
            }

            LocalGameSetup.SetSeats(seats);
            SceneManager.LoadScene("Game");
        }
    }
}
