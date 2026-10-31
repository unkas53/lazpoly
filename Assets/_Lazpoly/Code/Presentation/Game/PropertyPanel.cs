using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Texts;
using Lazpoly.Presentation.UI;

namespace Lazpoly.Presentation.Game
{
    /// <summary>A player's properties, each with the build, sell, mortgage and redeem actions the rules allow now.</summary>
    public static class PropertyPanel
    {
        public static void Render(UiBuilder ui, GameSceneController controller, GameState state, int playerId)
        {
            BoardDefinition board = controller.Session.Board;
            ui.Label("Mülklerin", UiTextStyle.Heading);
            bool any = false;

            for (int square = 0; square < board.Count; square++)
            {
                if (state.GetOwner(square) != playerId) continue;
                any = true;
                SquareDefinition definition = board[square];

                UiBuilder card = ui.Card();
                UiBuilder title = card.Row();
                UiBuilder.Fix(title.Swatch(controller.GroupColorOf(square), 16f), 16f, 16f);
                title.Label(definition.Name);

                string status = state.IsMortgaged(square) ? "ipotekli" : EventDescriber.LevelText(state.GetBuildingLevel(square));
                int rent = GameRules.CalculateRent(board, controller.Session.Rules, state, square);
                card.Label($"{status}, kira {rent}", UiTextStyle.Small);

                // The build button appears only when the rules allow it; otherwise say what is missing.
                var build = new BuildCommand(playerId, square);
                string hint = controller.IsAllowed(build) ? null : BuildHint.For(board, state, playerId, square);
                if (hint != null) card.Label(hint, UiTextStyle.Small, UiTheme.Accent);

                UiBuilder actions = card.Row(6f);
                Offer(actions, controller, build, $"Ev +{definition.BuildingCost}");
                Offer(actions, controller, new SellBuildingCommand(playerId, square), "Ev Sat");
                Offer(actions, controller, new MortgageCommand(playerId, square), $"İpotek +{definition.MortgageValue}");
                Offer(actions, controller, new RedeemCommand(playerId, square), $"Kurtar -{controller.Session.Rules.RedeemCost(definition.MortgageValue)}");
            }

            if (!any) ui.Label("Henüz mülkün yok.", UiTextStyle.Small);
        }

        // Only allowed actions get a button, so the row stays short.
        private static void Offer(UiBuilder row, GameSceneController controller, GameCommand command, string label)
        {
            if (controller.IsAllowed(command)) row.Button(label, () => controller.Submit(command), height: UiTheme.SmallButtonHeight);
        }
    }
}
