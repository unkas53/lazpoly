using Lazpoly.Core.Commands;
using Lazpoly.Core.State;
using Lazpoly.Presentation.UI;

namespace Lazpoly.Presentation.Game
{
    /// <summary>
    /// Auction controls. Every player this machine controls who is still in the auction gets a row:
    /// all of them in a local game, one in an online game.
    /// </summary>
    public static class AuctionPanel
    {
        private static readonly int[] Raises = { 10, 50, 100 };

        public static void Render(UiBuilder ui, GameSceneController controller, GameState state)
        {
            AuctionState auction = state.Auction;
            ui.Label($"Açık artırma: {controller.Session.Board[auction.SquareIndex].Name}", UiTextStyle.Heading);
            string leader = auction.HighestBidderId == GameState.NoOwner
                ? "Henüz teklif yok"
                : $"En yüksek: {state.FindPlayer(auction.HighestBidderId)?.Name}, {auction.HighestBid}";
            ui.Label(leader, color: UiTheme.Accent);

            bool anyRow = false;
            foreach (PlayerState player in state.Players)
            {
                var pass = new PassBidCommand(player.Id);
                if (!controller.IsAllowed(pass)) continue;
                anyRow = true;

                UiBuilder card = ui.Card();
                card.Label($"{player.Name} (parası {player.Money})");
                UiBuilder row = card.Row();
                foreach (int raise in Raises)
                {
                    var bid = new PlaceBidCommand(player.Id, auction.HighestBid + raise);
                    row.Button($"+{raise}", () => controller.Submit(bid), controller.IsAllowed(bid), UiButtonStyle.Primary, UiTheme.SmallButtonHeight);
                }

                row.Button("Pas", () => controller.Submit(pass), height: UiTheme.SmallButtonHeight);
            }

            if (!anyRow) ui.Label("Diğer oyuncuların teklifleri bekleniyor.", UiTextStyle.Small);
        }
    }
}
