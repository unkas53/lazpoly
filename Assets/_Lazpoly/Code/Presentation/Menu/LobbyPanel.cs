using System;
using System.Collections.Generic;
using Lazpoly.App.Lobby;
using Lazpoly.Core.Rules;
using Lazpoly.Net.Messages;
using Lazpoly.Net.Transport;
using Lazpoly.Presentation.Setup;
using Lazpoly.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Menu
{
    /// <summary>
    /// Online part of the main menu: create a room or join one by code, invite friends, see who is
    /// in and who is ready, read the house rules the owner picked, and start. Members other than
    /// the owner follow the owner into the game automatically.
    /// </summary>
    public sealed class LobbyPanel : IDisposable
    {
        private readonly ILobbyService _lobby;
        private readonly ITransport _transport;
        private readonly Action _loadGameScene;
        private readonly Action _openTableRules;
        private readonly int _boardMoney;
        private string _message;
        private string _problem;
        private string _code = "";
        private bool _launched;
        private IReadOnlyList<LobbyFriend> _friends = Array.Empty<LobbyFriend>();
        private LobbyStatus? _friendsLoadedFor;

        /// <param name="lobby">Null when no lobby service is registered (the panel then shows why).</param>
        /// <param name="openTableRules">Opens the house rules window; null hides the "change" button.</param>
        /// <param name="boardMoney">The board's own starting money, for the rules summary.</param>
        public LobbyPanel(ILobbyService lobby, ITransport transport, Action loadGameScene, Action openTableRules = null, int boardMoney = 0)
        {
            _lobby = lobby;
            _transport = transport;
            _loadGameScene = loadGameScene ?? throw new ArgumentNullException(nameof(loadGameScene));
            _openTableRules = openTableRules;
            _boardMoney = boardMoney;
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
            // The owner's chosen rules go up to the room as soon as they change, so every member reads the same summary.
            if (_lobby.Status == LobbyStatus.InLobby && _lobby.IsOwner && !TableSetup.Chosen.SameAs(_lobby.Table)) _lobby.SetTable(TableSetup.Chosen);
            if (_launched || _lobby.Status != LobbyStatus.InLobby || _lobby.IsOwner || !_lobby.IsGameStarted || RoomIsOutdated) return;

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
                TableRules table = _lobby.Table;
                int key = HashCode.Combine(_lobby.Status, _lobby.IsOwner, _message, _problem, _friends.Count);
                key = HashCode.Combine(key, table.StartingMoney, table.AuctionsEnabled, table.RoundLimit, table.TurnSeconds, table.BankReturns);
                key = HashCode.Combine(key, _lobby.RoomProtocolVersion);
                foreach (LobbyMember member in _lobby.Members) key = HashCode.Combine(key, member.Id, member.Name, member.IsReady, member.ProtocolVersion);
                return key;
            }
        }

        public void Render(UiBuilder ui)
        {
            ui.Label(L("Çevrim içi (Steam)"), UiTextStyle.Heading);
            if (!IsAvailable)
            {
                ui.Notice(L("Steam açık değil. Çevrim içi oynamak için Steam'i açıp oyunu oradan başlat."));
                return;
            }

            if (_problem != null) ui.Notice(_problem, L("Tamam"), () => _problem = null);
            switch (_lobby.Status)
            {
                case LobbyStatus.Idle:
                    ui.Button(L("Oda Kur"), () =>
                    {
                        _message = null;
                        _lobby.Create(RulesConfig.AbsoluteMaxPlayers);
                    }, style: UiButtonStyle.Primary);
                    RenderJoinByCode(ui);
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

        /// <summary>Join a friend's room by the code they read out, when neither invite route is handy.</summary>
        private void RenderJoinByCode(UiBuilder ui)
        {
            ui.Label(L("Oda koduyla katıl"), UiTextStyle.Small);
            UiBuilder row = ui.Row();
            row.Input(_code, text => _code = text, 20, InputField.ContentType.IntegerNumber);
            UiBuilder.Fix(row.Button(L("Katıl"), JoinByCode, height: UiTheme.FieldHeight), width: 110f);
        }

        private void JoinByCode()
        {
            if (!ulong.TryParse(_code.Trim(), out ulong id) || id == 0)
            {
                _problem = L("Oda kodu yalnızca rakamlardan oluşur; arkadaşın odasındaki kodu kopyalayıp sana gönderebilir.");
                return;
            }

            _message = null;
            _lobby.Join(id);
        }

        private void RenderRoom(UiBuilder ui)
        {
            int ready = ReadyCount();
            ui.Label(F($"Oda ({_lobby.Members.Count}/{RulesConfig.AbsoluteMaxPlayers})"));
            UiBuilder members = ui.Card();
            foreach (LobbyMember member in _lobby.Members)
            {
                UiBuilder row = members.Row(10f, expand: false);
                bool owner = member.Id == _lobby.OwnerId;
                bool outdated = IsOutdated(member);
                row.Swatch(outdated ? UiTheme.Warning : member.IsReady ? UiTheme.Gain : UiTheme.Muted, 14f, round: true);
                UiBuilder.Fix(row.Label(owner ? F($"{member.Name} (ev sahibi)") : member.Name), -1f).flexibleWidth = 1f;
                row.Label(outdated ? L("sürümü farklı") : member.IsReady ? L("hazır") : L("bekliyor"), UiTextStyle.Small,
                    outdated ? UiTheme.Warning : member.IsReady ? UiTheme.Gain : UiTheme.Muted, TextAnchor.MiddleRight);
            }

            if (RoomIsOutdated)
            {
                ui.Notice(F($"Ev sahibinin oyun sürümü seninkinden farklı (protokol {_lobby.RoomProtocolVersion}, seninki {MessageCodec.ProtocolVersion}). Birlikte oynamak için ikiniz de Steam'den son güncellemeyi alın."));
            }
            else if (_lobby.IsOwner && OutdatedCount() > 0)
            {
                ui.Notice(L("Sürümü farklı olan oyuncular Steam'den güncelleme almadan oyun başlatılamaz."));
            }

            RenderRoomCode(ui);
            RenderTableSummary(ui);
            if (_lobby.IsOverlayAvailable) ui.Button(L("Steam ile Davet Et"), _lobby.InviteFriends, height: UiTheme.SmallButtonHeight);
            RenderInvitableFriends(ui);

            if (_lobby.IsOwner)
            {
                bool enough = _lobby.Members.Count >= RulesConfig.AbsoluteMinPlayers;
                bool everyone = ready == _lobby.Members.Count;
                bool sameVersion = OutdatedCount() == 0;
                ui.Label(!enough ? L("En az 2 oyuncu gerekli.") : everyone ? L("Herkes hazır.") : F($"{ready}/{_lobby.Members.Count} hazır; kalanlar bekleniyor."),
                    UiTextStyle.Small, everyone && enough ? UiTheme.Gain : UiTheme.Muted);
                ui.Button(L("Çevrim İçi Oyunu Başlat"), StartAsHost, enough && everyone && sameVersion, UiButtonStyle.Primary);
            }
            else if (!RoomIsOutdated)
            {
                bool me = IsLocalReady();
                ui.Button(me ? L("Hazır Değilim") : L("Hazırım"), () => _lobby.SetReady(!me), style: me ? UiButtonStyle.Selected : UiButtonStyle.Primary);
                ui.Label(me ? L("Ev sahibinin oyunu başlatması bekleniyor.") : L("Hazır olunca ev sahibi oyunu başlatabilir."), UiTextStyle.Small);
            }

            ui.Button(L("Odadan Çık"), _lobby.Leave, style: UiButtonStyle.Danger, height: UiTheme.SmallButtonHeight);
        }

        /// <summary>A member whose build speaks another protocol than this one; unknown counts as fine until they say.</summary>
        private static bool IsOutdated(LobbyMember member) => member.ProtocolVersion != 0 && member.ProtocolVersion != MessageCodec.ProtocolVersion;

        /// <summary>Members the owner cannot start with.</summary>
        public int OutdatedCount()
        {
            int count = 0;
            foreach (LobbyMember member in _lobby.Members) count += IsOutdated(member) ? 1 : 0;
            return count;
        }

        /// <summary>For a guest: the room is run by a build this one cannot play with.</summary>
        public bool RoomIsOutdated => IsAvailable && _lobby.Status == LobbyStatus.InLobby && !_lobby.IsOwner &&
                                      _lobby.RoomProtocolVersion != 0 && _lobby.RoomProtocolVersion != MessageCodec.ProtocolVersion;

        /// <summary>How many members are ready; the owner always is.</summary>
        public int ReadyCount()
        {
            int count = 0;
            foreach (LobbyMember member in _lobby.Members) count += member.IsReady || member.Id == _lobby.OwnerId ? 1 : 0;
            return count;
        }

        /// <summary>Whether the owner may start: enough members and all of them ready.</summary>
        public bool CanStart => IsAvailable && _lobby.Status == LobbyStatus.InLobby && _lobby.IsOwner &&
                                _lobby.Members.Count >= RulesConfig.AbsoluteMinPlayers && ReadyCount() == _lobby.Members.Count && OutdatedCount() == 0;

        private bool IsLocalReady()
        {
            foreach (LobbyMember member in _lobby.Members)
            {
                if (member.Id == _lobby.LocalId) return member.IsReady;
            }

            return false;
        }

        private void RenderRoomCode(UiBuilder ui)
        {
            UiBuilder row = ui.Row(10f, expand: false);
            UiBuilder.Fix(row.Label(F($"Oda kodu: {_lobby.RoomId}"), UiTextStyle.Small), -1f).flexibleWidth = 1f;
            UiBuilder.Fix(row.Button(L("Kopyala"), () =>
            {
                GUIUtility.systemCopyBuffer = _lobby.RoomId.ToString();
                _message = L("Oda kodu kopyalandı; arkadaşına gönder, menüden koduyla katılsın.");
            }, height: UiTheme.SmallButtonHeight), width: 110f);
        }

        /// <summary>The owner's house rules, the same line on every member's screen; only the owner can change them.</summary>
        private void RenderTableSummary(UiBuilder ui)
        {
            UiBuilder row = ui.Row(10f, expand: false);
            UiBuilder.Fix(row.Label(F($"Masa: {TableSetup.Summary(_lobby.Table, _boardMoney)}"), UiTextStyle.Small, UiTheme.Muted), -1f).flexibleWidth = 1f;
            if (_lobby.IsOwner && _openTableRules != null) UiBuilder.Fix(row.Button(L("Değiştir"), _openTableRules, height: UiTheme.SmallButtonHeight), width: 110f);
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

            if (shown == 0) list.Label(L("Odası açık arkadaşın yok. Arkadaşın oda kurunca burada görünür, davet gönderebilir ya da oda kodunu paylaşabilir."), UiTextStyle.Small);
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
                case LobbyError.Unavailable: _problem = L("Steam açık değil. Steam'i açıp oyunu oradan başlat."); break;
                case LobbyError.CreateFailed: _problem = L("Oda kurulamadı. Steam bağlantını kontrol edip yeniden dene."); break;
                case LobbyError.LobbyFull: _problem = F($"Oda dolu: en çok {RulesConfig.AbsoluteMaxPlayers} kişi oynayabilir. Ev sahibinden yeni bir oda kurmasını iste."); break;
                default: _problem = L("Odaya katılınamadı. Kod yanlış olabilir ya da oda kapanmış; arkadaşından yeni kodu iste."); break;
            }
        }
    }
}
