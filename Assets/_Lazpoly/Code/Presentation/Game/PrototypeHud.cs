using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.State;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lazpoly.Presentation.Game
{
    /// <summary>
    /// Phase 2 prototype interface drawn with IMGUI so the rules can be played end to end.
    /// The final interface (Phase 6) replaces this component; the controller API stays the same.
    /// </summary>
    public sealed class PrototypeHud : MonoBehaviour
    {
        private GameSceneController _controller;
        private GUIStyle _title;

        public void Bind(GameSceneController controller)
        {
            _controller = controller;
        }

        private void OnGUI()
        {
            if (_controller?.Session == null) return;

            _title ??= new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            GameState state = _controller.Session.State;

            GUILayout.BeginArea(new Rect(12, 12, 300, Screen.height - 24), GUI.skin.box);
            DrawPlayers(state);
            GUILayout.Space(8);
            DrawActions(state);
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(Screen.width - 352, 12, 340, 260), GUI.skin.box);
            GUILayout.Label("Olaylar", _title);
            foreach (string line in _controller.Log) GUILayout.Label(line);
            GUILayout.EndArea();
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
                GUILayout.Label($"{marker}{player.Name}: {status}");
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
                if (GUILayout.Button("Ana Menü", GUILayout.Height(36))) SceneManager.LoadScene("MainMenu");
                return;
            }

            if (_controller.IsBusy)
            {
                GUILayout.Label("...");
                return;
            }

            PlayerState player = state.CurrentPlayer;
            GUILayout.Label($"{player.Name} oynuyor", _title);

            switch (state.Phase)
            {
                case GamePhase.WaitingForRoll:
                    if (GUILayout.Button("Zar At", GUILayout.Height(36))) _controller.Submit(new RollDiceCommand(player.Id));
                    break;

                case GamePhase.AwaitingBuyDecision:
                    SquareDefinition square = _controller.Session.Board[player.Position];
                    GUILayout.Label($"{square.Name}: {square.Price} (kira {square.BaseRent})");
                    if (GUILayout.Button($"Satın Al ({square.Price})", GUILayout.Height(36)))
                        _controller.Submit(new BuyPropertyCommand(player.Id));
                    if (GUILayout.Button("Alma", GUILayout.Height(30)))
                        _controller.Submit(new DeclinePurchaseCommand(player.Id));
                    break;

                case GamePhase.TurnEnd:
                    if (GUILayout.Button("Sırayı Bitir", GUILayout.Height(36))) _controller.Submit(new EndTurnCommand(player.Id));
                    break;
            }

            if (_controller.LastRejection != null) GUILayout.Label(_controller.LastRejection);
        }
    }
}
