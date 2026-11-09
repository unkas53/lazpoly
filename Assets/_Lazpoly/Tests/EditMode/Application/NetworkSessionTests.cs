using System.Collections.Generic;
using Lazpoly.App.Session;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Events;
using Lazpoly.Core.State;
using Lazpoly.Net.Messages;
using Lazpoly.Net.Transport;
using NUnit.Framework;

namespace Lazpoly.App.Tests
{
    public class NetworkSessionTests
    {
        private static void AssertAllInStep(NetTable table)
        {
            ulong hostHash = table.HostState.ComputeHash();
            foreach (KeyValuePair<int, ClientSession> pair in table.Clients)
            {
                Assert.IsTrue(pair.Value.IsReady, $"Client of player {pair.Key} has no state.");
                Assert.AreEqual(hostHash, pair.Value.State.ComputeHash(), $"Client of player {pair.Key} drifted.");
            }
        }

        [Test]
        public void EightPlayersStayInStepThroughALongGame()
        {
            NetTable table = NetTable.Create(8, seed: 42UL);
            var published = new Dictionary<int, int>();
            foreach (KeyValuePair<int, ClientSession> pair in table.Clients)
            {
                int player = pair.Key;
                published[player] = 0;
                pair.Value.EventsPublished += _ => published[player]++;
            }

            table.StartAll();
            AssertAllInStep(table);

            int steps = 0;
            while (steps < 600 && table.Step())
            {
                steps++;
                AssertAllInStep(table);
            }

            AssertAllInStep(table);
            Assert.That(table.HostState.TurnNumber, Is.GreaterThan(16), "The game hardly moved.");
            foreach (KeyValuePair<int, ClientSession> pair in table.Clients)
            {
                Assert.AreEqual(0, pair.Value.ResyncCount, $"Client of player {pair.Key} needed a resync on a clean network.");
                Assert.That(published[pair.Key], Is.GreaterThan(0));
                Assert.AreEqual(pair.Key, pair.Value.MyPlayerId);
            }
        }

        /// <summary>Lets the host act until <paramref name="playerId"/> must roll.</summary>
        private static void PlayUntilTurnOf(NetTable table, int playerId)
        {
            for (int i = 0; i < 200 && !(table.HostState.CurrentPlayer.Id == playerId && table.HostState.Phase == GamePhase.WaitingForRoll); i++)
            {
                Assert.IsTrue(table.Step(), "The game ended before the player's turn.");
            }

            Assert.AreEqual(playerId, table.HostState.CurrentPlayer.Id);
        }

        [Test]
        public void AnIdlePlayerGetsACautiousMoveThenTheComputerTakesTheirSeat()
        {
            NetTable table = NetTable.Create(2);
            table.StartAll();
            PlayUntilTurnOf(table, 2);
            var notices = new List<PlayerTimedOutEvent>();
            int rolls = 0;
            table.Clients[2].EventsPublished += events =>
            {
                foreach (GameEvent gameEvent in events)
                {
                    if (gameEvent is PlayerTimedOutEvent notice) notices.Add(notice);
                    if (gameEvent is DiceRolledEvent) rolls++;
                }
            };

            table.Now += Deadline(GamePhase.WaitingForRoll) - 1;
            table.Pump();
            Assert.AreEqual(0, rolls, "The host moved before the time and the grace were up.");

            table.Now += 2;
            table.Pump();
            Assert.AreEqual(1, notices.Count);
            Assert.IsFalse(notices[0].StandInTookOver);
            Assert.AreEqual(1, rolls, "The host should have rolled for the idle player.");
            Assert.IsFalse(table.Host.IsAiPlaying(2));
            Assert.AreEqual(table.HostState.ComputeHash(), table.Clients[2].State.ComputeHash());

            table.Now += LongestDeadline + 1;
            table.Pump();
            Assert.AreEqual(2, notices.Count);
            Assert.IsTrue(notices[1].StandInTookOver);
            Assert.IsTrue(table.Host.IsAiPlaying(2));
            Assert.IsTrue(table.Host.IsIdle(2));

            // The stand-in moves when the game asks the computer; one move of their own gives the seat back.
            GameCommand own = table.NextCommand();
            Assert.AreEqual(2, own.PlayerId);
            table.Clients[2].Submit(own);
            table.Pump();
            Assert.IsFalse(table.Host.IsAiPlaying(2));
            Assert.IsFalse(table.Host.IsIdle(2));
        }

