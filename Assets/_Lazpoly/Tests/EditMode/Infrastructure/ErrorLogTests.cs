using System;
using System.IO;
using Lazpoly.Infra.Logging;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Infra.Tests
{
    public class ErrorLogTests
    {
        private string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "lazpoly-log-test-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        [Test]
        public void AnEntryHasAStampAKindTheMessageAndTheIndentedTrace()
        {
            string text = ErrorLog.Format(new DateTime(2026, 10, 31, 21, 5, 9), "Exception", "NullReferenceException: boom\nsecond line", "at A.B()\n\nat C.D()\r\n");

            string[] lines = text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            Assert.AreEqual("2026-10-31 21:05:09 EXCEPTION NullReferenceException: boom second line", lines[0]);
            Assert.AreEqual("    at A.B()", lines[1]);
            Assert.AreEqual("    at C.D()", lines[2]);
            Assert.AreEqual(string.Empty, lines[3], "An entry ends with a blank line.");
        }

        [Test]
        public void EntriesAppendToTheFileAndTheFolderIsCreated()
        {
            var log = new ErrorLog(Path.Combine(_folder, "deep", ErrorLog.FileName));

            log.BeginRun(DateTime.Now, "1.2.3", "Test");
            log.Append(DateTime.Now, "Error", "first", null);
            log.Append(DateTime.Now, "Error", "second", "trace");

            string text = File.ReadAllText(log.Path);
            StringAssert.Contains("RUN  Cash Father 1.2.3 (Test)", text);
            Assert.Less(text.IndexOf("first", StringComparison.Ordinal), text.IndexOf("second", StringComparison.Ordinal));
        }

        [Test]
        public void AFullLogIsKeptOnceAndStartedAgain()
        {
            var log = new ErrorLog(Path.Combine(_folder, ErrorLog.FileName), maxBytes: 400);

            for (int i = 0; i < 12; i++) log.Append(DateTime.Now, "Error", $"entry {i:00} " + new string('x', 40), null);

            Assert.IsTrue(File.Exists(log.RotatedPath), "The old log is kept as .1.");
            Assert.LessOrEqual(new FileInfo(log.Path).Length, 400);
            StringAssert.Contains("entry 11", File.ReadAllText(log.Path));
            StringAssert.DoesNotContain("entry 11", File.ReadAllText(log.RotatedPath));
        }

        [Test]
        public void AReportIsAnEntryWithItsLinesIndented()
        {
            var log = new ErrorLog(Path.Combine(_folder, ErrorLog.FileName));
            log.AppendReport(new DateTime(2026, 11, 2, 20, 0, 0), "Oyuncu sorun bildirdi", new[] { "oturum: yerel", "", "  12  > Roll(p1) -> ok" });

            string text = File.ReadAllText(log.Path);
            StringAssert.Contains("2026-11-02 20:00:00 REPORT Oyuncu sorun bildirdi", text);
            StringAssert.Contains("    oturum: yerel", text);
            StringAssert.Contains("    12  > Roll(p1) -> ok", text);
        }

        [Test]
        public void OnlySeriousMessagesAreKept()
        {
            Assert.IsTrue(ErrorLogService.Keeps(LogType.Error));
            Assert.IsTrue(ErrorLogService.Keeps(LogType.Exception));
            Assert.IsTrue(ErrorLogService.Keeps(LogType.Assert));
            Assert.IsFalse(ErrorLogService.Keeps(LogType.Warning));
            Assert.IsFalse(ErrorLogService.Keeps(LogType.Log));
        }
    }
}
