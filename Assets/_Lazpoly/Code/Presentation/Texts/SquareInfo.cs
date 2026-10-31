using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Texts
{
    /// <summary>One line of a square's card: a label, its value and whether it is what applies right now.</summary>
    public readonly struct SquareInfoRow
    {
        public SquareInfoRow(string label, string value, bool current = false)
        {
            Label = label;
            Value = value;
            Current = current;
        }

        public string Label { get; }
        public string Value { get; }
        public bool Current { get; }
    }

    /// <summary>
    /// What the square card shows: price, the whole rent table with the rent that applies now marked,
    /// building cost, mortgage value, owner and how much of the group each player holds.
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
                    info.Rows.Add(new SquareInfoRow(L("Ödenecek"), square.TaxAmount.ToString()));
                    info.Notes.Add(board.HasPot ? L("Buraya gelen öder; para çay kumbarasına girer.") : L("Buraya gelen bankaya öder."));
                    return info;
                }

                case SquareType.Card:
                {
                    var info = new SquareInfo(L(square.Name), L("Sokak Haberi"));
                    info.Rows.Add(new SquareInfoRow(L("Destedeki kart"), board.Cards.Count.ToString()));
                    info.Notes.Add(L("Buraya gelen bir kart çeker: para kazanabilir, ödeyebilir ya da başka kareye gidebilir."));
                    return info;
                }

                case SquareType.Pot:
                {
                    var info = new SquareInfo(L(square.Name), L("Çay kumbarası"));
                    info.Rows.Add(new SquareInfoRow(L("Kumbarada"), state.Pot.ToString(), state.Pot > 0));
                    info.Notes.Add(L("Vergiler ve kart cezaları kumbarada birikir; buraya gelen hepsini alır."));
                    return info;
                }

                case SquareType.Travel:
                {
                    var info = new SquareInfo(L(square.Name), L("Yolculuk"));
                    info.Rows.Add(new SquareInfoRow(L("Bilet"), square.Fee.ToString()));
                    info.Notes.Add(L("Buraya gelen isterse bileti ödeyip tahtadaki herhangi bir kareye gider."));
                    info.Notes.Add(L("Başlangıçtan geçmiş sayılmaz."));
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
            var info = new SquareInfo(L(square.Name), string.IsNullOrEmpty(groupName) ? L("Mülk") : F($"Mülk · {L(groupName)}"));

            int owner = state.GetOwner(squareIndex);
            int level = state.GetBuildingLevel(squareIndex);
            bool mortgaged = state.IsMortgaged(squareIndex);
            bool wholeGroup = owner != GameState.NoOwner && GameRules.OwnsWholeGroup(board, state, owner, square.GroupId);
            bool live = owner != GameState.NoOwner && !mortgaged;

            info.Rows.Add(new SquareInfoRow(L("Fiyat"), square.Price.ToString()));
            info.Rows.Add(new SquareInfoRow(L("Kira"), square.BaseRent.ToString(), live && level == 0 && !wholeGroup));
            info.Rows.Add(new SquareInfoRow(L("Kira, rengin tamamı"), (square.BaseRent * rules.FullGroupRentMultiplier).ToString(), live && level == 0 && wholeGroup));

            if (square.IsDevelopable)
            {
                for (int i = 0; i < square.DevelopedRents.Count; i++)
                {
                    string label = i + 1 == SquareDefinition.MaxBuildingLevel ? L("Kira, otel") : F($"Kira, {i + 1} ev");
                    info.Rows.Add(new SquareInfoRow(label, square.DevelopedRents[i].ToString(), live && level == i + 1));
                }

                info.Rows.Add(new SquareInfoRow(L("Ev maliyeti"), square.BuildingCost.ToString()));
            }

            info.Rows.Add(new SquareInfoRow(L("İpotek değeri"), square.MortgageValue.ToString()));

            string ownerName = owner == GameState.NoOwner ? null : state.FindPlayer(owner)?.Name;
            info.Notes.Add(ownerName == null ? L("Sahibi yok: buraya gelen satın alabilir.") : mortgaged ? F($"Sahibi {ownerName} (ipotekli, kira alınmaz).") : F($"Sahibi {ownerName}."));
            info.Notes.Add(GroupNote(board, state, square.GroupId));
            if (square.IsDevelopable) info.Notes.Add(L("Ev kurmak için rengin bütün kareleri aynı oyuncuda olmalı."));
            return info;
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
