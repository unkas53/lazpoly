using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Engine;
using Lazpoly.Core.State;

namespace Lazpoly.Presentation.Texts
{
    /// <summary>
    /// Says why a player cannot build on a property right now, so the property list can explain a
    /// missing "build" button. The rules themselves stay in <see cref="GameRules"/>; this only names
    /// the first condition that is not met, in the order a player would fix them.
    /// </summary>
    public static class BuildHint
    {
        /// <returns>Null when the square takes no buildings or nothing stands in the way.</returns>
        public static string For(BoardDefinition board, GameState state, int playerId, int squareIndex)
        {
            SquareDefinition square = board[squareIndex];
            if (!square.IsDevelopable || state.GetOwner(squareIndex) != playerId) return null;

            int level = state.GetBuildingLevel(squareIndex);
            if (level >= SquareDefinition.MaxBuildingLevel) return "En yüksek seviyede (otel).";

            var missing = new List<string>();
            bool mortgaged = false, behind = false;
            foreach (int other in GameRules.GroupSquares(board, square.GroupId))
            {
                if (state.GetOwner(other) != playerId) missing.Add(board[other].Name);
                if (state.IsMortgaged(other)) mortgaged = true;
                if (other != squareIndex && state.GetBuildingLevel(other) < level) behind = true;
            }

            if (missing.Count > 0) return $"Ev için bu rengin tamamı gerekli; eksik: {string.Join(", ", missing)}.";
            if (mortgaged) return "Bu renkte ipotekli kare var; önce onu kurtar.";
            if (behind) return "Evler eşit kurulur: önce bu rengin diğer karelerine kur.";

            PlayerState player = state.FindPlayer(playerId);
            if (player != null && player.Money < square.BuildingCost) return $"Ev için {square.BuildingCost} gerekli, paran yetmiyor.";

            bool rightTime = state.CurrentPlayer.Id == playerId &&
                             (state.Phase == GamePhase.WaitingForRoll || state.Phase == GamePhase.TurnEnd);
            return rightTime ? null : "Ev, kendi sıranda zar atmadan önce ya da sırayı bitirmeden önce kurulur.";
        }
    }
}
