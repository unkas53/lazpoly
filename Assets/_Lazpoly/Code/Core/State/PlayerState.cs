namespace Lazpoly.Core.State
{
    /// <summary>One player's part of the game state. Changed only by <see cref="GameStateReducer"/>.</summary>
    public sealed class PlayerState
    {
        internal PlayerState(int id, string name, int money)
        {
            Id = id;
            Name = name;
            Money = money;
        }

        public int Id { get; }
        public string Name { get; }
        public int Money { get; internal set; }

        /// <summary>Index of the board square the player stands on.</summary>
        public int Position { get; internal set; }

        public bool IsBankrupt { get; internal set; }

        /// <summary>How many times the player has passed the start square. Until the first pass, each town takes a single house.</summary>
        public int Laps { get; internal set; }

        /// <summary>The player's next roll is a choice of two (from a lookout square or card).</summary>
        public bool HasLookout { get; internal set; }
    }
}
