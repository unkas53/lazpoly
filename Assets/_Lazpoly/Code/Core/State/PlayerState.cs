using System.Collections.Generic;

namespace Lazpoly.Core.State
{
    /// <summary>One player's part of the game state. Changed only by <see cref="GameStateReducer"/>.</summary>
    public sealed class PlayerState
    {
        internal PlayerState(int id, string name, int money, string figure = null)
        {
            Id = id;
            Name = name;
            Money = money;
            Figure = figure ?? string.Empty;
        }

        public int Id { get; }
        public string Name { get; }

        /// <summary>The character picked in the lobby (see <see cref="PlayerSeat.Figure"/>); empty when none was picked.</summary>
        public string Figure { get; }
        public int Money { get; internal set; }

        /// <summary>Index of the board square the player stands on.</summary>
        public int Position { get; internal set; }

        public bool IsBankrupt { get; internal set; }

        /// <summary>How many times the player has passed the start square. Until the first pass, each town takes a single house.</summary>
        public int Laps { get; internal set; }

        /// <summary>The player's next roll is a choice of two (from a lookout square or card).</summary>
        public bool HasLookout { get; internal set; }

        /// <summary>How many more of their turns the player waits in jail; 0 when free.</summary>
        public int JailTurns { get; internal set; }

        public bool InJail => JailTurns > 0;

        internal readonly List<int> HandCards = new List<int>();

        /// <summary>The cards the player keeps to play later (Faz 30), as indexes into the board's deck, in the order drawn; open to all.</summary>
        public IReadOnlyList<int> Hand => HandCards;

        /// <summary>What the player's investment fund pays on their next pass of Start; 0 when they hold none (Faz 30).</summary>
        public int FundPayout { get; internal set; }
    }
}
