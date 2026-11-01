using System;
using Lazpoly.Core.Events;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Audio;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    public class SoundCuesTests
    {
        [Test]
        public void EveryCueHasAClipInResources()
        {
            foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue)))
            {
                if (cue == SoundCue.None) continue;
                Assert.IsNotNull(Resources.Load<AudioClip>(SoundCues.ResourcePath(cue)), $"missing clip for {cue}");
            }
        }

        [Test]
        public void FileNamesAreSnakeCase()
        {
            Assert.AreEqual("dice_shake", SoundCues.FileName(SoundCue.DiceShake));
            Assert.AreEqual("ui_click", SoundCues.FileName(SoundCue.UiClick));
            Assert.AreEqual("Lazpoly/Sfx/win", SoundCues.ResourcePath(SoundCue.Win));
            Assert.IsNull(SoundCues.ResourcePath(SoundCue.None));
        }

        [Test]
        public void APurchaseSoundsOnceOnItsPayment()
        {
            Assert.AreEqual(SoundCue.Buy, SoundCues.For(new MoneyChangedEvent(1, -60, MoneyReason.PropertyPurchase)));
            Assert.AreEqual(SoundCue.None, SoundCues.For(new PropertyOwnerChangedEvent(3, GameState.NoOwner, 1)));
        }

        [Test]
        public void RentSoundsOnThePayersSideOnly()
        {
            Assert.AreEqual(SoundCue.MoneyOut, SoundCues.For(new MoneyChangedEvent(1, -50, MoneyReason.Rent)));
            Assert.AreEqual(SoundCue.None, SoundCues.For(new MoneyChangedEvent(2, 50, MoneyReason.Rent)));
        }

        [Test]
        public void EconomyActionsHaveTheirOwnSounds()
        {
            Assert.AreEqual(SoundCue.MoneyIn, SoundCues.For(new MoneyChangedEvent(1, 200, MoneyReason.PassedStart)));
            Assert.AreEqual(SoundCue.MoneyOut, SoundCues.For(new MoneyChangedEvent(1, -100, MoneyReason.Tax)));
            Assert.AreEqual(SoundCue.Build, SoundCues.For(new MoneyChangedEvent(1, -50, MoneyReason.Building)));
            Assert.AreEqual(SoundCue.Sell, SoundCues.For(new MoneyChangedEvent(1, 25, MoneyReason.BuildingSale)));
            Assert.AreEqual(SoundCue.Sell, SoundCues.For(new MoneyChangedEvent(1, 30, MoneyReason.PropertySale)));
        }

        [Test]
        public void AuctionsTradesAndTheEndSound()
        {
            Assert.AreEqual(SoundCue.Bid, SoundCues.For(new BidPlacedEvent(1, 40)));
            Assert.AreEqual(SoundCue.Gavel, SoundCues.For(new AuctionEndedEvent(3, 1, 40)));
            Assert.AreEqual(SoundCue.None, SoundCues.For(new AuctionEndedEvent(3, GameState.NoOwner, 0)));
            Assert.AreEqual(SoundCue.None, SoundCues.For(new MoneyChangedEvent(1, -40, MoneyReason.Auction)));
            Assert.AreEqual(SoundCue.Trade, SoundCues.For(new TradeResolvedEvent(TradeOutcome.Accepted)));
            Assert.AreEqual(SoundCue.None, SoundCues.For(new TradeResolvedEvent(TradeOutcome.Rejected)));
            Assert.AreEqual(SoundCue.Bankrupt, SoundCues.For(new PlayerBankruptedEvent(2, 1)));
            Assert.AreEqual(SoundCue.Win, SoundCues.For(new GameEndedEvent(1)));
        }

        [Test]
        public void DiceAndStepsAreTimedByTheirAnimations()
        {
            Assert.AreEqual(SoundCue.None, SoundCues.For(new DiceRolledEvent(1, 3, 4)));
            Assert.AreEqual(SoundCue.None, SoundCues.For(new PlayerMovedEvent(1, 0, 7, false)));
        }

        [Test]
        public void VolumesAndPitchStayInRange()
        {
            foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue)))
            {
                Assert.That(SoundCues.Volume(cue), Is.InRange(0.1f, 1f), cue.ToString());
                Assert.That(SoundCues.PitchJitter(cue), Is.InRange(0f, 0.1f), cue.ToString());
            }
        }
    }
}
