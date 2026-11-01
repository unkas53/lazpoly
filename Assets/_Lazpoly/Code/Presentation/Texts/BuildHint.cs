using Lazpoly.Core.Board;
using Lazpoly.Core.Engine;
using Lazpoly.Core.State;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Texts
{
    /// <summary>
    /// Says why a player cannot build on a town right now, so the property list can explain a
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
            if (level >= SquareDefinition.MaxBuildingLevel) return L("En yüksek seviyede (otel).");

            PlayerState player = state.FindPlayer(playerId);
            if (player == null) return null;
            bool myTurn = state.CurrentPlayer.Id == playerId &&
                          (state.Phase == GamePhase.WaitingForRoll || state.Phase == GamePhase.TurnEnd);
            if (!myTurn || player.Position != squareIndex || state.BuiltThisVisit == GameState.NotArrived)
                return L("Ev ya da otel, kendi sıranda bu şehre gelince kurulur.");

            if (GameRules.NextIsHotel(state, squareIndex) && state.BuiltThisVisit > 0)
                return L("Otel, dört evli şehre bir sonraki gelişinde kurulur.");
            if (player.Laps == 0 && level >= GameRules.FirstLapHouseLimit)
                return F($"Başlangıçtan geçene kadar her şehre tek ev; sonra {GameRules.MaxHouses} eve kadar.");

            if (player.Money < square.BuildingCost)
            {
                return GameRules.NextIsHotel(state, squareIndex)
                    ? F($"Otel için {square.BuildingCost} gerekli, paran yetmiyor.")
                    : F($"Ev için {square.BuildingCost} gerekli, paran yetmiyor.");
            }

            return null;
        }
    }
}
