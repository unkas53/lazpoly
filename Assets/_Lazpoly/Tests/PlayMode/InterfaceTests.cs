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

            // The first screen only asks how to play.
            AssertMouseReaches("Tek Oyunculu");
            Assert.IsTrue(Press(null, "Tek Oyunculu"), "No single player button.");
            yield return null;
            yield return null;

            // A mouse click needs the button to live from press to release: the menu must not rebuild while idle.
            Button idleStart = Find(null, "Oyunu Başlat");
            for (int frame = 0; frame < 5; frame++) yield return null;
            Assert.IsTrue(idleStart != null && idleStart.gameObject.activeInHierarchy, "The menu rebuilt itself while nothing changed.");

            AssertMouseReaches("Oyunu Başlat");
            Assert.IsTrue(Press(null, "Oyunu Başlat"), "No start button.");

            // The scene changes behind a short fade, so the table appears a few frames later.
            GameSceneController controller = null;
            float pressed = Time.realtimeSinceStartup;
            while (controller == null && Time.realtimeSinceStartup - pressed < 5f)
            {
                yield return null;
                controller = Object.FindAnyObjectByType<GameSceneController>();
            }

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

            // Roll and move on past every city (on its card) through buttons; the turn ends by itself.
            int soundsBefore = AudioPlayer.PlayedCount;
            // Building is put off; rides are declined; of the two lookout rolls the first is taken (its
            // button starts with a die); a debt is paid when the cash is there, else sold for automatically.
            string[] order = { "ZAR AT", "Devam Et", "Şimdi Değil", "Burada kal", "1 +", "2 +", "3 +", "4 +", "5 +", "6 +", "Borcu Öde", "Otomatik Sat" };
            float started = Time.realtimeSinceStartup;
            bool sawTarget = false;
            while (controller.Session.State.TurnNumber < 6 && controller.Session.State.Phase != GamePhase.GameOver)
            {
                Assert.Less(Time.realtimeSinceStartup - started, 120f, "The HUD stopped offering a way forward.");
                yield return null; // Let the HUD redraw for the latest state.
                sawTarget |= controller.Markers.Target >= 0;
                if (controller.IsBusy) continue;

                foreach (string label in order)
                {
                    if (!Press(hud.transform, label)) continue;
                    Assert.IsNull(controller.LastRejection, $"'{label}' was offered but rejected.");
                    break;
                }
            }

            Assert.That(controller.Session.State.TurnNumber, Is.GreaterThanOrEqualTo(6));
            Assert.IsTrue(sawTarget, "No roll marked the square the pawn was walking to.");
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

        [UnityTest]
        public IEnumerator TheSideDockIsGoneAndTheBottomStripKeepsTheMenu()
        {
            LocalGameSetup.SetSeats(LocalGameSetup.DefaultSeats(2));
            SceneManager.LoadScene("Game");
            yield return null;
            yield return null;

            var controller = Object.FindAnyObjectByType<GameSceneController>();
            var hud = controller.GetComponent<GameHud>();
            while (controller.IsBusy) yield return null;

            Assert.IsNull(GameObject.Find("Dock"), "The round buttons at the right edge are gone.");
            AssertMouseReaches("Menü");
            AssertMouseReaches("Mülkler");
            Assert.IsNotNull(Find(hud.transform, "ZAR AT"), "Rolling keeps its big button.");
            Assert.IsNull(hud.BoardChoices(controller.Session.State), "Nothing is chosen on the board at the start of a turn.");

            // The rings over the squares: one target at a time, and any number of choices.
            Lazpoly.Presentation.Tabletop.SquareMarkers markers = controller.Markers;
            markers.ShowTarget(5, Color.red);
            Assert.AreEqual(5, markers.Target);
            markers.ShowTarget(7, Color.blue);
            Assert.AreEqual(7, markers.Target, "A new target replaces the old one.");
            markers.HideTarget();
            Assert.AreEqual(-1, markers.Target);
            markers.ShowChoices(new[] { 1, 2, 3 });
            Assert.AreEqual(3, markers.Choices.Count);
            markers.ShowChoices(null);
            Assert.AreEqual(0, markers.Choices.Count);

            Assert.IsTrue(Press(hud.transform, "Menü"));
            yield return null;
            Assert.IsTrue(hud.PauseOpen, "The menu button opens the menu.");
        }

        [UnityTest]
        public IEnumerator KeysOpenThePauseMenuAndPlayTheTurn()
        {
            LocalGameSetup.SetSeats(LocalGameSetup.DefaultSeats(2));
            SceneManager.LoadScene("Game");
            yield return null;
            yield return null;

            var controller = Object.FindAnyObjectByType<GameSceneController>();
            var hud = controller.GetComponent<GameHud>();
            while (controller.IsBusy) yield return null;
            Assert.AreEqual(GamePhase.WaitingForRoll, controller.Session.State.Phase);

            // Esc opens the menu and holds the game; the menu offers the way out and a second Esc closes it.
            Assert.IsTrue(hud.PressKey(KeyCode.Escape));
            yield return null;
            Assert.IsTrue(hud.PauseOpen);
            Assert.IsTrue(controller.Paused);
            AssertMouseReaches("Devam Et");
            Assert.IsNotNull(Find(hud.transform, "Kaydet ve Çık"));
            Assert.IsFalse(hud.PressKey(KeyCode.Space), "Nothing rolls while the menu is open.");
            Assert.IsTrue(hud.PressKey(KeyCode.Escape));
            yield return null;
            Assert.IsFalse(hud.PauseOpen);
            Assert.IsFalse(controller.Paused);

            // H opens help wherever the turn stands; Esc closes it again. Neither moves the game on.
            int turn = controller.Session.State.TurnNumber;
            Assert.IsTrue(hud.PressKey(KeyCode.H));
            yield return null;
            Assert.IsNotNull(Find(hud.transform, "İpuçlarını Yeniden Göster"), "The help window did not open.");
            Assert.IsFalse(hud.PressKey(KeyCode.Space), "Nothing rolls while a window is open.");
            Assert.IsTrue(hud.PressKey(KeyCode.Escape));
            yield return null;
            Assert.IsNull(Find(hud.transform, "İpuçlarını Yeniden Göster"), "The help window did not close.");
            Assert.AreEqual(GamePhase.WaitingForRoll, controller.Session.State.Phase);
            Assert.AreEqual(turn, controller.Session.State.TurnNumber);

            // Space rolls for the person on turn, exactly as the button would.
            Assert.IsTrue(hud.PressKey(KeyCode.Space));
            Assert.IsNull(controller.LastRejection);
            // A double that lands on a square with nothing to decide leaves the phase at WaitingForRoll again, so the dice are the proof.
            Assert.AreNotEqual(0, controller.Session.State.LastDie1, "The dice were rolled.");
            Assert.IsTrue(controller.IsBusy, "The roll is being shown.");
            Assert.IsFalse(hud.PressKey(KeyCode.Space), "Nothing is offered while the board animates.");
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
