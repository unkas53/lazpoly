using System.Collections;
using Lazpoly.Core.Commands;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Game;
using Lazpoly.Presentation.Menu;
using Lazpoly.Presentation.Setup;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lazpoly.PlayTests
{
    /// <summary>
    /// Loads the real scenes and plays the Game scene through its controller, the same calls the HUD makes.
    /// Any exception or error log during the run fails the test.
    /// </summary>
    public class GameSceneSmokeTests
    {
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

        [UnityTest]
        public IEnumerator MainMenuLoadsWithItsController()
        {
            SceneManager.LoadScene("MainMenu");
            yield return null;
            yield return null;

            Assert.IsNotNull(Object.FindFirstObjectByType<MainMenuController>());
        }

        [UnityTest]
        public IEnumerator FourPlayersCanPlaySeveralRounds()
        {
            LocalGameSetup.SetSeats(LocalGameSetup.DefaultSeats(4));
            SceneManager.LoadScene("Game");
            yield return null;
            yield return null;

            var controller = Object.FindFirstObjectByType<GameSceneController>();
            Assert.IsNotNull(controller, "Game scene has no GameSceneController.");
            Assert.AreEqual(4, controller.Session.State.Players.Count);

            const int actions = 60;
            const float timeoutSeconds = 120f;
            float started = Time.realtimeSinceStartup;
            int done = 0;

            while (done < actions && controller.Session.State.Phase != GamePhase.GameOver)
            {
                Assert.Less(Time.realtimeSinceStartup - started, timeoutSeconds, "The scene stopped making progress.");
                if (controller.IsBusy)
                {
                    yield return null;
                    continue;
                }

                GameState state = controller.Session.State;
                int player = state.CurrentPlayer.Id;
                switch (state.Phase)
                {
                    case GamePhase.WaitingForRoll: controller.Submit(new RollDiceCommand(player)); break;
                    case GamePhase.AwaitingBuyDecision: controller.Submit(new BuyPropertyCommand(player)); break;
                    case GamePhase.TurnEnd: controller.Submit(new EndTurnCommand(player)); break;
                }

                Assert.IsNull(controller.LastRejection, "A legal action was rejected.");
                done++;
                yield return null;
            }

            while (controller.IsBusy) yield return null;

            Assert.That(controller.Session.State.TurnNumber, Is.GreaterThan(4), "Turns did not advance.");
            Assert.That(controller.Log.Count, Is.GreaterThan(0), "The event log stayed empty.");
        }
    }
}
