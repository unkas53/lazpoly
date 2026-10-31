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

namespace Lazpoly.Presentation.Game
{
    /// <summary>
    /// The game screen's interface: players and actions on the left, the event log and menu buttons
    /// on the right, and windows for trading, settings and the end of the game. It offers only
    /// actions <see cref="GameSceneController.IsAllowed"/> approves; the host still validates them.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        private const float SideWidth = 380f;
        private const float Margin = 16f;

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
        private bool _endDismissed;
        private UiView _squareCard;
        private GameObject _squareCardPanel;
        private int _hoveredSquare = NoSquare;
        private int _pinnedSquare = NoSquare;
        private int _highlightedFor = NoSquare;
        private Text _banner;
        private CanvasGroup _bannerGroup;
        private float _bannerUntil;
        private int _lastTurnPlayer = -1;
        private int _lastTurnNumber = -1;
        private Text _cardTitle;
        private Text _cardText;
        private CanvasGroup _cardGroup;
        private readonly Dictionary<int, RollingNumber> _money = new Dictionary<int, RollingNumber>();
        private readonly Dictionary<int, Text> _moneyLabels = new Dictionary<int, Text>();
        private TipMemory _tipMemory;
        private UiView _tipView;
        private GameObject _tipPanel;
        private Tip? _tip;
        private UiView _howToWindow;
        private Text _timer;
        private GameObject _howToLayer;

        private const int NoSquare = -1;
        private const float BannerSeconds = 1.8f;
        private const float CardWidth = 440f;

        /// <summary>Screen pixels each side column covers, margins included; the camera keeps the board clear of them.</summary>
        public float ReservedSidePixels => (SideWidth + 2f * Margin) * (_canvas != null ? _canvas.scaleFactor : 1f);

        /// <summary>The content of the left column, for tests.</summary>
        public RectTransform SideContent { get; private set; }

        /// <summary>The square whose card is showing (pinned by a click, else under the pointer), or -1.</summary>
        public int ShownSquare => _pinnedSquare != NoSquare ? _pinnedSquare : _hoveredSquare;

        /// <summary>The first-game hint on screen, or null.</summary>
        public Tip? ShownTip => _tip;

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
            UiRoot.Background(side, UiTheme.Panel);
            UiBuilder sideBuilder = UiRoot.ScrollPanel(side);
            SideContent = sideBuilder.Container;
            _side = new UiView(sideBuilder, RenderSide);

            RectTransform log = UiRoot.Rect(_canvas.transform, "Log", new Vector2(1f, 0f), Vector2.one,
                new Vector2(-Margin - SideWidth, Margin + 182f), new Vector2(-Margin, -Margin));
            UiRoot.Background(log, UiTheme.Panel);
            _log = new UiView(UiRoot.ScrollPanel(log), RenderLog);

