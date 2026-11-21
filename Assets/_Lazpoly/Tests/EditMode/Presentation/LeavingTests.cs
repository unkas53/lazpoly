using System;
using Lazpoly.Core.Board;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Effects;
using Lazpoly.Presentation.Game;
using Lazpoly.Presentation.Tabletop;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    /// <summary>
    /// The owner's wishes (2026-11-17 and 18): a fine animation for the buildings on a town when it is sold and when it
    /// is torn down, professional and in keeping with the game, with dollar signs for the gold glints. Sold, a building
    /// ducks, springs up and is gone in a puff of cloud out of which dollar signs rise; torn down, it is jolted and
    /// drops into cartoon dust, its rubble thrown out.
    /// </summary>
    public class LeavingTests
    {
        private const int Samples = 200;

        [Test]
        public void ASoldBuildingDucksSpringsUpAndIsGoneInABlink()
        {
            LeavingPose start = LeavingMotion.Sold(0f);
            AssertSame(Vector3.one, start.Scale, "it starts at its own size");
            Assert.AreEqual(0f, start.Lift, 1e-4f);

            LeavingPose ducked = LeavingMotion.Sold(LeavingMotion.Duck);
            Assert.AreEqual(1f - LeavingMotion.DuckBy, ducked.Scale.y, 1e-3f, "it ducks before it springs");
            Assert.Greater(ducked.Scale.x, 1f, "what it loses in height it gains in width");
            Assert.AreEqual(ducked.Scale.x, ducked.Scale.z, 1e-5f);
            Assert.AreEqual(0f, ducked.Lift, 1e-3f, "it is still on its tile at the bottom of the duck");

            LeavingPose sprung = LeavingMotion.Sold(LeavingMotion.PopAt - 1e-3f);
            Assert.AreEqual(1f + LeavingMotion.Spring, sprung.Scale.y, 1e-2f, "it springs up tall");
            Assert.Less(sprung.Scale.x, 1f, "and narrow");
            Assert.AreEqual(LeavingMotion.SpringLift, sprung.Lift, 1e-2f, "off its tile as it pops");

            LeavingPose end = LeavingMotion.Sold(1f);
            Assert.AreEqual(1f, end.Lift, 1e-4f);
            Assert.LessOrEqual(end.Scale.y, LeavingMotion.Least * (1f + LeavingMotion.Spring) + 1e-4f, "it is gone at the end");
            Assert.LessOrEqual(end.Scale.x, LeavingMotion.Least + 1e-4f, "it is gone at the end");
            AssertSame(end.Scale, LeavingMotion.Sold(2f).Scale, "past its time it stays as it ended");
            Assert.AreEqual(-(1f - LeavingMotion.Least) * (1f + LeavingMotion.Spring) * 0.5f, end.Sink, 1e-4f, "it dwindles into its own middle, inside the puff, not to its foot below it");
            Assert.Less(LeavingMotion.Sold(LeavingMotion.PopAt + (1f - LeavingMotion.PopAt) * 0.5f).Scale.y, 0.2f, "most of it is gone half way through the pop");

            LeavingPose before = start;
            for (int i = 1; i <= Samples; i++)
            {
                float t = i / (float)Samples;
                LeavingPose pose = LeavingMotion.Sold(t);
                Assert.GreaterOrEqual(pose.Lift, before.Lift - 1e-5f, $"it never comes back down (sample {i})");
                Assert.LessOrEqual(pose.Sink, 1e-6f, "a sold building does not go into the ground");
                if (t < LeavingMotion.PopAt) Assert.AreEqual(0f, pose.Sink, 1e-6f, "it stands on its foot until it pops");
                Assert.AreEqual(0f, pose.Turn, 1e-6f, "it does not spin");
                Assert.AreEqual(0f, pose.Tilt, 1e-6f, "it does not lean");
                Assert.AreEqual(0f, pose.Shake, 1e-6f, "it does not shake");
                Assert.Less(Mathf.Abs(pose.Lift - before.Lift), 0.04f, $"it jumps at sample {i}");
                if (t < LeavingMotion.PopAt) Assert.Less((pose.Scale - before.Scale).magnitude, 0.05f, $"its size jumps at sample {i}");
                else Assert.LessOrEqual(pose.Scale.y, before.Scale.y + 1e-5f, $"it grows back as it pops (sample {i})");
                Assert.GreaterOrEqual(pose.Scale.x, LeavingMotion.Least * 0.9f, "its size never turns to nothing while it is shown");
                Assert.GreaterOrEqual(pose.Scale.y, LeavingMotion.Least - 1e-5f, "its size never turns to nothing while it is shown");
                before = pose;
            }
        }

        [Test]
        public void ATornDownBuildingIsJoltedThenDropsEverFasterWhereItStands()
        {
            LeavingPose start = LeavingMotion.Demolished(0f);
            AssertSame(Vector3.one, start.Scale, "it starts at its own size");
            Assert.AreEqual(0f, start.Shake, 1e-4f);
            Assert.AreEqual(0f, start.Tilt, 1e-4f);

            float left = 0f, right = 0f;
            LeavingPose before = start;
            for (int i = 1; i <= Samples; i++)
            {
                float t = i / (float)Samples;
                LeavingPose pose = LeavingMotion.Demolished(t);
                Assert.AreEqual(0f, pose.Lift, 1e-5f, "it never leaves the ground");
                Assert.AreEqual(0f, pose.Turn, 1e-5f, "it does not turn");
                Assert.LessOrEqual(Mathf.Abs(pose.Shake), 1f + 1e-4f);
                Assert.LessOrEqual(pose.Scale.y, before.Scale.y + 1e-5f, $"it never grows back (sample {i})");
                Assert.GreaterOrEqual(pose.Sink, before.Sink - 1e-6f, $"it never comes back up (sample {i})");
                if (t < LeavingMotion.Jolt) Assert.AreEqual(0f, pose.Sink, 1e-6f, "the jolt does not sink it");
                Assert.GreaterOrEqual(pose.Scale.y, LeavingMotion.Least - 1e-5f);
                Assert.Less(before.Scale.y - pose.Scale.y, 0.03f, $"it drops at once at sample {i}");
                Assert.Less(Mathf.Abs(pose.Shake - before.Shake), 0.6f, $"its shake jumps at sample {i}");
                if (t < LeavingMotion.Jolt) Assert.GreaterOrEqual(pose.Scale.y, 1f - LeavingMotion.Settle - 1e-4f, "the jolt only settles it");
                if (t < LeavingMotion.Jolt)
                {
                    left = Mathf.Min(left, pose.Shake);
                    right = Mathf.Max(right, pose.Shake);
                }

                before = pose;
            }

            Assert.Less(left, -0.5f, "the jolt does not shake it to one side");
            Assert.Greater(right, 0.5f, "the jolt does not shake it to the other side");

            // The drop is a fall: little in its first half, most in its second.
            float half = LeavingMotion.Jolt + (1f - LeavingMotion.Jolt) * 0.5f;
            float firstHalf = LeavingMotion.Demolished(LeavingMotion.Jolt).Scale.y - LeavingMotion.Demolished(half).Scale.y;
            float secondHalf = LeavingMotion.Demolished(half).Scale.y - LeavingMotion.Demolished(1f).Scale.y;
            Assert.Greater(secondHalf, firstHalf * 2f, "it does not fall faster as it goes");

            LeavingPose end = LeavingMotion.Demolished(1f);
            Assert.AreEqual((1f - LeavingMotion.Settle) * LeavingMotion.Crush, end.Scale.y, 1e-4f, "it is crushed, not flattened like a box");
            Assert.AreEqual(end.Scale.y, end.Sink, 1e-4f, "its roof is down at the ground at the end, in the dust");
            Assert.Greater(LeavingMotion.Demolished(half).Scale.y - LeavingMotion.Demolished(half).Sink, 0.5f, "half way down it still stands out of the ground");
            Assert.AreEqual(LeavingMotion.Lean, end.Tilt, 1e-3f);
            Assert.AreEqual(0f, end.Shake, 1e-3f, "the rumble has died away");
            Assert.Greater(LeavingMotion.Demolished(half).Scale.x, 1.05f, "it bulges as it drops");
        }

        [Test]
        public void EachLeavingHasItsTimeAndWhatStaysBehindShowsAtItsEnd()
        {
            Assert.AreEqual(LeavingMotion.SoldSeconds, LeavingMotion.Seconds(Leaving.Sold));
            Assert.AreEqual(LeavingMotion.DemolishedSeconds, LeavingMotion.Seconds(Leaving.Demolished));
            Assert.AreEqual(LeavingMotion.BlastedSeconds, LeavingMotion.Seconds(Leaving.Blasted));
            foreach (Leaving how in Enum.GetValues(typeof(Leaving)))
            {
                // A building blown up is gone in the blink the fire hides it for; the others are watched going.
                Assert.That(LeavingMotion.Seconds(how), Is.InRange(how == Leaving.Blasted ? 0.3f : 0.5f, 1.6f), $"{how}: short enough not to hold the game up, long enough to be seen");
                LeavingPose own = how == Leaving.Sold ? LeavingMotion.Sold(0.5f) : how == Leaving.Demolished ? LeavingMotion.Demolished(0.5f) : LeavingMotion.Blasted(0.5f);
                AssertSame(LeavingMotion.At(how, 0.5f).Scale, own.Scale, $"{how}");
            }

            Assert.That(LeavingMotion.PopAt, Is.InRange(LeavingMotion.Duck, 1f), "it pops after its duck");
            Assert.That(LeavingMotion.UncoverAt, Is.InRange(LeavingMotion.PopAt, 1f), "what stays behind shows inside the puff");
        }

        [Test]
        public void OnlyTheDemolitionCardsAndTheMissileTearDown()
        {
            foreach (CardKind kind in Enum.GetValues(typeof(CardKind)))
            {
                bool tears = kind == CardKind.Demolish || kind == CardKind.IllegalFloor || kind == CardKind.Missile;
                Assert.AreEqual(tears, CardPlay.TearsDown(kind), $"{kind}");
                Assert.AreEqual(kind == CardKind.Missile, CardPlay.Fires(kind), $"{kind}: only the missile is fired from a hatch");
            }
        }

        [Test]
        public void APoseIsLaidOnTopOfWhereThePieceRests()
        {
            var piece = new GameObject("Leaving Test");
            try
            {
                var rest = new Vector3(1f, 2f, 3f);
                var size = new Vector3(2f, 2f, 2f);
                Quaternion facing = Quaternion.Euler(0f, 90f, 0f);
                const float width = 2f;

                const float height = 3f;
                BoardView.PoseLeaving(piece.transform, rest, size, facing, LeavingMotion.Sold(0f), width, height);
                AssertSame(rest, piece.transform.localPosition, "at the start it stands where it rests");
                AssertSame(size, piece.transform.localScale, "at the start it has its own size");
                Assert.Less(Quaternion.Angle(facing, piece.transform.localRotation), 0.1f);

                var pose = new LeavingPose { Lift = 0.5f, Turn = 90f, Shake = 1f, Scale = new Vector3(1f, 0.5f, 1f) };
                BoardView.PoseLeaving(piece.transform, rest, size, facing, pose, width, height);
                Vector3 side = facing * Vector3.right * (width * BoardView.LeaveShake);
                AssertSame(rest + Vector3.up * (0.5f * width * BoardView.LeaveLift) + side, piece.transform.localPosition, "lifted and shaken to its own side");
                AssertSame(new Vector3(2f, 1f, 2f), piece.transform.localScale, "its size along its own axes");
                Assert.Less(Quaternion.Angle(Quaternion.Euler(0f, 180f, 0f), piece.transform.localRotation), 0.1f, "turned about its own upright");

                var sunk = new LeavingPose { Sink = 0.5f, Scale = Vector3.one };
                BoardView.PoseLeaving(piece.transform, rest, size, facing, sunk, width, height);
                AssertSame(rest + Vector3.down * (0.5f * height), piece.transform.localPosition, "gone into the ground by a share of its own height");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(piece);
            }
        }

        [Test]
        public void ABurstIsFittedToTheBoxOfItsBuilding()
        {
            BoardView.Fit(new Vector3(0f, 0.1f, 0f), new Vector3(0.6f, 1.1f, 0.4f), 0.1f, 1f, out Vector3 foot, out float across, out float high);
            AssertSame(new Vector3(0.3f, 0.1f, 0.2f), foot, "the middle of its foot, on the ground");
            Assert.AreEqual(0.5f, across, 1e-4f, "the mean of its two sides");
            Assert.AreEqual(1f, high, 1e-4f, "its height above the ground");

            BoardView.Fit(Vector3.zero, new Vector3(0.01f, 0.01f, 0.01f), 0f, 1f, out _, out across, out high);
            Assert.AreEqual(0.2f, across, 1e-4f, "a speck still gets a burst that can be seen");
            Assert.AreEqual(0.15f, high, 1e-4f);
            BoardView.Fit(Vector3.zero, new Vector3(5f, 9f, 5f), 0f, 1f, out _, out across, out high);
            Assert.AreEqual(0.9f, across, 1e-4f, "nothing on a tile is wider than the tile");
            Assert.AreEqual(2f, high, 1e-4f);
        }

        [Test]
        public void TheShapesAreClearAtTheirEdgesAndSolidWhereTheyShouldBe()
        {
            foreach (SoftSprite sprite in Enum.GetValues(typeof(SoftSprite)))
            {
                for (int i = 0; i <= 40; i++)
                {
                    float along = i / 20f - 1f;
                    Assert.AreEqual(0f, SoftSprites.Alpha(sprite, along, 1f), 1e-5f, $"{sprite} shows at its top edge");
                    Assert.AreEqual(0f, SoftSprites.Alpha(sprite, -1f, along), 1e-5f, $"{sprite} shows at its left edge");
                    for (int j = 0; j <= 40; j++) Assert.That(SoftSprites.Alpha(sprite, along, j / 20f - 1f), Is.InRange(0f, 1f), $"{sprite}");
                }
            }

            Assert.AreEqual(1f, SoftSprites.Alpha(SoftSprite.Cloud, 0f, 0f), 1e-4f, "a cloud is solid at its heart");
            Assert.AreEqual(1f, SoftSprites.Alpha(SoftSprite.Dot, 0f, 0f), 1e-4f, "a glow is brightest in the middle");
            Assert.Less(SoftSprites.Alpha(SoftSprite.Ring, 0f, 0f), 0.01f, "a ring is open in the middle");
            Assert.Greater(SoftSprites.Alpha(SoftSprite.Ring, 0.78f, 0f), 0.95f, "a ring is solid on its line");
            Assert.Greater(SoftSprites.Alpha(SoftSprite.Dot, 0.3f, 0f), SoftSprites.Alpha(SoftSprite.Dot, 0.6f, 0f), "a glow thins outwards");

            // A cloud's outline is lumpy (further out along a lobe than between two) and clean: solid or clear, with
            // hardly anything between.
            float lobe = 0.5f, between = 0.5f + Mathf.PI / 6f;
            Assert.AreEqual(1f, SoftSprites.Alpha(SoftSprite.Cloud, Mathf.Cos(lobe) * 0.72f, Mathf.Sin(lobe) * 0.72f), 1e-4f, "a cloud has no lobes");
            Assert.AreEqual(0f, SoftSprites.Alpha(SoftSprite.Cloud, Mathf.Cos(between) * 0.72f, Mathf.Sin(between) * 0.72f), 1e-4f, "a cloud is a plain round ball");
            int solid = 0, soft = 0, lit = 0;
            for (int i = 0; i < 64; i++)
            {
                for (int j = 0; j < 64; j++)
                {
                    float x = (i + 0.5f) / 32f - 1f, y = (j + 0.5f) / 32f - 1f;
                    float cloud = SoftSprites.Alpha(SoftSprite.Cloud, x, y);
                    if (cloud > 0.99f) solid++;
                    else if (cloud > 0.01f) soft++;
                    Assert.GreaterOrEqual(SoftSprites.Alpha(SoftSprite.DollarEdge, x, y), SoftSprites.Alpha(SoftSprite.Dollar, x, y) - 1e-5f, "a sign shows outside its rim");
                    if (SoftSprites.Alpha(SoftSprite.CloudLight, x, y) < 0.5f) continue;
                    lit++;
                    Assert.Greater(cloud, 0.5f, "a cloud's lit side reaches past the cloud");
                }
            }

            Assert.Less(soft, solid / 4, "a cloud's edge is blurred");
            Assert.That(lit, Is.InRange(solid * 0.4f, solid * 0.7f), "a cloud's lit side is a good half of it");

            // The dollar sign: a bar through an S of two bows, the upper open to the right, the lower to the left.
            float bow = SoftSprites.Bow;
            Assert.AreEqual(1f, SoftSprites.Alpha(SoftSprite.Dollar, 0f, 0f), 1e-4f, "the bar runs through the middle");
            Assert.AreEqual(1f, SoftSprites.Alpha(SoftSprite.Dollar, 0f, SoftSprites.BarReach - 0.05f), 1e-4f, "the bar stands out above the S");
            Assert.AreEqual(1f, SoftSprites.Alpha(SoftSprite.Dollar, 0f, 0.05f - SoftSprites.BarReach), 1e-4f, "and below it");
            Assert.AreEqual(0f, SoftSprites.Alpha(SoftSprite.Dollar, 0f, 0.92f), 1e-4f, "the bar ends");
            Assert.AreEqual(1f, SoftSprites.Alpha(SoftSprite.Dollar, -bow, bow), 1e-4f, "the upper bow comes down the left");
            Assert.AreEqual(1f, SoftSprites.Alpha(SoftSprite.Dollar, bow, -bow), 1e-4f, "the lower bow comes up the right");
            float gapX = bow * Mathf.Cos(20f * Mathf.Deg2Rad), gapY = bow - bow * Mathf.Sin(20f * Mathf.Deg2Rad);
            Assert.AreEqual(0f, SoftSprites.Alpha(SoftSprite.Dollar, gapX, gapY), 1e-4f, "the upper bow is closed on the right: an 8, not an S");
            Assert.AreEqual(0f, SoftSprites.Alpha(SoftSprite.Dollar, -gapX, -gapY), 1e-4f, "the lower bow is closed on the left: an 8, not an S");
            float outside = bow + SoftSprites.Stroke + SoftSprites.Rim * 0.5f;
            Assert.AreEqual(0f, SoftSprites.Alpha(SoftSprite.Dollar, -outside, bow), 1e-4f, "the sign is thicker than its stroke");
            Assert.AreEqual(1f, SoftSprites.Alpha(SoftSprite.DollarEdge, -outside, bow), 1e-4f, "the rim does not stand out round the sign");

            // Each shape has its own cell of one sheet.
            var seen = new System.Collections.Generic.HashSet<Vector2>();
            foreach (SoftSprite sprite in Enum.GetValues(typeof(SoftSprite)))
            {
                Rect cell = SoftSprites.Uv(sprite);
                Assert.AreEqual(1f / SoftSprites.Columns, cell.width, 1e-6f);
                Assert.AreEqual(1f / SoftSprites.Rows, cell.height, 1e-6f);
                Assert.GreaterOrEqual(cell.xMin, 0f);
                Assert.GreaterOrEqual(cell.yMin, 0f);
                Assert.LessOrEqual(cell.xMax, 1f + 1e-6f, $"{sprite} is off the sheet");
                Assert.LessOrEqual(cell.yMax, 1f + 1e-6f, $"{sprite} is off the sheet");
                Assert.IsTrue(seen.Add(cell.position), $"{sprite} shares its place on the sheet");
            }

            Texture2D sheet = SoftSprites.Sheet();
            Assert.AreEqual(SoftSprites.Cell * SoftSprites.Columns, sheet.width);
            Assert.AreEqual(SoftSprites.Cell * SoftSprites.Rows, sheet.height);
            Assert.AreSame(sheet, SoftSprites.Sheet(), "the sheet is drawn once");
        }

        [Test]
        public void APieceComesInMovesSlowsPopsAndGoesOutByPlainArithmetic()
        {
            var piece = new SoftParticle
            {
                Born = 1f, Life = 2f, From = new Vector3(1f, 2f, 3f), Drift = new Vector3(2f, 0f, 0f), Rise = 1f, Fall = 0.5f,
                SizeFrom = 1f, SizeTo = 3f, Alpha = 0.8f, FadeIn = 0.25f, FadeFrom = 0.5f, Turn = 10f, Spin = 80f,
            };
            Assert.IsFalse(piece.Alive(0.5f), "it shows before it is born");
            Assert.AreEqual(0f, piece.Opacity(piece.Age(0.5f)));
            Assert.IsTrue(piece.Alive(1f));
            Assert.IsTrue(piece.Alive(2f));
            Assert.IsFalse(piece.Alive(3f), "it stays after its life");
            Assert.AreEqual(0f, piece.Opacity(piece.Age(3f)));

            Assert.AreEqual(0f, piece.Opacity(0f), 1e-5f, "it pops in");
            Assert.AreEqual(0.4f, piece.Opacity(0.125f), 1e-4f, "half way in");
            Assert.AreEqual(0.8f, piece.Opacity(0.4f), 1e-5f, "at its fullest");
            Assert.AreEqual(0.4f, piece.Opacity(0.75f), 1e-4f, "half way out");
            Assert.Less(piece.Opacity(0.99f), 0.01f, "it snaps out");

            AssertSame(new Vector3(1f, 2f, 3f), piece.Position(0f), "it starts where it was put");
            AssertSame(new Vector3(3f, 2.5f, 3f), piece.Position(1f), "carried, risen and fallen by the end");
            Assert.Greater(piece.Position(0.5f).x - 1f, 1f, "it is carried fast at first and slows");
            Assert.AreEqual(1f, piece.Size(0f), 1e-5f);
            Assert.AreEqual(3f, piece.Size(1f), 1e-5f);
            Assert.AreEqual(10f, piece.Angle(0f), 1e-4f);
            Assert.AreEqual(90f, piece.Angle(1f), 1e-4f);

            piece.Sit = 0.5f;
            Assert.AreEqual(2.5f, piece.Position(0f).y, 1e-5f, "a cloud is held up by a share of its size");

            // A cartoon piece pops in, a little past its size and back, and dwindles to nothing at the end.
            var cloud = new SoftParticle { Life = 1f, SizeFrom = 2f, SizeTo = 2f, Pop = 0.2f, ShrinkFrom = 0.6f, Alpha = 1f, FadeFrom = 1f };
            Assert.AreEqual(0f, cloud.Size(0f), 1e-5f, "it starts from nothing");
            Assert.Greater(cloud.Size(0.14f), 2f, "it pops a little past its size");
            Assert.AreEqual(2f, cloud.Size(0.2f), 1e-4f, "and comes back to it");
            Assert.AreEqual(2f, cloud.Size(0.6f), 1e-4f, "it holds its size");
            Assert.AreEqual(2f * (1f - Ease.InCubic(0.5f)), cloud.Size(0.8f), 1e-4f, "then dwindles, slowly at first");
            Assert.AreEqual(0f, cloud.Size(1f), 1e-5f, "to nothing");
            Assert.AreEqual(1f, cloud.Opacity(0.5f), 1e-5f, "solid all the while");
        }

        [Test]
        public void ASoftPiecesQuadFacesTheEyeOrLiesOnTheGround()
        {
            var piece = new SoftParticle { Life = 1f, SizeFrom = 2f, SizeTo = 2f, Alpha = 1f };
            LeavingBurst.Quad(piece, 0.5f, Vector3.right, Vector3.up, out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d);
            AssertSame(new Vector3(-1f, -1f, 0f), a, "lower left");
            AssertSame(new Vector3(1f, -1f, 0f), b, "lower right");
            AssertSame(new Vector3(1f, 1f, 0f), c, "upper right");
            AssertSame(new Vector3(-1f, 1f, 0f), d, "upper left");

            piece.Turn = 90f;
            LeavingBurst.Quad(piece, 0.5f, Vector3.right, Vector3.up, out a, out _, out c, out _);
            AssertSame(new Vector3(1f, -1f, 0f), a, "turned a quarter");
            AssertSame(new Vector3(-1f, 1f, 0f), c, "turned a quarter");

            piece.Turn = 0f;
            piece.Flat = true;
            LeavingBurst.Quad(piece, 0.5f, new Vector3(0.7f, 0f, 0.7f), new Vector3(0f, 0.7f, -0.7f), out a, out _, out c, out _);
            AssertSame(new Vector3(-1f, 0f, -1f), a, "a ring lies on the ground whatever the eye");
            AssertSame(new Vector3(1f, 0f, 1f), c, "a ring lies on the ground whatever the eye");

            // Drawn nearer the eye, a piece keeps its place on the screen and comes out of the plane it faces; a ring
            // on the ground does not move.
            piece.Fore = 0.5f;
            LeavingBurst.Quad(piece, 0.5f, Vector3.right, Vector3.up, out a, out _, out c, out _);
            AssertSame(new Vector3(-1f, 0f, -1f), a, "a ring is lifted off the ground towards the eye");
            piece.Flat = false;
            LeavingBurst.Quad(piece, 0.5f, Vector3.right, Vector3.up, out a, out _, out c, out _);
            AssertSame(new Vector3(-1f, -1f, -0.5f), a, "towards the eye is back along its forward");
            AssertSame(new Vector3(1f, 1f, -0.5f), c, "towards the eye is back along its forward");
        }

        [Test]
        public void ASaleLaysRingsAFlashAPuffAndDollarSignsAndClearsItselfAway()
        {
            var holder = new GameObject("Burst Test");
            try
            {
                var foot = new Vector3(1f, 0.5f, 3f);
                const float width = 0.8f, height = 1.2f, tile = 2f, lift = 0.6f;
                LeavingBurst burst = LeavingBurst.Play(holder.transform, Leaving.Sold, foot, width, height, tile, lift, Color.red, 7);
                Assert.AreEqual(Leaving.Sold, burst.How);
                Assert.AreEqual(LeavingBurst.SoldParticles, burst.Particles.Count);
                Assert.AreEqual(0, burst.RubbleCount, "a sold building leaves rubble");
                Assert.That(burst.Seconds, Is.InRange(LeavingMotion.SoldSeconds + 0.8f, 2.3f), "the signs outlast the building, but not by long");

                float pop = LeavingMotion.SoldSeconds * LeavingMotion.PopAt;
                Assert.AreEqual(0, burst.Showing(pop - 0.1f), "something is in the air before the building pops");
                Assert.Greater(burst.Showing(pop + 0.1f), 2 * LeavingBurst.Poofs, "the puff is not there as it pops");

                int signs = 0, green = 0, clouds = 0, rings = 0;
                for (int index = 0; index < burst.Particles.Count; index++)
                {
                    SoftParticle particle = burst.Particles[index];
                    Assert.Greater(particle.Life, 0f);
                    for (int i = 0; i <= 20; i++)
                    {
                        Vector3 at = particle.Position(i / 20f);
                        Assert.GreaterOrEqual(at.y, foot.y - 1e-4f, $"a {particle.Sprite} goes under the ground");
                        Assert.Less(new Vector2(at.x - foot.x, at.z - foot.z).magnitude, tile, $"a {particle.Sprite} flies a tile away");
                    }

                    if (particle.Sprite == SoftSprite.Ring)
                    {
                        rings++;
                        Assert.IsTrue(particle.Flat, "a ring stands up");
                        Assert.LessOrEqual(particle.SizeTo, tile * 1.15f + 1e-4f, "a ring spreads far past its tile");
                    }

                    if (particle.Sprite == SoftSprite.Cloud || particle.Sprite == SoftSprite.DollarEdge)
                    {
                        // A shape of two tones: its other tone is the next piece, the same in all but shape and colour.
                        SoftParticle top = burst.Particles[index + 1];
                        Assert.AreEqual(particle.Sprite == SoftSprite.Cloud ? SoftSprite.CloudLight : SoftSprite.Dollar, top.Sprite);
                        AssertSame(particle.From, top.From, "the two tones part");
                        AssertSame(particle.Drift, top.Drift, "the two tones part");
                        Assert.AreEqual(particle.Born, top.Born);
                        Assert.AreEqual(particle.Life, top.Life);
                        Assert.AreEqual(particle.SizeTo, top.SizeTo);
                        Assert.AreEqual(particle.Turn, top.Turn);
                        Assert.AreEqual(particle.Spin, top.Spin);
                        Assert.AreNotEqual(Luma(particle.Color), Luma(top.Color), "the two tones are one");
                    }

                    if (particle.Sprite == SoftSprite.Cloud)
                    {
                        clouds++;
                        Assert.AreEqual(LeavingBurst.Fore(width, height), particle.Fore, 1e-5f, "the puff is drawn before the building it takes");
                        Assert.Greater(particle.Pop, 0f, "a cloud fades in instead of popping");
                        Assert.Greater(particle.ShrinkFrom, 0f, "a cloud fades out instead of dwindling");
                    }

                    if (particle.Sprite != SoftSprite.Dollar) continue;
                    signs++;
                    SoftParticle rim = burst.Particles[index - 1];
                    bool money = particle.Color.g > particle.Color.r;
                    if (money) green++;
                    Assert.AreEqual(money ? LeavingBurst.Money.g : LeavingBurst.Gold.g, particle.Color.g, "a sign is gold or money green");
                    Assert.AreEqual(money ? LeavingBurst.MoneyRim.g : LeavingBurst.GoldRim.g, rim.Color.g, "a sign's rim is the dark of its own colour");
                    Assert.Less(Luma(rim.Color), Luma(particle.Color) * 0.5f, "a sign's rim is too pale to read it by");
                    Assert.GreaterOrEqual(particle.Born, pop, "a sign flies before the building pops");
                    Assert.Greater(particle.From.y, foot.y + lift * LeavingMotion.SpringLift, "a sign does not start where the building vanishes");
                    Assert.Greater(particle.Position(1f).y, particle.From.y + 0.1f * tile, "a sign does not rise");
                    Assert.That(particle.SizeTo, Is.InRange(0.24f * tile, 0.4f * tile), "a sign is big enough to read and smaller than the building");
                    Assert.Less(Mathf.Abs(particle.Turn) + Mathf.Abs(particle.Spin), 40f, "a sign tumbles: it should stay upright enough to read");
                    Assert.AreEqual(1f, particle.Opacity(0.5f), 1e-5f, "a sign is solid while it hangs");
                }

                Assert.AreEqual(2, rings);
                Assert.AreEqual(LeavingBurst.Poofs, clouds);
                Assert.AreEqual(LeavingBurst.Dollars, signs, "the dollar signs the owner asked for");
                Assert.That(green, Is.InRange(2, LeavingBurst.Dollars / 2), "mostly gold, some money green");

                // The same building leaves the same way; another scatters otherwise.
                LeavingBurst again = LeavingBurst.Play(holder.transform, Leaving.Sold, foot, width, height, tile, lift, Color.red, 7);
                LeavingBurst other = LeavingBurst.Play(holder.transform, Leaving.Sold, foot, width, height, tile, lift, Color.red, 8);
                bool differs = false;
                for (int i = 0; i < burst.Particles.Count; i++)
                {
                    AssertSame(burst.Particles[i].Drift, again.Particles[i].Drift, "the same seed scatters differently");
                    differs |= (burst.Particles[i].Drift - other.Particles[i].Drift).magnitude > 1e-3f;
                }

                Assert.IsTrue(differs, "another seed scatters the same");
                UnityEngine.Object.DestroyImmediate(again.gameObject);
                UnityEngine.Object.DestroyImmediate(other.gameObject);

                // One mesh carries every soft piece; when its time is up the burst is gone.
                burst.Step(burst.Seconds * 0.5f);
                Assert.IsFalse(burst == null, "the burst goes before its time");
                MeshFilter soft = holder.GetComponentInChildren<MeshFilter>();
                Assert.AreEqual(LeavingBurst.SoldParticles * 4, soft.sharedMesh.vertexCount);
                Assert.Greater(burst.Showing(burst.Age), 0);
                burst.Step(burst.Seconds);
                Assert.IsTrue(burst == null, "the burst stays after its time");
                Assert.AreEqual(0, holder.transform.childCount, "the burst leaves something behind");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void ADemolitionBurstsDustOutAndThrowsRubbleThatLandsOnTheTable()
        {
            var holder = new GameObject("Burst Test");
            try
            {
                var foot = new Vector3(3.4f, 0.5f, 0f);
                const float width = 0.8f, height = 1.2f, tile = 2f, bound = 4f;
                LeavingBurst burst = LeavingBurst.Play(holder.transform, Leaving.Demolished, foot, width, height, tile, 0f, Color.red, 3, bound);
                Assert.AreEqual(Leaving.Demolished, burst.How);
                Assert.AreEqual(LeavingBurst.DemolishedParticles, burst.Particles.Count);
                Assert.AreEqual(LeavingBurst.Rubble, burst.RubbleCount);

                // The rubble is of the building's colours: its white walls (a little soiled), its roof and mortar.
                Color[] tones = LeavingBurst.RubbleTones(Color.white, Color.blue);
                Assert.AreEqual(3, tones.Length);
                Assert.Greater(Mathf.Min(tones[0].r, tones[0].g, tones[0].b), 0.75f, "the walls' rubble is no longer light");
                Assert.AreEqual(Color.blue, tones[1], "a third of the rubble is the roof's colour");
                Assert.AreNotEqual(Color.blue, LeavingBurst.RubbleTones(Color.white, null)[1], "with no roof given the rubble is the wall's alone");
                Assert.That(burst.Seconds, Is.InRange(LeavingMotion.DemolishedSeconds + 0.8f, 3f), "the dust hangs a while after the building, but not long");
                for (int i = 0; i < burst.RubbleCount; i++) Assert.IsFalse(burst.RubbleShown(i), "rubble shows before the walls give way");

                float drop = LeavingMotion.DemolishedSeconds * LeavingMotion.Jolt;
                int clouds = 0;
                foreach (SoftParticle particle in burst.Particles)
                {
                    if (particle.Sprite == SoftSprite.Cloud && particle.Born >= drop) clouds++;
                    bool dust = particle.Sprite == SoftSprite.Cloud || particle.Sprite == SoftSprite.CloudLight;
                    Assert.AreEqual(dust && particle.Born >= drop ? LeavingBurst.Fore(width, height) : 0f, particle.Fore, 1e-5f, "the dust the building falls into is drawn before it, and nothing else");
                    if (particle.Sprite == SoftSprite.Cloud) Assert.Less(Luma(particle.Color), Luma(LeavingBurst.Puff), "dust is as white as a sale's puff");
                    for (int i = 0; i <= 20; i++)
                    {
                        Assert.GreaterOrEqual(particle.Position(i / 20f).y, foot.y - 1e-4f, $"a {particle.Sprite} goes under the ground");
                    }
                }

                Assert.AreEqual(LeavingBurst.Clouds + LeavingBurst.Plumes + LeavingBurst.Caps + LeavingBurst.Wisps, clouds);

                // The dust up the walls starts at them, no higher than the roof, and comes down with them.
                int plumes = 2 * LeavingBurst.Kicks + 1 + 2 * LeavingBurst.Clouds;
                for (int i = 0; i < LeavingBurst.Plumes; i++)
                {
                    SoftParticle plume = burst.Particles[plumes + 2 * i];
                    Assert.AreEqual(SoftSprite.Cloud, plume.Sprite);
                    float out0 = new Vector2(plume.From.x - foot.x, plume.From.z - foot.z).magnitude;
                    Assert.Greater(out0, width * 0.5f, $"plume {i} starts inside the walls");
                    Assert.Less(plume.Position(1f).y, plume.From.y - height * 0.1f, $"plume {i} hangs in the air while the building falls");
                    Assert.That(plume.From.y - foot.y, Is.InRange(height * 0.3f, height * 0.8f), $"plume {i}");
                }

                // And a column of it before the building, from near its foot to its roof, falling in with it.
                int caps = plumes + 2 * LeavingBurst.Plumes;
                for (int i = 0; i < LeavingBurst.Caps; i++)
                {
                    SoftParticle cap = burst.Particles[caps + 2 * i];
                    Assert.AreEqual(SoftSprite.Cloud, cap.Sprite);
                    Assert.Less(new Vector2(cap.From.x - foot.x, cap.From.z - foot.z).magnitude, width * 0.25f, $"cap {i} is off the building's middle");
                    Assert.AreEqual(Mathf.Lerp(0.2f, 0.9f, i / (LeavingBurst.Caps - 1f)) * height, cap.From.y - foot.y, 1e-4f, $"cap {i}");
                    Assert.Less(cap.Position(1f).y - foot.y, height * 0.3f, $"cap {i} stays up when the building is down");
                }

                Assert.Greater(LeavingBurst.Fore(width, height), new Vector2(width, height * 0.5f).magnitude * 0.9f, "the dust is not drawn far enough forward to clear the building");

                Color32 lit = LeavingBurst.Lit(LeavingBurst.DustTones[0]);
                Assert.Greater(Luma(lit), Luma(LeavingBurst.DustTones[0]), "a cloud's lit side is darker than the cloud");
                Assert.Less(Luma(lit), Luma(LeavingBurst.DustLight));
                Assert.Greater(burst.Showing(drop + 0.6f), burst.Showing(0.1f), "the cloud does not grow as the building drops");

                // Thrown: in the air soon after the walls give way.
                burst.Step(drop + 0.45f);
                float highest = 0f;
                for (int i = 0; i < burst.RubbleCount; i++)
                {
                    Assert.IsTrue(burst.RubbleShown(i), $"rubble {i} is hidden");
                    Assert.GreaterOrEqual(burst.RubblePlace(i).y, foot.y, $"rubble {i} is under the ground");
                    highest = Mathf.Max(highest, burst.RubblePlace(i).y - foot.y);
                }

                Assert.Greater(highest, height * 0.5f, "no rubble is thrown up");

                // Landed: near the ground, near the building, and none past the table's edge (the building stands by it).
                burst.Step(1.65f - burst.Age);
                for (int i = 0; i < burst.RubbleCount; i++)
                {
                    Vector3 place = burst.RubblePlace(i);
                    Assert.That(place.y - foot.y, Is.InRange(0f, tile * 0.25f), $"rubble {i} has not come down");
                    Assert.LessOrEqual(Mathf.Abs(place.x), bound, $"rubble {i} is off the table");
                    Assert.LessOrEqual(Mathf.Abs(place.z), bound, $"rubble {i} is off the table");
                    Assert.Less(new Vector2(place.x - foot.x, place.z - foot.z).magnitude, tile, $"rubble {i} is thrown a tile away");
                }

                Assert.AreEqual(1f, LeavingBurst.RubbleSize(LeavingBurst.RubbleSeconds), 1e-5f, "rubble dwindles while it should lie");
                Assert.AreEqual(0f, LeavingBurst.RubbleSize(LeavingBurst.RubbleSeconds + LeavingBurst.RubbleGone), 1e-5f, "rubble stays for good");
                Assert.AreEqual(0f, LeavingBurst.RubbleSize(-0.1f));

                burst.Step(burst.Seconds);
                Assert.IsTrue(burst == null, "the burst stays after its time");
                Assert.AreEqual(0, holder.transform.childCount, "the burst leaves something behind");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void WithoutAnimationBuildingsAndTheMarkerSimplyGo()
        {
            BoardData data = BoardDataLoader.LoadFromResources();
            var holder = new GameObject("Board Test");
            try
            {
                BoardView board = BoardView.Create(holder.transform, data);
                int town = -1;
                for (int i = 0; i < data.Board.Count && town < 0; i++)
                {
                    if (data.Board[i].Type == SquareType.Property && data.Board[i].IsDevelopable) town = i;
                }

                board.SetOwner(town, Color.red);
                Assert.IsTrue(board.MarkerShown(town), "a town just bought shows its plot");
                board.SetBuildingLevel(town, 2);
                Assert.IsFalse(board.MarkerShown(town), "the plot gives way to the house");

                // A floor torn off: the lower house stands at once.
                board.SetBuildingLevel(town, 1, animate: false, leaving: Leaving.Demolished);
                for (int i = 0; i < board.BuildingPieces(town).Count; i++) Assert.AreEqual(i == 0, board.BuildingPieces(town)[i].activeSelf, $"house {i + 1}");

                // The last building goes: the plot is back; the town goes: nothing stands on it.
                board.SetBuildingLevel(town, 0, animate: false, leaving: Leaving.Demolished);
                foreach (GameObject piece in board.BuildingPieces(town)) Assert.IsFalse(piece.activeSelf, "a building stays on a town with none");
                Assert.IsTrue(board.MarkerShown(town), "the plot is not back");
                board.ClearOwner(town, animate: false, leaving: Leaving.Demolished);
                Assert.IsFalse(board.MarkerShown(town), "the plot stays on a town nobody owns");
                Assert.AreEqual(0, board.LeavingCount);
                Assert.AreEqual(0, holder.GetComponentsInChildren<LeavingBurst>(true).Length, "a burst flies with no animation asked");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }

        private static float Luma(Color32 colour) => 0.3f * colour.r + 0.59f * colour.g + 0.11f * colour.b;

        private static void AssertSame(Vector3 expected, Vector3 actual, string what)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-3f, what);
            Assert.AreEqual(expected.y, actual.y, 1e-3f, what);
            Assert.AreEqual(expected.z, actual.z, 1e-3f, what);
        }
    }
}
