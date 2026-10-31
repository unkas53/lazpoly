using System.Collections.Generic;
using Lazpoly.Core.State;
using UnityEngine;

namespace Lazpoly.Presentation.Setup
{
    /// <summary>Hands the players chosen in the main menu to the Game scene.</summary>
    public static class LocalGameSetup
    {
        private static IReadOnlyList<PlayerSeat> _seats;

        public static void SetSeats(IReadOnlyList<PlayerSeat> seats)
        {
            _seats = seats;
        }

        /// <summary>
        /// Returns the menu's choice once. When the Game scene is started directly in the editor,
        /// falls back to two default players so the scene is playable on its own.
        /// </summary>
        public static IReadOnlyList<PlayerSeat> TakeSeats()
        {
            IReadOnlyList<PlayerSeat> seats = _seats ?? DefaultSeats(2);
            _seats = null;
            return seats;
        }

        public static IReadOnlyList<PlayerSeat> DefaultSeats(int count)
        {
            var seats = new PlayerSeat[count];
            for (int i = 0; i < count; i++)
            {
                seats[i] = new PlayerSeat(i + 1, $"Oyuncu {i + 1}");
            }

            return seats;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _seats = null;
        }
    }
}
