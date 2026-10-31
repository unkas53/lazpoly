using System.Collections;
using System.IO;
using Lazpoly.App.Save;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Game;
using Lazpoly.Presentation.Setup;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Lazpoly.PlayTests
{
    /// <summary>Plays through the real interface: pressing its buttons the way a player would.</summary>
    public class InterfaceTests
    {
        private float _previousTimeScale;

        [SetUp]
        public void SetUp()
        {
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 20f;
            string folder = Path.Combine(Application.temporaryCachePath, "lazpoly-test-save");
            Directory.CreateDirectory(folder);
            SaveGameStore.FolderOverride = folder;
            SaveGameStore.Delete();
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = _previousTimeScale;
            SaveGameStore.Delete();
            SaveGameStore.FolderOverride = null;
        }

        [UnityTest]
        public IEnumerator SinglePlayerPresetStartsAGameAgainstThreeComputers()
        {
            SceneManager.LoadScene("MainMenu");
            yield return null;
            yield return null;

            Assert.IsTrue(Press(null, "Tek Oyunculu: sen + 3 bilgisayar"), "No single player button.");
            yield return null;
            Assert.IsTrue(Press(null, "Oyunu Başlat"), "No start button.");
            yield return null;
            yield return null;

            var controller = Object.FindAnyObjectByType<GameSceneController>();
            Assert.IsNotNull(controller, "The game did not start.");
            Assert.AreEqual(4, controller.Session.State.Players.Count);
            Assert.IsTrue(controller.Session.CanControl(1));
            for (int id = 2; id <= 4; id++) Assert.IsTrue(controller.Session.IsAiPlaying(id));
        }

        [UnityTest]
        public IEnumerator TwoPeopleCanPlayTurnsWithTheHudButtons()
        {
            LocalGameSetup.SetSeats(LocalGameSetup.DefaultSeats(2));
            SceneManager.LoadScene("Game");
            yield return null;
            yield return null;

            var controller = Object.FindAnyObjectByType<GameSceneController>();
            var hud = Object.FindAnyObjectByType<GameHud>();
            Assert.IsNotNull(hud, "The game has no HUD.");

            // Roll, decline every purchase, pass in every auction and end the turn: all through buttons.
            string[] order = { "Zar At", "Alma, açık artırmaya çıkar", "Pas", "Borcu Öde", "İflas Et", "Sırayı Bitir" };
            float started = Time.realtimeSinceStartup;
            while (controller.Session.State.TurnNumber < 6 && controller.Session.State.Phase != GamePhase.GameOver)
            {
                Assert.Less(Time.realtimeSinceStartup - started, 120f, "The HUD stopped offering a way forward.");
                yield return null; // Let the HUD redraw for the latest state.
                if (controller.IsBusy) continue;

                foreach (string label in order)
                {
                    if (!Press(hud.SideContent, label)) continue;
                    Assert.IsNull(controller.LastRejection, $"'{label}' was offered but rejected.");
                    break;
                }
            }

            Assert.That(controller.Session.State.TurnNumber, Is.GreaterThanOrEqualTo(6));
        }

        /// <summary>Clicks the first interactable button whose label starts with this text (under <paramref name="root"/>, or anywhere).</summary>
        private static bool Press(Transform root, string label)
        {
            Button[] buttons = root != null ? root.GetComponentsInChildren<Button>() : Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            foreach (Button button in buttons)
            {
                Text text = button.GetComponentInChildren<Text>();
                if (text == null || !text.text.StartsWith(label) || !button.interactable || !button.gameObject.activeInHierarchy) continue;
                button.onClick.Invoke();
                return true;
            }

            return false;
        }
    }
}