        [Test]
        public void MovingInTimeForgivesAnEarlierTimeout()
        {
            NetTable table = NetTable.Create(2);
            table.StartAll();
            PlayUntilTurnOf(table, 2);

            table.Now += LongestDeadline + 1;
            table.Pump();
            table.Clients[2].Submit(table.NextCommand());
            table.Pump();

            table.Now += LongestDeadline + 1;
            table.Pump();
            if (table.HostState.Phase == GamePhase.GameOver) return;
            Assert.IsFalse(table.Host.IsAiPlaying(table.HostState.CurrentPlayer.Id), "A single timeout after a move of one's own should not hand the seat over.");
        }

        /// <summary>When the host acts for an idle person in a phase: the decision time plus the grace for animations.</summary>
        private static double Deadline(GamePhase phase) => TurnClock.LimitFor(phase) + HostSession.GraceSeconds;

        /// <summary>A wait past every phase's deadline, so the host has surely acted.</summary>
        private static double LongestDeadline => Deadline(GamePhase.AwaitingDebtPayment);

        [Test]
        public void SellingAndAnsweringGetTwiceTheDecisionTime()
        {
            Assert.AreEqual(TurnClock.DecisionSeconds, TurnClock.LimitFor(GamePhase.WaitingForRoll));
            Assert.AreEqual(TurnClock.DecisionSeconds, TurnClock.LimitFor(GamePhase.AwaitingBuyDecision));
            Assert.AreEqual(TurnClock.DecisionSeconds, TurnClock.LimitFor(GamePhase.TurnEnd));
            Assert.AreEqual(TurnClock.DecisionSeconds * TurnClock.LongDecisionFactor, TurnClock.LimitFor(GamePhase.AwaitingDebtPayment));
            Assert.AreEqual(TurnClock.DecisionSeconds * TurnClock.LongDecisionFactor, TurnClock.LimitFor(GamePhase.AwaitingTradeResponse));

            double now = 0;
            var clock = new TurnClock(() => now);
            now = TurnClock.DecisionSeconds + 1;
            Assert.IsTrue(clock.HasExpired(GamePhase.WaitingForRoll));
            Assert.IsFalse(clock.HasExpired(GamePhase.WaitingForRoll, Lazpoly.Core.Rules.TableRules.DefaultTurnSeconds, grace: 2), "The grace holds the clock a little longer.");
            Assert.IsFalse(clock.HasExpired(GamePhase.AwaitingDebtPayment));
            clock.Restart();
            Assert.AreEqual(TurnClock.DecisionSeconds, clock.Remaining(GamePhase.TurnEnd), 1e-9);
        }

        [Test]
        public void TheTablePicksTheDecisionTimeAndMayRunNoClockAtAll()
        {
            Assert.AreEqual(10, TurnClock.LimitFor(GamePhase.WaitingForRoll, 10));
            Assert.AreEqual(20, TurnClock.LimitFor(GamePhase.AwaitingDebtPayment, 10));
            Assert.AreEqual(TurnClock.MaxDecisionSeconds, TurnClock.LimitFor(GamePhase.WaitingForRoll, 90), "Older tables stored a whole move's time; it is capped.");
            Assert.IsTrue(double.IsPositiveInfinity(TurnClock.LimitFor(GamePhase.WaitingForRoll, 0)));
            Assert.IsTrue(double.IsPositiveInfinity(TurnClock.LimitFor(GamePhase.AwaitingTradeResponse, 0)));

            double now = 0;
            var clock = new TurnClock(() => now);
            now = 1e6;
            Assert.IsFalse(clock.HasExpired(GamePhase.WaitingForRoll, 0), "No clock never runs out.");
            Assert.IsTrue(clock.HasExpired(GamePhase.WaitingForRoll, 10));
        }

        [Test]
        public void AHostAtATableWithoutAClockNeverMovesForAnyone()
        {
            NetTable table = NetTable.Create(2, table: new Lazpoly.Core.Rules.TableRules(0, 0, turnSeconds: 0));
            table.StartAll();
            PlayUntilTurnOf(table, 2);
            Assert.IsTrue(double.IsPositiveInfinity(table.Host.SecondsLeft));

            table.Now += 24 * 3600;
            table.Pump();
            Assert.AreEqual(0, table.HostState.LastDie1, "Nobody rolled for the idle player.");
            Assert.IsFalse(table.Host.IsAiPlaying(2));
            Assert.AreEqual(0, table.Clients[2].Rules.TurnSeconds, "The guest learns the table's clock from the host.");
        }

