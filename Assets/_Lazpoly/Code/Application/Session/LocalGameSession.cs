using System;
using System.Collections.Generic;
using Lazpoly.App.AI;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Events;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;

namespace Lazpoly.App.Session
{
    /// <summary>
    /// Single-machine session: this process runs the rules, the people take turns at the same screen
    /// (hot seat) and the computer plays the AI seats.
    /// </summary>
    public sealed class LocalGameSession : IGameSession
    {
        private readonly GameEngine _engine;
        private readonly AiRoster _ai;

        /// <param name="ai">Computer players; null or empty when everyone is a person.</param>
        public LocalGameSession(GameEngine engine, AiRoster ai = null)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _ai = ai;
            if (ai == null) return;
            foreach (AiPlayer player in ai.Players)
            {
                if (engine.State.FindPlayer(player.PlayerId) == null) throw new ArgumentException($"AI player {player.PlayerId} is not seated.", nameof(ai));
            }
        }

        public BoardDefinition Board => _engine.Board;
        public RulesConfig Rules => _engine.Rules;
        public GameState State => _engine.State;
        public bool IsReady => true;

        // Never raised locally; present to satisfy the session contract.
        public event Action Ready { add { } remove { } }
        public event Action StateReset { add { } remove { } }
        public event Action<SessionEndReason> Ended { add { } remove { } }

        public event Action<IReadOnlyList<GameEvent>> EventsPublished;
        public event Action<RejectionReason> CommandRejected;

        public bool CanControl(int playerId) => State.FindPlayer(playerId) != null && !IsAiPlaying(playerId);

        public bool IsAiPlaying(int playerId) => _ai != null && _ai.Controls(playerId);

        public void Submit(GameCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (IsAiPlaying(command.PlayerId))
            {
                CommandRejected?.Invoke(RejectionReason.NotYourPlayer);
                return;
            }

            CommandResult result = _engine.Execute(command);
            if (!result.IsAccepted)
            {
                CommandRejected?.Invoke(result.Reason);
                return;
            }

            if (result.Events.Count > 0) EventsPublished?.Invoke(result.Events);
        }

        public bool StepAi()
        {
            GameCommand command = _ai?.NextCommand(_engine.Board, _engine.Rules, _engine.State);
            if (command == null) return false;

            CommandResult result = _engine.Execute(command);
            if (!result.IsAccepted)
            {
                // The AI only picks commands the rules allow, so this is a bug, not a game situation.
                throw new InvalidOperationException($"AI chose a rejected {command.GetType().Name} ({result.Reason}).");
            }

            if (result.Events.Count > 0) EventsPublished?.Invoke(result.Events);
            return true;
        }

        public void Tick()
        {
        }
    }
}
