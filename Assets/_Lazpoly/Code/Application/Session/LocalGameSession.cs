using System;
using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Events;
using Lazpoly.Core.State;

namespace Lazpoly.App.Session
{
    /// <summary>Single-machine session: this process is the host and every player sits here (hot seat).</summary>
    public sealed class LocalGameSession : IGameSession
    {
        private readonly GameEngine _engine;

        public LocalGameSession(GameEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        public BoardDefinition Board => _engine.Board;
        public GameState State => _engine.State;
        public bool IsReady => true;

        // Never raised locally; present to satisfy the session contract.
        public event Action Ready { add { } remove { } }
        public event Action StateReset { add { } remove { } }
        public event Action<SessionEndReason> Ended { add { } remove { } }

        public event Action<IReadOnlyList<GameEvent>> EventsPublished;
        public event Action<RejectionReason> CommandRejected;

        public bool CanControl(int playerId) => State.FindPlayer(playerId) != null;

        public void Submit(GameCommand command)
        {
            CommandResult result = _engine.Execute(command);
            if (!result.IsAccepted)
            {
                CommandRejected?.Invoke(result.Reason);
                return;
            }

            if (result.Events.Count > 0) EventsPublished?.Invoke(result.Events);
        }

        public void Tick()
        {
        }
    }
}
