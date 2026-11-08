using System;
using System.Collections;
using System.Collections.Generic;
using Lazpoly.App.AI;
using Lazpoly.App.Avatars;
using Lazpoly.App.Lobby;
using Lazpoly.App.Presence;
using Lazpoly.App.Save;
using Lazpoly.Core.Board;
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
    /// Main menu in the owner's kit: the city picture across the screen and, on a 1920x1080 design frame over it,
    /// the home screen (<see cref="MenuHome"/>): play at once, host or join a room, set up a game against the
    /// computer or on one computer, pick a character, read how to play, settings. A way in that needs setting up
    /// opens on a paper sheet over the menu; an online room fills that sheet with the lobby's columns.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        private enum Mode { Home, Single, Local, Online }

        private static readonly string[] DifficultyNames = { "Kolay", "Orta", "Zor" };

        /// <summary>What sits in a local seat: a person (0) or the computer at a difficulty.</summary>
        private static readonly string[] SeatKinds = { "İnsan", "AI: Kolay", "AI: Orta", "AI: Zor" };

        /// <summary>How many of the board's dearest towns the home screen names.</summary>
        public const int TownsShown = 4;

        private readonly List<string> _names = new List<string>();
        private readonly List<int> _seatKinds = new List<int>();

        /// <summary>Each local seat's character (all eight seats, so a seat keeps its pick when the count goes down and up).</summary>
        private readonly List<string> _seatFigures = new List<string>();

        /// <summary>The local seat whose character the strip under the seats changes.</summary>
        private int _figureSeat;
        private readonly List<RectTransform> _frames = new List<RectTransform>();
        private int _playerCount = 2;
        private Mode _mode = Mode.Home;
        private int _opponents = 3;
        private AiDifficulty _difficulty = AiDifficulty.Normal;
        private RectTransform _canvas;
        private MenuHome _home;
        private UiView _infoView;
        private GameObject _sheetLayer;
        private UiView _sheetView;
        private UiView _footerView;
        private CharacterWindow _characters;
        private CharacterChoice _choice;
        private UiView _settingsView;
        private GameObject _settingsLayer;
        private bool _confirmQuit;
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
        private float _unmutedVolume;
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
                RoomDressing.ApplyQuality(_settingsService.Current.Quality);
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
            _choice = new CharacterChoice(new PlayerPrefsStorage());
            var wanted = new string[RulesConfig.AbsoluteMaxPlayers];
            wanted[0] = _choice.Selected.Id; // The one setting up sits first, as their own character.
            _seatFigures.AddRange(FigurePicks.Resolve(wanted, CharacterChoice.Ids));

            ITransport transport = null;
            GameServices.Registry?.TryGet(out _lobby);
            GameServices.Registry?.TryGet(out transport);
            GameServices.Registry?.TryGet(out _presence);
            GameServices.Registry?.TryGet(out _avatars);
            _lobbyPanel = new LobbyPanel(_lobby, transport, () => SceneFader.Load("Game"), OpenTableRules, _boardMoney, _choice);
            if (_lobby != null) _lobby.Failed += OnLobbyFailed;
            _settings = new SettingsPanel();
        }

        private void OnDestroy()
        {
            if (_settingsService != null) _settingsService.Changed -= OnSettingsChanged;
            if (_lobby != null) _lobby.Failed -= OnLobbyFailed;
            _lobbyPanel?.Dispose();
            _profileFace?.Dispose();
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
            RoomDressing.ApplyQuality(settings.Quality);
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

            QuietCamera();
            Canvas canvas = UiRoot.CreateCanvas(transform, "Main Menu UI", 0);
            _canvas = (RectTransform)canvas.transform;

            // The city picture covers the screen, cropped rather than letterboxed; the design frame sits over it whole.
            RectTransform picture = UiRoot.Rect(_canvas, "City Background", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image background = MenuKit.Picture(picture, MenuKit.Background, Color.white);
            if (background.sprite == null) background.color = UiTheme.Backdrop;
            var cover = picture.gameObject.AddComponent<AspectRatioFitter>();
            cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            cover.aspectRatio = background.sprite != null ? background.sprite.rect.width / background.sprite.rect.height : 16f / 9f;

            _home = new MenuHome(Frame(_canvas, "Home"), new MenuHomeActions
            {
                Play = StartSingleGame,
                Host = () =>
                {
                    _mode = Mode.Online;
                    _lobbyPanel.HostRoom();
                },
                Join = () => _mode = Mode.Online,
                Single = () => _mode = Mode.Single,
                Local = () => _mode = Mode.Local,
                Characters = () => _characters.Show(_choice.Selected),
                HowToPlay = OpenHowToPlay,
                Settings = OpenSettings,
                ToggleSound = ToggleSound,
            }, TownsLine(_boardData.Board, TownsShown));
            UiBuilder.AddColumnLayout(_home.InfoArea.gameObject, 0, 4f);
            _home.InfoArea.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
            _infoView = new UiView(new UiBuilder(_home.InfoArea), RenderInfo);

            // A mode's setup: a paper sheet over the dimmed menu, the lobby's ready line at its foot.
            RectTransform sheetFrame = Frame(DimLayer(_canvas, "Sheet Layer", out _sheetLayer), "Sheet Frame");
            Rect area = MenuLayout.Sheet;
            RectTransform sheet = MenuKit.Place(sheetFrame, "Sheet", area.x, area.y, area.width, area.height);
            _sheet = sheet;
            UiRoot.Panel(sheet);
            RectTransform inside = UiRoot.Stretch(sheet, "Inside");
            UiBuilder.AddColumnLayout(inside.gameObject, 36, 20f);
            RectTransform body = UiRoot.Stretch(inside, "Body");
            LayoutElement grow = UiBuilder.Fix(body.gameObject, -1f);
            grow.preferredHeight = 0f;
            grow.flexibleHeight = 1f;
            _sheetView = new UiView(UiRoot.ScrollPanel(body, 0), RenderSheet);
            RectTransform footer = UiRoot.Stretch(inside, "Footer");
            UiBuilder.Fix(footer.gameObject, height: 72f);
            UiBuilder.AddColumnLayout(footer.gameObject, 0, 0f);
            footer.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
            _footerView = new UiView(new UiBuilder(footer), RenderFooter);
            _sheetLayer.SetActive(false);

            RectTransform characterFrame = Frame(DimLayer(_canvas, "Characters Layer", out GameObject characterLayer), "Characters Frame");
            _characters = new CharacterWindow(characterFrame, characterLayer, option => PickOwnCharacter(option.Id));

            _settingsView = new UiView(UiRoot.Modal(_canvas, "Settings", 520f, out _settingsLayer), RenderSettings);
            _settingsLayer.SetActive(false);
            _tableView = new UiView(UiRoot.Modal(_canvas, "Table Rules", 640f, out _tableLayer), RenderTableRules);
            _tableLayer.SetActive(false);
            // The page describes the table rules chosen for the next game.
            _howToView = new UiView(UiRoot.Modal(_canvas, "How To Play", HowToPlayPanel.Width, out _howToLayer),
                ui => HowToPlayPanel.Render(ui, _boardData.Board, _boardData.Rules.WithTable(TableSetup.Chosen), () => _howToLayer.SetActive(false)));
            _howToLayer.SetActive(false);
            ScaleFrames();
            if (ScreenTour.Requested && !ScreenTour.MenuShown && !PerfRun.Requested) StartCoroutine(TourMenu());
        }

        /// <summary>
        /// The menu's part of the screen tour (<see cref="ScreenTour"/>): the home screen, the characters, settings,
        /// how to play, each mode's sheet and the table rules; then on to the tour's table.
        /// </summary>
        private IEnumerator TourMenu()
        {
            ScreenTour.MarkMenuShown();
            yield return ScreenTour.Until(() => !BootSplash.IsShowing, 15f); // The opening screen fades out over the menu first.
            yield return ScreenTour.Take("menu");
            _characters.Show(_choice.Selected);
            yield return ScreenTour.Take("karakterler");
            _characters.Hide();
            OpenSettings();
            yield return ScreenTour.Take("ayarlar");
            CloseSettings();
            OpenHowToPlay();
            yield return ScreenTour.Take("nasil-oynanir");
            _howToLayer.SetActive(false);
            _mode = Mode.Single;
            yield return ScreenTour.Take("tek-oyunculu");
            OpenTableRules();
            yield return ScreenTour.Take("masa-kurallari");
            _tableLayer.SetActive(false);
            _mode = Mode.Local;
            yield return ScreenTour.Take("ayni-bilgisayarda");
            _mode = Mode.Online;
            yield return ScreenTour.Take("cevrim-ici");
            _mode = Mode.Home;
            LocalGameSetup.SetSeats(ScreenTour.Seats(), ScreenTour.Ai());
            SceneFader.Load("Game");
        }

        /// <summary>The scene's camera draws nothing now that the menu is a picture: no board, no sky, only the canvas.</summary>
        private static void QuietCamera()
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UiTheme.Backdrop;
            camera.cullingMask = 0;
        }

        /// <summary>A 1920x1080 design frame, centred on <paramref name="parent"/>; <see cref="ScaleFrames"/> fits it to the screen.</summary>
        private RectTransform Frame(RectTransform parent, string name)
        {
            RectTransform frame = UiRoot.Rect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            frame.sizeDelta = MenuLayout.Design;
            _frames.Add(frame);
            return frame;
        }

        /// <summary>A layer over the whole screen in the kit's dim, blocking clicks to what lies under it.</summary>
        private static RectTransform DimLayer(RectTransform canvas, string name, out GameObject layer)
        {
            RectTransform dim = UiRoot.Stretch(canvas, name);
            UiRoot.Background(dim, MenuKit.Dim, rounded: false);
            layer = dim.gameObject;
            layer.AddComponent<UiFocusScope>();
            return dim;
        }

        private void ScaleFrames()
        {
            if (_canvas == null) return;
            float scale = MenuLayout.FrameScale(_canvas.rect.size);
            foreach (RectTransform frame in _frames)
            {
                if (frame.localScale.x != scale) frame.localScale = new Vector3(scale, scale, 1f);
            }
        }

        /// <summary>"İstanbul · New York · Londra · Paris": the board's dearest towns, dearest first.</summary>
        public static string TownsLine(BoardDefinition board, int count)
        {
            var towns = new List<SquareDefinition>();
            for (int i = 0; i < board.Count; i++)
            {
                if (board[i].Type == SquareType.Property && !board[i].IsResort) towns.Add(board[i]);
            }

            towns.Sort((a, b) => b.Price.CompareTo(a.Price));
            var names = new List<string>();
            for (int i = 0; i < towns.Count && i < count; i++) names.Add(towns[i].Name);
            return string.Join(" · ", names);
        }

        private void LateUpdate()
        {
            if (_home == null) return;
            ScaleFrames();
            bool sheet = _mode != Mode.Home;
            if (_sheetLayer.activeSelf != sheet) _sheetLayer.SetActive(sheet);
            if (sheet)
            {
                PlaceSheet(_mode == Mode.Online && _lobbyPanel.InRoom ? MenuLayout.Sheet : MenuLayout.SetupSheet);
                int modeKey = _mode == Mode.Online ? _lobbyPanel.ViewKey : HashCode.Combine(_playerCount, _opponents, _difficulty, FiguresKey());
                _sheetView.Refresh(HashCode.Combine(_mode, modeKey, TableKey()));
                _footerView.Refresh(HashCode.Combine(_lobbyPanel.InRoom, _lobbyPanel.InRoom ? _lobbyPanel.ViewKey : 0));
            }

            TakePlatformName();
            _home.SetProfile(_names[0], _choice.Selected.Name);
            ShowProfilePhoto();
            _home.SetMuted(Muted);
            _home.SetStatus(StatusLine());
            _infoView.Refresh(HashCode.Combine(_savedGame != null, _brokenSave, CanRejoin(out _)));
            if (_settingsLayer.activeSelf) _settingsView.Refresh(HashCode.Combine(_confirmQuit));
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

        private bool _platformNamed;
        private IPlayerAvatars _avatars;
        private Game.PlayerFaces _profileFace;
        private const int ProfileSeat = 1;

        /// <summary>
        /// Once Steam is up, the profile card's disc shows the person's Steam profile picture (asked for every half
        /// second until Steam has it, as on the cards at the table); without Steam or a picture the letter stays.
        /// </summary>
        private void ShowProfilePhoto()
        {
            if (_profileFace == null)
            {
                if (_avatars == null || _lobby == null || !_lobby.IsAvailable || _lobby.LocalId == 0) return;
                _profileFace = new Game.PlayerFaces(_avatars, Game.SeatAccounts.Local(ProfileSeat, _lobby.LocalId), null);
            }

            _profileFace.Update(Time.unscaledTime);
            if (_profileFace.HasPhoto(ProfileSeat) && !_home.ShowsPhoto) _home.SetPhoto(_profileFace.For(ProfileSeat));
        }

        /// <summary>
        /// Once Steam is up, the person at this computer goes by their Steam name on the profile card and in the
        /// first seat, unless they already typed a name of their own.
        /// </summary>
        private void TakePlatformName()
        {
            if (_platformNamed || _lobby == null || !_lobby.IsAvailable) return;
            _platformNamed = true;
            _names[0] = FirstSeatName(_names[0], F($"Oyuncu {1}"), _lobby.LocalName);
        }

        /// <summary>The first seat's name once the platform's is known: the platform's in place of the untouched default.</summary>
        public static string FirstSeatName(string current, string untouched, string platform) =>
            current == untouched && !string.IsNullOrWhiteSpace(platform) ? platform.Trim() : current;

        private int _statusKey = int.MinValue;
        private string _statusLine = "";

        /// <summary>"Steam · 2 arkadaş çevrim içi · Sürüm 0.16": Steam's state and the build, so a friend's "which one do you have?" has an answer.</summary>
        private string StatusLine()
        {
            bool steam = _lobbyPanel.IsAvailable;
            int online = steam ? OnlineFriends() : 0;
            int key = HashCode.Combine(steam, online, Lang.Current);
            if (key == _statusKey) return _statusLine;
            _statusKey = key;
            string state = !steam ? L("Steam kapalı") : online == 1 ? L("Steam · 1 arkadaş çevrim içi") : F($"Steam · {online} arkadaş çevrim içi");
            _statusLine = $"{state} · {F($"Sürüm {Application.version}")}";
            return _statusLine;
        }

        /// <summary>Whether the game is silent: the master volume at nothing.</summary>
        private bool Muted => _settingsService != null ? _settingsService.Current.MasterVolume <= 0.001f : AudioListener.volume <= 0.001f;

        /// <summary>The sound button: silence, or back to the volume there was before it.</summary>
        private void ToggleSound()
        {
            float before = _settingsService != null ? _settingsService.Current.MasterVolume : AudioListener.volume;
            float after = before > 0.001f ? 0f : (_unmutedVolume > 0.001f ? _unmutedVolume : GameSettings.Default.MasterVolume);
            if (before > 0.001f) _unmutedVolume = before;
            if (_settingsService != null) _settingsService.Change(_settingsService.Current.WithMasterVolume(after));
            else AudioListener.volume = after;
        }

        /// <summary>
        /// The line under the play buttons: the unreadable save to delete, the saved game to go on with, the room
        /// still open to go back to; otherwise the kit's motto.
        /// </summary>
        private void RenderInfo(UiBuilder ui)
        {
            UiBuilder row = ui.Row(16f, expand: false);
            row.Container.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
            if (_brokenSave)
            {
                // One line beside its button, like the saved game's: the space under the play buttons is short.
                UiBuilder words = row.Column(0f);
                UiBuilder.Fix(words.Container.gameObject, -1f).flexibleWidth = 1f;
                words.Eyebrow(L("Kayıtlı oyun okunamadı"), UiTheme.Warning);
                words.Label(L("Dosya bozuk ya da eski bir sürümden; silip yeni oyun kurabilirsin."), UiTextStyle.Small, MenuKit.Muted);
                UiBuilder.Fix(row.Button(L("Kaydı Sil"), () =>
                {
                    SaveGameStore.Delete();
                    _brokenSave = false;
                }, height: UiTheme.ButtonHeight), width: 160f);
            }
            else if (_savedGame != null)
            {
                UiBuilder words = row.Column(0f);
                UiBuilder.Fix(words.Container.gameObject, -1f).flexibleWidth = 1f;
                words.Eyebrow(L("Kaldığın yerden"), UiTheme.Gain);
                words.Label(SaveSummary(_savedGame, SaveGameStore.SavedAt), UiTextStyle.Small, MenuKit.Muted);
                UiBuilder.Fix(row.Button(L("Kayıtlı Oyuna Devam  →"), ResumeSavedGame, style: UiButtonStyle.Primary, height: 56f), width: 300f);
            }
            else if (CanRejoin(out ulong room))
            {
                UiBuilder words = row.Column(0f);
                UiBuilder.Fix(words.Container.gameObject, -1f).flexibleWidth = 1f;
                words.Eyebrow(L("Odan hâlâ açık"), UiTheme.Gain);
                words.Label(L("Son çevrim içi oyun kapanmadan koptu; odaya dönersen eski koltuğun seni bekler."), UiTextStyle.Small, MenuKit.Muted);
                UiBuilder.Fix(row.Button(L("Unut"), LastRoomStore.Forget, height: UiTheme.ButtonHeight), width: 110f);
                UiBuilder.Fix(row.Button(L("Odaya Geri Dön  →"), () =>
                {
                    _rejoining = true;
                    _lobby.Join(room);
                }, style: UiButtonStyle.Primary, height: 56f), width: 260f);
            }
            else
            {
                Text motto = row.Label(L("2–8 OYUNCU   ·   STRATEJİ, ŞANS VE REKABET"), UiTextStyle.Small, MenuKit.Muted);
                motto.font = UiTheme.BoldFont;
                motto.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
        }

        /// <summary>A mode's setup on the sheet: one player or one computer in a card in its middle; online, the lobby.</summary>
        private void RenderSheet(UiBuilder ui)
        {
            if (_mode == Mode.Online && _lobbyPanel.InRoom)
            {
                _lobbyPanel.Render(ui);
                return;
            }

            // The setup sits straight on the window-sized sheet, as the kit's windows hold their content.
            switch (_mode)
            {
                case Mode.Single: RenderSingle(ui); break;
                case Mode.Local: RenderLocalGame(ui); break;
                case Mode.Online: RenderOnline(ui); break;
            }
        }

        private RectTransform _sheet;

        /// <summary>Sizes the sheet for what it holds: the setup in a window-sized sheet, the room's lobby across the wide one.</summary>
        private void PlaceSheet(Rect area)
        {
            var position = new Vector2(area.x, -area.y);
            if (_sheet.anchoredPosition == position && _sheet.sizeDelta == area.size) return;
            _sheet.anchoredPosition = position;
            _sheet.sizeDelta = area.size;
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

            ui.Eyebrow(L("Karakterin"), UiTheme.Muted);
            CharacterPicker.Render(ui, _choice.Selected.Id, null, PickOwnCharacter);
            ui.Label(L("Bilgisayarlar kalan karakterleri alır."), UiTextStyle.Small, UiTheme.Muted);

            ui.Space(6f);
            RenderTableLine(ui);
            ui.Button(L("Oyunu Başlat"), StartSingleGame, style: UiButtonStyle.Primary, height: 56f);
        }

        /// <summary>The person's own character, picked on the one-player sheet: it is also their first pick from now on.</summary>
        private void PickOwnCharacter(string id)
        {
            _choice.Select(id);
            Swap(0, id); // The one setting up sits first at the local table too.
        }

        /// <summary>
        /// Gives a local seat a character. A seat hidden by a smaller player count may hold it; the two swap, so the
        /// eight seats never share a character, whatever the count.
        /// </summary>
        private void Swap(int seat, string id)
        {
            int held = _seatFigures.IndexOf(id);
            if (held >= 0 && held != seat) _seatFigures[held] = _seatFigures[seat];
            _seatFigures[seat] = id;
        }

        /// <summary>Changes whenever a character pick on the sheets changes, so they redraw.</summary>
        private int FiguresKey()
        {
            int key = HashCode.Combine(_choice.Selected.Id, _figureSeat);
            foreach (string figure in _seatFigures) key = HashCode.Combine(key, figure);
            return key;
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

            if (_figureSeat >= _playerCount) _figureSeat = 0;
            for (int i = 0; i < _playerCount; i++)
            {
                int seat = i;
                // Only the name field takes the spare width, so the face stays a round 48 px button.
                UiBuilder row = ui.Row(expand: false);
                row.Swatch(PlayerPalette.ForSeat(seat), 22f);
                UiBuilder face = row.Tile(() => _figureSeat = seat, seat == _figureSeat ? UiTheme.Fill(UiButtonStyle.Selected) : (Color?)null, 4);
                face.Container.name = $"Seat {seat + 1} Character";
                face.Container.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
                UiBuilder.Fix(face.Container.gameObject, width: 48f, height: 48f);
                CharacterPicker.Face(CharacterPicker.Centred(face), CharacterChoice.Find(_seatFigures[seat]), 40f);
                UiBuilder.Fix(row.Input(_names[seat], text => _names[seat] = text, 24)).flexibleWidth = 1f;
                UiBuilder.Fix(row.Button(L(SeatKinds[_seatKinds[seat]]), () => _seatKinds[seat] = (_seatKinds[seat] + 1) % SeatKinds.Length,
                    style: _seatKinds[seat] == 0 ? UiButtonStyle.Secondary : UiButtonStyle.Selected, height: UiTheme.FieldHeight), width: 130f);
            }

            ui.Label(L("Koltuktaki düğmeye basarak insan ya da bilgisayar (Kolay, Orta, Zor) seçersin."), UiTextStyle.Small, UiTheme.Muted);
            ui.Eyebrow(F($"{_figureSeat + 1}. koltuğun karakteri"), UiTheme.Muted);
            CharacterPicker.Render(ui, _seatFigures[_figureSeat], LocalHolder, id => Swap(_figureSeat, id));
            ui.Label(L("Bir koltuğun yüzüne basıp karakterini aşağıdan seçersin; başka koltuktaki karakter seçilemez."), UiTextStyle.Small, UiTheme.Muted);
            ui.Space(6f);
            RenderTableLine(ui);
            ui.Button(L("Oyunu Başlat"), StartLocalGame, style: UiButtonStyle.Primary, height: 56f);
        }

        private static int TableKey()
        {
            TableRules table = TableSetup.Chosen;
            return HashCode.Combine(table.StartingMoney, table.RoundLimit, table.TurnSeconds, table.TimeLimitMinutes);
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

        /// <summary>The sheet's foot: in a room the lobby's ready line, while setting up a game a word on what is left.</summary>
        private void RenderFooter(UiBuilder ui)
        {
            if (_lobbyPanel.InRoom)
            {
                _lobbyPanel.RenderFooter(ui);
                return;
            }

            UiBuilder words = ui.Column(0f);
            words.Eyebrow(L("Masa seni bekliyor"), UiTheme.Text);
            words.Label(L("Kurulumu bitir, oyunu başlat."), UiTextStyle.Small, UiTheme.Muted);
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
            _confirmQuit = false;
            _settingsLayer.SetActive(true);
            _settingsView.Invalidate();
        }

        private void CloseSettings()
        {
            _confirmQuit = false;
            _settingsLayer.SetActive(false);
        }

        /// <summary>The settings, and under them the way out of the game, asked twice.</summary>
        private void RenderSettings(UiBuilder ui)
        {
            _settings.Render(ui, CloseSettings);
            ui.Button(_confirmQuit ? L("Emin misin? Oyundan çık") : L("Oyundan Çık"), () =>
            {
                if (_confirmQuit) Application.Quit();
                else _confirmQuit = true;
            }, style: UiButtonStyle.Danger, height: UiTheme.SmallButtonHeight);
        }

        /// <summary>Esc closes the window on top, as it does in the game, and leaves a setup sheet for the home screen.</summary>
        public bool CloseTopWindow()
        {
            if (_howToLayer.activeSelf) _howToLayer.SetActive(false);
            else if (_tableLayer.activeSelf) _tableLayer.SetActive(false);
            else if (_settingsLayer.activeSelf) CloseSettings();
            else if (_characters.IsOpen) _characters.Hide();
            else if (_mode != Mode.Home && !LeavingHidesARoom()) _mode = Mode.Home;
            else return false;
            AudioPlayer.Play(SoundCue.UiClick);
            return true;
        }

        /// <summary>Leaving the online screen while joining or in a room would hide the room, so it stays.</summary>
        private bool LeavingHidesARoom() => _mode == Mode.Online && _lobby != null && _lobby.Status != LobbyStatus.Idle;

        /// <summary>"Tur 12 · 4 oyuncu · 01.11.2026 18:30": what the save holds, so the player knows which game it is.</summary>
        public static string SaveSummary(SavedGame save, DateTime? savedAt)
        {
            int players = 0;
            foreach (GameStateSnapshot.PlayerEntry player in save.Snapshot.Players) players += player.IsBankrupt ? 0 : 1;
            string when = savedAt.HasValue ? savedAt.Value.ToString("dd.MM.yyyy HH:mm") : "";
            string line = F($"Tur {save.Snapshot.Round} · {players} oyuncu");
            return when.Length > 0 ? $"{line} · {when}" : line;
        }

        /// <summary>The name of the other seat at the table that holds a character; null when none does.</summary>
        private string LocalHolder(string id)
        {
            for (int i = 0; i < _playerCount; i++)
            {
                if (i != _figureSeat && _seatFigures[i] == id) return string.IsNullOrWhiteSpace(_names[i]) ? F($"Oyuncu {i + 1}") : _names[i].Trim();
            }

            return null;
        }

        private void StartLocalGame()
        {
            var seats = new PlayerSeat[_playerCount];
            var ai = new Dictionary<int, AiDifficulty>();
            IReadOnlyList<string> figures = FigurePicks.Resolve(_seatFigures.GetRange(0, _playerCount), CharacterChoice.Ids);
            for (int i = 0; i < _playerCount; i++)
            {
                string name = string.IsNullOrWhiteSpace(_names[i]) ? $"Oyuncu {i + 1}" : _names[i].Trim();
                seats[i] = new PlayerSeat(i + 1, name, figures[i]);
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
            var wanted = new string[seats.Length];
            wanted[0] = _choice.Selected.Id;
            IReadOnlyList<string> figures = FigurePicks.Resolve(wanted, CharacterChoice.Ids); // The computers take the rest.
            seats[0] = new PlayerSeat(1, string.IsNullOrWhiteSpace(_names[0]) ? L("Oyuncu 1") : _names[0].Trim(), figures[0]);
            for (int i = 1; i <= _opponents; i++)
            {
                seats[i] = new PlayerSeat(i + 1, F($"Bilgisayar {i}"), figures[i]);
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