        [Test]
        public void AShortTurnTimeRunsOutSooner()
        {
            NetTable table = NetTable.Create(2, table: new Lazpoly.Core.Rules.TableRules(0, 0, turnSeconds: 10));
            table.StartAll();
            PlayUntilTurnOf(table, 2);
            int rolls = 0;
            table.Clients[2].EventsPublished += events =>
            {
                foreach (GameEvent gameEvent in events) if (gameEvent is DiceRolledEvent) rolls++;
            };

            table.Now += 10 + HostSession.GraceSeconds - 1;
            table.Pump();
            Assert.AreEqual(0, rolls);
            table.Now += 2;
            table.Pump();
            Assert.AreEqual(1, rolls, "Past ten seconds and the grace the host rolled for the idle player.");
        }

        [Test]
        public void TheCautiousMoveCommitsToNothing()
        {
            Assert.IsInstanceOf<DeclinePurchaseCommand>(TimeoutPolicy.CommandFor(StateIn(GamePhase.AwaitingBuyDecision), 1));
            Assert.IsInstanceOf<RejectTradeCommand>(TimeoutPolicy.CommandFor(StateIn(GamePhase.AwaitingTradeResponse), 1));
            Assert.IsInstanceOf<DeclineTravelCommand>(TimeoutPolicy.CommandFor(StateIn(GamePhase.AwaitingTravelDecision), 1));
            Assert.IsInstanceOf<DeclineCupCommand>(TimeoutPolicy.CommandFor(StateIn(GamePhase.AwaitingCupChoice), 1), "Time running out on the cup spends nothing.");
            Assert.IsInstanceOf<RollInJailCommand>(TimeoutPolicy.CommandFor(StateIn(GamePhase.AwaitingJailDecision), 1), "Time running out in jail spends nothing.");
            Assert.IsInstanceOf<DeclineTakeoverCommand>(TimeoutPolicy.CommandFor(StateIn(GamePhase.AwaitingTakeoverDecision), 1), "Time running out takes over nothing.");
            Assert.IsInstanceOf<EndTurnCommand>(TimeoutPolicy.CommandFor(StateIn(GamePhase.TurnEnd), 1), "Nothing is built when time runs out.");
            Assert.IsNull(TimeoutPolicy.CommandFor(StateIn(GamePhase.AwaitingDebtPayment), 1), "A debt takes the automatic sale, not one move.");
            GameState noDebt = StateIn(GamePhase.AwaitingDebtPayment);
            Assert.AreEqual(0, TimeoutPolicy.CommandsFor(NetTestData.Board(), NetTestData.Rules(), noDebt, 1).Count, "Without a debt there is nothing to sell.");
        }

        private static GameState StateIn(GamePhase phase)
        {
            GameState state = GameState.CreateInitial(NetTestData.Board(), NetTestData.Rules(), new[] { new PlayerSeat(1, "A"), new PlayerSeat(2, "B") });
            GameStateReducer.Apply(state, new PhaseChangedEvent(phase));
            return state;
        }

        [Test]
        public void ClientsPlayWithTheHostsTableRules()
        {
            NetTable table = NetTable.Create(3, table: new Lazpoly.Core.Rules.TableRules(1600, 3, 60));
            Assert.AreEqual(800, table.Clients[2].Rules.StartingMoney, "Before the game starts a client only has the board data.");
            table.StartAll();

            foreach (ClientSession client in table.Clients.Values)
            {
                Assert.AreEqual(1600, client.Rules.StartingMoney);
                Assert.AreEqual(60, client.Rules.TurnSeconds);
                Assert.AreEqual(3, client.Rules.RoundLimit);
                Assert.AreEqual(1600, client.State.Players[0].Money);
            }

            int steps = 0;
            while (steps < 400 && table.Step()) steps++;
            Assert.AreEqual(GamePhase.GameOver, table.HostState.Phase, "Three rounds should end the game.");
            AssertAllInStep(table);
        }

        [Test]
        public void EachMachineControlsOnlyItsOwnPlayer()
        {
            NetTable table = NetTable.Create(3);
            table.StartAll();

            Assert.IsTrue(table.Host.CanControl(1));
            Assert.IsFalse(table.Host.CanControl(2));
            Assert.IsTrue(table.Clients[2].CanControl(2));
            Assert.IsFalse(table.Clients[2].CanControl(1));
            Assert.IsFalse(table.Clients[2].CanControl(3));
        }

