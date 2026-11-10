using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Game;
using Lazpoly.Presentation.Tabletop;
using Lazpoly.Presentation.Texts;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    /// <summary>Faz 28: the roll's total in the middle of the screen in place of the two dice printed on the lawn.</summary>
    public class RollTotalTests
    {
        [SetUp]
        public void SetUp() => Lang.Set(Lazpoly.Infra.Settings.Language.Turkish);

        [Test]
        public void TheTotalIsTheSumOfBothDiceAndADoubleSaysCift()
        {
            Assert.AreEqual("7", RollTotal.Text(3, 4));
            Assert.AreEqual("12", RollTotal.Text(6, 6));
            Assert.AreEqual("", RollTotal.Text(0, 0), "nothing before the first roll");
            Assert.IsNull(RollTotal.Note(3, 4), "no word under a plain roll");
            Assert.AreEqual("ÇİFT", RollTotal.Note(2, 2));
            Assert.IsNull(RollTotal.Note(0, 0));
            Assert.AreEqual("DOUBLE", Lang.Load(Lazpoly.Infra.Settings.Language.English)["ÇİFT"]);
        }

        [Test]
        public void TheTotalPopsInStaysAndIsGoneBeforeTheGameGoesOn()
        {
            Assert.AreEqual(0f, RollTotal.Alpha(-0.01f));
            Assert.AreEqual(0f, RollTotal.Alpha(0f), "it fades in");
            Assert.AreEqual(1f, RollTotal.Alpha(RollTotal.Seconds / 2f), "fully there in the middle");
            Assert.Less(RollTotal.Alpha(RollTotal.Seconds - 0.05f), 0.5f, "fading at the end");
            Assert.AreEqual(0f, RollTotal.Alpha(RollTotal.Seconds), "gone when the table goes on");
            Assert.IsFalse(RollTotal.Showing(RollTotal.Seconds));
            Assert.IsTrue(RollTotal.Showing(0.5f));
            Assert.That(RollTotal.Seconds, Is.InRange(0.8f, 1.5f), "long enough to read, short enough not to hold the game up");

            Assert.That(RollTotal.Scale(0f), Is.EqualTo(0.6f).Within(1e-4f));
            Assert.Greater(RollTotal.Scale(RollTotal.PopSeconds * 0.7f), 1f, "a little overshoot");
            Assert.That(RollTotal.Scale(RollTotal.PopSeconds), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(RollTotal.Scale(1f), Is.EqualTo(1f).Within(1e-4f));

            float fast = RollTotal.Seconds / 2f;
            Assert.AreEqual(0f, RollTotal.Alpha(fast, fast), "at double speed it is gone in half the time");
            Assert.Greater(RollTotal.Alpha(fast / 2f, fast), 0.9f);
        }

        [Test]
        public void TheGameClockRunsOnlyWhileTheGameIsPlayed()
        {
            Assert.IsTrue(GameSceneController.GameClockRuns(paused: false, over: false, opening: false));
            Assert.IsFalse(GameSceneController.GameClockRuns(paused: true, over: false, opening: false), "stands still while paused");
            Assert.IsFalse(GameSceneController.GameClockRuns(paused: false, over: true, opening: false), "stops when the game is over");
            Assert.IsFalse(GameSceneController.GameClockRuns(paused: false, over: false, opening: true),
                "waits for the starting money and the draw");
        }

        [Test]
        public void TheLawnNoLongerPrintsTheDice()
        {
            BoardData data = BoardDataLoader.LoadFromResources();
            Assert.IsFalse(data.Board.HasPot, "the shipped board has no pot line either");
            var holder = new GameObject("Board Test");
            try
            {
                BoardView board = BoardView.Create(holder.transform, data);
                Assert.IsNull(board.transform.Find("Scoreboard"), "no dice figures on the lawn (the owner's wish, Faz 28)");
                foreach (TextMesh text in board.GetComponentsInChildren<TextMesh>(true))
                    Assert.AreNotEqual("–  –", text.text, $"{text.name} still prints the dice");

                // The total stands up the screen from the lawn's middle (the dice rest below it), well short of the clock sign.
                float up = Vector3.Dot(board.transform.InverseTransformPoint(board.RollTotalSpot), BoardView.ScreenUp(BoardCameraRig.BaseYaw));
                Assert.Greater(up, 0f);
                Assert.Less(up, board.Inner * BoardView.ClockOffset * 0.5f, "clear of the clock sign");

                // The table carries no logo (the owner's wish, 2026-11-10).
                Assert.IsNull(board.transform.Find("Title"), "no logo on the lawn");
                foreach (TextMesh text in board.GetComponentsInChildren<TextMesh>(true))
                    Assert.AreNotEqual("CASH FATHER", text.text, $"{text.name} prints the name");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }
    }
}
