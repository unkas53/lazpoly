using System;
using System.Collections.Generic;
using System.IO;
using Lazpoly.App.AI;
using Lazpoly.Core.State;
using UnityEngine;

namespace Lazpoly.Presentation.Perf
{
    /// <summary>
    /// The automatic performance run: started with <c>Lazpoly.exe -perf</c>, the menu opens a table of
    /// eight computer players at once, the probe measures for <see cref="Seconds"/>, the report lands in
    /// <c>lazpoly-perf.txt</c> next to the game and the game quits. The build step runs it after every build.
    /// </summary>
    public static class PerfRun
    {
        public const string Flag = "-perf";
        public const float WarmUpSeconds = 3f;
        public const float Seconds = 15f;
        public const int PlayerCount = 8;
        public const string ReportFileName = "lazpoly-perf.txt";

        private static bool? _requested;
        private static bool _started;

        /// <summary>Whether this process was started with <see cref="Flag"/>.</summary>
        public static bool Requested
        {
            get
            {
                if (!_requested.HasValue) _requested = IsRequested(Environment.GetCommandLineArgs());
                return _requested.Value;
            }
        }

        /// <summary>True once the menu has sent the run to the table, so a return to the menu does not start it again.</summary>
        public static bool Started => _started;

        public static bool IsRequested(string[] args)
        {
            if (args == null) return false;
            foreach (string arg in args) if (string.Equals(arg, Flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Next to the executable (the project folder in the editor), so the build step can pick it up.</summary>
        public static string ReportPath => Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? Application.persistentDataPath, ReportFileName);

        /// <summary>Eight computer players, mixed difficulties, as the heaviest table the game offers.</summary>
        public static IReadOnlyList<PlayerSeat> Seats()
        {
            var seats = new PlayerSeat[PlayerCount];
            for (int i = 0; i < PlayerCount; i++) seats[i] = new PlayerSeat(i + 1, $"Bilgisayar {i + 1}");
            return seats;
        }

        public static IReadOnlyDictionary<int, AiDifficulty> Ai()
        {
            var ai = new Dictionary<int, AiDifficulty>();
            for (int i = 0; i < PlayerCount; i++) ai[i + 1] = (AiDifficulty)(1 + i % 3);
            return ai;
        }

        /// <summary>Marks the run as sent to the table.</summary>
        public static void MarkStarted() => _started = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _requested = null;
            _started = false;
        }
    }
}