            RectTransform menu = UiRoot.Rect(_canvas.transform, "Menu", new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-Margin - SideWidth, Margin), new Vector2(-Margin, Margin + 166f));
            UiRoot.Background(menu, UiTheme.Panel);
            UiBuilder.AddColumnLayout(menu.gameObject, 12, UiTheme.Spacing);
            _menu = new UiView(new UiBuilder(menu), RenderMenu);

            // The square card opens at the top of the free area, on the side away from the square it describes.
            // It never takes the mouse, so the squares under it can still be pointed at and clicked.
            RectTransform card = UiRoot.Rect(_canvas.transform, "Square Card", new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            card.pivot = new Vector2(0f, 1f);
            card.sizeDelta = new Vector2(CardWidth, 0f);
            UiRoot.Background(card, UiTheme.Panel);
            var cardGroup = card.gameObject.AddComponent<CanvasGroup>();
            cardGroup.blocksRaycasts = false;
            cardGroup.interactable = false;
            UiBuilder.AddColumnLayout(card.gameObject, 14, 4f);
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _squareCardPanel = card.gameObject;
            _squareCard = new UiView(new UiBuilder(card), RenderSquareCard);
            _squareCardPanel.SetActive(false);

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

            _tradeWindow = new UiView(UiRoot.Modal(_canvas.transform, "Trade", 760f, out _tradeLayer), RenderTrade);
            _settingsWindow = new UiView(UiRoot.Modal(_canvas.transform, "Settings", 520f, out _settingsLayer),
                ui => _settings.Render(ui, () => _settingsLayer.SetActive(false)));
            _endWindow = new UiView(UiRoot.Modal(_canvas.transform, "Game End", 560f, out _endLayer), RenderEnd);
            _howToWindow = new UiView(UiRoot.Modal(_canvas.transform, "How To Play", HowToPlayPanel.Width, out _howToLayer),
                ui => HowToPlayPanel.Render(ui, _controller.Session.Board, _controller.Session.Rules, () => _howToLayer.SetActive(false)));
            _howToLayer.SetActive(false);
            _tradeLayer.SetActive(false);
            _settingsLayer.SetActive(false);
            _endLayer.SetActive(false);
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
            UiBuilder.AddColumnLayout(card.gameObject, 18, 8f);
            var builder = new UiBuilder(card);
            _cardTitle = builder.Label("SOKAK HABERİ", UiTextStyle.Heading, new Color(0.55f, 0.28f, 0.08f), TextAnchor.MiddleCenter);
            _cardText = builder.Label("", UiTextStyle.Body, new Color(0.15f, 0.12f, 0.10f), TextAnchor.MiddleCenter);
            _cardText.fontSize = 22;
        }

        /// <summary>A hint box at the bottom centre, between the side columns.</summary>
        private void BuildTipPanel()
        {
            RectTransform tip = UiRoot.Rect(_canvas.transform, "Tip", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
            tip.pivot = new Vector2(0.5f, 0f);
            tip.anchoredPosition = new Vector2(0f, Margin);
            tip.sizeDelta = new Vector2(560f, 0f);
            UiRoot.Background(tip, new Color(0.10f, 0.16f, 0.22f, 0.96f));
            UiBuilder.AddColumnLayout(tip.gameObject, 14, 8f);
            tip.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _tipPanel = tip.gameObject;
            _tipView = new UiView(new UiBuilder(tip), RenderTip);
            _tipPanel.SetActive(false);
        }

        private void RenderTip(UiBuilder ui)
        {
            if (!_tip.HasValue) return;
            ui.Label("İpucu", UiTextStyle.Small, UiTheme.Accent);
            ui.Label(_tip.Value.Text, UiTextStyle.Small);
            UiBuilder row = ui.Row();
            row.Button("Anladım", DismissTip, style: UiButtonStyle.Primary, height: UiTheme.SmallButtonHeight);
            row.Button("İpuçlarını Kapat", () =>
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
            _timer.text = $"Hamle süresi: {left} sn";
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
            _cardTitle.text = player == null ? "SOKAK HABERİ" : $"SOKAK HABERİ · {player}";
            _cardText.text = EventDescriber.CardText(_controller.Session.Board, card);
            _cardGroup.alpha = Mathf.Clamp01(age / 0.15f) * Mathf.Clamp01((visible - age) / 0.4f);
        }

        private void LateUpdate()
        {
            if (_controller?.Session == null || _side == null) return;
            GameState state = _controller.Session.State;
            int revision = _controller.Revision;

            bool tradeOpen = state != null && !_controller.EndReason.HasValue && _trade.KeepOpen(state);
            // The pinned square is the destination of a ride, so the side panel follows it too.
            _side.Refresh(HashCode.Combine(revision, _controller.IsBusy, _controller.EndReason, tradeOpen, _pinnedSquare));
            _log.Refresh(revision);
            _menu.Refresh(0);

            UpdateMoney();
            UpdatePointer();
            UpdateSquareCard(state, revision);
            UpdateBanner(state);
            UpdateCardPopup();
            UpdateTip(state);
            UpdateTimer(state);

            SetLayer(_tradeLayer, _tradeWindow, tradeOpen, revision);
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
            }
        }

        private static void ShowMoney(Text label, RollingNumber money)
        {
            label.text = money.Shown.ToString();
            label.color = money.Direction > 0 ? UiTheme.Gain : money.Direction < 0 ? UiTheme.Warning : UiTheme.Accent;
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
            ui.Label(_pinnedSquare == square ? "Kapatmak için kareye yeniden tıkla." : "Sabitlemek için kareye tıkla.", UiTextStyle.Small, UiTheme.Muted);
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
                    _banner.text = $"Sıra sende, {player.Name}";
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
                ui.Label("Oyun durdu", UiTextStyle.Heading);
                ui.Label(GameSceneController.EndReasonText(_controller.EndReason.Value));
                ui.Button("Ana Menü", _controller.LeaveToMainMenu, style: UiButtonStyle.Primary);
                return;
            }

            if (state == null)
            {
                ui.Label("Ev sahibine bağlanılıyor...", UiTextStyle.Heading);
                ui.Button("Vazgeç", _controller.LeaveToMainMenu);
                return;
            }

            // What to do now comes first, so the main button never scrolls away; who is playing comes last.
            RenderHeader(ui, state);
            RenderActions(ui, state);
            RenderOwnProperties(ui, state);
            ui.Space(6f);
            ui.Label("Oyuncular", UiTextStyle.Small, UiTheme.Muted);
            RenderPlayers(ui, state);
        }

        private void RenderHeader(UiBuilder ui, GameState state)
        {
            UiBuilder header = ui.Row();
            int limit = _controller.Session.Rules.RoundLimit;
            header.Label(limit > 0 ? $"Tur {state.Round} / {limit}" : $"Tur {state.Round}", UiTextStyle.Small, UiTheme.Muted);
            if (state.LastDie1 != 0) header.Label($"Son zar: {state.LastDie1} + {state.LastDie2}", UiTextStyle.Small, UiTheme.Accent, TextAnchor.MiddleRight);
        }

        /// <summary>The property list of the person on turn here, with its build, sell and mortgage buttons.</summary>
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

            foreach (PlayerState player in state.Players)
            {
                bool current = player.Id == state.CurrentPlayer.Id && state.Phase != GamePhase.GameOver;
                UiBuilder card = ui.Card(current ? new Color(0.24f, 0.25f, 0.31f) : UiTheme.Card, 10);
                UiBuilder row = card.Row(10f);
                Color color = player.IsBankrupt ? UiTheme.Muted : _controller.ColorOf(player.Id);
                row.Swatch(color, 20f);
                Text name = row.Label(player.Name, color: player.IsBankrupt ? UiTheme.Muted : UiTheme.Text);
                name.font = current ? UiTheme.BoldFont : UiTheme.Font;
                Text money = row.Label(player.IsBankrupt ? "iflas" : string.Empty, color: UiTheme.Accent, alignment: TextAnchor.MiddleRight);
                UiBuilder.Fix(money, width: 90f);
                if (!player.IsBankrupt) TrackMoney(player.Id, money);

                string tag = PlayerTag(player);
                if (tag != null) card.Label(tag, UiTextStyle.Small);
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

        /// <summary>One small square per property the player owns, in board order (so groups sit together); dim = mortgaged.</summary>
        private void RenderOwnedSquares(UiBuilder card, GameState state, int playerId)
        {
            UiBuilder row = null;
            for (int square = 0; square < state.SquareCount; square++)
            {
                if (state.GetOwner(square) != playerId) continue;
                row ??= card.Row(3f, expand: false);
                Color color = _controller.GroupColorOf(square);
                if (state.IsMortgaged(square)) color = Color.Lerp(color, Color.black, 0.65f);
                row.Swatch(color, 13f);
            }
        }

        private string PlayerTag(PlayerState player)
        {
            bool ai = _controller.Session.IsAiPlaying(player.Id);
            if (!_controller.IsConnected(player.Id)) return ai ? "Bağlantı yok, bilgisayar oynuyor" : "Bağlantı yok";
            return ai ? "Bilgisayar" : null;
        }

        private void RenderActions(UiBuilder ui, GameState state)
        {
            if (state.Phase == GamePhase.GameOver)
            {
                ui.Label($"Kazanan: {state.FindPlayer(state.WinnerId)?.Name}", UiTextStyle.Heading, UiTheme.Accent);
                ui.Button("Ana Menü", _controller.LeaveToMainMenu, style: UiButtonStyle.Primary);
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

            if (_controller.LastRejection != null) ui.Label(_controller.LastRejection, UiTextStyle.Small, UiTheme.Warning);
        }

        private void RenderTurnActions(UiBuilder ui, GameState state)
        {
            PlayerState player = state.CurrentPlayer;
            UiBuilder turn = ui.Row(10f, expand: false);
            turn.Swatch(_controller.ColorOf(player.Id), 20f);
            UiBuilder.Fix(turn.Label(_controller.Session.CanControl(player.Id) ? $"Sıra sende, {player.Name}" : $"{player.Name} oynuyor", UiTextStyle.Heading), -1f).flexibleWidth = 1f;

            if (!_controller.Session.CanControl(player.Id))
            {
                ui.Label(_controller.Session.IsAiPlaying(player.Id) ? "Bilgisayar düşünüyor..." : "Sıra onda, hamlesi bekleniyor.", UiTextStyle.Small);
                return;
            }

            switch (state.Phase)
            {
                case GamePhase.WaitingForRoll:
                    ui.Button("Zar At", () => _controller.Submit(new RollDiceCommand(player.Id)), style: UiButtonStyle.Primary, height: 52f);
                    break;

                case GamePhase.AwaitingBuyDecision:
                    RenderBuyDecision(ui, player);
                    break;

                case GamePhase.TurnEnd:
                    ui.Button("Sırayı Bitir", () => _controller.Submit(new EndTurnCommand(player.Id)), style: UiButtonStyle.Primary, height: 52f);
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

            if (state.Phase == GamePhase.WaitingForRoll || state.Phase == GamePhase.TurnEnd)
            {
                ui.Button("Takas Teklif Et", () => _trade.Open(player.Id), height: UiTheme.SmallButtonHeight);
            }
        }

        private void RenderBuyDecision(UiBuilder ui, PlayerState player)
        {
            int squareIndex = player.Position;
            SquareDefinition square = _controller.Session.Board[squareIndex];
            UiBuilder card = ui.Card();
            UiBuilder title = card.Row();
            UiBuilder.Fix(title.Swatch(_controller.GroupColorOf(squareIndex), 16f), 16f, 16f);
            title.Label(square.Name, UiTextStyle.Heading);
            card.Label($"Fiyat {square.Price}, kira {square.BaseRent}");

            var buy = new BuyPropertyCommand(player.Id);
            ui.Button($"Satın Al ({square.Price})", () => _controller.Submit(buy), _controller.IsAllowed(buy), UiButtonStyle.Primary, 52f);
            if (player.Money < square.Price) ui.Label("Paran yetmiyor: ipotek ya da bina satışıyla para bulabilirsin.", UiTextStyle.Small, UiTheme.Warning);
            ui.Button(_controller.Session.Rules.AuctionsEnabled ? "Alma, açık artırmaya çıkar" : "Alma",
                () => _controller.Submit(new DeclinePurchaseCommand(player.Id)));
        }

        /// <summary>A ride from the travel square: the player picks the destination by clicking a square on the board.</summary>
        private void RenderTravel(UiBuilder ui, PlayerState player)
        {
            BoardDefinition board = _controller.Session.Board;
            int fee = board[player.Position].Fee;
            UiBuilder card = ui.Card();
            card.Label($"{board[player.Position].Name}: {fee} ödeyip istediğin kareye gidebilirsin.");
            card.Label("Tahtada gitmek istediğin kareye tıkla.", UiTextStyle.Small, UiTheme.Muted);

            int target = _pinnedSquare;
            var travel = new TravelCommand(player.Id, target);
            if (target >= 0 && _controller.IsAllowed(travel))
            {
                ui.Button($"{board[target].Name} karesine git ({fee})", () =>
                {
                    _pinnedSquare = NoSquare;
                    _controller.Submit(travel);
                }, style: UiButtonStyle.Primary, height: 52f);
            }
            else
            {
                ui.Button("Önce bir kare seç", null, false, UiButtonStyle.Primary, 52f);
            }

            ui.Button("Burada kal", () => _controller.Submit(new DeclineTravelCommand(player.Id)));
        }

        /// <summary>The lookout's two rolls, each with where it would take the player.</summary>
        private void RenderDiceChoice(UiBuilder ui, GameState state, PlayerState player)
        {
            BoardDefinition board = _controller.Session.Board;
            ui.Label("Seyir Terası: iki atıştan birini seç.", UiTextStyle.Small);
            for (int option = 0; option < 2; option++)
            {
                int[] dice = GameRules.DiceOption(state, option);
                if (dice == null) continue;
                int sum = dice[0] + dice[1];
                string destination = board[(player.Position + sum) % board.Count].Name;
                var choose = new ChooseDiceCommand(player.Id, option);
                ui.Button($"{dice[0]} + {dice[1]} = {sum}  →  {destination}", () => _controller.Submit(choose), _controller.IsAllowed(choose),
                    UiButtonStyle.Primary, 52f);
            }
        }

        private void RenderDebt(UiBuilder ui, GameState state, PlayerState player)
        {
            DebtState debt = state.Debt;
            string creditor = EventDescriber.CreditorText(state, debt.CreditorId);
            UiBuilder card = ui.Card(new Color(0.30f, 0.16f, 0.16f));
            card.Label($"Borç: {debt.Amount} → {creditor}", UiTextStyle.Heading);
            card.Label(EventDescriber.ReasonText(debt.Reason), UiTextStyle.Small);
            card.Label("Bina sat ya da ipotek ederek para bul, sonra öde.", UiTextStyle.Small);

            var pay = new PayDebtCommand(player.Id);
            ui.Button($"Borcu Öde ({debt.Amount})", () => _controller.Submit(pay), _controller.IsAllowed(pay), UiButtonStyle.Primary, 52f);
            ui.Button("İflas Et", () => _controller.Submit(new DeclareBankruptcyCommand(player.Id)), style: UiButtonStyle.Danger, height: UiTheme.SmallButtonHeight);
        }

        private void RenderLog(UiBuilder ui)
        {
            ui.Label("Olaylar", UiTextStyle.Heading);
            var lines = _controller.Log;
            var tones = _controller.LogTones;
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                Color color = tones[i] == LogTone.Gain ? UiTheme.Gain : tones[i] == LogTone.Loss ? UiTheme.Warning : i == lines.Count - 1 ? UiTheme.Text : UiTheme.Muted;
                ui.Label(lines[i], UiTextStyle.Small, color);
            }
        }

        /// <summary>Each solvent player's worth (money, properties and buildings), richest first.</summary>
        private void RenderWorthTable(UiBuilder ui, GameState state)
        {
            var players = new List<PlayerState>();
            foreach (PlayerState player in state.Players)
            {
                if (!player.IsBankrupt) players.Add(player);
            }

            BoardDefinition board = _controller.Session.Board;
            players.Sort((a, b) => GameRules.NetWorth(board, state, b.Id).CompareTo(GameRules.NetWorth(board, state, a.Id)));
            foreach (PlayerState player in players)
            {
                UiBuilder row = ui.Row();
                row.Swatch(_controller.ColorOf(player.Id), 16f);
                row.Label(player.Name);
                row.Label($"{GameRules.NetWorth(board, state, player.Id)} değer", color: UiTheme.Accent, alignment: TextAnchor.MiddleRight);
            }
        }

        private void RenderMenu(UiBuilder ui)
        {
            UiBuilder row = ui.Row();
            row.Button("Ayarlar", () =>
            {
                _settingsLayer.SetActive(true);
                _settingsWindow.Invalidate();
            });
            row.Button("Nasıl Oynanır", () =>
            {
                _howToLayer.SetActive(true);
                _howToWindow.Invalidate();
            });
            ui.Button("Görünümü Sıfırla (Home)", () => _controller.CameraRig?.ResetView(), height: UiTheme.SmallButtonHeight);
            ui.Button("Oyundan Çık", _controller.LeaveToMainMenu, style: UiButtonStyle.Danger);
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
                ui.Label("Oyun durdu", UiTextStyle.Heading);
                ui.Label(GameSceneController.EndReasonText(_controller.EndReason.Value));
            }
            else
            {
                GameState state = _controller.Session.State;
                PlayerState winner = state.FindPlayer(state.WinnerId);
                ui.Label("Oyun bitti", UiTextStyle.Heading);
                ui.Label($"Kazanan: {winner?.Name}", UiTextStyle.Title, winner != null ? _controller.ColorOf(winner.Id) : UiTheme.Accent, TextAnchor.MiddleCenter);
                bool byLimit = _controller.Session.Rules.RoundLimit > 0 && state.ActivePlayerCount() > 1;
                ui.Label(byLimit ? $"Tur sınırı doldu ({state.Round} tur): en zengin oyuncu kazandı." : $"{state.Round} turda bitti.",
                    UiTextStyle.Small, alignment: TextAnchor.MiddleCenter);
                if (byLimit) RenderWorthTable(ui, state);
            }

            ui.Space(8f);
            UiBuilder row = ui.Row();
            row.Button("Tahtaya Bak", () => _endDismissed = true);
            row.Button("Ana Menü", _controller.LeaveToMainMenu, style: UiButtonStyle.Primary);
        }
    }
}
