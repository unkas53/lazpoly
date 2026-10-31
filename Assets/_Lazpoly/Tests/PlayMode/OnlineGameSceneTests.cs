using System;
using System.Collections;
using System.Collections.Generic;
using Lazpoly.App.Lobby;
using Lazpoly.App.Session;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Random;
using Lazpoly.Core.State;
using Lazpoly.Net.Transport;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Game;
using Lazpoly.Presentation.Setup;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lazpoly.PlayTests
{
    /// <summary>
    /// Runs the real Game scene as an online host and as an online client over the in-memory
    /// network; the test plays the other machine. Steam is not involved.
    /// </summary>
    public class OnlineGameSceneTests
    {
        private const int Actions = 40;
        private const float TimeoutSeconds = 120f;
        private float _previousTimeScale;

        [SetUp]
        public void SpeedUp()
        {
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 20f;
        }

        [TearDown]
        public void RestoreSpeed()
        {
            Time.timeScale = _previousTimeScale;
        }

        private static IEnumerator LoadGameScene(Action<GameSceneController> found)
        {
            SceneManager.LoadScene("Game");
            yield return null;
            yield return null;
            var controller = UnityEngine.Object.FindAnyObjectByType<GameSceneController>();
            Assert.IsNotNull(controller, "Game scene has no GameSceneController.");
            found(controller);
        }

        [UnityTest]
        public IEnumerator SceneHostsAnOnlineGame()
        {
            BoardData data = BoardDataLoader.LoadFromResources();
            var network = new LoopbackNetwork();
            ITransport hostTransport = network.CreateTransport(1000);
            OnlineSeatPlan plan = OnlineSeatPlan.Create(1000, new[] { new LobbyMember(1000, "Ev Sahibi"), new LobbyMember(1001, "Misafir") });
            OnlineGameSetup.Set(OnlineLaunch.AsHost(hostTransport, plan));

            var guest = new ClientSession(network.CreateTransport(1001), 1000, data.Board, data.Rules, data.Checksum);
            var guestRejections = new List<RejectionReason>();
            guest.CommandRejected += guestRejections.Add;
            guest.Start();

            GameSceneController controller = null;
            yield return LoadGameScene(c => controller = c);
            Assert.IsInstanceOf<HostSession>(controller.Session);

            float started = Time.realtimeSinceStartup;
            int done = 0;
            while (done < Actions && controller.Session.State.Phase != GamePhase.GameOver)
            {
                Assert.Less(Time.realtimeSinceStartup - started, TimeoutSeconds, "The online game stopped making progress.");
                guest.Tick();
                GameState hostState = controller.Session.State;
                bool inStep = guest.IsReady && guest.State.ComputeHash() == hostState.ComputeHash();
                if (controller.IsBusy || !inStep)
                {
                    yield return null;
                    continue;
                }

                GameCommand command = PlayBot.Next(controller.Session.Board, controller.Session.Rules, hostState);
                if (command.PlayerId == plan.HostPlayerId)
                {
                    controller.Submit(command);
                    Assert.IsNull(controller.LastRejection, "A legal host action was rejected.");
                }
                else
                {
                    guest.Submit(command);
                }

                done++;
                yield return null;
                yield return null;
            }

            while (controller.IsBusy) yield return null;
            guest.Tick();

            CollectionAssert.IsEmpty(guestRejections, "A legal guest action was rejected.");
            Assert.AreEqual(controller.Session.State.ComputeHash(), guest.State.ComputeHash(), "The guest drifted.");
            Assert.AreEqual(0, guest.ResyncCount);
            Assert.That(controller.Session.State.TurnNumber, Is.GreaterThan(4), "Turns did not advance.");
        }

        [UnityTest]
        public IEnumerator SceneJoinsAnOnlineGameAsAClient()
        {
            BoardData data = BoardDataLoader.LoadFromResources();
            var network = new LoopbackNetwork();
            ITransport hostTransport = network.CreateTransport(2000);
            ITransport guestTransport = network.CreateTransport(2001);

            var seats = new[] { new PlayerSeat(1, "Ev Sahibi"), new PlayerSeat(2, "Sen") };
            var engine = new GameEngine(data.Board, data.Rules, seats, new Pcg32Random(11UL));
            var host = new HostSession(engine, hostTransport, 1, new Dictionary<ulong, int> { { 2001UL, 2 } }, data.Checksum);
            OnlineGameSetup.Set(OnlineLaunch.AsClient(guestTransport, 2000));

            GameSceneController controller = null;
            yield return LoadGameScene(c => controller = c);
            Assert.IsInstanceOf<ClientSession>(controller.Session);
            host.Start();

            float started = Time.realtimeSinceStartup;
            while (!controller.Session.IsReady)
            {
                Assert.Less(Time.realtimeSinceStartup - started, 10f, "The client never received the game.");
                host.Tick();
                yield return null;
            }

            Assert.IsTrue(controller.Session.CanControl(2));
            Assert.IsFalse(controller.Session.CanControl(1));

            int done = 0;
            while (done < Actions && engine.State.Phase != GamePhase.GameOver)
            {
                Assert.Less(Time.realtimeSinceStartup - started, TimeoutSeconds, "The online game stopped making progress.");
                host.Tick();
                bool inStep = controller.Session.State.ComputeHash() == engine.State.ComputeHash();
                if (controller.IsBusy || !inStep)
                {
                    yield return null;
                    continue;
                }

                GameCommand command = PlayBot.Next(engine.Board, engine.Rules, engine.State);
                if (command.PlayerId == 1) host.Submit(command);
                else controller.Submit(command);

                done++;
                yield return null;
                yield return null;
            }

            host.Tick();
            yield return null;
            while (controller.IsBusy) yield return null;

            Assert.IsNull(controller.LastRejection, "A legal client action was rejected.");
            Assert.IsNull(controller.EndReason);
            Assert.AreEqual(engine.State.ComputeHash(), controller.Session.State.ComputeHash(), "The client scene drifted.");
            Assert.That(engine.State.TurnNumber, Is.GreaterThan(4), "Turns did not advance.");
        }
    }
}