        [Test]
        public void ClientCannotActForAnotherPlayerEvenWithAForgedMessage()
        {
            NetTable table = NetTable.Create(3);
            table.StartAll();
            var rejections = new List<RejectionReason>();
            table.Clients[2].CommandRejected += rejections.Add;
            ulong before = table.HostState.ComputeHash();

            // The session itself refuses...
            table.Clients[2].Submit(new RollDiceCommand(1));
            // ...and a hand-made message that skips the session is refused by the host.
            table.ClientTransports[2].Send(NetTable.HostPeer,
                MessageCodec.Encode(new CommandMessage { Command = new RollDiceCommand(1) }));
            table.Pump();

            CollectionAssert.AreEqual(new[] { RejectionReason.NotYourPlayer, RejectionReason.NotYourPlayer }, rejections);
            Assert.AreEqual(before, table.HostState.ComputeHash());
        }

        [Test]
        public void HostRefusesARemoteCommandOutOfTurn()
        {
            NetTable table = NetTable.Create(3);
            table.StartAll();
            var rejections = new List<RejectionReason>();
            table.Clients[3].CommandRejected += rejections.Add;

            table.Clients[3].Submit(new RollDiceCommand(3));
            table.Pump();

            CollectionAssert.AreEqual(new[] { RejectionReason.NotYourTurn }, rejections);
            Assert.AreEqual(0, table.HostState.LastDie1);
        }

        [Test]
        public void HostCannotActForARemotePlayer()
        {
            NetTable table = NetTable.Create(2);
            table.StartAll();
            while (table.HostState.CurrentPlayer.Id == 1 && table.Step())
            {
            }

            Assert.AreEqual(2, table.HostState.CurrentPlayer.Id);

            var rejections = new List<RejectionReason>();
            table.Host.CommandRejected += rejections.Add;
            table.Host.Submit(new RollDiceCommand(2));

            CollectionAssert.AreEqual(new[] { RejectionReason.NotYourPlayer }, rejections);
        }

        [Test]
        public void StrangerIsRefused()
        {
            NetTable table = NetTable.Create(2);
            table.StartAll();
            ITransport strangerTransport = table.Network.CreateTransport(999);
            var stranger = new ClientSession(strangerTransport, NetTable.HostPeer, table.Board, table.Rules, table.Checksum);
            var ended = new List<SessionEndReason>();
            stranger.Ended += ended.Add;

            stranger.Start();
            for (int i = 0; i < 4; i++)
            {
                table.Pump(1);
                stranger.Tick();
            }

            CollectionAssert.AreEqual(new[] { SessionEndReason.NotInThisGame }, ended);
            Assert.IsFalse(stranger.IsReady);
        }

        [Test]
        public void ClientWithDifferentBoardDataIsRefused()
        {
            NetTable table = NetTable.Create(2);
            var ended = new List<SessionEndReason>();

            // Replace player 2's machine with one whose data does not match the host's.
            table.Network.Disconnect(NetTable.PeerOf(2));
            ITransport transport = table.Network.CreateTransport(NetTable.PeerOf(2));
            var client = new ClientSession(transport, NetTable.HostPeer, table.Board, table.Rules, table.Checksum + 1);
            client.Ended += ended.Add;
            table.Clients[2] = client;

            table.StartAll();

            CollectionAssert.AreEqual(new[] { SessionEndReason.BoardMismatch }, ended);
            Assert.IsFalse(client.IsReady);
        }

        [Test]
        public void OlderProtocolIsRefused()
        {
            NetTable table = NetTable.Create(2);
            ITransport transport = table.ClientTransports[2];
            var answers = new List<NetMessage>();
            transport.Received += (_, data) => answers.Add(MessageCodec.Decode(data));

            transport.Send(NetTable.HostPeer, MessageCodec.Encode(new HelloMessage
            {
                ProtocolVersion = MessageCodec.ProtocolVersion + 1,
                BoardChecksum = table.Checksum,
            }));
            table.Host.Tick();
            transport.Poll();

            Assert.IsTrue(answers.Exists(m => m is RefusedMessage r && r.Reason == RefusalReason.VersionMismatch));
            Assert.IsFalse(table.Host.IsConnected(2));
        }

