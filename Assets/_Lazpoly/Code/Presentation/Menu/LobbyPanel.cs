using System;
using Lazpoly.App.Lobby;
using Lazpoly.Core.Rules;
using Lazpoly.Net.Transport;
using Lazpoly.Presentation.Setup;
using UnityEngine;

namespace Lazpoly.Presentation.Menu
{
    /// <summary>
    /// Online part of the prototype main menu (IMGUI): create a room, invite friends, see who is
    /// in, and start. Members other than the owner follow the owner into the game automatically.
    /// </summary>
    public sealed class LobbyPanel : IDisposable
    {
        private readonly ILobbyService _lobby;
        private readonly ITransport _transport;
        private readonly Action _loadGameScene;
        private string _message;
        private bool _launched;

        /// <param name="lobby">Null when no lobby service is registered (the panel then shows why).</param>
        public LobbyPanel(ILobbyService lobby, ITransport transport, Action loadGameScene)
        {
            _lobby = lobby;
            _transport = transport;
            _loadGameScene = loadGameScene ?? throw new ArgumentNullException(nameof(loadGameScene));
            if (_lobby != null) _lobby.Failed += OnFailed;
        }

        public bool IsAvailable => _lobby != null && _transport != null && _lobby.IsAvailable;

        public void Dispose()
        {
            if (_lobby != null) _lobby.Failed -= OnFailed;
        }

        /// <summary>Call every frame: sends a member into the game once the owner has started it.</summary>
        public void Update()
        {
            if (_launched || !IsAvailable || _lobby.Status != LobbyStatus.InLobby || _lobby.IsOwner || !_lobby.IsGameStarted) return;

            _launched = true;
            OnlineGameSetup.Set(OnlineLaunch.AsClient(_transport, _lobby.OwnerId));
            _loadGameScene();
        }

        public void Draw()
        {
            GUILayout.Label("Çevrim içi (Steam)");
            if (!IsAvailable)
            {
                GUI.enabled = false;
                GUILayout.Button("Steam açık değil", GUILayout.Height(32));
                GUI.enabled = true;
                return;
            }

            switch (_lobby.Status)
            {
                case LobbyStatus.Idle:
                    if (GUILayout.Button("Oda Kur", GUILayout.Height(32)))
                    {
                        _message = null;
                        _lobby.Create(RulesConfig.AbsoluteMaxPlayers);
                    }

                    GUILayout.Label("Bir arkadaşının odasına katılmak için Steam davetini kabul et.");
                    break;

                case LobbyStatus.Creating:
                case LobbyStatus.Joining:
                    GUILayout.Label("Odaya bağlanılıyor...");
                    if (GUILayout.Button("Vazgeç", GUILayout.Height(28))) _lobby.Leave();
                    break;

                case LobbyStatus.InLobby:
                    DrawRoom();
                    break;
            }

            if (_message != null) GUILayout.Label(_message);
        }

        private void DrawRoom()
        {
            GUILayout.Label($"Oda ({_lobby.Members.Count}/{RulesConfig.AbsoluteMaxPlayers})");
            foreach (LobbyMember member in _lobby.Members)
            {
                string marker = member.Id == _lobby.OwnerId ? " (ev sahibi)" : "";
                GUILayout.Label($"  {member.Name}{marker}");
            }

            if (GUILayout.Button("Arkadaş Davet Et", GUILayout.Height(28))) _lobby.InviteFriends();

            if (_lobby.IsOwner)
            {
                bool enough = _lobby.Members.Count >= RulesConfig.AbsoluteMinPlayers;
                GUI.enabled = enough;
                if (GUILayout.Button(enough ? "Çevrim İçi Oyunu Başlat" : "En az 2 oyuncu gerekli", GUILayout.Height(36)))
                {
                    StartAsHost();
                }

                GUI.enabled = true;
            }
            else
            {
                GUILayout.Label("Ev sahibinin oyunu başlatması bekleniyor.");
            }

            if (GUILayout.Button("Odadan Çık", GUILayout.Height(28))) _lobby.Leave();
        }

        private void StartAsHost()
        {
            OnlineSeatPlan plan;
            try
            {
                plan = OnlineSeatPlan.Create(_lobby.LocalId, _lobby.Members);
            }
            catch (ArgumentException exception)
            {
                _message = exception.Message;
                return;
            }

            _launched = true;
            OnlineGameSetup.Set(OnlineLaunch.AsHost(_transport, plan));
            _lobby.StartGame();
            _loadGameScene();
        }

        private void OnFailed(LobbyError error)
        {
            switch (error)
            {
                case LobbyError.Unavailable: _message = "Steam açık değil."; break;
                case LobbyError.CreateFailed: _message = "Oda kurulamadı."; break;
                case LobbyError.LobbyFull: _message = "Oda dolu."; break;
                default: _message = "Odaya katılınamadı."; break;
            }
        }
    }
}
