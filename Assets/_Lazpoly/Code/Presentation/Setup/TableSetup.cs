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
        public static readonly int[] StartingMoneyChoices = { 0, 1000, 2000 };

        /// <summary>Round limit choices: unlimited, a normal short game and a quick one.</summary>
        public static readonly int[] RoundLimitChoices = { 0, 25, 15 };

        /// <summary>Online move time choices in seconds; 0 runs no clock.</summary>
        public static readonly int[] TurnSecondsChoices = { 60, 90, 120, 0 };

        public static TableRules Chosen { get; set; } = TableRules.Default;

        /// <summary>"Sınırsız", "Kısa (25 tur)"...</summary>
        public static string RoundLimitText(int limit) =>
            limit == 0 ? L("Sınırsız") : limit == RoundLimitChoices[1] ? F($"Kısa ({limit} tur)") : F($"Çok kısa ({limit} tur)");

        public static string StartingMoneyText(int money, int boardMoney) => money == 0 ? F($"{boardMoney} (normal)") : money.ToString();

        /// <summary>"60 sn", "90 sn"... or "Süresiz".</summary>
        public static string TurnSecondsText(int seconds) => seconds == 0 ? L("Süresiz") : F($"{seconds} sn");

        /// <summary>What becomes of a bankrupt player's squares, as the rules window names the choice.</summary>
        public static string BankReturnsText(BankReturns rule) =>
            rule == BankReturns.Auction ? L("Hemen açık artırmaya çıkar") : L("Gelen alır");

        /// <summary>One line for the menu, e.g. "1500 para · açık artırma var · sınırsız · 90 sn hamle".</summary>
        public static string Summary(TableRules table, int boardMoney)
        {
            string line = F($"{(table.StartingMoney == 0 ? boardMoney : table.StartingMoney)} para · {(table.AuctionsEnabled ? L("açık artırma var") : L("açık artırma yok"))} · {RoundLimitText(table.RoundLimit).ToLowerInvariant()}");
            line += " · " + (table.TurnSeconds == 0 ? L("süresiz hamle") : F($"{table.TurnSeconds} sn hamle"));
            if (table.AuctionsBankReturns) line += " · " + L("iflas mülkü açık artırmada");
            return line;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Chosen = TableRules.Default;
    }
}
