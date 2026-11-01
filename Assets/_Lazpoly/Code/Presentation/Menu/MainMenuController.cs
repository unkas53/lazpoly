using System;
using System.Collections.Generic;
using Lazpoly.App.AI;
using Lazpoly.App.Lobby;
using Lazpoly.App.Presence;
using Lazpoly.App.Save;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;
using Lazpoly.Infra;
using Lazpoly.Infra.Settings;
using Lazpoly.Net.Transport;
using Lazpoly.Presentation.Audio;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Effects;
using Lazpoly.Presentation.Perf;
using Lazpoly.Presentation.Setup;
using Lazpoly.Presentation.Texts;
using Lazpoly.Presentation.Tabletop;
using Lazpoly.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Menu
{
    /// <summary>
    /// Main menu. The first screen asks only how to play (alone against the computer, together on this
    /// machine, or online) and offers the saved game; the chosen mode then gets its own setup screen.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        private enum Mode { Home, Single, Local, Online }

        private static readonly string[] DifficultyNames = { "Kolay", "Orta", "Zor" };

        /// <summary>What sits in a local seat: a person (0) or the computer at a difficulty.</summary>
        private static readonly string[] SeatKinds = { "İnsan", "AI: Kolay", "AI: Orta", "AI: Zor" };

        private readonly List<string> _names = new List<string>();
        private readonly List<int> _seatKinds = new List<int>();
        private int _playerCount = 2;
        private UiView _titleView;
        private UiView _mainView;
        private Mode _mode = Mode.Home;
        private int _opponents = 3;
        private AiDifficulty _difficulty = AiDifficulty.Normal;
        private UiView _footerView;
        private UiView _settingsView;
        private GameObject _settingsLayer;
        private UiView _tableView;
        private GameObject _tableLayer;
        private int _boardMoney;
        private BoardData _boardData;
        private UiView _howToView;
        private GameObject _howToLayer;
        private LobbyPanel _lobbyPanel;
        private SavedGame _savedGame;
        private bool _brokenSave;
        private bool _rejoining;
        private SettingsPanel _settings;
        private SettingsService _settingsService;
        private ILobbyService _lobby;
        private IPresenceService _presence;

        private void Awake()
        {
            GameServices.Registry?.TryGet(out _settingsService);
            if (_settingsService != null)
            {
                Lang.Apply(_settingsService.Current.Language);
                _settingsService.Changed += OnSettingsChanged;
            }

            AudioPlayer.Ensure();
            MusicPlayer.Ensure();
            for (int i = 0; i < RulesConfig.AbsoluteMaxPlayers; i++)
            {
                _names.Add(F($"Oyuncu {i + 1}"));
                _seatKinds.Add(0);
            }

            FindSavedGame();
            _boardData = BoardDataLoader.LoadFromResources();
            _boardMoney = _boardData.Rules.StartingMoney;

            ITransport transport = null;
            GameServices.Registry?.TryGet(out _lobby);
            GameServices.Registry?.TryGet(out transport);
            GameServices.Registry?.TryGet(out _presence);
            _lobbyPanel = new LobbyPanel(_lobby, transport, () => SceneFader.Load("Game"), OpenTableRules, _boardMoney);
            if (_lobby != null) _lobby.Failed += OnLobbyFailed;
            _settings = new SettingsPanel();
        }

        private void OnDestroy()
        {
            if (_settingsService != null) _settingsService.Changed -= OnSettingsChanged;
            if (_lobby != null) _lobby.Failed -= OnLobbyFailed;
            _lobbyPanel?.Dispose();
        }

        /// <summary>A room that cannot be re-entered is gone; the offer to go back to it goes too.</summary>
        private void OnLobbyFailed(LobbyError error)
        {
            if (!_rejoining) return;
            _rejoining = false;
            LastRoomStore.Forget();
        }

        private static void OnSettingsChanged(GameSettings settings)
        {
            Lang.Apply(settings.Language);
            UiRoot.ApplyScale(settings.UiScaleFactor);
        }

        private void Update()
        {
            _lobbyPanel.Update();
            // Joining a room from a Steam invite lands on the online screen, whatever was showing.
            if (_lobby != null && _lobby.Status != LobbyStatus.Idle) _mode = Mode.Online;
            if (_presence == null) return;
            bool inRoom = _lobby != null && _lobby.Status == LobbyStatus.InLobby;
            _presence.Show(inRoom ? PresenceText.InRoom(_lobby.Members.Count, RulesConfig.AbsoluteMaxPlayers) : PresenceText.MainMenu());
        }

        private void Start()
        {
            if (PerfRun.Requested && !PerfRun.Started)
            {
                // Started with -perf: straight to a full table of computers; the game scene measures and quits.
                PerfRun.MarkStarted();
                LocalGameSetup.SetSeats(PerfRun.Seats(), PerfRun.Ai());
                SceneFader.Load("Game");
            }

            MenuBackdrop.Create(transform, _boardData);
            Canvas canvas = UiRoot.CreateCanvas(transform, "Main Menu UI", 0);
            UiRoot.Background(UiRoot.Stretch(canvas.transform, "Backdrop"), UiTheme.Veil, rounded: false);

            RectTransform title = UiRoot.Rect(canvas.transform, "Title", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-600f, -160f), new Vector2(600f, -30f));
            _titleView = new UiView(new UiBuilder(title), RenderTitle);
            UiBuilder.AddColumnLayout(title.gameObject, 0, 2f);

            RectTransform main = UiRoot.Rect(canvas.transform, "Main Panel", new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-420f, 120f), new Vector2(420f, -176f));
            UiRoot.Panel(main);
            _mainView = new UiView(UiRoot.ScrollPanel(main, 28), RenderMain);

            // The build's version in the corner, so a bug report or a friend's "which one do you have?" has an answer.
            RectTransform version = UiRoot.Rect(canvas.transform, "Version", Vector2.one, Vector2.one, new Vector2(-260f, -40f), new Vector2(-16f, -12f));
            Text versionLabel = new UiBuilder(version).Label(F($"Sürüm {Application.version}"), UiTextStyle.Small, UiTheme.Muted, TextAnchor.MiddleRight);
            var versionRect = (RectTransform)versionLabel.transform;
            versionRect.anchorMin = Vector2.zero;
            versionRect.anchorMax = Vector2.one;
            versionRect.offsetMin = Vector2.zero;
            versionRect.offsetMax = Vector2.zero;

            RectTransform footer = UiRoot.Rect(canvas.transform, "Footer", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-600f, 40f), new Vector2(600f, 96f));
            _footerView = new UiView(new UiBuilder(footer), RenderFooter);
            var row = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = UiTheme.Spacing * 2f;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;

            _settingsView = new UiView(UiRoot.Modal(canvas.transform, "Settings", 520f, out _settingsLayer), ui => _settings.Render(ui, CloseSettings));
            _settingsLayer.SetActive(false);
            _tableView = new UiView(UiRoot.Modal(canvas.transform, "Table Rules", 560f, out _tableLayer), RenderTableRules);
            _tableLayer.SetActive(false);
            // The page describes the table rules chosen for the next game.
            _howToView = new UiView(UiRoot.Modal(canvas.transform, "How To Play", HowToPlayPanel.Width, out _howToLayer),
                ui => HowToPlayPanel.Render(ui, _boardData.Board, _boardData.Rules.WithTable(TableSetup.Chosen), () => _howToLayer.SetActive(false)));
            _howToLayer.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_mainView == null) return;
            _titleView.Refresh(0);
            int modeKey = _mode == Mode.Online ? _lobbyPanel.ViewKey
                : HashCode.Combine(_playerCount, _opponents, _difficulty, _savedGame != null, _brokenSave, CanRejoin(out _));
            _mainView.Refresh(HashCode.Combine(_mode, modeKey, TableKey()));
            _footerView.Refresh(0);
            if (_settingsLayer.activeSelf) _settingsView.Refresh(0);
            if (_tableLayer.activeSelf) _tableView.Refresh(TableKey());
            if (_howToLayer.activeSelf) _howToView.Refresh(TableKey());
        }

        /// <summary>The game's mark: a flag beside the name, the tagline underneath.</summary>
        private void RenderTitle(UiBuilder ui)
        {
            UiBuilder row = ui.Row(16f, expand: false);
            var centre = row.Container.GetComponent<HorizontalLayoutGroup>();
            centre.childAlignment = TextAnchor.MiddleCenter;
            row.Icon(UiIcon.Flag, 64f, UiTheme.Accent);
            Text name = row.Label("LAZPOLY", UiTextStyle.Title, UiTheme.Accent, TextAnchor.MiddleCenter);
            UiRoot.AddShadow(name.gameObject, 3f);
            ui.Label(L("Arkadaşlarınla ya da bilgisayara karşı mülk topla, kirayı topla, son kalan sen ol."), UiTextStyle.Small, alignment: TextAnchor.MiddleCenter);
        }

        private void RenderMain(UiBuilder ui)
        {
            switch (_mode)
            {
                case Mode.Home: RenderHome(ui); break;
                case Mode.Single: RenderSingle(ui); break;
                case Mode.Local: RenderLocalGame(ui); break;
                case Mode.Online: RenderOnline(ui); break;
            }
        }

        private void RenderHome(UiBuilder ui)
        {
            if (CanRejoin(out ulong room))
            {
                ui.IconButton(UiIcon.Network, L("Çevrim İçi Oyuna Yeniden Bağlan"), () =>
                {
                    _rejoining = true;
                    _lobby.Join(room);
                }, style: UiButtonStyle.Primary, height: 60f);
                UiBuilder row = ui.Row(10f, expand: false);
                UiBuilder.Fix(row.Label(L("Son çevrim içi oyun kapanmadan koptu; odaya dönersen eski koltuğun seni bekler."), UiTextStyle.Small, UiTheme.Muted), -1f).flexibleWidth = 1f;
                UiBuilder.Fix(row.Button(L("Unut"), LastRoomStore.Forget, height: UiTheme.SmallButtonHeight), width: 90f);
                ui.Space(10f);
            }

            if (_savedGame != null)
            {
                ui.IconButton(UiIcon.Bookmark, L("Kayıtlı Oyuna Devam Et"), ResumeSavedGame, style: UiButtonStyle.Primary, height: 60f);
                ui.Label(SaveSummary(_savedGame, SaveGameStore.SavedAt), UiTextStyle.Small, UiTheme.Muted, TextAnchor.MiddleCenter);
                ui.Space(10f);
            }
            else if (_brokenSave)
            {
                ui.Notice(L("Kayıtlı oyun okunamadı; dosya bozulmuş ya da eski bir sürümden kalmış olabilir. Silip yeni bir oyun kurabilirsin."),
                    L("Kaydı Sil"), () =>
                    {
                        SaveGameStore.Delete();
                        _brokenSave = false;
                    });
                ui.Space(10f);
            }

            ui.Label(L("Nasıl oynamak istersin?"), UiTextStyle.Heading);
            ModeCard(ui, UiIcon.Dice, L("Tek Oyunculu"), L("Sen ve bilgisayar rakipler. Rakip sayısını ve zorluğu seçersin."), Mode.Single);
            ModeCard(ui, UiIcon.People, L("Aynı Bilgisayarda"), L("2-8 kişi aynı ekranda sırayla oynar; istenen koltuğa bilgisayar da oturabilir."), Mode.Local);
            ModeCard(ui, UiIcon.Network, L("Çevrim İçi (Steam)"), L("Oda kur, arkadaşlarını davet et ya da açık bir odaya katıl."), Mode.Online);
        }

        /// <summary>One way to play: an icon, the name and a line about it, all on one clickable tile.</summary>
        private void ModeCard(UiBuilder ui, UiIcon icon, string title, string description, Mode mode)
        {
            UiBuilder tile = ui.Tile(() => _mode = mode, padding: 16);
            UiBuilder row = tile.Row(16f, expand: false);
            row.Icon(icon, 48f, UiTheme.Accent);
            UiBuilder text = row.Column();
            text.Label(title, UiTextStyle.Heading);
            text.Label(description, UiTextStyle.Small);
        }

        private void BackButton(UiBuilder ui, string title)
        {
            UiBuilder row = ui.Row(12f, expand: false);
            UiBuilder.Fix(row.Button(L("< Geri"), () => _mode = Mode.Home, height: UiTheme.SmallButtonHeight), width: 110f);
            UiBuilder.Fix(row.Label(title, UiTextStyle.Heading), -1f).flexibleWidth = 1f;
            ui.Space(4f);
        }

        private void RenderTableLine(UiBuilder ui)
        {
            UiBuilder row = ui.Row(12f, expand: false);
            UiBuilder.Fix(row.Label(F($"Masa: {TableSetup.Summary(TableSetup.Chosen, _boardMoney)}"), UiTextStyle.Small, UiTheme.Muted), -1f).flexibleWidth = 1f;
            UiBuilder.Fix(row.Button(L("Değiştir"), OpenTableRules, height: UiTheme.SmallButtonHeight), width: 120f);
        }

        private void RenderSingle(UiBuilder ui)
        {
            BackButton(ui, L("Tek Oyunculu"));
            UiBuilder name = ui.Row();
            UiBuilder.Fix(name.Label(L("Adın")), width: 150f);
            name.Input(_names[0], text => _names[0] = text, 24);

            UiBuilder count = ui.Row();
            UiBuilder.Fix(count.Label(L("Rakip sayısı")), width: 150f);
            count.Label(_opponents.ToString(), UiTextStyle.Heading, UiTheme.Accent, TextAnchor.MiddleCenter);
            UiBuilder.Fix(count.Button("-", () => _opponents--, _opponents > 1), width: 52f);
            UiBuilder.Fix(count.Button("+", () => _opponents++, _opponents < RulesConfig.AbsoluteMaxPlayers - 1), width: 52f);

            UiBuilder level = ui.Row();
            UiBuilder.Fix(level.Label(L("Zorluk")), width: 150f);
            for (int i = 0; i < DifficultyNames.Length; i++)
            {
                var difficulty = (AiDifficulty)(i + 1);
                level.Button(L(DifficultyNames[i]), () => _difficulty = difficulty,
                    style: _difficulty == difficulty ? UiButtonStyle.Selected : UiButtonStyle.Secondary, height: UiTheme.SmallButtonHeight);
            }

            ui.Space(6f);
            RenderTableLine(ui);
            ui.Button(L("Oyunu Başlat"), StartSingleGame, style: UiButtonStyle.Primary, height: 56f);
        }

        private void RenderOnline(UiBuilder ui)
        {
            // Leaving the screen while in a room would hide the room, so going back is offered only outside one.
            if (_lobby == null || _lobby.Status == LobbyStatus.Idle) BackButton(ui, "");
            _lobbyPanel.Render(ui);
        }

        private void RenderLocalGame(UiBuilder ui)
        {
            BackButton(ui, L("Aynı Bilgisayarda"));
            UiBuilder count = ui.Row();
            count.Label(F($"Oyuncu sayısı: {_playerCount}"));
            UiBuilder.Fix(count.Button("-", () => _playerCount--, _playerCount > RulesConfig.AbsoluteMinPlayers), width: 52f);
            UiBuilder.Fix(count.Button("+", () => _playerCount++, _playerCount < RulesConfig.AbsoluteMaxPlayers), width: 52f);

            for (int i = 0; i < _playerCount; i++)
            {
                int seat = i;
                UiBuilder row = ui.Row();
                row.Swatch(PlayerPalette.ForSeat(seat), 22f);
                row.Input(_names[seat], text => _names[seat] = text, 24);
                UiBuilder.Fix(row.Button(L(SeatKinds[_seatKinds[seat]]), () => _seatKinds[seat] = (_seatKinds[seat] + 1) % SeatKinds.Length,
                    style: _seatKinds[seat] == 0 ? UiButtonStyle.Secondary : UiButtonStyle.Selected, height: UiTheme.FieldHeight), width: 130f);
            }

            ui.Label(L("Koltuktaki düğmeye basarak insan ya da bilgisayar (Kolay, Orta, Zor) seçersin."), UiTextStyle.Small, UiTheme.Muted);
            ui.Space(6f);
            RenderTableLine(ui);
            ui.Button(L("Oyunu Başlat"), StartLocalGame, style: UiButtonStyle.Primary, height: 56f);
        }

        private static int TableKey()
        {
            TableRules table = TableSetup.Chosen;
            return HashCode.Combine(table.StartingMoney, table.AuctionsEnabled, table.RoundLimit);
        }

        /// <summary>House rules for the next new game, local or hosted online.</summary>
        private void RenderTableRules(UiBuilder ui)
        {
            TableRules table = TableSetup.Chosen;
            ui.Title(UiIcon.List, L("Masa Kuralları"));
            ui.Label(L("Yeni yerel oyunlarda ve kurduğun çevrim içi odalarda geçerli. Kayıtlı oyun kendi kurallarıyla sürer."), UiTextStyle.Small, UiTheme.Muted);

            ui.Label(L("Başlangıç parası"));
            UiBuilder money = ui.Row();
            foreach (int choice in TableSetup.StartingMoneyChoices)
            {
                int value = choice;
                money.Button(TableSetup.StartingMoneyText(value, _boardMoney), () => TableSetup.Chosen = TableSetup.Chosen.WithStartingMoney(value),
                    style: table.StartingMoney == value ? UiButtonStyle.Selected : UiButtonStyle.Secondary, height: UiTheme.SmallButtonHeight);
            }

            ui.Label(L("Oyun süresi"));
            UiBuilder rounds = ui.Row();
            foreach (int choice in TableSetup.RoundLimitChoices)
            {
                int value = choice;
                rounds.Button(TableSetup.RoundLimitText(value), () => TableSetup.Chosen = TableSetup.Chosen.WithRoundLimit(value),
                    style: table.RoundLimit == value ? UiButtonStyle.Selected : UiButtonStyle.Secondary, height: UiTheme.SmallButtonHeight);
            }

            ui.Label(table.RoundLimit > 0 ? L("Sınır dolunca parası, mülkleri ve binaları en değerli olan kazanır.") : L("Tek bir oyuncu kalana kadar sürer."),
                UiTextStyle.Small, UiTheme.Muted);
            ui.Toggle(L("Alınmayan mülk açık artırmaya çıksın"), table.AuctionsEnabled, on => TableSetup.Chosen = TableSetup.Chosen.WithAuctions(on));
            ui.Space(6f);
            ui.Button(L("Tamam"), () => _tableLayer.SetActive(false), style: UiButtonStyle.Primary);
        }

        private void RenderFooter(UiBuilder ui)
        {
            ui.IconButton(UiIcon.Book, L("Nasıl Oynanır"), () =>
            {
                _howToLayer.SetActive(true);
                _howToView.Invalidate();
            });
            ui.IconButton(UiIcon.List, L("Masa Kuralları"), OpenTableRules);
            ui.IconButton(UiIcon.Sliders, L("Ayarlar"), OpenSettings);
            ui.IconButton(UiIcon.Door, L("Çıkış"), Application.Quit, style: UiButtonStyle.Danger);
        }

        private void OpenTableRules()
        {
            _tableLayer.SetActive(true);
            _tableView.Invalidate();
        }

        private void OpenSettings()
        {
            _settingsLayer.SetActive(true);
            _settingsView.Invalidate();
        }

        private void CloseSettings() => _settingsLayer.SetActive(false);

        /// <summary>"Tur 12 · 4 oyuncu · 01.11.2026 18:30": what the save holds, so the player knows which game it is.</summary>
        public static string SaveSummary(SavedGame save, DateTime? savedAt)
        {
            int players = 0;
            foreach (GameStateSnapshot.PlayerEntry player in save.Snapshot.Players) players += player.IsBankrupt ? 0 : 1;
            string when = savedAt.HasValue ? savedAt.Value.ToString("dd.MM.yyyy HH:mm") : "";
            string line = F($"Tur {save.Snapshot.Round} · {players} oyuncu");
            return when.Length > 0 ? $"{line} · {when}" : line;
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
            SceneFader.Load("Game");
        }

        private void StartSingleGame()
        {
            var seats = new PlayerSeat[_opponents + 1];
            var ai = new Dictionary<int, AiDifficulty>();
            seats[0] = new PlayerSeat(1, string.IsNullOrWhiteSpace(_names[0]) ? L("Oyuncu 1") : _names[0].Trim());
            for (int i = 1; i <= _opponents; i++)
            {
                seats[i] = new PlayerSeat(i + 1, F($"Bilgisayar {i}"));
                ai.Add(i + 1, _difficulty);
            }

            LocalGameSetup.SetSeats(seats, ai);
            SceneFader.Load("Game");
        }

        /// <summary>Whether a guest seat in an unfinished online game is remembered and Steam could take us back to it.</summary>
        private bool CanRejoin(out ulong room)
        {
            room = 0;
            return _lobby != null && _lobby.IsAvailable && _lobby.Status == LobbyStatus.Idle && LastRoomStore.TryGet(out room);
        }

        /// <summary>Offers the save only if it was made with the board data this build ships.</summary>
        private void FindSavedGame()
        {
            if (!SaveGameStore.TryRead(out SavedGame save))
            {
                _brokenSave = SaveGameStore.Exists;
                return;
            }

            BoardData data = BoardDataLoader.LoadFromResources();
            if (save.BoardChecksum == data.Checksum) _savedGame = save;
            else _brokenSave = true; // Made with other board data: this build cannot continue it.
        }

        private void ResumeSavedGame()
        {
            LocalGameSetup.SetResume(_savedGame);
            SceneFader.Load("Game");
        }
    }
}
