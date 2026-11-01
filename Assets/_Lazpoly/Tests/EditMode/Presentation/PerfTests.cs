using System;
using System.Collections.Generic;
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
