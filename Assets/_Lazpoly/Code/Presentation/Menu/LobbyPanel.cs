using System;
using System.Collections.Generic;
using Lazpoly.App.Lobby;
using Lazpoly.Core.Rules;
using Lazpoly.Net.Messages;
using Lazpoly.Net.Transport;
using Lazpoly.Presentation.Setup;
using Lazpoly.Presentation.Tabletop;
using Lazpoly.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Menu
{
    /// <summary>
    /// Online part of the main menu: create a room or join one by code, invite friends, see who is
    /// in and who is ready, read the house rules the owner picked, and start. Outside a room it is a
    /// sheet in the middle of the window; in a room it fills the window with three columns (the table
    /// settings, the seats, the friends) and the window's footer carries the big ready/start button.
    /// Members other than the owner follow the owner into the game automatically.
    /// </summary>
    public sealed class LobbyPanel : IDisposable
    {
        /// <summary>Seats in a row of the seat grid.</summary>
        public const int SeatsPerRow = 4;

        private const float SettingsWidth = 380f;
        private const float FriendsWidth = 360f;
        private const float FriendListHeight = 236f;

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

        /// <summary>Whether this machine sits in a room right now (the lobby fills the window).</summary>
        public bool InRoom => IsAvailable && _lobby.Status == LobbyStatus.InLobby;

        public void Dispose()
        {
            if (_lobby != null) _lobby.Failed -= OnFailed;
        }

        /// <summary>Friends as last loaded; reloaded whenever the room status changes or on request.</summary>
        public IReadOnlyList<LobbyFriend> Friends => _friends;

        /// <summary>The "Özel Masa" card: opens a room at once when Steam is there and we are not in one.</summary>
        public void HostRoom()
        {
            if (!IsAvailable || _lobby.Status != LobbyStatus.Idle) return;
            _message = null;
            _lobby.Create(RulesConfig.AbsoluteMaxPlayers);
        }

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
                key = HashCode.Combine(key, _lobby.RoomProtocolVersion, _lobby.IsOverlayAvailable);
                foreach (LobbyMember member in _lobby.Members) key = HashCode.Combine(key, member.Id, member.Name, member.IsReady, member.ProtocolVersion);
                return key;
            }
        }

        /// <summary>The online page: the join sheet, the connecting note, or the room's three columns.</summary>
        public void Render(UiBuilder ui)
        {
            if (!IsAvailable)
            {
                ui.Title(UiIcon.Network, L("Çevrim içi (Steam)"));
                ui.Notice(L("Steam açık değil. Çevrim içi oynamak için Steam'i açıp oyunu oradan başlat."));
                return;
            }

            switch (_lobby.Status)
            {
                case LobbyStatus.Idle:
                    RenderJoin(ui);
                    break;

                case LobbyStatus.Creating:
                case LobbyStatus.Joining:
                    ui.Title(UiIcon.Network, L("Çevrim içi (Steam)"));
                    if (_problem != null) ui.Notice(_problem, L("Tamam"), () => _problem = null);
                    ui.Label(L("Odaya bağlanılıyor..."));
                    ui.Button(L("Vazgeç"), _lobby.Leave);
                    break;

                case LobbyStatus.InLobby:
                    RenderRoom(ui);
                    break;
            }
        }

        /// <summary>Outside a room: open one, or join a friend's by code or from the list.</summary>
        private void RenderJoin(UiBuilder ui)
        {
            ui.Eyebrow(L("Çevrim içi (Steam)"), UiTheme.Gain);
            ui.Label(L("Arkadaşlarla oyna"), UiTextStyle.Heading);
            ui.Label(L("Bir oda kur ve arkadaşlarını çağır, ya da açık bir odaya katıl."), UiTextStyle.Small, UiTheme.Muted);
            if (_problem != null) ui.Notice(_problem, L("Tamam"), () => _problem = null);
            ui.Space(4f);
            ui.IconButton(UiIcon.Network, L("Oda Kur"), HostRoom, style: UiButtonStyle.Primary, height: 56f);
            ui.Space(8f);

            ui.Eyebrow(L("Oda koduyla katıl"), UiTheme.Muted);
            UiBuilder row = ui.Row();
            row.Input(_code, text => _code = text, 20, InputField.ContentType.IntegerNumber);
            UiBuilder.Fix(row.Button(L("Katıl"), JoinByCode, height: UiTheme.FieldHeight), width: 130f);

            ui.Eyebrow(L("Odası açık arkadaşlar"), UiTheme.Muted);
            UiBuilder list = ui.Scroll(150f);
            int shown = 0;
            foreach (LobbyFriend friend in _friends)
            {
                if (friend.LobbyId == 0) continue;
                shown++;
                UiBuilder line = list.Row(10f, expand: false);
                line.Swatch(ColorOf(shown), 30f, round: true, mark: Initial(friend.Name));
                UiBuilder.Fix(line.Label(friend.Name), -1f).flexibleWidth = 1f;
                UiBuilder.Fix(line.Button(L("Katıl"), () =>
                {
                    _message = null;
                    _lobby.Join(friend.LobbyId);
                }, style: UiButtonStyle.Primary, height: UiTheme.SmallButtonHeight), width: 100f);
            }

            if (shown == 0) list.Label(L("Odası açık arkadaşın yok. Arkadaşın oda kurunca burada görünür, davet gönderebilir ya da oda kodunu paylaşabilir."), UiTextStyle.Small);
            UiBuilder foot = ui.Row(10f, expand: false);
            UiBuilder.Fix(foot.Label(_message ?? "", UiTextStyle.Small, UiTheme.Accent), -1f).flexibleWidth = 1f;
            UiBuilder.Fix(foot.Button(L("Listeyi Yenile"), RefreshFriends, height: UiTheme.SmallButtonHeight), width: 150f);
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

        /// <summary>In a room: the settings column, the seats and the friends side by side.</summary>
        private void RenderRoom(UiBuilder ui)
        {
            if (_problem != null) ui.Notice(_problem, L("Tamam"), () => _problem = null);
            if (RoomIsOutdated)
            {
                ui.Notice(F($"Ev sahibinin oyun sürümü seninkinden farklı (protokol {_lobby.RoomProtocolVersion}, seninki {MessageCodec.ProtocolVersion}). Birlikte oynamak için ikiniz de Steam'den son güncellemeyi alın."));
            }
            else if (_lobby.IsOwner && OutdatedCount() > 0)
            {
                ui.Notice(L("Sürümü farklı olan oyuncular Steam'den güncelleme almadan oyun başlatılamaz."));
            }

            UiBuilder columns = ui.Row(MenuLayout.CardGap, expand: false);
            columns.Container.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;

            UiBuilder settings = columns.Card(UiTheme.Card, 18);
            UiBuilder.Fix(settings.Container.gameObject, width: SettingsWidth);
            RenderSettings(settings);

            UiBuilder seats = columns.Card(UiTheme.Card, 18);
            UiBuilder.Fix(seats.Container.gameObject, -1f).flexibleWidth = 1f;
            RenderSeats(seats);

            UiBuilder friends = columns.Card(UiTheme.Card, 18);
            UiBuilder.Fix(friends.Container.gameObject, width: FriendsWidth);
            RenderFriends(friends);
        }

        /// <summary>A column's heading: an icon in a tinted disc, the title and a small tag at the right.</summary>
        private static void ColumnHeading(UiBuilder ui, UiIcon icon, Color tint, string title, string tag)
        {
            UiBuilder row = ui.Row(10f, expand: false);
            row.Icon(icon, 26f, tint);
            Text label = row.Label(Upper(title), UiTextStyle.Body, UiTheme.Text);
            label.font = UiTheme.BoldFont;
            UiBuilder.Fix(label, -1f).flexibleWidth = 1f;
            row.Chip(tag, UiPalette.Ink.Wash(tint), UiPalette.Ink.Deep(tint), 26f);
            ui.Rule();
        }

        private void RenderSettings(UiBuilder ui)
        {
            ColumnHeading(ui, UiIcon.List, UiPalette.Ink.Stripes[4], L("Masa ayarları"), _lobby.IsOwner ? L("Ev sahibi") : L("Salt okunur"));
            TableRulesForm.Render(ui, _lobby.Table, _lobby.IsOwner, _boardMoney, table => TableSetup.Chosen = table, explain: false);
            ui.Space(4f);
            ui.Label(_lobby.IsOwner ? L("Seçimlerin odadaki herkese anında görünür.") : L("Masa kurallarını ev sahibi belirler; burada görürsün."),
                UiTextStyle.Small, UiTheme.Muted);
            if (_lobby.IsOwner && _openTableRules != null) ui.Button(L("Açıklamalarıyla Gör"), _openTableRules, height: UiTheme.SmallButtonHeight);
        }

        private void RenderSeats(UiBuilder ui)
        {
            int capacity = RulesConfig.AbsoluteMaxPlayers;
            ColumnHeading(ui, UiIcon.People, UiPalette.Ink.Stripes[3], L("Oyuncu koltukları"), F($"{_lobby.Members.Count} / {capacity} dolu"));
            IReadOnlyList<LobbyMember> order = SeatOrder();
            int rows = (capacity + SeatsPerRow - 1) / SeatsPerRow;
            bool inviteOffered = false;
            for (int row = 0; row < rows; row++)
            {
                UiBuilder line = ui.Row(12f);
                line.Container.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;
                for (int column = 0; column < SeatsPerRow; column++)
                {
                    int seat = row * SeatsPerRow + column;
                    if (seat >= capacity) break;
                    if (seat < order.Count) RenderSeat(line, seat, order[seat]);
                    else
                    {
                        RenderEmptySeat(line, !inviteOffered);
                        inviteOffered = true;
                    }
                }
            }

            RenderRoomCode(ui);
        }

        /// <summary>Members as the game will seat them: the owner first, then lobby order (<see cref="OnlineSeatPlan"/>).</summary>
        private IReadOnlyList<LobbyMember> SeatOrder()
        {
            var order = new List<LobbyMember>(_lobby.Members.Count);
            foreach (LobbyMember member in _lobby.Members) if (member.Id == _lobby.OwnerId) order.Add(member);
            foreach (LobbyMember member in _lobby.Members) if (member.Id != _lobby.OwnerId) order.Add(member);
            return order;
        }

        private void RenderSeat(UiBuilder row, int seat, LobbyMember member)
        {
            bool owner = member.Id == _lobby.OwnerId;
            bool me = member.Id == _lobby.LocalId;
            bool outdated = IsOutdated(member);
            Color color = PlayerPalette.ForSeat(seat);
            // Each band is dark enough for its white capitals: the gold and the green are deepened.
            Color band = outdated ? UiTheme.Warning : owner ? UiPalette.Ink.Masthead : member.IsReady ? UiPalette.Ink.Deep(UiTheme.Gain, 0.2f) : UiPalette.Ink.Deep(UiPalette.Ink.Stripes[3]);
            string status = outdated ? L("Sürümü farklı") : owner ? L("Ev sahibi") : member.IsReady ? L("Hazır") : L("Bekliyor");

            UiBuilder card = row.Card(UiTheme.Panel, 10);
            card.Chip(status, band, Color.white, 28f);
            card.Space(2f);
            Centred(card).Swatch(color, 64f, round: true, mark: Initial(member.Name));
            Text name = card.Label(member.Name, UiTextStyle.Body, UiTheme.Text, TextAnchor.MiddleCenter);
            name.font = UiTheme.BoldFont;
            card.Label(me ? L("Senin koltuğun") : owner ? L("Masayı kurdu") : member.IsReady ? L("Oyuna hazır") : L("Hazır olması bekleniyor"),
                UiTextStyle.Small, UiTheme.Muted, TextAnchor.MiddleCenter);
            UiBuilder pawn = Centred(card, 6f);
            pawn.Label(L("Piyon"), UiTextStyle.Small, UiTheme.Muted);
            pawn.Swatch(color, 16f, round: true);
        }

        private void RenderEmptySeat(UiBuilder row, bool offerInvite)
        {
            UiBuilder card = row.Card(UiTheme.Field, 10);
            card.Chip(L("Boş koltuk"), UiTheme.RestingFill, UiTheme.Muted, 28f);
            card.Space(2f);
            Centred(card).Swatch(UiTheme.RestingFill, 64f, round: true, mark: "+");
            card.Label(L("Arkadaşını çağır"), UiTextStyle.Small, UiTheme.Muted, TextAnchor.MiddleCenter);
            if (offerInvite && _lobby.IsOverlayAvailable) card.Button(L("Davet Et"), _lobby.InviteFriends, height: UiTheme.SmallButtonHeight);
            else card.Label(L("Oda koduyla da girilir."), UiTextStyle.Small, UiTheme.Muted, TextAnchor.MiddleCenter);
        }

        /// <summary>A row whose content sits in the middle, for an avatar or a pair of small things.</summary>
        private static UiBuilder Centred(UiBuilder ui, float spacing = 0f)
        {
            UiBuilder row = ui.Row(spacing, expand: false);
            row.Container.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            return row;
        }

        private void RenderRoomCode(UiBuilder ui)
        {
            UiBuilder bar = ui.Card(UiTheme.Field, 12);
            UiBuilder row = bar.Row(12f, expand: false);
            UiBuilder code = row.Column();
            code.Eyebrow(L("Masa kodu"), UiTheme.Muted);
            Text number = code.Label(_lobby.RoomId.ToString(), UiTextStyle.Heading, UiTheme.Text);
            number.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiBuilder.Fix(row.Label(L("Arkadaşlarına bu kodu göndererek davet et; menüdeki \"Odaya Katıl\" kutusuna yazarlar."), UiTextStyle.Small, UiTheme.Muted), -1f).flexibleWidth = 1f;
            UiBuilder.Fix(row.Button(L("Kopyala"), () =>
            {
                GUIUtility.systemCopyBuffer = _lobby.RoomId.ToString();
                _message = L("Oda kodu kopyalandı; arkadaşına gönder, menüden koduyla katılsın.");
            }, height: UiTheme.SmallButtonHeight), width: 130f);
            if (_message != null) bar.Label(_message, UiTextStyle.Small, UiTheme.Gain);
        }

        private void RenderFriends(UiBuilder ui)
        {
            int online = 0;
            foreach (LobbyFriend friend in _friends) online += friend.IsOnline ? 1 : 0;
            ColumnHeading(ui, UiIcon.People, UiPalette.Ink.Stripes[1], L("Arkadaşlar"), F($"{online} çevrim içi"));
            if (_lobby.IsOverlayAvailable) ui.IconButton(UiIcon.People, L("Steam ile Davet Et"), _lobby.InviteFriends, style: UiButtonStyle.Primary);

            UiBuilder list = ui.Scroll(FriendListHeight);
            int shown = 0;
            foreach (LobbyFriend friend in _friends)
            {
                if (!friend.IsOnline || _lobby.IsMember(friend.Id)) continue;
                shown++;
                UiBuilder row = list.Row(10f, expand: false);
                row.Swatch(ColorOf(shown), 36f, round: true, mark: Initial(friend.Name));
                UiBuilder text = row.Column(0f);
                Text name = text.Label(friend.Name, UiTextStyle.Small, UiTheme.Text);
                name.font = UiTheme.BoldFont;
                text.Label(friend.LobbyId != 0 ? L("Bir odada") : L("Çevrim içi"), UiTextStyle.Small, UiTheme.Muted);
                UiBuilder.Fix(row.Button(L("Davet"), () => InviteFriend(friend), height: UiTheme.SmallButtonHeight), width: 90f);
            }

            if (shown == 0) list.Label(L("Çevrim içi arkadaş bulunamadı."), UiTextStyle.Small);
            ui.Button(L("Listeyi Yenile"), RefreshFriends, height: UiTheme.SmallButtonHeight);

            UiBuilder tip = ui.Card(UiTheme.Field, 12);
            tip.Eyebrow(L("Masanı doldur"), UiTheme.Text);
            tip.Label(L("Davet Steam sohbetine düşer; Lazpoly açıkken kabul eden doğrudan odaya gelir. Boş koltuk kalırsa oyun o kadar kişiyle başlar."),
                UiTextStyle.Small, UiTheme.Muted);
        }

        /// <summary>The window's footer while in a room: where things stand, the way out and the big button.</summary>
        public void RenderFooter(UiBuilder ui)
        {
            if (!InRoom) return;
            int ready = ReadyCount();
            int count = _lobby.Members.Count;
            bool enough = count >= RulesConfig.AbsoluteMinPlayers;
            bool everyone = ready == count;

            UiBuilder row = ui.Row(16f, expand: false);
            row.Container.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
            UiBuilder status = row.Column(0f);
            UiBuilder.Fix(status.Container.gameObject, -1f).flexibleWidth = 1f;
            if (_lobby.IsOwner)
            {
                status.Eyebrow(!enough ? L("En az 2 oyuncu gerekli.") : everyone ? L("Herkes hazır.") : F($"{ready}/{count} hazır; kalanlar bekleniyor."),
                    everyone && enough ? UiTheme.Gain : UiTheme.Text);
                status.Label(L("Herkes hazır olunca oyunu sen başlatırsın."), UiTextStyle.Small, UiTheme.Muted);
            }
            else
            {
                bool me = IsLocalReady();
                status.Eyebrow(me ? L("Hazırsın; ev sahibi bekleniyor.") : L("Masa seni bekliyor."), me ? UiTheme.Gain : UiTheme.Text);
                status.Label(L("Herkes hazır olunca ev sahibi oyunu başlatır."), UiTextStyle.Small, UiTheme.Muted);
            }

            UiBuilder.Fix(row.Button(L("Odadan Çık"), _lobby.Leave, style: UiButtonStyle.Danger, height: UiTheme.ButtonHeight), width: 160f);
            if (_lobby.IsOwner)
            {
                UiBuilder.Fix(row.Button(L("Oyunu Başlat  →"), StartAsHost, enough && everyone && OutdatedCount() == 0, UiButtonStyle.Primary, 56f), width: 300f);
            }
            else if (!RoomIsOutdated)
            {
                bool me = IsLocalReady();
                UiBuilder.Fix(row.Button(me ? L("Hazırsın  ✓") : L("Hazırım  →"), () => _lobby.SetReady(!me), style: me ? UiButtonStyle.Selected : UiButtonStyle.Primary, height: 56f), width: 300f);
            }
        }

        /// <summary>The first letter of a name, for an avatar disc.</summary>
        public static string Initial(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            string trimmed = name.Trim();
            return char.ToUpperInvariant(trimmed[0]).ToString();
        }

        private static Color ColorOf(int index) => PlayerPalette.ForSeat(index);

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
