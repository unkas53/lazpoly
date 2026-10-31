using System.Collections.Generic;
using Lazpoly.App.AI;
using Lazpoly.Core.State;

namespace Lazpoly.App.Save
{
    /// <summary>Everything needed to continue a local game exactly where it stopped.</summary>
    public sealed class SavedGame
    {
        /// <summary>Board and rules the game was played on; a save from other data is not loaded.</summary>
        public ulong BoardChecksum;

        /// <summary>Dice generator, so the game goes on with the same rolls it would have had.</summary>
        public ulong DiceState;
        public ulong DiceIncrement;

        /// <summary>Generator the computer players share.</summary>
        public ulong AiState;
        public ulong AiIncrement;

        /// <summary>Players run by the computer, by player id. Everyone else is a person.</summary>
        public Dictionary<int, SavedAiPlayer> AiPlayers = new Dictionary<int, SavedAiPlayer>();

        public GameStateSnapshot Snapshot;
    }

    public sealed class SavedAiPlayer
    {
        public AiDifficulty Difficulty;

        /// <summary>See <see cref="AiPlayer.LastProposalTurn"/>.</summary>
        public int LastProposalTurn = AiPlayer.NeverProposed;
    }
}
