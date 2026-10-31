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
            PlayerState me = state.FindPlayer(_fromId);
            PlayerState partner = state.FindPlayer(_toId);

            ui.Label("Takas teklifi", UiTextStyle.Heading);
            ui.Label("Önce kiminle takas edeceğini seç, sonra iki taraftan mülklere tıklayıp para yaz.", UiTextStyle.Small, UiTheme.Muted);

            UiBuilder partners = ui.Row();
            foreach (PlayerState player in state.Players)
            {
                if (player.Id == _fromId || player.IsBankrupt) continue;
                int id = player.Id;
                partners.Button(player.Name, () =>
                {
                    if (_toId == id) return;
                    _toId = id;
                    _take.Clear();
                    _takeMoney = "0";
                }, style: id == _toId ? UiButtonStyle.Selected : UiButtonStyle.Secondary, height: UiTheme.SmallButtonHeight);
            }

            UiBuilder columns = ui.Row(16f);
            UiBuilder give = columns.Card();
            give.Label($"Sen verirsin (paran {me.Money})", UiTextStyle.Body, UiTheme.Accent);
            RenderSquareChoices(give, state, _fromId, _give);
            RenderMoneyField(give, _giveMoney, text => _giveMoney = text);

            UiBuilder take = columns.Card();
            take.Label($"{partner.Name} verir (parası {partner.Money})", UiTextStyle.Body, UiTheme.Accent);
            RenderSquareChoices(take, state, _toId, _take);
            RenderMoneyField(take, _takeMoney, text => _takeMoney = text);

            TradeOffer offer = BuildOffer();
            ui.Label($"Özet: {Describe(offer.GiveSquares, offer.GiveMoney)} veriyorsun, karşılığında {Describe(offer.TakeSquares, offer.TakeMoney)} alıyorsun.", UiTextStyle.Small);

            UiBuilder buttons = ui.Row();
            buttons.Button("Vazgeç", Close);
            buttons.Button("Teklifi Gönder", () =>
            {
                TradeOffer current = BuildOffer();
                var command = new ProposeTradeCommand(current);
                if (_controller.IsAllowed(command))
                {
                    _controller.Submit(command);
                    _open = false;
                }
                else
                {
                    _problem = Problem(state, current);
                }
            }, style: UiButtonStyle.Primary);
            if (_problem != null) ui.Label(_problem, UiTextStyle.Small, UiTheme.Warning);
            _problem = null;
        }

        /// <summary>Why an offer cannot be sent, in words the player can act on.</summary>
        private static string Problem(GameState state, TradeOffer offer)
        {
            if (offer.IsEmpty) return "Teklif boş: en az bir mülk seç ya da para yaz.";
            if (offer.GiveMoney > state.FindPlayer(offer.FromPlayerId).Money) return "Verdiğin para, paranı aşıyor.";
            if (offer.TakeMoney > state.FindPlayer(offer.ToPlayerId).Money) return "İstediğin para, karşı tarafın parasını aşıyor.";
            return "Bu teklif şu an gönderilemez.";
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

        /// <summary>Every tradable square of one side as a button that is lit while it is part of the offer.</summary>
        private void RenderSquareChoices(UiBuilder ui, GameState state, int ownerId, HashSet<int> selected)
        {
            BoardDefinition board = _controller.Session.Board;
            UiBuilder list = ui.Scroll(260f);
            bool any = false;
            for (int square = 0; square < board.Count; square++)
            {
                if (state.GetOwner(square) != ownerId || board[square].Type != SquareType.Property) continue;
                any = true;
                int index = square;
                bool chosen = selected.Contains(square);
                bool locked = GameRules.GroupHasBuildings(board, state, square);
                string label = board[square].Name + (state.IsMortgaged(square) ? " (ipotekli)" : "") + (locked ? " (binalı)" : "");

                UiBuilder row = list.Row(8f, expand: false);
                row.Swatch(_controller.GroupColorOf(square), 16f);
                Button button = row.Button((chosen ? "• " : "") + label, () =>
                {
                    if (!selected.Remove(index)) selected.Add(index);
                }, !locked, chosen ? UiButtonStyle.Selected : UiButtonStyle.Secondary, UiTheme.SmallButtonHeight);
                UiBuilder.Fix(button, height: UiTheme.SmallButtonHeight).flexibleWidth = 1f;
            }

            if (!any) list.Label("Mülk yok.", UiTextStyle.Small);
            else list.Label("Binalı renkler önce bina satılınca takas edilir.", UiTextStyle.Small, UiTheme.Muted);
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
