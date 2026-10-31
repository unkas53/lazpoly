# Lazpoly

Steam için 3D, 2-8 oyunculu çevrim içi masa oyunu. Mimari ve yol haritası:
https://claude.ai/artifact/3hqmWm5rQuN5tmmuMwSXNn

## Durum: Faz 0 (proje iskeleti) tamamlandı

Doğrulama (2026-10-31, Unity 6000.6.7f1, Windows): derleme hatasız, 14/14 EditMode testi geçti,
Windows derlemesinde Steam bağlandı (`[Steam] Initialized as ... (App ID 480)`).

| Yol | İçerik |
| --- | --- |
| `Assets/_Lazpoly/Code/Core` | `Lazpoly.Core`: saf C#, Unity referansı yok. Şimdilik tohumlu RNG (`Pcg32Random`). |
| `Assets/_Lazpoly/Code/Application` | `Lazpoly.App`: boş, Faz 2'de dolacak. |
| `Assets/_Lazpoly/Code/Networking` | `Lazpoly.Net`: boş, Faz 3'te dolacak. |
| `Assets/_Lazpoly/Code/Infrastructure` | `Lazpoly.Infra`: servis kaydı ve yaşam döngüsü. |
| `Assets/_Lazpoly/Code/Platform` | `Lazpoly.Platform`: Steamworks.NET'e dokunan tek modül (`SteamService`). |
| `Assets/_Lazpoly/Code/Boot` | `Lazpoly.Boot`: açılış; servisleri ilk sahneden önce kurar. |
| `Assets/_Lazpoly/Code/Presentation` | `Lazpoly.Presentation`: boş, Faz 2'de dolacak. |
| `Assets/_Lazpoly/Code/Editor` | `Lazpoly > Create Scenes and Build Settings` menüsü. |
| `Assets/_Lazpoly/Scenes` | `MainMenu`, `Lobby`, `Game` (Build Settings'te) ve `Sandbox_Board` (yalnızca geliştirme). |
| `Assets/_Lazpoly/Tests/EditMode` | Core ve Infra birim testleri. |
| `steam_appid.txt` | Geliştirme için Spacewar (480). |

Bağımlılık yönü: Presentation → App → Core. Core hiçbir modülü bilmez. Boot en üstteki
bileşim köküdür (composition root) ve Infra ile Platform'u birbirine bağlar.

Mimari belgesinden bir sapma: ayrı bir `Boot` sahnesi yok. Açılış
`[RuntimeInitializeOnLoadMethod]` ile çalışır, böylece hangi sahnede Play'e basılırsa
basılsın servisler aynı şekilde kurulur.

Paketler bilerek az tutuldu: Steamworks.NET (`2025.164.1`), URP, Test Framework, uGUI ve IDE paketleri.

## Kurulum

1. Unity Hub'a **Unity 6000.6.7f1** kur.
2. Depoyu klonla ve Unity Hub'da **Add > Add project from disk** ile klasörü aç.
   Steamworks.NET bir git paketi olduğu için bilgisayarda Git kurulu olmalı.
3. Steam açık ve giriş yapılmış olsun.

## Testler ve kontrol

- **Window > General > Test Runner > EditMode > Run All**: tüm testler yeşil olmalı.
- Komut satırından:
  `Unity.exe -batchmode -projectPath <klasör> -runTests -testPlatform EditMode -testResults results.xml -logFile editor.log`
- Steam: bir Windows derlemesi al, `steam_appid.txt` dosyasını exe'nin yanına koy, çalıştır;
  `Player.log` içinde `[Steam] Initialized as` satırı görünmeli. Editörde Play'e basınca da Console'da aynı satır çıkar.

## Faz 2: yerel prototip

- **Oynamak:** `MainMenu` sahnesini aç, Play'e bas, 2-8 oyuncu seç, "Oyunu Başlat". `Game` sahnesi doğrudan
  açılırsa iki varsayılan oyuncuyla başlar.
- **Tahta ve ekonomi verisi:** `Assets/_Lazpoly/Data/Resources/Lazpoly/board.json`. Kare isimleri, fiyatlar,
  kiralar, vergiler, gruplar ve başlangıç parası buradan okunur; kod değiştirmeden düzenlenebilir.
  Şu anki değerler taslaktır.
- **Sahne kurulumu:** `Lazpoly > Add Scene Controllers` menüsü MainMenu ve Game sahnelerine kök bileşenleri ekler.
- **Geçici arayüz:** Menü ve oyun arayüzü IMGUI ile çizilen prototiptir (`PrototypeHud`, `MainMenuController`);
  Faz 6'da asıl arayüzle değiştirilecek. Tahta ve piyonlar da Blender modelleri gelene kadar basit şekillerdir.

## Faz 3: çevrim içi (Steam)

- **Nasıl çalışır:** Oyunu kuran oyuncunun makinesi ev sahibidir (host) ve kuralları yalnızca o çalıştırır.
  Diğer oyuncular hamle isteği gönderir; host doğrular, kabul ettiği hamlenin olaylarını sıra numarası ve
  durum özetiyle (hash) herkese yollar. Bir istemci olay kaçırırsa ya da özeti tutmazsa hosttan tam durumu
  yeniden ister. Kopan oyuncu odaya yeniden girince kaldığı yerden devam eder.
- **Oynamak:** Ana menüde "Oda Kur", sonra çevrim içi arkadaş listesinden "Davet". Arkadaş, Lazpoly açıkken
  Steam sohbetindeki daveti kabul eder ya da kendi menüsündeki "Odası açık arkadaşlar" listesinden "Katıl" der.
  Steam arayüzü (overlay) oyunda çalışıyorsa "Steam ile Davet Et" düğmesi de görünür. Ev sahibi
  "Çevrim İçi Oyunu Başlat" deyince herkes oyuna geçer.
- **Gerçek test için** iki ayrı Steam hesabı gerekir (iki bilgisayar ya da bir bilgisayar + bir sanal makine).
  Geliştirme App ID'si 480 olduğu için Steam arayüzünde oyun "Spacewar" görünür.
- **Steam'siz test:** `NetworkSessionTests` (1 host + 7 istemci, hile denemesi, kayıp mesaj, kopma, bozuk veri)
  ve `OnlineGameSceneTests` (Game sahnesi host ve istemci olarak) sanal bir ağ üzerinde çalışır.
- **Bilinen sınır:** Bağlantısı kopan oyuncunun sırası gelirse oyun o oyuncu dönene kadar bekler.
  Yerine yapay zekânın oynaması Faz 5'te gelecek.

## Faz 4: gelişmiş ekonomi

- **Satın alma ve açık artırma:** Sahipsiz mülke gelen oyuncu ya satın alır ya da "Alma, açık artırmaya çıkar" der; o zaman mülk
  hemen açık artırmaya çıkar. Teklifi en yüksek olan dışındaki herkes teklif verebilir ya da çekilebilir.
  Bir kişi kalınca o kazanır ve teklifini öder; kimse teklif vermezse mülk bankada kalır.
- **Bina:** Bir grubun tamamına sahip olan oyuncu, sırası geldiğinde (zar atmadan önce ya da tur sonunda)
  ev kurabilir: en fazla 4 ev, 5. seviye oteldir. Binalar grupta eşit kurulur ve eşit satılır (seviye farkı en
  çok 1). Bina satışı kuruluş fiyatının yarısını geri verir. Kira seviyeye göre `rents` listesinden okunur.
- **İpotek:** Binası olmayan grubun mülkü ipotek edilebilir; oyuncu fiyatın yarısını alır, ipotekli mülkten
  kira alınmaz ve o grupta bina kurulamaz. Kurtarmak ipotek değeri + %10 faizdir.
- **Takas:** Sırası gelen oyuncu bir başkasına para ve mülk karışımı teklif eder; karşı taraf kabul ya da
  reddeder, teklif eden geri çekebilir. Binası olan gruptan mülk takas edilemez (önce binalar satılır).
  İpotekli mülk ipoteğiyle birlikte el değiştirir.
- **Borç ve iflas:** Ödeyemeyen oyuncunun oyunu durur ve önünde "Borcu Öde" ile "İflas Et" çıkar. Bu sırada
  bina satıp mülk ipotek ederek para bulabilir. İflasta binalar bankaya satılır, tüm para alacaklıya geçer;
  mülkler alacaklı oyuncuya ipotekleriyle gider, alacaklı banka ise ipotekleri silinip bankaya döner.
- **Veri:** `board.json` içinde `mortgageInterestPercent`, `buildingSellbackPercent` ve her mülk için
  `buildingCost` ile 5 elemanlı `rents` (1-4 ev, otel). Şu anki değerler taslaktır (bina fiyatı grup grup
  50-200, kiralar taban kiranın 5/15/40/55/70 katı).
- **Arayüz (geçici):** Sol paneldeki mülk listesinde yalnızca o an kurala uyan düğmeler (Ev +, Ev Sat,
  İpotek, Kurtar) görünür; açık artırma ve takas için ayrı paneller açılır. Kurallar `GameRules` içinde tek
  yerdedir; host doğrulamak, arayüz düğmeleri göstermek için aynı fonksiyonu kullanır.
- **Testler:** `EconomyTests` her kuralı tek tek, `GameSimulationTests` 200 rastgele oyunda tutarlılığı,
  `NetworkSessionTests` istemciler arası takas ve açık artırmayı, `MessageCodecTests` yeni mesajları dener.
- **Bilinen sınırlar:** Açık artırmada süre sınırı yok; takas teklifine cevap vermeyen ya da artırmada
  bekleyen oyuncu oyunu bekletir (Faz 5'teki yapay zekâ ve süre aşımı ile çözülecek). Bankaya dönen mülkler
  yeniden açık artırmaya çıkmaz, biri üstüne gelene kadar bekler.

## Kurallar

Modüler sistemler, tek sorumluluk, durum değişikliklerini yalnızca host doğrular, her aşama test edilir,
gereksiz paket yok, onay olmadan sonraki faza geçilmez.
