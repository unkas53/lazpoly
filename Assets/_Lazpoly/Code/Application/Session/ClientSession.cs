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
    /// an event goes missing or its copy no longer matches the host's state hash. When the link to
    /// the host breaks it keeps knocking for <see cref="GiveUpSeconds"/> (a fresh Hello every
    /// <see cref="RetrySeconds"/>) before it gives the game up as lost; the host's answer puts the
    /// player back in their seat with the current state.
    /// </summary>
    public sealed class ClientSession : IGameSession
    {
        /// <summary>Seconds between two attempts to reach the host after the link broke.</summary>
        public const double RetrySeconds = 3;

        /// <summary>How long a client keeps trying before it reports the host gone.</summary>
        public const double GiveUpSeconds = 45;

        private readonly ITransport _transport;
        private readonly ulong _hostPeer;
        private readonly ulong _boardChecksum;
        private readonly RulesConfig _boardRules;
        private readonly Func<double> _now;
        private readonly Dictionary<int, PlayerPresence> _presence = new Dictionary<int, PlayerPresence>();
        private int _lastSequence;
        private bool _awaitingSnapshot;
        private bool _ended;
        private double _lostAt;
        private double _lastAttemptAt;

        /// <param name="now">Seconds from any fixed point, for the reconnect timing; null uses a monotonic system clock.</param>
        public ClientSession(ITransport transport, ulong hostPeer, BoardDefinition board, RulesConfig rules, ulong boardChecksum,
            Func<double> now = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            Board = board ?? throw new ArgumentNullException(nameof(board));
            _boardRules = rules ?? throw new ArgumentNullException(nameof(rules));
            Rules = rules;
            _hostPeer = hostPeer;
            _boardChecksum = boardChecksum;
            _now = now ?? TurnClock.SystemClock();
            _transport.Received += OnReceived;
            _transport.PeerDisconnected += OnPeerDisconnected;
        }

        public BoardDefinition Board { get; }
        /// <summary>The board data's rules with the host's table rules, once the game has started.</summary>
        public RulesConfig Rules { get; private set; }
        public GameState State { get; private set; }
        public bool IsReady => State != null;
        public int MyPlayerId { get; private set; } = -1;

        /// <summary>How many times this client found its copy out of step and re-synced.</summary>
        public int ResyncCount { get; private set; }

        /// <summary>True from the moment the link to the host broke until the host answers again (or the client gives up).</summary>
        public bool IsReconnecting { get; private set; }

        /// <summary>Hellos sent since the link broke; 0 when connected.</summary>
        public int ReconnectAttempts { get; private set; }

        /// <summary>Seconds left before this client gives the host up; <see cref="GiveUpSeconds"/> while connected.</summary>
        public double SecondsUntilGiveUp => IsReconnecting ? Math.Max(0, GiveUpSeconds - (_now() - _lostAt)) : GiveUpSeconds;

        public event Action Ready;
        public event Action StateReset;
        public event Action<SessionEndReason> Ended;
        public event Action<IReadOnlyList<GameEvent>> EventsPublished;
        public event Action<RejectionReason> CommandRejected;

        /// <summary>Raised with false when the link to the host breaks and with true when the host answers again.</summary>
        public event Action<bool> ConnectionChanged;

        public bool CanControl(int playerId) => IsReady && playerId == MyPlayerId;

        /// <summary>As the host last reported it; false until the first presence message.</summary>
        public bool IsAiPlaying(int playerId) => _presence.TryGetValue(playerId, out PlayerPresence p) && p.AiPlaying;

        /// <summary>This machine: whether the host is reachable. Others: as the host last reported, true until it has.</summary>
        public bool IsConnected(int playerId)
        {
            if (playerId == MyPlayerId) return !IsReconnecting && !_ended;
            return !_presence.TryGetValue(playerId, out PlayerPresence p) || p.Connected;
        }

        public int PingMs(int playerId)
        {
            if (playerId == MyPlayerId) return IsReconnecting || _ended ? -1 : _transport.PingMs(_hostPeer);
            return _presence.TryGetValue(playerId, out PlayerPresence p) ? p.PingMs : -1;
        }

        /// <summary>The host runs every AI move.</summary>
        public bool StepAi() => false;

        /// <summary>Introduces this client to the host, which answers with the game state.</summary>
        public void Start() => SendHello();

        /// <summary>
        /// Something outside the session (the room) knows the host is gone for good: stop now
        /// instead of waiting out the grace time.
        /// </summary>
        public void ReportHostGone() => End(SessionEndReason.HostLeft);

        public void Submit(GameCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (_ended || !IsReady || IsReconnecting)
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

        /// <summary>The host keeps the game clock; its time arrives with the events.</summary>
        public void AddPlayTime(double seconds)
        {
        }

        public void Tick()
        {
            _transport.Poll();
            if (!IsReconnecting || _ended) return;

            double now = _now();
            if (now - _lostAt >= GiveUpSeconds)
            {
                End(SessionEndReason.HostLeft);
                return;
            }

            if (now - _lastAttemptAt >= RetrySeconds)
            {
                ReconnectAttempts++;
                SendHello();
            }
        }

        private void SendHello()
        {
            _lastAttemptAt = _now();
            Send(new HelloMessage { ProtocolVersion = MessageCodec.ProtocolVersion, BoardChecksum = _boardChecksum });
        }

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
                case PresenceMessage presence:
                    _presence.Clear();
                    foreach (PlayerPresence p in presence.Players) _presence[p.PlayerId] = p;
                    break;
                case HostLeavingMessage _:
                    End(SessionEndReason.HostLeft);
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
            Rules = _boardRules.WithTable(start.Table ?? TableRules.Default);
            State = restored;
            MyPlayerId = start.YourPlayerId;
            _lastSequence = start.Sequence;
            _awaitingSnapshot = false;
            bool wasReconnecting = IsReconnecting;
            IsReconnecting = false;
            ReconnectAttempts = 0;

            if (first) Ready?.Invoke();
            else StateReset?.Invoke();
            if (wasReconnecting) ConnectionChanged?.Invoke(true);
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

        /// <summary>The link broke: keep the state on screen and knock on the host's door until it answers or the time is up.</summary>
        private void OnPeerDisconnected(ulong peer)
        {
            if (peer != _hostPeer || _ended || IsReconnecting) return;
            IsReconnecting = true;
            ReconnectAttempts = 0;
            _lostAt = _now();
            _lastAttemptAt = _lostAt;
            _awaitingSnapshot = false; // The answer to a Hello is a snapshot anyway.
            ConnectionChanged?.Invoke(false);
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
            IsReconnecting = false; // The knocking is over, one way or the other.
            Ended?.Invoke(reason);
        }

        private bool Send(NetMessage message) => _transport.Send(_hostPeer, MessageCodec.Encode(message));
    }
}