        [Test]
        public void MalformedDataDoesNotDisturbTheGame()
        {
            NetTable table = NetTable.Create(3);
            table.StartAll();

            table.ClientTransports[2].Send(NetTable.HostPeer, new byte[] { 0xFF, 0x01, 0x02 });
            table.ClientTransports[3].Send(NetTable.HostPeer, new byte[] { (byte)MessageType.Command });
            table.ClientTransports[3].Send(NetTable.HostPeer, new byte[] { (byte)MessageType.Events, 1, 0 });
            table.Pump();

            for (int i = 0; i < 30 && table.Step(); i++)
            {
            }

            AssertAllInStep(table);
        }

        [Test]
        public void LostEventsAreRecoveredFromASnapshot()
        {
            LossyTransport lossy = null;
            NetTable table = NetTable.Create(3, wrapClient: (peer, transport) =>
            {
                if (peer != NetTable.PeerOf(2)) return transport;
                lossy = new LossyTransport(transport);
                return lossy;
            });
            table.StartAll();
            int resets = 0;
            table.Clients[2].StateReset += () => resets++;

            int toDrop = 1;
            lossy.DropIncoming = data => data[0] == (byte)MessageType.Events && toDrop-- > 0;
            for (int i = 0; i < 12 && table.Step(); i++)
            {
            }

            AssertAllInStep(table);
            Assert.AreEqual(1, table.Clients[2].ResyncCount);
            Assert.AreEqual(1, resets);
            Assert.AreEqual(0, table.Clients[3].ResyncCount);
        }

        [Test]
        public void DisconnectedPlayerCanRejoinAndCatchUp()
        {
            NetTable table = NetTable.Create(3);
            var changes = new List<(int Player, bool Connected)>();
            table.Host.RemotePlayerConnectionChanged += (player, connected) => changes.Add((player, connected));
            table.StartAll();
            for (int i = 0; i < 10 && table.Step(); i++)
            {
            }

            ulong peer = NetTable.PeerOf(3);
            table.Network.Disconnect(peer);
            table.Pump();
            Assert.IsFalse(table.Host.IsConnected(3));

            // Play on while player 3 is away, as long as it is not their turn.
            for (int i = 0; i < 20 && table.HostState.CurrentPlayer.Id != 3; i++)
            {
                GameCommand command = table.NextCommand();
                table.SessionOf(command.PlayerId).Submit(command);
                table.Host.Tick();
                table.Clients[2].Tick();
                table.Host.Tick();
            }

            ITransport transport = table.Network.CreateTransport(peer);
            var rejoined = new ClientSession(transport, NetTable.HostPeer, table.Board, table.Rules, table.Checksum);
            table.Clients[3] = rejoined;
            table.ClientTransports[3] = transport;
            rejoined.Start();
            table.Pump();

            Assert.IsTrue(rejoined.IsReady);
            Assert.AreEqual(3, rejoined.MyPlayerId);
            Assert.IsTrue(table.Host.IsConnected(3));
            Assert.Contains((3, false), changes);
            Assert.AreEqual((3, true), changes[changes.Count - 1]);

            for (int i = 0; i < 20 && table.Step(); i++)
            {
            }

            AssertAllInStep(table);
        }

        [Test]
        public void ClientsKeepKnockingAfterTheHostDropsAndGiveUpAfterTheGraceTime()
        {
            NetTable table = NetTable.Create(3);
            table.StartAll();
            var ended = new List<SessionEndReason>();
            var rejections = new List<RejectionReason>();
            var links = new List<bool>();
            foreach (ClientSession client in table.Clients.Values)
            {
                client.Ended += ended.Add;
                client.CommandRejected += rejections.Add;
            }

            table.Clients[2].ConnectionChanged += links.Add;

            table.Network.Disconnect(NetTable.HostPeer);
            foreach (ClientSession client in table.Clients.Values) client.Tick();
            table.Clients[2].Submit(new RollDiceCommand(2));

            Assert.IsEmpty(ended, "The board should stay up while the client tries to get back.");
            Assert.IsTrue(table.Clients[2].IsReconnecting);
            Assert.IsFalse(table.Clients[2].IsConnected(2));
            Assert.IsTrue(table.Clients[2].IsReady, "The last known state stays on screen.");
            CollectionAssert.AreEqual(new[] { false }, links);
            CollectionAssert.AreEqual(new[] { RejectionReason.NotConnected }, rejections);

            for (int i = 0; i < 2; i++)
            {
                table.Now += ClientSession.RetrySeconds + 0.1;
                foreach (ClientSession client in table.Clients.Values) client.Tick();
            }

            Assert.AreEqual(2, table.Clients[2].ReconnectAttempts, "One knock per retry interval.");
            Assert.That(table.Clients[2].SecondsUntilGiveUp, Is.LessThan(ClientSession.GiveUpSeconds));

            table.Now += ClientSession.GiveUpSeconds;
            foreach (ClientSession client in table.Clients.Values) client.Tick();
            CollectionAssert.AreEqual(new[] { SessionEndReason.HostLeft, SessionEndReason.HostLeft }, ended);
            Assert.IsFalse(table.Clients[2].IsReconnecting);
        }

