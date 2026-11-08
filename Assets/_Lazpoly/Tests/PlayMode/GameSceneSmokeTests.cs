using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Lazpoly.App.AI;
using Lazpoly.App.Save;
using Lazpoly.App.Session;
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

        private string _saveFolder;

        [SetUp]
        public void SpeedUp()
        {
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 20f;

            // Local games save themselves; keep that away from the real save slot.
            _saveFolder = Path.Combine(Application.temporaryCachePath, "lazpoly-test-save");
            Directory.CreateDirectory(_saveFolder);
            SaveGameStore.FolderOverride = _saveFolder;
            SaveGameStore.Delete();
        }

        [TearDown]
        public void RestoreSpeed()
        {
            Time.timeScale = _previousTimeScale;
            SaveGameStore.Delete();
            SaveGameStore.FolderOverride = null;
        }

        [UnityTest]
        public IEnumerator MainMenuLoadsWithItsController()
        {
            SceneManager.LoadScene("MainMenu");
            yield return null;
            yield return null;

            Assert.IsNotNull(Object.FindAnyObjectByType<MainMenuController>());
        }

        [UnityTest]
        public IEnumerator FourPlayersCanPlaySeveralRounds()
        {
            LocalGameSetup.SetSeats(LocalGameSetup.DefaultSeats(4));
            SceneManager.LoadScene("Game");
            yield return null;
            yield return null;

            var controller = Object.FindAnyObjectByType<GameSceneController>();
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

                IGameSession session = controller.Session;
                controller.Submit(PlayBot.Next(session.Board, session.Rules, session.State));

                Assert.IsNull(controller.LastRejection, "A legal action was rejected.");
                done++;
                yield return null;
            }

            while (controller.IsBusy) yield return null;

            Assert.That(controller.Session.State.TurnNumber, Is.GreaterThan(4), "Turns did not advance.");
            Assert.That(controller.Log.Count, Is.GreaterThan(0), "The event log stayed empty.");
        }

        [UnityTest]
        public IEnumerator ComputerPlayersPlayOnTheirOwnAndTheGameResumes()
        {
            var ai = new Dictionary<int, AiDifficulty>
            {
                { 1, AiDifficulty.Easy }, { 2, AiDifficulty.Normal }, { 3, AiDifficulty.Hard }, { 4, AiDifficulty.Normal },
            };
            LocalGameSetup.SetSeats(LocalGameSetup.DefaultSeats(4), ai);
            SceneManager.LoadScene("Game");
            yield return null;
            yield return null;

            var controller = Object.FindAnyObjectByType<GameSceneController>();
            Assert.IsNotNull(controller);
            Assert.IsFalse(controller.Session.CanControl(1), "Nobody at the screen plays a computer seat.");

            // A drawn player rolls first, whoever it is: the turn order starts with them and the table says so.
            GameState start = controller.Session.State;
            Assert.AreEqual(start.Players[0].Id, start.CurrentPlayer.Id, "The game starts with the first seat of the turn order.");
            // The starting money comes up through the hatches first; the draw is told once it has landed, and nobody plays before.
            string drawn = GameSceneController.FirstRollText(start.CurrentPlayer.Name);
            float dealt = Time.realtimeSinceStartup;
            while (!controller.Log.Any(entry => entry.Text == drawn))
            {
                Assert.AreEqual(1, controller.Session.State.TurnNumber, "The computer waits while the money is handed out.");
                Assert.IsTrue(controller.IsBusy, "The table holds still until the draw.");
                Assert.Less(Time.realtimeSinceStartup - dealt, 20f, "The log does not say who the draw picked.");
                yield return null;
            }

            Assert.AreEqual(1, controller.Session.State.TurnNumber, "The computer waits while the draw is shown.");

            float started = Time.realtimeSinceStartup;
            while (controller.Session.State.TurnNumber < 8 && controller.Session.State.Phase != GamePhase.GameOver)
            {
                Assert.Less(Time.realtimeSinceStartup - started, 120f, "The computer players stopped playing.");
                yield return null;
            }

            while (controller.IsBusy) yield return null;
            if (controller.Session.State.Phase == GamePhase.GameOver) yield break; // Ended early: nothing left to resume.

            Assert.IsTrue(SaveGameStore.TryRead(out SavedGame save), "The local game was not saved.");
            int savedTurn = save.Snapshot.TurnNumber;

            LocalGameSetup.SetResume(save);
            SceneManager.LoadScene("Game");
            yield return null;
            yield return null;

            controller = Object.FindAnyObjectByType<GameSceneController>();
            Assert.That(controller.Session.State.TurnNumber, Is.GreaterThanOrEqualTo(savedTurn), "The saved game did not resume.");
            Assert.IsTrue(controller.Session.IsAiPlaying(3));
            Assert.IsTrue(controller.Log.Any(entry => entry.Text == "Kayıtlı oyun yüklendi."), "The log does not say the save was loaded.");
            CollectionAssert.AreEqual(save.Snapshot.Players.Select(player => player.Id).ToArray(),
                controller.Session.State.Players.Select(player => player.Id).ToArray(), "A resumed game keeps its turn order.");
            Assert.IsFalse(controller.Log.Any(entry => entry.Text.StartsWith("Kura:")), "A resumed game draws nobody.");
        }
    }
}
