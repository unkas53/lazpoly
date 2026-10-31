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

            table.Now += TurnClock.TurnSeconds - 1;
            table.Pump();
            Assert.AreEqual(0, rolls, "The host moved before the time was up.");

            table.Now += 2;
            table.Pump();
            Assert.AreEqual(1, notices.Count);
            Assert.IsFalse(notices[0].StandInTookOver);
            Assert.AreEqual(1, rolls, "The host should have rolled for the idle player.");
            Assert.IsFalse(table.Host.IsAiPlaying(2));
            Assert.AreEqual(table.HostState.ComputeHash(), table.Clients[2].State.ComputeHash());

            table.Now += TurnClock.TurnSeconds + 1;
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

            table.Now += TurnClock.TurnSeconds + 1;
            table.Pump();
            table.Clients[2].Submit(table.NextCommand());
            table.Pump();

            table.Now += TurnClock.TurnSeconds + 1;
            table.Pump();
            if (table.HostState.Phase == GamePhase.GameOver) return;
            Assert.IsFalse(table.Host.IsAiPlaying(table.HostState.CurrentPlayer.Id), "A single timeout after a move of one's own should not hand the seat over.");
        }

        [Test]
        public void AnswersGetLessTimeThanTurns()
        {
            Assert.AreEqual(TurnClock.ResponseSeconds, TurnClock.LimitFor(GamePhase.AuctionInProgress));
            Assert.AreEqual(TurnClock.ResponseSeconds, TurnClock.LimitFor(GamePhase.AwaitingTradeResponse));
            Assert.AreEqual(TurnClock.TurnSeconds, TurnClock.LimitFor(GamePhase.AwaitingBuyDecision));
            Assert.Less(TurnClock.ResponseSeconds, TurnClock.TurnSeconds);

            double now = 0;
            var clock = new TurnClock(() => now);
            now = 29;
            Assert.IsFalse(clock.HasExpired(GamePhase.AuctionInProgress));
            now = 31;
            Assert.IsTrue(clock.HasExpired(GamePhase.AuctionInProgress));
            Assert.IsFalse(clock.HasExpired(GamePhase.TurnEnd));
            clock.Restart();
            Assert.AreEqual(TurnClock.TurnSeconds, clock.Remaining(GamePhase.TurnEnd), 1e-9);
        }

        [Test]
        public void TheCautiousMoveCommitsToNothing()
        {
            Assert.IsInstanceOf<DeclinePurchaseCommand>(TimeoutPolicy.CommandFor(StateIn(GamePhase.AwaitingBuyDecision), 1));
            Assert.IsInstanceOf<PassBidCommand>(TimeoutPolicy.CommandFor(StateIn(GamePhase.AuctionInProgress), 1));
            Assert.IsInstanceOf<RejectTradeCommand>(TimeoutPolicy.CommandFor(StateIn(GamePhase.AwaitingTradeResponse), 1));
            Assert.IsInstanceOf<DeclineTravelCommand>(TimeoutPolicy.CommandFor(StateIn(GamePhase.AwaitingTravelDecision), 1));
            Assert.IsNull(TimeoutPolicy.CommandFor(StateIn(GamePhase.AwaitingDebtPayment), 1), "A debt needs the computer to decide what to sell.");
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
            NetTable table = NetTable.Create(3, table: new Lazpoly.Core.Rules.TableRules(1600, false, 3));
            Assert.AreEqual(800, table.Clients[2].Rules.StartingMoney, "Before the game starts a client only has the board data.");
            table.StartAll();

            foreach (ClientSession client in table.Clients.Values)
            {
                Assert.AreEqual(1600, client.Rules.StartingMoney);
                Assert.IsFalse(client.Rules.AuctionsEnabled);
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
        public void ClientsStopWhenTheHostLeaves()
        {
            NetTable table = NetTable.Create(3);
            table.StartAll();
            var ended = new List<SessionEndReason>();
            var rejections = new List<RejectionReason>();
            foreach (ClientSession client in table.Clients.Values)
            {
                client.Ended += ended.Add;
                client.CommandRejected += rejections.Add;
            }

            table.Network.Disconnect(NetTable.HostPeer);
            foreach (ClientSession client in table.Clients.Values) client.Tick();
            table.Clients[2].Submit(new RollDiceCommand(2));

            CollectionAssert.AreEqual(new[] { SessionEndReason.HostLeft, SessionEndReason.HostLeft }, ended);
            CollectionAssert.AreEqual(new[] { RejectionReason.NotConnected }, rejections);
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
        public void EveryMachineBidsInAnAuction()
        {
            NetTable table = NetTable.Create(3, seed: 9UL);
            table.StartAll();
            PlayUntil(table, s => s.Phase == GamePhase.AwaitingBuyDecision);
            int decider = table.HostState.CurrentPlayer.Id;
            int square = table.HostState.FindPlayer(decider).Position;

            table.SessionOf(decider).Submit(new DeclinePurchaseCommand(decider));
            table.Pump();
            Assert.AreEqual(GamePhase.AuctionInProgress, table.Clients[2].State.Phase);

            table.Host.Submit(new PlaceBidCommand(1, 10));
            table.Pump();
            table.Clients[2].Submit(new PlaceBidCommand(2, 20));
            table.Pump();
            table.Clients[3].Submit(new PassBidCommand(3));
            table.Pump();
            table.Host.Submit(new PassBidCommand(1));
            table.Pump();

            Assert.AreEqual(2, table.HostState.GetOwner(square));
            Assert.IsNull(table.HostState.Auction);
            AssertAllInStep(table);
        }

        [Test]
        public void LongGamesWithAuctionsStayInStep()
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
            Assert.IsTrue(seen.Contains(typeof(AuctionEndedEvent)), "No auction happened.");
            Assert.IsTrue(seen.Contains(typeof(BidPlacedEvent)), "Nobody bid.");
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
            Assert.IsFalse(table.Clients[3].IsAiPlaying(2), "Clients do not run the AI.");

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

