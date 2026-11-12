using System.Collections;
using System.Collections.Generic;
using System.IO;
using Lazpoly.App.AI;
using Lazpoly.App.Save;
using Lazpoly.App.Session;
using Lazpoly.Core.Board;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Events;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Audio;
using Lazpoly.Presentation.Data;
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

            // The kit's home screen: play at once, rooms, the set-up games and the bottom row, all within reach of the mouse.
            foreach (string way in new[] { "HEMEN OYNA", "ODA KUR", "ODAYA KATIL", "TEK OYUNCULU", "AYNI BİLGİSAYARDA", "Karakterler", "Nasıl oynanır", "Ayarlar" })
            {
                AssertMouseReaches(way);
            }

            // Picking a character names it on the profile card; Esc closes the window. The player's own pick is put back.
            var menu = Object.FindAnyObjectByType<Lazpoly.Presentation.Menu.MainMenuController>();
            var choice = new CharacterChoice(new Lazpoly.Infra.Settings.PlayerPrefsStorage());
            string ownPick = choice.Selected.Id;
            Assert.IsTrue(Press(null, "Karakterler"));
            yield return null;
            AssertMouseReaches("Ada");
            Assert.IsTrue(Press(null, "Ada"));
            yield return null;
            Assert.AreEqual("Ada", choice.Selected.Name);
            Assert.IsTrue(ShowsText("Karakter: Ada"), "The profile card names the picked character.");
            Assert.IsTrue(menu.CloseTopWindow());
            yield return null;
            Assert.IsNull(Find(null, "Ada"), "The character window closed.");
            choice.Select(ownPick);

            Assert.IsTrue(Press(null, "TEK OYUNCULU"), "No single player button.");
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

            // A choice on the board (a ride waiting since the last turn, the cup, a card's town) keeps a square's card shut,
            // and the dice are random, so play is first brought to a moment with no such choice.
            float settled = Time.realtimeSinceStartup;
            while (controller.Session.State.Phase != GamePhase.GameOver && (controller.IsBusy || hud.BoardChoices(controller.Session.State) != null))
            {
                Assert.Less(Time.realtimeSinceStartup - settled, 60f, "The board kept asking for a square.");
                yield return null;
                if (controller.IsBusy || hud.BoardChoices(controller.Session.State) == null) continue;
                foreach (string label in order)
                {
                    if (Press(hud.transform, label)) break;
                }
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
            yield return null; // The table may only now have shown the draw for the first roll; let the HUD redraw.

            Assert.IsNull(GameObject.Find("Dock"), "The round buttons at the right edge are gone.");
            AssertMouseReaches("Menü");
            AssertMouseReaches("Takas");
            Assert.IsNull(Find(hud.transform, "Mülkler"), "The properties button is gone (the owner's wish, Faz 31).");
            Assert.IsNotNull(Find(hud.transform, "ZAR AT"), "Rolling keeps its big button.");
            Assert.IsNull(hud.BoardChoices(controller.Session.State), "Nothing is chosen on the board at the start of a turn.");

            // The target square glows (one at a time); the sale's choices are ringed.
            Lazpoly.Presentation.Tabletop.SquareMarkers markers = controller.Markers;
            markers.ShowTarget(5, Color.red);
            Assert.AreEqual(5, markers.Target);
            Assert.AreEqual(5, controller.Board.GlowingSquare, "The square itself lights up.");
            markers.ShowTarget(7, Color.blue);
            Assert.AreEqual(7, controller.Board.GlowingSquare, "A new target replaces the old one.");
            markers.HideTarget();
            Assert.AreEqual(-1, markers.Target);
            Assert.AreEqual(-1, controller.Board.GlowingSquare);
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
            yield return null; // The table may only now have shown the draw for the first roll; let the HUD redraw.
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

        [UnityTest]
        public IEnumerator ACardJustDrawnIsUsedFromItsOwnButton()
        {
            // The owner's wish (2026-11-12): a card that may be kept comes with "Kullan" and "Sakla" on it. A saved game
            // where the player on turn has just drawn the lookout.
            BoardData data = BoardDataLoader.LoadFromResources();
            LocalGame game = LocalGame.Create(data.Board, data.Rules, LocalGameSetup.DefaultSeats(2), new Dictionary<int, AiDifficulty>(), 7UL);
            SavedGame save = game.Save(data.Checksum);
            save.Table = save.Table.WithTurnSeconds(0); // No clock: the card is not kept for them while the test looks.
            GameStateSnapshot snapshot = save.Snapshot;
            int drawer = snapshot.Players[snapshot.CurrentPlayerIndex].Id;
            int lookout = -1;
            for (int i = 0; i < data.Board.Cards.Count && lookout < 0; i++)
            {
                if (data.Board.Cards[i].Kind == CardKind.Lookout) lookout = i;
            }

            Assert.GreaterOrEqual(lookout, 0, "The deck has no lookout.");
            snapshot.Players[snapshot.CurrentPlayerIndex].Hand.Add(lookout);
            snapshot.TurnNumber = 3;
            snapshot.Phase = GamePhase.AwaitingCardUse;
            LocalGameSetup.SetResume(save);

            SceneManager.LoadScene("Game");
            yield return null;
            yield return null;
            var controller = Object.FindAnyObjectByType<GameSceneController>();
            var hud = controller.GetComponent<GameHud>();
            while (controller.IsBusy) yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual(GamePhase.AwaitingCardUse, controller.Session.State.Phase);

            CenterNotice card = hud.DrawnCard;
            Assert.IsTrue(card.Shown, "The drawn card is not up.");
            Assert.IsTrue(card.Interactive, "The drawn card takes no clicks.");
            Assert.IsNotNull(Find(card.Rect, "Sakla"), "Keeping is not offered.");
            Assert.IsNotNull(Find(card.Rect, "Kullan"), "Using is not offered.");
            AssertMouseReaches("Kullan");
            AssertMouseReaches("Sakla");
            Assert.IsTrue(Press(card.Rect, "Kullan"));
            Assert.IsNull(controller.LastRejection);
            while (controller.IsBusy) yield return null;
            yield return null;

            GameState state = controller.Session.State;
            Assert.AreNotEqual(GamePhase.AwaitingCardUse, state.Phase, "The card still waits.");
            Assert.IsTrue(state.FindPlayer(drawer).HasLookout, "The card was not used.");
            Assert.AreEqual(0, state.FindPlayer(drawer).Hand.Count, "The used card stayed in the hand.");
            Assert.IsFalse(card.Interactive, "The card still asks.");
        }

        [UnityTest]
        public IEnumerator APlayerWhoCannotPayTicksTownsOnTheBoardAndSellsThem()
        {
            // A saved game where the first player owes the second 100 in rent with 10 in hand and three towns.
            BoardData data = BoardDataLoader.LoadFromResources();
            LocalGame game = LocalGame.Create(data.Board, data.Rules, LocalGameSetup.DefaultSeats(2), new Dictionary<int, AiDifficulty>(), 7UL);
            SavedGame save = game.Save(data.Checksum);
            save.Table = save.Table.WithTurnSeconds(0); // No clock: nothing is sold for them while the test looks.
            GameStateSnapshot snapshot = save.Snapshot;
            int debtor = snapshot.Players[snapshot.CurrentPlayerIndex].Id;
            int creditor = snapshot.Players[1 - snapshot.CurrentPlayerIndex].Id;
            var towns = new List<int>();
            for (int i = 0; i < data.Board.Count && towns.Count < 3; i++)
            {
                if (data.Board[i].Type == SquareType.Property) towns.Add(i);
            }

            foreach (int town in towns) snapshot.Owners[town] = debtor;
            snapshot.Players[snapshot.CurrentPlayerIndex].Money = 10;
            snapshot.Players[snapshot.CurrentPlayerIndex].Position = towns[0];
            snapshot.TurnNumber = 3;
            snapshot.Phase = GamePhase.AwaitingDebtPayment;
            snapshot.Debt = new DebtState(debtor, creditor, 100, MoneyReason.Rent);
            LocalGameSetup.SetResume(save);

            SceneManager.LoadScene("Game");
            yield return null;
            yield return null;
            var controller = Object.FindAnyObjectByType<GameSceneController>();
            var hud = controller.GetComponent<GameHud>();
            while (controller.IsBusy) yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual(GamePhase.AwaitingDebtPayment, controller.Session.State.Phase);

            // The one on turn stands solid; the other is see-through.
            Assert.IsFalse(controller.PawnOf(debtor).Faded, "The player on turn is faded.");
            Assert.IsTrue(controller.PawnOf(creditor).Faded, "The waiting player is not faded.");

            // The card is up with what is owed, and each town carries its tag.
            DebtSaleView sale = hud.DebtSale;
            Assert.IsTrue(sale.Shown, "The sale card did not open.");
            Assert.IsNull(Find(hud.transform, "Borcu Öde"), "Paying is offered without the money.");
            Assert.IsNull(Find(hud.transform, "Sat"), "Selling is offered with nothing ticked.");
            foreach (int town in towns)
            {
                RectTransform tag = sale.TagOf(town);
                Assert.IsNotNull(tag, $"Town {town} has no tag.");
                Assert.IsTrue(tag.gameObject.activeInHierarchy, $"Town {town}'s tag is not on screen.");
            }

            // "Otomatik Seç" only ticks the towns the automatic sale would sell (the owner's wish, 2026-11-12): nothing
            // is sold until "Sat" is pressed.
            GameState state = controller.Session.State;
            Assert.IsNull(Find(hud.transform, "Otomatik Satış"), "Selling by itself is left to the clock.");
            Assert.IsTrue(Press(hud.transform, "Otomatik Seç"));
            yield return null;
            Assert.Greater(sale.Sale.Picked.Count, 0, "Nothing was ticked.");
            Assert.AreEqual(GamePhase.AwaitingDebtPayment, controller.Session.State.Phase, "Picking sold or paid something.");
            foreach (int town in towns) Assert.AreEqual(debtor, controller.Session.State.GetOwner(town), $"Town {town} was sold by picking.");
            foreach (int town in new List<int>(sale.Sale.Picked))
            {
                Assert.IsTrue(sale.TagOf(town).GetComponentInChildren<Toggle>().isOn, $"Town {town}'s tick does not show.");
                sale.Toggle(town);
            }

            yield return null;
            Assert.AreEqual(0, sale.Sale.Picked.Count);

            // Tick towns on their tags until they cover the debt, then sell.
            var ticked = new List<int>();
            int gain = 0;
            foreach (int town in towns)
            {
                if (10 + gain >= 100) break;
                Toggle box = sale.TagOf(town).GetComponentInChildren<Toggle>();
                Assert.IsNotNull(box, $"Town {town}'s tag has no tick box.");
                box.isOn = true;
                ticked.Add(town);
                gain += GameRules.PropertySaleProceeds(data.Board, controller.Session.Rules, state, town);
                yield return null;
                Assert.IsTrue(sale.Sale.IsPicked(town));
            }

            Assume.That(10 + gain, Is.GreaterThanOrEqualTo(100), "The three first towns do not cover the test's debt.");
            AssertMouseReaches("Sat");
            int creditorMoney = state.FindPlayer(creditor).Money;
            Assert.IsTrue(Press(hud.transform, "Sat"));
            Assert.IsNull(controller.LastRejection);
            while (controller.IsBusy) yield return null;
            yield return null;

            state = controller.Session.State;
            Assert.AreNotEqual(GamePhase.AwaitingDebtPayment, state.Phase, "The debt is still open.");
            foreach (int town in ticked) Assert.AreEqual(GameState.NoOwner, state.GetOwner(town), $"Town {town} was not sold.");
            Assert.AreEqual(10 + gain - 100, state.FindPlayer(debtor).Money);
            Assert.AreEqual(creditorMoney + 100, state.FindPlayer(creditor).Money);
            Assert.IsFalse(sale.Shown, "The sale card stayed up.");
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

        /// <summary>Whether some text on screen reads exactly this.</summary>
        private static bool ShowsText(string value)
        {
            foreach (GameObject sceneRoot in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (Text text in sceneRoot.GetComponentsInChildren<Text>())
                {
                    if (text.text == value && text.gameObject.activeInHierarchy) return true;
                }
            }

            return false;
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
