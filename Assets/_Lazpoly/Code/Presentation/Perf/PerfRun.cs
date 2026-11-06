using System;
using System.Collections.Generic;
using System.IO;
using Lazpoly.App.AI;
using Lazpoly.App.Save;
using Lazpoly.Core.Board;
using Lazpoly.Core.Events;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Setup;
using UnityEngine;

namespace Lazpoly.Presentation.Perf
{
    /// <summary>
    /// The automatic performance run: started with <c>Lazpoly.exe -perf</c>, the menu opens a table of
    /// eight computer players at once, the probe measures for <see cref="Seconds"/>, the report lands in
    /// <c>lazpoly-perf.txt</c> next to the game and the game quits. The build step runs it after every build.
    /// The run leaves the player's save slot alone (see GameSceneController.SaveLocalGame).
    /// With <see cref="BuiltFlag"/> as well, the table starts fully built (<see cref="BuildUp"/>), the
    /// heaviest board there can be, and every building level can be looked at. With <see cref="DebtFlag"/>
    /// the first player is a person who owes rent they cannot pay (<see cref="OpenDebt"/>), so the sale
    /// screen and its tags over the towns stand on the measured table.
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

        /// <summary>With <see cref="Flag"/>: the player on turn is a person in debt, selling.</summary>
        public const string DebtFlag = "-debt";

        /// <summary>The debt <see cref="OpenDebt"/> sets up, against the debtor's <see cref="DebtMoney"/>.</summary>
        public const int DebtAmount = 100;
        public const int DebtMoney = 10;

        /// <summary>How many towns the debtor holds, spread around the board.</summary>
        public const int DebtTowns = 4;

        private static bool? _requested;
        private static bool? _builtUp;
        private static bool? _inDebt;
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

        /// <summary>Whether the run asks for the sale screen (<see cref="DebtFlag"/>); it means nothing without <see cref="Flag"/>.</summary>
        public static bool InDebt
        {
            get
            {
                if (!_inDebt.HasValue) _inDebt = HasArgument(Environment.GetCommandLineArgs(), DebtFlag);
                return _inDebt.Value;
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

        /// <summary>
        /// Puts the player on turn in debt: a person (no longer played by the computer) with
        /// <see cref="DebtMoney"/> and <see cref="DebtTowns"/> towns, owing the next player <see cref="DebtAmount"/>
        /// in rent, at a table without a clock so the sale screen stays up for the whole run.
        /// </summary>
        public static void OpenDebt(SavedGame save, BoardDefinition board)
        {
            if (save?.Snapshot == null) throw new ArgumentNullException(nameof(save));
            if (board == null) throw new ArgumentNullException(nameof(board));
            GameStateSnapshot snapshot = save.Snapshot;
            if (snapshot.Players.Count < 2 || snapshot.Owners.Length != board.Count) throw new ArgumentException("The snapshot does not fit the board.", nameof(save));

            GameStateSnapshot.PlayerEntry debtor = snapshot.Players[snapshot.CurrentPlayerIndex];
            GameStateSnapshot.PlayerEntry creditor = snapshot.Players[(snapshot.CurrentPlayerIndex + 1) % snapshot.Players.Count];
            var towns = new List<int>();
            for (int i = 0; i < board.Count; i++)
            {
                if (board[i].Type == SquareType.Property) towns.Add(i);
            }

            int step = Math.Max(1, towns.Count / DebtTowns);
            for (int n = 0; n < DebtTowns && n * step < towns.Count; n++) snapshot.Owners[towns[n * step]] = debtor.Id;
            debtor.Money = DebtMoney;
            if (towns.Count > 0) debtor.Position = towns[0];
            snapshot.Phase = GamePhase.AwaitingDebtPayment;
            snapshot.TurnNumber = Math.Max(snapshot.TurnNumber, 2);
            snapshot.Debt = new DebtState(debtor.Id, creditor.Id, DebtAmount, MoneyReason.Rent);
            save.AiPlayers.Remove(debtor.Id);
            save.Table = save.Table.WithTurnSeconds(0);
        }

        /// <summary>Next to the executable (the project folder in the editor), so the build step can pick it up.</summary>
        public static string ReportPath => Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? Application.persistentDataPath, ReportFileName);

        /// <summary>Eight computer players, mixed difficulties, as the heaviest table the game offers.</summary>
        public static IReadOnlyList<PlayerSeat> Seats()
        {
            var seats = new PlayerSeat[PlayerCount];
            for (int i = 0; i < PlayerCount; i++) seats[i] = new PlayerSeat(i + 1, $"Bilgisayar {i + 1}", CharacterChoice.ForSeat(i).Id);
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
            _inDebt = null;
            _started = false;
        }
    }
}
