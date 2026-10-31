using System;
using System.Collections.Generic;
using Lazpoly.App.AI;
using Lazpoly.App.Save;
using Lazpoly.Core.Board;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Random;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;

namespace Lazpoly.App.Session
{
    /// <summary>
    /// A game played on this machine (people and computer players), together with the random
    /// generators it needs so that it can be saved and resumed exactly.
    /// </summary>
    public sealed class LocalGame
    {
        private readonly Pcg32Random _dice;
        private readonly AiRoster _ai;
        private readonly GameEngine _engine;

        private LocalGame(GameEngine engine, Pcg32Random dice, AiRoster ai)
        {
            _engine = engine;
            _dice = dice;
            _ai = ai;
            Session = new LocalGameSession(engine, ai);
        }

        public LocalGameSession Session { get; }

        /// <param name="aiPlayers">Players the computer plays, with their difficulty; everyone else is a person.</param>
        public static LocalGame Create(BoardDefinition board, RulesConfig rules, IReadOnlyList<PlayerSeat> seats,
            IReadOnlyDictionary<int, AiDifficulty> aiPlayers, ulong seed)
        {
            var dice = new Pcg32Random(seed);
            var ai = new AiRoster(new Pcg32Random(seed, stream: 77UL));
            var engine = new GameEngine(board, rules, seats, dice);
            if (aiPlayers != null)
            {
                foreach (KeyValuePair<int, AiDifficulty> pair in aiPlayers)
                {
                    if (engine.State.FindPlayer(pair.Key) == null) throw new ArgumentException($"AI player {pair.Key} is not seated.", nameof(aiPlayers));
                    ai.Add(pair.Key, pair.Value);
                }
            }

            return new LocalGame(engine, dice, ai);
        }

        /// <summary>Continues a saved game. Throws <see cref="ArgumentException"/> if it was saved on other board data.</summary>
        public static LocalGame Resume(BoardDefinition board, RulesConfig rules, ulong boardChecksum, SavedGame save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (save.BoardChecksum != boardChecksum) throw new ArgumentException("The save was made with different board data.", nameof(save));

            Pcg32Random dice = Pcg32Random.FromState(save.DiceState, save.DiceIncrement);
            var ai = new AiRoster(Pcg32Random.FromState(save.AiState, save.AiIncrement));
            GameEngine engine = GameEngine.Restore(board, rules, save.Snapshot, dice);
            foreach (KeyValuePair<int, SavedAiPlayer> pair in save.AiPlayers)
            {
                if (pair.Value == null || engine.State.FindPlayer(pair.Key) == null)
                {
                    throw new ArgumentException($"Saved AI player {pair.Key} is not seated.", nameof(save));
                }

                ai.Add(pair.Key, pair.Value.Difficulty, pair.Value.LastProposalTurn);
            }

            return new LocalGame(engine, dice, ai);
        }

        public SavedGame Save(ulong boardChecksum)
        {
            var save = new SavedGame
            {
                BoardChecksum = boardChecksum,
                DiceState = _dice.State,
                DiceIncrement = _dice.Increment,
                AiState = _ai.Random.State,
                AiIncrement = _ai.Random.Increment,
                Snapshot = GameStateSnapshot.Capture(_engine.State),
            };
            foreach (AiPlayer player in _ai.Players)
            {
                save.AiPlayers.Add(player.PlayerId, new SavedAiPlayer { Difficulty = player.Difficulty, LastProposalTurn = player.LastProposalTurn });
            }
            return save;
        }
    }
}
