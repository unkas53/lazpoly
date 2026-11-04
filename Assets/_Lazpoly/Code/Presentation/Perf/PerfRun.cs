using System;
using System.Collections.Generic;
using System.IO;
using Lazpoly.App.AI;
using Lazpoly.Core.Board;
using Lazpoly.Core.State;
using UnityEngine;

namespace Lazpoly.Presentation.Perf
{
    /// <summary>
    /// The automatic performance run: started with <c>Lazpoly.exe -perf</c>, the menu opens a table of
    /// eight computer players at once, the probe measures for <see cref="Seconds"/>, the report lands in
    /// <c>lazpoly-perf.txt</c> next to the game and the game quits. The build step runs it after every build.
    /// The run leaves the player's save slot alone (see GameSceneController.SaveLocalGame).
    /// With <see cref="BuiltFlag"/> as well, the table starts fully built (<see cref="BuildUp"/>), the
    /// heaviest board there can be, and every building level can be looked at.
    /// </summary>
    public static class PerfRun
    {
        public const string Flag = "-perf";
        public const float WarmUpSeconds = 3f;
        public const float Seconds = 15f;
        public const int PlayerCount = 8;
        public const string ReportFileName = "lazpoly-perf.txt";

        /// <summary>With <see cref="Flag"/>: every town starts owned and built.</summary>
        public const string BuiltFlag = "-built";

        /// <summary>Everyone's money on the built table, so nobody goes bankrupt (and loses their buildings) during the run.</summary>
        public const int BuiltMoney = 100000;

        private static bool? _requested;
        private static bool? _builtUp;
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

        /// <summary>Whether the run asks for the built table (<see cref="BuiltFlag"/>); it means nothing without <see cref="Flag"/>.</summary>
        public static bool BuiltUp
        {
            get
            {
                if (!_builtUp.HasValue) _builtUp = HasArgument(Environment.GetCommandLineArgs(), BuiltFlag);
                return _builtUp.Value;
            }
        }

        public static bool IsRequested(string[] args) => HasArgument(args, Flag);

        public static bool HasArgument(string[] args, string flag)
        {
            if (args == null) return false;
            foreach (string arg in args) if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>
        /// Builds up a new game's snapshot: every town goes to the players in turn, and the developable ones
        /// carry levels from the hotel down to the bare plot and round again, so each building (and the owner's
        /// plot) stands somewhere and most of the board is tall. Everyone gets <see cref="BuiltMoney"/>.
        /// </summary>
        public static void BuildUp(GameStateSnapshot snapshot, BoardDefinition board)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (snapshot.Players.Count == 0 || snapshot.Owners.Length != board.Count) throw new ArgumentException("The snapshot does not fit the board.", nameof(snapshot));

            int town = 0, built = 0;
            for (int i = 0; i < board.Count; i++)
            {
                if (board[i].Type != SquareType.Property) continue;
                snapshot.Owners[i] = snapshot.Players[town++ % snapshot.Players.Count].Id;
                snapshot.BuildingLevels[i] = board[i].IsDevelopable ? SquareDefinition.MaxBuildingLevel - built++ % (SquareDefinition.MaxBuildingLevel + 1) : 0;
            }

            foreach (GameStateSnapshot.PlayerEntry player in snapshot.Players) player.Money = BuiltMoney;
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
            _builtUp = null;
            _started = false;
        }
    }
}
