using Lazpoly.App.Session;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Menu;
using Lazpoly.Presentation.Texts;
using UnityEngine;

namespace Lazpoly.Presentation.Game
{
    /// <summary>
    /// Prototype interface drawn with IMGUI so the rules can be played end to end. It offers only
    /// actions <see cref="GameSceneController.IsAllowed"/> approves. The final interface (Phase 6)
    /// replaces this component and its panels; the controller API stays the same.
    /// </summary>
    public sealed class PrototypeHud : MonoBehaviour
    {
        private const float LeftWidth = 340f;

        private GameSceneController _controller;
        private PropertyPanel _properties;
        private TradePanel _trade;
        private SettingsPanel _settings;
        private GUIStyle _title;
        private Vector2 _leftScroll;

        public void Bind(GameSceneController controller)
        {
            _controller = controller;
            _properties = new PropertyPanel(controller);
            _trade = new TradePanel(controller);
            _settings = new SettingsPanel();
        }

        private void OnGUI()
        {
            if (_controller?.Session == null) return;

            _title ??= new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            GameState state = _controller.Session.State;

            GUILayout.BeginArea(new Rect(12, 12, LeftWidth, Screen.height - 24), GUI.skin.box);
            _leftScroll = GUILayout.BeginScrollView(_leftScroll);
            if (_controller.EndReason.HasValue)
            {
                DrawEnded(_controller.EndReason.Value);
            }
            else if (state == null)
            {
                GUILayout.Label("Ev sahibine bağlanılıyor...", _title);
                if (GUILayout.Button("Vazgeç", GUILayout.Height(32))) _controller.LeaveToMainMenu();
            }
            else
            {
                DrawPlayers(state);
                GUILayout.Space(8);
                DrawActions(state);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(Screen.width - 352, 12, 340, 260), GUI.skin.box);
            GUILayout.Label("Olaylar", _title);
            foreach (string line in _controller.Log) GUILayout.Label(line);
            GUILayout.EndArea();

            if (state != null && !_controller.EndReason.HasValue) _trade.DrawEditorWindow(state);
        }

        private void DrawPlayers(GameState state)
        {
            GUILayout.Label($"Tur {state.TurnNumber}", _title);
            foreach (PlayerState player in state.Players)
            {
                bool current = player.Id == state.CurrentPlayer.Id && state.Phase != GamePhase.GameOver;
                Color previous = GUI.color;
                GUI.color = player.IsBankrupt ? Color.gray : _controller.ColorOf(player.Id);
                string marker = current ? "▶ " : "   ";
                string status = player.IsBankrupt ? "iflas" : player.Money.ToString();
                bool ai = _controller.Session.IsAiPlaying(player.Id);
                string connection = _controller.IsConnected(player.Id) ? (ai ? " (AI)" : "") : (ai ? " (bağlantı yok, AI oynuyor)" : " (bağlantı yok)");
                GUILayout.Label($"{marker}{player.Name}: {status}{connection}");
                GUI.color = previous;
            }

            if (state.LastDie1 != 0)
            {
                GUILayout.Label($"Zar: {state.LastDie1} + {state.LastDie2}");
            }
        }

        private void DrawActions(GameState state)
        {
            if (state.Phase == GamePhase.GameOver)
            {
                GUILayout.Label($"Kazanan: {state.FindPlayer(state.WinnerId)?.Name}", _title);
                if (GUILayout.Button("Ana Menü", GUILayout.Height(36))) _controller.LeaveToMainMenu();
                return;
            }

            if (_controller.IsBusy)
            {
                GUILayout.Label("...");
                return;
            }

            // Auctions and trade answers involve players other than the one on turn.
            if (state.Phase == GamePhase.AuctionInProgress)
            {
                AuctionPanel.Draw(_controller, state, _title);
            }
            else if (state.Phase == GamePhase.AwaitingTradeResponse)
            {
                _trade.DrawResponse(state, _title);
            }
            else
            {
                DrawTurnActions(state);
            }

            if (_controller.LastRejection != null) GUILayout.Label(_controller.LastRejection);
            DrawLeaveButton();
        }

