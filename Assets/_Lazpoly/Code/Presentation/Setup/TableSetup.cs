using Lazpoly.App.Session;
using Lazpoly.Core.Rules;
using UnityEngine;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Setup
{
    /// <summary>
    /// The house rules picked in the main menu ("Masa Kuralları"). New local games and games this
    /// machine hosts use them; a saved game keeps the rules it was started with, and online
    /// clients get the host's rules with the game.
    /// </summary>
    public static class TableSetup
    {
        /// <summary>Starting money choices; 0 means the board's own amount.</summary>
        public static readonly int[] StartingMoneyChoices = { 0, 1000000, 3000000 };

        /// <summary>Round limit choices: unlimited, a normal short game and a quick one.</summary>
        public static readonly int[] RoundLimitChoices = { 0, 25, 15 };

        /// <summary>Decision time choices in seconds (selling and trade answers get twice as long); 0 runs no clock.</summary>
        public static readonly int[] TurnSecondsChoices = { 10, 15, 25, 0 };

        /// <summary>Game time choices in minutes (the owner's rule, Faz 28): a standard table plays 30.</summary>
        public static readonly int[] TimeLimitChoices = { 15, 30, 60 };

        public static TableRules Chosen { get; set; } = TableRules.Default;

        /// <summary>"Sınırsız", "Kısa (25 tur)"...</summary>
        public static string RoundLimitText(int limit) =>
            limit == 0 ? L("Sınırsız") : limit == RoundLimitChoices[1] ? F($"Kısa ({limit} tur)") : F($"Çok kısa ({limit} tur)");

        public static string StartingMoneyText(int money, int boardMoney) => money == 0 ? F($"{boardMoney} (normal)") : M(money);

        /// <summary>"10 sn", "15 sn"... or "Süresiz".</summary>
        public static string TurnSecondsText(int seconds) => seconds == 0 ? L("Süresiz") : F($"{seconds} sn");

        /// <summary>"15 dk", "30 dk", "60 dk"; "Süresiz" for a table without a game clock.</summary>
        public static string TimeLimitText(int minutes) => minutes == 0 ? L("Süresiz") : F($"{minutes} dk");

        /// <summary>One line for the menu, e.g. "2M para · 30 dk · sınırsız · 15 sn karar".</summary>
        public static string Summary(TableRules table, int boardMoney)
        {
            string line = F($"{(table.StartingMoney == 0 ? boardMoney : table.StartingMoney)} para · {TimeLimitText(table.TimeLimitMinutes).ToLowerInvariant()} · {RoundLimitText(table.RoundLimit).ToLowerInvariant()}");
            line += " · " + (table.TurnSeconds == 0 ? L("süresiz") : F($"{System.Math.Min(table.TurnSeconds, TurnClock.MaxDecisionSeconds)} sn karar"));
            return line;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Chosen = TableRules.Default;
    }
}
