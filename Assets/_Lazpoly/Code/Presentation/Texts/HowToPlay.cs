using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Rules;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Texts
{
    /// <summary>The "Nasıl Oynanır" page: short sections whose numbers come from the board and rules in use.</summary>
    public static class HowToPlay
    {
        public static IReadOnlyList<KeyValuePair<string, string>> Sections(BoardDefinition board, RulesConfig rules)
        {
            int fare = 0;
            for (int i = 0; i < board.Count; i++)
            {
                if (board[i].Type == SquareType.Travel) fare = board[i].Fee;
            }

            var sections = new List<KeyValuePair<string, string>>
            {
                Section(L("Amaç"), rules.RoundLimit > 0
                    ? F($"Mülk topla, kira al. {rules.RoundLimit} tur sonunda parası, mülkleri ve binaları en değerli olan kazanır; ondan önce herkes iflas ederse son kalan kazanır.")
                    : L("Mülk topla, kira al ve diğerlerini iflas ettir. Son kalan oyuncu kazanır.")),
                Section(L("Sıra"), F($"Zar at, piyonun o kadar ilerler. Başlangıçtan her geçişte {rules.PassStartBonus} alırsın. Çift atarsan bir kez daha atarsın; üst üste {rules.MaxConsecutiveDoubles} çift atarsan hamlen yanar.")),
                Section(L("Mülkler"), rules.AuctionsEnabled
                    ? L("Sahipsiz bir kareye gelince satın alabilirsin. Almazsan açık artırmaya çıkar ve en çok veren alır. Başkasının karesine gelen sahibine kira öder.")
                    : L("Sahipsiz bir kareye gelince satın alabilirsin; almazsan bankada kalır. Başkasının karesine gelen sahibine kira öder.")),
                Section(L("Şehirler ve evler"), F($"Satın aldığın şehre yeniden gelince ev kurabilirsin: başlangıçtan ilk geçişine kadar bir ev, sonra dörde kadar. Dört evli şehre bir daha gelince otel kurulur. Bir rengin bütün kareleri sendeyse boş kirası {rules.FullGroupRentMultiplier} katına çıkar.")),
                Section(L("Para sıkıntısı"), F($"Parası yetmeyen mülkünü bankaya satar: banka fiyatın yarısını öder, şehir yeniden sahipsiz kalır. Binalar maliyetinin %{rules.BuildingSellbackPercent} değerine geri satılır. Borcunu ödeyemeyen iflas eder.")),
                Section(L("Takas"), L("Kendi sıranda başka bir oyuncuya para ve mülk karşılığında takas teklif edebilirsin. Binalı şehirler takas edilemez.")),
            };

            if (HasType(board, SquareType.Card)) sections.Add(Section(L("Sokak Haberi"), L("Bu kareye gelen bir kart çeker: para kazanabilir, ceza ödeyebilir, başka bir kareye gidebilir ya da dürbün bulabilir.")));
            if (board.HasPot) sections.Add(Section(L("Çay Molası"), L("Vergiler ve kart cezaları çay kumbarasında birikir. Çay Molası'na gelen kumbaradaki her şeyi alır.")));
            if (HasType(board, SquareType.Travel)) sections.Add(Section(L("Otogar"), F($"Otogara gelen isterse {fare} bilet parası ödeyip tahtadaki herhangi bir kareye gider. Bu yolculuk başlangıçtan geçmiş sayılmaz.")));
            if (HasType(board, SquareType.Lookout)) sections.Add(Section(L("Seyir Terası"), L("Buraya gelen bir sonraki sırasında iki kez zar atar ve hangisiyle gideceğini seçer.")));
            sections.Add(Section(L("İpucu"), L("Bir kareye fareyle gel ya da tıkla: fiyatı, kira tablosu ve sahibi kartında görünür. Fare tekeriyle yakınlaş, sağ tuşla tahtayı döndür, Home ile başa dön.")));
            return sections;
        }

        private static KeyValuePair<string, string> Section(string title, string body) => new KeyValuePair<string, string>(title, body);

        private static bool HasType(BoardDefinition board, SquareType type)
        {
            for (int i = 0; i < board.Count; i++)
            {
                if (board[i].Type == type) return true;
            }

            return false;
        }
    }
}
