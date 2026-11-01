using Lazpoly.Core.State;
using Lazpoly.Presentation.Game;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    public class HotkeysTests
    {
        private static HotkeyContext OnTurn(GamePhase phase) => new HotkeyContext { Acting = true, Phase = phase, CanBuild = true, CanTrade = true };

        [Test]
        public void SpaceAndEnterRollOrEndTheTurnOnlyWhenThatIsOffered()
        {
            Assert.AreEqual(HotkeyAction.Roll, Hotkeys.Resolve(KeyCode.Space, OnTurn(GamePhase.WaitingForRoll)));
            Assert.AreEqual(HotkeyAction.Roll, Hotkeys.Resolve(KeyCode.Return, OnTurn(GamePhase.WaitingForRoll)));
            Assert.AreEqual(HotkeyAction.EndTurn, Hotkeys.Resolve(KeyCode.KeypadEnter, OnTurn(GamePhase.TurnEnd)));
            Assert.AreEqual(HotkeyAction.None, Hotkeys.Resolve(KeyCode.Space, OnTurn(GamePhase.AwaitingDebtPayment)), "Paying a debt takes a decision.");
            Assert.AreEqual(HotkeyAction.None, Hotkeys.Resolve(KeyCode.Space, OnTurn(GamePhase.AwaitingBuyDecision)), "Buying is never done by reflex.");
        }

        [Test]
        public void NothingMovesForSomeoneWhoIsNotOnTurnHere()
        {
            var watching = new HotkeyContext { Acting = false, Phase = GamePhase.WaitingForRoll, CanBuild = true, CanTrade = true };
            Assert.AreEqual(HotkeyAction.None, Hotkeys.Resolve(KeyCode.Space, watching));
            Assert.AreEqual(HotkeyAction.None, Hotkeys.Resolve(KeyCode.B, watching));
            Assert.AreEqual(HotkeyAction.None, Hotkeys.Resolve(KeyCode.T, watching));
            Assert.AreEqual(HotkeyAction.HowToPlay, Hotkeys.Resolve(KeyCode.H, watching), "Help is always there.");
            Assert.AreEqual(HotkeyAction.PauseMenu, Hotkeys.Resolve(KeyCode.Escape, watching));
        }

        [Test]
        public void LettersOpenTheirCardsOnlyWhileTheButtonsShow()
        {
            Assert.AreEqual(HotkeyAction.BuildCard, Hotkeys.Resolve(KeyCode.B, OnTurn(GamePhase.TurnEnd)));
            Assert.AreEqual(HotkeyAction.Trade, Hotkeys.Resolve(KeyCode.T, OnTurn(GamePhase.WaitingForRoll)));

            var noBuilding = new HotkeyContext { Acting = true, Phase = GamePhase.TurnEnd, CanBuild = false, CanTrade = true };
            Assert.AreEqual(HotkeyAction.None, Hotkeys.Resolve(KeyCode.B, noBuilding));
            var noTrade = new HotkeyContext { Acting = true, Phase = GamePhase.AwaitingTravelDecision, CanBuild = false, CanTrade = false };
            Assert.AreEqual(HotkeyAction.None, Hotkeys.Resolve(KeyCode.T, noTrade));
        }

        [Test]
        public void EscapeClosesWhatIsOpenAndOtherwiseOpensTheMenu()
        {
            Assert.AreEqual(HotkeyAction.PauseMenu, Hotkeys.Resolve(KeyCode.Escape, OnTurn(GamePhase.WaitingForRoll)));
            Assert.AreEqual(HotkeyAction.Close, Hotkeys.Resolve(KeyCode.Escape, new HotkeyContext { PauseOpen = true }));
            Assert.AreEqual(HotkeyAction.Close, Hotkeys.Resolve(KeyCode.Escape, new HotkeyContext { ClosableOpen = true, Acting = true }));
            Assert.AreEqual(HotkeyAction.None, Hotkeys.Resolve(KeyCode.Escape, new HotkeyContext { BlockingOpen = true, Acting = true }),
                "The city card must be answered, not dismissed.");
        }

        [Test]
        public void OpenWindowsSwallowTheOtherKeys()
        {
            var paused = new HotkeyContext { PauseOpen = true, Acting = true, Phase = GamePhase.WaitingForRoll, CanBuild = true, CanTrade = true };
            Assert.AreEqual(HotkeyAction.None, Hotkeys.Resolve(KeyCode.Space, paused));
            Assert.AreEqual(HotkeyAction.None, Hotkeys.Resolve(KeyCode.H, paused));
            var cityCard = new HotkeyContext { BlockingOpen = true, Acting = true, Phase = GamePhase.AwaitingBuyDecision };
            Assert.AreEqual(HotkeyAction.None, Hotkeys.Resolve(KeyCode.Return, cityCard));
        }

        [Test]
        public void TypingInAFieldIsNeverACommand()
        {
            var typing = new HotkeyContext { Typing = true, Acting = true, Phase = GamePhase.WaitingForRoll, CanBuild = true, CanTrade = true };
            foreach (KeyCode key in Hotkeys.Keys) Assert.AreEqual(HotkeyAction.None, Hotkeys.Resolve(key, typing), key.ToString());
        }
    }
}
