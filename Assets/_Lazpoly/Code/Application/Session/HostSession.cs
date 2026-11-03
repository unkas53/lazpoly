using System;
using System.Collections.Generic;
using Lazpoly.App.AI;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Events;
using Lazpoly.Core.Random;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;
using Lazpoly.Net.Messages;
using Lazpoly.Net.Transport;

namespace Lazpoly.App.Session
{
    /// <summary>
    /// The authoritative side of an online game. Owns the engine, accepts commands only from the
    /// peer that owns the player, and broadcasts numbered events with the resulting state hash.
    /// When a player's connection drops, or they never arrive within <see cref="NoShowSeconds"/> of the
    /// start, the computer plays for them until they come (back). When a player lets their time run out
    /// (<see cref="TurnClock"/>, plus <see cref="GraceSeconds"/> while their screen still plays the last
    /// move), the host decides for them as <see cref="TimeoutPolicy"/> says; after
    /// <see cref="StrikesBeforeStandIn"/> timeouts in a row the computer takes their seat until they act
    /// again. Every <see cref="PresenceSeconds"/> the host tells the clients who is connected, who the
    /// computer plays and everyone's latency.
    /// </summary>
    public sealed class HostSession : IGameSession
    {
        private readonly GameEngine _engine;
        private readonly ITransport _transport;
        private readonly int _localPlayerId;
        private readonly Dictionary<ulong, int> _remotePlayers;
        private readonly HashSet<ulong> _connected = new HashSet<ulong>();
        private readonly HashSet<ulong> _everConnected = new HashSet<ulong>();
        private readonly ulong _boardChecksum;
        private readonly AiRoster _standIns;
        private readonly TurnClock _clock;
        private readonly Dictionary<int, int> _strikes = new Dictionary<int, int>();
        private readonly HashSet<int> _idle = new HashSet<int>();
        private int _sequence;
        private double _startedAt;
        private bool _started;
        private bool _noShowsSeated;
        private double _nextPresenceAt;
        private bool _left;

        /// <summary>
        /// The clock starts when the host accepts a move, but a guest can act only once their screen has
        /// played it (dice, the pawn's walk); this much longer is waited before deciding for them.
        /// </summary>
        public const double GraceSeconds = 6;

        /// <summary>Timeouts in a row after which the computer plays for an idle player.</summary>
        public const int StrikesBeforeStandIn = 2;

        /// <summary>How long after the start a seat waits for its player before the computer takes it.</summary>
        public const double NoShowSeconds = 30;

        /// <summary>How often the host tells the clients about everyone's connection.</summary>
        public const double PresenceSeconds = 2;

        /// <summary>Difficulty of the computer player that stands in for a dropped player.</summary>
        public const AiDifficulty StandInDifficulty = AiDifficulty.Normal;

        /// <param name="remotePlayers">Network peer of every other seat, mapped to the player it controls.</param>
        public HostSession(GameEngine engine, ITransport transport, int localPlayerId,
            IReadOnlyDictionary<ulong, int> remotePlayers, ulong boardChecksum, ulong standInSeed = 1UL, TurnClock clock = null)
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
            _standIns = new AiRoster(new Pcg32Random(standInSeed, stream: 91UL));
            _clock = clock ?? new TurnClock();
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

        public bool IsAiPlaying(int playerId) => _standIns.Controls(playerId);

        public int PingMs(int playerId)
        {
            if (playerId == _localPlayerId) return 0;
            foreach (ulong peer in _connected)
            {
                if (_remotePlayers[peer] == playerId) return _transport.PingMs(peer);
            }

            return -1;
        }

        /// <summary>Everyone's connection as the host sees it, in seat order.</summary>
        public List<PlayerPresence> Presence()
        {
            var presence = new List<PlayerPresence>();
            foreach (PlayerState player in _engine.State.Players)
            {
                presence.Add(new PlayerPresence(player.Id, IsConnected(player.Id), IsAiPlaying(player.Id), PingMs(player.Id)));
            }

            return presence;
        }

        /// <summary>True while the computer plays for a player because they stopped answering (not because they dropped).</summary>
        public bool IsIdle(int playerId) => _idle.Contains(playerId);

        /// <summary>Seconds left before the host moves for the player the game waits on; infinite at a table without a clock.</summary>
        public double SecondsLeft => _clock.Remaining(_engine.State.Phase, _engine.Rules.TurnSeconds);

        public bool StepAi()
        {
            GameCommand command = _standIns.NextCommand(_engine.Board, _engine.Rules, _engine.State);
            if (command == null) return false;

            CommandResult result = _engine.Execute(command);
            if (!result.IsAccepted)
            {
                // The AI only picks commands the rules allow, so this is a bug, not a game situation.
                throw new InvalidOperationException($"AI chose a rejected {command.GetType().Name} ({result.Reason}).");
            }

            Broadcast(result.Events);
            return true;
        }

        /// <summary>Sends the starting state to every remote player and starts the wait for latecomers.</summary>
        public void Start()
        {
            _started = true;
            _startedAt = _clock.Now;
            _nextPresenceAt = _startedAt + PresenceSeconds;
            foreach (ulong peer in _remotePlayers.Keys) SendGameStart(peer);
        }

