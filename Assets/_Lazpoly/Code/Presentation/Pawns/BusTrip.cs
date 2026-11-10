using System.Collections;
using Lazpoly.Presentation.Tabletop;
using UnityEngine;

namespace Lazpoly.Presentation.Pawns
{
    /// <summary>
    /// A player's ride from the bus station, the owner's wish (2026-11-10): the piece hops into the bus, the bus drives
    /// down to the road round the lawn and along it to the chosen square, the piece hops out onto the square, and the
    /// bus goes home by itself while the game goes on.
    /// </summary>
    public static class BusTrip
    {
        /// <summary>Whether the bus can take <paramref name="pawn"/>: the board has its bus and the piece stands at the station.</summary>
        public static bool CanCarry(BoardView board, PawnView pawn) =>
            board != null && pawn != null && board.Bus != null && pawn.Square == board.StationSquare;

        /// <summary>The whole ride of <paramref name="pawn"/> to <paramref name="destination"/>; ends when the piece stands there.</summary>
        public static IEnumerator Carry(BoardView board, PawnView pawn, int destination)
        {
            BusView bus = board.Bus;
            float speed = Mathf.Max(pawn.Speed, 0.1f);
            // Still on its way back from the last ride: it is at the stop at once for this one.
            bus.Park();
            yield return pawn.HopInto(bus.Roof);
            BusRoute way = board.BusRouteTo(destination);
            yield return bus.Drive(way, BusRoute.Seconds(way.Length) / speed);
            yield return pawn.HopOutOf(bus.Roof, destination);
            BusRoute home = board.BusRouteHome(destination);
            bus.DriveHome(home, BusRoute.Seconds(home.Length) / speed);
        }
    }
}
