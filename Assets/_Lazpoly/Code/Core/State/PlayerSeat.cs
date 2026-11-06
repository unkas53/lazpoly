using System;

namespace Lazpoly.Core.State
{
    /// <summary>A player taking part in a game, as chosen in the lobby. Turn order follows seat order.</summary>
    public sealed class PlayerSeat
    {
        /// <param name="figure">The character the player picked in the lobby (a figure id); empty when none was picked.</param>
        public PlayerSeat(int id, string name, string figure = null)
        {
            if (id < 0) throw new ArgumentOutOfRangeException(nameof(id), "Player ids must be non-negative.");
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Player name is required.", nameof(name));

            Id = id;
            Name = name;
            Figure = figure ?? string.Empty;
        }

        public int Id { get; }
        public string Name { get; }

        /// <summary>The character the player plays as: only how the pawn and the card look, never a rule.</summary>
        public string Figure { get; }
    }
}
