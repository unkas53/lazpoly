using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Presentation.Audio;
using Lazpoly.Presentation.Effects;
using Lazpoly.Presentation.Menu;
using Lazpoly.Presentation.UI;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    /// <summary>
    /// The interface's motion, light and sounds (the owner's wish, 2026-11-21: a main menu, loading screens, menus
    /// and buttons that suit the game, supported by animation, with button sounds). Each is a plain function of
    /// time or place, read here.
    /// </summary>
    public class InterfaceMotionTests
    {
        [Test]
        public void AButtonLiftsUnderThePointerAndSinksUnderAClick()
        {
            Assert.AreEqual(1f, UiHover.Target(false, false, true), 1e-6f);
            Assert.AreEqual(UiHover.Lift, UiHover.Target(true, false, true), 1e-6f);
            Assert.AreEqual(UiHover.Sink, UiHover.Target(true, true, true), 1e-6f, "held down it sinks");
            Assert.AreEqual(1f, UiHover.Target(true, true, false), 1e-6f, "a button that cannot be pressed stays flat");
            Assert.That(UiHover.Lift, Is.InRange(1.02f, 1.08f), "a lift that is felt, not a jump");
            Assert.That(UiHover.Sink, Is.InRange(0.94f, 0.99f));
            Assert.Less(UiHover.SinkSeconds, UiHover.LiftSeconds, "the click answers faster than the hover");
            Assert.Less(UiHover.LiftSeconds, 0.15f);

            // The hover tick: a new touch only, and never a rattle.
            var here = new Vector3(400f, 300f, 0f);
            var there = new Vector3(401f, 300f, 0f);
            Assert.IsFalse(UiHover.Ticks(here, here, 10f, 1f), "a window redrawn under a resting pointer is no new touch");
            Assert.IsFalse(UiHover.Ticks(there, here, 10f, 10f - UiHover.TickGap / 2f), "sweeping a row does not rattle");
            Assert.IsTrue(UiHover.Ticks(there, here, 10f, 10f - 2f * UiHover.TickGap));
            Assert.IsTrue(UiHover.Ticks(here, new Vector3(float.MinValue, 0f, 0f), 0f, float.MinValue), "the first touch of a game ticks");
        }

        [Test]
        public void AWindowGrowsIntoPlaceATouchPastItsSizeAndBack()
        {
            Assert.AreEqual(0f, UiFadeIn.AlphaAt(0f), 1e-6f);
            Assert.AreEqual(1f, UiFadeIn.AlphaAt(UiFadeIn.FadeShare), 1e-6f, "the dimming is there before the window has settled");
            Assert.AreEqual(1f, UiFadeIn.AlphaAt(1f), 1e-6f);
            Assert.AreEqual(UiFadeIn.StartScale, UiFadeIn.ScaleAt(0f), 1e-5f);
            Assert.AreEqual(1f, UiFadeIn.ScaleAt(1f), 1e-5f);
            float largest = 0f;
            for (int i = 0; i <= 100; i++) largest = Mathf.Max(largest, UiFadeIn.ScaleAt(i / 100f));
            Assert.That(largest, Is.InRange(1.002f, 1.03f), "a touch past its size, like something set down");
            Assert.That(UiFadeIn.Seconds, Is.InRange(0.15f, 0.3f), "quick: a window never keeps the player waiting");
            Assert.That(UiFadeIn.StartScale, Is.InRange(0.85f, 0.97f));
        }

        [Test]
        public void AScreensPartsComeInOneAfterAnother()
        {
            Assert.AreEqual(0f, UiEntrance.Delay(0), 1e-6f);
            Assert.AreEqual(3f * UiEntrance.Step, UiEntrance.Delay(3), 1e-6f);
            Assert.AreEqual(0f, UiEntrance.Delay(-2), 1e-6f);
            Assert.AreEqual(0f, UiEntrance.Progress(0.1f, 0.2f), 1e-6f, "a later part waits its turn");
            Assert.AreEqual(0.5f, UiEntrance.Progress(0.2f + UiEntrance.Seconds / 2f, 0.2f), 1e-5f);
            Assert.AreEqual(1f, UiEntrance.Progress(5f, 0.2f), 1e-6f);
            Assert.AreEqual(1f, UiEntrance.Aside(0f), 1e-6f, "it starts aside");
            Assert.AreEqual(0f, UiEntrance.Aside(1f), 1e-6f, "and ends in its place");
            Assert.Less(UiEntrance.Aside(0.5f), 0.5f, "fast at first, settling gently");
            Assert.AreEqual(0f, UiEntrance.Solid(0f), 1e-6f);
            Assert.AreEqual(1f, UiEntrance.Solid(0.5f), 1e-6f, "solid by half way: nothing slides about half seen");
            // The last part of the home screen's rail has arrived about a second after the screen shows.
            Assert.Less(UiEntrance.Delay(MenuLayout.RailParts.Count) + UiEntrance.Seconds, 1.2f);
        }

        [Test]
        public void TheLogoHoversTheRaysTurnAndTheSheenCrossesNowAndThen()
        {
            Assert.AreEqual(0f, UiFloat.RiseAt(0f, 6f, 5.5f), 1e-5f);
            Assert.AreEqual(6f, UiFloat.RiseAt(5.5f / 4f, 6f, 5.5f), 1e-4f, "a quarter of the way round it is at its highest");
            Assert.AreEqual(-6f, UiFloat.RiseAt(5.5f * 3f / 4f, 6f, 5.5f), 1e-4f);
            for (int i = 0; i < 60; i++) Assert.That(UiFloat.RiseAt(i * 0.37f, 6f, 5.5f), Is.InRange(-6.001f, 6.001f));

            Assert.AreEqual(0f, UiSpin.TurnAt(0f, 2.2f), 1e-6f);
            Assert.AreEqual(22f, UiSpin.TurnAt(10f, 2.2f), 1e-4f);
            Assert.That(UiSpin.TurnAt(100000f, 2.2f), Is.InRange(0f, 360f), "kept within one round however long the menu stays open");
            Assert.That(MenuRail.RaysTurn, Is.InRange(1f, 4f), "slow: a backdrop, never a wheel");
            Assert.AreEqual(MenuRail.RaysTurn, LoadingScreen.RaysTurn, 1e-6f, "the menu and the loading screen turn alike");
            Assert.That(MenuRail.RaysStrength, Is.InRange(0.04f, 0.16f), "rays that are sensed more than seen");

            Assert.AreEqual(0f, UiShine.CrossingAt(0f, 4f, 1f), 1e-6f);
            Assert.AreEqual(0.5f, UiShine.CrossingAt(0.5f, 4f, 1f), 1e-6f);
            Assert.AreEqual(-1f, UiShine.CrossingAt(2f, 4f, 1f), 1e-6f, "then nothing for a while");
            Assert.AreEqual(0.5f, UiShine.CrossingAt(8.5f, 4f, 1f), 1e-5f, "and again");
            Assert.That(UiShine.BandShare, Is.InRange(0.15f, 0.4f));
        }

        [Test]
        public void TheDustRisesThroughItsFieldAndNothingIsRandom()
        {
            var field = new Vector2(600f, 1080f);
            float leftmost = 1f, rightmost = 0f;
            for (int mote = 0; mote < MenuRail.Motes; mote++)
            {
                float lowest = float.MaxValue, highest = float.MinValue;
                for (int step = 0; step < 480; step++)
                {
                    UiDust.Mote at = UiDust.MoteAt(mote, step * 0.5f, field);
                    Assert.That(at.Position.y, Is.InRange(-UiDust.Margin - 0.01f, field.y + UiDust.Margin + 0.01f), "it stays about its field");
                    Assert.That(at.Position.x, Is.InRange(-16.01f, field.x + 16.01f), "swaying a little, never wandering off");
                    Assert.That(at.Alpha, Is.InRange(0f, 1f));
                    Assert.That(at.Size, Is.InRange(5f, 16f), "motes, not lamps");
                    lowest = Mathf.Min(lowest, at.Position.y);
                    highest = Mathf.Max(highest, at.Position.y);
                }

                Assert.Less(lowest, 60f, $"mote {mote} starts at the bottom");
                Assert.Greater(highest, field.y - 60f, $"mote {mote} reaches the top");
                Assert.AreEqual(UiDust.MoteAt(mote, 12.5f, field).Position.y, UiDust.MoteAt(mote, 12.5f, field).Position.y, 1e-6f, "the same mote, the same path");
                float across = UiDust.Trait(mote, 3);
                Assert.That(across, Is.InRange(0f, 1f));
                leftmost = Mathf.Min(leftmost, across);
                rightmost = Mathf.Max(rightmost, across);
            }

            Assert.Less(leftmost, 0.2f, "the motes are spread over the field");
            Assert.Greater(rightmost, 0.8f);
            Assert.That(MenuRail.Motes, Is.InRange(12, 40), "air, not a snowstorm");
            Assert.That(LoadingScreen.Motes, Is.InRange(12, 48));
        }

        [Test]
        public void TheLightsAreSoftAndStayInsideTheirPictures()
        {
            Assert.AreEqual(1f, UiFx.GlowAt(0.5f, 0.5f), 1e-6f);
            Assert.AreEqual(0f, UiFx.GlowAt(0f, 0.5f), 1e-6f, "nothing at the rim: no square shows");
            Assert.AreEqual(0f, UiFx.GlowAt(0.02f, 0.02f), 1e-6f, "nor in the corners");
            Assert.AreEqual(0.5f, UiFx.GlowAt(0.25f, 0.5f), 1e-5f);
            float before = 2f;
            for (int i = 0; i <= 50; i++)
            {
                float light = UiFx.GlowAt(0.5f + i / 100f, 0.5f);
                Assert.LessOrEqual(light, before, "it only falls off outwards");
                before = light;
            }

            // The sunburst: nothing in the very middle (the logo stands there) nor at the rim, and as many rays as it says.
            Assert.AreEqual(0f, UiFx.RaysAt(0.5f, 0.5f), 1e-6f);
            Assert.AreEqual(0f, UiFx.RaysAt(1f, 0.5f), 1e-6f);
            const int steps = 3600;
            int rays = 0;
            bool lit = UiFx.RaysAt(0.7f, 0.5f) > 0.01f;
            Assert.IsTrue(lit, "a ray points straight to the right");
            for (int i = 1; i <= steps; i++)
            {
                float angle = i * 2f * Mathf.PI / steps;
                bool now = UiFx.RaysAt(0.5f + 0.2f * Mathf.Cos(angle), 0.5f + 0.2f * Mathf.Sin(angle)) > 0.01f;
                if (now && !lit) rays++;
                lit = now;
            }

            Assert.AreEqual(UiFx.RayCount, rays);
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++) Assert.That(UiFx.RaysAt((x + 0.5f) / 32f, (y + 0.5f) / 32f), Is.InRange(0f, 1f));
            }

            Assert.AreEqual(0f, UiFx.BandAt(0f), 1e-6f);
            Assert.AreEqual(1f, UiFx.BandAt(0.5f), 1e-6f);
            Assert.AreEqual(0f, UiFx.BandAt(1f), 1e-5f);
            Assert.AreEqual(UiFx.BandAt(0.2f), UiFx.BandAt(0.8f), 1e-5f, "the same on both sides");
            Assert.AreEqual(0f, UiFx.EdgeAt(0f), 1e-6f);
            Assert.AreEqual(1f, UiFx.EdgeAt(0.5f), 1e-6f);
            Assert.AreEqual(0.5f, UiFx.EdgeAt(1f / UiFx.EdgeSize), 1e-5f, "soft over its outermost texels, so a leaning line shows no steps");
            Assert.AreEqual(1f, UiFx.EdgeAt(2f / UiFx.EdgeSize), 1e-5f);
            Assert.AreEqual(1f, UiFx.DotAt(0.5f, 0.5f), 1e-6f);
            Assert.AreEqual(0f, UiFx.DotAt(0f, 0.5f), 1e-6f);

            // A ground of two shades keeps the end colours beyond the part that shows.
            Color bottom = new Color(0f, 0f, 0.2f), top = new Color(0f, 0f, 0.4f);
            Assert.AreEqual(0.2f, UiFx.ShadeAt(0f, bottom, top, 0.3f, 0.7f).b, 1e-6f);
            Assert.AreEqual(0.4f, UiFx.ShadeAt(1f, bottom, top, 0.3f, 0.7f).b, 1e-6f);
            Assert.AreEqual(0.3f, UiFx.ShadeAt(0.5f, bottom, top, 0.3f, 0.7f).b, 1e-5f);
        }

        [Test]
        public void TheRailLeansAndEveryPartOnItStaysInsideItsGoldLine()
        {
            Assert.AreEqual(MenuLayout.RailTop, MenuLayout.RailEdge(0f), 1e-4f);
            Assert.AreEqual(MenuLayout.RailBottom, MenuLayout.RailEdge(MenuLayout.Design.y), 1e-4f);
            Assert.Greater(MenuLayout.RailTop, MenuLayout.RailBottom, "its top leans to the right");
            Assert.That(MenuLayout.RailLean, Is.InRange(4f, 12f), "a lean that is seen, not a slide");
            foreach (KeyValuePair<string, Rect> part in MenuLayout.RailParts)
            {
                // The edge is nearest at a part's foot; the hairline runs inside the gold.
                Assert.Less(part.Value.xMax, MenuLayout.RailEdge(part.Value.yMax) - MenuLayout.RailHairline, $"{part.Key} crosses the rail's hairline");
            }

            foreach (Rect part in new[] { MenuLayout.Characters, MenuLayout.HowToPlay, MenuLayout.Settings, MenuLayout.Sound, MenuLayout.WorldTitle, MenuLayout.WorldTowns })
            {
                Assert.Greater(part.xMin, MenuLayout.RailEdge(part.yMin) + MenuRail.ShadowReach, "what stands on the sky keeps clear of the rail and its shadow");
            }

            // The rail takes the same share of a 16:9 and of a squarer screen, and less of a wide one.
            Assert.AreEqual(0.375f, MenuLayout.RailShare(new Vector2(1920f, 1080f)), 1e-5f);
            Assert.AreEqual(0.375f, MenuLayout.RailShare(new Vector2(1600f, 1200f)), 1e-5f);
            Assert.AreEqual(720f / 2560f, MenuLayout.RailShare(new Vector2(2560f, 1080f)), 1e-5f);
            Assert.AreEqual(0f, MenuLayout.RailShare(Vector2.zero), 1e-6f);

            // The lights inside the band are placed from the middle of its edge; the band outreaches any screen.
            Vector2 atEdge = MenuRail.FromEdge(new Vector2(MenuLayout.RailEdge(MenuLayout.Design.y / 2f), MenuLayout.Design.y / 2f));
            Assert.AreEqual(0f, atEdge.x, 1e-4f);
            Assert.AreEqual(0f, atEdge.y, 1e-4f);
            Vector2 light = MenuRail.FromEdge(MenuHome.Light);
            Assert.Less(light.x, -150f, "the light stands well inside the band");
            Assert.Greater(light.y, 0f, "above the middle, behind the logo");
            Assert.AreEqual(1f, MenuRail.DesignFrom + MenuRail.DesignTo, 1e-6f);
            Assert.That(MenuRail.DesignFrom, Is.InRange(0.2f, 0.4f), "the navy shades only over the part that shows");
            Assert.Greater(MenuRail.BandHeight, 2f * MenuLayout.Design.y, "a squarer screen shows rail above and below the design");
            Assert.Less(MenuRail.EdgeBeyond, MenuRail.EdgeDrawn / 2f, "the gold line lies over the band's edge, most of it inside");
        }

        [Test]
        public void TheMenusTableShowsInThePartTheRailLeavesFree()
        {
            Assert.AreEqual(0.5f, MenuBackdrop.FreeMiddle(0f), 1e-6f, "no rail: the middle of the screen");
            Assert.AreEqual(0.6875f, MenuBackdrop.FreeMiddle(0.375f), 1e-6f);
            Assert.AreEqual(1f, MenuBackdrop.FreeMiddle(2f), 1e-6f);

            // Framed in the narrower free part the table is drawn smaller, so none of it is cut by the rail.
            float whole = MenuBackdrop.FramedSize(8f, 1920f, 1080f, 0f);
            float beside = MenuBackdrop.FramedSize(8f, 1920f, 1080f, 0.375f);
            Assert.Greater(beside, whole, "a larger view: the table smaller on the screen");
            Assert.AreEqual(MenuBackdrop.FramedSize(8f, 1920f, 1080f, 0.8f), MenuBackdrop.FramedSize(8f, 1920f, 1080f, 0.95f), 1e-4f, "never framed in a sliver");

            // The camera looks aside by so much that the table's middle shows at the free part's middle.
            Assert.AreEqual(0f, MenuBackdrop.Aside(0.5f, 10f, 16f / 9f), 1e-6f);
            Assert.AreEqual(10f * 16f / 9f, MenuBackdrop.Aside(1f, 10f, 16f / 9f), 1e-4f, "at the right edge: half the view's width");
            Assert.AreEqual(0.1875f * 2f * 10f * 16f / 9f, MenuBackdrop.Aside(MenuBackdrop.FreeMiddle(0.375f), 10f, 16f / 9f), 1e-4f);

            // It breathes and bobs slowly, a little.
            Assert.AreEqual(12f, MenuBackdrop.BreathingSize(12f, 0f), 1e-5f);
            Assert.AreEqual(12f * (1f + MenuBackdrop.Breath), MenuBackdrop.BreathingSize(12f, MenuBackdrop.BreathSeconds / 4f), 1e-4f);
            Assert.That(MenuBackdrop.Breath, Is.InRange(0.005f, 0.04f), "felt, never a zoom");
            Assert.AreEqual(0f, MenuBackdrop.BobAt(0f), 1e-6f);
            Assert.AreEqual(MenuBackdrop.Bob, MenuBackdrop.BobAt(MenuBackdrop.BobSeconds / 4f), 1e-5f);
            Assert.GreaterOrEqual(MenuBackdrop.BobSeconds, 6f, "calm: nothing behind the menu tires the eye");
            Assert.GreaterOrEqual(MenuBackdrop.BreathSeconds, 12f);

            // The game that is shown: every seat holds towns, a few are for sale, plots, houses and hotels all stand.
            var seats = new HashSet<int>();
            var levels = new HashSet<int>();
            int free = 0;
            for (int town = 0; town < 20; town++)
            {
                (int seat, int level) = MenuBackdrop.Holding(town);
                if (seat < 0)
                {
                    free++;
                    Assert.AreEqual(0, level, "nothing stands on a town for sale");
                    continue;
                }

                Assert.That(seat, Is.InRange(0, MenuBackdrop.Seats - 1));
                Assert.That(level, Is.InRange(0, SquareDefinition.MaxBuildingLevel));
                seats.Add(seat);
                levels.Add(level);
            }

            Assert.AreEqual(MenuBackdrop.Seats, seats.Count, "every player holds towns");
            Assert.AreEqual(SquareDefinition.MaxBuildingLevel + 1, levels.Count, "bare plots, houses of every count and hotels");
            Assert.That(free, Is.InRange(2, 5), "a few towns are still for sale");
        }

        [Test]
        public void TheInterfaceTicksOnATouchAndSoundsTheWayToTheTable()
        {
            Assert.AreEqual("ui_hover", SoundCues.FileName(SoundCue.UiHover));
            Assert.AreEqual("ui_close", SoundCues.FileName(SoundCue.UiClose));
            Assert.AreEqual("ui_start", SoundCues.FileName(SoundCue.UiStart));
            Assert.AreEqual("ui_leave", SoundCues.FileName(SoundCue.UiLeave));
            Assert.AreEqual(SoundCue.UiStart, SoundCues.ForSceneChange("Game"), "going to the table");
            Assert.AreEqual(SoundCue.UiLeave, SoundCues.ForSceneChange("MainMenu"), "coming back from it");
            Assert.GreaterOrEqual(SoundCues.Variants(SoundCue.UiHover), 3, "a row of buttons swept by the pointer never repeats one tick");
            Assert.Greater(SoundCues.PitchJitter(SoundCue.UiHover), 0f);
            Assert.Less(SoundCues.Volume(SoundCue.UiHover), SoundCues.Volume(SoundCue.UiClick), "a touch is quieter than a press");
            Assert.Less(SoundCues.Volume(SoundCue.UiClose), SoundCues.Volume(SoundCue.UiOpen), "a window going is quieter than one coming");
            Assert.Greater(SoundCues.Volume(SoundCue.UiStart), SoundCues.Volume(SoundCue.UiClick), "starting a game is a moment");
            foreach (SoundCue cue in new[] { SoundCue.UiClose, SoundCue.UiStart, SoundCue.UiLeave })
            {
                Assert.AreEqual(1, SoundCues.Variants(cue), cue.ToString());
                Assert.AreEqual(0f, SoundCues.PitchJitter(cue), 1e-6f, cue.ToString());
            }
        }
    }
}
