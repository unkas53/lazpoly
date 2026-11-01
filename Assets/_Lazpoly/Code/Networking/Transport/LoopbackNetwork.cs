using System;
using System.Collections.Generic;

namespace Lazpoly.Net.Transport
{
    /// <summary>
    /// In-memory network for tests and single-machine runs: every transport created here can
    /// reach the others. Messages are delivered in order when the receiver polls.
    /// </summary>
    public sealed class LoopbackNetwork
    {
        private readonly Dictionary<ulong, LoopbackTransport> _peers = new Dictionary<ulong, LoopbackTransport>();
        private readonly Dictionary<ulong, LoopbackTransport> _severed = new Dictionary<ulong, LoopbackTransport>();

        /// <summary>The latency every transport reports for a reachable peer; tests set it to check it travels.</summary>
        public int PingMs { get; set; }

        public ITransport CreateTransport(ulong peer)
        {
            if (_peers.ContainsKey(peer)) throw new ArgumentException($"Peer {peer} already exists.", nameof(peer));
            var transport = new LoopbackTransport(this, peer);
            _peers.Add(peer, transport);
            return transport;
        }

        /// <summary>Simulates a peer dropping off: it stops receiving and everyone else is told.</summary>
        public void Disconnect(ulong peer)
        {
            if (!_peers.Remove(peer)) return;
            foreach (LoopbackTransport other in _peers.Values)
            {
                other.Enqueue(new Delivery(peer, null));
            }
        }

        /// <summary>Brings back a peer that was disconnected, with an empty inbox.</summary>
        public ITransport Reconnect(ulong peer)
        {
            Disconnect(peer);
            return CreateTransport(peer);
        }

        /// <summary>
        /// Simulates a peer's own link failing: everyone else is told it dropped, and from its side every
        /// other peer drops too. The peer keeps its transport, so <see cref="Restore"/> can plug it back in.
        /// </summary>
        public void Sever(ulong peer)
        {
            if (!_peers.TryGetValue(peer, out LoopbackTransport severed)) return;
            Disconnect(peer);
            foreach (ulong other in _peers.Keys) severed.Enqueue(new Delivery(other, null));
            _severed.Add(peer, severed);
        }

        /// <summary>Plugs a severed peer's link back in; it reaches the others again and they it.</summary>
        public void Restore(ulong peer)
        {
            if (!_severed.TryGetValue(peer, out LoopbackTransport severed)) throw new ArgumentException($"Peer {peer} was not severed.", nameof(peer));
            _severed.Remove(peer);
            _peers.Add(peer, severed);
        }

        private int PingBetween(ulong from, ulong to) => _peers.ContainsKey(from) && _peers.ContainsKey(to) && from != to ? PingMs : -1;

        private bool Deliver(ulong from, ulong to, byte[] payload)
        {
            if (!_peers.ContainsKey(from)) return false;
            if (!_peers.TryGetValue(to, out LoopbackTransport target)) return false;

            var copy = new byte[payload.Length];
            Buffer.BlockCopy(payload, 0, copy, 0, payload.Length);
            target.Enqueue(new Delivery(from, copy));
            return true;
        }

        private readonly struct Delivery
        {
            public Delivery(ulong from, byte[] payload)
            {
                From = from;
                Payload = payload;
            }

            public ulong From { get; }

            /// <summary>Null marks a disconnect notice.</summary>
            public byte[] Payload { get; }
        }

        private sealed class LoopbackTransport : ITransport
        {
            private readonly LoopbackNetwork _network;
            private readonly Queue<Delivery> _inbox = new Queue<Delivery>();

            public LoopbackTransport(LoopbackNetwork network, ulong peer)
            {
                _network = network;
                LocalPeer = peer;
            }

            public ulong LocalPeer { get; }
            public event Action<ulong, byte[]> Received;
            public event Action<ulong> PeerDisconnected;

            public bool Send(ulong peer, byte[] payload)
            {
                if (payload == null) throw new ArgumentNullException(nameof(payload));
                return _network.Deliver(LocalPeer, peer, payload);
            }

            public void Poll()
            {
                int count = _inbox.Count;
                for (int i = 0; i < count; i++)
                {
                    Delivery delivery = _inbox.Dequeue();
                    if (delivery.Payload == null) PeerDisconnected?.Invoke(delivery.From);
                    else Received?.Invoke(delivery.From, delivery.Payload);
                }
            }

            public int PingMs(ulong peer) => _network.PingBetween(LocalPeer, peer);

            internal void Enqueue(Delivery delivery) => _inbox.Enqueue(delivery);
        }
    }
}
