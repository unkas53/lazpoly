using System.Collections.Generic;
using System.Text;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.State;
using UnityEngine;

namespace Lazpoly.Presentation.Game
{
    /// <summary>
    /// Prototype (IMGUI) trading: an editor where the player on turn builds an offer, and the
    /// answer buttons shown to whoever the offer is for.
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
        private Vector2 _scroll;

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

        public void DrawEditorWindow(GameState state)
        {
            if (!_open) return;
            bool stillMyTurn = state.CurrentPlayer.Id == _fromId &&
                               (state.Phase == GamePhase.WaitingForRoll || state.Phase == GamePhase.TurnEnd);
            if (!stillMyTurn)
            {
                _open = false;
                return;
            }

            if (_toId == GameState.NoOwner || state.FindPlayer(_toId)?.IsBankrupt != false) _toId = NextPartner(state, _toId);
            if (_toId == GameState.NoOwner)
            {
                _open = false;
                return;
            }

            const float width = 460f;
            GUILayout.BeginArea(new Rect((Screen.width - width) / 2f, 60f, width, Screen.height - 120f), GUI.skin.box);
            GUILayout.Label("Takas teklifi");

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Kime: {state.FindPlayer(_toId).Name}");
            if (GUILayout.Button("Değiştir", GUILayout.Width(90)))
            {
                _toId = NextPartner(state, _toId);
                _take.Clear();
            }

            GUILayout.EndHorizontal();

            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(width / 2f - 12f));
            GUILayout.Label("Verdiklerin");
            DrawSquareToggles(state, _fromId, _give);
            _giveMoney = MoneyField(_giveMoney);
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUILayout.Width(width / 2f - 12f));
            GUILayout.Label("İstediklerin");
            DrawSquareToggles(state, _toId, _take);
            _takeMoney = MoneyField(_takeMoney);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();

            var command = new ProposeTradeCommand(BuildOffer());
            GUI.enabled = _controller.IsAllowed(command);
            if (GUILayout.Button("Teklifi Gönder", GUILayout.Height(32)))
            {
                _controller.Submit(command);
                _open = false;
            }

            GUI.enabled = true;
            if (GUILayout.Button("Kapat", GUILayout.Height(26))) _open = false;
            GUILayout.EndArea();
        }

        public void DrawResponse(GameState state, GUIStyle title)
        {
            TradeOffer offer = state.PendingTrade;
            string from = state.FindPlayer(offer.FromPlayerId)?.Name;
            string to = state.FindPlayer(offer.ToPlayerId)?.Name;
            GUILayout.Label("Takas teklifi", title);
            GUILayout.Label($"{from} → {to}");
            GUILayout.Label($"{from} verir: {Describe(offer.GiveSquares, offer.GiveMoney)}");
            GUILayout.Label($"{from} ister: {Describe(offer.TakeSquares, offer.TakeMoney)}");

            bool answered = false;
            answered |= Offer(new AcceptTradeCommand(offer.ToPlayerId), $"{to}: Kabul Et");
            answered |= Offer(new RejectTradeCommand(offer.ToPlayerId), $"{to}: Reddet");
            answered |= Offer(new CancelTradeCommand(offer.FromPlayerId), $"{from}: Teklifi Geri Çek");
            if (!answered) GUILayout.Label($"{to} oyuncusunun cevabı bekleniyor.");
        }

        private bool Offer(GameCommand command, string label)
        {
            if (!_controller.IsAllowed(command)) return false;
            if (GUILayout.Button(label, GUILayout.Height(30))) _controller.Submit(command);
            return true;
        }

        private void DrawSquareToggles(GameState state, int ownerId, HashSet<int> selected)
        {
            BoardDefinition board = _controller.Session.Board;
            bool any = false;
            for (int square = 0; square < board.Count; square++)
            {
                if (state.GetOwner(square) != ownerId || GameRules.GroupHasBuildings(board, state, square)) continue;
                any = true;
                string label = state.IsMortgaged(square) ? $"{board[square].Name} (ipotekli)" : board[square].Name;
                bool on = GUILayout.Toggle(selected.Contains(square), label);
                if (on) selected.Add(square);
                else selected.Remove(square);
            }

            if (!any) GUILayout.Label("Takas edilebilir mülk yok.");
        }

        private static string MoneyField(string text)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Para:", GUILayout.Width(40));
            text = GUILayout.TextField(text, 7);
            GUILayout.EndHorizontal();
            return text;
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
