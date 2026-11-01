using System.Collections.Generic;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Events;
using Lazpoly.Core.State;

namespace Lazpoly.Net.Messages
{
    public enum MessageType : byte
    {
        Hello = 1,
        GameStart = 2,
        Refused = 3,
        Command = 4,
        Events = 5,
        Rejected = 6,
        SnapshotRequest = 7,
        Presence = 8,
        HostLeaving = 9,
    }

    public abstract class NetMessage
    {
        public abstract MessageType Type { get; }
    }

    /// <summary>Client → host: "I am here, send me the game." Sent on joining and on rejoining.</summary>
    public sealed class HelloMessage : NetMessage
    {
        public override MessageType Type => MessageType.Hello;
        public int ProtocolVersion;
        public ulong BoardChecksum;
    }

    /// <summary>Host → client: everything needed to show the game from now on.</summary>
    public sealed class GameStartMessage : NetMessage
    {
        public override MessageType Type => MessageType.GameStart;
        public int ProtocolVersion;
        public ulong BoardChecksum;
        public int YourPlayerId;

        /// <summary>Sequence number of the last event already contained in <see cref="Snapshot"/>.</summary>
        public int Sequence;

        /// <summary>The house rules the host set up; the client plays its board data with them.</summary>
        public Lazpoly.Core.Rules.TableRules Table = Lazpoly.Core.Rules.TableRules.Default;

        public GameStateSnapshot Snapshot;
    }

    public enum RefusalReason : byte
    {
        NotInThisGame = 1,
        VersionMismatch = 2,
        BoardMismatch = 3,
    }

    /// <summary>Host → client: the client cannot take part.</summary>
    public sealed class RefusedMessage : NetMessage
    {
        public override MessageType Type => MessageType.Refused;
        public RefusalReason Reason;
    }

    /// <summary>Client → host: a request to act. The host decides.</summary>
    public sealed class CommandMessage : NetMessage
    {
        public override MessageType Type => MessageType.Command;
        public GameCommand Command;
    }

    /// <summary>Host → all clients: the events of one accepted command, numbered, with the resulting state hash.</summary>
    public sealed class EventsMessage : NetMessage
    {
        public override MessageType Type => MessageType.Events;
        public int Sequence;
        public List<GameEvent> Events = new List<GameEvent>();
        public ulong StateHash;
    }

    /// <summary>Host → one client: its command was refused.</summary>
    public sealed class RejectedMessage : NetMessage
    {
        public override MessageType Type => MessageType.Rejected;
        public RejectionReason Reason;
    }

    /// <summary>Client → host: "my copy is out of step, send me the full state."</summary>
    public sealed class SnapshotRequestMessage : NetMessage
    {
        public override MessageType Type => MessageType.SnapshotRequest;
    }

    /// <summary>How one seat is doing on the network, as the host sees it.</summary>
    public readonly struct PlayerPresence
    {
        public PlayerPresence(int playerId, bool connected, bool aiPlaying, int pingMs)
        {
            PlayerId = playerId;
            Connected = connected;
            AiPlaying = aiPlaying;
            PingMs = pingMs;
        }

        public int PlayerId { get; }

        /// <summary>The player's machine is reachable right now.</summary>
        public bool Connected { get; }

        /// <summary>The computer moves for the player (dropped, never arrived, or idle).</summary>
        public bool AiPlaying { get; }

        /// <summary>Round-trip time between the host and the player's machine; -1 when unknown.</summary>
        public int PingMs { get; }
    }

    /// <summary>Host → all clients, every couple of seconds: who is connected, who the computer plays, and their latency.</summary>
    public sealed class PresenceMessage : NetMessage
    {
        public override MessageType Type => MessageType.Presence;
        public List<PlayerPresence> Players = new List<PlayerPresence>();
    }

    /// <summary>Host → all clients: the host is leaving on purpose, so the game ends now rather than after a grace time.</summary>
    public sealed class HostLeavingMessage : NetMessage
    {
        public override MessageType Type => MessageType.HostLeaving;
    }
}
