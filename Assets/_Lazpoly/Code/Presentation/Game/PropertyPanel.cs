using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Texts;
using UnityEngine;

namespace Lazpoly.Presentation.Game
{
    /// <summary>Prototype (IMGUI) list of a player's properties with build, sell, mortgage and redeem buttons.</summary>
    public sealed class PropertyPanel
    {
        private readonly GameSceneController _controller;

        public PropertyPanel(GameSceneController controller)
        {
            _controller = controller;
        }

        public void Draw(GameState state, int playerId)
        {
            BoardDefinition board = _controller.Session.Board;
            GUILayout.Label("Mülkler");
            bool any = false;

            for (int square = 0; square < board.Count; square++)
            {
                if (state.GetOwner(square) != playerId) continue;
                any = true;
                SquareDefinition definition = board[square];

                string status = state.IsMortgaged(square) ? "ipotekli" : EventDescriber.LevelText(state.GetBuildingLevel(square));
                int rent = GameRules.CalculateRent(board, _controller.Session.Rules, state, square);
                GUILayout.Label($"{definition.Name}: {status}, kira {rent}");

                GUILayout.BeginHorizontal();
                Offer(new BuildCommand(playerId, square), $"Ev +{definition.BuildingCost}");
                Offer(new SellBuildingCommand(playerId, square), "Ev Sat");
                Offer(new MortgageCommand(playerId, square), $"İpotek +{definition.MortgageValue}");
                Offer(new RedeemCommand(playerId, square), $"Kurtar -{_controller.Session.Rules.RedeemCost(definition.MortgageValue)}");
                GUILayout.EndHorizontal();
            }

            if (!any) GUILayout.Label("Henüz mülk yok.");
        }

        // Only allowed actions get a button, so the row stays short.
        private void Offer(GameCommand command, string label)
        {
            if (_controller.IsAllowed(command) && GUILayout.Button(label)) _controller.Submit(command);
        }
    }
}
