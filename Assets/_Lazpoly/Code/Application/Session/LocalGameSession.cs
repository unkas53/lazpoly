using System;
using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Events;
using Lazpoly.Core.State;

namespace Lazpoly.App.Session
{
    /// <summary>Single-machine session: this process is the host. Used for hot-seat and, later, single player.</summary>
    public sealed class LocalGameSession : IGameSession
    {
        private readonly GameEngine _engine;

        public LocalGameSession(GameEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        public BoardDefinition Board => _engine.Board;
        public GameState State => _engine.State;

        public event Action<IReadOnlyList<GameEvent>> EventsPublished;

        public CommandResult Submit(GameCommand command)
        {
            CommandResult result = _engine.Execute(command);
            if (result.IsAccepted && result.Events.Count > 0)
            {
                EventsPublished?.Invoke(result.Events);
            }

            return result;
        }
    }
}
