using System;
using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Events;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;
using Lazpoly.Net.Messages;
using Lazpoly.Net.Transport;

namespace Lazpoly.App.Session
{
    /// <summary>
    /// The authoritative side of an online game. Owns the engine, accepts commands only from the
    /// peer that owns the player, and broadcasts numbered events with the resulting state hash.
    /// </summary>
    public sealed class HostSession : IGameSession
    {
        private readonly GameEngine _engine;
        private readonly ITransport _transport;
        private readonly int _localPlayerId;
        private readonly Dictionary<ulong, int> _remotePlayers;
        private readonly HashSet<ulong> _connected = new HashSet<ulong>();
        private readonly ulong _boardChecksum;
        private int _sequence;

        /// <param name="remotePlayers">Network peer of every other seat, mapped to the player it controls.</param>
        public HostSession(GameEngine engine, ITransport transport, int localPlayerId,
            IReadOnlyDictionary<ulong, int> remotePlayers, ulong boardChecksum)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            if (engine.State.FindPlayer(localPlayerId) == null) throw new ArgumentException("Host player is not seated.", nameof(localPlayerId));

            _remotePlayers = new Dictionary<ulong, int>();
            foreach (KeyValuePair<ulong, int> pair in remotePlayers ?? throw new ArgumentNullException(nameof(remotePlayers)))
            {
                if (engine.State.FindPlayer(pair.Value) == null || pair.Value == localPlayerId)
                {
                    throw new ArgumentException($"Peer {pair.Key} is mapped to an invalid player {pair.Value}.", nameof(remotePlayers));
                }

                _remotePlayers.Add(pair.Key, pair.Value);
            }

            _localPlayerId = localPlayerId;
            _boardChecksum = boardChecksum;
            _transport.Received += OnReceived;
            _transport.PeerDisconnected += OnPeerDisconnected;
        }

        public BoardDefinition Board => _engine.Board;
        public RulesConfig Rules => _engine.Rules;
        public GameState State => _engine.State;
        public bool IsReady => true;

        public event Action Ready { add { } remove { } }
        public event Action StateReset { add { } remove { } }
        public event Action<SessionEndReason> Ended { add { } remove { } }
        public event Action<IReadOnlyList<GameEvent>> EventsPublished;
        public event Action<RejectionReason> CommandRejected;

        /// <summary>Raised when a remote player connects (true) or drops (false).</summary>
        public event Action<int, bool> RemotePlayerConnectionChanged;

        public bool IsConnected(int playerId)
        {
            if (playerId == _localPlayerId) return true;
            foreach (ulong peer in _connected)
            {
                if (_remotePlayers[peer] == playerId) return true;
            }

            return false;
        }

        public bool CanControl(int playerId) => playerId == _localPlayerId;

        /// <summary>Sends the starting state to every remote player.</summary>
        public void Start()
        {
            foreach (ulong peer in _remotePlayers.Keys) SendGameStart(peer);
        }

        public void Submit(GameCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (!CanControl(command.PlayerId))
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

            Broadcast(result.Events);
        }

        public void Tick() => _transport.Poll();

        private void OnReceived(ulong peer, byte[] payload)
        {
            NetMessage message;
            try
            {
                message = MessageCodec.Decode(payload);
            }
            catch (FormatException)
            {
                return; // Malformed data from a peer is dropped, never trusted.
            }

            if (!_remotePlayers.TryGetValue(peer, out int playerId))
            {
                Send(peer, new RefusedMessage { Reason = RefusalReason.NotInThisGame });
                return;
            }

            switch (message)
            {
                case HelloMessage hello:
                    if (hello.ProtocolVersion != MessageCodec.ProtocolVersion)
                    {
                        Send(peer, new RefusedMessage { Reason = RefusalReason.VersionMismatch });
                        return;
                    }

                    if (hello.BoardChecksum != _boardChecksum)
                    {
                        Send(peer, new RefusedMessage { Reason = RefusalReason.BoardMismatch });
                        return;
                    }

                    MarkConnected(peer, playerId);
                    SendGameStart(peer);
                    break;

                case CommandMessage command:
                    MarkConnected(peer, playerId);
                    if (command.Command.PlayerId != playerId)
                    {
                        Send(peer, new RejectedMessage { Reason = RejectionReason.NotYourPlayer });
                        return;
                    }

                    CommandResult result = _engine.Execute(command.Command);
                    if (!result.IsAccepted)
                    {
                        Send(peer, new RejectedMessage { Reason = result.Reason });
                        return;
                    }

                    Broadcast(result.Events);
                    break;

                case SnapshotRequestMessage _:
                    SendGameStart(peer);
                    break;
            }
        }

        private void OnPeerDisconnected(ulong peer)
        {
            if (_connected.Remove(peer) && _remotePlayers.TryGetValue(peer, out int playerId))
            {
                RemotePlayerConnectionChanged?.Invoke(playerId, false);
            }
        }

        private void MarkConnected(ulong peer, int playerId)
        {
            if (_connected.Add(peer)) RemotePlayerConnectionChanged?.Invoke(playerId, true);
        }

        private void Broadcast(IReadOnlyList<GameEvent> events)
        {
            _sequence++;
            var message = new EventsMessage { Sequence = _sequence, StateHash = _engine.State.ComputeHash() };
            message.Events.AddRange(events);
            byte[] payload = MessageCodec.Encode(message);
            foreach (ulong peer in _remotePlayers.Keys) _transport.Send(peer, payload);

            EventsPublished?.Invoke(events);
        }

        private void SendGameStart(ulong peer)
        {
            Send(peer, new GameStartMessage
            {
                ProtocolVersion = MessageCodec.ProtocolVersion,
                BoardChecksum = _boardChecksum,
                YourPlayerId = _remotePlayers[peer],
                Sequence = _sequence,
                Snapshot = GameStateSnapshot.Capture(_engine.State),
            });
        }

        private void Send(ulong peer, NetMessage message) => _transport.Send(peer, MessageCodec.Encode(message));
    }
}