        private void DrawTurnActions(GameState state)
        {
            PlayerState player = state.CurrentPlayer;
            GUILayout.Label($"{player.Name} oynuyor", _title);

            if (!_controller.Session.CanControl(player.Id))
            {
                GUILayout.Label(_controller.Session.IsAiPlaying(player.Id) ? "Bilgisayar düşünüyor..." : "Sıra onda, hamlesi bekleniyor.");
                return;
            }

            switch (state.Phase)
            {
                case GamePhase.WaitingForRoll:
                    if (GUILayout.Button("Zar At", GUILayout.Height(36))) _controller.Submit(new RollDiceCommand(player.Id));
                    break;

                case GamePhase.AwaitingBuyDecision:
                    SquareDefinition square = _controller.Session.Board[player.Position];
                    GUILayout.Label($"{square.Name}: {square.Price} (kira {square.BaseRent})");
                    var buy = new BuyPropertyCommand(player.Id);
                    GUI.enabled = _controller.IsAllowed(buy);
                    if (GUILayout.Button($"Satın Al ({square.Price})", GUILayout.Height(36))) _controller.Submit(buy);
                    GUI.enabled = true;
                    if (player.Money < square.Price) GUILayout.Label("Paran yetmiyor: ipotek ya da bina satışıyla para bulabilirsin.");
                    if (GUILayout.Button("Alma, açık artırmaya çıkar", GUILayout.Height(30)))
                        _controller.Submit(new DeclinePurchaseCommand(player.Id));
                    break;

                case GamePhase.TurnEnd:
                    if (GUILayout.Button("Sırayı Bitir", GUILayout.Height(36))) _controller.Submit(new EndTurnCommand(player.Id));
                    break;

                case GamePhase.AwaitingDebtPayment:
                    DrawDebt(state, player);
                    break;
            }

            if (state.Phase == GamePhase.WaitingForRoll || state.Phase == GamePhase.TurnEnd)
            {
                if (GUILayout.Button("Takas Teklif Et", GUILayout.Height(28))) _trade.Open(player.Id);
            }

            GUILayout.Space(8);
            _properties.Draw(state, player.Id);
        }

        private void DrawDebt(GameState state, PlayerState player)
        {
            DebtState debt = state.Debt;
            string creditor = debt.CreditorId == GameState.NoOwner ? "banka" : state.FindPlayer(debt.CreditorId)?.Name;
            GUILayout.Label($"Borç: {debt.Amount} → {creditor} ({EventDescriber.ReasonText(debt.Reason)})");
            GUILayout.Label("Bina sat ya da ipotek ederek para bul, sonra öde.");

            var pay = new PayDebtCommand(player.Id);
            GUI.enabled = _controller.IsAllowed(pay);
            if (GUILayout.Button($"Borcu Öde ({debt.Amount})", GUILayout.Height(36))) _controller.Submit(pay);
            GUI.enabled = true;
            if (GUILayout.Button("İflas Et", GUILayout.Height(28))) _controller.Submit(new DeclareBankruptcyCommand(player.Id));
        }

        private void DrawEnded(SessionEndReason reason)
        {
            GUILayout.Label("Oyun durdu", _title);
            GUILayout.Label(GameSceneController.EndReasonText(reason));
            if (GUILayout.Button("Ana Menü", GUILayout.Height(36))) _controller.LeaveToMainMenu();
        }

        private void DrawLeaveButton()
        {
            GUILayout.Space(16);
            if (GUILayout.Button(_settings.IsOpen ? "Ayarları Kapat" : "Ayarlar", GUILayout.Height(28))) _settings.IsOpen = !_settings.IsOpen;
            _settings.Draw();
            if (GUILayout.Button("Oyundan Çık", GUILayout.Height(28))) _controller.LeaveToMainMenu();
        }
    }
}
