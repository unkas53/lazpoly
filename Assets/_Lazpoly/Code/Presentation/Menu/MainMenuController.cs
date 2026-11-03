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
    /// Main menu: one paper window over the board, with a green masthead, a greeting, six cards for the
    /// ways to play and a footer with the quickest way to a table. A card opens its mode's setup sheet
    /// in the middle of the window; an online room fills the window with the lobby's columns.
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
        private Mode _mode = Mode.Home;
        private int _opponents = 3;
        private AiDifficulty _difficulty = AiDifficulty.Normal;
        private UiView _mastheadView;
        private UiView _titleView;
        private UiView _cardsView;
        private UiView _sheetView;
        private UiView _footerView;
        private GameObject _home;
        private GameObject _sheet;
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
            UiTheme.Use(UiPalette.Paper);
            GameServices.Registry?.TryGet(out _settingsService);
            if (_settingsService != null)
            {
                Lang.Apply(_settingsService.Current.Language);
                ColorVision.Apply(_settingsService.Current.ColorBlind);
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
            if (Input.GetKeyDown(KeyCode.Escape)) CloseTopWindow();
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

            // The paper window: rounded, with a shadow, clipping the masthead's band to its corners.
            var margin = new Vector2(MenuLayout.WindowMargin, MenuLayout.WindowMargin);
            RectTransform window = UiRoot.Rect(canvas.transform, "Window", Vector2.zero, Vector2.one, margin, -margin);
            UiRoot.Background(window, UiTheme.Panel, shadow: true);
            window.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            RectTransform masthead = UiRoot.Rect(window, "Masthead", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -MenuLayout.MastheadHeight), Vector2.zero);
            UiRoot.Background(masthead, UiPalette.Ink.Masthead, rounded: false).raycastTarget = false;
            RectTransform edge = UiRoot.Rect(masthead, "Edge", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 4f));
            UiRoot.Background(edge, UiPalette.Ink.MastheadDeep, rounded: false).raycastTarget = false;
            RectTransform brand = UiRoot.Rect(masthead, "Brand", Vector2.zero, Vector2.one, new Vector2(MenuLayout.Padding, 0f), new Vector2(-MenuLayout.Padding, 0f));
            var line = brand.gameObject.AddComponent<HorizontalLayoutGroup>();
            line.spacing = 16f;
            line.childAlignment = TextAnchor.MiddleLeft;
            line.childControlWidth = true;
            line.childControlHeight = true;
            line.childForceExpandWidth = false;
            line.childForceExpandHeight = false;
            _mastheadView = new UiView(new UiBuilder(brand), RenderMasthead);

            // The page under the masthead: the home grid or a mode's sheet, and the footer line under either.
            RectTransform page = UiRoot.Rect(window, "Page", Vector2.zero, Vector2.one, new Vector2(MenuLayout.Padding, MenuLayout.Padding),
                new Vector2(-MenuLayout.Padding, -MenuLayout.MastheadHeight - MenuLayout.Padding));
            UiBuilder.AddColumnLayout(page.gameObject, 0, MenuLayout.RowGap);

            _home = UiRoot.Stretch(page, "Home").gameObject;
            UiBuilder.AddColumnLayout(_home, 0, MenuLayout.RowGap);
            Flexible(_home);
            RectTransform title = UiRoot.Stretch(_home.transform, "Title Strip");
            UiBuilder.Fix(title.gameObject, height: MenuLayout.TitleHeight);
            var strip = title.gameObject.AddComponent<HorizontalLayoutGroup>();
            strip.spacing = MenuLayout.CardGap;
            strip.childAlignment = TextAnchor.MiddleLeft;
            strip.childControlWidth = true;
            strip.childControlHeight = true;
            strip.childForceExpandWidth = false;
            strip.childForceExpandHeight = false;
            _titleView = new UiView(new UiBuilder(title), RenderTitle);
            RectTransform cards = UiRoot.Stretch(_home.transform, "Cards");
            UiBuilder.AddColumnLayout(cards.gameObject, 0, MenuLayout.CardGap);
            Flexible(cards.gameObject);
            _cardsView = new UiView(new UiBuilder(cards), RenderCards);

            RectTransform sheet = UiRoot.Stretch(page, "Sheet");
            Flexible(sheet.gameObject);
            _sheet = sheet.gameObject;
            _sheetView = new UiView(UiRoot.ScrollPanel(sheet, 0), RenderSheet);
            _sheet.SetActive(false);

            RectTransform footer = UiRoot.Stretch(page, "Footer");
            UiBuilder.Fix(footer.gameObject, height: MenuLayout.FooterHeight);
            UiBuilder.AddColumnLayout(footer.gameObject, 0, 0f);
            footer.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
            _footerView = new UiView(new UiBuilder(footer), RenderFooter);

            _settingsView = new UiView(UiRoot.Modal(canvas.transform, "Settings", 520f, out _settingsLayer), ui => _settings.Render(ui, CloseSettings));
            _settingsLayer.SetActive(false);
            _tableView = new UiView(UiRoot.Modal(canvas.transform, "Table Rules", 640f, out _tableLayer), RenderTableRules);
            _tableLayer.SetActive(false);
            // The page describes the table rules chosen for the next game.
            _howToView = new UiView(UiRoot.Modal(canvas.transform, "How To Play", HowToPlayPanel.Width, out _howToLayer),
                ui => HowToPlayPanel.Render(ui, _boardData.Board, _boardData.Rules.WithTable(TableSetup.Chosen), () => _howToLayer.SetActive(false)));
            _howToLayer.SetActive(false);
        }

        /// <summary>An element that takes the page's spare height.</summary>
        private static void Flexible(GameObject element)
        {
            LayoutElement layout = UiBuilder.Fix(element, -1f);
            layout.preferredHeight = 0f;
            layout.flexibleHeight = 1f;
        }

        private void LateUpdate()
        {
            if (_footerView == null) return;
            bool home = _mode == Mode.Home;
            if (_home.activeSelf != home)
            {
                _home.SetActive(home);
                _sheet.SetActive(!home);
            }

            _mastheadView.Refresh(HashCode.Combine(_lobbyPanel.IsAvailable, OnlineFriends(), _lobbyPanel.InRoom));
            if (home)
            {
                _titleView.Refresh(0);
                _cardsView.Refresh(HashCode.Combine(_lobbyPanel.IsAvailable, TableKey()));
            }
            else
            {
                int modeKey = _mode == Mode.Online ? _lobbyPanel.ViewKey : HashCode.Combine(_playerCount, _opponents, _difficulty);
                _sheetView.Refresh(HashCode.Combine(_mode, modeKey, TableKey()));
            }

            int footerKey = _lobbyPanel.InRoom ? _lobbyPanel.ViewKey : HashCode.Combine(_savedGame != null, _brokenSave, CanRejoin(out _), _opponents, _difficulty);
            _footerView.Refresh(HashCode.Combine(_mode, _lobbyPanel.InRoom, footerKey));
            if (_settingsLayer.activeSelf) _settingsView.Refresh(0);
            if (_tableLayer.activeSelf) _tableView.Refresh(TableKey());
            if (_howToLayer.activeSelf) _howToView.Refresh(TableKey());
        }

        /// <summary>Friends online right now, as the lobby last counted them.</summary>
        private int OnlineFriends()
        {
            int online = 0;
            foreach (LobbyFriend friend in _lobbyPanel.Friends) online += friend.IsOnline ? 1 : 0;
            return online;
        }

        /// <summary>The green band: the brand mark and name, Steam's state, the version, settings and the way out.</summary>
        private void RenderMasthead(UiBuilder ui)
        {
            ui.Swatch(UiPalette.Ink.Gold, 56f, mark: "L");
            UiBuilder name = ui.Column(0f);
            UiBuilder.Fix(name.Container.gameObject, -1f).flexibleWidth = 0f;
            Text brand = name.Label("LAZPOLY", UiTextStyle.Heading, UiPalette.Ink.OnMasthead);
            brand.horizontalOverflow = HorizontalWrapMode.Overflow;
            name.Eyebrow(L("Karadeniz Masası"), UiPalette.Ink.OnMastheadMuted).horizontalOverflow = HorizontalWrapMode.Overflow;

            UiBuilder.Fix(ui.Label("", UiTextStyle.Small), -1f).flexibleWidth = 1f;
            if (_lobbyPanel.IsAvailable)
            {
                int online = OnlineFriends();
                ui.Chip(online == 1 ? L("Steam · 1 arkadaş çevrim içi") : F($"Steam · {online} arkadaş çevrim içi"), UiPalette.Ink.MastheadDeep, UiPalette.Ink.OnMastheadMuted);
            }
            else
            {
                ui.Chip(L("Steam kapalı"), UiPalette.Ink.MastheadDeep, UiPalette.Ink.OnMastheadMuted);
            }

            // The build's version, so a bug report or a friend's "which one do you have?" has an answer.
            ui.Label(F($"Sürüm {Application.version}"), UiTextStyle.Small, UiPalette.Ink.OnMastheadMuted).horizontalOverflow = HorizontalWrapMode.Overflow;
            OnMasthead(ui.RoundButton(UiIcon.Sliders, L("Ayarlar"), OpenSettings, size: 48f));
            OnMasthead(ui.RoundButton(UiIcon.Door, L("Çıkış"), Application.Quit, style: UiButtonStyle.Danger, size: 48f));
        }

        /// <summary>A round button's caption is written for paper; on the green band it turns cream.</summary>
        private static void OnMasthead(Button button)
        {
            foreach (Text caption in button.GetComponentsInChildren<Text>()) caption.color = UiPalette.Ink.OnMasthead;
        }

        /// <summary>The greeting under the masthead, with the logo at the right when the picture is in the build.</summary>
        private void RenderTitle(UiBuilder ui)
        {
            UiBuilder words = ui.Column(4f);
            words.Eyebrow(L("Yol arkadaşların burada"), UiTheme.Gain);
            words.Label(L("Bugün hangi masaya oturuyoruz?"), UiTextStyle.Display);
            words.Label(L("Arkadaşlarınla ya da bilgisayara karşı Karadeniz'in şehirlerini topla, kirayı topla, son kalan sen ol."), UiTextStyle.Small, UiTheme.Muted);
            if (!MenuArt.HasLogo) return;
            Image logo = ui.Swatch(Color.white, MenuLayout.LogoSize);
            logo.sprite = MenuArt.Logo;
            logo.type = Image.Type.Simple;
            logo.preserveAspect = true;
            logo.raycastTarget = false;
        }

        /// <summary>The six ways in, three to a row: each a card with a coloured edge, a title, a line and a tag.</summary>
        private void RenderCards(UiBuilder ui)
        {
            bool steam = _lobbyPanel.IsAvailable;
            UiBuilder top = CardRow(ui);
            ModeCard(top, 0, UiIcon.Network, L("Özel Masa"), L("Oda kur, arkadaşlarını Steam'den davet et; kuralları sen seçersin."),
                steam ? L("Steam ile") : L("Steam gerekir"), () =>
                {
                    _mode = Mode.Online;
                    _lobbyPanel.HostRoom();
                });
            ModeCard(top, 1, UiIcon.People, L("Odaya Katıl"), L("Arkadaşının açtığı masaya oda koduyla ya da listeden gir."),
                steam ? L("Oda kodu") : L("Steam gerekir"), () => _mode = Mode.Online);
            ModeCard(top, 2, UiIcon.Dice, L("Tek Oyunculu"), L("Sen ve bilgisayar rakipler. Rakip sayısını ve zorluğu seçersin."),
                L("1-7 bilgisayar"), () => _mode = Mode.Single);

            UiBuilder bottom = CardRow(ui);
            ModeCard(bottom, 3, UiIcon.Home, L("Aynı Bilgisayarda"), L("2-8 kişi aynı ekranda sırayla oynar; istenen koltuğa bilgisayar da oturabilir."),
                L("2-8 oyuncu"), () => _mode = Mode.Local);
            // The card's line is the rules as chosen, so the tag stays short enough for its pill.
            ModeCard(bottom, 4, UiIcon.List, L("Masa Kuralları"), TableSetup.Summary(TableSetup.Chosen, _boardMoney), L("Sonraki oyun"), OpenTableRules);
            ModeCard(bottom, 5, UiIcon.Book, L("Nasıl Oynanır"), L("Şehirler, binalar, kira, takas ve kartlar: masanın kuralları kısaca."),
                L("Rehber"), OpenHowToPlay);
        }

        /// <summary>A row of cards that share the grid's width and height equally.</summary>
        private static UiBuilder CardRow(UiBuilder ui)
        {
            UiBuilder row = ui.Row(MenuLayout.CardGap);
            row.Container.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;
            LayoutElement layout = UiBuilder.Fix(row.Container.gameObject, -1f, MenuLayout.MinCardHeight);
            layout.flexibleHeight = 1f;
            return row;
        }

        /// <summary>One way in: a stripe in the card's colour, the title, a line about it, a tag and the icon in a tinted disc.</summary>
        private static void ModeCard(UiBuilder row, int index, UiIcon icon, string title, string description, string tag, Action open)
        {
            Color color = UiPalette.Ink.Stripes[index % UiPalette.Ink.Stripes.Length];
            UiBuilder tile = row.Tile(open, padding: 20);
            var card = (RectTransform)tile.Container;
            card.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            LayoutElement layout = UiBuilder.Fix(card.gameObject, -1f);
            layout.preferredWidth = 0f;
            layout.flexibleWidth = 1f;
            card.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(20 + (int)MenuLayout.CardStripe, 20, 20, 20);

            // The stripe hugs the left edge, outside the column's flow; the mask trims it to the card's corners.
            RectTransform stripe = UiRoot.Rect(card, "Stripe", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(MenuLayout.CardStripe, 0f));
            UiRoot.Background(stripe, color, rounded: false).raycastTarget = false;
            stripe.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            tile.Label(title, UiTextStyle.Heading);
            tile.Label(description, UiTextStyle.Small, UiTheme.Muted);
            UiBuilder.Fix(tile.Label("", UiTextStyle.Small), -1f).flexibleHeight = 1f;
            UiBuilder foot = tile.Row(12f, expand: false);
            foot.Container.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
            foot.Chip(tag, UiPalette.Ink.Wash(color), UiPalette.Ink.Deep(color));
            UiBuilder.Fix(foot.Label("", UiTextStyle.Small), -1f).flexibleWidth = 1f;
            Image disc = foot.Swatch(UiPalette.Ink.Wash(color), 52f, round: true);
            disc.raycastTarget = false;
            RectTransform picture = UiIcons.Create((RectTransform)disc.transform, icon, 28f, color);
            picture.anchorMin = picture.anchorMax = new Vector2(0.5f, 0.5f);
            picture.anchoredPosition = Vector2.zero;
            picture.sizeDelta = new Vector2(28f, 28f);
            foot.Icon(UiIcon.Next, 22f, UiTheme.Muted);
        }

        /// <summary>A mode's setup, in a sheet in the middle of the window; a room fills the window instead.</summary>
        private void RenderSheet(UiBuilder ui)
        {
            if (_mode == Mode.Online && _lobbyPanel.InRoom)
            {
                _lobbyPanel.Render(ui);
                return;
            }

            UiBuilder centre = ui.Row(0f, expand: false);
            centre.Container.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperCenter;
            UiBuilder sheet = centre.Card(UiTheme.Card, 28);
            UiBuilder.Fix(sheet.Container.gameObject, width: MenuLayout.SheetWidth);
            switch (_mode)
            {
                case Mode.Single: RenderSingle(sheet); break;
                case Mode.Local: RenderLocalGame(sheet); break;
                case Mode.Online: RenderOnline(sheet); break;
            }
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
            return HashCode.Combine(table.StartingMoney, table.AuctionsEnabled, table.RoundLimit, table.TurnSeconds, table.BankReturns);
        }

        /// <summary>House rules for the next new game, local or hosted online.</summary>
        private void RenderTableRules(UiBuilder ui)
        {
            ui.Title(UiIcon.List, L("Masa Kuralları"));
            ui.Label(L("Yeni yerel oyunlarda ve kurduğun çevrim içi odalarda geçerli. Kayıtlı oyun kendi kurallarıyla sürer."), UiTextStyle.Small, UiTheme.Muted);
            TableRulesForm.Render(ui, TableSetup.Chosen, true, _boardMoney, table => TableSetup.Chosen = table);
            ui.Space(6f);
            ui.Button(L("Tamam"), () => _tableLayer.SetActive(false), style: UiButtonStyle.Primary);
        }

        /// <summary>The footer line: on the home page the motto and the quickest way to a table; in a room the lobby's ready line.</summary>
        private void RenderFooter(UiBuilder ui)
        {
            if (_lobbyPanel.InRoom)
            {
                _lobbyPanel.RenderFooter(ui);
                return;
            }

            UiBuilder row = ui.Row(16f, expand: false);
            row.Container.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
            UiBuilder words = row.Column(0f);
            if (_mode != Mode.Home)
            {
                words.Eyebrow(L("Masa seni bekliyor"), UiTheme.Text);
                words.Label(L("Kurulumu bitir, oyunu başlat."), UiTextStyle.Small, UiTheme.Muted);
                return;
            }

            if (_brokenSave)
            {
                UiBuilder.Fix(words.Container.gameObject, -1f).flexibleWidth = 1f;
                words.Notice(L("Kayıtlı oyun okunamadı; dosya bozulmuş ya da eski bir sürümden kalmış olabilir. Silip yeni bir oyun kurabilirsin."),
                    L("Kaydı Sil"), () =>
                    {
                        SaveGameStore.Delete();
                        _brokenSave = false;
                    });
            }
            else if (_savedGame != null)
            {
                words.Eyebrow(L("Kaldığın yerden"), UiTheme.Gain);
                words.Label(SaveSummary(_savedGame, SaveGameStore.SavedAt), UiTextStyle.Small, UiTheme.Muted);
            }
            else if (CanRejoin(out _))
            {
                words.Eyebrow(L("Odan hâlâ açık"), UiTheme.Gain);
                words.Label(L("Son çevrim içi oyun kapanmadan koptu; odaya dönersen eski koltuğun seni bekler."), UiTextStyle.Small, UiTheme.Muted);
            }
            else
            {
                words.Eyebrow(L("Masa seni bekliyor"), UiTheme.Text);
                words.Label(_opponents == 1 ? L("Hemen bir bilgisayara karşı başla; ayarlar Tek Oyunculu'da.") : F($"Hemen {_opponents} bilgisayara karşı başla; ayarlar Tek Oyunculu'da."),
                    UiTextStyle.Small, UiTheme.Muted);
            }

            Vector2 size = MenuLayout.FooterButton;
            if (_savedGame != null)
            {
                UiBuilder.Fix(row.Button(L("Kayıtlı Oyuna Devam  →"), ResumeSavedGame, style: UiButtonStyle.Primary, height: size.y), width: size.x);
            }
            else if (CanRejoin(out ulong room))
            {
                UiBuilder.Fix(row.Button(L("Unut"), LastRoomStore.Forget, height: UiTheme.ButtonHeight), width: 110f);
                UiBuilder.Fix(row.Button(L("Odaya Geri Dön  →"), () =>
                {
                    _rejoining = true;
                    _lobby.Join(room);
                }, style: UiButtonStyle.Primary, height: size.y), width: size.x);
            }
            else
            {
                UiBuilder.Fix(row.Button(L("Masaya Otur  →"), StartSingleGame, style: UiButtonStyle.Primary, height: size.y), width: size.x);
            }
        }

        private void OpenHowToPlay()
        {
            _howToLayer.SetActive(true);
            _howToView.Invalidate();
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

        /// <summary>Esc closes the window on top, as it does in the game; with none open it does nothing.</summary>
        public bool CloseTopWindow()
        {
            if (_howToLayer.activeSelf) _howToLayer.SetActive(false);
            else if (_settingsLayer.activeSelf) CloseSettings();
            else if (_tableLayer.activeSelf) _tableLayer.SetActive(false);
            else return false;
            AudioPlayer.Play(SoundCue.UiClick);
            return true;
        }

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

        /// <summary>A game against the chosen number of computers at the chosen difficulty; the footer's button starts one at once.</summary>
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
