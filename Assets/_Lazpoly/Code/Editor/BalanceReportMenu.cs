using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Lazpoly.App.Balance;
using Lazpoly.Presentation.Data;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace Lazpoly.Editor
{
    /// <summary>
    /// Simulates many computer games on the shipped board and writes a Markdown report per player
    /// count into Reports/Balance. From the command line:
    /// -executeMethod Lazpoly.Editor.BalanceReportMenu.Run [-balanceGames 1000] [-balancePlayers 2,4,6]
    /// </summary>
    public static class BalanceReportMenu
    {
        public const string OutputFolder = "Reports/Balance";
        private const int DefaultGames = 1000;
        private static readonly int[] DefaultPlayers = { 2, 4, 6 };

        [MenuItem("Lazpoly/Balance Report")]
        public static void Run()
        {
            string[] args = Environment.GetCommandLineArgs();
            int games = int.TryParse(Argument(args, "-balanceGames"), out int parsed) && parsed > 0 ? parsed : DefaultGames;
            string playersText = Argument(args, "-balancePlayers");
            int[] players = playersText == null ? DefaultPlayers : playersText.Split(',').Select(int.Parse).ToArray();

            BoardData data = BoardDataLoader.LoadFromResources();
            Directory.CreateDirectory(OutputFolder);
            foreach (int count in players)
            {
                var settings = new BalanceSettings(games, count);
                Stopwatch clock = Stopwatch.StartNew();
                BalanceResult result = BalanceSimulator.Run(data.Board, data.Rules, settings);
                string report = BalanceReport.Markdown(data.Board, data.Rules, settings, result, id => data.GroupName(id) ?? $"Grup {id}");
                string path = Path.Combine(OutputFolder, $"denge-{count}-oyuncu.md");
                File.WriteAllText(path, report);
                Debug.Log($"Balance report: {path} ({games} games, {count} players, {clock.Elapsed.TotalSeconds:0.0} s).");
            }
        }

        private static string Argument(string[] args, string name)
        {
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
