using System;
using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Events;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;

namespace Lazpoly.App.Session
{
    public enum SessionEndReason
    {
        HostLeft = 1,
        NotInThisGame = 2,
        VersionMismatch = 3,
        BoardMismatch = 4,
    }

    /// <summary>
    /// What the scene talks to, whatever runs the rules: the local engine, the host side of an
    /// online game, or a client that only mirrors the host.
    /// </summary>
    public interface IGameSession
    {
        BoardDefinition Board { get; }

        RulesConfig Rules { get; }

        /// <summary>The current state; null until <see cref="IsReady"/> (a client waits for the host).</summary>
        GameState State { get; }

        bool IsReady { get; }

        /// <summary>Raised once when <see cref="State"/> first becomes available.</summary>
        event Action Ready;

        /// <summary>Raised with the events of every accepted command, already applied to <see cref="State"/>.</summary>
        event Action<IReadOnlyList<GameEvent>> EventsPublished;

        /// <summary>Raised when <see cref="State"/> was replaced from a host snapshot; views must redraw from scratch.</summary>
        event Action StateReset;

        /// <summary>Raised when a command submitted from this machine was refused.</summary>
        event Action<RejectionReason> CommandRejected;

        /// <summary>Raised when the session can no longer continue (host gone, refused, incompatible).</summary>
        event Action<SessionEndReason> Ended;

        /// <summary>Whether the person (or AI) on this machine plays the given player.</summary>
        bool CanControl(int playerId);

        void Submit(GameCommand command);

        /// <summary>Processes network traffic. Call once per frame.</summary>
        void Tick();
    }
}
