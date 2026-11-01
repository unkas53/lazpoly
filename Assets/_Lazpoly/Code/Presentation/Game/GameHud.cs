using System;
using System.Collections.Generic;
using Lazpoly.App.AI;
using Lazpoly.App.Session;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.State;
using Lazpoly.Infra.Settings;
using Lazpoly.Presentation.Audio;
using Lazpoly.Presentation.Effects;
using Lazpoly.Presentation.Menu;
using Lazpoly.Presentation.Tabletop;
using Lazpoly.Presentation.Texts;
using Lazpoly.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Game
{
    /// <summary>
    /// The game screen's interface: players and actions on the left, the event log and menu button
    /// on the right, and windows for trading, settings, pausing and the end of the game. It offers only
    /// actions <see cref="GameSceneController.IsAllowed"/> approves; the host still validates them.
    /// Keyboard shortcuts (<see cref="Hotkeys"/>) press the same buttons.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        private const float SideWidth = UiLayout.SideWidth;
        private const float Margin = UiLayout.Margin;

        private GameSceneController _controller;
        private TradePanel _trade;
        private SettingsPanel _settings;
        private Canvas _canvas;
        private UiView _side;
        private UiView _log;
        private UiView _menu;
        private UiView _tradeWindow;
        private UiView _settingsWindow;
        private UiView _endWindow;
        private GameObject _tradeLayer;
        private GameObject _settingsLayer;
        private GameObject _endLayer;
        private UiView _pauseWindow;
        private GameObject _pauseLayer;
        private bool _endDismissed;
        private UiView _squareCard;
        private GameObject _squareCardPanel;
        private int _hoveredSquare = NoSquare;
        private int _pinnedSquare = NoSquare;
        private Text _perfLabel;
        private RectTransform _perfRect;
        private float _perfRefreshAt;
        private int _highlightedFor = NoSquare;
        private Text _banner;
        private CanvasGroup _bannerGroup;
        private float _bannerUntil;
        private int _lastTurnPlayer = -1;
        private int _lastTurnNumber = -1;
        private Text _cardTitle;
        private Text _cardText;
        private CanvasGroup _cardGroup;
        private RectTransform _cardRect;
        private Text _rollText;
        private Text _rollNote;
        private CanvasGroup _rollGroup;
        private RectTransform _rollRect;
        private readonly Dictionary<int, RollingNumber> _money = new Dictionary<int, RollingNumber>();
        private readonly Dictionary<int, Text> _moneyLabels = new Dictionary<int, Text>();
        private readonly Dictionary<int, Image> _playerCards = new Dictionary<int, Image>();
        private readonly Dictionary<int, Color> _playerCardColors = new Dictionary<int, Color>();
        private readonly Dictionary<int, float> _moneyFlash = new Dictionary<int, float>();
        private readonly Dictionary<int, int> _moneyFlashSign = new Dictionary<int, int>();
        private int _panelHoverSquare = NoSquare;
        private TipMemory _tipMemory;
        private UiView _tipView;
        private GameObject _tipPanel;
        private Tip? _tip;
        private UiView _howToWindow;
        private Text _timer;
        private GameObject _howToLayer;
        private UiView _cityWindow;
        private GameObject _cityLayer;
        private UiView _buildWindow;
        private GameObject _buildLayer;
        private int _buildCount = 1;
        private int _buildCardDismissed;

        private const int NoSquare = -1;
        private const float BannerSeconds = 1.8f;
        private const float RollSeconds = 1.5f;
        private const float CardWidth = 440f;
        private const float CardButton = UiLayout.CardButtonHeight;
        /// <summary>The menu strip under the log: one row of buttons with the panel's padding.</summary>
        private const float MenuHeight = UiTheme.ButtonHeight + 24f;

        /// <summary>Screen pixels each side column covers, margins included; the camera keeps the board clear of them.</summary>
        public float ReservedSidePixels => UiLayout.ReservedSidePixels(_canvas != null ? _canvas.scaleFactor : 1f);

        /// <summary>The content of the left column, for tests.</summary>
        public RectTransform SideContent { get; private set; }

        /// <summary>The square whose card is showing: pinned by a click, else under the pointer on the board, else the swatch pointed at in the side panel; or -1.</summary>
        public int ShownSquare => _pinnedSquare != NoSquare ? _pinnedSquare : _hoveredSquare != NoSquare ? _hoveredSquare : _panelHoverSquare;

        /// <summary>The first-game hint on screen, or null.</summary>
        public Tip? ShownTip => _tip;

        /// <summary>Whether the city card (buy or move on) is open for the person here.</summary>
        public bool CityCardOpen => _cityLayer != null && _cityLayer.activeSelf;

        /// <summary>Whether the building card (how many houses, or the hotel) is open for the person here.</summary>
        public bool BuildCardOpen => _buildLayer != null && _buildLayer.activeSelf;

        /// <summary>Whether the pause menu is open.</summary>
        public bool PauseOpen => _pauseLayer != null && _pauseLayer.activeSelf;

        /// <summary>Hint memory; tests swap in one that is not kept in PlayerPrefs.</summary>
        public TipMemory TipMemory
        {
            get => _tipMemory ?? (_tipMemory = new TipMemory(new PlayerPrefsStorage()));
            set => _tipMemory = value;
        }

        /// <summary>Shows a square's card until another square or the empty table is clicked.</summary>
        public void PinSquare(int squareIndex) => _pinnedSquare = squareIndex;

        public void Bind(GameSceneController controller)
        {
            _controller = controller;
            _trade = new TradePanel(controller);
            _settings = new SettingsPanel();
            Build();
        }

        private void Build()
        {
            _canvas = UiRoot.CreateCanvas(transform, "Game UI", 0);

            RectTransform side = UiRoot.Rect(_canvas.transform, "Side", Vector2.zero, new Vector2(0f, 1f),
                new Vector2(Margin, Margin), new Vector2(Margin + SideWidth, -Margin));
            UiRoot.Panel(side);
            UiBuilder sideBuilder = UiRoot.ScrollPanel(side);
            SideContent = sideBuilder.Container;
            _side = new UiView(sideBuilder, RenderSide);

            RectTransform log = UiRoot.Rect(_canvas.transform, "Log", new Vector2(1f, 0f), Vector2.one,
                new Vector2(-Margin - SideWidth, 2f * Margin + MenuHeight), new Vector2(-Margin, -Margin));
            UiRoot.Panel(log);
            _log = new UiView(UiRoot.ScrollPanel(log), RenderLog);

            RectTransform menu = UiRoot.Rect(_canvas.transform, "Menu", new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-Margin - SideWidth, Margin), new Vector2(-Margin, Margin + MenuHeight));
            UiRoot.Background(menu, UiTheme.Panel, shadow: true);
            UiBuilder.AddColumnLayout(menu.gameObject, 12, UiTheme.Spacing);
            _menu = new UiView(new UiBuilder(menu), RenderMenu);

            // The square card opens at the top of the free area, on the side away from the square it describes.
            // It never takes the mouse, so the squares under it can still be pointed at and clicked.
            RectTransform card = UiRoot.Rect(_canvas.transform, "Square Card", new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            card.pivot = new Vector2(0f, 1f);
            card.sizeDelta = new Vector2(CardWidth, 0f);
            UiRoot.Background(card, UiTheme.Panel, shadow: true);
            var cardGroup = card.gameObject.AddComponent<CanvasGroup>();
            cardGroup.blocksRaycasts = false;
            cardGroup.interactable = false;
            UiBuilder.AddColumnLayout(card.gameObject, 14, 4f);
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _squareCardPanel = card.gameObject;
            _squareCard = new UiView(new UiBuilder(card), RenderSquareCard);
            _squareCardPanel.SetActive(false);

            // Frame rate and memory behind F3, in the strip above the turn banner.
            RectTransform perf = UiRoot.Rect(_canvas.transform, "Perf Overlay", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-200f, -44f), new Vector2(200f, -14f));
            _perfLabel = new UiBuilder(perf).Label("", UiTextStyle.Small, UiTheme.Muted, TextAnchor.MiddleCenter);
            perf.gameObject.SetActive(false);
            _perfRect = perf;

            RectTransform banner = UiRoot.Rect(_canvas.transform, "Turn Banner", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-320f, -150f), new Vector2(320f, -60f));
            UiRoot.Background(banner, new Color(0.08f, 0.09f, 0.12f, 0.92f));
            _bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            _bannerGroup.blocksRaycasts = false;
            _bannerGroup.alpha = 0f;
            _banner = new UiBuilder(banner).Label("", UiTextStyle.Heading, UiTheme.Accent, TextAnchor.MiddleCenter);
            var bannerRect = (RectTransform)_banner.transform;
            bannerRect.anchorMin = Vector2.zero;
            bannerRect.anchorMax = Vector2.one;
            bannerRect.offsetMin = Vector2.zero;
            bannerRect.offsetMax = Vector2.zero;
            _banner.fontSize = 34;

            BuildCardPopup();
            BuildRollPopup();
            BuildTipPanel();

            RectTransform timer = UiRoot.Rect(_canvas.transform, "Move Timer", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-180f, -52f), new Vector2(180f, -12f));
            _timer = new UiBuilder(timer).Label("", UiTextStyle.Body, UiTheme.Muted, TextAnchor.MiddleCenter);
            var timerRect = (RectTransform)_timer.transform;
            timerRect.anchorMin = Vector2.zero;
            timerRect.anchorMax = Vector2.one;
            timerRect.offsetMin = Vector2.zero;
            timerRect.offsetMax = Vector2.zero;
            _timer.raycastTarget = false;

            // The pause menu comes first, so settings and help opened from it draw on top of it.
            _pauseWindow = new UiView(UiRoot.Modal(_canvas.transform, "Pause", UiLayout.WindowWidth, out _pauseLayer), RenderPause);
            _tradeWindow = new UiView(UiRoot.Modal(_canvas.transform, "Trade", UiLayout.WideWindowWidth, out _tradeLayer), RenderTrade);
            _settingsWindow = new UiView(UiRoot.Modal(_canvas.transform, "Settings", UiLayout.WindowWidth, out _settingsLayer),
                ui => _settings.Render(ui, () => _settingsLayer.SetActive(false)));
            _endWindow = new UiView(UiRoot.Modal(_canvas.transform, "Game End", UiLayout.WindowWidth, out _endLayer), RenderEnd);
            _howToWindow = new UiView(UiRoot.Modal(_canvas.transform, "How To Play", HowToPlayPanel.Width, out _howToLayer),
                ui => HowToPlayPanel.Render(ui, _controller.Session.Board, _controller.Session.Rules, () => _howToLayer.SetActive(false)));
            _cityWindow = new UiView(UiRoot.Modal(_canvas.transform, "City Card", UiLayout.WindowWidth, out _cityLayer), RenderCityCard);
            _buildWindow = new UiView(UiRoot.Modal(_canvas.transform, "Build Card", UiLayout.WindowWidth, out _buildLayer), RenderBuildCard);
            _pauseLayer.SetActive(false);
            _howToLayer.SetActive(false);
            _tradeLayer.SetActive(false);
            _settingsLayer.SetActive(false);
            _endLayer.SetActive(false);
            _cityLayer.SetActive(false);
            _buildLayer.SetActive(false);
        }

        /// <summary>The drawn "Sokak Haberi" card, shown in the middle for a few seconds. It never takes the mouse.</summary>
        private void BuildCardPopup()
        {
            RectTransform card = UiRoot.Rect(_canvas.transform, "Card Popup", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-260f, -40f), new Vector2(260f, 150f));
            UiRoot.Background(card, new Color(0.98f, 0.93f, 0.78f, 0.97f));
            _cardGroup = card.gameObject.AddComponent<CanvasGroup>();
            _cardGroup.blocksRaycasts = false;
            _cardGroup.interactable = false;
            _cardGroup.alpha = 0f;
            _cardRect = card;
            UiBuilder.AddColumnLayout(card.gameObject, 18, 8f);
            var builder = new UiBuilder(card);
            _cardTitle = builder.Label(L("SOKAK HABERİ"), UiTextStyle.Heading, new Color(0.55f, 0.28f, 0.08f), TextAnchor.MiddleCenter);
            _cardText = builder.Label("", UiTextStyle.Body, new Color(0.15f, 0.12f, 0.10f), TextAnchor.MiddleCenter);
            _cardText.fontSize = 22;
        }

        /// <summary>The dice result in big figures under the banner, with a note for doubles. It never takes the mouse.</summary>
        private void BuildRollPopup()
        {
            RectTransform roll = UiRoot.Rect(_canvas.transform, "Roll Popup", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-200f, -262f), new Vector2(200f, -160f));
            UiRoot.Background(roll, new Color(0.08f, 0.09f, 0.12f, 0.88f), shadow: true);
            _rollGroup = roll.gameObject.AddComponent<CanvasGroup>();
            _rollGroup.blocksRaycasts = false;
            _rollGroup.interactable = false;
            _rollGroup.alpha = 0f;
            _rollRect = roll;
            UiBuilder.AddColumnLayout(roll.gameObject, 10, 2f);
            var builder = new UiBuilder(roll);
            _rollText = builder.Label("", UiTextStyle.Title, UiTheme.Text, TextAnchor.MiddleCenter);
            _rollText.fontSize = 40;
            _rollNote = builder.Label("", UiTextStyle.Small, UiTheme.Accent, TextAnchor.MiddleCenter);
        }

        private void UpdateRollPopup()
        {
            float age = Time.unscaledTime - _controller.ShownRollTime;
            float visible = RollSeconds + GameSceneController.RollReadSeconds;
            if (_controller.ShownDie1 == 0 || age > visible)
            {
                _rollGroup.alpha = 0f;
                return;
            }

            int die1 = _controller.ShownDie1, die2 = _controller.ShownDie2;
            _rollText.text = $"{die1} + {die2} = {die1 + die2}";
            _rollNote.text = die1 != die2 ? string.Empty
                : _controller.ShownRollForfeited ? L("Üçüncü çift: hamle yanar, sıra geçer.")
                : L("Çift! Sıra bitince yeniden atarsın.");
            _rollNote.color = _controller.ShownRollForfeited ? UiTheme.Warning : UiTheme.Accent;
            _rollGroup.alpha = Mathf.Clamp01(age / 0.12f) * Mathf.Clamp01((visible - age) / 0.35f);
            _rollRect.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, Ease.OutBack(Mathf.Clamp01(age / 0.25f)));
        }

        /// <summary>A hint box at the bottom centre, between the side columns.</summary>
        private void BuildTipPanel()
        {
            RectTransform tip = UiRoot.Rect(_canvas.transform, "Tip", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
            tip.pivot = new Vector2(0.5f, 0f);
            tip.anchoredPosition = new Vector2(0f, Margin);
            tip.sizeDelta = new Vector2(560f, 0f);
            UiRoot.Background(tip, new Color(0.10f, 0.16f, 0.22f, 0.96f), shadow: true);
            UiBuilder.AddColumnLayout(tip.gameObject, 14, 8f);
            tip.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _tipPanel = tip.gameObject;
            _tipView = new UiView(new UiBuilder(tip), RenderTip);
            _tipPanel.SetActive(false);
        }

        private void RenderTip(UiBuilder ui)
        {
            if (!_tip.HasValue) return;
            ui.Label(L("İpucu"), UiTextStyle.Small, UiTheme.Accent);
            ui.Label(_tip.Value.Text, UiTextStyle.Small);
            UiBuilder row = ui.Row();
            row.Button(L("Anladım"), DismissTip, style: UiButtonStyle.Primary, height: UiTheme.SmallButtonHeight);
            row.Button(L("İpuçlarını Kapat"), () =>
            {
                TipMemory.TurnOff();
                ShowTip(null);
            }, height: UiTheme.SmallButtonHeight);
        }

        /// <summary>Closes the hint on screen for good.</summary>
        public void DismissTip()
        {
            if (_tip.HasValue) TipMemory.MarkSeen(_tip.Value.Id);
            ShowTip(null);
        }

        /// <summary>
        /// Shows the hint that fits the person at this machine now. A hint counts as seen once it is
        /// dismissed or its situation has passed, so each one appears in a single situation.
        /// </summary>
        private void UpdateTip(GameState state)
        {
            Tip? next = null;
            int player = TipPlayer(state);
            if (TipMemory.Enabled && state != null && player != NoPlayer && !_controller.IsBusy && !_controller.EndReason.HasValue)
            {
                next = Tips.Next(_controller.Session.Board, _controller.Session.Rules, state, player, TipMemory.Seen);
            }

            if (next?.Id == _tip?.Id) return;
            if (_tip.HasValue && !_controller.IsBusy) TipMemory.MarkSeen(_tip.Value.Id);
            if (_controller.IsBusy && next == null) return; // Keep the hint while the board animates.
            ShowTip(next);
        }

        private void ShowTip(Tip? tip)
        {
            _tip = tip;
            if (_tipPanel.activeSelf != tip.HasValue) _tipPanel.SetActive(tip.HasValue);
            _tipView.Invalidate();
            if (tip.HasValue) _tipView.Refresh((int)tip.Value.Id);
        }

        private const int NoPlayer = -1;

        /// <summary>Online, while the game waits on the person here: how long until the host moves for them.</summary>
        private void UpdateTimer(GameState state)
        {
            bool show = _controller.IsOnline && state != null && state.Phase != GamePhase.GameOver && !_controller.IsBusy &&
                        !_controller.EndReason.HasValue && WaitsOnSomeoneHere(state);
            if (!show)
            {
                _timer.text = "";
                return;
            }

            int left = Mathf.Max(0, Mathf.CeilToInt(_controller.SecondsLeft));
            _timer.text = F($"Hamle süresi: {left} sn");
            _timer.color = left <= 10 ? UiTheme.Warning : UiTheme.Muted;
        }

        private bool WaitsOnSomeoneHere(GameState state)
        {
            foreach (int id in AiTurnOrder.PlayersToAct(state))
            {
                if (_controller.Session.CanControl(id) && !_controller.Session.IsAiPlaying(id)) return true;
            }

            return false;
        }

        /// <summary>The person the game waits on at this machine, else the first person seated here.</summary>
        private int TipPlayer(GameState state)
        {
            if (state == null) return NoPlayer;
            IGameSession session = _controller.Session;
            foreach (int id in AiTurnOrder.PlayersToAct(state))
            {
                if (session.CanControl(id) && !session.IsAiPlaying(id)) return id;
            }

            foreach (PlayerState player in state.Players)
            {
                if (!player.IsBankrupt && session.CanControl(player.Id) && !session.IsAiPlaying(player.Id)) return player.Id;
            }

            return NoPlayer;
        }

        private void UpdateCardPopup()
        {
            int card = _controller.ShownCard;
            float age = Time.unscaledTime - _controller.ShownCardTime;
            float visible = GameSceneController.CardReadSeconds + 1.6f;
            if (card < 0 || age > visible)
            {
                _cardGroup.alpha = 0f;
                return;
            }

            string player = _controller.Session.State.FindPlayer(_controller.ShownCardPlayer)?.Name;
            _cardTitle.text = player == null ? L("SOKAK HABERİ") : F($"SOKAK HABERİ · {player}");
            _cardText.text = L(EventDescriber.CardText(_controller.Session.Board, card));
            _cardGroup.alpha = Mathf.Clamp01(age / 0.15f) * Mathf.Clamp01((visible - age) / 0.4f);
            _cardRect.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, Ease.OutBack(Mathf.Clamp01(age / 0.3f)));
        }

        private void LateUpdate()
        {
            if (_controller?.Session == null || _side == null) return;
            GameState state = _controller.Session.State;
            ReadHotkeys(state);
            int revision = _controller.Revision;

            bool tradeOpen = state != null && !_controller.EndReason.HasValue && _trade.KeepOpen(state);
            // The pinned square is the destination of a ride, so the side panel follows it too.
            _side.Refresh(HashCode.Combine(revision, _controller.IsBusy, _controller.EndReason, tradeOpen, _pinnedSquare));
            _log.Refresh(revision);
            _menu.Refresh(0);

            UpdateMoney();
            UpdatePointer();
            UpdatePerfOverlay();
            UpdateSquareCard(state, revision);
            UpdateBanner(state);
            UpdateCardPopup();
            UpdateRollPopup();
            UpdateTip(state);
            UpdateTimer(state);

            SetLayer(_tradeLayer, _tradeWindow, tradeOpen, revision);
            bool cityOpen = !tradeOpen && WaitsOnPersonHere(state, GamePhase.AwaitingBuyDecision);
            SetLayer(_cityLayer, _cityWindow, cityOpen, revision);
            bool buildOpen = !tradeOpen && !cityOpen && OffersBuildCard(state);
            if (buildOpen && !_buildLayer.activeSelf) _buildCount = 1;
            SetLayer(_buildLayer, _buildWindow, buildOpen, HashCode.Combine(revision, _buildCount));
            SetLayer(_pauseLayer, _pauseWindow, _pauseLayer.activeSelf, HashCode.Combine(revision, _controller.EndReason));
            SetLayer(_settingsLayer, _settingsWindow, _settingsLayer.activeSelf, 0);
            SetLayer(_howToLayer, _howToWindow, _howToLayer.activeSelf, 0);
            bool ended = !_endDismissed && (_controller.EndReason.HasValue || (state != null && state.Phase == GamePhase.GameOver && !_controller.IsBusy));
            SetLayer(_endLayer, _endWindow, ended, revision);
        }

        /// <summary>Counts each player's money towards its new value; only the labels change, nothing is rebuilt.</summary>
        private void UpdateMoney()
        {
            foreach (KeyValuePair<int, Text> pair in _moneyLabels)
            {
                if (pair.Value == null || !_money.TryGetValue(pair.Key, out RollingNumber money)) continue;
                money.SetTarget(_controller.ShownMoneyOf(pair.Key));
                money.Step(Time.unscaledDeltaTime);
                ShowMoney(pair.Value, money);
                FlashCard(pair.Key, money.Direction);
            }
        }

        /// <summary>The player's card glows green or red while their money counts, then fades back over half a second.</summary>
        private void FlashCard(int playerId, int direction)
        {
            if (!_playerCards.TryGetValue(playerId, out Image card) || card == null) return;
            _moneyFlash.TryGetValue(playerId, out float flash);
            flash = direction != 0 ? 1f : Mathf.MoveTowards(flash, 0f, Time.unscaledDeltaTime * 2f);
            _moneyFlash[playerId] = flash;
            if (direction != 0) _moneyFlashSign[playerId] = direction; // Which way it went, kept for the fade.
            _moneyFlashSign.TryGetValue(playerId, out int sign);
            Color tint = sign < 0 ? UiTheme.Warning : UiTheme.Gain;
            card.color = Color.Lerp(_playerCardColors[playerId], tint, flash * 0.35f);
        }

        private static void ShowMoney(Text label, RollingNumber money)
        {
            label.text = money.Shown.ToString();
            label.color = money.Direction > 0 ? UiTheme.Gain : money.Direction < 0 ? UiTheme.Warning : UiTheme.Accent;
        }

        /// <summary>Whether the frame rate and memory strip is shown (F3).</summary>
        public bool PerfOverlayShown => _perfRect != null && _perfRect.gameObject.activeSelf;

        public void TogglePerfOverlay()
        {
            if (_perfRect == null) return;
            _perfRect.gameObject.SetActive(!_perfRect.gameObject.activeSelf);
            _perfRefreshAt = 0f;
        }

        private void UpdatePerfOverlay()
        {
            if (!PerfOverlayShown || Time.unscaledTime < _perfRefreshAt) return;
            _perfRefreshAt = Time.unscaledTime + 0.5f;
            _perfLabel.text = _controller.Probe != null ? _controller.Probe.OverlayText() : "";
        }

        /// <summary>The square under the mouse (not over the interface); a left click pins or unpins its card.</summary>
        private void UpdatePointer()
        {
            BoardView board = _controller.Board;
            Camera camera = Camera.main;
            if (board == null || camera == null) return;

            bool overInterface = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            _hoveredSquare = !overInterface && board.TryGetSquare(camera.ScreenPointToRay(Input.mousePosition), out int square) ? square : NoSquare;
            if (!overInterface && Input.GetMouseButtonDown(0)) _pinnedSquare = _hoveredSquare == _pinnedSquare ? NoSquare : _hoveredSquare;
            // A pinned square draws the view a little towards itself for as long as it stays pinned.
            _controller.CameraRig?.Peek(_pinnedSquare != NoSquare ? board.SquarePosition(_pinnedSquare) : (Vector3?)null);
        }

        private void UpdateSquareCard(GameState state, int revision)
        {
            int shown = state == null ? NoSquare : ShownSquare;
            bool open = shown != NoSquare;
            if (_squareCardPanel.activeSelf != open) _squareCardPanel.SetActive(open);
            if (open) _squareCard.Refresh(HashCode.Combine(shown, revision, _pinnedSquare == shown));

            if (shown == _highlightedFor || _controller.Board == null) return;
            _highlightedFor = shown;
            if (open) PlaceSquareCard(shown);
            BoardDefinition board = _controller.Session.Board;
            if (shown == NoSquare || board[shown].Type != SquareType.Property)
            {
                _controller.Board.Highlight(null);
                return;
            }

            _controller.Board.Highlight(new List<int>(GameRules.GroupSquares(board, board[shown].GroupId)));
        }

        private void PlaceSquareCard(int square)
        {
            var card = (RectTransform)_squareCardPanel.transform;
            Camera camera = Camera.main;
            bool squareOnLeft = camera == null || camera.WorldToScreenPoint(_controller.Board.SquarePosition(square)).x < Screen.width / 2f;
            float inset = SideWidth + 2f * Margin;
            if (squareOnLeft)
            {
                card.anchorMin = card.anchorMax = new Vector2(1f, 1f);
                card.pivot = new Vector2(1f, 1f);
                card.anchoredPosition = new Vector2(-inset, -Margin);
            }
            else
            {
                card.anchorMin = card.anchorMax = new Vector2(0f, 1f);
                card.pivot = new Vector2(0f, 1f);
                card.anchoredPosition = new Vector2(inset, -Margin);
            }
        }

        private void RenderSquareCard(UiBuilder ui)
        {
            int square = ShownSquare;
            IGameSession session = _controller.Session;
            SquareDefinition definition = session.Board[square];
            SquareInfo info = SquareInfo.Describe(session.Board, session.Rules, session.State, square, _controller.GroupNameOf(square));

            UiBuilder title = ui.Row(10f, expand: false);
            if (definition.Type == SquareType.Property) title.Swatch(_controller.GroupColorOf(square), 22f);
            title.Label(info.Title, UiTextStyle.Heading);
            ui.Label(info.Kind, UiTextStyle.Small);

            foreach (SquareInfoRow row in info.Rows)
            {
                UiBuilder line = ui.Row();
                Color color = row.Current ? UiTheme.Accent : UiTheme.Text;
                Text label = line.Label(row.Label, color: color);
                Text value = line.Label(row.Value, color: color, alignment: TextAnchor.MiddleRight);
                if (row.Current)
                {
                    label.font = UiTheme.BoldFont;
                    value.font = UiTheme.BoldFont;
                }
            }

            foreach (string note in info.Notes) ui.Label(note, UiTextStyle.Small);
            ui.Label(_pinnedSquare == square ? L("Kapatmak için kareye yeniden tıkla.") : L("Sabitlemek için kareye tıkla."), UiTextStyle.Small, UiTheme.Muted);
        }

        /// <summary>A short "your turn" banner whenever a new turn goes to someone at this screen.</summary>
        private void UpdateBanner(GameState state)
        {
            if (state != null && !_controller.IsBusy && state.Phase != GamePhase.GameOver &&
                (state.CurrentPlayer.Id != _lastTurnPlayer || state.TurnNumber != _lastTurnNumber))
            {
                _lastTurnPlayer = state.CurrentPlayer.Id;
                _lastTurnNumber = state.TurnNumber;
                PlayerState player = state.CurrentPlayer;
                if (_controller.Session.CanControl(player.Id))
                {
                    AudioPlayer.Play(SoundCue.YourTurn);
                    _banner.text = F($"Sıra sende, {player.Name}");
                    _banner.color = _controller.ColorOf(player.Id);
                    _bannerUntil = Time.unscaledTime + BannerSeconds;
                }
            }

            float left = _bannerUntil - Time.unscaledTime;
            _bannerGroup.alpha = Mathf.Clamp01(left / 0.4f);
        }

        private static void SetLayer(GameObject layer, UiView view, bool open, int key)
        {
            if (layer.activeSelf != open)
            {
                layer.SetActive(open);
                view.Invalidate();
            }

            if (open) view.Refresh(key);
        }

        private void RenderSide(UiBuilder ui)
        {
            GameState state = _controller.Session.State;
            if (_controller.EndReason.HasValue)
            {
                ui.Label(L("Oyun durdu"), UiTextStyle.Heading);
                ui.Notice(GameSceneController.EndReasonText(_controller.EndReason.Value));
                RenderContinueOffline(ui);
                ui.Button(L("Ana Menü"), _controller.LeaveToMainMenu, style: UiButtonStyle.Primary);
                return;
            }

            if (state == null)
            {
                ui.Label(L("Ev sahibine bağlanılıyor..."), UiTextStyle.Heading);
                ui.Button(L("Vazgeç"), _controller.LeaveToMainMenu);
                return;
            }

            if (_controller.IsReconnecting) RenderReconnecting(ui);

            // What to do now comes first, so the main button never scrolls away; who is playing comes last.
            RenderHeader(ui, state);
            RenderActions(ui, state);
            RenderOwnProperties(ui, state);
            ui.Space(6f);
            ui.Label(L("Oyuncular"), UiTextStyle.Small, UiTheme.Muted);
            RenderPlayers(ui, state);
        }

        private void RenderHeader(UiBuilder ui, GameState state)
        {
            UiBuilder header = ui.Row();
            int limit = _controller.Session.Rules.RoundLimit;
            header.Label(limit > 0 ? F($"Tur {state.Round} / {limit}") : F($"Tur {state.Round}"), UiTextStyle.Small, UiTheme.Muted);
            if (state.LastDie1 != 0) header.Label(F($"Son zar: {state.LastDie1} + {state.LastDie2}"), UiTextStyle.Small, UiTheme.Accent, TextAnchor.MiddleRight);
        }

        /// <summary>The property list of the person on turn here, with its build and sell buttons.</summary>
        private void RenderOwnProperties(UiBuilder ui, GameState state)
        {
            if (state.Phase == GamePhase.GameOver || _controller.IsBusy) return;
            PlayerState player = state.CurrentPlayer;
            if (!_controller.Session.CanControl(player.Id) || state.Phase == GamePhase.AuctionInProgress || state.Phase == GamePhase.AwaitingTradeResponse) return;
            ui.Space(6f);
            PropertyPanel.Render(ui, _controller, state, player.Id);
        }

        private void RenderPlayers(UiBuilder ui, GameState state)
        {
            _moneyLabels.Clear();
            _playerCards.Clear();

            // Players in seat order, the bankrupt ones sunk to the bottom so the live game stays on top.
            var players = new List<PlayerState>(state.Players);
            players.Sort((a, b) => a.IsBankrupt == b.IsBankrupt ? state.IndexOfPlayer(a.Id).CompareTo(state.IndexOfPlayer(b.Id)) : a.IsBankrupt ? 1 : -1);
            foreach (PlayerState player in players)
            {
                bool current = player.Id == state.CurrentPlayer.Id && state.Phase != GamePhase.GameOver;
                Color cardColor = current ? new Color(0.24f, 0.25f, 0.31f) : UiTheme.Card;
                UiBuilder card = ui.Card(cardColor, 10);
                _playerCards[player.Id] = card.Container.GetComponent<Image>();
                _playerCardColors[player.Id] = cardColor;
                UiBuilder row = card.Row(10f);
                Color color = player.IsBankrupt ? UiTheme.Muted : _controller.ColorOf(player.Id);
                row.Swatch(color, 20f, round: true);
                Text name = row.Label(player.Name, color: player.IsBankrupt ? UiTheme.Muted : UiTheme.Text);
                name.font = current ? UiTheme.BoldFont : UiTheme.Font;
                Text money = row.Label(player.IsBankrupt ? L("iflas") : string.Empty, color: UiTheme.Accent, alignment: TextAnchor.MiddleRight);
                UiBuilder.Fix(money, width: 90f);
                if (!player.IsBankrupt) TrackMoney(player.Id, money);

                RenderPlayerStatus(card, player);
                if (!player.IsBankrupt) RenderOwnedSquares(card, state, player.Id);
            }
        }

        private void TrackMoney(int playerId, Text label)
        {
            if (!_money.TryGetValue(playerId, out RollingNumber money))
            {
                money = new RollingNumber(_controller.ShownMoneyOf(playerId));
                _money[playerId] = money;
            }

            _moneyLabels[playerId] = label;
            ShowMoney(label, money);
        }

        /// <summary>One small square per property the player owns, in board order (so groups sit together).</summary>
        private void RenderOwnedSquares(UiBuilder card, GameState state, int playerId)
        {
            UiBuilder row = null;
            for (int square = 0; square < state.SquareCount; square++)
            {
                if (state.GetOwner(square) != playerId) continue;
                row ??= card.Row(4f, expand: false);
                Image swatch = row.Swatch(_controller.GroupColorOf(square), 16f);
                // Pointing at the swatch opens the square's card, so the name and rent are a glance away.
                swatch.raycastTarget = true;
                swatch.gameObject.AddComponent<SquareHoverTarget>().Bind(square, OnPanelSquareHovered);
            }
        }

        private void OnPanelSquareHovered(int square, bool over)
        {
            if (over) _panelHoverSquare = square;
            else if (_panelHoverSquare == square) _panelHoverSquare = NoSquare;
        }

        /// <summary>
        /// Who or what plays the seat. Online every seat also gets a connection dot (green: reachable,
        /// red: not) and, when known, the latency the host measures to that machine.
        /// </summary>
        private void RenderPlayerStatus(UiBuilder card, PlayerState player)
        {
            IGameSession session = _controller.Session;
            bool ai = session.IsAiPlaying(player.Id);
            bool connected = session.IsConnected(player.Id);
            string tag = !_controller.IsOnline ? (ai ? L("Bilgisayar") : null)
                : !connected ? (ai ? L("Bağlantı yok, bilgisayar oynuyor") : L("Bağlantı yok"))
                : ai ? L("Bilgisayar oynuyor (hamle yapmadı)") : null;
            if (!_controller.IsOnline || player.IsBankrupt)
            {
                if (tag != null) card.Label(tag, UiTextStyle.Small);
                return;
            }

            UiBuilder row = card.Row(6f, expand: false);
            row.Swatch(connected ? UiTheme.Gain : UiTheme.Warning, 10f, round: true);
            int ping = session.PingMs(player.Id);
            string latency = !connected ? L("bağlantı yok") : ping > 0 ? F($"{ping} ms") : ping == 0 ? L("bu bilgisayar") : L("gecikme ölçülüyor");
            UiBuilder.Fix(row.Label(latency, UiTextStyle.Small), -1f).flexibleWidth = 1f;
            if (tag != null) card.Label(tag, UiTextStyle.Small, connected ? UiTheme.Muted : UiTheme.Warning);
        }

        /// <summary>The link to the host broke: the board stays, the actions wait, and the player sees how long the game holds their seat open.</summary>
        private void RenderReconnecting(UiBuilder ui)
        {
            int left = Mathf.CeilToInt((float)_controller.SecondsUntilGiveUp);
            int attempt = Mathf.Max(1, _controller.ReconnectAttempts);
            ui.Notice(F($"Ev sahibiyle bağlantı koptu; yeniden deneniyor ({attempt}. deneme). {left} sn içinde bağlanamazsa oyun burada biter. Bu sırada yerine bilgisayar oynar."));
        }

        /// <summary>A guest whose host is gone may keep playing the last state it saw, against computers.</summary>
        private void RenderContinueOffline(UiBuilder ui)
        {
            if (!_controller.CanContinueOffline) return;
            ui.Button(L("Bilgisayarlara Karşı Devam Et"), _controller.ContinueAgainstComputers, style: UiButtonStyle.Primary);
            ui.Label(L("Oyun son görüntüden yerel oyun olarak sürer; diğer koltuklarda bilgisayar oynar ve önceki yerel kaydın üzerine yazılır."),
                UiTextStyle.Small, UiTheme.Muted);
        }

        private void RenderActions(UiBuilder ui, GameState state)
        {
            if (state.Phase == GamePhase.GameOver)
            {
                ui.Label(F($"Kazanan: {state.FindPlayer(state.WinnerId)?.Name}"), UiTextStyle.Heading, UiTheme.Accent);
                ui.Button(L("Ana Menü"), _controller.LeaveToMainMenu, style: UiButtonStyle.Primary);
                return;
            }

            if (_controller.IsBusy)
            {
                ui.Label("...", UiTextStyle.Heading, UiTheme.Muted);
                return;
            }

            // Auctions and trade answers involve players other than the one on turn.
            if (state.Phase == GamePhase.AuctionInProgress) AuctionPanel.Render(ui, _controller, state);
            else if (state.Phase == GamePhase.AwaitingTradeResponse) _trade.RenderResponse(ui, state);
            else RenderTurnActions(ui, state);

            if (_controller.LastRejection != null) ui.Notice(_controller.LastRejection);
        }

        private void RenderTurnActions(UiBuilder ui, GameState state)
        {
            PlayerState player = state.CurrentPlayer;
            UiBuilder turn = ui.Row(10f, expand: false);
            turn.Swatch(_controller.ColorOf(player.Id), 22f, round: true);
            UiBuilder.Fix(turn.Label(_controller.Session.CanControl(player.Id) ? F($"Sıra sende, {player.Name}") : F($"{player.Name} oynuyor"), UiTextStyle.Heading), -1f).flexibleWidth = 1f;

            if (!_controller.Session.CanControl(player.Id))
            {
                ui.Label(_controller.Session.IsAiPlaying(player.Id) ? F($"{player.Name} düşünüyor (bilgisayar)...") : F($"{player.Name} oynuyor, hamlesi bekleniyor."), UiTextStyle.Small);
                return;
            }

            switch (state.Phase)
            {
                case GamePhase.WaitingForRoll:
                    ui.Button(L("Zar At (Boşluk)"), () => _controller.Submit(new RollDiceCommand(player.Id)), style: UiButtonStyle.Primary, height: CardButton);
                    break;

                case GamePhase.AwaitingBuyDecision:
                    RenderBuyDecision(ui, player);
                    break;

                case GamePhase.TurnEnd:
                    ui.Button(L("Sırayı Bitir (Boşluk)"), () => _controller.Submit(new EndTurnCommand(player.Id)), style: UiButtonStyle.Primary, height: CardButton);
                    break;

                case GamePhase.AwaitingDebtPayment:
                    RenderDebt(ui, state, player);
                    break;

                case GamePhase.AwaitingTravelDecision:
                    RenderTravel(ui, player);
                    break;

                case GamePhase.AwaitingDiceChoice:
                    RenderDiceChoice(ui, state, player);
                    break;
            }

            if (OffersExtras(state))
            {
                UiBuilder extras = ui.Row();
                if (CanReopenBuildCard(state)) extras.Button(L("Bina Kartı (B)"), ReopenBuildCard, height: UiTheme.SmallButtonHeight);
                extras.Button(L("Takas Teklif Et (T)"), () => _trade.Open(player.Id), height: UiTheme.SmallButtonHeight);
            }
        }

        /// <summary>The row of side actions (building card, trade) shows while the turn is at a free moment.</summary>
        private static bool OffersExtras(GameState state) => state.Phase == GamePhase.WaitingForRoll || state.Phase == GamePhase.TurnEnd;

        /// <summary>The building card was put away for this visit but could still be used.</summary>
        private bool CanReopenBuildCard(GameState state) =>
            AffordableBuildings(state, state.CurrentPlayer) > 0 && _buildCardDismissed == BuildVisitKey(state);

        /// <summary>The decision itself is on the city card in the middle; the column only says so.</summary>
        private void RenderBuyDecision(UiBuilder ui, PlayerState player)
        {
            SquareDefinition square = _controller.Session.Board[player.Position];
            ui.Label(F($"{L(square.Name)} sahipsiz: şehir kartında satın al ya da devam et."), UiTextStyle.Small);
        }

        /// <summary>True while the game waits in <paramref name="phase"/> on a person (not the computer) sitting at this screen.</summary>
        private bool WaitsOnPersonHere(GameState state, GamePhase phase)
        {
            if (state == null || state.Phase != phase || _controller.IsBusy || _controller.EndReason.HasValue) return false;
            int id = state.CurrentPlayer.Id;
            return _controller.Session.CanControl(id) && !_controller.Session.IsAiPlaying(id);
        }

        /// <summary>
        /// The building card opens by itself once per arrival, when the person here stands on their own
        /// town and can afford at least one building there. "Not now" closes it for this visit; the
        /// property list can open it again.
        /// </summary>
        private bool OffersBuildCard(GameState state)
        {
            bool waiting = WaitsOnPersonHere(state, GamePhase.TurnEnd) || WaitsOnPersonHere(state, GamePhase.WaitingForRoll);
            if (!waiting) return false;
            PlayerState player = state.CurrentPlayer;
            if (AffordableBuildings(state, player) == 0) return false;
            return _buildCardDismissed != BuildVisitKey(state);
        }

        private static int BuildVisitKey(GameState state) => HashCode.Combine(state.TurnNumber, state.CurrentPlayer.Position);

        /// <summary>How many buildings the player may add on their square now, money included.</summary>
        private int AffordableBuildings(GameState state, PlayerState player)
        {
            BoardDefinition board = _controller.Session.Board;
            int allowed = GameRules.BuildableNow(board, state, player, player.Position);
            if (allowed == 0) return 0;
            int cost = board[player.Position].BuildingCost;
            return Math.Min(allowed, cost > 0 ? player.Money / cost : 0);
        }

        /// <summary>Opens the building card for the current visit again, after "not now".</summary>
        public void ReopenBuildCard()
        {
            _buildCardDismissed = 0;
            _buildCount = 1;
        }

        /// <summary>The city's card: its details, and at the bottom "buy" or "move on".</summary>
        private void RenderCityCard(UiBuilder ui)
        {
            GameState state = _controller.Session.State;
            PlayerState player = state.CurrentPlayer;
            int squareIndex = player.Position;
            SquareDefinition square = _controller.Session.Board[squareIndex];
            RenderCityDetails(ui, squareIndex);

            var buy = new BuyPropertyCommand(player.Id);
            bool affordable = player.Money >= square.Price;
            ui.Label(affordable ? F($"Paran: {player.Money}, alırsan kalan: {player.Money - square.Price}") : F($"Paran: {player.Money}"), UiTextStyle.Small, UiTheme.Muted);
            if (!affordable) ui.Label(L("Paran yetmiyor: bina ya da mülk satışıyla para bulabilirsin."), UiTextStyle.Small, UiTheme.Warning);
            if (_controller.Session.Rules.AuctionsEnabled) ui.Label(L("Devam edersen şehir açık artırmaya çıkar."), UiTextStyle.Small, UiTheme.Muted);

            ui.Actions(L("Devam Et"), () => _controller.Submit(new DeclinePurchaseCommand(player.Id)),
                F($"Satın Al ({square.Price})"), () => _controller.Submit(buy), _controller.IsAllowed(buy));
        }

        /// <summary>Name, colour, price, rent table and owner of a city, as on the hover card.</summary>
        private void RenderCityDetails(UiBuilder ui, int squareIndex)
        {
            IGameSession session = _controller.Session;
            SquareInfo info = SquareInfo.Describe(session.Board, session.Rules, session.State, squareIndex, _controller.GroupNameOf(squareIndex));

            ui.CardHeader(info.Title, info.Kind, _controller.GroupColorOf(squareIndex));

            UiBuilder table = ui.Card(UiTheme.Field, 10);
            foreach (SquareInfoRow row in info.Rows)
            {
                UiBuilder line = table.Row();
                Color color = row.Current ? UiTheme.Accent : UiTheme.Text;
                Text label = line.Label(row.Label, UiTextStyle.Small, color);
                Text value = line.Label(row.Value, UiTextStyle.Small, color, TextAnchor.MiddleRight);
                if (row.Current)
                {
                    label.font = UiTheme.BoldFont;
                    value.font = UiTheme.BoldFont;
                }
            }

            foreach (string note in info.Notes) ui.Label(note, UiTextStyle.Small);
        }

        /// <summary>
        /// The building card: how many houses to add (or the hotel), what they cost and what money is
        /// left afterwards. Building sends one command per house; the host accepts each in turn.
        /// </summary>
        private void RenderBuildCard(UiBuilder ui)
        {
            GameState state = _controller.Session.State;
            PlayerState player = state.CurrentPlayer;
            int squareIndex = player.Position;
            SquareDefinition square = _controller.Session.Board[squareIndex];
            int level = state.GetBuildingLevel(squareIndex);
            bool hotel = GameRules.NextIsHotel(state, squareIndex);
            int most = AffordableBuildings(state, player);
            int count = Mathf.Clamp(_buildCount, 1, Mathf.Max(1, most));
            int cost = count * square.BuildingCost;

            ui.CardHeader(hotel ? F($"{L(square.Name)}: otel kur") : F($"{L(square.Name)}: ev kur"),
                F($"Şu an: {EventDescriber.LevelText(level)}, kira {GameRules.CalculateRent(_controller.Session.Board, _controller.Session.Rules, state, squareIndex)}"),
                _controller.GroupColorOf(squareIndex));

            if (!hotel)
            {
                UiBuilder pick = ui.Row();
                UiBuilder.Fix(pick.Label(L("Kaç ev?")), width: 110f);
                UiBuilder.Fix(pick.Button("-", () => _buildCount = count - 1, count > 1), width: 52f);
                pick.Label(count.ToString(), UiTextStyle.Heading, UiTheme.Accent, TextAnchor.MiddleCenter);
                UiBuilder.Fix(pick.Button("+", () => _buildCount = count + 1, count < most), width: 52f);
                ui.Label(player.Laps == 0
                    ? L("Başlangıçtan geçene kadar her şehre tek ev kurulur; sonra dörde kadar.")
                    : F($"Bu gelişte en çok {most} ev kurabilirsin."), UiTextStyle.Small, UiTheme.Muted);
            }

            UiBuilder sums = ui.Card(UiTheme.Field, 10);
            Line(sums, hotel ? L("Otel") : F($"{count} ev"), F($"{cost}"));
            Line(sums, L("Paran"), player.Money.ToString());
            Line(sums, L("Kalan para"), (player.Money - cost).ToString(), true);
            Line(sums, L("Yeni kira"), square.DevelopedRents[Mathf.Min(level + count, square.DevelopedRents.Count) - 1].ToString());

            ui.Actions(L("Şimdi Değil"), () => _buildCardDismissed = BuildVisitKey(state),
                hotel ? F($"Otel Kur ({cost})") : F($"{count} Ev Kur ({cost})"), () =>
                {
                    for (int i = 0; i < count; i++) _controller.Submit(new BuildCommand(player.Id, squareIndex));
                    _buildCardDismissed = BuildVisitKey(state);
                }, most > 0);
        }

        private static void Line(UiBuilder card, string label, string value, bool strong = false)
        {
            UiBuilder line = card.Row();
            Color color = strong ? UiTheme.Accent : UiTheme.Text;
            Text left = line.Label(label, UiTextStyle.Small, color);
            Text right = line.Label(value, UiTextStyle.Small, color, TextAnchor.MiddleRight);
            if (strong)
            {
                left.font = UiTheme.BoldFont;
                right.font = UiTheme.BoldFont;
            }
        }

        /// <summary>A ride from the travel square: the player picks the destination by clicking a square on the board.</summary>
        private void RenderTravel(UiBuilder ui, PlayerState player)
        {
            BoardDefinition board = _controller.Session.Board;
            int fee = board[player.Position].Fee;
            UiBuilder card = ui.Card();
            card.Label(F($"{board[player.Position].Name}: {fee} ödeyip istediğin kareye gidebilirsin."));
            card.Label(L("Tahtada gitmek istediğin kareye tıkla."), UiTextStyle.Small, UiTheme.Muted);

            int target = _pinnedSquare;
            var travel = new TravelCommand(player.Id, target);
            if (target >= 0 && _controller.IsAllowed(travel))
            {
                ui.Button(F($"{L(board[target].Name)} karesine git ({fee})"), () =>
                {
                    _pinnedSquare = NoSquare;
                    _controller.Submit(travel);
                }, style: UiButtonStyle.Primary, height: CardButton);
            }
            else
            {
                ui.Button(L("Önce bir kare seç"), null, false, UiButtonStyle.Primary, CardButton);
            }

            ui.Button(L("Burada kal"), () => _controller.Submit(new DeclineTravelCommand(player.Id)));
        }

        /// <summary>The lookout's two rolls, each with where it would take the player.</summary>
        private void RenderDiceChoice(UiBuilder ui, GameState state, PlayerState player)
        {
            BoardDefinition board = _controller.Session.Board;
            ui.Label(L("Seyir Terası: iki atıştan birini seç."), UiTextStyle.Small);
            for (int option = 0; option < 2; option++)
            {
                int[] dice = GameRules.DiceOption(state, option);
                if (dice == null) continue;
                int sum = dice[0] + dice[1];
                string destination = board[(player.Position + sum) % board.Count].Name;
                var choose = new ChooseDiceCommand(player.Id, option);
                ui.Button($"{dice[0]} + {dice[1]} = {sum}  →  {destination}", () => _controller.Submit(choose), _controller.IsAllowed(choose),
                    UiButtonStyle.Primary, CardButton);
            }
        }

        private void RenderDebt(UiBuilder ui, GameState state, PlayerState player)
        {
            DebtState debt = state.Debt;
            string creditor = EventDescriber.CreditorText(state, debt.CreditorId);
            UiBuilder card = ui.Card(new Color(0.30f, 0.16f, 0.16f));
            card.CardHeader(F($"Borç: {debt.Amount} → {creditor}"), EventDescriber.ReasonText(debt.Reason));
            card.Label(L("Bina ya da mülk satarak para bul, sonra öde."), UiTextStyle.Small);

            var pay = new PayDebtCommand(player.Id);
            ui.Actions(L("İflas Et"), () => _controller.Submit(new DeclareBankruptcyCommand(player.Id)),
                F($"Borcu Öde ({debt.Amount})"), () => _controller.Submit(pay), _controller.IsAllowed(pay), UiButtonStyle.Danger);
        }

        /// <summary>
        /// The event log, newest at the top so nothing needs scrolling to see what just happened. Each
        /// turn's lines sit under a heading in the player's colour; a dot in the actor's colour marks
        /// whose line each one is.
        /// </summary>
        private void RenderLog(UiBuilder ui)
        {
            ui.Title(UiIcon.List, L("Olaylar"));
            IReadOnlyList<LogEntry> entries = _controller.Log;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                LogEntry entry = entries[i];
                bool newest = i == entries.Count - 1;
                if (entry.IsTurnStart)
                {
                    if (!newest) ui.Space(4f);
                    UiBuilder heading = ui.Row(8f, expand: false);
                    heading.Swatch(_controller.ColorOf(entry.PlayerId), 12f, round: true);
                    Text title = heading.Label(entry.Text, UiTextStyle.Small, _controller.ColorOf(entry.PlayerId));
                    title.font = UiTheme.BoldFont;
                    continue;
                }

                Color color = entry.Tone == LogTone.Gain ? UiTheme.Gain : entry.Tone == LogTone.Loss ? UiTheme.Warning : newest ? UiTheme.Text : UiTheme.Muted;
                UiBuilder line = ui.Row(8f, expand: false);
                Color dot = entry.PlayerId == GameState.NoOwner ? Color.clear : _controller.ColorOf(entry.PlayerId);
                line.Swatch(dot, 8f, round: true).raycastTarget = false;
                UiBuilder.Fix(line.Label(entry.Text, UiTextStyle.Small, color), -1f).flexibleWidth = 1f;
            }
        }

        /// <summary>
        /// The final standings: solvent players by worth (money, properties and buildings), richest
        /// first, then the bankrupt ones. Each row shows money, properties and total worth.
        /// </summary>
        private void RenderStandings(UiBuilder ui, GameState state)
        {
            BoardDefinition board = _controller.Session.Board;
            var players = new List<PlayerState>(state.Players);
            players.Sort((a, b) =>
            {
                if (a.IsBankrupt != b.IsBankrupt) return a.IsBankrupt ? 1 : -1;
                return GameRules.NetWorth(board, state, b.Id).CompareTo(GameRules.NetWorth(board, state, a.Id));
            });

            UiBuilder table = ui.Card(UiTheme.Field, 10);
            UiBuilder head = table.Row();
            UiBuilder.Fix(head.Label("", UiTextStyle.Small, UiTheme.Muted), width: 30f);
            head.Label(L("Oyuncu"), UiTextStyle.Small, UiTheme.Muted);
            UiBuilder.Fix(head.Label(L("Para"), UiTextStyle.Small, UiTheme.Muted, TextAnchor.MiddleRight), width: 80f);
            UiBuilder.Fix(head.Label(L("Mülk"), UiTextStyle.Small, UiTheme.Muted, TextAnchor.MiddleRight), width: 60f);
            UiBuilder.Fix(head.Label(L("Değer"), UiTextStyle.Small, UiTheme.Muted, TextAnchor.MiddleRight), width: 90f);

            for (int i = 0; i < players.Count; i++)
            {
                PlayerState player = players[i];
                bool winner = player.Id == state.WinnerId;
                Color color = player.IsBankrupt ? UiTheme.Muted : UiTheme.Text;
                UiBuilder row = table.Row();
                UiBuilder.Fix(row.Label($"{i + 1}.", color: color), width: 30f);
                row.Swatch(player.IsBankrupt ? UiTheme.Muted : _controller.ColorOf(player.Id), 16f, round: true);
                Text name = row.Label(player.Name, color: color);
                if (winner) name.font = UiTheme.BoldFont;
                if (player.IsBankrupt)
                {
                    UiBuilder.Fix(row.Label(L("iflas"), color: color, alignment: TextAnchor.MiddleRight), width: 230f);
                    continue;
                }

                UiBuilder.Fix(row.Label(player.Money.ToString(), color: color, alignment: TextAnchor.MiddleRight), width: 80f);
                UiBuilder.Fix(row.Label(OwnedCount(state, player.Id).ToString(), color: color, alignment: TextAnchor.MiddleRight), width: 60f);
                UiBuilder.Fix(row.Label(GameRules.NetWorth(board, state, player.Id).ToString(), color: UiTheme.Accent, alignment: TextAnchor.MiddleRight), width: 90f);
            }
        }

        private static int OwnedCount(GameState state, int playerId)
        {
            int count = 0;
            for (int square = 0; square < state.SquareCount; square++)
            {
                if (state.GetOwner(square) == playerId) count++;
            }

            return count;
        }

        /// <summary>The strip under the log: the pause menu and help. Settings and leaving live in the pause menu.</summary>
        private void RenderMenu(UiBuilder ui)
        {
            UiBuilder row = ui.Row();
            row.IconButton(UiIcon.Pause, L("Menü (Esc)"), OpenPause);
            row.IconButton(UiIcon.Book, L("Nasıl Oynanır (H)"), OpenHowToPlay);
        }

        private void OpenPause()
        {
            _pauseLayer.SetActive(true);
            _pauseWindow.Invalidate();
            _controller.SetPaused(true);
        }

        private void ClosePause()
        {
            _pauseLayer.SetActive(false);
            _controller.SetPaused(false);
        }

        private void OpenSettings()
        {
            _settingsLayer.SetActive(true);
            _settingsWindow.Invalidate();
        }

        private void OpenHowToPlay()
        {
            _howToLayer.SetActive(true);
            _howToWindow.Invalidate();
        }

        /// <summary>
        /// The pause menu: carry on, settings, help, the shortcut list and the way out. A local game
        /// stands still behind it and is saved on leaving; an online game goes on, so it says so.
        /// </summary>
        private void RenderPause(UiBuilder ui)
        {
            GameState state = _controller.Session.State;
            bool over = _controller.EndReason.HasValue || (state != null && state.Phase == GamePhase.GameOver);
            bool online = _controller.IsOnline;
            ui.Title(UiIcon.Pause, online || over ? L("Menü") : L("Oyun duraklatıldı"));
            if (online && !over) ui.Label(L("Çevrim içi oyun arkada sürer; hamle süresi işlemeye devam eder."), UiTextStyle.Small, UiTheme.Muted);

            ui.Button(L("Devam Et (Esc)"), ClosePause, style: UiButtonStyle.Primary, height: CardButton);
            UiBuilder row = ui.Row();
            row.IconButton(UiIcon.Sliders, L("Ayarlar"), OpenSettings);
            row.IconButton(UiIcon.Book, L("Nasıl Oynanır (H)"), OpenHowToPlay);
            UiBuilder view = ui.Row();
            view.IconButton(UiIcon.Home, L("Görünümü Sıfırla (Home)"), () => _controller.CameraRig?.ResetView(), height: UiTheme.SmallButtonHeight);
            view.IconButton(UiIcon.Camera, L("Kamera Açısı (C)"), _controller.ToggleCameraView, height: UiTheme.SmallButtonHeight);

            UiBuilder keys = ui.Card(UiTheme.Field, 10);
            keys.Label(L("Klavye"), UiTextStyle.Small, UiTheme.Muted);
            Line(keys, L("Boşluk / Enter"), L("zar at, sırayı bitir"));
            Line(keys, "B", L("bina kartı"));
            Line(keys, "T", L("takas teklif et"));
            Line(keys, "H", L("nasıl oynanır"));
            Line(keys, "C", L("kamera açısı: masa başı / kuş bakışı"));
            Line(keys, "Home", L("görünümü sıfırla"));
            Line(keys, "F3 / F11 / F12", L("kare hızı, ekran görüntüsü açısı, ekran görüntüsü"));
            Line(keys, "Esc", L("menü, pencereyi kapat"));

            ui.Space(4f);
            if (over)
            {
                ui.IconButton(UiIcon.Door, L("Ana Menü"), _controller.LeaveToMainMenu, style: UiButtonStyle.Danger);
            }
            else if (!online)
            {
                ui.Label(L("Oyun kaydedilir; ana menüdeki Kayıtlı Oyuna Devam Et düğmesiyle sürdürebilirsin."), UiTextStyle.Small, UiTheme.Muted);
                ui.IconButton(UiIcon.Door, L("Kaydet ve Çık"), _controller.LeaveToMainMenu, style: UiButtonStyle.Danger);
            }
            else
            {
                ui.Label(_controller.IsHost ? L("Odayı kapatırsın; oyun herkes için biter.") : L("Odadan çıkarsın; yerine bilgisayar oynar."),
                    UiTextStyle.Small, UiTheme.Muted);
                ui.IconButton(UiIcon.Door, L("Oyundan Ayrıl"), _controller.LeaveToMainMenu, style: UiButtonStyle.Danger);
            }
        }

        /// <summary>Turns the keys pressed this frame into the actions their buttons would take.</summary>
        private void ReadHotkeys(GameState state)
        {
            if (!Input.anyKeyDown) return;
            foreach (KeyCode key in Hotkeys.Keys)
            {
                if (Input.GetKeyDown(key) && PressKey(key)) return;
            }
        }

        /// <summary>Acts on one key as the keyboard would; tests press keys through it. True when the key did something.</summary>
        public bool PressKey(KeyCode key)
        {
            GameState state = _controller.Session?.State;
            HotkeyAction action = Hotkeys.Resolve(key, CurrentHotkeyContext(state));
            if (action == HotkeyAction.None) return false;
            AudioPlayer.Play(SoundCue.UiClick);
            Perform(action, state);
            return true;
        }

        private HotkeyContext CurrentHotkeyContext(GameState state)
        {
            bool acting = state != null && WaitsOnPersonHere(state, state.Phase);
            return new HotkeyContext
            {
                Typing = IsTyping(),
                PauseOpen = _pauseLayer.activeSelf,
                ClosableOpen = _settingsLayer.activeSelf || _howToLayer.activeSelf || _tradeLayer.activeSelf || _endLayer.activeSelf || _buildLayer.activeSelf,
                BlockingOpen = _cityLayer.activeSelf,
                Acting = acting,
                Phase = state?.Phase ?? GamePhase.GameOver,
                CanBuild = acting && OffersExtras(state) && CanReopenBuildCard(state),
                CanTrade = acting && OffersExtras(state),
            };
        }

        /// <summary>True while a text field (a trade amount, say) has the keyboard.</summary>
        private static bool IsTyping()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null) return false;
            var field = selected.GetComponent<InputField>();
            return field != null && field.isFocused;
        }

        private void Perform(HotkeyAction action, GameState state)
        {
            switch (action)
            {
                case HotkeyAction.Roll:
                    Submit(new RollDiceCommand(state.CurrentPlayer.Id));
                    break;
                case HotkeyAction.EndTurn:
                    Submit(new EndTurnCommand(state.CurrentPlayer.Id));
                    break;
                case HotkeyAction.BuildCard:
                    ReopenBuildCard();
                    break;
                case HotkeyAction.Trade:
                    _trade.Open(state.CurrentPlayer.Id);
                    break;
                case HotkeyAction.HowToPlay:
                    OpenHowToPlay();
                    break;
                case HotkeyAction.PauseMenu:
                    OpenPause();
                    break;
                case HotkeyAction.CameraView:
                    _controller.ToggleCameraView();
                    break;
                case HotkeyAction.PerfOverlay:
                    TogglePerfOverlay();
                    break;
                case HotkeyAction.ScreenshotPreset:
                    _controller.NextScreenshotPreset();
                    break;
                case HotkeyAction.Screenshot:
                    _controller.TakeScreenshot();
                    break;
                case HotkeyAction.Close:
                    CloseTopWindow(state);
                    break;
            }

            _side.Invalidate();
        }

        /// <summary>A shortcut sends only what its button would: the same command, only when the rules allow it now.</summary>
        private void Submit(GameCommand command)
        {
            if (_controller.IsAllowed(command)) _controller.Submit(command);
        }

        /// <summary>Esc closes whatever is on top, in the order the windows stack.</summary>
        private void CloseTopWindow(GameState state)
        {
            if (_settingsLayer.activeSelf) _settingsLayer.SetActive(false);
            else if (_howToLayer.activeSelf) _howToLayer.SetActive(false);
            else if (_pauseLayer.activeSelf) ClosePause();
            else if (_tradeLayer.activeSelf) _trade.Close();
            else if (_buildLayer.activeSelf && state != null) _buildCardDismissed = BuildVisitKey(state);
            else if (_endLayer.activeSelf) _endDismissed = true;
        }

        private void RenderTrade(UiBuilder ui)
        {
            GameState state = _controller.Session.State;
            if (state != null) _trade.RenderEditor(ui, state);
        }

        private void RenderEnd(UiBuilder ui)
        {
            if (_controller.EndReason.HasValue)
            {
                ui.Title(UiIcon.Warning, L("Oyun durdu"), UiTheme.Warning);
                ui.Notice(GameSceneController.EndReasonText(_controller.EndReason.Value));
                RenderContinueOffline(ui);
            }
            else
            {
                GameState state = _controller.Session.State;
                PlayerState winner = state.FindPlayer(state.WinnerId);
                ui.Title(UiIcon.Flag, L("Oyun bitti"));
                ui.Label(F($"Kazanan: {winner?.Name}"), UiTextStyle.Title, winner != null ? _controller.ColorOf(winner.Id) : UiTheme.Accent, TextAnchor.MiddleCenter);
                bool byLimit = _controller.Session.Rules.RoundLimit > 0 && state.ActivePlayerCount() > 1;
                ui.Label(byLimit ? F($"Tur sınırı doldu ({state.Round} tur): en zengin oyuncu kazandı.") : F($"{state.Round} turda bitti."),
                    UiTextStyle.Small, alignment: TextAnchor.MiddleCenter);
                RenderStandings(ui, state);
            }

            ui.Space(8f);
            UiBuilder row = ui.Row();
            row.Button(L("Tahtaya Bak"), () => _endDismissed = true);
            if (_controller.CanRestart) row.Button(L("Yeniden Oyna"), _controller.RestartLocalGame);
            row.Button(L("Ana Menü"), _controller.LeaveToMainMenu, style: UiButtonStyle.Primary);
        }
    }
}
