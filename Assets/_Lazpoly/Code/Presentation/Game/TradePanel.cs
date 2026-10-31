using System.Collections.Generic;
using System.Text;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.State;
using Lazpoly.Presentation.UI;
using UnityEngine.UI;

namespace Lazpoly.Presentation.Game
{
    /// <summary>
    /// Trading: an editor where the player on turn builds an offer, and the answer buttons shown
    /// to whoever the offer is for.
    /// </summary>
    public sealed class TradePanel
    {
        private readonly GameSceneController _controller;
        private readonly HashSet<int> _give = new HashSet<int>();
        private readonly HashSet<int> _take = new HashSet<int>();
        private bool _open;
        private int _fromId;
        private int _toId;
        private string _giveMoney = "0";
        private string _takeMoney = "0";
        private string _problem;

        public TradePanel(GameSceneController controller)
        {
            _controller = controller;
        }

        public void Open(int fromPlayerId)
        {
            _open = true;
            _fromId = fromPlayerId;
            _toId = GameState.NoOwner;
            _give.Clear();
            _take.Clear();
            _giveMoney = "0";
            _takeMoney = "0";
        }

        public bool IsOpen => _open;

        public void Close() => _open = false;

        /// <summary>Closes the editor if the offer can no longer be made; true while it stays open.</summary>
        public bool KeepOpen(GameState state)
        {
            if (!_open) return false;
            bool stillMyTurn = state.CurrentPlayer.Id == _fromId &&
                               (state.Phase == GamePhase.WaitingForRoll || state.Phase == GamePhase.TurnEnd);
            if (stillMyTurn && (_toId == GameState.NoOwner || state.FindPlayer(_toId)?.IsBankrupt != false)) _toId = NextPartner(state, _toId);
            if (!stillMyTurn || _toId == GameState.NoOwner) _open = false;
            return _open;
        }

        /// <summary>The offer editor, drawn in its own window.</summary>
        public void RenderEditor(UiBuilder ui, GameState state)
        {
            if (!KeepOpen(state)) return;

            ui.Label("Takas teklifi", UiTextStyle.Heading);
            UiBuilder partner = ui.Row();
            partner.Label($"Kime: {state.FindPlayer(_toId).Name}");
            UiBuilder.Fix(partner.Button("Değiştir", () =>
            {
                _toId = NextPartner(state, _toId);
                _take.Clear();
            }, height: UiTheme.SmallButtonHeight), width: 130f);

            UiBuilder columns = ui.Row(16f);
            UiBuilder give = columns.Card();
            give.Label("Verdiklerin", UiTextStyle.Small);
            RenderSquareToggles(give, state, _fromId, _give);
            RenderMoneyField(give, _giveMoney, text => _giveMoney = text);

            UiBuilder take = columns.Card();
            take.Label("İstediklerin", UiTextStyle.Small);
            RenderSquareToggles(take, state, _toId, _take);
            RenderMoneyField(take, _takeMoney, text => _takeMoney = text);

            ui.Space(4f);
            UiBuilder buttons = ui.Row();
            buttons.Button("Kapat", Close);
            buttons.Button("Teklifi Gönder", () =>
            {
                var command = new ProposeTradeCommand(BuildOffer());
                if (_controller.IsAllowed(command))
                {
                    _controller.Submit(command);
                    _open = false;
                }
                else
                {
                    _problem = "Bu teklif kurallara uymuyor: para ya da seçilen mülkleri kontrol et.";
                }
            }, style: UiButtonStyle.Primary);
            if (_problem != null) ui.Label(_problem, UiTextStyle.Small, UiTheme.Warning);
            _problem = null;
        }

        /// <summary>The answer buttons, for whoever the pending offer is for (or its sender, to withdraw it).</summary>
        public void RenderResponse(UiBuilder ui, GameState state)
        {
            TradeOffer offer = state.PendingTrade;
            string from = state.FindPlayer(offer.FromPlayerId)?.Name;
            string to = state.FindPlayer(offer.ToPlayerId)?.Name;
            ui.Label("Takas teklifi", UiTextStyle.Heading);
            UiBuilder card = ui.Card();
            card.Label($"{from} → {to}", color: UiTheme.Accent);
            card.Label($"{from} verir: {Describe(offer.GiveSquares, offer.GiveMoney)}");
            card.Label($"{from} ister: {Describe(offer.TakeSquares, offer.TakeMoney)}");

            bool answered = false;
            answered |= Offer(ui, new AcceptTradeCommand(offer.ToPlayerId), $"{to}: Kabul Et", UiButtonStyle.Primary);
            answered |= Offer(ui, new RejectTradeCommand(offer.ToPlayerId), $"{to}: Reddet", UiButtonStyle.Danger);
            answered |= Offer(ui, new CancelTradeCommand(offer.FromPlayerId), $"{from}: Teklifi Geri Çek", UiButtonStyle.Secondary);
            if (!answered) ui.Label($"{to} oyuncusunun cevabı bekleniyor.", UiTextStyle.Small);
        }

        private bool Offer(UiBuilder ui, GameCommand command, string label, UiButtonStyle style)
        {
            if (!_controller.IsAllowed(command)) return false;
            ui.Button(label, () => _controller.Submit(command), style: style);
            return true;
        }

        private void RenderSquareToggles(UiBuilder ui, GameState state, int ownerId, HashSet<int> selected)
        {
            BoardDefinition board = _controller.Session.Board;
            UiBuilder list = ui.Scroll(260f);
            bool any = false;
            for (int square = 0; square < board.Count; square++)
            {
                if (state.GetOwner(square) != ownerId || GameRules.GroupHasBuildings(board, state, square)) continue;
                any = true;
                int index = square;
                string label = state.IsMortgaged(square) ? $"{board[square].Name} (ipotekli)" : board[square].Name;
                list.Toggle(label, selected.Contains(square), on =>
                {
                    if (on) selected.Add(index);
                    else selected.Remove(index);
                });
            }

            if (!any) list.Label("Takas edilebilir mülk yok.", UiTextStyle.Small);
        }

        private static void RenderMoneyField(UiBuilder ui, string value, System.Action<string> changed)
        {
            UiBuilder row = ui.Row();
            UiBuilder.Fix(row.Label("Para"), width: 56f);
            row.Input(value, changed, 7, InputField.ContentType.IntegerNumber);
        }

        private TradeOffer BuildOffer()
        {
            int.TryParse(_giveMoney, out int give);
            int.TryParse(_takeMoney, out int take);
            return new TradeOffer(_fromId, _toId, give, take, new List<int>(_give), new List<int>(_take));
        }

        private string Describe(IReadOnlyList<int> squares, int money)
        {
            var text = new StringBuilder();
            foreach (int square in squares)
            {
                if (text.Length > 0) text.Append(", ");
                text.Append(_controller.Session.Board[square].Name);
            }

            if (money > 0)
            {
                if (text.Length > 0) text.Append(", ");
                text.Append(money);
            }

            return text.Length > 0 ? text.ToString() : "hiçbir şey";
        }

        private int NextPartner(GameState state, int afterId)
        {
            int count = state.Players.Count;
            int start = afterId == GameState.NoOwner ? state.IndexOfPlayer(_fromId) : state.IndexOfPlayer(afterId);
            for (int step = 1; step <= count; step++)
            {
                PlayerState candidate = state.Players[(start + step) % count];
                if (candidate.Id != _fromId && !candidate.IsBankrupt) return candidate.Id;
            }

            return GameState.NoOwner;
        }
    }
}
