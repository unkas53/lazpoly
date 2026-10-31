using System.Collections;
using System.Collections.Generic;
using System.IO;
using Lazpoly.App.Save;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Audio;
using Lazpoly.Presentation.Game;
using Lazpoly.Presentation.Setup;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
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

            // A mouse click needs the button to live from press to release: the menu must not rebuild while idle.
            Button idleStart = Find(null, "Oyunu Başlat");
            for (int frame = 0; frame < 5; frame++) yield return null;
            Assert.IsTrue(idleStart != null && idleStart.gameObject.activeInHierarchy, "The menu rebuilt itself while nothing changed.");

            AssertMouseReaches("Oyunu Başlat");
            AssertMouseReaches("Tek Oyunculu: sen + 3 bilgisayar");
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
            int soundsBefore = AudioPlayer.PlayedCount;
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
            Assert.Greater(AudioPlayer.PlayedCount, soundsBefore, "Playing turns made no sound.");

            // Once every event is shown, the money on screen is the real money.
            while (controller.IsBusy) yield return null;
            foreach (PlayerState player in controller.Session.State.Players)
            {
                Assert.AreEqual(player.Money, controller.ShownMoneyOf(player.Id), $"{player.Name}'s money on screen");
            }

            // A clicked square opens its card with the square's name and price.
            hud.PinSquare(1);
            yield return null;
            yield return null;
            Assert.AreEqual(1, hud.ShownSquare);
            string name = controller.Session.Board[1].Name;
            bool shown = false;
            foreach (Text text in hud.GetComponentsInChildren<Text>())
            {
                shown |= text.text == name && text.gameObject.activeInHierarchy;
            }

            Assert.IsTrue(shown, $"The card for '{name}' is not on screen.");
        }

        /// <summary>
        /// A real mouse click goes to whatever the event system hits first at the pointer, so nothing
        /// (an invisible panel, a closed window) may lie on top of the button.
        /// </summary>
        private static void AssertMouseReaches(string label)
        {
            Button button = Find(null, label);
            Assert.IsNotNull(button, $"No '{label}' button.");
            Vector3[] corners = new Vector3[4];
            ((RectTransform)button.transform).GetWorldCorners(corners);
            Vector2 center = (corners[0] + corners[2]) / 2f;

            var pointer = new PointerEventData(EventSystem.current) { position = center };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.Greater(hits.Count, 0, $"Nothing under the '{label}' button.");
            GameObject top = hits[0].gameObject;
            Assert.IsTrue(top == button.gameObject || top.transform.IsChildOf(button.transform),
                $"'{top.name}' lies on top of the '{label}' button.");
            Assert.AreEqual(button.gameObject, ExecuteEvents.GetEventHandler<IPointerClickHandler>(top));
        }

        private static bool Press(Transform root, string label)
        {
            Button button = Find(root, label);
            if (button == null) return false;
            button.onClick.Invoke();
            return true;
        }

        /// <summary>The first interactable button whose label starts with this text (under <paramref name="root"/>, or in the scene).</summary>
        private static Button Find(Transform root, string label)
        {
            var buttons = new List<Button>();
            if (root != null) buttons.AddRange(root.GetComponentsInChildren<Button>());
            else
            {
                foreach (GameObject sceneRoot in SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    buttons.AddRange(sceneRoot.GetComponentsInChildren<Button>());
                }
            }

            foreach (Button button in buttons)
            {
                Text text = button.GetComponentInChildren<Text>();
                if (text != null && text.text.StartsWith(label) && button.interactable && button.gameObject.activeInHierarchy) return button;
            }

            return null;
        }
    }
}
