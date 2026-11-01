using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Lazpoly.Presentation.Perf
{
    /// <summary>What one performance run measured; <see cref="PerfReport"/> turns it into text and verdicts.</summary>
    public sealed class PerfSample
    {
        public IReadOnlyList<float> FrameSeconds { get; set; } = Array.Empty<float>();
        public float AllocatedMegabytes { get; set; }
        public float ReservedMegabytes { get; set; }
        public float LoadSeconds { get; set; } = -1f;
        public int ScreenWidth { get; set; }
        public int ScreenHeight { get; set; }
        public string Quality { get; set; } = "";
        public int Players { get; set; }
        public string Version { get; set; } = "";
        public string Device { get; set; } = "";
    }

    /// <summary>
    /// The performance gates from the polish plan and the arithmetic behind them: average frame rate,
    /// the slowest one percent of frames, memory and the menu-to-table load time. Pure, so the gates
    /// are tested without a scene.
    /// </summary>
    public static class PerfReport
    {
        /// <summary>Frames per second the game must hold at 1080p on Medium quality (the plan's gate).</summary>
        public const float TargetFps = 60f;

        /// <summary>Frames per second the Low setting must hold on an integrated graphics chip.</summary>
        public const float LowTargetFps = 30f;

        /// <summary>Memory the game scene may use, in megabytes.</summary>
        public const float MemoryLimitMegabytes = 600f;

        /// <summary>Seconds from the menu button to the table on screen.</summary>
        public const float LoadLimitSeconds = 3f;

        public static float AverageFps(IReadOnlyList<float> frameSeconds)
        {
            if (frameSeconds == null || frameSeconds.Count == 0) return 0f;
            double total = 0;
            foreach (float frame in frameSeconds) total += frame;
            return total <= 0 ? 0f : (float)(frameSeconds.Count / total);
        }

        /// <summary>The frame rate of the slowest one percent of frames: the stutter a player feels.</summary>
        public static float OnePercentLowFps(IReadOnlyList<float> frameSeconds)
        {
            if (frameSeconds == null || frameSeconds.Count == 0) return 0f;
            var sorted = new List<float>(frameSeconds);
            sorted.Sort();
            int slowest = Math.Max(1, sorted.Count / 100);
            double total = 0;
            for (int i = sorted.Count - slowest; i < sorted.Count; i++) total += sorted[i];
            return total <= 0 ? 0f : (float)(slowest / total);
        }

        /// <summary>The gate a quality level has to pass; Low is judged against the integrated-graphics target.</summary>
        public static float FpsTargetFor(string quality) =>
            string.Equals(quality, "Low", StringComparison.OrdinalIgnoreCase) ? LowTargetFps : TargetFps;

        public static bool PassesFps(PerfSample sample) => AverageFps(sample.FrameSeconds) >= FpsTargetFor(sample.Quality);
        public static bool PassesMemory(PerfSample sample) => sample.AllocatedMegabytes > 0f && sample.AllocatedMegabytes < MemoryLimitMegabytes;
        public static bool PassesLoad(PerfSample sample) => sample.LoadSeconds >= 0f && sample.LoadSeconds < LoadLimitSeconds;

        /// <summary>The report as Markdown, ready for the PR description and Reports/Perf.</summary>
        public static string Format(PerfSample sample, DateTime when)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("# Lazpoly performans ölçümü");
            sb.AppendLine();
            sb.AppendLine($"{when.ToString("yyyy-MM-dd HH:mm", c)} · sürüm {sample.Version} · {sample.Device}");
            sb.AppendLine($"{sample.ScreenWidth}x{sample.ScreenHeight} · kalite {sample.Quality} · {sample.Players} bilgisayar oyuncu · {sample.FrameSeconds.Count} kare ölçüldü");
            sb.AppendLine();
            sb.AppendLine("| Kapı | Hedef | Ölçüm | Sonuç |");
            sb.AppendLine("|---|---|---|---|");
            sb.AppendLine($"| Kare hızı (ortalama) | ≥ {FpsTargetFor(sample.Quality):0} FPS | {AverageFps(sample.FrameSeconds).ToString("0.0", c)} FPS | {Verdict(PassesFps(sample))} |");
            sb.AppendLine($"| Kare hızı (en yavaş %1) | bilgi | {OnePercentLowFps(sample.FrameSeconds).ToString("0.0", c)} FPS | – |");
            sb.AppendLine($"| Bellek (ayrılmış) | < {MemoryLimitMegabytes:0} MB | {sample.AllocatedMegabytes.ToString("0", c)} MB (rezerve {sample.ReservedMegabytes.ToString("0", c)} MB) | {Verdict(PassesMemory(sample))} |");
            string load = sample.LoadSeconds >= 0f ? sample.LoadSeconds.ToString("0.00", c) + " sn" : "ölçülmedi";
            sb.AppendLine($"| Yükleme (menü → masa) | < {LoadLimitSeconds:0} sn | {load} | {Verdict(PassesLoad(sample))} |");
            return sb.ToString();
        }

        private static string Verdict(bool passed) => passed ? "geçti" : "KALDI";
    }
}