        [Test]
        public void ClientsStopAtOnceWhenTheHostLeavesOnPurposeOrTheRoomSaysSo()
        {
            NetTable table = NetTable.Create(3);
            table.StartAll();
            var ended = new List<SessionEndReason>();
            foreach (ClientSession client in table.Clients.Values) client.Ended += ended.Add;

            table.Host.Leave();
            table.Host.Leave();
            table.Pump();
            CollectionAssert.AreEqual(new[] { SessionEndReason.HostLeft, SessionEndReason.HostLeft }, ended);

            NetTable other = NetTable.Create(2);
            other.StartAll();
            var otherEnded = new List<SessionEndReason>();
            other.Clients[2].Ended += otherEnded.Add;
            other.Clients[2].ReportHostGone();
            other.Clients[2].ReportHostGone();
            CollectionAssert.AreEqual(new[] { SessionEndReason.HostLeft }, otherEnded);
        }

        [Test]
        public void AClientWhoseLinkBreaksGetsItsSeatBackWithTheCurrentState()
        {
            NetTable table = NetTable.Create(3, seed: 5UL);
            table.StartAll();
            for (int i = 0; i < 6 && table.Step(); i++)
            {
            }

            ClientSession guest = table.Clients[2];
            int resets = 0;
            var links = new List<bool>();
            guest.StateReset += () => resets++;
            guest.ConnectionChanged += links.Add;

            ulong peer = NetTable.PeerOf(2);
            table.Network.Sever(peer);
            table.Pump();
            Assert.IsTrue(guest.IsReconnecting);
            Assert.IsTrue(table.Host.IsAiPlaying(2), "The computer holds the seat meanwhile.");
            Assert.IsFalse(table.Host.IsConnected(2));

            // The game goes on without player 2's machine; its knocks go nowhere yet.
            table.Now += ClientSession.RetrySeconds + 0.1;
            table.Pump();
            Assert.AreEqual(1, guest.ReconnectAttempts);
            for (int i = 0; i < 4; i++)
            {
                if (!table.Host.StepAi())
                {
                    GameCommand command = table.NextCommand();
                    if (command.PlayerId == 2) break;
                    table.SessionOf(command.PlayerId).Submit(command);
                }

                table.Host.Tick();
                table.Clients[3].Tick();
            }

            table.Network.Restore(peer);
            table.Now += ClientSession.RetrySeconds + 0.1;
            table.Pump();

            Assert.IsFalse(guest.IsReconnecting);
            Assert.AreEqual(0, guest.ReconnectAttempts);
            Assert.AreEqual(2, guest.MyPlayerId);
            Assert.IsTrue(table.Host.IsConnected(2));
            Assert.IsFalse(table.Host.IsAiPlaying(2), "The player is back in charge.");
            Assert.AreEqual(1, resets, "The host's answer replaces the stale state once.");
            CollectionAssert.AreEqual(new[] { false, true }, links);
            AssertAllInStep(table);

            for (int i = 0; i < 10 && table.Step(); i++)
            {
            }

            AssertAllInStep(table);
        }

