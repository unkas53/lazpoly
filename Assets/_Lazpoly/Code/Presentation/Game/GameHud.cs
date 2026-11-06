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
using Lazpoly.Presentation.Models;
using Lazpoly.Presentation.Tabletop;
using Lazpoly.Presentation.Texts;
using Lazpoly.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Game
{
    /// <summary>The windows of the game screen that a button or a key opens.</summary>
    public enum HudWindow { Pause, Settings, HowToPlay, Properties, Log, Trade }

    /// <summary>
    /// The game screen's interface around the diamond-shaped board: a player card in each corner, the
    /// big roll button over the lawn, decisions in a panel at the bottom, the newest event and a few small
    /// buttons in a strip under it, and windows for the log, properties, trading, settings, pausing and the end of
    /// the game. It offers only actions <see cref="GameSceneController.IsAllowed"/> approves; the host
    /// still validates them. Keyboard shortcuts (<see cref="Hotkeys"/>) press the same buttons.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        private const float CornerWidth = UiLayout.CornerWidth;
        private const float Margin = UiLayout.Margin;

        /// <summary>Width of the stack at the bottom centre: the hint, the decision panel and the event strip.</summary>
        public const float BottomWidth = 760f;

        private GameSceneController _controller;
        private TradePanel _trade;
        private SettingsPanel _settings;
        private Canvas _canvas;
        private readonly UiView[] _corners = new UiView[4];
        private UiView _turn;
        private RectTransform _turnPanel;
        private RectTransform _bottom;
        private UiView _ticker;
        private UiView _logWindow;
        private GameObject _logLayer;
        private UiView _propertiesWindow;
        private GameObject _propertiesLayer;
        private UiView _tradeWindow;
        private UiView _settingsWindow;
        private UiView _endWindow;
        private GameObject _tradeLayer;
        private GameObject _settingsLayer;
        private GameObject _endLayer;
        private UiView _pauseWindow;
        private GameObject _pauseLayer;
        private bool _endDismissed;
        private UiView _squareWindow;
        private GameObject _squareLayer;
        private SquareCard _squareCard;
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
        private CanvasGroup _rollGroup;
        private RectTransform _rollPrompt;
        private float _rollPromptShownAt = float.NegativeInfinity;
        private RectTransform _rollRect;
        private readonly Dictionary<int, RollingNumber> _money = new Dictionary<int, RollingNumber>();
        private readonly Dictionary<int, Text> _moneyLabels = new Dictionary<int, Text>();
        private readonly Dictionary<int, PlayerCardView> _playerCards = new Dictionary<int, PlayerCardView>();
        private readonly Dictionary<int, float> _moneyFlash = new Dictionary<int, float>();
        private readonly Dictionary<int, int> _moneyFlashSign = new Dictionary<int, int>();
        private TipMemory _tipMemory;
        private UiView _tipView;
        private GameObject _tipPanel;
        private Tip? _tip;
        private UiView _howToWindow;
        private Text _timer;
        private Image _bannerFill;
        private CanvasGroup _timeEdge;
        private GameObject _howToLayer;
        private UiView _cityWindow;
        private GameObject _cityLayer;
        private UiView _buildWindow;
        private GameObject _buildLayer;
        private UiView _jailWindow;
        private GameObject _jailLayer;
        private UiView _offerWindow;
        private GameObject _offerLayer;
        private int _buildCardDismissed;
        private PropertyCard _propertyCard;
        private DebtSaleView _debtSale;
        private BuildingPreview _preview;

        private const int NoSquare = -1;
        private const float BannerSeconds = 1.8f;

        /// <summary>How long the turn's end waits, once nothing is left to decide, before the turn passes on.</summary>
        public const float AutoEndDelaySeconds = 0.6f;

        private float _turnEndSince = -1f;
        private int _autoEndedKey;
        private float _announcedAt = float.NegativeInfinity;
        private const float RollSeconds = 1.5f;
        private const float CardButton = UiLayout.CardButtonHeight;
        private const float WindowListHeight = 420f;

        /// <summary>The debt card's sale list: about five properties before it scrolls.</summary>
        /// <summary>The corner card of the player on turn: a wash of the masthead green, clearly greener than the cream cards and still light enough for dark ink.</summary>
        public static readonly Color CurrentCardColor = UiPalette.Ink.Wash(UiPalette.Ink.Masthead, 0.56f);

        /// <summary>Screen pixels kept clear of the board at each side edge (see <see cref="UiLayout.ReservedSidePixels"/>).</summary>
        public float ReservedSidePixels => UiLayout.ReservedSidePixels(_canvas != null ? _canvas.scaleFactor : 1f);

        /// <summary>Whether the decision panel at the bottom has something to show.</summary>
        public bool TurnPanelShown => _turnPanel != null && _turnPanel.gameObject.activeSelf;

        /// <summary>
        /// Screen pixels the bottom stack covers while the decision panel is up, margin included, so the
        /// camera can lift the board clear of it; nothing while the stack holds only the hint and the strip.
        /// </summary>
        public float ReservedBottomPixels =>
            TurnPanelShown && _bottom != null ? (_bottom.rect.height + 2f * Margin) * (_canvas != null ? _canvas.scaleFactor : 1f) : 0f;

        /// <summary>Whether the event log window is open.</summary>
        public bool LogOpen => _logLayer != null && _logLayer.activeSelf;

        /// <summary>Whether the properties window (build and sell) is open.</summary>
        public bool PropertiesOpen => _propertiesLayer != null && _propertiesLayer.activeSelf;

        /// <summary>The square whose card is open: the one clicked on the board or in a player's card; or -1.</summary>
        public int ShownSquare => _pinnedSquare;

        /// <summary>The first-game hint on screen, or null.</summary>
        public Tip? ShownTip => _tip;

        /// <summary>Whether the city card (buy or move on) is open for the person here.</summary>
        public bool CityCardOpen => _cityLayer != null && _cityLayer.activeSelf;

        /// <summary>Whether the building card (how many houses, or the hotel) is open for the person here.</summary>
        public bool BuildCardOpen => _buildLayer != null && _buildLayer.activeSelf;

        /// <summary>The sale screen of a person who cannot pay (the card and the tags over their towns).</summary>
        public DebtSaleView DebtSale => _debtSale;

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

        /// <summary>Opens one of the windows the game screen's buttons and keys open, as they would (the screen tour uses it).</summary>
        public void Open(HudWindow window)
        {
            GameState state = _controller.Session?.State;
            switch (window)
            {
                case HudWindow.Pause: OpenPause(); break;
                case HudWindow.Settings: OpenSettings(); break;
                case HudWindow.HowToPlay: OpenHowToPlay(); break;
                case HudWindow.Properties: OpenProperties(); break;
                case HudWindow.Log:
                    if (!_logLayer.activeSelf) ToggleLog();
                    break;
                case HudWindow.Trade:
                    if (state != null) _trade.Open(state.CurrentPlayer.Id);
                    break;
            }
        }

        /// <summary>Closes the window on top, as Esc does.</summary>
        public void CloseTop() => CloseTopWindow(_controller.Session?.State);

        public void Bind(GameSceneController controller)
        {
            _controller = controller;
            _trade = new TradePanel(controller);
            _settings = new SettingsPanel();
            _preview = new BuildingPreview(transform);
            _propertyCard = new PropertyCard(controller, _preview);
            _squareCard = new SquareCard(controller, _preview);
            Build();
        }

        private void OnDestroy() => _preview?.Dispose();

        private void Build()
        {
            _canvas = UiRoot.CreateCanvas(transform, "Game UI", 0);

            // A player card in each corner: 0 top left, 1 top right, 2 bottom left, 3 bottom right; seats 5-8 stack on 1-4.
            for (int corner = 0; corner < 4; corner++)
            {
                var anchor = new Vector2(corner % 2, corner < 2 ? 1f : 0f);
                RectTransform rect = UiRoot.Rect(_canvas.transform, $"Corner {corner}", anchor, anchor, Vector2.zero, Vector2.zero);
                rect.pivot = anchor;
                rect.anchoredPosition = new Vector2(anchor.x == 0f ? Margin : -UiLayout.RightInset, anchor.y == 0f ? Margin : -Margin);
                rect.sizeDelta = new Vector2(CornerWidth, 0f);
                UiBuilder.AddColumnLayout(rect.gameObject, 0, UiTheme.Spacing);
                rect.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                int index = corner;
                _corners[corner] = new UiView(new UiBuilder(rect), ui => RenderCorner(ui, index));
            }

            // The stack at the bottom centre: the hint on top, the decision panel, and the event strip at the very bottom.
            RectTransform bottom = UiRoot.Rect(_canvas.transform, "Bottom", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
            bottom.pivot = new Vector2(0.5f, 0f);
            bottom.anchoredPosition = new Vector2(0f, Margin);
            bottom.sizeDelta = new Vector2(BottomWidth, 0f);
            UiBuilder.AddColumnLayout(bottom.gameObject, 0, UiTheme.Spacing);
            bottom.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _bottom = bottom;
            BuildTipPanel(bottom);
            _turnPanel = UiRoot.Rect(bottom, "Turn", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            UiRoot.Panel(_turnPanel);
            UiBuilder.AddColumnLayout(_turnPanel.gameObject, 14, UiTheme.Spacing);
            _turn = new UiView(new UiBuilder(_turnPanel), RenderTurn);
            _turnPanel.gameObject.SetActive(false);
            RectTransform ticker = UiRoot.Rect(bottom, "Ticker", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            UiBuilder.AddColumnLayout(ticker.gameObject, 0, 0f);
            _ticker = new UiView(new UiBuilder(ticker), RenderTicker);

            // A debt's sale screen: its card in the middle and a tag over each of the player's towns.
            _debtSale = new DebtSaleView(_canvas.transform, _controller);

            // Frame rate and memory behind F3, in the strip above the turn banner.
            RectTransform perf = UiRoot.Rect(_canvas.transform, "Perf Overlay", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-200f, -44f), new Vector2(200f, -14f));
            _perfLabel = new UiBuilder(perf).Label("", UiTextStyle.Small, UiTheme.Muted, TextAnchor.MiddleCenter);
            perf.gameObject.SetActive(false);
            _perfRect = perf;

            RectTransform banner = UiRoot.Rect(_canvas.transform, "Turn Banner", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-320f, -150f), new Vector2(320f, -60f));
            _bannerFill = UiRoot.Banner(banner, 90f, dark: false); // The cream skin, tinted in the player's colour when it shows.
            _bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            _bannerGroup.blocksRaycasts = false;
            _bannerGroup.alpha = 0f;
            _banner = new UiBuilder(banner).Label("", UiTextStyle.Heading, UiPalette.Ink.OnMasthead, TextAnchor.MiddleCenter);
            var bannerRect = (RectTransform)_banner.transform;
            bannerRect.anchorMin = Vector2.zero;
            bannerRect.anchorMax = Vector2.one;
            bannerRect.offsetMin = Vector2.zero;
            bannerRect.offsetMax = Vector2.zero;
            _banner.fontSize = 34;

            BuildCardPopup();
            BuildRollPopup();
            BuildRollPrompt();

            RectTransform timer = UiRoot.Rect(_canvas.transform, "Move Timer", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-180f, -52f), new Vector2(180f, -12f));
            _timer = new UiBuilder(timer).Label("", UiTextStyle.Body, UiTheme.Muted, TextAnchor.MiddleCenter);
            var timerRect = (RectTransform)_timer.transform;
            timerRect.anchorMin = Vector2.zero;
            timerRect.anchorMax = Vector2.one;
            timerRect.offsetMin = Vector2.zero;
            timerRect.offsetMax = Vector2.zero;
            _timer.raycastTarget = false;
            BuildTimeEdge();

            // The pause menu comes first, so settings and help opened from it draw on top of it.
            _pauseWindow = new UiView(UiRoot.Modal(_canvas.transform, "Pause", UiLayout.WindowWidth, out _pauseLayer), RenderPause);
            _tradeWindow = new UiView(UiRoot.Modal(_canvas.transform, "Trade", UiLayout.WideWindowWidth, out _tradeLayer), RenderTrade);
            _settingsWindow = new UiView(UiRoot.Modal(_canvas.transform, "Settings", UiLayout.WindowWidth, out _settingsLayer),
                ui => _settings.Render(ui, () => _settingsLayer.SetActive(false)));
            _endWindow = new UiView(UiRoot.Modal(_canvas.transform, "Game End", UiLayout.WindowWidth, out _endLayer), RenderEnd);
            _howToWindow = new UiView(UiRoot.Modal(_canvas.transform, "How To Play", HowToPlayPanel.Width, out _howToLayer),
                ui => HowToPlayPanel.Render(ui, _controller.Session.Board, _controller.Session.Rules, () => _howToLayer.SetActive(false)));
            _cityWindow = new UiView(UiRoot.Modal(_canvas.transform, "City Card", UiLayout.PropertyCardWidth, out _cityLayer), RenderCityCard);
            _buildWindow = new UiView(UiRoot.Modal(_canvas.transform, "Build Card", UiLayout.PropertyCardWidth, out _buildLayer), RenderBuildCard);
            // A jailed turn's choice opens in the middle of the screen (the owner's wish, Faz 23).
            _jailWindow = new UiView(UiRoot.Modal(_canvas.transform, "Jail Card", UiLayout.WindowWidth, out _jailLayer), RenderJailCard);
            // A trade offer's answer opens in the middle too, for whoever here sent or received it (Faz 23).
            _offerWindow = new UiView(UiRoot.Modal(_canvas.transform, "Trade Offer", UiLayout.WindowWidth, out _offerLayer), RenderOffer);
            // The log and the properties open on request, over whatever card is up.
            _logWindow = new UiView(UiRoot.Modal(_canvas.transform, "Log", UiLayout.WindowWidth, out _logLayer), RenderLogWindow);
            _propertiesWindow = new UiView(UiRoot.Modal(_canvas.transform, "Properties", UiLayout.WindowWidth, out _propertiesLayer), RenderProperties);
            // The square card opens over everything on a click, in the middle of the screen; a click on the dimmed board closes it.
            _squareWindow = new UiView(UiRoot.Modal(_canvas.transform, "Square Card", UiLayout.WindowWidth, out _squareLayer), RenderSquareCard);
            var dim = _squareLayer.AddComponent<Button>();
            dim.transition = Selectable.Transition.None;
            dim.onClick.AddListener(() => _pinnedSquare = NoSquare);
            _squareLayer.SetActive(false);
            _logLayer.SetActive(false);
            _propertiesLayer.SetActive(false);
            _pauseLayer.SetActive(false);
            _howToLayer.SetActive(false);
            _tradeLayer.SetActive(false);
            _settingsLayer.SetActive(false);
            _endLayer.SetActive(false);
            _cityLayer.SetActive(false);
            _buildLayer.SetActive(false);
            _jailLayer.SetActive(false);
            _offerLayer.SetActive(false);
        }

        /// <summary>The drawn "Sokak Haberi" card, shown in the middle for a few seconds. It never takes the mouse.</summary>
        private void BuildCardPopup()
        {
            RectTransform card = UiRoot.Rect(_canvas.transform, "Card Popup", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-260f, -40f), new Vector2(260f, 150f));
            UiRoot.Panel(card).raycastTarget = false; // A sheet of the kit's paper, like every window.
            _cardGroup = card.gameObject.AddComponent<CanvasGroup>();
            _cardGroup.blocksRaycasts = false;
            _cardGroup.interactable = false;
            _cardGroup.alpha = 0f;
            _cardRect = card;
            UiBuilder.AddColumnLayout(card.gameObject, 18, 8f);
            var builder = new UiBuilder(card);
            _cardTitle = builder.Label(L("SOKAK HABERİ"), UiTextStyle.Heading, UiTheme.Accent, TextAnchor.MiddleCenter);
            _cardText = builder.Label("", UiTextStyle.Body, UiTheme.Text, TextAnchor.MiddleCenter);
            _cardText.fontSize = 22;
        }

        /// <summary>A note under the banner when a double is rolled (the lawn shows the figures). It never takes the mouse.</summary>
        private void BuildRollPopup()
        {
            RectTransform roll = UiRoot.Rect(_canvas.transform, "Roll Popup", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-220f, -214f), new Vector2(220f, -160f));
            UiRoot.Banner(roll, 54f);
            _rollGroup = roll.gameObject.AddComponent<CanvasGroup>();
            _rollGroup.blocksRaycasts = false;
            _rollGroup.interactable = false;
            _rollGroup.alpha = 0f;
            _rollRect = roll;
            UiBuilder.AddColumnLayout(roll.gameObject, 10, 2f);
            _rollText = new UiBuilder(roll).Label("", UiTextStyle.Body, UiPalette.Ink.OnMasthead, TextAnchor.MiddleCenter);
            _rollText.font = UiTheme.BoldFont;
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

            // Only doubles are worth a note, and only to the person who rolled them: a computer's doubles show in its bubble.
            if (_controller.ShownDie1 != _controller.ShownDie2 || !_controller.ShownRollByPerson)
            {
                _rollGroup.alpha = 0f;
                return;
            }

            _rollText.text = _controller.ShownRollForfeited ? L("Üçüncü çift: hamle yanar, sıra geçer.") : L("Çift! Sıra bitince yeniden atarsın.");
            _rollText.color = _controller.ShownRollForfeited ? UiPalette.Ink.Gold : UiPalette.Ink.OnMasthead;
            _rollGroup.alpha = Mathf.Clamp01(age / 0.12f) * Mathf.Clamp01((visible - age) / 0.35f);
            _rollRect.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, Ease.OutBack(Mathf.Clamp01(age / 0.25f)));
        }

        /// <summary>The roll button's red: it tints the kit's cream skin to a brick red.</summary>
        public static readonly Color RollPromptRed = new Color(0.88f, 0.25f, 0.21f);

        /// <summary>
        /// The big "roll" button over the lawn: it pops up when the roll is this person's and goes the
        /// moment it is pressed. Space and Enter roll too.
        /// </summary>
        private void BuildRollPrompt()
        {
            RectTransform prompt = UiRoot.Rect(_canvas.transform, "Roll Prompt", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            prompt.pivot = new Vector2(0.5f, 0.5f);
            prompt.anchoredPosition = new Vector2(0f, -UiLayout.RollPromptDrop);
            prompt.sizeDelta = new Vector2(UiLayout.RollPromptWidth, UiLayout.RollPromptHeight);
            UiBuilder.AddColumnLayout(prompt.gameObject, 0, 0f);
            // Red with white words (the owner's wish, Faz 23): the kit's cream skin tinted, so hover and press still show.
            Button button = new UiBuilder(prompt).Button(L("Zar At").ToUpperInvariant(), PressRollPrompt, true, UiButtonStyle.Secondary, UiLayout.RollPromptHeight);
            button.GetComponent<Image>().color = RollPromptRed;
            Text label = button.GetComponentInChildren<Text>();
            label.color = Color.white;
            label.fontSize = 34;
            label.resizeTextMaxSize = 34;
            _rollPrompt = prompt;
            prompt.gameObject.SetActive(false);
        }

        private void PressRollPrompt()
        {
            GameState state = _controller.Session.State;
            if (!WaitsOnPersonHere(state, GamePhase.WaitingForRoll)) return;
            _rollPrompt.gameObject.SetActive(false);
            Submit(new RollDiceCommand(state.CurrentPlayer.Id));
        }

        /// <summary>Shows the roll button while the roll is this person's and no window covers the board; it pops in when it appears.</summary>
        private void UpdateRollPrompt(GameState state, bool windowOpen)
        {
            if (_rollPrompt == null) return;
            bool shown = !windowOpen && WaitsOnPersonHere(state, GamePhase.WaitingForRoll);
            if (shown != _rollPrompt.gameObject.activeSelf)
            {
                _rollPrompt.gameObject.SetActive(shown);
                if (shown) _rollPromptShownAt = Time.unscaledTime;
            }

            if (!shown) return;
            float age = Time.unscaledTime - _rollPromptShownAt;
            _rollPrompt.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, Ease.OutBack(Mathf.Clamp01(age / 0.28f)));
        }

        /// <summary>A hint box at the top of the bottom stack.</summary>
        private void BuildTipPanel(RectTransform stack)
        {
            RectTransform tip = UiRoot.Rect(stack, "Tip", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            UiRoot.Card(tip);
            UiBuilder.AddColumnLayout(tip.gameObject, 14, 8f);
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

        /// <summary>Seconds before the decision clock runs out during which the screen edge glows red.</summary>
        public const float EdgeWarningSeconds = 5f;

        /// <summary>Four thin strips along the screen edges, invisible until the move clock nearly runs out.</summary>
        private void BuildTimeEdge()
        {
            RectTransform edge = UiRoot.Stretch(_canvas.transform, "Time Edge");
            _timeEdge = edge.gameObject.AddComponent<CanvasGroup>();
            _timeEdge.alpha = 0f;
            _timeEdge.blocksRaycasts = false;
            _timeEdge.interactable = false;
            const float thickness = 14f;
            Strip("Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), Vector2.zero);
            Strip("Bottom", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness));
            Strip("Left", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f));
            Strip("Right", new Vector2(1f, 0f), Vector2.one, new Vector2(-thickness, 0f), Vector2.zero);

            void Strip(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
            {
                RectTransform strip = UiRoot.Rect(edge, name, anchorMin, anchorMax, offsetMin, offsetMax);
                Image image = UiRoot.Background(strip, UiTheme.Warning, rounded: false);
                image.raycastTarget = false;
            }
        }

        /// <summary>
        /// While the game waits on the person here: how long until the game decides for them (rolls, lets the
        /// town go, ends the turn, sells). In the last <see cref="EdgeWarningSeconds"/> the screen edge pulses
        /// red as well, so the warning reaches a player looking at the board rather than the clock. No clock
        /// at a table without one.
        /// </summary>
        private void UpdateTimer(GameState state)
        {
            float seconds = _controller.SecondsLeft;
            bool show = state != null && state.Phase != GamePhase.GameOver && !_controller.IsBusy && !_controller.Paused &&
                        !_controller.EndReason.HasValue && !float.IsInfinity(seconds) && WaitsOnSomeoneHere(state);
            ShowTimeOnCard(state, show ? seconds : (float?)null);
            if (!show)
            {
                _timer.text = "";
                _timeEdge.alpha = 0f;
                return;
            }

            int left = Mathf.Max(0, Mathf.CeilToInt(seconds));
            _timer.text = F($"Kalan süre: {left} sn");
            bool urgent = seconds <= EdgeWarningSeconds;
            _timer.color = urgent ? UiTheme.Warning : UiTheme.Muted;
            _timeEdge.alpha = urgent ? 0.35f + 0.35f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * Mathf.PI)) : 0f;
        }

        /// <summary>The thin clock line on the card on turn: the share of the decision's time left, hidden when nobody here is timed.</summary>
        private void ShowTimeOnCard(GameState state, float? seconds)
        {
            int current = state != null ? state.CurrentPlayer.Id : NoPlayer;
            float limit = state != null ? (float)TurnClock.LimitFor(state.Phase, _controller.Session.Rules.TurnSeconds) : 0f;
            foreach (KeyValuePair<int, PlayerCardView> card in _playerCards)
            {
                bool timed = card.Key == current && seconds.HasValue && limit > 0f && !float.IsInfinity(limit);
                card.Value.SetTime(timed ? seconds.Value / limit : (float?)null);
            }
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
            if (_controller?.Session == null || _ticker == null) return;
            GameState state = _controller.Session.State;
            ReadHotkeys(state);
            int revision = _controller.Revision;

            bool tradeOpen = state != null && !_controller.EndReason.HasValue && _trade.KeepOpen(state);
            // The pinned square is the destination of a ride, so the decision panel follows it too.
            // While a debt is sold on the board, the town under the mouse is named with its price on the panel.
            int key = HashCode.Combine(revision, _controller.IsBusy, _controller.EndReason, tradeOpen, _pinnedSquare,
                SellingOnBoard(state) ? _hoveredSquare : NoSquare);
            foreach (UiView corner in _corners) corner.Refresh(key);
            _turn.Refresh(key);
            // The panel shows only while there is something to decide or to say.
            bool turnShown = _turnPanel.childCount > 0;
            if (_turnPanel.gameObject.activeSelf != turnShown) _turnPanel.gameObject.SetActive(turnShown);
            _ticker.Refresh(key);

            UpdateMoney();
            UpdatePointer(state);
            UpdatePerfOverlay();
            UpdateSquareCard(state, revision);
            bool selling = state != null && SellingOnBoard(state);
            _debtSale.Update(state, selling ? BoardChoices(state) : null, selling && !_controller.IsBusy && !_controller.EndReason.HasValue);
            UpdateBanner(state);
            UpdateCardPopup();
            UpdateRollPopup();
            UpdateTip(state);
            UpdateTimer(state);
            AutoEndTurn(state, tradeOpen);

            SetLayer(_tradeLayer, _tradeWindow, tradeOpen, revision);
            bool cityOpen = !tradeOpen && WaitsOnPersonHere(state, GamePhase.AwaitingBuyDecision);
            SetLayer(_cityLayer, _cityWindow, cityOpen, revision);
            SetLayer(_offerLayer, _offerWindow, OffersAnswerHere(state), revision);
            bool jailOpen = !tradeOpen && WaitsOnPersonHere(state, GamePhase.AwaitingJailDecision);
            SetLayer(_jailLayer, _jailWindow, jailOpen, revision);
            bool buildOpen = !tradeOpen && !cityOpen && OffersBuildCard(state);
            SetLayer(_buildLayer, _buildWindow, buildOpen, revision);
            SetLayer(_pauseLayer, _pauseWindow, _pauseLayer.activeSelf, HashCode.Combine(revision, _controller.EndReason, _controller.ReportNote));
            SetLayer(_settingsLayer, _settingsWindow, _settingsLayer.activeSelf, 0);
            SetLayer(_howToLayer, _howToWindow, _howToLayer.activeSelf, 0);
            bool ended = !_endDismissed && (_controller.EndReason.HasValue || (state != null && state.Phase == GamePhase.GameOver && !_controller.IsBusy));
            SetLayer(_endLayer, _endWindow, ended, revision);
            SetLayer(_logLayer, _logWindow, _logLayer.activeSelf, revision);
            SetLayer(_propertiesLayer, _propertiesWindow, _propertiesLayer.activeSelf && CanOpenProperties(state), revision);
            bool windowOpen = tradeOpen || _pauseLayer.activeSelf || _settingsLayer.activeSelf || _howToLayer.activeSelf || ended || _logLayer.activeSelf ||
                              _propertiesLayer.activeSelf || _squareLayer.activeSelf;
            UpdateRollPrompt(state, windowOpen);
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
            if (!_playerCards.TryGetValue(playerId, out PlayerCardView view) || view.Background == null) return;
            Image card = view.Background;
            _moneyFlash.TryGetValue(playerId, out float flash);
            flash = direction != 0 ? 1f : Mathf.MoveTowards(flash, 0f, Time.unscaledDeltaTime * 2f);
            _moneyFlash[playerId] = flash;
            if (direction != 0) _moneyFlashSign[playerId] = direction; // Which way it went, kept for the fade.
            _moneyFlashSign.TryGetValue(playerId, out int sign);
            Color tint = sign < 0 ? UiTheme.Warning : UiTheme.Gain;
            card.color = Color.Lerp(Color.white, tint, flash * 0.35f); // The kit's skin rests untinted.
        }

        private static void ShowMoney(Text label, RollingNumber money)
        {
            label.text = Lang.N(money.Shown);
            label.color = money.Direction > 0 ? UiTheme.Gain : money.Direction < 0 ? UiTheme.Warning : UiTheme.Text; // At rest in the kit's ink.
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

        /// <summary>
        /// The square under the mouse (not over the interface) lights up its colour group; a left click
        /// opens its card. While the board is a choice (<see cref="BoardChoices"/>) only the ringed squares
        /// answer: a click picks a ride's destination, or sells the clicked town for a debt.
        /// </summary>
        private void UpdatePointer(GameState state)
        {
            BoardView board = _controller.Board;
            Camera camera = Camera.main;
            if (board == null || camera == null) return;

            bool overInterface = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            _hoveredSquare = !overInterface && board.TryGetSquare(camera.ScreenPointToRay(Input.mousePosition), out int square) ? square : NoSquare;
            if (!overInterface && Input.GetMouseButtonDown(0)) ClickSquare(state, _hoveredSquare);
            // While a ride's destination is being chosen the view leans a little towards the chosen square.
            bool choosing = ChoosingRide(state);
            _controller.CameraRig?.Peek(choosing && _pinnedSquare != NoSquare ? board.SquarePosition(_pinnedSquare) : (Vector3?)null);
        }

        /// <summary>What a click on a square (or on the empty table, <see cref="NoSquare"/>) does now.</summary>
        public void ClickSquare(GameState state, int square)
        {
            if (SellingOnBoard(state))
            {
                // Each click on one of their towns ticks it for the sale (or unticks it); the rest of the board does nothing.
                if (square != NoSquare) _debtSale?.Toggle(square);
                return;
            }

            if (ChoosingRide(state))
            {
                if (square == NoSquare || BoardChoices(state).Contains(square)) _pinnedSquare = square;
                return;
            }

            _pinnedSquare = square == _pinnedSquare ? NoSquare : square;
        }

        /// <summary>True while the person here picks where to ride: a click then chooses the square instead of opening its card.</summary>
        private bool ChoosingRide(GameState state) => WaitsOnPersonHere(state, GamePhase.AwaitingTravelDecision);

        /// <summary>True while the person here covers a debt: their towns are ringed on the board and a click sells one.</summary>
        public bool SellingOnBoard(GameState state) =>
            WaitsOnPersonHere(state, GamePhase.AwaitingDebtPayment) && state.Debt != null && state.Debt.PlayerId == state.CurrentPlayer.Id;

        private readonly List<int> _choices = new List<int>();
        private int _choicesKey;

        /// <summary>
        /// The squares the person here may click on the board now: where a ride may go
        /// (<see cref="GameRules.IsRideDestination"/>), or their own towns while a debt is sold. Null when
        /// the board is not a choice. The list is reused; read it, don't keep it.
        /// </summary>
        public List<int> BoardChoices(GameState state)
        {
            bool ride = ChoosingRide(state);
            bool sale = !ride && SellingOnBoard(state);
            if (!ride && !sale) return null;

            _choices.Clear();
            BoardDefinition board = _controller.Session.Board;
            PlayerState player = state.CurrentPlayer;
            for (int i = 0; i < board.Count; i++)
            {
                bool choice = ride
                    ? GameRules.IsRideDestination(board, state, player, i)
                    : board[i].Type == SquareType.Property && state.GetOwner(i) == player.Id;
                if (choice) _choices.Add(i);
            }

            return _choices;
        }

        /// <summary>
        /// Shows the board's choice, redrawn only when it changes: for a ride the reachable squares light
        /// up and the rest of the board goes dark; for a debt the person's towns light up and are ringed.
        /// </summary>
        private void UpdateBoardChoices(GameState state)
        {
            List<int> choices = state == null ? null : BoardChoices(state);
            int key = 0;
            if (choices != null)
            {
                key = HashCode.Combine(state.Phase, choices.Count) | 1;
                foreach (int square in choices) key = HashCode.Combine(key, square);
            }

            if (key == _choicesKey) return;
            _choicesKey = key;
            bool ride = choices != null && ChoosingRide(state);
            _controller.Markers?.ShowChoices(ride ? null : choices);
            // The group light under the mouse is drawn again from scratch once the choice is over.
            _highlightedFor = ChoiceHighlight;
            _controller.Board?.Highlight(choices, dimOthers: ride);
        }

        /// <summary>Stands in <see cref="_highlightedFor"/> while the board lights a choice rather than a group.</summary>
        private const int ChoiceHighlight = -2;

        private void UpdateSquareCard(GameState state, int revision)
        {
            bool choosing = state != null && BoardChoices(state) != null;
            bool open = state != null && _pinnedSquare != NoSquare && !choosing;
            SetLayer(_squareLayer, _squareWindow, open, HashCode.Combine(revision, _pinnedSquare));
            UpdateBoardChoices(state);
            if (choosing) return;

            int shown = state == null ? NoSquare : _pinnedSquare != NoSquare ? _pinnedSquare : _hoveredSquare;
            if (shown == _highlightedFor || _controller.Board == null) return;
            _highlightedFor = shown;
            BoardDefinition board = _controller.Session.Board;
            if (shown == NoSquare || board[shown].Type != SquareType.Property)
            {
                _controller.Board.Highlight(null);
                return;
            }

            _controller.Board.Highlight(new List<int>(GameRules.GroupSquares(board, board[shown].GroupId)));
        }

        private void RenderSquareCard(UiBuilder ui) =>
            _squareCard.Render(ui, _pinnedSquare, () => _pinnedSquare = NoSquare, square => _pinnedSquare = square);

        /// <summary>
        /// The banner: a player who could not pay even by selling everything (the board shows their
        /// bankruptcy meanwhile) takes it first, for everyone at the table; then a short "your turn"
        /// whenever a new turn goes to someone at this screen.
        /// </summary>
        private void UpdateBanner(GameState state)
        {
            if (_controller.AnnouncementTime > _announcedAt)
            {
                _announcedAt = _controller.AnnouncementTime;
                ShowBanner(_controller.Announcement, _controller.ColorOf(_controller.AnnouncementPlayer), GameSceneController.AnnouncementSeconds);
            }

            bool announcing = Time.unscaledTime < _announcedAt + GameSceneController.AnnouncementSeconds;
            if (state != null && !announcing && !_controller.IsBusy && state.Phase != GamePhase.GameOver &&
                (state.CurrentPlayer.Id != _lastTurnPlayer || state.TurnNumber != _lastTurnNumber))
            {
                _lastTurnPlayer = state.CurrentPlayer.Id;
                _lastTurnNumber = state.TurnNumber;
                PlayerState player = state.CurrentPlayer;
                if (_controller.Session.CanControl(player.Id))
                {
                    AudioPlayer.Play(SoundCue.YourTurn);
                    ShowBanner(F($"Sıra sende, {player.Name}"), _controller.ColorOf(player.Id), BannerSeconds);
                }
            }

            float left = _bannerUntil - Time.unscaledTime;
            _bannerGroup.alpha = Mathf.Clamp01(left / 0.4f);
        }

        /// <summary>The band takes the player's colour and the words whichever ink reads on it.</summary>
        private void ShowBanner(string text, Color color, float seconds)
        {
            _banner.text = text;
            _bannerFill.color = color;
            _banner.color = ColorVision.TextOn(color);
            _bannerUntil = Time.unscaledTime + seconds;
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

        /// <summary>The panel at the bottom: what the game waits on from the person here, or why it stopped. Empty (and hidden) at a free moment.</summary>
        private void RenderTurn(UiBuilder ui)
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
            RenderActions(ui, state);
        }

        /// <summary>
        /// The strip at the very bottom: the round and the newest event (a click opens the whole log), and
        /// at its end the small buttons for the properties, a trade offer and the menu (help, settings and
        /// the way out are there).
        /// </summary>
        private void RenderTicker(UiBuilder ui)
        {
            GameState state = _controller.Session.State;
            UiBuilder strip = ui.Tile(ToggleLog, UiTheme.Panel, 8);
            UiBuilder row = strip.Row(8f, expand: false);
            if (state != null)
            {
                int limit = _controller.Session.Rules.RoundLimit;
                Text round = row.Label(limit > 0 ? F($"Tur {state.Round} / {limit}") : F($"Tur {state.Round}"), UiTextStyle.Small, UiTheme.Muted);
                round.font = UiTheme.BoldFont;
                UiBuilder.Fix(round, width: 110f);
            }

            IReadOnlyList<LogEntry> entries = _controller.Log;
            if (entries.Count == 0)
            {
                UiBuilder.Fix(row.Label(L("Oyun başlıyor..."), UiTextStyle.Small, UiTheme.Muted), -1f).flexibleWidth = 1f;
            }
            else
            {
                LogEntry last = entries[entries.Count - 1];
                Color dot = last.PlayerId == GameState.NoOwner ? Color.clear : _controller.ColorOf(last.PlayerId);
                row.Swatch(dot, 10f, round: true).raycastTarget = false;
                Color color = last.Tone == LogTone.Gain ? UiTheme.Gain : last.Tone == LogTone.Loss ? UiTheme.Warning : UiTheme.Text;
                // One line only: a long event shrinks a little and is then cut, so the strip never grows taller.
                Text line = row.Label(last.Text, UiTextStyle.Small, color);
                line.verticalOverflow = VerticalWrapMode.Truncate;
                UiBuilder.ShrinkToFit(line);
                UiBuilder.Fix(line, -1f, TickerLineHeight).flexibleWidth = 1f;
            }

            RenderToolbar(row, state);
        }

        /// <summary>Height of the newest event's single line in the bottom strip.</summary>
        private const float TickerLineHeight = 24f;

        /// <summary>Width of each small button at the end of the bottom strip.</summary>
        public const float ToolbarButtonWidth = 120f;

        /// <summary>
        /// The few buttons the game screen keeps (the side dock is gone): the properties and a trade offer
        /// while the turn is this person's to use, and the menu. Rolling has its own big button and Space;
        /// the building card opens by itself and comes back with B.
        /// </summary>
        private void RenderToolbar(UiBuilder row, GameState state)
        {
            bool over = state == null || state.Phase == GamePhase.GameOver || _controller.EndReason.HasValue;
            bool free = !over && WaitsOnPersonHere(state, state.Phase) && OffersExtras(state);
            int id = over ? GameState.NoOwner : state.CurrentPlayer.Id;
            UiBuilder.Fix(row.IconButton(UiIcon.Flag, L("Mülkler"), OpenProperties, !over && CanOpenProperties(state), height: UiTheme.SmallButtonHeight),
                ToolbarButtonWidth);
            UiBuilder.Fix(row.IconButton(UiIcon.People, L("Takas"), () => _trade.Open(id), free, height: UiTheme.SmallButtonHeight), ToolbarButtonWidth);
            UiBuilder.Fix(row.IconButton(UiIcon.Pause, L("Menü"), OpenPause, height: UiTheme.SmallButtonHeight), ToolbarButtonWidth);
        }

        /// <summary>The properties window (build and sell) is for the person on turn here, at any moment that is theirs to act in.</summary>
        private bool CanOpenProperties(GameState state)
        {
            if (state == null || state.Phase == GamePhase.GameOver || _controller.IsBusy || _controller.EndReason.HasValue) return false;
            if (state.Phase == GamePhase.AwaitingTradeResponse || state.Phase == GamePhase.AwaitingBuyDecision) return false;
            int id = state.CurrentPlayer.Id;
            return _controller.Session.CanControl(id) && !_controller.Session.IsAiPlaying(id);
        }

        private void OpenProperties()
        {
            _propertiesLayer.SetActive(true);
            _propertiesWindow.Invalidate();
        }

        private void ToggleLog()
        {
            _logLayer.SetActive(!_logLayer.activeSelf);
            _logWindow.Invalidate();
        }

        /// <summary>The players of one corner: seats 1-4 take a corner each, seats 5-8 stack on them; the bottom corners stack upwards from the edge.</summary>
        private void RenderCorner(UiBuilder ui, int corner)
        {
            GameState state = _controller.Session.State;
            if (state == null) return;
            var here = new List<PlayerState>();
            foreach (PlayerState player in state.Players)
            {
                if (state.IndexOfPlayer(player.Id) % 4 == corner) here.Add(player);
            }

            if (corner >= 2) here.Reverse();
            foreach (PlayerState player in here) RenderPlayerCard(ui, state, player);
        }

        /// <summary>A player's card in the player card kit's look (<see cref="PlayerCardView"/>): their face, seat number, name, money and towns.</summary>
        private void RenderPlayerCard(UiBuilder ui, GameState state, PlayerState player)
        {
            bool current = player.Id == state.CurrentPlayer.Id && state.Phase != GamePhase.GameOver;
            int seat = FirstPlayerDraw.LobbySeat(state.Players, player.Id);
            var model = new PlayerCardModel
            {
                Name = player.Name,
                Number = PlayerCardView.NumberOf(seat),
                Color = _controller.ColorOf(player.Id),
                State = player.IsBankrupt ? PlayerCardState.Bankrupt : current ? PlayerCardState.Active : PlayerCardState.Normal,
                Face = _controller.Faces?.For(player.Id),
                Initial = Initial(player.Name),
                Pips = player.IsBankrupt ? Array.Empty<PlayerCardPip>() : PlayerCardView.GroupsOf(state, player.Id, _controller.GroupOf, _controller.GroupColorOf),
            };
            DescribeSeat(model, player);
            PlayerCardView view = PlayerCardView.Create(ui.Container, CornerWidth, model, PinSquare);
            _playerCards[player.Id] = view;
            if (view.Balance != null) TrackMoney(player.Id, view.Balance);
            else _moneyLabels.Remove(player.Id);
        }

        /// <summary>The letter on a player's avatar: the first of their name, capitalised the Turkish way for a dotted i.</summary>
        public static string Initial(string name)
        {
            string trimmed = name?.Trim();
            if (string.IsNullOrEmpty(trimmed)) return "?";
            string first = trimmed.Substring(0, 1);
            return first == "i" ? "İ" : first.ToUpperInvariant();
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

        /// <summary>
        /// Who plays the seat, on its card. On turn the pill says it: "SIRA SENDE" to the person here, otherwise
        /// whether a computer thinks or a person elsewhere plays. Resting cards name a computer, and online show
        /// a connection dot with the latency the host measures (or that the link is lost).
        /// </summary>
        private void DescribeSeat(PlayerCardModel model, PlayerState player)
        {
            DescribeConnection(model, player);
            if (!player.InJail) return;
            // A jailed player's resting card says so, with the turns still to wait, before who plays the seat.
            string jail = F($"Hapiste ({player.JailTurns})");
            model.Status = string.IsNullOrEmpty(model.Status) ? jail : $"{jail} · {model.Status}";
        }

        private void DescribeConnection(PlayerCardModel model, PlayerState player)
        {
            IGameSession session = _controller.Session;
            bool ai = session.IsAiPlaying(player.Id);
            bool connected = session.IsConnected(player.Id);
            model.Pill = Lang.Upper(session.CanControl(player.Id) && !ai ? L("Sıra sende") : ai ? L("düşünüyor") : L("oynuyor"));
            if (!_controller.IsOnline)
            {
                model.Status = ai ? L("Bilgisayar") : null;
                return;
            }

            model.StatusDot = connected ? UiTheme.Gain : UiTheme.Warning;
            int ping = session.PingMs(player.Id);
            model.Status = !connected ? L("Bağlantı yok")
                : ai ? L("Bilgisayar oynuyor")
                : ping > 0 ? F($"{ping} ms") : ping == 0 ? L("bu bilgisayar") : L("gecikme ölçülüyor");
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
            // The end has its own window (the winner, standings, main menu); the bottom stays empty (the owner's wish, Faz 23).
            if (state.Phase == GamePhase.GameOver || _controller.IsBusy) return;

            // A trade answer comes from a player other than the one on turn. Whoever here sent or received the offer
            // answers it on the card in the middle (OffersAnswerHere); anyone else only reads it here.
            if (state.Phase == GamePhase.AwaitingTradeResponse)
            {
                if (!OffersAnswerHere(state)) _trade.RenderResponse(ui, state);
            }
            else
            {
                RenderTurnActions(ui, state);
            }

            if (_controller.LastRejection != null) ui.Notice(_controller.LastRejection);
        }

        /// <summary>The decisions that are not a card of their own; rolling has the big button (the turn ends by itself), buying is on the city card.</summary>
        private void RenderTurnActions(UiBuilder ui, GameState state)
        {
            PlayerState player = state.CurrentPlayer;
            if (!_controller.Session.CanControl(player.Id)) return;

            switch (state.Phase)
            {
                // A debt has its own card in the middle of the screen (DebtSaleView).
                case GamePhase.AwaitingTravelDecision:
                    RenderTravel(ui, player);
                    break;

                case GamePhase.AwaitingDiceChoice:
                    RenderDiceChoice(ui, state, player);
                    break;
            }
        }

        /// <summary>
        /// The turn ends by itself once the person here has nothing left to decide on it: the board has
        /// played the move and no building card, trade or property window is open. A short pause lets the
        /// landing be seen first. With a card open, the decision clock ends the turn if nobody does.
        /// </summary>
        private void AutoEndTurn(GameState state, bool tradeOpen)
        {
            bool due = WaitsOnPersonHere(state, GamePhase.TurnEnd) && !_controller.Paused && !tradeOpen && !OffersBuildCard(state) &&
                       !_propertiesLayer.activeSelf && !_pauseLayer.activeSelf;
            if (!due)
            {
                _turnEndSince = -1f;
                return;
            }

            if (_turnEndSince < 0f) _turnEndSince = Time.unscaledTime;
            if (Time.unscaledTime - _turnEndSince < AutoEndDelaySeconds) return;
            // Once per turn: online the state stays at the turn's end until the host answers.
            int key = HashCode.Combine(state.TurnNumber, state.CurrentPlayer.Id);
            if (key == _autoEndedKey) return;
            _autoEndedKey = key;
            Submit(new EndTurnCommand(state.CurrentPlayer.Id));
        }

        /// <summary>The building card and trading are offered while the turn is at a free moment.</summary>
        private static bool OffersExtras(GameState state) => state.Phase == GamePhase.WaitingForRoll || state.Phase == GamePhase.TurnEnd;

        /// <summary>The building card was put away for this visit but could still be used.</summary>
        private bool CanReopenBuildCard(GameState state) =>
            AffordableBuildings(state, state.CurrentPlayer) > 0 && _buildCardDismissed == BuildVisitKey(state);

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
            _propertyCard.Reset();
        }

        /// <summary>
        /// The city's card while it is for sale: the land and the houses the first visit allows,
        /// "buy" with the whole price, or "move on". Buying puts the building card away for this visit.
        /// </summary>
        private void RenderCityCard(UiBuilder ui)
        {
            GameState state = _controller.Session.State;
            int playerId = state.CurrentPlayer.Id;
            _propertyCard.Render(ui, buying: true, dismiss: () => _controller.Submit(new DeclinePurchaseCommand(playerId)),
                done: () => _buildCardDismissed = BuildVisitKey(state));
        }

        /// <summary>
        /// The same card on an owned city: the levels above the one it stands at, what they cost
        /// and earn, "build" or "not now". Building sends one command per house; the host accepts each in turn.
        /// </summary>
        private void RenderBuildCard(UiBuilder ui)
        {
            GameState state = _controller.Session.State;
            _propertyCard.Render(ui, buying: false, dismiss: () => _buildCardDismissed = BuildVisitKey(state),
                done: () => _buildCardDismissed = BuildVisitKey(state));
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
            card.Label(L("Tahtada parlayan karelerden gitmek istediğine tıkla; piyon oraya yürür."), UiTextStyle.Small, UiTheme.Muted);

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

        /// <summary>
        /// A turn in jail, on a card in the middle of the screen: roll for a double, which lets the player out for free
        /// (the first button), or pay the exit fee and roll now. Any other roll waits the turn out; after the last one the
        /// player is free. When the clock runs out the dice are rolled.
        /// </summary>
        private void RenderJailCard(UiBuilder ui)
        {
            GameState state = _controller.Session.State;
            if (state == null || state.Phase != GamePhase.AwaitingJailDecision) return;
            PlayerState player = state.CurrentPlayer;
            BoardDefinition board = _controller.Session.Board;
            int fee = GameRules.JailFee(board);
            ui.Title(UiIcon.Door, Lang.Upper(L(board[board.JailIndex].Name)));
            ui.Label(F($"{player.JailTurns} tur daha beklemen gerekiyor."), UiTextStyle.Body);
            ui.Label(player.JailTurns > 1
                ? F($"{fee} ödersen hemen çıkıp zar atarsın. Zar atarsan çift gelince bedava çıkıp ilerlersin; gelmezse sıra geçer.")
                : F($"{fee} ödersen hemen çıkıp zar atarsın. Zar atarsan çift gelince bedava çıkıp ilerlersin; gelmezse bu son bekleyişin, sonraki turda serbestsin."),
                UiTextStyle.Small, UiTheme.Muted);

            // Rolling comes first: it is free (the owner's wish); paying is the second choice.
            ui.Button(L("Zar at (çift gelirse çık)"), () => _controller.Submit(new RollInJailCommand(player.Id)), true,
                UiButtonStyle.Primary, CardButton);
            var pay = new PayJailFeeCommand(player.Id);
            bool canPay = _controller.IsAllowed(pay);
            ui.Button(canPay ? F($"Öde ve çık ({fee})") : F($"Paran yetmiyor ({fee})"), () => _controller.Submit(pay), canPay,
                UiButtonStyle.Secondary, CardButton);
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

        /// <summary>
        /// The event log, newest at the top so nothing needs scrolling to see what just happened. Each
        /// turn's lines sit under a heading in the player's colour; a dot in the actor's colour marks
        /// whose line each one is.
        /// </summary>
        private void RenderLogWindow(UiBuilder ui)
        {
            ui.Title(UiIcon.List, L("Olaylar"));
            RenderLog(ui.Scroll(WindowListHeight));
            ui.Button(L("Kapat (Esc)"), () => _logLayer.SetActive(false));
        }

        /// <summary>The properties window: the person on turn's list with its build and sell buttons.</summary>
        private void RenderProperties(UiBuilder ui)
        {
            GameState state = _controller.Session.State;
            if (state == null) return;
            PropertyPanel.Render(ui.Scroll(WindowListHeight), _controller, state, state.CurrentPlayer.Id);
            ui.Button(L("Kapat (Esc)"), () => _propertiesLayer.SetActive(false));
        }

        private void RenderLog(UiBuilder ui)
        {
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

                UiBuilder.Fix(row.Label(Lang.N(player.Money), color: color, alignment: TextAnchor.MiddleRight), width: 80f);
                UiBuilder.Fix(row.Label(OwnedCount(state, player.Id).ToString(), color: color, alignment: TextAnchor.MiddleRight), width: 60f);
                UiBuilder.Fix(row.Label(Lang.N(GameRules.NetWorth(board, state, player.Id)), color: UiTheme.Money, alignment: TextAnchor.MiddleRight), width: 90f);
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
            if (online && !over) ui.Label(L("Çevrim içi oyun arkada sürer; karar süresi işlemeye devam eder."), UiTextStyle.Small, UiTheme.Muted);

            ui.Button(L("Devam Et (Esc)"), ClosePause, style: UiButtonStyle.Primary, height: CardButton);
            UiBuilder row = ui.Row();
            row.IconButton(UiIcon.Sliders, L("Ayarlar"), OpenSettings);
            row.IconButton(UiIcon.Book, L("Nasıl Oynanır (H)"), OpenHowToPlay);
            ui.IconButton(UiIcon.Home, L("Görünümü Sıfırla (Home)"), () => _controller.CameraRig?.ResetView(), height: UiTheme.SmallButtonHeight);

            UiBuilder keys = ui.Card(UiTheme.Field, 10);
            keys.Label(L("Klavye"), UiTextStyle.Small, UiTheme.Muted);
            Line(keys, L("Boşluk / Enter"), L("zar at"));
            Line(keys, "B", L("bina kartı"));
            Line(keys, "T", L("takas teklif et"));
            Line(keys, "H", L("nasıl oynanır"));
            Line(keys, "Home", L("görünümü sıfırla"));
            Line(keys, "F3 / F11 / F12", L("kare hızı, ekran görüntüsü açısı, ekran görüntüsü"));
            Line(keys, "Esc", L("menü, pencereyi kapat"));

            ui.Space(4f);
            ui.Button(L("Sorun Bildir"), _controller.ReportProblem, height: UiTheme.SmallButtonHeight);
            ui.Label(_controller.ReportNote ?? L("Oyunun durumunu ve son hamleleri hata günlüğüne yazar, günlük klasörünü açar; dosyayı bildirimine ekle."),
                UiTextStyle.Small, UiTheme.Muted);
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
                ButtonFocused = UiFocus.ButtonFocused,
                PauseOpen = _pauseLayer.activeSelf,
                ClosableOpen = _squareLayer.activeSelf || _settingsLayer.activeSelf || _howToLayer.activeSelf || _propertiesLayer.activeSelf || _logLayer.activeSelf ||
                               _tradeLayer.activeSelf || _endLayer.activeSelf || _buildLayer.activeSelf,
                BlockingOpen = _cityLayer.activeSelf || _jailLayer.activeSelf || _offerLayer.activeSelf,
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

            _ticker.Invalidate();
            _turn.Invalidate();
        }

        /// <summary>A shortcut sends only what its button would: the same command, only when the rules allow it now.</summary>
        private void Submit(GameCommand command)
        {
            if (_controller.IsAllowed(command)) _controller.Submit(command);
        }

        /// <summary>Esc closes whatever is on top, in the order the windows stack.</summary>
        private void CloseTopWindow(GameState state)
        {
            if (_squareLayer.activeSelf) _pinnedSquare = NoSquare;
            else if (_settingsLayer.activeSelf) _settingsLayer.SetActive(false);
            else if (_howToLayer.activeSelf) _howToLayer.SetActive(false);
            else if (_pauseLayer.activeSelf) ClosePause();
            else if (_propertiesLayer.activeSelf) _propertiesLayer.SetActive(false);
            else if (_logLayer.activeSelf) _logLayer.SetActive(false);
            else if (_tradeLayer.activeSelf) _trade.Close();
            else if (_buildLayer.activeSelf && state != null) _buildCardDismissed = BuildVisitKey(state);
            else if (_endLayer.activeSelf) _endDismissed = true;
        }

        /// <summary>True while a trade offer waits and a person here sent it or is asked: its card then opens in the middle.</summary>
        private bool OffersAnswerHere(GameState state)
        {
            if (state == null || state.Phase != GamePhase.AwaitingTradeResponse || state.PendingTrade == null) return false;
            if (_controller.IsBusy || _controller.EndReason.HasValue) return false;
            IGameSession session = _controller.Session;
            bool PersonHere(int id) => session.CanControl(id) && !session.IsAiPlaying(id);
            return PersonHere(state.PendingTrade.ToPlayerId) || PersonHere(state.PendingTrade.FromPlayerId);
        }

        private void RenderOffer(UiBuilder ui)
        {
            GameState state = _controller.Session.State;
            if (!OffersAnswerHere(state)) return;
            _trade.RenderResponse(ui, state);
            if (_controller.LastRejection != null) ui.Notice(_controller.LastRejection);
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
