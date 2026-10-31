using System;
using System.Collections.Generic;
using Lazpoly.App.Achievements;
using Lazpoly.App.Presence;
using Lazpoly.Core.Board;
using Lazpoly.Core.Events;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;
using NUnit.Framework;

namespace Lazpoly.App.Tests
{
    public class AchievementTrackerTests
    {
        private const int Me = 1;
        private const int Computer = 2;
        private const int Friend = 3;
        private const int A1 = 1, A2 = 2;

        private static readonly BoardDefinition Board = new BoardDefinition(new[]
        {
            SquareDefinition.Start("Start"),
            SquareDefinition.Property("A1", 100, 10, 1, 50, new[] { 50, 150, 400, 550, 700 }),
            SquareDefinition.Property("A2", 100, 10, 1, 50, new[] { 50, 150, 400, 550, 700 }),
            SquareDefinition.Property("Solo", 150, 15, 0),
        });

        private static readonly RulesConfig Rules = new RulesConfig(1000, 0, 6, 3, 2, 10, 50);

        private GameState _state;
        private AchievementTracker _tracker;

        private void NewGame(bool online = false)
        {
            _state = GameState.CreateInitial(Board, Rules, new[] { new PlayerSeat(Me, "Ben"), new PlayerSeat(Computer, "AI"), new PlayerSeat(Friend, "Can") });
            _tracker = new AchievementTracker(Board, id => id == Me, id => id == Computer, online);
        }

        private IReadOnlyList<string> Play(params GameEvent[] events)
        {
            foreach (GameEvent gameEvent in events) GameStateReducer.Apply(_state, gameEvent);
            return _tracker.Observe(events, _state);
        }

        [Test]
        public void OwningAWholeGroupCountsOnlyForMe()
        {
            NewGame();
            CollectionAssert.IsEmpty(Play(new PropertyOwnerChangedEvent(A1, GameState.NoOwner, Computer), new PropertyOwnerChangedEvent(A2, GameState.NoOwner, Computer)));

            NewGame();
            CollectionAssert.IsEmpty(Play(new PropertyOwnerChangedEvent(A1, GameState.NoOwner, Me)));
            CollectionAssert.AreEqual(new[] { AchievementCatalog.FullGroup }, Play(new PropertyOwnerChangedEvent(A2, GameState.NoOwner, Me)));
        }

        [Test]
        public void EachAchievementIsReportedOnce()
        {
            NewGame();
            Play(new PropertyOwnerChangedEvent(A1, GameState.NoOwner, Me), new PropertyOwnerChangedEvent(A2, GameState.NoOwner, Me));

            CollectionAssert.AreEqual(new[] { AchievementCatalog.FirstHotel }, Play(new BuildingLevelChangedEvent(A1, 5)));
            CollectionAssert.IsEmpty(Play(new BuildingLevelChangedEvent(A2, 5)));
            CollectionAssert.IsEmpty(Play(new BuildingLevelChangedEvent(A2, 4)), "Selling down is not building a hotel.");
        }

        [Test]
        public void AuctionTradeAndBankruptcy()
        {
            NewGame();
            Play(new AuctionStartedEvent(3));
            CollectionAssert.AreEqual(new[] { AchievementCatalog.AuctionWin }, Play(new AuctionEndedEvent(3, Me, 80)));

            Play(new PropertyOwnerChangedEvent(A1, GameState.NoOwner, Friend));
            var offer = new TradeOffer(Friend, Me, 0, 50, new[] { A1 }, Array.Empty<int>());
            CollectionAssert.IsEmpty(Play(new TradeProposedEvent(offer)));
            CollectionAssert.AreEqual(new[] { AchievementCatalog.TradeDone }, Play(new TradeResolvedEvent(TradeOutcome.Accepted)));

            CollectionAssert.IsEmpty(Play(new PlayerBankruptedEvent(Computer, GameState.NoOwner)), "Ruined by the bank, not by me.");
            CollectionAssert.AreEqual(new[] { AchievementCatalog.BankruptRival }, Play(new PlayerBankruptedEvent(Friend, Me)));
        }

        [Test]
        public void RejectedTradesAndOthersTradesDoNotCount()
        {
            NewGame();
            var mine = new TradeOffer(Me, Friend, 10, 0, Array.Empty<int>(), Array.Empty<int>());
            Play(new TradeProposedEvent(mine));
            CollectionAssert.IsEmpty(Play(new TradeResolvedEvent(TradeOutcome.Rejected)));

            var theirs = new TradeOffer(Computer, Friend, 10, 0, Array.Empty<int>(), Array.Empty<int>());
            Play(new TradeProposedEvent(theirs));
            CollectionAssert.IsEmpty(Play(new TradeResolvedEvent(TradeOutcome.Accepted)));
        }

        [Test]
        public void WinningAgainstComputersOnline()
        {
            NewGame(online: true);
            CollectionAssert.AreEquivalent(
                new[] { AchievementCatalog.FirstWin, AchievementCatalog.OnlineWin, AchievementCatalog.BeatComputers },
                Play(new GameEndedEvent(Me)));

            NewGame();
            CollectionAssert.IsEmpty(Play(new GameEndedEvent(Friend)));
        }

        [Test]
        public void HoldingALotOfMoney()
        {
            NewGame();
            CollectionAssert.IsEmpty(Play(new MoneyChangedEvent(Friend, AchievementCatalog.TycoonMoney, MoneyReason.Rent)));
            CollectionAssert.AreEqual(new[] { AchievementCatalog.Tycoon },
                Play(new MoneyChangedEvent(Me, AchievementCatalog.TycoonMoney - 1000, MoneyReason.Rent)));
        }

        [Test]
        public void CatalogIdsAreUniqueAndFitSteam()
        {
            var ids = new HashSet<string>();
            foreach (AchievementDefinition definition in AchievementCatalog.All)
            {
                Assert.IsTrue(ids.Add(definition.Id), $"{definition.Id} is listed twice.");
                StringAssert.IsMatch("^[A-Z0-9_]{1,128}$", definition.Id);
                Assert.IsNotEmpty(definition.Title);
                Assert.IsNotEmpty(definition.Description);
            }
        }

        [Test]
        public void PresenceLinesAreShort()
        {
            Assert.AreEqual("Bilgisayarlara karşı, 12. tur, 4 oyuncu", PresenceText.InGame(false, true, 12, 4));
            Assert.AreEqual("Çevrim içi oyunda, 3. tur, 2 oyuncu", PresenceText.InGame(true, true, 3, 2));
            Assert.AreEqual("Oda kuruyor (2/8)", PresenceText.InRoom(2, 8));
            Assert.LessOrEqual(PresenceText.InGame(true, false, 999, 8).Length, 64);
        }
    }
}
