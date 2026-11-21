using System.Linq;
using Lazpoly.Core.Board;
using Lazpoly.Core.Events;
using Lazpoly.Presentation.Audio;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Effects;
using Lazpoly.Presentation.Game;
using Lazpoly.Presentation.Models;
using Lazpoly.Presentation.Tabletop;
using Lazpoly.Presentation.Texts;
using NUnit.Framework;
using UnityEngine;
using Language = Lazpoly.Infra.Settings.Language;

namespace Lazpoly.Presentation.Tests
{
    /// <summary>
    /// The owner's card (2026-11-21): "bir şehre füze at", with animations and sounds that suit it. What the missile
    /// looks like, what its card shows and says, and what is heard; its flight and its blast are in
    /// <see cref="MissileTests"/>.
    /// </summary>
    public class MissileViewTests
    {
        [TearDown]
        public void BackToTurkish() => Lang.Set(Language.Turkish);

        [Test]
        public void TheMissileIsAToyRocketInItsPlayersColourThatFitsItsHatch()
        {
            var holder = new GameObject("Missile Test");
            try
            {
                var colour = new Color(0.2f, 0.5f, 0.9f);
                GameObject missile = MissileParts.Create(holder.transform, "Missile", colour);
                Transform body = missile.transform.Find(MissileParts.BodyName), trim = missile.transform.Find(MissileParts.TrimName);
                Transform nozzle = missile.transform.Find(MissileParts.NozzleName), flame = missile.transform.Find(MissileParts.FlameName);
                Assert.IsNotNull(body);
                Assert.IsNotNull(trim);
                Assert.IsNotNull(nozzle);
                Assert.IsNotNull(flame);
                Assert.AreEqual(5, missile.GetComponentsInChildren<MeshRenderer>().Length, "plain: body, trim, nozzle and a flame of two tones");
                Assert.That(Vector4.Distance(colour, trim.GetComponent<MeshRenderer>().sharedMaterial.color), Is.LessThan(1e-4f), "its nose, band and fins wear its player's colour");
                Assert.That(Vector4.Distance(MissileParts.Paint, body.GetComponent<MeshRenderer>().sharedMaterial.color), Is.LessThan(1e-4f), "an ivory body");
                Assert.Greater(MissileParts.Paint.grayscale, 0.85f, "an ivory body, as the buildings' walls");
                Assert.Less(MissileParts.Iron.grayscale, 0.3f);

                // Without its flame it is a length long from tail to nose along y, about its middle, and as wide as its fins.
                var whole = new Bounds(missile.transform.position, Vector3.zero);
                foreach (Transform part in new[] { body, trim, nozzle }) whole.Encapsulate(part.GetComponent<MeshRenderer>().bounds);
                Assert.AreEqual(MissileParts.Length, whole.size.y, 0.01f, "from tail to nose along y");
                Assert.AreEqual(0f, whole.center.y, 0.01f, "about its own middle");
                Assert.AreEqual(MissileParts.Span, whole.size.x, 0.02f, "its fins from tip to tip");
                Assert.AreEqual(whole.size.x, whole.size.z, 0.02f, "four fins, the same all round");
                Assert.AreEqual(0f, whole.center.x, 0.005f);
                Bounds tube = body.GetComponent<MeshRenderer>().bounds;
                Assert.AreEqual(2f * MissileParts.Radius, tube.size.x, 0.01f);
                Assert.Less(tube.max.y, trim.GetComponent<MeshRenderer>().bounds.max.y - 0.2f, "the nose stands on the body");

                // The flame hangs from the nozzle, shines by itself and takes no part in the shadows.
                Assert.AreEqual(-MissileParts.Length * 0.5f, flame.localPosition.y, 1e-4f, "the flame starts at the tail");
                Material unlit = Resources.Load<Material>(MissileParts.FlameMaterialResource);
                Assert.IsNotNull(unlit, "the unlit material of the fireworks' sparks");
                Assert.AreNotEqual(unlit.shader, body.GetComponent<MeshRenderer>().sharedMaterial.shader, "the missile itself is lit like every model");
                MeshRenderer[] fire = flame.GetComponentsInChildren<MeshRenderer>();
                Assert.AreEqual(2, fire.Length);
                foreach (MeshRenderer tongue in fire)
                {
                    Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, tongue.shadowCastingMode, "fire casts no shadow");
                    Assert.IsFalse(tongue.receiveShadows);
                    Assert.Less(tongue.bounds.max.y, -MissileParts.Length * 0.5f + 0.01f, "the flame is behind the missile");
                    Assert.Greater(tongue.bounds.min.y, -MissileParts.Length * 0.5f - MissileParts.FlameLength - 0.01f);
                    // Unlit, as the fireworks' sparks: a glow switched on in code is left out of a build (RC 101: a dark flame).
                    Assert.AreEqual(unlit.shader, tongue.sharedMaterial.shader, "the flame is drawn unlit, so it shines the same in sun and shade");
                    Assert.AreNotSame(unlit, tongue.sharedMaterial, "each missile's flame has its own material to throw away");
                    Assert.GreaterOrEqual(tongue.sharedMaterial.color.r, 1f, "the flame is at least as bright as its colour");
                    Assert.Greater(tongue.sharedMaterial.color.r, tongue.sharedMaterial.color.b * 2f, "fire is orange or yellow");
                }

                // Both tones are solid, so the yellow core is wider than the orange at the nozzle and shorter than it:
                // yellow where it leaves the missile, orange behind (RC 102: a core inside the flame was not seen).
                Bounds core = flame.Find(MissileParts.FlameCoreName).GetComponent<MeshRenderer>().bounds;
                Assert.Less(core.size.y, MissileParts.FlameLength * 0.9f, "a bright core in a longer flame");
                Assert.Greater(core.size.x, 2f * MissileParts.FlameRadius + 0.01f, "the core stands out of the flame round it");
                Assert.Less(core.size.x, 2f * MissileParts.Radius, "the flame is no wider than the missile");
                Assert.Greater(fire.Max(tongue => tongue.sharedMaterial.color.g) - fire.Min(tongue => tongue.sharedMaterial.color.g), 0.3f, "two tones: orange and yellow");
                Assert.AreNotEqual(UnityEngine.Rendering.ShadowCastingMode.Off, body.GetComponent<MeshRenderer>().shadowCastingMode, "the missile itself casts a shadow as it flies");

                // On the table it comes up through its player's hatch without touching the frame, and is big enough to follow.
                float across = MissileStrike.Size * BoardView.SquareWidth * MissileParts.Span;
                Assert.Less(across, MoneyHatch.HoleWidth - 0.1f, "its fins scrape the hatch");
                Assert.That(MissileStrike.Size, Is.InRange(0.5f, 0.8f), "seen from across the table, but no bigger than a house");
                Assert.Greater(MissileFlight.Hidden, 0.5f, "all of it is under the lawn before it lifts off");
            }
            finally
            {
                foreach (MeshRenderer part in holder.GetComponentsInChildren<MeshRenderer>()) Object.DestroyImmediate(part.sharedMaterial);
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void TheMissileCardIsARedAttackCardWithItsOwnPictureAndWords()
        {
            BoardData data = BoardDataLoader.LoadFromResources();
            int card = Enumerable.Range(0, data.Board.Cards.Count).Single(i => data.Board.Cards[i].Kind == CardKind.Missile);
            Assert.AreEqual("Card_missile", CardArt.PictureFor(CardKind.Missile));
            Assert.IsNotNull(CardArt.Get(CardArt.PictureFor(CardKind.Missile)), "run node Tools/Art/make_card_art.js");
            Assert.AreEqual(CardTone.Attack, CardArt.ToneFor(CardKind.Missile));
            Assert.AreEqual("SALDIRI KARTI", CardArt.Family(CardKind.Missile));
            Assert.AreEqual("FÜZE", CardArt.Title(CardKind.Missile));
            Assert.AreEqual("Füze", CardPlay.Name(CardKind.Missile));
            StringAssert.StartsWith("Evi olan rakip bir şehre füze at", CardPlay.Body(data.Board, card), "the name is the title already");
            StringAssert.Contains("Otelli şehre atılamaz", CardPlay.Body(data.Board, card), "the owner's choice (2026-11-21): a hotel is spared");
            Assert.AreEqual("Şehre füze atılıyor; üzerindeki bütün evler yıkılacak.", CardPlay.Effect(data.Board, new CardPlayedEvent(1, card, 1)));
            Assert.IsTrue(CardPlay.Fires(CardKind.Missile));
            Assert.IsTrue(CardPlay.TearsDown(CardKind.Missile));

            Lang.Set(Language.English);
            Assert.AreEqual("Missile", CardPlay.Name(CardKind.Missile));
            StringAssert.StartsWith("Fire a missile at a rival town", CardPlay.Body(data.Board, card));
            StringAssert.Contains("destroyed", CardPlay.Effect(data.Board, new CardPlayedEvent(1, card, 1)));
        }

        [Test]
        public void TheMissileIsHeardLiftingOffFlyingAndBlowingUpOrRingingOnAShield()
        {
            Assert.AreEqual("missile_launch", SoundCues.FileName(SoundCue.MissileLaunch));
            Assert.AreEqual("missile_blast", SoundCues.FileName(SoundCue.MissileBlast));
            Assert.AreEqual("missile_block", SoundCues.FileName(SoundCue.MissileBlock));
            Assert.AreEqual(SoundCue.MissileBlast, SoundCues.ForMissileHit(false));
            Assert.AreEqual(SoundCue.MissileBlock, SoundCues.ForMissileHit(true));
            Assert.AreEqual(SoundCue.None, SoundCues.ForLeaving(Leaving.Blasted), "what a missile blows up is heard in the blast, not as a collapse on top of it");
            foreach (SoundCue cue in new[] { SoundCue.MissileLaunch, SoundCue.MissileBlast, SoundCue.MissileBlock })
            {
                Assert.AreEqual(1, SoundCues.Variants(cue), cue.ToString());
                Assert.AreEqual(0f, SoundCues.PitchJitter(cue), $"{cue} keeps its pitch");
                Assert.That(SoundCues.Volume(cue), Is.InRange(0.6f, 0.95f), $"{cue} is one of the table's moments");
            }

            Assert.Greater(SoundCues.Volume(SoundCue.MissileBlast), SoundCues.Volume(SoundCue.BuildingFall), "the one explosion of the game is its loudest sound");
            Assert.Greater(SoundCues.Volume(SoundCue.MissileBlast), SoundCues.Volume(SoundCue.MissileLaunch));

            // Each clip is cut to the animation: the engine is heard for the flight and stops where the blast begins.
            AudioClip launch = Resources.Load<AudioClip>(SoundCues.ResourcePath(SoundCue.MissileLaunch));
            AudioClip blast = Resources.Load<AudioClip>(SoundCues.ResourcePath(SoundCue.MissileBlast));
            AudioClip block = Resources.Load<AudioClip>(SoundCues.ResourcePath(SoundCue.MissileBlock));
            Assert.IsNotNull(launch, "run Tools/Audio/make_sfx_kenney.py missile_launch");
            Assert.IsNotNull(blast, "run Tools/Audio/make_sfx_kenney.py missile_blast");
            Assert.IsNotNull(block, "run Tools/Audio/make_sfx_kenney.py missile_block");
            Assert.AreEqual(MissileFlight.Seconds, launch.length, 0.1f, "the engine is heard from lift-off to the hit");
            Assert.That(blast.length, Is.InRange(LeavingMotion.BlastedSeconds + 0.8f, 2.4f), "the rubble is heard coming down after the boom");
            Assert.That(block.length, Is.InRange(0.4f, 1.2f), "a shield rings and is quiet again");
        }
    }
}
