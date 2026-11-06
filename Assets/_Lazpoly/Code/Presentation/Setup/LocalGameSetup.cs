using System.Collections.Generic;
using Lazpoly.App.AI;
using Lazpoly.App.Save;
using Lazpoly.Core.State;
using UnityEngine;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Setup
{
    /// <summary>Hands the main menu's local game choice (new players or a saved game) to the Game scene.</summary>
    public static class LocalGameSetup
    {
        private static readonly IReadOnlyDictionary<int, AiDifficulty> NoAi = new Dictionary<int, AiDifficulty>();

        private static IReadOnlyList<PlayerSeat> _seats;
        private static IReadOnlyDictionary<int, AiDifficulty> _aiPlayers;
        private static SavedGame _resume;

        /// <param name="aiPlayers">Seats the computer plays, by player id; null when everyone is a person.</param>
        public static void SetSeats(IReadOnlyList<PlayerSeat> seats, IReadOnlyDictionary<int, AiDifficulty> aiPlayers = null)
        {
            _seats = seats;
            _aiPlayers = aiPlayers;
            _resume = null;
        }

        /// <summary>Asks the Game scene to continue a saved game instead of starting a new one.</summary>
        public static void SetResume(SavedGame save)
        {
            _resume = save;
            _seats = null;
            _aiPlayers = null;
        }

        /// <summary>The saved game to continue, once; null when the menu chose new players.</summary>
        public static SavedGame TakeResume()
        {
            SavedGame save = _resume;
            _resume = null;
            return save;
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

        /// <summary>The AI seats chosen with the last <see cref="SetSeats"/>, once; empty when none.</summary>
        public static IReadOnlyDictionary<int, AiDifficulty> TakeAiPlayers()
        {
            IReadOnlyDictionary<int, AiDifficulty> ai = _aiPlayers ?? NoAi;
            _aiPlayers = null;
            return ai;
        }

        public static IReadOnlyList<PlayerSeat> DefaultSeats(int count)
        {
            var seats = new PlayerSeat[count];
            for (int i = 0; i < count; i++)
            {
                seats[i] = new PlayerSeat(i + 1, F($"Oyuncu {i + 1}"), CharacterChoice.ForSeat(i).Id);
            }

            return seats;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _seats = null;
            _aiPlayers = null;
            _resume = null;
        }
    }
}
