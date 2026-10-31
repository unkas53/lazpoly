using System;
using System.Collections.Generic;
using Lazpoly.App.Lobby;
using Lazpoly.Core.Rules;
using Lazpoly.Net.Transport;
using Lazpoly.Presentation.Setup;
using Lazpoly.Presentation.UI;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Menu
{
    /// <summary>
    /// Online part of the main menu: create a room, invite friends, see who is
    /// in, and start. Members other than the owner follow the owner into the game automatically.
    /// </summary>
    public sealed class LobbyPanel : IDisposable
    {
        private readonly ILobbyService _lobby;
        private readonly ITransport _transport;
        private readonly Action _loadGameScene;
        private string _message;
        private bool _launched;
        private IReadOnlyList<LobbyFriend> _friends = Array.Empty<LobbyFriend>();
        private LobbyStatus? _friendsLoadedFor;

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

        /// <summary>Friends as last loaded; reloaded whenever the room status changes or on request.</summary>
        public IReadOnlyList<LobbyFriend> Friends => _friends;

        /// <summary>
        /// Call every frame: reloads the friend list when the room status changes, and sends a member
        /// into the game once the owner has started it.
        /// </summary>
        public void Update()
        {
            if (!IsAvailable) return;
            if (_friendsLoadedFor != _lobby.Status) RefreshFriends();
            if (_launched || _lobby.Status != LobbyStatus.InLobby || _lobby.IsOwner || !_lobby.IsGameStarted) return;

            _launched = true;
            OnlineGameSetup.Set(OnlineLaunch.AsClient(_transport, _lobby.OwnerId));
            _loadGameScene();
        }

        /// <summary>Changes whenever something this panel shows changes.</summary>
        public int ViewKey
        {
            get
            {
                if (!IsAvailable) return 0;
                int key = HashCode.Combine(_lobby.Status, _lobby.IsOwner, _message, _friends.Count);
                foreach (LobbyMember member in _lobby.Members) key = HashCode.Combine(key, member.Id, member.Name);
                return key;
            }
        }

        public void Render(UiBuilder ui)
        {
            ui.Label(L("Çevrim içi (Steam)"), UiTextStyle.Heading);
            if (!IsAvailable)
            {
                ui.Label(L("Steam açık değil. Çevrim içi oynamak için oyunu Steam üzerinden başlat."), UiTextStyle.Small);
                return;
            }

            switch (_lobby.Status)
            {
                case LobbyStatus.Idle:
                    ui.Button(L("Oda Kur"), () =>
                    {
                        _message = null;
                        _lobby.Create(RulesConfig.AbsoluteMaxPlayers);
                    }, style: UiButtonStyle.Primary);
                    RenderJoinableFriends(ui);
                    break;

                case LobbyStatus.Creating:
                case LobbyStatus.Joining:
                    ui.Label(L("Odaya bağlanılıyor..."));
                    ui.Button(L("Vazgeç"), _lobby.Leave);
                    break;

                case LobbyStatus.InLobby:
                    RenderRoom(ui);
                    break;
            }

            if (_message != null) ui.Label(_message, UiTextStyle.Small, UiTheme.Accent);
        }

        private void RenderRoom(UiBuilder ui)
        {
            ui.Label(F($"Oda ({_lobby.Members.Count}/{RulesConfig.AbsoluteMaxPlayers})"));
            UiBuilder members = ui.Card();
            foreach (LobbyMember member in _lobby.Members)
            {
                string marker = member.Id == _lobby.OwnerId ? L("  (ev sahibi)") : "";
                members.Label($"{member.Name}{marker}");
            }

            if (_lobby.IsOverlayAvailable) ui.Button(L("Steam ile Davet Et"), _lobby.InviteFriends, height: UiTheme.SmallButtonHeight);
            RenderInvitableFriends(ui);

            if (_lobby.IsOwner)
            {
                bool enough = _lobby.Members.Count >= RulesConfig.AbsoluteMinPlayers;
                ui.Label(L("Masa kuralları alttaki \"Masa Kuralları\" düğmesinden seçilir."), UiTextStyle.Small, UiTheme.Muted);
                ui.Button(enough ? L("Çevrim İçi Oyunu Başlat") : L("En az 2 oyuncu gerekli"), StartAsHost, enough, UiButtonStyle.Primary);
            }
            else
            {
                ui.Label(L("Ev sahibinin oyunu başlatması bekleniyor."), UiTextStyle.Small);
            }

            ui.Button(L("Odadan Çık"), _lobby.Leave, style: UiButtonStyle.Danger, height: UiTheme.SmallButtonHeight);
        }

        /// <summary>Lists friends who are in a Lazpoly room right now, so joining needs no invite.</summary>
        private void RenderJoinableFriends(UiBuilder ui)
        {
            ui.Label(L("Odası açık arkadaşlar"), UiTextStyle.Small);
            UiBuilder list = ui.Scroll(150f);
            int shown = 0;
            foreach (LobbyFriend friend in _friends)
            {
                if (friend.LobbyId == 0) continue;
                shown++;
                UiBuilder row = list.Row();
                row.Label(friend.Name);
                UiBuilder.Fix(row.Button(L("Katıl"), () =>
                {
                    _message = null;
                    _lobby.Join(friend.LobbyId);
                }, style: UiButtonStyle.Primary, height: UiTheme.SmallButtonHeight), width: 90f);
            }

            if (shown == 0) list.Label(L("Odası açık arkadaşın yok. Arkadaşın oda kurunca burada görünür ya da davet gönderebilir."), UiTextStyle.Small);
            ui.Button(L("Listeyi Yenile"), RefreshFriends, height: UiTheme.SmallButtonHeight);
        }

        private void RenderInvitableFriends(UiBuilder ui)
        {
            ui.Label(L("Çevrim içi arkadaşlar"), UiTextStyle.Small);
            UiBuilder list = ui.Scroll(150f);
            int shown = 0;
            foreach (LobbyFriend friend in _friends)
            {
                if (!friend.IsOnline || _lobby.IsMember(friend.Id)) continue;
                shown++;
                UiBuilder row = list.Row();
                row.Label(friend.Name);
                UiBuilder.Fix(row.Button(L("Davet"), () => InviteFriend(friend), height: UiTheme.SmallButtonHeight), width: 90f);
            }

            if (shown == 0) list.Label(L("Çevrim içi arkadaş bulunamadı."), UiTextStyle.Small);
            ui.Button(L("Listeyi Yenile"), RefreshFriends, height: UiTheme.SmallButtonHeight);
        }

        public void InviteFriend(LobbyFriend friend)
        {
            _message = _lobby.Invite(friend.Id)
                ? F($"{friend.Name} davet edildi. Lazpoly açıkken Steam sohbetindeki daveti kabul etsin ya da menüdeki listeden \"Katıl\" desin.")
                : F($"{friend.Name} davet edilemedi.");
        }

        public void RefreshFriends()
        {
            _friendsLoadedFor = _lobby.Status;
            _friends = _lobby.GetFriends() ?? Array.Empty<LobbyFriend>();
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
                case LobbyError.Unavailable: _message = L("Steam açık değil."); break;
                case LobbyError.CreateFailed: _message = L("Oda kurulamadı."); break;
                case LobbyError.LobbyFull: _message = L("Oda dolu."); break;
                default: _message = L("Odaya katılınamadı."); break;
            }
        }
    }
}
