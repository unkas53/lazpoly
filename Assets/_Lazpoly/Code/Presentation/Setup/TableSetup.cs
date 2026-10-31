using Lazpoly.Core.Rules;
using UnityEngine;

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
        public static readonly int[] StartingMoneyChoices = { 0, 1000, 2000 };

        /// <summary>Round limit choices: unlimited, a normal short game and a quick one.</summary>
        public static readonly int[] RoundLimitChoices = { 0, 25, 15 };

        public static TableRules Chosen { get; set; } = TableRules.Default;

        /// <summary>"Sınırsız", "Kısa (25 tur)"...</summary>
        public static string RoundLimitText(int limit) =>
            limit == 0 ? "Sınırsız" : limit == RoundLimitChoices[1] ? $"Kısa ({limit} tur)" : $"Çok kısa ({limit} tur)";

        public static string StartingMoneyText(int money, int boardMoney) => money == 0 ? $"{boardMoney} (normal)" : money.ToString();

        /// <summary>One line for the menu, e.g. "1500 para · açık artırma var · sınırsız".</summary>
        public static string Summary(TableRules table, int boardMoney) =>
            $"{(table.StartingMoney == 0 ? boardMoney : table.StartingMoney)} para · " +
            $"{(table.AuctionsEnabled ? "açık artırma var" : "açık artırma yok")} · {RoundLimitText(table.RoundLimit).ToLowerInvariant()}";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Chosen = TableRules.Default;
    }
}
