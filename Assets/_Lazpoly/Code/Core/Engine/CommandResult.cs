using System;
using System.Collections.Generic;
using Lazpoly.Core.Events;

namespace Lazpoly.Core.Engine
{
    public sealed class CommandResult
    {
        private static readonly IReadOnlyList<GameEvent> NoEvents = Array.Empty<GameEvent>();

        private CommandResult(RejectionReason reason, IReadOnlyList<GameEvent> events)
        {
            Reason = reason;
            Events = events;
        }

        public bool IsAccepted => Reason == RejectionReason.None;
        public RejectionReason Reason { get; }

        /// <summary>Events produced by an accepted command, in the order they were applied.</summary>
        public IReadOnlyList<GameEvent> Events { get; }

        internal static CommandResult Accepted(IReadOnlyList<GameEvent> events) =>
            new CommandResult(RejectionReason.None, events);

        internal static CommandResult Rejected(RejectionReason reason) => new CommandResult(reason, NoEvents);
    }
}
