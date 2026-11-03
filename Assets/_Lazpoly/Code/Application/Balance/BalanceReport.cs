using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Lazpoly.App.AI;
using Lazpoly.Core.Board;
using Lazpoly.Core.Rules;

namespace Lazpoly.App.Balance
{
    /// <summary>Writes a <see cref="BalanceResult"/> as a Markdown report a person can read and compare.</summary>
    public static class BalanceReport
    {
        private static readonly CultureInfo Number = CultureInfo.InvariantCulture;

        public static string Markdown(BoardDefinition board, RulesConfig rules, BalanceSettings settings, BalanceResult result, Func<int, string> groupName = null)
        {
            if (board == null || rules == null || settings == null || result == null) throw new ArgumentNullException();
            groupName = groupName ?? (id => $"Grup {id}");
            var text = new StringBuilder();
            int finished = result.Rounds.Count;

            text.AppendLine("# Lazpoly denge raporu");
            text.AppendLine();
            text.AppendLine($"{result.Games} oyun, {settings.Players} bilgisayar oyuncu, ilk tohum {settings.FirstSeed}. " +
                            $"Başlangıç parası {rules.StartingMoney}, " +
                            $"tur sınırı {(rules.RoundLimit > 0 ? rules.RoundLimit.ToString(Number) : "yok")}.");
            text.AppendLine();

            text.AppendLine("## Oyun süresi");
            text.AppendLine();
            text.AppendLine($"- Biten oyun: {finished}, takılan: {result.Stalled}, tur sınırıyla biten: {result.EndedByRoundLimit}");
            if (finished > 0)
            {
                List<int> rounds = result.Rounds.OrderBy(r => r).ToList();
                text.AppendLine($"- Tur: ortalama {Fixed(rounds.Average())}, ortanca {Percentile(rounds, 50)}, " +
                                $"%10 {Percentile(rounds, 10)}, %90 {Percentile(rounds, 90)}, en uzun {rounds[rounds.Count - 1]}");
            }

            text.AppendLine($"- İlk {BalanceResult.EarlyRounds} turda borca düşülen oyun: {Percent(result.GamesWithEarlyDebt, result.Games)}");
            if (result.FirstBankruptcyRounds.Count > 0) text.AppendLine($"- İlk iflas: ortalama {Fixed(result.FirstBankruptcyRounds.Average())}. tur");
            text.AppendLine($"- Oyun başına: {Fixed(Per(result.CardDraws, result.Games))} kart, {Fixed(Per(result.Rides, result.Games))} otogar yolculuğu, " +
                            $"{Fixed(Per(result.PotPaidOut, result.Games))} kumbaradan ödenen para");
            text.AppendLine();

            text.AppendLine("## Kazananlar");
            text.AppendLine();
            text.AppendLine("| Sıra | Kazanma |");
            text.AppendLine("|---|---|");
            for (int seat = 0; seat < result.WinsBySeat.Length; seat++) text.AppendLine($"| {seat + 1}. oyuncu | {Percent(result.WinsBySeat[seat], finished)} |");
            text.AppendLine();
            text.AppendLine("| Zorluk | Oturduğu koltuk | Kazanma | Koltuk başına |");
            text.AppendLine("|---|---|---|---|");
            foreach (AiDifficulty difficulty in new[] { AiDifficulty.Easy, AiDifficulty.Normal, AiDifficulty.Hard })
            {
                result.SeatsByDifficulty.TryGetValue(difficulty, out int seats);
                result.WinsByDifficulty.TryGetValue(difficulty, out int wins);
                text.AppendLine($"| {difficulty} | {seats} | {wins} | {Percent(wins, seats)} |");
            }

            text.AppendLine();
            text.AppendLine("## Renk grupları");
            text.AppendLine();
            text.AppendLine("Kira geliri ve yatırımın geri dönüşü (oyun başına kira / grubun toplam fiyatı).");
            text.AppendLine();
            text.AppendLine("| Grup | Fiyat | Oyun başına kira | Geri dönüş | Tamamlanan oyun | Ortalama tamamlanma turu |");
            text.AppendLine("|---|---|---|---|---|---|");
            foreach (int group in Groups(board))
            {
                long price = 0;
                long rent = 0;
                for (int square = 0; square < board.Count; square++)
                {
                    if (board[square].Type != SquareType.Property || board[square].GroupId != group) continue;
                    price += board[square].Price;
                    rent += result.RentBySquare[square];
                }

                double perGame = Per(rent, result.Games);
                result.GroupCompletedRounds.TryGetValue(group, out List<int> completed);
                int completedGames = completed?.Count ?? 0;
                string when = completedGames > 0 ? Fixed(completed.Average()) : "-";
                text.AppendLine($"| {groupName(group)} | {price} | {Fixed(perGame)} | {Fixed(price > 0 ? perGame / price : 0)} | {Percent(completedGames, result.Games)} | {when} |");
            }

            text.AppendLine();
            text.AppendLine("## Kareler");
            text.AppendLine();
            text.AppendLine("| # | Kare | Gelinme payı | Satın alınma (oyun başına) | Oyun başına kira |");
            text.AppendLine("|---|---|---|---|---|");
            long landings = result.Landings.Sum(l => (long)l);
            for (int square = 0; square < board.Count; square++)
            {
                bool property = board[square].Type == SquareType.Property;
                text.AppendLine($"| {square} | {board[square].Name} | {Percent(result.Landings[square], landings)} | " +
                                $"{(property ? Fixed(Per(result.PurchasesBySquare[square], result.Games)) : "-")} | " +
                                $"{(property ? Fixed(Per(result.RentBySquare[square], result.Games)) : "-")} |");
            }

            return text.ToString();
        }

        private static IEnumerable<int> Groups(BoardDefinition board)
        {
            var groups = new SortedSet<int>();
            for (int square = 0; square < board.Count; square++)
            {
                if (board[square].Type == SquareType.Property) groups.Add(board[square].GroupId);
            }

            return groups;
        }

        private static double Per(long value, int count) => count > 0 ? (double)value / count : 0;

        private static string Fixed(double value) => value.ToString("0.##", Number);

        private static string Percent(long part, long whole) => whole > 0 ? ((double)part / whole).ToString("P1", Number) : "-";

        private static int Percentile(List<int> sorted, int percent) => sorted[Math.Min(sorted.Count - 1, sorted.Count * percent / 100)];
    }
}
