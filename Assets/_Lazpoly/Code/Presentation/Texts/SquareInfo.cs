using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Texts
{
    /// <summary>One line of a square's card: a label, its value, whether it is what applies right now, and whether it belongs to the rent table.</summary>
    public readonly struct SquareInfoRow
    {
        public SquareInfoRow(string label, string value, bool current = false, bool rent = false)
        {
            Label = label;
            Value = value;
            Current = current;
            Rent = rent;
        }

        public string Label { get; }
        public string Value { get; }
        public bool Current { get; }

        /// <summary>True for a line of the rent table (land, whole colour, houses, hotel); false for a fact such as the price.</summary>
        public bool Rent { get; }
    }

    /// <summary>
    /// What the square card shows: price, building cost and sale value as facts, the whole rent table
    /// (<see cref="SquareInfoRow.Rent"/>) with the rent that applies now marked, and notes on the owner
    /// and how much of the group each player holds.
    /// </summary>
    public sealed class SquareInfo
    {
        private SquareInfo(string title, string kind)
        {
            Title = title;
            Kind = kind;
        }

        public string Title { get; }
        public string Kind { get; }
        public List<SquareInfoRow> Rows { get; } = new List<SquareInfoRow>();
        public List<string> Notes { get; } = new List<string>();

        public static SquareInfo Describe(BoardDefinition board, RulesConfig rules, GameState state, int squareIndex, string groupName)
        {
            SquareDefinition square = board[squareIndex];
            switch (square.Type)
            {
                case SquareType.Start:
                {
                    var info = new SquareInfo(L(square.Name), L("Başlangıç"));
                    info.Notes.Add(F($"Buradan her geçişte {rules.PassStartBonus} alırsın."));
                    return info;
                }

                case SquareType.Tax:
                {
                    var info = new SquareInfo(L(square.Name), L("Vergi"));
                    if (square.TaxPercent > 0)
                    {
                        info.Rows.Add(new SquareInfoRow(L("Oran"), F($"%{square.TaxPercent}")));
                        info.Notes.Add(L("Mülk değerinden alınır: şehirlerin ve tatil yerlerinin fiyatı ile binaların maliyeti. Nakit sayılmaz; mülkü olmayan ödemez."));
                    }
                    else
                    {
                        info.Rows.Add(new SquareInfoRow(L("Ödenecek"), M(square.TaxAmount)));
                    }

                    info.Notes.Add(board.HasPot ? L("Buraya gelen öder; para çay kumbarasına girer.") : L("Buraya gelen bankaya öder."));
                    return info;
                }

                case SquareType.Card:
                {
                    var info = new SquareInfo(L(square.Name), L(square.Name));
                    info.Rows.Add(new SquareInfoRow(L("Destedeki kart"), board.Cards.Count.ToString()));
                    info.Notes.Add(L("Buraya gelen bir kart çeker: para kazanabilir, ödeyebilir ya da başka kareye gidebilir."));
                    return info;
                }

                case SquareType.Pot:
                {
                    var info = new SquareInfo(L(square.Name), L("Çay kumbarası"));
                    info.Rows.Add(new SquareInfoRow(L("Kumbarada"), M(state.Pot), state.Pot > 0));
                    info.Notes.Add(L("Vergiler ve kart cezaları kumbarada birikir; buraya gelen hepsini alır."));
                    return info;
                }

                case SquareType.Jail:
                {
                    var info = new SquareInfo(L(square.Name), L("Hapis"));
                    info.Rows.Add(new SquareInfoRow(L("Bekleme"), F($"{rules.JailTurns} tur")));
                    info.Rows.Add(new SquareInfoRow(L("Erken çıkış"), M(square.Fee)));
                    info.Notes.Add(F($"Buraya gelen {rules.JailTurns} kez sırası gelene kadar burada bekler. Sırası gelince isterse {square.Fee} ödeyip hemen çıkar ve zar atar ya da bedava zar atar: çift gelirse çıkıp o zarla ilerler."));
                    info.Notes.Add(F($"Üst üste {rules.MaxConsecutiveDoubles} çift atan ve hapse gönderen kartı çeken de buraya düşer; Başlangıç'tan geçmiş sayılmaz."));
                    return info;
                }

                case SquareType.Travel:
                {
                    var info = new SquareInfo(L(square.Name), L("Yolculuk"));
                    info.Rows.Add(new SquareInfoRow(L("Bilet"), M(square.Fee)));
                    info.Notes.Add(L("Buraya gelen isterse bileti ödeyip başka oyuncuların şehirleri dışındaki bir kareye yürüyerek gider."));
                    info.Notes.Add(L("Başlangıçtan geçmiş sayılmaz."));
                    return info;
                }

                case SquareType.Cup:
                {
                    var info = new SquareInfo(L(square.Name), L("Kupa"));
                    info.Rows.Add(new SquareInfoRow(L("Ücret"), M(square.Fee)));
                    info.Rows.Add(new SquareInfoRow(L("Sıradaki artış"), F($"%{GameRules.NextCupPercent(board, state, squareIndex)}")));
                    info.Rows.Add(new SquareInfoRow(L("Kupanın şehri"),
                        state.CupSquare == GameState.NoCup ? L("henüz yok") : F($"{L(board[state.CupSquare].Name)} (%{state.CupPercent})"), state.CupSquare != GameState.NoCup));
                    info.Notes.Add(F($"Buraya gelen isterse {square.Fee} ödeyip kupayı kendi şehirlerinden birine getirir; o şehrin kirası artar. Kupa tek bir şehirde durur: yeni bir şehir seçilince eskisinden kalkar."));
                    info.Notes.Add(F($"Artış ilk seçimde %{square.BoostPercent}; oyun boyunca kim seçerse seçsin her seçimde %{square.BoostStep} büyür. Tatil yerleri seçilemez."));
                    return info;
                }

                case SquareType.Lookout:
                {
                    var info = new SquareInfo(L(square.Name), L("Seyir"));
                    info.Notes.Add(L("Buraya gelen bir sonraki zarında iki kez atar ve hangisiyle gideceğini seçer."));
                    return info;
                }

                case SquareType.Property:
                    return DescribeProperty(board, rules, state, squareIndex, groupName);

                default:
                {
                    var info = new SquareInfo(L(square.Name), L("Dinlenme"));
                    info.Notes.Add(L("Burada bir şey olmaz; soluklan."));
                    return info;
                }
            }
        }

        private static SquareInfo DescribeProperty(BoardDefinition board, RulesConfig rules, GameState state, int squareIndex, string groupName)
        {
            SquareDefinition square = board[squareIndex];
            if (square.IsResort) return DescribeResort(board, state, squareIndex);
            var info = new SquareInfo(L(square.Name), string.IsNullOrEmpty(groupName) ? L("Mülk") : F($"Mülk · {L(groupName)}"));

            int owner = state.GetOwner(squareIndex);
            int level = state.GetBuildingLevel(squareIndex);
            bool wholeGroup = owner != GameState.NoOwner && GameRules.OwnsWholeGroup(board, state, owner, square.GroupId);
            bool live = owner != GameState.NoOwner;

            info.Rows.Add(new SquareInfoRow(L("Fiyat"), M(square.Price)));
            info.Rows.Add(new SquareInfoRow(L("Arsa"), M(square.BaseRent), live && level == 0 && !wholeGroup, rent: true));
            info.Rows.Add(new SquareInfoRow(L("Rengin tamamı"), M(square.BaseRent * rules.FullGroupRentMultiplier), live && level == 0 && wholeGroup, rent: true));

            if (square.IsDevelopable)
            {
                for (int i = 0; i < square.DevelopedRents.Count; i++)
                {
                    string label = i + 1 == SquareDefinition.MaxBuildingLevel ? L("Otel") : F($"{i + 1} ev");
                    // With the whole colour in one hand every level's rent is multiplied too (Faz 26).
                    int rent = square.DevelopedRents[i] * (wholeGroup ? rules.FullGroupRentMultiplier : 1);
                    info.Rows.Add(new SquareInfoRow(label, M(rent), live && level == i + 1, rent: true));
                }

                info.Rows.Add(new SquareInfoRow(L("Ev maliyeti"), M(square.BuildingCost)));
            }

            info.Rows.Add(new SquareInfoRow(L("Satış değeri"), M(square.SaleValue)));
            bool cup = state.CupSquare == squareIndex;
            if (cup) info.Rows.Add(new SquareInfoRow(L("Kupa"), F($"kira +%{state.CupPercent}"), live, rent: true));

            string ownerName = owner == GameState.NoOwner ? null : state.FindPlayer(owner)?.Name;
            info.Notes.Add(ownerName == null ? L("Sahibi yok: buraya gelen satın alabilir.") : F($"Sahibi {ownerName}."));
            if (cup) info.Notes.Add(F($"Şampiyonlar Kupası bu şehirde: kirası %{state.CupPercent} fazla."));
            info.Notes.Add(GroupNote(board, state, square.GroupId));
            if (square.IsDevelopable) info.Notes.Add(L("Sahibi şehre yeniden gelince ev kurar: başlangıçtan geçene kadar bir, sonra dörde kadar; dört evden sonra otel."));
            return info;
        }

        /// <summary>A holiday resort: its price, the rent for one to all four held by the same owner, and who holds them.</summary>
        private static SquareInfo DescribeResort(BoardDefinition board, GameState state, int squareIndex)
        {
            SquareDefinition square = board[squareIndex];
            var info = new SquareInfo(L(square.Name), L("Tatil yeri"));
            int owner = state.GetOwner(squareIndex);
            int held = owner == GameState.NoOwner ? 0 : GameRules.ResortsOwned(board, state, owner);
            int resorts = ResortCount(board);

            info.Rows.Add(new SquareInfoRow(L("Fiyat"), M(square.Price)));
            for (int count = 1; count <= resorts; count++)
            {
                info.Rows.Add(new SquareInfoRow(F($"{count} tatil yeri"), M(GameRules.ResortRent(square.BaseRent, count)), held == count, rent: true));
            }

            info.Rows.Add(new SquareInfoRow(L("Satış değeri"), M(square.SaleValue)));
            string ownerName = owner == GameState.NoOwner ? null : state.FindPlayer(owner)?.Name;
            info.Notes.Add(ownerName == null ? L("Sahibi yok: buraya gelen satın alabilir.") : F($"Sahibi {ownerName}."));
            info.Notes.Add(L("Tatil yerine bina kurulmaz. Sahibinin elindeki her yeni tatil yeriyle kirası iki katına çıkar: bir, iki, dört ya da sekiz kat."));
            return info;
        }

        /// <summary>How many holiday resorts the board has.</summary>
        public static int ResortCount(BoardDefinition board)
        {
            int count = 0;
            for (int i = 0; i < board.Count; i++)
            {
                if (board[i].IsResort) count++;
            }

            return count;
        }

        /// <summary>Who holds how much of the group, e.g. "Bu renk: 3 kare; Ayşe 2, sahipsiz 1."</summary>
        private static string GroupNote(BoardDefinition board, GameState state, int groupId)
        {
            var counts = new List<KeyValuePair<int, int>>();
            int total = 0;
            foreach (int square in GameRules.GroupSquares(board, groupId))
            {
                total++;
                int owner = state.GetOwner(square);
                int index = counts.FindIndex(pair => pair.Key == owner);
                if (index < 0) counts.Add(new KeyValuePair<int, int>(owner, 1));
                else counts[index] = new KeyValuePair<int, int>(owner, counts[index].Value + 1);
            }

            var parts = new List<string>();
            foreach (KeyValuePair<int, int> pair in counts)
            {
                string name = pair.Key == GameState.NoOwner ? L("sahipsiz") : state.FindPlayer(pair.Key)?.Name;
                parts.Add($"{name} {pair.Value}");
            }

            return F($"Bu renk: {total} kare; {string.Join(", ", parts)}.");
        }
    }
}
