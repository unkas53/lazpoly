using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Lazpoly.App.AI;
using Lazpoly.App.Save;
using Lazpoly.Core.Board;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Setup;
using UnityEngine;

namespace Lazpoly.Presentation.Perf
{
    /// <summary>
    /// The screen tour: started with <c>Lazpoly.exe -shots</c>, the game opens its windows one after another and
    /// keeps a picture of each in <c>lazpoly-shots</c> next to the game, then quits, so every window's look can
    /// be checked on a build without a hand on the mouse. The menu shows its home screen, characters, settings,
    /// how to play, each mode's sheet and the table rules; then a table of a person and three computers opens
    /// with the person on a town for sale (<see cref="Stage"/>), for the city card, the building card, a square's
    /// card, a resort's card, the table, the properties, a trade, the log, the pause menu and its settings and help. The tour
    /// never touches the save slot. The debt's sale screen has its own run (<c>-perf -debt</c>).
    /// </summary>
    public static class ScreenTour
    {
        public const string Flag = "-shots";
        public const string FolderName = "lazpoly-shots";

        /// <summary>The person at the tour's table: seat 1, the computers sit in 2-4.</summary>
        public const int PersonId = 1;
        public const int PlayerCount = 4;

        /// <summary>How long a window is given to open, fade in and settle before its picture is taken.</summary>
        public const float SettleSeconds = 0.8f;

        /// <summary>The person's money at the tour's table: enough for the dearest town and its buildings.</summary>
        public const int PersonMoney = 5000;

        /// <summary>How many towns each computer holds at the start, so the corner cards and the board show owners.</summary>
        public const int TownsEach = 2;

        /// <summary>The resorts the first two computers hold, so the board shows two stages of the beach.</summary>
        public static readonly int[] ResortsHeld = { 3, 1 };

        private static bool? _requested;
        private static bool _menuShown;
        private static int _count;

        /// <summary>Whether this process was started with <see cref="Flag"/>.</summary>
        public static bool Requested
        {
            get
            {
                if (!_requested.HasValue) _requested = PerfRun.HasArgument(Environment.GetCommandLineArgs(), Flag);
                return _requested.Value;
            }
        }

        /// <summary>True once the menu's part of the tour is done, so a return to the menu does not start it again.</summary>
        public static bool MenuShown => _menuShown;

        public static void MarkMenuShown() => _menuShown = true;

        /// <summary>Next to the executable (the project folder in the editor).</summary>
        public static string Folder => Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? Application.persistentDataPath, FolderName);

        /// <summary>The picture's file name: its place in the tour and what it shows, "03-ayarlar.png".</summary>
        public static string FileName(int number, string name) => $"{number:00}-{name}.png";

        /// <summary>Waits for the screen to settle, then keeps a picture of it; the file is written over the next frame.</summary>
        public static IEnumerator Take(string name)
        {
            yield return new WaitForSecondsRealtime(SettleSeconds);
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, FileName(++_count, name));
            ScreenCapture.CaptureScreenshot(path, 1);
            Debug.Log($"[Lazpoly] Tour picture: {path}");
            yield return null;
            yield return null;
        }

        /// <summary>Waits until <paramref name="done"/> holds or <paramref name="seconds"/> pass, whichever comes first.</summary>
        public static IEnumerator Until(Func<bool> done, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < end) yield return null;
        }

        /// <summary>A person and three computers of each difficulty.</summary>
        public static IReadOnlyList<PlayerSeat> Seats()
        {
            var seats = new PlayerSeat[PlayerCount];
            seats[0] = new PlayerSeat(PersonId, "Oyuncu", CharacterChoice.ForSeat(0).Id);
            for (int i = 1; i < PlayerCount; i++) seats[i] = new PlayerSeat(i + 1, $"Bilgisayar {i}", CharacterChoice.ForSeat(i).Id);
            return seats;
        }

        public static IReadOnlyDictionary<int, AiDifficulty> Ai()
        {
            var ai = new Dictionary<int, AiDifficulty>();
            for (int i = 1; i < PlayerCount; i++) ai[i + 1] = (AiDifficulty)i;
            return ai;
        }

        /// <summary>The turns the jailed computer of the tour still waits.</summary>
        public const int JailedTurns = 2;

        /// <summary>
        /// Sets the tour's table: the computers hold <see cref="TownsEach"/> towns each and the first two the
        /// <see cref="ResortsHeld"/> resorts, everyone has been round
        /// the board once, the first computer sits in jail, and the person is on turn standing on the first town for sale with
        /// <see cref="PersonMoney"/>, deciding whether to buy it, at a table without a clock.
        /// </summary>
        public static void Stage(SavedGame save, BoardDefinition board)
        {
            if (save?.Snapshot == null) throw new ArgumentNullException(nameof(save));
            if (board == null) throw new ArgumentNullException(nameof(board));
            GameStateSnapshot snapshot = save.Snapshot;
            int person = snapshot.Players.FindIndex(p => p.Id == PersonId);
            if (person < 0 || snapshot.Owners.Length != board.Count) throw new ArgumentException("The snapshot is not the tour's table.", nameof(save));

            var towns = new List<int>();
            for (int i = 0; i < board.Count; i++)
            {
                if (board[i].Type == SquareType.Property && board[i].IsDevelopable) towns.Add(i);
            }

            int next = 1; // The first developable town stays for sale: the person stands on it.
            foreach (GameStateSnapshot.PlayerEntry player in snapshot.Players)
            {
                player.Laps = Math.Max(player.Laps, 1);
                if (player.Id == PersonId) continue;
                for (int n = 0; n < TownsEach && next < towns.Count; n++) snapshot.Owners[towns[next++]] = player.Id;
            }

            var resorts = new List<int>();
            for (int i = 0; i < board.Count; i++)
            {
                if (board[i].IsResort) resorts.Add(i);
            }

            int resort = 0, computer = 0;
            foreach (GameStateSnapshot.PlayerEntry player in snapshot.Players)
            {
                if (player.Id == PersonId) continue;
                if (computer >= ResortsHeld.Length) break;
                for (int n = 0; n < ResortsHeld[computer] && resort < resorts.Count; n++) snapshot.Owners[resorts[resort++]] = player.Id;
                computer++;
            }

            // One computer sits in jail, so the pictures show the cell with a pawn in it and the card saying so.
            GameStateSnapshot.PlayerEntry jailed = snapshot.Players.Find(p => p.Id != PersonId);
            if (board.HasJail && jailed != null)
            {
                jailed.Position = board.JailIndex;
                jailed.JailTurns = JailedTurns;
            }

            GameStateSnapshot.PlayerEntry here = snapshot.Players[person];
            here.Money = PersonMoney;
            if (towns.Count > 0) here.Position = towns[0];
            snapshot.CurrentPlayerIndex = person;
            snapshot.Phase = GamePhase.AwaitingBuyDecision;
            snapshot.TurnNumber = Math.Max(snapshot.TurnNumber, 2);
            save.AiPlayers.Remove(PersonId);
            save.Table = save.Table.WithTurnSeconds(0);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _requested = null;
            _menuShown = false;
            _count = 0;
        }
    }
}
