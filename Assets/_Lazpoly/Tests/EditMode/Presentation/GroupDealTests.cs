using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Events;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Game;
using NUnit.Framework;

namespace Lazpoly.Presentation.Tests
{
    public class GroupDealTests
    {
        private static readonly int[] Rents = { 50, 150, 400, 550, 700 };

        private static readonly BoardDefinition Board = new BoardDefinition(new[]
        {
            SquareDefinition.Start("Başlangıç"),
            SquareDefinition.Property("Rio de Janeiro", 120, 8, 2, 50, Rents),
            SquareDefinition.Property("Buenos Aires", 120, 8, 2, 50, Rents),
            SquareDefinition.Property("Lima", 140, 10, 2, 50, Rents),
            SquareDefinition.Resort("Bodrum", 200, 50, 9),
            SquareDefinition.Property("Paris", 280, 26, 7, 95, Rents),
            SquareDefinition.Property("Londra", 300, 28, 7, 95, Rents),
            SquareDefinition.Resort("Bali", 200, 50, 9),
        });

        private static Dictionary<int, int> Owners(params int[] owners)
        {
            var shown = new Dictionary<int, int>();
            for (int i = 0; i < owners.Length; i++) shown[i] = owners[i];
            return shown;
        }

        private const int None = GameState.NoOwner;

        [Test]
        public void TheCardStaysFourSecondsAndFadesInTheLastHalfSecond()
        {
            Assert.AreEqual(4f, GroupDeal.Seconds);
            Assert.AreEqual(1f, CenterNotice.Alpha(3.5f, GroupDeal.Seconds), 1e-4f);
            Assert.AreEqual(0.5f, CenterNotice.Alpha(3.75f, GroupDeal.Seconds), 1e-4f);
            Assert.IsTrue(CenterNotice.Showing(3.99f, GroupDeal.Seconds));
            Assert.IsFalse(CenterNotice.Showing(4f, GroupDeal.Seconds), "Gone at four seconds, when the table goes on.");
            Assert.AreEqual(1f, CenterNotice.Alpha(4.5f), 1e-4f, "The other cards still stay five seconds.");
        }

        [Test]
        public void TheLastTownOfAGroupCompletesItForItsNewOwner()
        {
            Dictionary<int, int> owners = Owners(None, 1, 1, None, None, None, None, None);
            Assert.IsTrue(GroupDeal.Completes(Board, owners, 3, 1), "Lima is the last of South America.");
            Assert.IsFalse(GroupDeal.Completes(Board, owners, 3, 2), "Someone else buying it completes nothing.");
            Assert.IsFalse(GroupDeal.Completes(Board, owners, 3, None), "Going back to the bank completes nothing.");
            Assert.IsFalse(GroupDeal.Completes(Board, Owners(None, 1, None, None, None, None, None, None), 3, 1), "One town is still the bank's.");
            Assert.IsFalse(GroupDeal.Completes(Board, Owners(None, 1, 1, 1, None, None, None, None), 3, 1), "Already theirs: nothing new.");
        }

        [Test]
        public void ATradeThatHandsOverAWholeGroupCompletesItOnceOnItsLastTown()
        {
            Dictionary<int, int> owners = Owners(None, None, None, None, None, 2, 2, None);
            int cards = 0;
            foreach (int town in new[] { 5, 6 })
            {
                if (GroupDeal.Completes(Board, owners, town, 1)) cards++;
                owners[town] = 1;
            }

            Assert.AreEqual(1, cards);
        }

        [Test]
        public void HolidayResortsHaveNoCard()
        {
            Assert.IsFalse(GroupDeal.Completes(Board, Owners(None, None, None, None, 1, None, None, None), 7, 1));
        }

        [Test]
        public void TheCardNamesThePlayerAndEveryTownOfTheGroup()
        {
            Assert.AreEqual("Bilgisayar 3 yoğun bir mesai harcayarak Rio de Janeiro, Buenos Aires ve Lima arasında ticaret anlaşması imzaladı",
                GroupDeal.Text("Bilgisayar 3", GroupDeal.TownNames(Board, 2)));
            Assert.AreEqual("Ayşe yoğun bir mesai harcayarak Paris ve Londra arasında ticaret anlaşması imzaladı",
                GroupDeal.Text("Ayşe", GroupDeal.TownNames(Board, 7)));
            Assert.AreEqual("Bu şehirlerin kiraları artık 2 katı.", GroupDeal.RentText(2));
        }

        [Test]
        public void TheCardWaitsForTheRestOfATradeAndForBuildingsBoughtWithTheTown()
        {
            Assert.IsTrue(GameSceneController.GroupDealWaits(new PropertyOwnerChangedEvent(6, 2, 1)));
            Assert.IsTrue(GameSceneController.GroupDealWaits(new TradeResolvedEvent(TradeOutcome.Accepted)));
            Assert.IsTrue(GameSceneController.GroupDealWaits(new BuildingLevelChangedEvent(3, 1)));
            Assert.IsFalse(GameSceneController.GroupDealWaits(new PhaseChangedEvent(GamePhase.TurnEnd)));
            Assert.IsFalse(GameSceneController.GroupDealWaits(null));
        }
    }
}
