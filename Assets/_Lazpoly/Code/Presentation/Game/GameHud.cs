using System;
using Lazpoly.App.Session;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Menu;
using Lazpoly.Presentation.Texts;
using Lazpoly.Presentation.UI;
using UnityEngine;
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

        /// <summary>Screen pixels each side column covers, margins included; the camera keeps the board clear of them.</summary>
        public float ReservedSidePixels => (SideWidth + 2f * Margin) * (_canvas != null ? _canvas.scaleFactor : 1f);

        /// <summary>The content of the left column, for tests.</summary>
        public RectTransform SideContent { get; private set; }

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
                new Vector2(-Margin - SideWidth, Margin + 132f), new Vector2(-Margin, -Margin));
            UiRoot.Background(log, UiTheme.Panel);
            _log = new UiView(UiRoot.ScrollPanel(log), RenderLog);

            RectTransform menu = UiRoot.Rect(_canvas.transform, "Menu", new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-Margin - SideWidth, Margin), new Vector2(-Margin, Margin + 116f));
            UiRoot.Background(menu, UiTheme.Panel);
            UiBuilder.AddColumnLayout(menu.gameObject, 12, UiTheme.Spacing);
            _menu = new UiView(new UiBuilder(menu), RenderMenu);

            _tradeWindow = new UiView(UiRoot.Modal(_canvas.transform, "Trade", 760f, out _tradeLayer), RenderTrade);
            _settingsWindow = new UiView(UiRoot.Modal(_canvas.transform, "Settings", 520f, out _settingsLayer),
                ui => _settings.Render(ui, () => _settingsLayer.SetActive(false)));
            _endWindow = new UiView(UiRoot.Modal(_canvas.transform, "Game End", 560f, out _endLayer), RenderEnd);
            _tradeLayer.SetActive(false);
            _settingsLayer.SetActive(false);
            _endLayer.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_controller?.Session == null || _side == null) return;
            GameState state = _controller.Session.State;
            int revision = _controller.Revision;

            bool tradeOpen = state != null && !_controller.EndReason.HasValue && _trade.KeepOpen(state);
            _side.Refresh(HashCode.Combine(revision, _controller.IsBusy, _controller.EndReason, tradeOpen));
            _log.Refresh(revision);
            _menu.Refresh(0);

            SetLayer(_tradeLayer, _tradeWindow, tradeOpen, revision);
            SetLayer(_settingsLayer, _settingsWindow, _settingsLayer.activeSelf, 0);
            bool ended = !_endDismissed && (_controller.EndReason.HasValue || (state != null && state.Phase == GamePhase.GameOver && !_controller.IsBusy));
            SetLayer(_endLayer, _endWindow, ended, revision);
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

            RenderPlayers(ui, state);
            ui.Space(4f);
            RenderActions(ui, state);
        }

        private void RenderPlayers(UiBuilder ui, GameState state)
        {
            UiBuilder header = ui.Row();
            header.Label($"Tur {state.TurnNumber}", UiTextStyle.Heading);
            if (state.LastDie1 != 0) header.Label($"Zar: {state.LastDie1} + {state.LastDie2}", color: UiTheme.Accent, alignment: TextAnchor.MiddleRight);

            foreach (PlayerState player in state.Players)
            {
                bool current = player.Id == state.CurrentPlayer.Id && state.Phase != GamePhase.GameOver;
                UiBuilder card = ui.Card(current ? new Color(0.24f, 0.25f, 0.31f) : UiTheme.Card, 10);
                UiBuilder row = card.Row(10f);
                Color color = player.IsBankrupt ? UiTheme.Muted : _controller.ColorOf(player.Id);
                row.Swatch(color, 20f);
                Text name = row.Label(player.Name, color: player.IsBankrupt ? UiTheme.Muted : UiTheme.Text);
                name.fontStyle = current ? FontStyle.Bold : FontStyle.Normal;
                UiBuilder.Fix(row.Label(player.IsBankrupt ? "iflas" : player.Money.ToString(), color: UiTheme.Accent, alignment: TextAnchor.MiddleRight), width: 90f);

                string tag = PlayerTag(player);
                if (tag != null) card.Label(tag, UiTextStyle.Small);
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
            ui.Label($"{player.Name} oynuyor", UiTextStyle.Heading);

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
            }

            if (state.Phase == GamePhase.WaitingForRoll || state.Phase == GamePhase.TurnEnd)
            {
                ui.Button("Takas Teklif Et", () => _trade.Open(player.Id), height: UiTheme.SmallButtonHeight);
            }

            ui.Space(8f);
            PropertyPanel.Render(ui, _controller, state, player.Id);
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
            ui.Button("Alma, açık artırmaya çıkar", () => _controller.Submit(new DeclinePurchaseCommand(player.Id)));
        }

        private void RenderDebt(UiBuilder ui, GameState state, PlayerState player)
        {
            DebtState debt = state.Debt;
            string creditor = debt.CreditorId == GameState.NoOwner ? "banka" : state.FindPlayer(debt.CreditorId)?.Name;
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
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                ui.Label(lines[i], UiTextStyle.Small, i == lines.Count - 1 ? UiTheme.Text : UiTheme.Muted);
            }
        }

        private void RenderMenu(UiBuilder ui)
        {
            ui.Button("Ayarlar", () =>
            {
                _settingsLayer.SetActive(true);
                _settingsWindow.Invalidate();
            });
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
                ui.Label($"{state.TurnNumber} turda bitti.", UiTextStyle.Small, alignment: TextAnchor.MiddleCenter);
            }

            ui.Space(8f);
            UiBuilder row = ui.Row();
            row.Button("Tahtaya Bak", () => _endDismissed = true);
            row.Button("Ana Menü", _controller.LeaveToMainMenu, style: UiButtonStyle.Primary);
        }
    }
}
