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
    /// When a player's connection drops, the computer plays for them until they come back. When a
    /// player lets their time run out (<see cref="TurnClock"/>), the host makes the most cautious move
    /// for them; after <see cref="StrikesBeforeStandIn"/> timeouts in a row the computer takes their
    /// seat until they act again.
    /// </summary>
    public sealed class HostSession : IGameSession
    {
        private readonly GameEngine _engine;
        private readonly ITransport _transport;
        private readonly int _localPlayerId;
        private readonly Dictionary<ulong, int> _remotePlayers;
        private readonly HashSet<ulong> _connected = new HashSet<ulong>();
        private readonly ulong _boardChecksum;
        private readonly AiRoster _standIns;
        private readonly TurnClock _clock;
        private readonly Dictionary<int, int> _strikes = new Dictionary<int, int>();
        private readonly HashSet<int> _idle = new HashSet<int>();
        private int _sequence;

        /// <summary>Timeouts in a row after which the computer plays for an idle player.</summary>
        public const int StrikesBeforeStandIn = 2;

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

        /// <summary>True while the computer plays for a player because they stopped answering (not because they dropped).</summary>
        public bool IsIdle(int playerId) => _idle.Contains(playerId);

        /// <summary>Seconds left before the host moves for the player the game waits on.</summary>
        public double SecondsLeft => _clock.Remaining(_engine.State.Phase);

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

            MarkActive(command.PlayerId);
            Broadcast(result.Events);
        }

        public void Tick()
        {
            _transport.Poll();
            CheckTimeouts();
        }

        /// <summary>Moves for every person the game has waited on for too long.</summary>
        private void CheckTimeouts()
        {
            GameState state = _engine.State;
            if (state.Phase == GamePhase.GameOver || !_clock.HasExpired(state.Phase)) return;

            var waiting = new List<int>();
            foreach (int playerId in AiTurnOrder.PlayersToAct(state))
            {
                if (!_standIns.Controls(playerId)) waiting.Add(playerId);
            }

            // In an auction several bidders may be idle; each still asked is passed in turn.
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

            GameCommand command = strikes < StrikesBeforeStandIn ? TimeoutPolicy.CommandFor(_engine.State, playerId) : null;
            CommandResult result = command != null ? _engine.Execute(command) : null;
            if (result != null && result.IsAccepted)
            {
                var events = new List<GameEvent> { new PlayerTimedOutEvent(playerId, standInTookOver: false) };
                events.AddRange(result.Events);
                Broadcast(events);
                return;
            }

            // No cautious move fits (an open debt) or the player keeps missing turns: the computer plays for them.
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
            }
        }

        private void MarkConnected(ulong peer, int playerId)
        {
            if (!_connected.Add(peer)) return;
            _standIns.Remove(playerId);
            RemotePlayerConnectionChanged?.Invoke(playerId, true);
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
