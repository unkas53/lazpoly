using System;
using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Random;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;

namespace Lazpoly.App.AI
{
    /// <summary>
    /// The computer players of one game and the random source they share. Runs only on the machine
    /// that owns the rules (local game or online host); clients just see the resulting events.
    /// </summary>
    public sealed class AiRoster
    {
        private readonly Dictionary<int, AiPlayer> _players = new Dictionary<int, AiPlayer>();

        public AiRoster(Pcg32Random random)
        {
            Random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>Kept so a saved game resumes with the same AI choices.</summary>
        public Pcg32Random Random { get; }

        public int Count => _players.Count;

        public bool Controls(int playerId) => _players.ContainsKey(playerId);

        /// <summary>Every computer player, in no particular order.</summary>
        public IEnumerable<AiPlayer> Players => _players.Values;

        public void Add(int playerId, AiDifficulty difficulty, int lastProposalTurn = AiPlayer.NeverProposed)
        {
            if (_players.ContainsKey(playerId)) throw new ArgumentException($"Player {playerId} already has an AI.", nameof(playerId));
            _players.Add(playerId, new AiPlayer(playerId, difficulty, lastProposalTurn));
        }

        public bool Remove(int playerId) => _players.Remove(playerId);

        /// <summary>The next command an AI-played player owes the game, or null when the game waits on a person.</summary>
        public GameCommand NextCommand(BoardDefinition board, RulesConfig rules, GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            foreach (int playerId in AiTurnOrder.PlayersToAct(state))
            {
                if (!_players.TryGetValue(playerId, out AiPlayer player)) continue;
                GameCommand command = player.Decide(board, rules, state, Random);
                if (command != null) return command;
            }

            return null;
        }
    }
}
