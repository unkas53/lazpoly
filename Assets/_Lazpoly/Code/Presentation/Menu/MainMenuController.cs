using System.Collections.Generic;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Setup;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lazpoly.Presentation.Menu
{
    /// <summary>Phase 2 prototype main menu (IMGUI): choose 2-8 local players and start.</summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        private readonly List<string> _names = new List<string>();
        private int _playerCount = 2;
        private GUIStyle _title;

        private void Awake()
        {
            for (int i = 0; i < RulesConfig.AbsoluteMaxPlayers; i++) _names.Add($"Oyuncu {i + 1}");
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

            GUI.enabled = false;
            GUILayout.Button("Çevrim İçi (yakında)", GUILayout.Height(32));
            GUI.enabled = true;

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
