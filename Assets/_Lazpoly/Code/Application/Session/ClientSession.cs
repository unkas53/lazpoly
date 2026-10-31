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
    /// A player's view of an online game hosted elsewhere. Never decides anything: it sends
    /// commands to the host and mirrors the host's events, re-syncing from a snapshot whenever
    /// an event goes missing or its copy no longer matches the host's state hash.
    /// </summary>
    public sealed class ClientSession : IGameSession
    {
        private readonly ITransport _transport;
        private readonly ulong _hostPeer;
        private readonly ulong _boardChecksum;
        private int _lastSequence;
        private bool _awaitingSnapshot;
        private bool _ended;

        public ClientSession(ITransport transport, ulong hostPeer, BoardDefinition board, RulesConfig rules, ulong boardChecksum)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            Board = board ?? throw new ArgumentNullException(nameof(board));
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _hostPeer = hostPeer;
            _boardChecksum = boardChecksum;
            _transport.Received += OnReceived;
            _transport.PeerDisconnected += OnPeerDisconnected;
        }

        public BoardDefinition Board { get; }
        public RulesConfig Rules { get; }
        public GameState State { get; private set; }
        public bool IsReady => State != null;
        public int MyPlayerId { get; private set; } = -1;

        /// <summary>How many times this client found its copy out of step and re-synced.</summary>
        public int ResyncCount { get; private set; }

        public event Action Ready;
        public event Action StateReset;
        public event Action<SessionEndReason> Ended;
        public event Action<IReadOnlyList<GameEvent>> EventsPublished;
        public event Action<RejectionReason> CommandRejected;

        public bool CanControl(int playerId) => IsReady && playerId == MyPlayerId;

        /// <summary>Only the host knows who the computer plays.</summary>
        public bool IsAiPlaying(int playerId) => false;

        /// <summary>The host runs every AI move.</summary>
        public bool StepAi() => false;

        /// <summary>Introduces this client to the host, which answers with the game state.</summary>
        public void Start()
        {
            Send(new HelloMessage { ProtocolVersion = MessageCodec.ProtocolVersion, BoardChecksum = _boardChecksum });
        }

        public void Submit(GameCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (_ended || !IsReady)
            {
                CommandRejected?.Invoke(RejectionReason.NotConnected);
                return;
            }

            if (!CanControl(command.PlayerId))
            {
                CommandRejected?.Invoke(RejectionReason.NotYourPlayer);
                return;
            }

            if (!Send(new CommandMessage { Command = command })) CommandRejected?.Invoke(RejectionReason.NotConnected);
        }

        public void Tick() => _transport.Poll();

        private void OnReceived(ulong peer, byte[] payload)
        {
            if (peer != _hostPeer || _ended) return;

            NetMessage message;
            try
            {
                message = MessageCodec.Decode(payload);
            }
            catch (FormatException)
            {
                RequestSnapshot();
                return;
            }

            switch (message)
            {
                case GameStartMessage start:
                    ApplyGameStart(start);
                    break;
                case EventsMessage events:
                    ApplyEvents(events);
                    break;
                case RejectedMessage rejected:
                    CommandRejected?.Invoke(rejected.Reason);
                    break;
                case RefusedMessage refused:
                    End(refused.Reason == RefusalReason.VersionMismatch ? SessionEndReason.VersionMismatch
                        : refused.Reason == RefusalReason.BoardMismatch ? SessionEndReason.BoardMismatch
                        : SessionEndReason.NotInThisGame);
                    break;
            }
        }

        private void ApplyGameStart(GameStartMessage start)
        {
            if (start.ProtocolVersion != MessageCodec.ProtocolVersion)
            {
                End(SessionEndReason.VersionMismatch);
                return;
            }

            if (start.BoardChecksum != _boardChecksum)
            {
                End(SessionEndReason.BoardMismatch);
                return;
            }

            GameState restored;
            try
            {
                restored = start.Snapshot.Restore();
            }
            catch (ArgumentException)
            {
                RequestSnapshot();
                return;
            }

            if (restored.SquareCount != Board.Count || restored.FindPlayer(start.YourPlayerId) == null)
            {
                End(SessionEndReason.BoardMismatch);
                return;
            }

            bool first = State == null;
            State = restored;
            MyPlayerId = start.YourPlayerId;
            _lastSequence = start.Sequence;
            _awaitingSnapshot = false;

            if (first) Ready?.Invoke();
            else StateReset?.Invoke();
        }

        private void ApplyEvents(EventsMessage message)
        {
            if (!IsReady || _awaitingSnapshot) return;

            if (message.Sequence <= _lastSequence) return; // Already contained in our state.
            if (message.Sequence != _lastSequence + 1)
            {
                RequestSnapshot();
                return;
            }

            try
            {
                foreach (GameEvent gameEvent in message.Events) GameStateReducer.Apply(State, gameEvent);
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is ArgumentException ||
                                              exception is IndexOutOfRangeException)
            {
                RequestSnapshot();
                return;
            }

            _lastSequence = message.Sequence;
            EventsPublished?.Invoke(message.Events);

            if (State.ComputeHash() != message.StateHash) RequestSnapshot();
        }

        private void OnPeerDisconnected(ulong peer)
        {
            if (peer == _hostPeer) End(SessionEndReason.HostLeft);
        }

        private void RequestSnapshot()
        {
            if (_awaitingSnapshot) return;
            _awaitingSnapshot = true;
            ResyncCount++;
            Send(new SnapshotRequestMessage());
        }

        private void End(SessionEndReason reason)
        {
            if (_ended) return;
            _ended = true;
            Ended?.Invoke(reason);
        }

        private bool Send(NetMessage message) => _transport.Send(_hostPeer, MessageCodec.Encode(message));
    }
}
