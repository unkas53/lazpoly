using System;

namespace Lazpoly.Core.State
{
    /// <summary>A player taking part in a game, as chosen in the lobby. Turn order follows seat order.</summary>
    public sealed class PlayerSeat
    {
        public PlayerSeat(int id, string name)
        {
            if (id < 0) throw new ArgumentOutOfRangeException(nameof(id), "Player ids must be non-negative.");
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Player name is required.", nameof(name));

            Id = id;
            Name = name;
        }

        public int Id { get; }
        public string Name { get; }
    }
}
