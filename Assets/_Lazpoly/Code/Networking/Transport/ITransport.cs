using System;

namespace Lazpoly.Net.Transport
{
    /// <summary>
    /// Reliable, ordered delivery of byte messages between peers identified by a 64-bit id
    /// (the Steam id in production). Implemented by Steam and by an in-memory loopback for tests.
    /// </summary>
    public interface ITransport
    {
        ulong LocalPeer { get; }

        /// <summary>Raised from <see cref="Poll"/> for each message that arrived.</summary>
        event Action<ulong, byte[]> Received;

        /// <summary>Raised from <see cref="Poll"/> when a peer can no longer be reached.</summary>
        event Action<ulong> PeerDisconnected;

        /// <summary>Queues a message; returns false if it could not be handed to the network.</summary>
        bool Send(ulong peer, byte[] payload);

        /// <summary>Delivers everything that arrived since the last call. Call once per frame.</summary>
        void Poll();

        /// <summary>Round-trip time to a peer in milliseconds, or -1 when there is no live session with it.</summary>
        int PingMs(ulong peer);
    }
}
