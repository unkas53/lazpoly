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
using Lazpoly.Presentation.Effects;
using Lazpoly.Presentation.Game;
using Lazpoly.Presentation.Setup;
using Lazpoly.Presentation.Tabletop;
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

            // The home screen: play at once, rooms, the set-up games on the rail and the windows' buttons at the top right, all within reach of the mouse.
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

        [UnityTest]
        public IEnumerator ARideIsPickedOnTheCardOverTheLawnAndTheViewStaysStill()
        {
            // The owner's wish (2026-11-17): picking where the bus goes opens no panel at the bottom and does not draw the
            // view back. A saved game where the player on turn starts the turn at the bus station.
            BoardData data = BoardDataLoader.LoadFromResources();
            LocalGame game = LocalGame.Create(data.Board, data.Rules, LocalGameSetup.DefaultSeats(2), new Dictionary<int, AiDifficulty>(), 7UL);
            SavedGame save = game.Save(data.Checksum);
            save.Table = save.Table.WithTurnSeconds(0); // No clock: the ride is not declined for them while the test looks.
            GameStateSnapshot snapshot = save.Snapshot;
            int rider = snapshot.Players[snapshot.CurrentPlayerIndex].Id;
            int station = -1;
            for (int i = 0; i < data.Board.Count && station < 0; i++)
            {
                if (data.Board[i].Type == SquareType.Travel) station = i;
            }

            Assert.GreaterOrEqual(station, 0, "The board has no bus station.");
            snapshot.Players[snapshot.CurrentPlayerIndex].Position = station;
            snapshot.TurnNumber = 3;
            snapshot.Phase = GamePhase.AwaitingTravelDecision;
            LocalGameSetup.SetResume(save);

            SceneManager.LoadScene("Game");
            yield return null;
            yield return null;
            var controller = Object.FindAnyObjectByType<GameSceneController>();
            var hud = controller.GetComponent<GameHud>();
            while (controller.IsBusy) yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual(GamePhase.AwaitingTravelDecision, controller.Session.State.Phase);

            // The card is up over the lawn with the station's name; going is not offered before a square is picked.
            BoardPickView pick = hud.Pick;
            Assert.IsTrue(pick.Shown, "The pick card did not open.");
            Assert.AreEqual(data.Board[station].Name, pick.Pick.Title);
            Assert.IsFalse(pick.Pick.Ready);
            Assert.AreEqual(-1, pick.PickedSquare);
            Assert.IsNull(Find(hud.transform, "Git"), "Going is offered with no square picked.");
            Assert.IsNull(Find(hud.transform, "ZAR AT"), "Rolling is offered under the pick card.");
            AssertMouseReaches("Burada kal");

            // The view is the framed one: the whole board at its usual size, whatever the game asks.
            Camera camera = Camera.main;
            float framed = BoardCameraFraming.OrthographicSize(controller.Board.HalfExtent, BoardView.PlateThickness, BoardCameraRig.Pitch,
                Screen.width, Screen.height, hud.ReservedSidePixels);
            float waited = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - waited < 1f) yield return null; // The rig eases by the real clock.
            Assert.AreEqual(framed, camera.orthographicSize, framed * 0.01f, "The view is drawn back (or in) while a ride is picked.");
            Vector3 stood = camera.transform.position;
            float size = camera.orthographicSize;

            // A click on a square the bus may go to picks it: its plate stands over it and the view stays where it is.
            GameState state = controller.Session.State;
            int destination = -1;
            for (int step = 1; step < data.Board.Count && destination < 0; step++)
            {
                int square = (station + step) % data.Board.Count;
                bool free = data.Board[square].Type == SquareType.Property && state.GetOwner(square) == GameState.NoOwner;
                if (free && GameRules.IsRideDestination(data.Board, state, state.FindPlayer(rider), square)) destination = square;
            }

            Assert.GreaterOrEqual(destination, 0, "The bus has nowhere to go.");
            hud.ClickSquare(state, station);
            yield return null;
            Assert.AreEqual(-1, pick.PickedSquare, "The station itself was picked.");
            hud.ClickSquare(state, destination);
            waited = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - waited < 0.5f) yield return null;
            Assert.AreEqual(destination, pick.PickedSquare, "The picked square has no plate.");
            Assert.IsTrue(pick.Pick.Ready);
            Assert.Less(Vector3.Distance(stood, camera.transform.position), 0.01f, "The view moved towards the picked square.");
            Assert.AreEqual(size, camera.orthographicSize, size * 0.001f, "The view changed its size for the pick.");

            // "Git" takes the bus there and the card goes away.
            AssertMouseReaches("Git");
            int money = state.FindPlayer(rider).Money;
            Assert.IsTrue(Press(hud.transform, "Git"));
            Assert.IsNull(controller.LastRejection);
            float left = Time.realtimeSinceStartup;
            while (controller.IsBusy)
            {
                Assert.Less(Time.realtimeSinceStartup - left, 60f, "The ride never ended.");
                yield return null;
            }

            yield return null;
            state = controller.Session.State;
            Assert.AreEqual(destination, state.FindPlayer(rider).Position, "The bus did not go to the picked square.");
            // A ride past Start also pays its bonus, so the fare is the least that is gone on a ride that does not pass it.
            if (destination > station) Assert.AreEqual(money - data.Board[station].Fee, state.FindPlayer(rider).Money, "The fare was not paid.");
            Assert.IsFalse(pick.Shown, "The pick card stayed up.");
            Assert.AreEqual(-1, pick.PickedSquare);
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

        [UnityTest]
        public IEnumerator ASoldBuildingSpringsAwayAndATornDownOneFallsInAndNothingIsLeftBehind()
        {
            // The owner's wish (2026-11-17): a fine animation for a town's buildings when it is sold and when it is torn
            // down. The board is shown a house on a town nobody owns, so the game itself is not touched.
            LocalGameSetup.SetSeats(LocalGameSetup.DefaultSeats(2));
            SceneManager.LoadScene("Game");
            yield return null;
            yield return null;
            var controller = Object.FindAnyObjectByType<GameSceneController>();
            float started = Time.realtimeSinceStartup;
            while (controller.IsBusy && Time.realtimeSinceStartup - started < 30f) yield return null;
            BoardView board = controller.Board;
            BoardDefinition squares = controller.Session.Board;
            int town = -1;
            // The last town of the board: no pawn reaches it from the start in the time the test takes.
            for (int i = squares.Count - 1; i > 0 && town < 0; i--)
            {
                if (squares[i].Type == SquareType.Property && squares[i].IsDevelopable && controller.Session.State.GetOwner(i) == GameState.NoOwner) town = i;
            }

            Assert.GreaterOrEqual(town, 0, "The board has no free town.");
            Time.timeScale = 2f; // Slow enough for the test to see a leaving on its way, whatever the frame takes.

            // Sold with its town: the house springs up off its tile into a puff, and neither it nor the plot stays.
            board.SetOwner(town, Color.red);
            board.SetBuildingLevel(town, 2);
            yield return null;
            Transform house = board.BuildingPieces(town)[1].transform;
            Vector3 rest = house.localPosition, size = house.localScale;
            board.SetBuildingLevel(town, 0, animate: true, leaving: Leaving.Sold);
            board.ClearOwner(town, animate: true, leaving: Leaving.Sold);
            Assert.AreEqual(1, board.LeavingCount, "One building leaves: the plot under it does not.");
            Assert.IsTrue(house.gameObject.activeSelf, "The sold house is gone at once.");
            Assert.IsFalse(board.MarkerShown(town), "The plot shows under a house that is leaving with its town.");
            LeavingBurst puff = board.GetComponentInChildren<LeavingBurst>();
            Assert.IsNotNull(puff, "A sold house leaves without its puff and dollar signs.");
            Assert.AreEqual(Leaving.Sold, puff.How);
            Assert.AreEqual(SoundCue.BuildingSold, AudioPlayer.LastCue, "A sold house leaves without the till's sound.");
            float highest = 0f;
            started = Time.realtimeSinceStartup;
            while (board.LeavingCount > 0 && Time.realtimeSinceStartup - started < 10f)
            {
                highest = Mathf.Max(highest, house.localPosition.y - rest.y);
                yield return null;
            }

            Assert.AreEqual(0, board.LeavingCount, "The sold house never finished leaving.");
            Assert.Greater(highest, 0f, "The sold house never left its tile.");
            foreach (GameObject piece in board.BuildingPieces(town)) Assert.IsFalse(piece.activeSelf, "A building stays on a sold town.");
            Assert.IsFalse(board.MarkerShown(town), "The plot stays on a sold town.");
            Assert.Less((house.localPosition - rest).magnitude, 1e-3f, "The house's model is not put back where it rests.");
            Assert.Less((house.localScale - size).magnitude, 1e-3f, "The house's model does not get its size back.");

            // A floor torn off by a card: the house falls in where it stands, dust rises, and the lower house then stands
            // with no plot.
            board.SetOwner(town, Color.red);
            board.SetBuildingLevel(town, 2);
            yield return null;
            Transform lower = board.BuildingPieces(town)[0].transform;
            board.SetBuildingLevel(town, 1, animate: true, leaving: Leaving.Demolished);
            Assert.AreEqual(1, board.LeavingCount);
            Assert.IsTrue(house.gameObject.activeSelf, "The torn-down house is gone at once.");
            Assert.IsFalse(lower.gameObject.activeSelf, "The lower house stands inside the one still falling.");
            bool dust = false;
            foreach (LeavingBurst burst in board.GetComponentsInChildren<LeavingBurst>()) dust |= burst.How == Leaving.Demolished;
            Assert.IsTrue(dust, "A torn-down house falls without dust.");
            Assert.AreEqual(SoundCue.BuildingFall, AudioPlayer.LastCue, "A torn-down house falls without the sound of its collapse.");
            float lowest = 1f, deepest = 0f;
            highest = 0f;
            started = Time.realtimeSinceStartup;
            while ((board.LeavingCount > 0 || !lower.gameObject.activeSelf) && Time.realtimeSinceStartup - started < 10f)
            {
                highest = Mathf.Max(highest, house.localPosition.y - rest.y);
                if (house.gameObject.activeSelf) lowest = Mathf.Min(lowest, house.localScale.y / size.y);
                deepest = Mathf.Max(deepest, rest.y - house.localPosition.y);
                yield return null;
            }

            Assert.AreEqual(0, board.LeavingCount, "The torn-down house never finished falling.");
            Assert.AreEqual(0f, highest, 1e-4f, "The torn-down house left the ground.");
            Assert.Less(lowest, 0.8f, "The torn-down house was never crushed.");
            Assert.Greater(deepest, 0f, "The torn-down house never went down into its dust.");
            Assert.IsFalse(house.gameObject.activeSelf, "The torn-down house stays.");
            Assert.IsTrue(lower.gameObject.activeSelf, "The lower house does not stand after the floor is gone.");
            Assert.IsFalse(board.MarkerShown(town), "The plot shows beside a house.");

            // The bursts clear themselves away; the board is as it was.
            started = Time.realtimeSinceStartup;
            while (board.GetComponentInChildren<LeavingBurst>() != null && Time.realtimeSinceStartup - started < 10f) yield return null;
            Assert.IsNull(board.GetComponentInChildren<LeavingBurst>(), "A burst never clears itself away.");
            board.SetBuildingLevel(town, 0);
            board.ClearOwner(town, animate: false);
            Assert.IsFalse(board.MarkerShown(town));
        }

        [UnityTest]
        public IEnumerator ABuildingGoesUpFromItsTileInDustToTheHammerAndCanLeaveBeforeItStands()
        {
            // The owner's wish (2026-11-18): a building moment in the manner of the sale and the tearing down (dust, the
            // storey rising, in time with the hammer). The board is shown houses on a town nobody owns, so the game
            // itself is not touched.
            LocalGameSetup.SetSeats(LocalGameSetup.DefaultSeats(2));
            SceneManager.LoadScene("Game");
            yield return null;
            yield return null;
            var controller = Object.FindAnyObjectByType<GameSceneController>();
            float started = Time.realtimeSinceStartup;
            while (controller.IsBusy && Time.realtimeSinceStartup - started < 30f) yield return null;
            BoardView board = controller.Board;
            BoardDefinition squares = controller.Session.Board;
            int town = -1;
            // The last town of the board: no pawn reaches it from the start in the time the test takes.
            for (int i = squares.Count - 1; i > 0 && town < 0; i--)
            {
                if (squares[i].Type == SquareType.Property && squares[i].IsDevelopable && controller.Session.State.GetOwner(i) == GameState.NoOwner) town = i;
            }

            Assert.GreaterOrEqual(town, 0, "The board has no free town.");
            Time.timeScale = 2f; // Slow enough for the test to see a building on its way up, whatever the frame takes.

            // A second storey: the new house comes up from the tile in its dust while the old one ducks away under it.
            board.SetOwner(town, Color.red);
            board.SetBuildingLevel(town, 1);
            yield return null;
            Transform old = board.BuildingPieces(town)[0].transform, house = board.BuildingPieces(town)[1].transform;
            Vector3 oldSize = old.localScale, size = house.localScale, rest = house.localPosition;
            int hammers = 0;
            bool grand = false;
            board.RisingStarted += hotel =>
            {
                hammers++;
                grand = hotel;
            };
            board.SetBuildingLevel(town, 2, animate: true);
            Assert.AreEqual(1, board.RisingCount, "The new house does not go up.");
            Assert.AreEqual(1, hammers, "The scene is not told the house starts to rise.");
            Assert.IsFalse(grand, "A house goes up as a hotel does.");
            Assert.AreEqual(SoundCue.Build, AudioPlayer.LastCue, "A house goes up without the hammer.");
            Assert.IsTrue(house.gameObject.activeSelf);
            Assert.Less(house.localScale.y, size.y * 0.2f, "The new house does not start from the tile.");
            Assert.IsTrue(old.gameObject.activeSelf, "The old house is gone at once rather than ducking away.");
            RisingBurst dust = board.GetComponentInChildren<RisingBurst>();
            Assert.IsNotNull(dust, "A house goes up without dust.");
            Assert.IsFalse(dust.Grand);
            float tallest = 0f, moved = 0f;
            started = Time.realtimeSinceStartup;
            while (board.RisingCount > 0 && Time.realtimeSinceStartup - started < 10f)
            {
                tallest = Mathf.Max(tallest, house.localScale.y / size.y);
                moved = Mathf.Max(moved, (house.localPosition - rest).magnitude);
                yield return null;
            }

            Assert.AreEqual(0, board.RisingCount, "The house never finished going up.");
            Assert.Greater(tallest, 0.9f, "The house was never seen near its height on its way.");
            Assert.Less(moved, 1e-3f, "A rising house leaves its place: it grows from its foot.");
            Assert.Less((house.localScale - size).magnitude, 1e-3f, "The house does not stand at its size.");
            Assert.IsFalse(old.gameObject.activeSelf, "The old house stays inside the new one.");
            Assert.Less((old.localScale - oldSize).magnitude, 1e-3f, "The old house's model does not get its size back.");
            Assert.IsFalse(board.MarkerShown(town), "The plot shows beside a house.");

            // A hotel: its own sound, and a greater dust. Sold before it stands, it is whole at once, leaves as any
            // building does, and its model is put back at its size.
            int last = SquareDefinition.MaxBuildingLevel - 1;
            Transform hotelPiece = board.BuildingPieces(town)[last].transform;
            Vector3 hotelSize = hotelPiece.localScale;
            board.SetBuildingLevel(town, SquareDefinition.MaxBuildingLevel, animate: true);
            Assert.AreEqual(2, hammers);
            Assert.IsTrue(grand, "The scene is not told a hotel is going up.");
            Assert.AreEqual(SoundCue.Hotel, AudioPlayer.LastCue, "A hotel goes up without its own sound.");
            bool greater = false;
            foreach (RisingBurst burst in board.GetComponentsInChildren<RisingBurst>()) greater |= burst.Grand;
            Assert.IsTrue(greater, "A hotel goes up in a house's dust.");
            yield return null;
            Assert.AreEqual(1, board.RisingCount, "The hotel stands at once.");
            board.SetBuildingLevel(town, 0, animate: true, leaving: Leaving.Sold);
            board.ClearOwner(town, animate: true, leaving: Leaving.Sold);
            Assert.AreEqual(0, board.RisingCount, "A hotel that is leaving is still going up.");
            Assert.AreEqual(1, board.LeavingCount, "A hotel sold as it goes up does not leave.");
            started = Time.realtimeSinceStartup;
            while ((board.LeavingCount > 0 || board.GetComponentInChildren<RisingBurst>() != null) && Time.realtimeSinceStartup - started < 10f) yield return null;
            Assert.AreEqual(0, board.LeavingCount, "The hotel never finished leaving.");
            Assert.IsNull(board.GetComponentInChildren<RisingBurst>(), "The dust never clears itself away.");
            foreach (GameObject piece in board.BuildingPieces(town)) Assert.IsFalse(piece.activeSelf, "A building stays on a sold town.");
            Assert.Less((hotelPiece.localScale - hotelSize).magnitude, 1e-3f, "The hotel's model does not get its size back.");
            Assert.Less((house.localScale - size).magnitude, 1e-3f, "The house under the hotel does not get its size back.");
            Assert.IsFalse(board.MarkerShown(town), "The plot stays on a sold town.");
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
