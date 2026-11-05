using System;
using System.Collections.Generic;
using Lazpoly.App.AI;
using Lazpoly.App.Save;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Random;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Perf;
using Lazpoly.Presentation.Tabletop;
using NUnit.Framework;

namespace Lazpoly.Presentation.Tests
{
    public class PerfTests
    {
        private static List<float> Frames(float seconds, int count)
        {
            var frames = new List<float>(count);
            for (int i = 0; i < count; i++) frames.Add(seconds);
            return frames;
        }

        [Test]
        public void FrameRateIsCountedFromFrameTimes()
        {
            List<float> steady = Frames(1f / 60f, 600);
            Assert.AreEqual(60f, PerfReport.AverageFps(steady), 0.01f);
            Assert.AreEqual(60f, PerfReport.OnePercentLowFps(steady), 0.01f);

            // Six hitches of 100 ms among 600 frames: the average barely moves, the slowest one percent shows them.
            for (int i = 0; i < 6; i++) steady[i * 100] = 0.1f;
            Assert.Greater(PerfReport.AverageFps(steady), 50f);
            Assert.AreEqual(10f, PerfReport.OnePercentLowFps(steady), 0.01f);

            Assert.AreEqual(0f, PerfReport.AverageFps(new List<float>()));
            Assert.AreEqual(0f, PerfReport.OnePercentLowFps(null));
        }

        [Test]
        public void GatesFollowThePlan()
        {
            var medium = new PerfSample { FrameSeconds = Frames(1f / 65f, 300), AllocatedMegabytes = 420f, LoadSeconds = 1.8f, Quality = "Medium" };
            Assert.IsTrue(PerfReport.PassesFps(medium));
            Assert.IsTrue(PerfReport.PassesMemory(medium));
            Assert.IsTrue(PerfReport.PassesLoad(medium));

            var slow = new PerfSample { FrameSeconds = Frames(1f / 40f, 300), AllocatedMegabytes = 700f, LoadSeconds = 4f, Quality = "High" };
            Assert.IsFalse(PerfReport.PassesFps(slow));
            Assert.IsFalse(PerfReport.PassesMemory(slow));
            Assert.IsFalse(PerfReport.PassesLoad(slow));

            var low = new PerfSample { FrameSeconds = Frames(1f / 40f, 300), AllocatedMegabytes = 300f, LoadSeconds = 1f, Quality = "Low" };
            Assert.IsTrue(PerfReport.PassesFps(low), "Low is judged against 30 FPS");
            Assert.IsFalse(PerfReport.PassesLoad(new PerfSample { LoadSeconds = -1f }), "an unmeasured load time does not pass");
        }

        [Test]
        public void TheReportNamesEveryGateAndItsVerdict()
        {
            var sample = new PerfSample
            {
                FrameSeconds = Frames(1f / 72f, 1000), AllocatedMegabytes = 388f, ReservedMegabytes = 512f, LoadSeconds = 1.25f,
                ScreenWidth = 1920, ScreenHeight = 1080, Quality = "Medium", Players = 8, Version = "0.10.3", Device = "Test GPU · Test CPU",
            };
            string report = PerfReport.Format(sample, new DateTime(2026, 11, 1, 18, 30, 0));
            StringAssert.Contains("2026-11-01 18:30", report);
            StringAssert.Contains("1920x1080", report);
            StringAssert.Contains("72.0 FPS", report);
            StringAssert.Contains("388 MB", report);
            StringAssert.Contains("1.25 sn", report);
            StringAssert.Contains("Kare hızı (ortalama)", report);
            StringAssert.Contains("Bellek", report);
            StringAssert.Contains("Yükleme", report);
            Assert.AreEqual(3, CountOf(report, "| geçti |"));
            StringAssert.DoesNotContain("KALDI", report);
        }

        [Test]
        public void ThePerfFlagIsReadFromTheCommandLine()
        {
            Assert.IsTrue(PerfRun.IsRequested(new[] { "Lazpoly.exe", "-perf" }));
            Assert.IsTrue(PerfRun.IsRequested(new[] { "Lazpoly.exe", "-PERF", "-screen-width", "1920" }));
            Assert.IsFalse(PerfRun.IsRequested(new[] { "Lazpoly.exe" }));
            Assert.IsFalse(PerfRun.IsRequested(null));
            Assert.AreEqual(PerfRun.PlayerCount, PerfRun.Seats().Count);
            Assert.AreEqual(PerfRun.PlayerCount, PerfRun.Ai().Count, "every seat at the perf table is a computer");
        }

