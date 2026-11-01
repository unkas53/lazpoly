using Lazpoly.Core.Commands;
using Lazpoly.Core.State;
using Lazpoly.Presentation.UI;

using static Lazpoly.Presentation.Texts.Lang;
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
            string leader = auction.HighestBidderId == GameState.NoOwner
                ? L("Henüz teklif yok")
                : F($"En yüksek: {state.FindPlayer(auction.HighestBidderId)?.Name}, {auction.HighestBid}");
            ui.CardHeader(F($"Açık artırma: {L(controller.Session.Board[auction.SquareIndex].Name)}"), leader,
                controller.GroupColorOf(auction.SquareIndex));

            bool anyRow = false;
            foreach (PlayerState player in state.Players)
            {
                var pass = new PassBidCommand(player.Id);
                if (!controller.IsAllowed(pass)) continue;
                anyRow = true;

                UiBuilder card = ui.Card();
                card.Label(F($"{player.Name} (parası {player.Money})"));
                // Passing on the left, bidding on the right, like the way out and the action on every card.
                UiBuilder row = card.Row();
                row.Button(L("Pas"), () => controller.Submit(pass), height: UiTheme.SmallButtonHeight);
                foreach (int raise in Raises)
                {
                    var bid = new PlaceBidCommand(player.Id, auction.HighestBid + raise);
                    row.Button($"+{raise}", () => controller.Submit(bid), controller.IsAllowed(bid), UiButtonStyle.Primary, UiTheme.SmallButtonHeight);
                }
            }

            if (!anyRow) ui.Label(L("Diğer oyuncuların teklifleri bekleniyor."), UiTextStyle.Small);
        }
    }
}