        [Test]
        public void ASeatWhosePlayerNeverArrivesGoesToTheComputerAfterTheGraceTime()
        {
            NetTable table = NetTable.Create(3);
            table.Host.Start();
            table.Clients[2].Start();
            table.Pump();
            Assert.IsTrue(table.Host.IsConnected(2));
            Assert.IsFalse(table.Host.IsConnected(3));

            table.Now += HostSession.NoShowSeconds - 1;
            table.Pump();
            Assert.IsFalse(table.Host.IsAiPlaying(3), "The seat is still held open.");

            table.Now += 2;
            table.Pump();
            Assert.IsTrue(table.Host.IsAiPlaying(3));
            Assert.IsTrue(table.Clients[2].IsAiPlaying(3), "The other guests hear who the computer plays.");
            Assert.IsFalse(table.Clients[2].IsConnected(3));

            // The latecomer still gets the seat the moment they arrive.
            table.Clients[3].Start();
            table.Pump();
            Assert.IsFalse(table.Host.IsAiPlaying(3));
            Assert.IsTrue(table.Host.IsConnected(3));
            Assert.IsTrue(table.Clients[2].IsConnected(3));
            AssertAllInStep(table);
        }

        [Test]
        public void TheHostTellsEveryoneWhoIsConnectedAndTheirLatency()
        {
            NetTable table = NetTable.Create(3);
            table.Network.PingMs = 42;
            table.StartAll();

            ClientSession guest = table.Clients[2];
            Assert.IsTrue(guest.IsConnected(1));
            Assert.IsTrue(guest.IsConnected(3));
            Assert.AreEqual(0, guest.PingMs(1), "The host measures nothing to itself.");
            Assert.AreEqual(42, guest.PingMs(3));
            Assert.AreEqual(42, guest.PingMs(2), "A guest reads its own latency from its transport.");
            Assert.AreEqual(0, table.Host.PingMs(1));
            Assert.AreEqual(42, table.Host.PingMs(2));

            table.Network.Disconnect(NetTable.PeerOf(3));
            table.Pump();
            Assert.IsFalse(guest.IsConnected(3));
            Assert.IsTrue(guest.IsAiPlaying(3));
            Assert.AreEqual(-1, guest.PingMs(3));
            Assert.AreEqual(-1, table.Host.PingMs(3));

            // Presence also goes out on its own, every couple of seconds, so latency stays fresh.
            table.Network.PingMs = 7;
            table.Now += HostSession.PresenceSeconds + 0.1;
            table.Pump();
            Assert.AreEqual(0, guest.PingMs(1));
            Assert.AreEqual(7, table.Host.PingMs(2));
            Assert.AreEqual(7, guest.PingMs(2));
        }

        [Test]
        public void SessionMustBeBuiltWithAValidSeatMap()
        {
            NetTable table = NetTable.Create(2);
            var transport = table.Network.CreateTransport(500);

            Assert.Throws<System.ArgumentException>(() =>
                new HostSession(table.Engine, transport, 1, new Dictionary<ulong, int> { { 501UL, 1 } }, table.Checksum));
            Assert.Throws<System.ArgumentException>(() =>
                new HostSession(table.Engine, transport, 1, new Dictionary<ulong, int> { { 501UL, 9 } }, table.Checksum));
            Assert.Throws<System.ArgumentException>(() =>
                new HostSession(table.Engine, transport, 7, new Dictionary<ulong, int>(), table.Checksum));
        }
    
        private static void PlayUntil(NetTable table, System.Func<GameState, bool> condition)
        {
            for (int i = 0; i < 500 && !condition(table.HostState); i++)
            {
                Assert.IsTrue(table.Step(), "The game ended before the situation came up.");
            }

            Assert.IsTrue(condition(table.HostState), "The situation never came up.");
        }

        [Test]
        public void ClientsTradeWithEachOtherThroughTheHost()
        {
            NetTable table = NetTable.Create(3, seed: 5UL);
            table.StartAll();
            PlayUntil(table, s => s.CurrentPlayer.Id == 2 && s.Phase == GamePhase.WaitingForRoll);
            int money2 = table.HostState.FindPlayer(2).Money;
            int money3 = table.HostState.FindPlayer(3).Money;
            var rejections = new List<RejectionReason>();
            table.Clients[2].CommandRejected += rejections.Add;

            table.Clients[2].Submit(new ProposeTradeCommand(new TradeOffer(2, 3, 50, 0, null, null)));
            table.Pump();
            Assert.AreEqual(GamePhase.AwaitingTradeResponse, table.Clients[3].State.Phase);

            // Player 2 cannot answer for player 3, not even with a hand-made message.
            table.Clients[2].Submit(new AcceptTradeCommand(3));
            table.ClientTransports[2].Send(NetTable.HostPeer, MessageCodec.Encode(new CommandMessage { Command = new AcceptTradeCommand(3) }));
            table.Pump();
            CollectionAssert.AreEqual(new[] { RejectionReason.NotYourPlayer, RejectionReason.NotYourPlayer }, rejections);
            Assert.AreEqual(GamePhase.AwaitingTradeResponse, table.HostState.Phase);

            table.Clients[3].Submit(new AcceptTradeCommand(3));
            table.Pump();

            Assert.AreEqual(money2 - 50, table.HostState.FindPlayer(2).Money);
            Assert.AreEqual(money3 + 50, table.HostState.FindPlayer(3).Money);
            Assert.AreEqual(GamePhase.WaitingForRoll, table.HostState.Phase);
            AssertAllInStep(table);
        }