        [Test]
        public void TheBuiltTableOwnsEveryTownAndShowsEveryLevel()
        {
            Assert.IsTrue(PerfRun.HasArgument(new[] { "Lazpoly.exe", "-perf", "-BUILT" }, PerfRun.BuiltFlag));
            Assert.IsFalse(PerfRun.HasArgument(new[] { "Lazpoly.exe", "-perf" }, PerfRun.BuiltFlag));

            var board = new BoardDefinition(new[]
            {
                SquareDefinition.Start("Start"),
                SquareDefinition.Property("A1", 60, 6, 1, 50, new[] { 30, 90, 240, 330, 420 }),
                SquareDefinition.Property("A2", 80, 8, 1, 50, new[] { 40, 120, 320, 440, 560 }),
                SquareDefinition.Tax("Tax", 100),
                SquareDefinition.Property("B1", 120, 12, 2, 100, new[] { 60, 180, 480, 660, 840 }),
                SquareDefinition.Property("B2", 140, 14, 2, 100, new[] { 70, 210, 560, 770, 980 }),
                SquareDefinition.Rest("Rest"),
                SquareDefinition.Property("C1", 200, 20, 3, 150, new[] { 100, 300, 800, 1100, 1400 }),
                SquareDefinition.Property("C2", 220, 22, 3, 150, new[] { 110, 330, 880, 1210, 1540 }),
                SquareDefinition.Property("Solo", 150, 15, 0),
            });
            var rules = new RulesConfig(800, 100, 6, 3, 2, 50);
            var engine = new GameEngine(board, rules, PerfRun.Seats(), new Pcg32Random(3UL));
            GameStateSnapshot snapshot = GameStateSnapshot.Capture(engine.State);
            PerfRun.BuildUp(snapshot, board);

            GameEngine built = GameEngine.Restore(board, rules, snapshot, new Pcg32Random(3UL));
            var levels = new HashSet<int>();
            for (int i = 0; i < board.Count; i++)
            {
                bool town = board[i].Type == SquareType.Property;
                Assert.AreEqual(town, built.State.GetOwner(i) != GameState.NoOwner, $"square {i}");
                if (board[i].IsDevelopable) levels.Add(built.State.GetBuildingLevel(i));
                else Assert.AreEqual(0, built.State.GetBuildingLevel(i));
            }

            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3, 4, 5 }, levels, "every building level, and the bare plot, stands somewhere");
            foreach (PlayerState player in built.State.Players) Assert.AreEqual(PerfRun.BuiltMoney, player.Money);
        }

        [Test]
        public void TheDebtTableLeavesAPersonSellingTownsForRentTheyCannotPay()
        {
            BoardDefinition board = BoardDataLoader.LoadFromResources().Board;
            var rules = new RulesConfig(800, 100, 6, 3, 2, 50);
            var engine = new GameEngine(board, rules, PerfRun.Seats(), new Pcg32Random(3UL));
            var save = new SavedGame { Snapshot = GameStateSnapshot.Capture(engine.State) };
            foreach (PlayerState player in engine.State.Players) save.AiPlayers[player.Id] = new SavedAiPlayer { Difficulty = AiDifficulty.Normal };
            int debtor = engine.State.CurrentPlayer.Id;
            PerfRun.OpenDebt(save, board);

            Assert.IsFalse(save.AiPlayers.ContainsKey(debtor), "a person sells, not the computer");
            Assert.AreEqual(PerfRun.PlayerCount - 1, save.AiPlayers.Count);
            Assert.AreEqual(0, save.Table.TurnSeconds, "no clock sells for them during the run");

            GameEngine table = GameEngine.Restore(board, rules, save.Snapshot, new Pcg32Random(3UL));
            GameState state = table.State;
            Assert.AreEqual(GamePhase.AwaitingDebtPayment, state.Phase);
            Assert.AreEqual(debtor, state.CurrentPlayer.Id);
            Assert.AreEqual(debtor, state.Debt.PlayerId);
            Assert.AreEqual(PerfRun.DebtAmount, state.Debt.Amount);
            Assert.AreEqual(PerfRun.DebtMoney, state.FindPlayer(debtor).Money);
            int towns = 0;
            for (int i = 0; i < board.Count; i++) towns += state.GetOwner(i) == debtor ? 1 : 0;
            Assert.AreEqual(PerfRun.DebtTowns, towns);
            Assert.AreNotEqual(RejectionReason.None, table.Validate(new PayDebtCommand(debtor)), "the money does not cover the debt yet");
        }

        [Test]
        public void TheScreenToursTableLeavesThePersonOnATownForSale()
        {
            BoardDefinition board = BoardDataLoader.LoadFromResources().Board;
            var rules = new RulesConfig(800, 100, 6, 3, 2, 50);
            var engine = new GameEngine(board, rules, ScreenTour.Seats(), new Pcg32Random(5UL));
            var save = new SavedGame { Snapshot = GameStateSnapshot.Capture(engine.State) };
            foreach (KeyValuePair<int, AiDifficulty> seat in ScreenTour.Ai()) save.AiPlayers[seat.Key] = new SavedAiPlayer { Difficulty = seat.Value };
            ScreenTour.Stage(save, board);

            Assert.IsFalse(save.AiPlayers.ContainsKey(ScreenTour.PersonId), "the person decides");
            Assert.AreEqual(ScreenTour.PlayerCount - 1, save.AiPlayers.Count, "three computers play the others");
            Assert.AreEqual(0, save.Table.TurnSeconds, "no clock decides for the person while the pictures are taken");

            GameEngine table = GameEngine.Restore(board, rules, save.Snapshot, new Pcg32Random(5UL));
            GameState state = table.State;
            Assert.AreEqual(GamePhase.AwaitingBuyDecision, state.Phase);
            Assert.AreEqual(ScreenTour.PersonId, state.CurrentPlayer.Id);
            int square = state.CurrentPlayer.Position;
            Assert.IsTrue(board[square].IsDevelopable, "a town with buildings to show on the cards");
            Assert.AreEqual(GameState.NoOwner, state.GetOwner(square), "for sale");
            Assert.AreEqual(RejectionReason.None, table.Validate(new BuyPropertyCommand(ScreenTour.PersonId)), "the person can buy it");
            foreach (PlayerState player in state.Players)
            {
                int towns = 0;
                for (int i = 0; i < board.Count; i++) towns += state.GetOwner(i) == player.Id ? 1 : 0;
                Assert.AreEqual(player.Id == ScreenTour.PersonId ? 0 : ScreenTour.TownsEach, towns, player.Name);
            }

            Assert.AreEqual("03-ayarlar.png", ScreenTour.FileName(3, "ayarlar"));
            Assert.IsTrue(PerfRun.HasArgument(new[] { "Lazpoly.exe", "-SHOTS" }, ScreenTour.Flag));
        }

        [Test]
        public void ScreenshotPresetsAreDistinctAndWithinTheCamerasReach()
        {
            Assert.AreEqual(5, ScreenshotPresets.All.Length);
            var names = new HashSet<string>();
            foreach (ScreenshotPreset preset in ScreenshotPresets.All)
            {
                Assert.IsTrue(names.Add(preset.Name), $"{preset.Name} twice");
                Assert.That(preset.Zoom, Is.InRange(BoardCameraRig.MinZoom, BoardCameraRig.MaxZoom));
            }

            Assert.AreEqual(ScreenshotPresets.All[0].Name, ScreenshotPresets.At(5).Name, "stepping wraps round");
            Assert.AreEqual(ScreenshotPresets.All[4].Name, ScreenshotPresets.At(-1).Name);
            Assert.AreEqual("lazpoly-20261101-183000.png", Screenshots.FileName(new DateTime(2026, 11, 1, 18, 30, 0)));
        }

        private static int CountOf(string text, string part)
        {
            int count = 0;
            for (int at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal)) count++;
            return count;
        }
    }
}
