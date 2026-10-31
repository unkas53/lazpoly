using System;
using System.Collections.Generic;
using Lazpoly.App.Session;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Random;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;
using Lazpoly.Net.Transport;

namespace Lazpoly.App.Tests
{
    /// <summary>
    /// An online table on the in-memory network: the host (peer 100) plays player 1 and client i
    /// (peer 100 + i) plays player i + 1. Tests drive it one action at a time.
    /// </summary>
    internal sealed class NetTable
    {
        public const ulong HostPeer = 100;

        private NetTable(int players, ulong seed, Func<ulong, ITransport, ITransport> wrapClient)
        {
            Board = NetTestData.Board();
            Rules = NetTestData.Rules();
            Checksum = BoardChecksum.Compute(Board, Rules);

            var seats = new PlayerSeat[players];
            var remote = new Dictionary<ulong, int>();
            for (int i = 0; i < players; i++)
            {
                seats[i] = new PlayerSeat(i + 1, $"P{i + 1}");
                if (i > 0) remote.Add(PeerOf(i + 1), i + 1);
            }

            Engine = new GameEngine(Board, Rules, seats, new Pcg32Random(seed));
            HostTransport = Network.CreateTransport(HostPeer);
            Host = new HostSession(Engine, HostTransport, 1, remote, Checksum);

            for (int player = 2; player <= players; player++)
            {
                ulong peer = PeerOf(player);
                ITransport transport = Network.CreateTransport(peer);
                if (wrapClient != null) transport = wrapClient(peer, transport);
                ClientTransports[player] = transport;
                Clients[player] = new ClientSession(transport, HostPeer, Board, Checksum);
            }
        }

        public LoopbackNetwork Network { get; } = new LoopbackNetwork();
        public BoardDefinition Board { get; }
        public RulesConfig Rules { get; }
        public ulong Checksum { get; }
        public GameEngine Engine { get; }
        public ITransport HostTransport { get; }
        public HostSession Host { get; }

        /// <summary>Client sessions by the player they play (2..n).</summary>
        public Dictionary<int, ClientSession> Clients { get; } = new Dictionary<int, ClientSession>();

        public Dictionary<int, ITransport> ClientTransports { get; } = new Dictionary<int, ITransport>();

        public GameState HostState => Engine.State;

        public static ulong PeerOf(int playerId) => HostPeer + (ulong)(playerId - 1);

        public static NetTable Create(int players, ulong seed = 1UL, Func<ulong, ITransport, ITransport> wrapClient = null) =>
            new NetTable(players, seed, wrapClient);

        /// <summary>Connects everyone and waits until every client has the state.</summary>
        public void StartAll()
        {
            Host.Start();
            foreach (ClientSession client in Clients.Values) client.Start();
            Pump();
        }

        /// <summary>Lets messages travel back and forth until the network is quiet.</summary>
        public void Pump(int rounds = 8)
        {
            for (int i = 0; i < rounds; i++)
            {
                Host.Tick();
                foreach (ClientSession client in Clients.Values) client.Tick();
            }
        }

        public IGameSession SessionOf(int playerId) => playerId == 1 ? (IGameSession)Host : Clients[playerId];

        /// <summary>The obvious next command for whoever's turn it is (always buys).</summary>
        public GameCommand NextCommand()
        {
            int player = HostState.CurrentPlayer.Id;
            switch (HostState.Phase)
            {
                case GamePhase.WaitingForRoll: return new RollDiceCommand(player);
                case GamePhase.AwaitingBuyDecision: return new BuyPropertyCommand(player);
                default: return new EndTurnCommand(player);
            }
        }

        /// <summary>The current player acts from their own machine. Returns false once the game is over.</summary>
        public bool Step()
        {
            if (HostState.Phase == GamePhase.GameOver) return false;
            GameCommand command = NextCommand();
            SessionOf(command.PlayerId).Submit(command);
            Pump();
            return HostState.Phase != GamePhase.GameOver;
        }
    }

    internal static class NetTestData
    {
        public static BoardDefinition Board() => new BoardDefinition(new[]
        {
            SquareDefinition.Start("Başlangıç"),
            SquareDefinition.Property("A1", 60, 20, 1),
            SquareDefinition.Property("A2", 80, 24, 1),
            SquareDefinition.Tax("Vergi", 100),
            SquareDefinition.Property("B1", 120, 40, 2),
            SquareDefinition.Property("B2", 140, 44, 2),
            SquareDefinition.Rest("Mola"),
            SquareDefinition.Property("C1", 200, 60, 3),
            SquareDefinition.Property("C2", 220, 66, 3),
            SquareDefinition.Tax("Küçük vergi", 50),
        });

        public static RulesConfig Rules() => new RulesConfig(800, 100, 6, 3, 2);
    }

    /// <summary>Wraps a client's transport so a test can lose chosen incoming messages.</summary>
    internal sealed class LossyTransport : ITransport
    {
        private readonly ITransport _inner;

        public LossyTransport(ITransport inner)
        {
            _inner = inner;
            _inner.Received += (peer, data) =>
            {
                if (DropIncoming != null && DropIncoming(data)) return;
                Received?.Invoke(peer, data);
            };
            _inner.PeerDisconnected += peer => PeerDisconnected?.Invoke(peer);
        }

        /// <summary>Returns true for a message that should vanish.</summary>
        public Func<byte[], bool> DropIncoming { get; set; }

        public ulong LocalPeer => _inner.LocalPeer;
        public event Action<ulong, byte[]> Received;
        public event Action<ulong> PeerDisconnected;
        public bool Send(ulong peer, byte[] payload) => _inner.Send(peer, payload);
        public void Poll() => _inner.Poll();
    }
}
