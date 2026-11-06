using System.Collections.Generic;
using Lazpoly.App.Session;
using Lazpoly.Core.Board;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;

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
                Section(L("Sıra"), board.HasJail
                    ? F($"Zar at, piyonun o kadar ilerler. Başlangıçtan her geçişte {rules.PassStartBonus} alırsın. Çift atarsan bir kez daha atarsın; üst üste {rules.MaxConsecutiveDoubles} çift atarsan hapse düşersin. Yapacak bir şey kalmayınca sıra kendiliğinden geçer.")
                    : F($"Zar at, piyonun o kadar ilerler. Başlangıçtan her geçişte {rules.PassStartBonus} alırsın. Çift atarsan bir kez daha atarsın; üst üste {rules.MaxConsecutiveDoubles} çift atarsan hamlen yanar. Yapacak bir şey kalmayınca sıra kendiliğinden geçer.")),
                Section(L("Mülkler"), L("Sahipsiz bir kareye gelince satın alabilirsin; almazsan bankada kalır. Başkasının karesine gelen sahibine kira öder.")),
                Section(L("Şehirler ve evler"), F($"Satın aldığın şehre yeniden gelince ev kurabilirsin: başlangıçtan ilk geçişine kadar bir ev, sonra dörde kadar. Dört evli şehre bir daha gelince otel kurulur. Bir rengin bütün kareleri sendeyse boş kirası {rules.FullGroupRentMultiplier} katına çıkar.")),
                Section(L("Para sıkıntısı"), F($"Mülk yalnız bir ödemeye (çoğunlukla kiraya) para yetmediğinde satılır; o an şehirlerin haritada halkalanır ve tıkladığın şehir bankaya satılır. Banka fiyatın yarısını öder, şehir yeniden sahipsiz kalır. Binalar maliyetinin %{rules.BuildingSellbackPercent} değerine geri satılır. Otomatik Sat düğmesi borcu kapatacak kadar şehri, binasız ve ucuz olanlardan başlayarak satar. Her şeyini satsa da borcunu ödeyemeyen iflas eder: bütün şehirleri ve binaları bankaya satılır, eline geçen para ne kadarsa alacaklıya o kadar ödenir.")),
                Section(L("Takas"), L("Kendi sıranda başka bir oyuncuya para ve mülk karşılığında takas teklif edebilirsin. Binalı şehirler takas edilemez.")),
            };

            if (rules.TurnSeconds > 0)
            {
                int decision = (int)TurnClock.LimitFor(GamePhase.WaitingForRoll, rules.TurnSeconds);
                int sale = (int)TurnClock.LimitFor(GamePhase.AwaitingDebtPayment, rules.TurnSeconds);
                sections.Insert(2, Section(L("Süre"), F($"Zar atmak, satın almak ve bina kurmak için {decision} sn, borç için satışa ve takas cevabına {sale} sn var. Süre dolunca oyun senin yerine karar verir: zarı atar, şehri almaz, sırayı bitirir, borcunu otomatik satışla öder.")));
            }

            if (HasType(board, SquareType.Card)) sections.Add(Section(CardSquaresTitle(board), L("Bu kareye gelen desteden rastgele bir kart çeker: para kazanabilir, ceza ödeyebilir, başka bir kareye gidebilir ya da dürbün bulabilir.")));
            if (board.HasPot) sections.Add(Section(L("Çay Molası"), L("Vergiler ve kart cezaları çay kumbarasında birikir. Çay Molası'na gelen kumbaradaki her şeyi alır.")));
            if (board.HasJail)
            {
                sections.Add(Section(L(board[board.JailIndex].Name), F($"Hapse gelen, üst üste {rules.MaxConsecutiveDoubles} çift atan ya da hapse gönderen kartı çeken, {rules.JailTurns} kez sırası gelene kadar orada bekler. Sırası gelince isterse {board[board.JailIndex].Fee} ödeyip hemen çıkar ve zar atar; ya da bedava zar atar: çift gelirse çıkıp o zarla ilerler (bir daha atmaz), gelmezse sıra geçer. Hapisteyken kira almak sürer.")));
            }

            if (HasType(board, SquareType.Travel)) sections.Add(Section(L("Otogar"), F($"Otogara gelen isterse {fare} bilet parası ödeyip bir kareye zar atmış gibi yürüyerek gider; Başlangıç'tan geçerse parasını alır. Başka bir oyuncunun şehrine gidilemez.")));
            if (HasType(board, SquareType.Lookout)) sections.Add(Section(L("Seyir Terası"), L("Buraya gelen bir sonraki sırasında iki kez zar atar ve hangisiyle gideceğini seçer.")));
            sections.Add(Section(L("İpucu"), L("Bir kareye fareyle gel ya da tıkla: fiyatı, kira tablosu ve sahibi kartında görünür. Fare tekeriyle yakınlaş, Home ile başa dön.")));
            return sections;
        }

        private static KeyValuePair<string, string> Section(string title, string body) => new KeyValuePair<string, string>(title, body);

        /// <summary>The card squares' names, once each: "Sokak Haberi", or "Sokak Haberi ve Şans" when the board has both (Faz 24).</summary>
        private static string CardSquaresTitle(BoardDefinition board)
        {
            var names = new List<string>();
            for (int i = 0; i < board.Count; i++)
            {
                if (board[i].Type == SquareType.Card && !names.Contains(board[i].Name)) names.Add(board[i].Name);
            }

            return names.Count == 2 ? F($"{L(names[0])} ve {L(names[1])}") : string.Join(", ", names.ConvertAll(name => L(name)));
        }

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
