using Lazpoly.Presentation.Dice;
using Lazpoly.Presentation.Tabletop;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    public class SceneryTests
    {
        [Test]
        public void TheDayRunsFromNoonToDuskOverTheGame()
        {
            Assert.AreEqual(0f, DayCycle.Progress(1, 25, gameOver: false));
            Assert.AreEqual(1f, DayCycle.Progress(25, 25, gameOver: false));
            Assert.AreEqual(1f, DayCycle.Progress(40, 25, gameOver: false), "past the limit it stays dusk");
            Assert.AreEqual(1f, DayCycle.Progress(3, 25, gameOver: true), "the end of the game is evening whatever the round");
            Assert.AreEqual(0f, DayCycle.Progress(1, 0, gameOver: false));
            Assert.AreEqual(1f, DayCycle.Progress(DayCycle.RoundsToEvening, 0, gameOver: false), "with no limit a long game reaches dusk on its own");

            float last = -1f;
            for (int round = 1; round <= 30; round++)
            {
                float progress = DayCycle.Progress(round, 0, gameOver: false);
                Assert.GreaterOrEqual(progress, last, "the day never runs backwards");
                Assert.That(progress, Is.InRange(0f, 1f));
                last = progress;
            }
        }

        [Test]
        public void EveningLightIsWarmerLowerAndDimmer()
        {
            DayLight noon = DayCycle.At(0f);
            DayLight dusk = DayCycle.At(1f);
            Assert.AreEqual(0f, noon.Evening);
            Assert.AreEqual(1f, dusk.Evening, 1e-4f);
            Assert.Less(dusk.SunPitch, noon.SunPitch, "the sun is lower in the evening");
            Assert.Less(dusk.SunIntensity, noon.SunIntensity);
            Assert.Greater(dusk.SunColor.r - dusk.SunColor.b, noon.SunColor.r - noon.SunColor.b, "the evening sun is redder");
            Assert.AreEqual(0f, DayCycle.At(0.3f).Evening, 1e-4f, "the first half of the day is still daylight");
            Assert.Greater(DayCycle.At(0.8f).Evening, 0.5f);
        }

        [Test]
        public void WavesStayWithinTheirHeightAndTheSeaFacesUp()
        {
            float highest = 0f;
            for (float x = -5f; x <= 5f; x += 0.37f)
            for (float z = -5f; z <= 5f; z += 0.41f)
            for (float t = 0f; t < 12f; t += 0.3f)
                highest = Mathf.Max(highest, Mathf.Abs(SeaSurface.Height(x, z, t)));
            Assert.LessOrEqual(highest, SeaSurface.Amplitude + 1e-4f);
            Assert.Greater(highest, SeaSurface.Amplitude * 0.8f, "the waves do rise somewhere");

            Mesh sea = SeaSurface.BuildMesh(4f, 6);
            Assert.AreEqual(49, sea.vertexCount);
            Assert.AreEqual(6 * 6 * 2 * 3, sea.triangles.Length);
            Assert.That(sea.bounds.size.x, Is.EqualTo(4f).Within(1e-4f));
            Vector3[] vertices = sea.vertices;
            int[] triangles = sea.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 normal = Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]], vertices[triangles[i + 2]] - vertices[triangles[i]]);
                Assert.Greater(normal.y, 0f, $"triangle {i / 3} of the sea faces down");
            }

            Object.DestroyImmediate(sea);
        }

        [Test]
        public void TheBoatDriftsRoundItsMooringAndComesBack()
        {
            var drift = new Vector2(0.5f, 0.3f);
            Vector3 start = TownLife.BoatOffset(drift, 0f);
            Vector3 later = TownLife.BoatOffset(drift, TownLife.DriftSeconds / 4f);
            Vector3 round = TownLife.BoatOffset(drift, TownLife.DriftSeconds);
            Assert.Greater(Vector3.Distance(start, later), 0.3f);
            Assert.That(Vector3.Distance(start, round), Is.LessThan(1e-3f));
            for (float t = 0f; t < TownLife.DriftSeconds; t += 1f)
            {
                Vector3 at = TownLife.BoatOffset(drift, t);
                Assert.LessOrEqual(Mathf.Abs(at.x), drift.x + 1e-4f);
                Assert.LessOrEqual(Mathf.Abs(at.z), drift.y + 1e-4f);
                Assert.AreEqual(0f, at.y);
            }
        }

        [Test]
        public void ThePeekNeverPullsTheViewOffTheBoard()
        {
            var center = new Vector3(1f, 0f, 2f);
            Vector3 inside = BoardCameraRig.ClampToBoard(new Vector3(3f, 0f, 1f), center, 6f);
            Assert.AreEqual(new Vector3(3f, 0f, 1f), inside);
            Vector3 clamped = BoardCameraRig.ClampToBoard(new Vector3(40f, 0f, -50f), center, 6f);
            Assert.AreEqual(new Vector3(7f, 0f, -4f), clamped);

            var aim = new Vector3(1f, 0f, 3f);
            Assert.AreEqual(aim, BoardCameraRig.PeekAim(aim, null, center, 6f, 0f), "nothing pinned, nothing moves");
            Vector3 peeked = BoardCameraRig.PeekAim(aim, new Vector3(7f, 0f, 3f), center, 6f, 0f);
            Assert.That(peeked.x, Is.EqualTo(aim.x + 6f * BoardCameraRig.PeekAmount).Within(1e-4f), "slides a fraction of the way");
            Assert.AreEqual(aim.y, peeked.y);
            Vector3 farPeek = BoardCameraRig.PeekAim(aim, new Vector3(100f, 0f, 3f), center, 6f, 0f);
            Assert.That(Vector3.Distance(farPeek, peeked), Is.LessThan(1e-4f), "a point off the board counts as its edge");

            // With the view turned a quarter round, a square to the world's +X is ahead (+Z) of the un-turned aim.
            Vector3 turned = BoardCameraRig.PeekAim(aim, new Vector3(7f, 0f, 2f), center, 6f, 90f);
            Assert.That(turned.z - aim.z, Is.GreaterThan(1f));
            Assert.That(Mathf.Abs(turned.x - aim.x), Is.LessThan(0.3f));
        }

        [Test]
        public void SettledDiceSquareUpToTheCameraAndShadowsShrinkInTheAir()
        {
            Assert.AreEqual(30f, DiceView.RestingYaw(30f, 0));
            Assert.AreEqual(30f + DiceView.FacingJitterDegrees, DiceView.RestingYaw(30f, 90), "the jitter is capped");
            Assert.Greater(DiceView.ShadowScale(0f), DiceView.ShadowScale(1f));
            Assert.Greater(DiceView.ShadowScale(1f), 0f);
        }
    }
}
