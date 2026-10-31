using System;
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
            _boardData = BoardDataLoader.LoadFromResources();
            _boardMoney = _boardData.Rules.StartingMoney;

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
            // Joining a room from a Steam invite lands on the online screen, whatever was showing.
            if (_lobby != null && _lobby.Status != LobbyStatus.Idle) _mode = Mode.Online;
            if (_presence == null) return;
            bool inRoom = _lobby != null && _lobby.Status == LobbyStatus.InLobby;
            _presence.Show(inRoom ? PresenceText.InRoom(_lobby.Members.Count, RulesConfig.AbsoluteMaxPlayers) : PresenceText.MainMenu());
        }

        private void Start()
        {
            MenuBackdrop.Create(transform, _boardData);
            Canvas canvas = UiRoot.CreateCanvas(transform, "Main Menu UI", 0);
            UiRoot.Background(UiRoot.Stretch(canvas.transform, "Backdrop"), UiTheme.Veil, rounded: false);

            RectTransform title = UiRoot.Rect(canvas.transform, "Title", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-600f, -150f), new Vector2(600f, -40f));
            _titleView = new UiView(new UiBuilder(title), RenderTitle);
            UiBuilder.AddColumnLayout(title.gameObject, 0, 4f);

            RectTransform main = UiRoot.Rect(canvas.transform, "Main Panel", new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-420f, 120f), new Vector2(420f, -170f));
            UiRoot.Background(main, UiTheme.Panel);
            _mainView = new UiView(UiRoot.ScrollPanel(main, 28), RenderMain);

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
            _titleView.Refresh(_savedGame != null ? 1 : 0);
            int modeKey = _mode == Mode.Online ? _lobbyPanel.ViewKey : HashCode.Combine(_playerCount, _opponents, _difficulty);
            _mainView.Refresh(HashCode.Combine(_mode, modeKey, TableKey()));
            _footerView.Refresh(0);
            if (_settingsLayer.activeSelf) _settingsView.Refresh(0);
            if (_tableLayer.activeSelf) _tableView.Refresh(TableKey());
            if (_howToLayer.activeSelf) _howToView.Refresh(TableKey());
        }

        private void RenderTitle(UiBuilder ui)
        {
            ui.Label("LAZPOLY", UiTextStyle.Title, UiTheme.Accent, TextAnchor.MiddleCenter);
            ui.Label("Arkadaşlarınla ya da bilgisayara karşı mülk topla, kirayı topla, son kalan sen ol.", UiTextStyle.Small, alignment: TextAnchor.MiddleCenter);
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
            if (_savedGame != null)
            {
                ui.Button("Kayıtlı Oyuna Devam Et", ResumeSavedGame, style: UiButtonStyle.Primary, height: 60f);
                ui.Space(10f);
            }

            ui.Label("Nasıl oynamak istersin?", UiTextStyle.Heading);
            ModeCard(ui, "Tek Oyunculu", "Sen ve bilgisayar rakipler. Rakip sayısını ve zorluğu seçersin.", Mode.Single);
            ModeCard(ui, "Aynı Bilgisayarda", "2-8 kişi aynı ekranda sırayla oynar; istenen koltuğa bilgisayar da oturabilir.", Mode.Local);
            ModeCard(ui, "Çevrim İçi (Steam)", "Oda kur, arkadaşlarını davet et ya da açık bir odaya katıl.", Mode.Online);
        }

        private void ModeCard(UiBuilder ui, string title, string description, Mode mode)
        {
            UiBuilder card = ui.Card(padding: 14);
            card.Button(title, () => _mode = mode, style: UiButtonStyle.Primary, height: 56f);
            card.Label(description, UiTextStyle.Small);
        }

        private void BackButton(UiBuilder ui, string title)
        {
            UiBuilder row = ui.Row(12f, expand: false);
            UiBuilder.Fix(row.Button("< Geri", () => _mode = Mode.Home, height: UiTheme.SmallButtonHeight), width: 110f);
            UiBuilder.Fix(row.Label(title, UiTextStyle.Heading), -1f).flexibleWidth = 1f;
            ui.Space(4f);
        }

        private void RenderTableLine(UiBuilder ui)
        {
            UiBuilder row = ui.Row(12f, expand: false);
            UiBuilder.Fix(row.Label($"Masa: {TableSetup.Summary(TableSetup.Chosen, _boardMoney)}", UiTextStyle.Small, UiTheme.Muted), -1f).flexibleWidth = 1f;
            UiBuilder.Fix(row.Button("Değiştir", OpenTableRules, height: UiTheme.SmallButtonHeight), width: 120f);
        }

        private void RenderSingle(UiBuilder ui)
        {
            BackButton(ui, "Tek Oyunculu");
            UiBuilder name = ui.Row();
            UiBuilder.Fix(name.Label("Adın"), width: 150f);
            name.Input(_names[0], text => _names[0] = text, 24);

            UiBuilder count = ui.Row();
            UiBuilder.Fix(count.Label("Rakip sayısı"), width: 150f);
            count.Label(_opponents.ToString(), UiTextStyle.Heading, UiTheme.Accent, TextAnchor.MiddleCenter);
            UiBuilder.Fix(count.Button("-", () => _opponents--, _opponents > 1), width: 52f);
            UiBuilder.Fix(count.Button("+", () => _opponents++, _opponents < RulesConfig.AbsoluteMaxPlayers - 1), width: 52f);

            UiBuilder level = ui.Row();
            UiBuilder.Fix(level.Label("Zorluk"), width: 150f);
            for (int i = 0; i < DifficultyNames.Length; i++)
            {
                var difficulty = (AiDifficulty)(i + 1);
                level.Button(DifficultyNames[i], () => _difficulty = difficulty,
                    style: _difficulty == difficulty ? UiButtonStyle.Selected : UiButtonStyle.Secondary, height: UiTheme.SmallButtonHeight);
            }

            ui.Space(6f);
            RenderTableLine(ui);
            ui.Button("Oyunu Başlat", StartSingleGame, style: UiButtonStyle.Primary, height: 56f);
        }

        private void RenderOnline(UiBuilder ui)
        {
            // Leaving the screen while in a room would hide the room, so going back is offered only outside one.
            if (_lobby == null || _lobby.Status == LobbyStatus.Idle) BackButton(ui, "");
            _lobbyPanel.Render(ui);
        }

        private void RenderLocalGame(UiBuilder ui)
        {
            BackButton(ui, "Aynı Bilgisayarda");
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

            ui.Label("Koltuktaki düğmeye basarak insan ya da bilgisayar (Kolay, Orta, Zor) seçersin.", UiTextStyle.Small, UiTheme.Muted);
            ui.Space(6f);
            RenderTableLine(ui);
            ui.Button("Oyunu Başlat", StartLocalGame, style: UiButtonStyle.Primary, height: 56f);
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
            ui.Label("Masa Kuralları", UiTextStyle.Heading);
            ui.Label("Yeni yerel oyunlarda ve kurduğun çevrim içi odalarda geçerli. Kayıtlı oyun kendi kurallarıyla sürer.", UiTextStyle.Small, UiTheme.Muted);

            ui.Label("Başlangıç parası");
            UiBuilder money = ui.Row();
            foreach (int choice in TableSetup.StartingMoneyChoices)
            {
                int value = choice;
                money.Button(TableSetup.StartingMoneyText(value, _boardMoney), () => TableSetup.Chosen = TableSetup.Chosen.WithStartingMoney(value),
                    style: table.StartingMoney == value ? UiButtonStyle.Selected : UiButtonStyle.Secondary, height: UiTheme.SmallButtonHeight);
            }

            ui.Label("Oyun süresi");
            UiBuilder rounds = ui.Row();
            foreach (int choice in TableSetup.RoundLimitChoices)
            {
                int value = choice;
                rounds.Button(TableSetup.RoundLimitText(value), () => TableSetup.Chosen = TableSetup.Chosen.WithRoundLimit(value),
                    style: table.RoundLimit == value ? UiButtonStyle.Selected : UiButtonStyle.Secondary, height: UiTheme.SmallButtonHeight);
            }

            ui.Label(table.RoundLimit > 0 ? "Sınır dolunca parası, mülkleri ve binaları en değerli olan kazanır." : "Tek bir oyuncu kalana kadar sürer.",
                UiTextStyle.Small, UiTheme.Muted);
            ui.Toggle("Alınmayan mülk açık artırmaya çıksın", table.AuctionsEnabled, on => TableSetup.Chosen = TableSetup.Chosen.WithAuctions(on));
            ui.Space(6f);
            ui.Button("Tamam", () => _tableLayer.SetActive(false), style: UiButtonStyle.Primary);
        }

        private void RenderFooter(UiBuilder ui)
        {
            ui.Button("Nasıl Oynanır", () =>
            {
                _howToLayer.SetActive(true);
                _howToView.Invalidate();
            });
            ui.Button("Masa Kuralları", OpenTableRules);
            ui.Button("Ayarlar", OpenSettings);
            ui.Button("Çıkış", Application.Quit, style: UiButtonStyle.Danger);
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

        private void StartSingleGame()
        {
            var seats = new PlayerSeat[_opponents + 1];
            var ai = new Dictionary<int, AiDifficulty>();
            seats[0] = new PlayerSeat(1, string.IsNullOrWhiteSpace(_names[0]) ? "Oyuncu 1" : _names[0].Trim());
            for (int i = 1; i <= _opponents; i++)
            {
                seats[i] = new PlayerSeat(i + 1, $"Bilgisayar {i}");
                ai.Add(i + 1, _difficulty);
            }

            LocalGameSetup.SetSeats(seats, ai);
            SceneManager.LoadScene("Game");
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
