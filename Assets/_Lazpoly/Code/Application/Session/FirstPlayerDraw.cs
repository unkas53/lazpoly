using System;
using System.Collections.Generic;
using Lazpoly.Core.Random;
using Lazpoly.Core.State;

namespace Lazpoly.App.Session
{
    /// <summary>
    /// The draw for who rolls first in a new game. The seats keep their order round the table; the list
    /// is turned so the drawn seat comes first, because the game always starts with the first seat and
    /// counts a round each time the turn comes back to it. Only the one who starts the game draws (the
    /// local game, or the host online); clients get the result in the state and a resumed game keeps its order.
    /// </summary>
    public static class FirstPlayerDraw
    {
        /// <summary>Draws a seat and turns the list so it starts.</summary>
        public static IReadOnlyList<PlayerSeat> Draw(IReadOnlyList<PlayerSeat> seats, IRandomSource random)
        {
            if (seats == null) throw new ArgumentNullException(nameof(seats));
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (seats.Count == 0) return seats;
            return StartWith(seats, random.NextInt(0, seats.Count));
        }

        /// <summary>The same seats round the table, starting with the one at <paramref name="first"/>.</summary>
        public static IReadOnlyList<PlayerSeat> StartWith(IReadOnlyList<PlayerSeat> seats, int first)
        {
            if (seats == null) throw new ArgumentNullException(nameof(seats));
            if (first < 0 || first >= seats.Count) throw new ArgumentOutOfRangeException(nameof(first));
            var turned = new List<PlayerSeat>(seats.Count);
            for (int i = 0; i < seats.Count; i++) turned.Add(seats[(first + i) % seats.Count]);
            return turned;
        }

        /// <summary>
        /// A player's place in the setup or lobby list, whatever the turn order: seats are numbered 1, 2, 3...
        /// in that list, so it is how many players have a smaller id. Colours and characters follow it, so
        /// they match the lobby even when the draw let someone else start.
        /// </summary>
        public static int LobbySeat(IReadOnlyList<PlayerState> players, int playerId)
        {
            if (players == null) throw new ArgumentNullException(nameof(players));
            int seat = 0;
            foreach (PlayerState player in players)
            {
                if (player.Id < playerId) seat++;
            }

            return seat;
        }
    }
}