        [Test]
        public void ADeclinedSquareStaysWithTheBankOnEveryMachine()
        {
            NetTable table = NetTable.Create(3, seed: 9UL);
            table.StartAll();
            PlayUntil(table, s => s.Phase == GamePhase.AwaitingBuyDecision);
            int decider = table.HostState.CurrentPlayer.Id;
            int square = table.HostState.FindPlayer(decider).Position;

            table.SessionOf(decider).Submit(new DeclinePurchaseCommand(decider));
            table.Pump();

            Assert.AreEqual(GameState.NoOwner, table.HostState.GetOwner(square));
            foreach (ClientSession client in table.Clients.Values)
            {
                Assert.AreEqual(GameState.NoOwner, client.State.GetOwner(square));
                Assert.AreNotEqual(GamePhase.AwaitingBuyDecision, client.State.Phase, "The decline moved every machine on.");
            }

            AssertAllInStep(table);
        }

        [Test]
        public void LongGamesStayInStep()
        {
            NetTable table = NetTable.Create(4, seed: 77UL);
            var seen = new HashSet<System.Type>();
            table.Host.EventsPublished += events =>
            {
                foreach (GameEvent gameEvent in events) seen.Add(gameEvent.GetType());
            };
            table.StartAll();

            for (int i = 0; i < 1500 && table.Step(); i++)
            {
            }

            AssertAllInStep(table);
            Assert.IsTrue(seen.Contains(typeof(PropertyOwnerChangedEvent)), "Nobody bought anything.");
        }

        [Test]
        public void ComputerPlaysForADroppedPlayerUntilTheyReturn()
        {
            NetTable table = NetTable.Create(3, seed: 5UL);
            var turnsStarted = new List<int>();
            table.Host.EventsPublished += events =>
            {
                foreach (GameEvent gameEvent in events)
                {
                    if (gameEvent is TurnStartedEvent started) turnsStarted.Add(started.PlayerId);
                }
            };
            table.StartAll();

            ulong peer = NetTable.PeerOf(2);
            table.Network.Disconnect(peer);
            table.Pump();
            Assert.IsTrue(table.Host.IsAiPlaying(2), "The computer takes over the dropped player.");
            Assert.IsFalse(table.Host.IsAiPlaying(3));
            Assert.IsTrue(table.Clients[3].IsAiPlaying(2), "The other guests hear from the host who the computer plays.");
            Assert.IsFalse(table.Clients[3].StepAi(), "Clients do not run the AI.");

            // Players 1 and 3 play from their machines; the host's AI plays player 2's turns.
            for (int i = 0; i < 2000 && turnsStarted.FindAll(id => id == 2).Count < 3; i++)
            {
                if (table.Host.StepAi())
                {
                    table.Pump();
                    continue;
                }

                if (table.HostState.Phase == GamePhase.GameOver) break;
                GameCommand command = table.NextCommand();
                Assert.AreNotEqual(2, command.PlayerId, "The game waited on the dropped player although the AI plays them.");
                table.SessionOf(command.PlayerId).Submit(command);
                table.Pump();
            }

            Assert.That(turnsStarted.FindAll(id => id == 2).Count, Is.GreaterThanOrEqualTo(2), "Player 2's turns were not played.");

            ITransport transport = table.Network.CreateTransport(peer);
            var rejoined = new ClientSession(transport, NetTable.HostPeer, table.Board, table.Rules, table.Checksum);
            table.Clients[2] = rejoined;
            table.ClientTransports[2] = transport;
            rejoined.Start();
            table.Pump();

            Assert.IsFalse(table.Host.IsAiPlaying(2), "The player is back in charge after reconnecting.");
            AssertAllInStep(table);
        }
    }
}

