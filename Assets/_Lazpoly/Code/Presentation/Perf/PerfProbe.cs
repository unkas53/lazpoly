using System;
using System.Collections.Generic;
using System.IO;
using Lazpoly.Infra;
using Lazpoly.Infra.Settings;
using UnityEngine;
using UnityEngine.Profiling;

namespace Lazpoly.Presentation.Perf
{
    /// <summary>
    /// Watches the frame rate and memory of the running game. The HUD shows its numbers behind F3;
    /// a <c>-perf</c> run (see <see cref="PerfRun"/>) lets it measure for a fixed time, write the
    /// report next to the game and quit.
    /// </summary>
    public sealed class PerfProbe : MonoBehaviour
    {
        /// <summary>Frames kept for the overlay's rolling numbers.</summary>
        private const int Window = 120;

        private readonly Queue<float> _recent = new Queue<float>(Window);
        private List<float> _run;
        private float _runUntil;
        private float _warmUpUntil;
        private Action<PerfSample> _onRunDone;
        private double _recentTotal;

        /// <summary>Seconds from the menu's button to the table on screen, when the scene knows it; -1 otherwise.</summary>
        public float LoadSeconds { get; set; } = -1f;

        /// <summary>Players at the table, for the report.</summary>
        public int Players { get; set; }

        public static PerfProbe Ensure(GameObject host)
        {
            PerfProbe probe = host.GetComponent<PerfProbe>();
            return probe != null ? probe : host.AddComponent<PerfProbe>();
        }

        /// <summary>Frames per second over the last <see cref="Window"/> frames.</summary>
        public float Fps => _recent.Count == 0 || _recentTotal <= 0 ? 0f : (float)(_recent.Count / _recentTotal);

        public static float AllocatedMegabytes => Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f);
        public static float ReservedMegabytes => Profiler.GetTotalReservedMemoryLong() / (1024f * 1024f);

        /// <summary>One line for the overlay: "61 FPS · 412 MB".</summary>
        public string OverlayText() => $"{Fps:0} FPS · {AllocatedMegabytes:0} MB";

        /// <summary>Measures for <paramref name="seconds"/> after <paramref name="warmUpSeconds"/>, then hands the sample over.</summary>
        public void StartRun(float warmUpSeconds, float seconds, Action<PerfSample> onDone)
        {
            _run = new List<float>(Mathf.CeilToInt(seconds * 240f));
            _warmUpUntil = Time.realtimeSinceStartup + warmUpSeconds;
            _runUntil = _warmUpUntil + seconds;
            _onRunDone = onDone;
        }

        public bool IsRunning => _run != null;

        private void Update()
        {
            float frame = Time.unscaledDeltaTime;
            if (frame <= 0f) return;
            _recent.Enqueue(frame);
            _recentTotal += frame;
            if (_recent.Count > Window) _recentTotal -= _recent.Dequeue();

            if (_run == null) return;
            float now = Time.realtimeSinceStartup;
            if (now >= _warmUpUntil) _run.Add(frame);
            if (now < _runUntil) return;

            var sample = new PerfSample
            {
                FrameSeconds = _run,
                AllocatedMegabytes = AllocatedMegabytes,
                ReservedMegabytes = ReservedMegabytes,
                LoadSeconds = LoadSeconds,
                ScreenWidth = Screen.width,
                ScreenHeight = Screen.height,
                Quality = QualityName(),
                Players = Players,
                Version = Application.version,
                Device = $"{SystemInfo.graphicsDeviceName} · {SystemInfo.processorType}",
            };
            _run = null;
            _onRunDone?.Invoke(sample);
        }

        private static string QualityName()
        {
            SettingsService settings = null;
            GameServices.Registry?.TryGet(out settings);
            return (settings?.Current.Quality ?? GraphicsQuality.High).ToString();
        }

        /// <summary>Writes the report where the player (and the build step) can find it and says where.</summary>
        public static string WriteReport(PerfSample sample)
        {
            string path = PerfRun.ReportPath;
            File.WriteAllText(path, PerfReport.Format(sample, DateTime.Now));
            return path;
        }
    }
}
