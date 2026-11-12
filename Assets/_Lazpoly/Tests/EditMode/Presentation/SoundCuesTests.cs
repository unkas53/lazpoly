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
        public void TheJailsGateIsHeardOpeningAndStrikingShut()
        {
            Assert.AreEqual("jail_open", SoundCues.FileName(SoundCue.JailOpen));
            Assert.AreEqual("jail_shut", SoundCues.FileName(SoundCue.JailShut));
            Assert.AreEqual(SoundCue.JailOpen, SoundCues.ForJailGate(true));
            Assert.AreEqual(SoundCue.JailShut, SoundCues.ForJailGate(false));
            foreach (SoundCue cue in new[] { SoundCue.JailOpen, SoundCue.JailShut })
            {
                Assert.AreEqual(1, SoundCues.Variants(cue));
                Assert.AreEqual(0f, SoundCues.PitchJitter(cue), "the same iron gate every time");
                Assert.That(SoundCues.Volume(cue), Is.InRange(0.4f, 0.7f), "a detail of the table, under the big moments");
            }

            // The game waits for neither: short recordings, the bolt and hinges, then the strike and the lock.
            AudioClip open = Resources.Load<AudioClip>(SoundCues.ResourcePath(SoundCue.JailOpen));
            AudioClip shut = Resources.Load<AudioClip>(SoundCues.ResourcePath(SoundCue.JailShut));
            Assert.IsNotNull(open, "run Tools/Audio/make_sfx_kenney.py jail_open");
            Assert.IsNotNull(shut, "run Tools/Audio/make_sfx_kenney.py jail_shut");
            Assert.That(open.length, Is.InRange(0.2f, 0.7f));
            Assert.That(shut.length, Is.InRange(Pawns.PawnView.JailStrikeAt + 0.3f, 1.2f), "the strike, then the lock");

            // The shutting sound starts so that its strike falls as the gate reaches its frame.
            float gate = Pawns.PawnView.JailGateSeconds, hop = Pawns.PawnView.JailHopSeconds, strike = Pawns.PawnView.JailStrikeAt;
            Assert.IsFalse(Pawns.PawnView.GateStrikeDue(0f, hop, 1f), "not while the piece is still coming out");
            Assert.IsFalse(Pawns.PawnView.GateStrikeDue(1f - strike / hop - 0.02f, hop, 1f));
            Assert.IsTrue(Pawns.PawnView.GateStrikeDue(1f - strike / hop + 0.02f, hop, 1f));
            Assert.IsTrue(Pawns.PawnView.GateStrikeDue(1f, hop, 1f));
            Assert.IsTrue(Pawns.PawnView.GateStrikeDue(1f - strike / gate + 0.02f, gate, 1f), "behind a prisoner too");
            Assert.IsTrue(Pawns.PawnView.GateStrikeDue(0f, gate, 2f), "a move shorter than the lead sounds from its start");
            Assert.Greater(1f - strike / hop, Pawns.PawnView.GateShutFrom, "on the way out the gate is already swinging when it is heard");
        }

        [Test]
        public void TheBusIsHeardStartingPullingAwaySoundingItsHornAndBraking()
        {
            // The owner's wish (2026-11-11): the engine starting, the horn, the brakes and the engine pulling away.
            Assert.AreEqual("bus_start", SoundCues.FileName(SoundCue.BusStart));
            Assert.AreEqual("bus_rev", SoundCues.FileName(SoundCue.BusRev));
            Assert.AreEqual("bus_horn", SoundCues.FileName(SoundCue.BusHorn));
            Assert.AreEqual("bus_brake", SoundCues.FileName(SoundCue.BusBrake));
            foreach (SoundCue cue in new[] { SoundCue.BusStart, SoundCue.BusRev, SoundCue.BusHorn, SoundCue.BusBrake })
            {
                Assert.AreEqual(1, SoundCues.Variants(cue), cue.ToString());
                Assert.That(SoundCues.Volume(cue), Is.InRange(0.4f, 0.65f), $"{cue} sits under the table's big moments");
                Assert.AreEqual(0f, SoundCues.PitchJitter(cue), $"{cue} keeps its pitch");
            }

            Assert.LessOrEqual(SoundCues.Volume(SoundCue.BusRev), SoundCues.Volume(SoundCue.BusHorn), "the long engine sound lies under the short ones");

            // The engine's sound outlasts the longest drive (the ride fades it out when the bus stops), and the engine
            // has started by the time the bus pulls away.
            AudioClip rev = Resources.Load<AudioClip>(SoundCues.ResourcePath(SoundCue.BusRev));
            Assert.GreaterOrEqual(rev.length, Tabletop.BusRoute.MostSeconds, "the engine is heard for the whole of the longest drive");
            AudioClip start = Resources.Load<AudioClip>(SoundCues.ResourcePath(SoundCue.BusStart));
            Assert.That(start.length, Is.InRange(Pawns.BusTrip.StartSeconds, Pawns.BusTrip.StartSeconds + 0.6f), "the engine has caught when the bus pulls away");
            Assert.Less(Resources.Load<AudioClip>(SoundCues.ResourcePath(SoundCue.BusHorn)).length, 0.8f, "a short greeting, not a blast");
            Assert.Less(Resources.Load<AudioClip>(SoundCues.ResourcePath(SoundCue.BusBrake)).length, 1.2f);

            // The ride's timing: the bus waits for its engine, and brakes a moment before it stands.
            Assert.AreEqual(Pawns.BusTrip.StartSeconds - Pawns.PawnView.BoardingSeconds, Pawns.BusTrip.WaitToStart(1f), 1e-5f);
            Assert.AreEqual(Pawns.BusTrip.WaitToStart(1f) / 2f, Pawns.BusTrip.WaitToStart(2f), 1e-5f, "a faster game waits less");
            Assert.Greater(Pawns.BusTrip.WaitToStart(1f), 0.2f);
            Assert.AreEqual(3f - Pawns.BusTrip.BrakeLead, Pawns.BusTrip.BrakesAt(3f, 1f), 1e-5f);
            Assert.AreEqual(3f - Pawns.BusTrip.BrakeLead / 2f, Pawns.BusTrip.BrakesAt(3f, 2f), 1e-5f);
            Assert.AreEqual(0f, Pawns.BusTrip.BrakesAt(0.2f, 1f), "a drive shorter than the braking brakes from its start");
            Assert.Less(Pawns.BusTrip.BrakeLead, Tabletop.BusRoute.LeastSeconds, "even the shortest drive pulls away before it brakes");
            Assert.Less(Pawns.BusTrip.EngineFade, Pawns.BusTrip.BrakeLead, "the engine is quiet when the bus stands");

            // A sound that is stopped fades away instead of being cut.
            Assert.AreEqual(0.6f, AudioPlayer.FadedVolume(0.6f, 0f, 0.4f), 1e-6f);
            Assert.AreEqual(0.3f, AudioPlayer.FadedVolume(0.6f, 0.2f, 0.4f), 1e-6f);
            Assert.AreEqual(0f, AudioPlayer.FadedVolume(0.6f, 0.4f, 0.4f), 1e-6f);
            Assert.AreEqual(0f, AudioPlayer.FadedVolume(0.6f, 9f, 0.4f), 1e-6f);
            Assert.AreEqual(0f, AudioPlayer.FadedVolume(0.6f, 0f, 0f), "no time to fade over: silent at once");
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
            Assert.AreEqual(SoundCue.PassStart, SoundCues.For(new MoneyChangedEvent(1, 150, MoneyReason.PassedStart)));
            Assert.AreEqual(SoundCue.MoneyIn, SoundCues.For(new MoneyChangedEvent(1, 200, MoneyReason.Pot)));
            Assert.AreEqual(SoundCue.MoneyOut, SoundCues.For(new MoneyChangedEvent(1, -100, MoneyReason.Tax)));
            Assert.AreEqual(SoundCue.Build, SoundCues.For(new MoneyChangedEvent(1, -50, MoneyReason.Building)));
            Assert.AreEqual(SoundCue.Sell, SoundCues.For(new MoneyChangedEvent(1, 25, MoneyReason.BuildingSale)));
            Assert.AreEqual(SoundCue.PropertySale, SoundCues.For(new MoneyChangedEvent(1, 30, MoneyReason.PropertySale)));
            Assert.AreEqual("property_sale", SoundCues.FileName(SoundCue.PropertySale));
            Assert.AreEqual("turn_pass", SoundCues.FileName(SoundCue.TurnPass));
        }

        [Test]
        public void TradesAndTheEndSound()
        {
            Assert.AreEqual(SoundCue.None, SoundCues.For(new MoneyChangedEvent(1, -40, MoneyReason.Trade)), "The handshake already sounds.");
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

        [Test]
        public void FrequentSoundsHaveSeveralTakesInTheBuild()
        {
            Assert.That(SoundCues.Variants(SoundCue.PawnStep), Is.GreaterThanOrEqualTo(3), "steps are the most repeated sound");
            Assert.That(SoundCues.Variants(SoundCue.DiceLand), Is.GreaterThanOrEqualTo(2));
            Assert.That(SoundCues.Variants(SoundCue.UiClick), Is.GreaterThanOrEqualTo(2));
            Assert.AreEqual(0, SoundCues.Variants(SoundCue.None));
            Assert.AreEqual("Lazpoly/Sfx/pawn_step", SoundCues.VariantPath(SoundCue.PawnStep, 0));
            Assert.AreEqual("Lazpoly/Sfx/pawn_step_2", SoundCues.VariantPath(SoundCue.PawnStep, 1));
            foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue)))
            {
                for (int i = 0; i < SoundCues.Variants(cue); i++)
                {
                    AudioClip clip = Resources.Load<AudioClip>(SoundCues.VariantPath(cue, i));
                    Assert.IsNotNull(clip, $"missing take {i + 1} of {cue}; run Tools/Audio/make_sfx_kenney.py");
                    // A real click lasts a few hundredths of a second; anything shorter is an empty or broken file.
                    // Only the bus's engine is long: it is heard for a whole drive.
                    float longest = cue == SoundCue.BusRev ? 6f : 3.5f;
                    Assert.That(clip.length, Is.InRange(0.03f, longest), $"{cue} take {i + 1}");
                }
            }
        }

        [Test]
        public void TheSameTakeNeverPlaysTwiceInARow()
        {
            Assert.AreEqual(0, SoundCues.PickVariant(1, 0, 0.7f), "a single take is the only choice");
            Assert.AreEqual(0, SoundCues.PickVariant(0, -1, 0.3f));
            for (int count = 2; count <= 4; count++)
            {
                var seen = new System.Collections.Generic.HashSet<int>();
                for (int last = -1; last < count; last++)
                {
                    for (float r = 0f; r < 1f; r += 0.01f)
                    {
                        int pick = SoundCues.PickVariant(count, last, r);
                        Assert.That(pick, Is.InRange(0, count - 1));
                        Assert.AreNotEqual(last, pick, $"take {last} again out of {count}");
                        seen.Add(pick);
                    }
                }

                Assert.AreEqual(count, seen.Count, "every take gets its turn");
                Assert.That(SoundCues.PickVariant(count, count - 1, 0.9999f), Is.InRange(0, count - 2), "the top of the range is not the last take");
            }
        }
    }
}
