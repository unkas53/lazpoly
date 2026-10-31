using Lazpoly.Core.Commands;
using Lazpoly.Core.State;
using UnityEngine;

namespace Lazpoly.Presentation.Game
{
    /// <summary>
    /// Prototype (IMGUI) auction controls. Every player this machine controls who is still in the
    /// auction gets a row: all of them in a local game, one in an online game.
    /// </summary>
    public static class AuctionPanel
    {
        private static readonly int[] Raises = { 10, 50, 100 };

        public static void Draw(GameSceneController controller, GameState state, GUIStyle title)
        {
            AuctionState auction = state.Auction;
            GUILayout.Label($"Açık artırma: {controller.Session.Board[auction.SquareIndex].Name}", title);
            string leader = auction.HighestBidderId == GameState.NoOwner
                ? "henüz teklif yok"
                : $"{state.FindPlayer(auction.HighestBidderId)?.Name}: {auction.HighestBid}";
            GUILayout.Label($"En yüksek: {leader}");

            bool anyRow = false;
            foreach (PlayerState player in state.Players)
            {
                var pass = new PassBidCommand(player.Id);
                if (!controller.IsAllowed(pass)) continue;
                anyRow = true;

                GUILayout.BeginHorizontal();
                GUILayout.Label($"{player.Name} ({player.Money})", GUILayout.Width(120));
                foreach (int raise in Raises)
                {
                    var bid = new PlaceBidCommand(player.Id, auction.HighestBid + raise);
                    GUI.enabled = controller.IsAllowed(bid);
                    if (GUILayout.Button($"+{raise}")) controller.Submit(bid);
                }

                GUI.enabled = true;
                if (GUILayout.Button("Pas")) controller.Submit(pass);
                GUILayout.EndHorizontal();
            }

            if (!anyRow) GUILayout.Label("Diğer oyuncuların teklifleri bekleniyor.");
        }
    }
}