        /// <summary>
        /// Tells every client the host is leaving on purpose, so they stop at once instead of waiting
        /// out their grace time. Safe to call more than once.
        /// </summary>
        public void Leave()
        {
            if (_left) return;
            _left = true;
            byte[] payload = MessageCodec.Encode(new HostLeavingMessage());
            foreach (ulong peer in _remotePlayers.Keys) _transport.Send(peer, payload);
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

            MarkActive(command.PlayerId);
            Broadcast(result.Events);
        }

        public void Tick()
        {
            _transport.Poll();
            SeatNoShows();
            CheckTimeouts();
            if (_started && _clock.Now >= _nextPresenceAt) BroadcastPresence();
        }

        /// <summary>Once the grace time after the start is over, the computer takes every seat whose player never arrived.</summary>
        private void SeatNoShows()
        {
            if (!_started || _noShowsSeated || _clock.Now - _startedAt < NoShowSeconds) return;
            _noShowsSeated = true;
            bool changed = false;
            foreach (KeyValuePair<ulong, int> pair in _remotePlayers)
            {
                if (_everConnected.Contains(pair.Key) || _standIns.Controls(pair.Value)) continue;
                PlayerState player = _engine.State.FindPlayer(pair.Value);
                if (player == null || player.IsBankrupt) continue;
                _standIns.Add(pair.Value, StandInDifficulty);
                changed = true;
            }

            if (changed) BroadcastPresence();
        }

        /// <summary>Moves for every person the game has waited on for too long.</summary>
        private void CheckTimeouts()
        {
            GameState state = _engine.State;
            if (state.Phase == GamePhase.GameOver || !_clock.HasExpired(state.Phase, _engine.Rules.TurnSeconds, GraceSeconds)) return;

            var waiting = new List<int>();
            foreach (int playerId in AiTurnOrder.PlayersToAct(state))
            {
                if (!_standIns.Controls(playerId)) waiting.Add(playerId);
            }

            // Everyone the game still waits on is moved for in turn.
            foreach (int playerId in waiting)
            {
                if (_engine.State.Phase == GamePhase.GameOver) break;
                if (!new List<int>(AiTurnOrder.PlayersToAct(_engine.State)).Contains(playerId)) continue;
                TimeOut(playerId);
            }

            _clock.Restart();
        }

        private void TimeOut(int playerId)
        {
            int strikes = (_strikes.TryGetValue(playerId, out int before) ? before : 0) + 1;
            _strikes[playerId] = strikes;

            IReadOnlyList<GameCommand> commands = strikes < StrikesBeforeStandIn
                ? TimeoutPolicy.CommandsFor(_engine.Board, _engine.Rules, _engine.State, playerId)
                : new GameCommand[0];
            var events = new List<GameEvent> { new PlayerTimedOutEvent(playerId, standInTookOver: false) };
            foreach (GameCommand command in commands)
            {
                CommandResult result = _engine.Execute(command);
                if (!result.IsAccepted) break;
                events.AddRange(result.Events);
            }

            if (events.Count > 1)
            {
                Broadcast(events);
                return;
            }

            // Nothing could be done for them or the player keeps missing turns: the computer plays for them.
            _idle.Add(playerId);
            if (!_standIns.Controls(playerId)) _standIns.Add(playerId, StandInDifficulty);
            Broadcast(new GameEvent[] { new PlayerTimedOutEvent(playerId, standInTookOver: true) });
        }

        /// <summary>A player made a move themselves: their timeouts are forgiven and an idle stand-in steps back.</summary>
        private void MarkActive(int playerId)
        {
            _strikes.Remove(playerId);
            if (_idle.Remove(playerId)) _standIns.Remove(playerId);
        }

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

                    if (_idle.Remove(playerId)) _standIns.Remove(playerId);

                    CommandResult result = _engine.Execute(command.Command);
                    if (!result.IsAccepted)
                    {
                        Send(peer, new RejectedMessage { Reason = result.Reason });
                        return;
                    }

                    MarkActive(playerId);
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
                _idle.Remove(playerId); // Now a stand-in for a dropped player, which only a reconnect ends.
                PlayerState player = _engine.State.FindPlayer(playerId);
                if (player != null && !player.IsBankrupt && !_standIns.Controls(playerId)) _standIns.Add(playerId, StandInDifficulty);
                RemotePlayerConnectionChanged?.Invoke(playerId, false);
                BroadcastPresence();
            }
        }

        private void MarkConnected(ulong peer, int playerId)
        {
            if (!_connected.Add(peer)) return;
            _everConnected.Add(peer);
            _standIns.Remove(playerId);
            RemotePlayerConnectionChanged?.Invoke(playerId, true);
            BroadcastPresence();
        }

        private void BroadcastPresence()
        {
            _nextPresenceAt = _clock.Now + PresenceSeconds;
            var message = new PresenceMessage();
            message.Players.AddRange(Presence());
            byte[] payload = MessageCodec.Encode(message);
            foreach (ulong peer in _connected) _transport.Send(peer, payload);
        }

        private void Broadcast(IReadOnlyList<GameEvent> events)
        {
            _clock.Restart();
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
                Table = _engine.Rules.Table,
                Snapshot = GameStateSnapshot.Capture(_engine.State),
            });
        }

        private void Send(ulong peer, NetMessage message) => _transport.Send(peer, MessageCodec.Encode(message));
    }
}
