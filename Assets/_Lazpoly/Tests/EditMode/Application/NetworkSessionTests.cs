using System.Collections.Generic;
using Lazpoly.App.Session;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
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
            var stranger = new ClientSession(strangerTransport, NetTable.HostPeer, table.Board, table.Checksum);
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
            var client = new ClientSession(transport, NetTable.HostPeer, table.Board, table.Checksum + 1);
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
            var rejoined = new ClientSession(transport, NetTable.HostPeer, table.Board, table.Checksum);
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
    }
}
