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

        /// <summary>Whether a person at this machine plays the given player (the HUD offers their actions).</summary>
        bool CanControl(int playerId);

        /// <summary>
        /// Whether the computer plays the given player: a chosen AI seat, or a stand-in for an online
        /// player who dropped, never arrived or stopped answering. Decided where the rules run; a
        /// client learns it from the host's presence messages.
        /// </summary>
        bool IsAiPlaying(int playerId);

        /// <summary>Whether the player's machine is reachable. Always true in a local game.</summary>
        bool IsConnected(int playerId);

        /// <summary>Round-trip time to the player's machine in milliseconds; 0 for this machine, -1 when unknown.</summary>
        int PingMs(int playerId);

        /// <summary>Lets the computer make one move if the game waits on an AI-played player. Returns whether it moved.</summary>
        bool StepAi();

        void Submit(GameCommand command);

        /// <summary>Processes network traffic. Call once per frame.</summary>
        void Tick();
    }
}
