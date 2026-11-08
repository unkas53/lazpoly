using System.Collections.Generic;
using System.Text;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.State;
using Lazpoly.Presentation.UI;
using UnityEngine.UI;

using static Lazpoly.Presentation.Texts.Lang;
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

            ui.CardHeader(L("Takas teklifi"), L("Önce kiminle takas edeceğini seç, sonra iki taraftan mülklere tıklayıp para yaz."));

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
            give.Label(F($"Sen verirsin (paran {me.Money})"), UiTextStyle.Body, UiTheme.Accent);
            RenderSquareChoices(give, state, _fromId, _give);
            RenderMoneyField(give, _giveMoney, text => _giveMoney = text);

            UiBuilder take = columns.Card();
            take.Label(F($"{partner.Name} verir (parası {partner.Money})"), UiTextStyle.Body, UiTheme.Accent);
            RenderSquareChoices(take, state, _toId, _take);
            RenderMoneyField(take, _takeMoney, text => _takeMoney = text);

            TradeOffer offer = BuildOffer();
            ui.Label(F($"Özet: {Describe(offer.GiveSquares, offer.GiveMoney)} veriyorsun, karşılığında {Describe(offer.TakeSquares, offer.TakeMoney)} alıyorsun."), UiTextStyle.Small);

            ui.Actions(L("Vazgeç"), Close, L("Teklifi Gönder"), () =>
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
            });
            if (_problem != null) ui.Notice(_problem);
            _problem = null;
        }

        /// <summary>Why an offer cannot be sent, in words the player can act on.</summary>
        private static string Problem(GameState state, TradeOffer offer)
        {
            if (offer.IsEmpty) return L("Teklif boş: en az bir mülk seç ya da para yaz.");
            if (offer.GiveMoney > state.FindPlayer(offer.FromPlayerId).Money) return L("Verdiğin para, paranı aşıyor.");
            if (offer.TakeMoney > state.FindPlayer(offer.ToPlayerId).Money) return L("İstediğin para, karşı tarafın parasını aşıyor.");
            return L("Bu teklif şu an gönderilemez.");
        }

        /// <summary>The answer buttons, for whoever the pending offer is for (or its sender, to withdraw it).</summary>
        public void RenderResponse(UiBuilder ui, GameState state)
        {
            TradeOffer offer = state.PendingTrade;
            string from = state.FindPlayer(offer.FromPlayerId)?.Name;
            string to = state.FindPlayer(offer.ToPlayerId)?.Name;
            ui.CardHeader(L("Takas teklifi"), $"{from} → {to}");
            UiBuilder card = ui.Card();
            card.Label(F($"{from} verir: {Describe(offer.GiveSquares, offer.GiveMoney)}"));
            card.Label(F($"{from} ister: {Describe(offer.TakeSquares, offer.TakeMoney)}"));

            // The answer, with "no" on the left and "yes" on the right like every card; the sender may only withdraw.
            var accept = new AcceptTradeCommand(offer.ToPlayerId);
            var reject = new RejectTradeCommand(offer.ToPlayerId);
            var cancel = new CancelTradeCommand(offer.FromPlayerId);
            bool answered = false;
            if (_controller.IsAllowed(accept) || _controller.IsAllowed(reject))
            {
                answered = true;
                ui.Actions(F($"{to}: Reddet"), () => _controller.Submit(reject), F($"{to}: Kabul Et"), () => _controller.Submit(accept),
                    _controller.IsAllowed(accept), UiButtonStyle.Danger);
            }

            if (_controller.IsAllowed(cancel))
            {
                answered = true;
                ui.Button(F($"{from}: Teklifi Geri Çek"), () => _controller.Submit(cancel), height: UiTheme.SmallButtonHeight);
            }

            if (!answered) ui.Label(F($"{to} oyuncusunun cevabı bekleniyor."), UiTextStyle.Small);
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
                bool locked = GameRules.HasBuildings(state, square);
                string label = board[square].Name + (locked ? L(" (binalı)") : "");

                UiBuilder row = list.Row(8f, expand: false);
                row.Swatch(_controller.GroupColorOf(square), 16f, mark: _controller.GroupMarkOf(square));
                Button button = row.Button((chosen ? "• " : "") + label, () =>
                {
                    if (!selected.Remove(index)) selected.Add(index);
                }, !locked, chosen ? UiButtonStyle.Selected : UiButtonStyle.Secondary, UiTheme.SmallButtonHeight);
                UiBuilder.Fix(button, height: UiTheme.SmallButtonHeight).flexibleWidth = 1f;
            }

            if (!any) list.Label(L("Mülk yok."), UiTextStyle.Small);
            else list.Label(L("Binalı şehirler önce bina satılınca takas edilir."), UiTextStyle.Small, UiTheme.Muted);
        }

        private static void RenderMoneyField(UiBuilder ui, string value, System.Action<string> changed)
        {
            UiBuilder row = ui.Row();
            UiBuilder.Fix(row.Label(L("Para")), width: 56f);
            row.Input(value, changed, 7, InputField.ContentType.IntegerNumber);
        }

        private TradeOffer BuildOffer()
        {
            int.TryParse(_giveMoney, out int give);
            int.TryParse(_takeMoney, out int take);
            return new TradeOffer(_fromId, _toId, give, take, new List<int>(_give), new List<int>(_take));
        }

        /// <summary>One side of an offer in words: its towns and money, or "hiçbir şey".</summary>
        public string Describe(IReadOnlyList<int> squares, int money)
        {
            var text = new StringBuilder();
            foreach (int square in squares)
            {
                if (text.Length > 0) text.Append(", ");
                text.Append(L(_controller.Session.Board[square].Name));
            }

            if (money > 0)
            {
                if (text.Length > 0) text.Append(", ");
                text.Append(M(money));
            }

            return text.Length > 0 ? text.ToString() : L("hiçbir şey");
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
