using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Texts;
using Lazpoly.Presentation.UI;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Game
{
    /// <summary>A player's properties, each with the build and sell actions the rules allow now.</summary>
    public static class PropertyPanel
    {
        public static void Render(UiBuilder ui, GameSceneController controller, GameState state, int playerId)
        {
            BoardDefinition board = controller.Session.Board;
            ui.Label(L("Mülklerin"), UiTextStyle.Heading);
            bool any = false;

            for (int square = 0; square < board.Count; square++)
            {
                if (state.GetOwner(square) != playerId) continue;
                any = true;
                SquareDefinition definition = board[square];

                UiBuilder card = ui.Card();
                UiBuilder title = card.Row();
                UiBuilder.Fix(title.Swatch(controller.GroupColorOf(square), 16f), 16f, 16f);
                title.Label(L(definition.Name));

                string status = EventDescriber.LevelText(state.GetBuildingLevel(square));
                int rent = GameRules.CalculateRent(board, controller.Session.Rules, state, square);
                card.Label(F($"{status}, kira {rent}"), UiTextStyle.Small);

                // The build button appears only when the rules allow it; otherwise say what is missing.
                var build = new BuildCommand(playerId, square);
                string hint = controller.IsAllowed(build) ? null : BuildHint.For(board, state, playerId, square);
                if (hint != null) card.Label(hint, UiTextStyle.Small, UiTheme.Accent);

                UiBuilder actions = card.Row(6f);
                Offer(actions, controller, build, GameRules.NextIsHotel(state, square) ? F($"Otel +{definition.BuildingCost}") : F($"Ev +{definition.BuildingCost}"));
                Offer(actions, controller, new SellBuildingCommand(playerId, square), L("Ev Sat"));
                int proceeds = GameRules.PropertySaleProceeds(board, controller.Session.Rules, state, square);
                Offer(actions, controller, new SellPropertyCommand(playerId, square), F($"Sat +{proceeds}"));
            }

            if (!any) ui.Label(L("Henüz mülkün yok."), UiTextStyle.Small);
        }

        // Only allowed actions get a button, so the row stays short.
        private static void Offer(UiBuilder row, GameSceneController controller, GameCommand command, string label)
        {
            if (controller.IsAllowed(command)) row.Button(label, () => controller.Submit(command), height: UiTheme.SmallButtonHeight);
        }
    }
}
