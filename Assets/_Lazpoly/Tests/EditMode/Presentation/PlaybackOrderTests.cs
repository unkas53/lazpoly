using System.Collections.Generic;
using Lazpoly.Core.Events;
using Lazpoly.Presentation.Game;
using Lazpoly.Presentation.Texts;
using NUnit.Framework;

namespace Lazpoly.Presentation.Tests
{
    /// <summary>Faz 29: the order the table shows a trade, a third double and a bankruptcy in.</summary>
    public class PlaybackOrderTests
    {
        [SetUp]
        public void SetUp() => Lang.Set(Lazpoly.Infra.Settings.Language.Turkish);

        [Test]
        public void AnAcceptedTradesCardComesBeforeItsMoneyAndTowns()
        {
            var proposed = new PhaseChangedEvent(Lazpoly.Core.State.GamePhase.AwaitingTradeResponse);
            var paid = new MoneyChangedEvent(1, -50000, MoneyReason.Trade);
            var got = new MoneyChangedEvent(2, 50000, MoneyReason.Trade);
            var town = new PropertyOwnerChangedEvent(3, 2, 1);
            var result = new TradeResolvedEvent(TradeOutcome.Accepted);
            var back = new PhaseChangedEvent(Lazpoly.Core.State.GamePhase.WaitingForRoll);

            List<GameEvent> order = PlaybackOrder.For(new GameEvent[] { proposed, paid, got, town, result, back });
            CollectionAssert.AreEqual(new GameEvent[] { proposed, result, paid, got, town, back }, order,
                "the card first, then the money and the town change hands (the owner's wish)");
        }

        [Test]
        public void OtherBatchesKeepTheirOrder()
        {
            var town = new PropertyOwnerChangedEvent(3, 2, 1);
            var rejected = new TradeResolvedEvent(TradeOutcome.Rejected);
            CollectionAssert.AreEqual(new GameEvent[] { town, rejected }, PlaybackOrder.For(new GameEvent[] { town, rejected }), "only an accepted trade moves");

            // A town bought by a third player before the trade stays where it was.
            var bought = new PropertyOwnerChangedEvent(5, Lazpoly.Core.State.GameState.NoOwner, 3);
            var paid = new MoneyChangedEvent(1, -100, MoneyReason.Trade);
            var got = new MoneyChangedEvent(2, 100, MoneyReason.Trade);
            var result = new TradeResolvedEvent(TradeOutcome.Accepted);
            CollectionAssert.AreEqual(new GameEvent[] { bought, result, paid, got }, PlaybackOrder.For(new GameEvent[] { bought, paid, got, result }));

            var rent = new MoneyChangedEvent(1, -100, MoneyReason.Rent);
            CollectionAssert.AreEqual(new GameEvent[] { rent, result }, PlaybackOrder.For(new GameEvent[] { rent, result }), "other money before a result stays where it was");
            Assert.IsEmpty(PlaybackOrder.For(null));
        }

        [Test]
        public void AThirdDoubleIsTheDoubleFollowedByAJumpToTheCell()
        {
            var third = new DiceRolledEvent(1, 4, 4);
            var toJail = new PlayerMovedEvent(1, 12, 8, false, MoveKind.Jump);
            Assert.IsTrue(GameSceneController.SendsToJail(third, toJail, 8));
            Assert.IsFalse(GameSceneController.SendsToJail(third, new PlayerMovedEvent(1, 12, 20, false), 8), "a double that walks");
            Assert.IsFalse(GameSceneController.SendsToJail(new DiceRolledEvent(1, 4, 3), toJail, 8), "not a double");
            Assert.IsFalse(GameSceneController.SendsToJail(third, new PlayerMovedEvent(2, 12, 8, false, MoveKind.Jump), 8), "another player");
            Assert.IsFalse(GameSceneController.SendsToJail(third, toJail, -1), "a board without a jail");
            Assert.IsFalse(GameSceneController.SendsToJail(third, null, 8));
        }

        [Test]
        public void TheThirdDoubleCardSpeaksToTheRollerAndAboutThemToOthers()
        {
            Assert.AreEqual("Art arda 3 kez çift attın, doğrudan hapse gidiyorsun.", GameHud.ThirdDoubleText(null));
            Assert.AreEqual("Ayşe art arda 3 kez çift attı, doğrudan hapse gidiyor.", GameHud.ThirdDoubleText("Ayşe"));
            Lang.Set(Lazpoly.Infra.Settings.Language.English);
            Assert.AreEqual("Ayşe rolled three doubles in a row and goes straight to jail.", GameHud.ThirdDoubleText("Ayşe"));
            Lang.Set(Lazpoly.Infra.Settings.Language.Turkish);
        }
    }
}
