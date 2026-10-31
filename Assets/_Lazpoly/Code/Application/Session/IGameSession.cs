using System;
using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Events;
using Lazpoly.Core.State;

namespace Lazpoly.App.Session
{
    /// <summary>
    /// What the scene talks to. The local session runs the engine in-process; the networked
    /// sessions added in Phase 3 send commands to the host and apply its events instead.
    /// </summary>
    public interface IGameSession
    {
        BoardDefinition Board { get; }
        GameState State { get; }

        /// <summary>Raised with the events of every accepted command, already applied to <see cref="State"/>.</summary>
        event Action<IReadOnlyList<GameEvent>> EventsPublished;

        CommandResult Submit(GameCommand command);
    }
}
