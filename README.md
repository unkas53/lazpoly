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
- **Arayüz:** Faz 6b'den beri uGUI ile koddan kurulur (bkz. Faz 6b). Tahta ve piyonlar Blender modelleri gelene
  kadar kodla yapılmış şekillerdir.

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
- **Kopan oyuncu:** Faz 5'ten beri bağlantısı kopan oyuncunun yerine bilgisayar (orta seviye) oynar; oyuncu
  geri bağlanınca kontrol ona döner.

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
- **Bilinen sınırlar:** Açık artırmada süre sınırı yok; bağlı olduğu hâlde cevap vermeyen bir insan oyuncu
  oyunu bekletir. Bankaya dönen mülkler yeniden açık artırmaya çıkmaz, biri üstüne gelene kadar bekler.

## Faz 5: yapay zekâ ve tek oyunculu

- **Tek oyunculu:** Ana menüde "Tek Oyunculu: sen + 3 bilgisayar" ya da her koltuğun yanındaki düğmeyle
  "İnsan / AI: Kolay / AI: Orta / AI: Zor" seçilir. İnsanlar aynı ekranda sırayla oynar, bilgisayar
  oyuncuları kendi hamlelerini yapar (izlenebilsin diye her hamle arasında kısa bir bekleme var).
- **Bilgisayar oyuncusu (`AiPlayer`):** Satın alma, açık artırma teklifi, ev kurma, ipotekten kurtarma,
  takas teklif etme ve cevaplama, borç ödeme kararlarını verir. Kararları yalnızca durumu okur ve her
  komutu göndermeden önce `GameRules` ile kontrol eder; motor da diğer oyuncular gibi doğrular. Zorluklar
  `AiProfile` içinde (yedek para, teklif sınırı, takas payı, hata oranı); kolay bazen bilerek hata yapar,
  zor grubunu tamamlamak için daha çok öder ve rakibe eksik parçasını pahalıya satar.
- **Kopan oyuncu:** Çevrim içi oyunda kopan oyuncunun yerine host üzerinde orta seviye bilgisayar oynar,
  oyuncu dönünce kontrol geri verilir. Bilgisayar yalnızca hostta çalışır, istemciler sonucu olay olarak görür.
- **Kayıt:** Yerel oyun her hamleden sonra tek kayıt yuvasına yazılır (`lazpoly-kayit.dat`, oyuncunun
  veri klasöründe); oyun bitince silinir. Ana menüde "Kayıtlı Oyuna Devam Et" çıkar. Kayıt zarı ve
  bilgisayarın rastgele sayılarını da sakladığı için oyun kaldığı yerden aynen devam eder. Tahta verisi
  değişmişse kayıt açılmaz. Çevrim içi oyunlar kaydedilmez.
- **Testler:** `AiSimulationTests` (faz kapısı) gerçek tahtada 8 bilgisayarlı 25 oyunu sonuna kadar oynatır;
  her hamlenin kabul edildiğini, durumun tutarlı kaldığını ve her oyunun bir kazananla bittiğini kontrol eder.
  `AiPlayerTests` tek tek kararları, `LocalGameAndSaveTests` kaydı ve aynen devam etmeyi,
  `NetworkSessionTests` kopan oyuncunun yerine oynamayı, sahne testi de kendi kendine oynayan oyunu ve
  kayıttan devamı dener.
- **Bilinen sınırlar:** Bilgisayar takas teklif ederken yalnızca para karşılığı tek mülk ister. Oyuna hiç
  bağlanmayan çevrim içi oyuncunun yerine bilgisayar geçmez (yalnızca bağlanıp kopanların yerine geçer).
  Zorluk değerleri taslaktır.

## Faz 6a: ayarlar, Steam durumu ve başarımlar

- **Ayarlar:** Ana menüde ve oyunda "Ayarlar": ses, tam ekran, dikey eşitleme, hızlı animasyonlar ve
  bilgisayar hamle hızı. Her değişiklik hemen uygulanır ve kaydedilir (`SettingsService`, Windows'ta
  PlayerPrefs). Ses ayarı hazır; oyunda henüz ses yok.
- **Steam durumu (Rich Presence):** Arkadaşların "Ana menüde", "Oda kuruyor (2/8)", "Bilgisayarlara karşı,
  12. tur, 4 oyuncu" gibi bir satır görür (Steam'de "Oyun bilgisi"). Arkadaş listesinin kendisinde
  görünen yerelleştirilmiş satır için gerçek App ID'de Steamworks sitesine bir Rich Presence dosyası
  yüklemek gerekir.
- **Başarımlar:** `AchievementTracker` oyun olaylarından başarımları çıkarır; yalnızca bu bilgisayardaki
  insan oyuncular kazanır (bilgisayarın ve diğer makinelerin hamleleri sayılmaz). `SteamAchievementService`
  Steam'e yazar. App ID 480'de bu başarımlar tanımlı olmadığından Steam kabul etmez (oyun bir uyarı yazar,
  oyun içi olay kaydında yine görünür). Gerçek App ID alınınca Steamworks sitesinde aşağıdaki API adlarıyla
  başarımlar oluşturulmalı:

  | API adı | Başlık | Açıklama |
  | --- | --- | --- |
  | `LAZ_FIRST_WIN` | İlk Zafer | Bir oyunu kazan. |
  | `LAZ_ONLINE_WIN` | Masanın Hâkimi | Çevrim içi bir oyunu kazan. |
  | `LAZ_BEAT_COMPUTERS` | Makineye Karşı | Bilgisayar oyuncularının da olduğu bir oyunu kazan. |
  | `LAZ_FULL_GROUP` | Tam Takım | Bir renk grubunun bütün mülklerine sahip ol. |
  | `LAZ_FIRST_HOTEL` | Otelci | İlk otelini kur. |
  | `LAZ_AUCTION_WIN` | Son Teklif | Bir açık artırmayı kazan. |
  | `LAZ_TRADE_DONE` | Pazarlıkçı | Bir takası tamamla. |
  | `LAZ_BANKRUPT_RIVAL` | Alacaklı | Bir rakibi sana olan borcu yüzünden iflas ettir. |
  | `LAZ_TYCOON` | Kodaman | Aynı anda 5000 paraya sahip ol. |

## Faz 6c: görsel cila (kodla yapılmış modeller)

- Tahta, piyonlar, evler ve zarlar artık kodla üretilen modeller (`MeshFactory`): köşeleri yuvarlatılmış
  kareler, mülklerde grup rengi şeridi, tornalanmış piyonlar, çatılı evler ve otel, ortada "LAZPOLY" yazısı.
- Zar atılınca iki zar ortada yuvarlanıp kuralların belirlediği sayılarda durur (`DiceView`); zar sonucu
  her zaman motordan gelir, animasyon yalnızca gösterir.
- Yumuşak gölgeli ışık ve koyu arka plan. Animasyon hızı ayarlardaki "Hızlı animasyonlar"a uyar.
- Gerçek Blender modelleri geldiğinde bu şekillerin yerine geçecek; tahta düzeni ve görünüm API'si aynı kalır.
- Proje URP ile çizer: `Assets/_Lazpoly/Settings/Lazpoly_URP.asset` Graphics ve tüm Quality seviyelerinde atanmış,
  modeller `Resources/Lazpoly/Materials/Model.mat` malzemesini kullanır. Bunlar eksikse oyun derlemesinde her şey
  eflatun çizilir; `Lazpoly > Set Up Render Pipeline` menüsü (veya
  `-executeMethod Lazpoly.Editor.RenderPipelineSetup.Run`) ikisini de oluşturur, `RenderingSetupTests` denetler.
- Kamera, tahtayı ekran boyutuna göre yan panellerin arasına sığacak uzaklığa çeker.

## Faz 6b: asıl arayüz

- IMGUI prototipi kalktı; menü ve oyun ekranı uGUI ile koddan kurulur, sahne ya da prefab gerektirmez.
  Ek paket yok (`com.unity.ugui` zaten projedeydi).
- `Presentation/UI`: `UiTheme` (renkler, yazı, boyutlar; 1920x1080 referans), `UiRoot` (canvas, panel, pencere),
  `UiBuilder` (etiket, düğme, onay kutusu, kaydırıcı, metin alanı, satır, kart, kaydırmalı liste) ve `UiView`
  (içeriği yalnızca gösterdiği şey değişince yeniden kurar; metin alanları odağını kaybetmez).
- Ana menü: başlık, kayıtlı oyuna devam, yerel oyun (oyuncu adları, renkleri, insan/AI seçimi) ve çevrim içi oda
  yan yana; ayarlar penceresi.
- Oyun ekranı (`GameHud`): solda oyuncular, zar, sıradaki hamleler, açık artırma, takas cevabı, borç ve mülkler;
  sağda olay kaydı ve menü; takas teklifi, ayarlar ve oyun sonu ayrı pencerede.
- Kamera tahtayı iki yan sütunun arasındaki alana olabildiğince büyük ve dikeyde ortalı yerleştirir
  (`BoardCameraFraming`). Kare adları daha keskin çizilir.
- Arayüz hâlâ yalnızca kuralların izin verdiği hamleleri sunar; host her komutu yeniden doğrular.

## Faz 6d: mağaza hazırlığı

- `Steam/` klasörü (Unity'nin dışında): iş ortağı sitesi kurulum rehberi (`Steam/README.md`, başarım tablosu
  dahil), Rich Presence dosyaları, SteamPipe `app_build.vdf` / `depot_build_windows.vdf` şablonları.
- `Lazpoly > Build Store Version` (`Lazpoly.Editor.StoreBuild.Run`): `Builds/Store/windows/` içine geliştirme
  seçenekleri kapalı, `steam_appid.txt` içermeyen Win64 derlemesi. Test App ID'siyle yalnızca
  `-allowDevelopmentAppId` verilirse deneme derlemesi alır.
- Gerçek App ID ile arkadaş listesinde durum satırı da görünür (`steam_display` = `#Status`).
- **Bekleyen (App ID gerekli):** `SteamSettings.AppId`, `steam_appid.txt`, vdf'lerdeki `APP_ID`/`DEPOT_ID`,
  başarımların ve Rich Presence dosyalarının siteye girilmesi, başarım simgeleri, yükleme ve kapalı beta anahtarları.

## Faz 7a: okunurluk ve mülk kartı

- **Kare kartı:** Bir kareye (ya da köşe kartındaki mülk rengine) tıklayınca kartı ekranın ortasında açılır; ×, Esc ya da dışarı tıklama kapatır. Fareyle üstüne gelmek yalnız o rengin karelerini biraz aydınlatır; diğer kareler olduğu gibi kalır (Faz 11e).
  Kartta fiyat, bütün kira tablosu (geçerli kira vurgulu), ev maliyeti, ipotek değeri, sahibi ve rengin kimde ne
  kadar olduğu yazar (`Texts/SquareInfo`). Kart, karenin ters tarafında açılır ve fareyi engellemez.
- **Grup vurgusu:** Kart açıkken aynı renkteki kareler parlar, diğerleri kararır (`BoardView.Highlight`).
- **Sahiplik:** Sahipli karenin çevresinde sahibin renginde bir çerçeve olur, ipotekliyse koyulaşır. Sol panelde her
  oyuncunun altında mülkleri tahta sırasıyla küçük renk kutucukları olarak dizilir.
- **Kare düzeni:** Ad ve fiyat karenin dış yarısında, kalın yazıyla; piyonlar iç yarısında (`BoardView.PawnSpot`).
- **Kamera:** Fare tekeri yakınlaştırır, sağ tuşla sürükleyince tahtanın çevresinde döner, Home tuşu ya da
  "Görünümü Sıfırla" başa alır (`BoardCameraRig`).
- **Sıra sende:** Sıra bu ekrandaki bir oyuncuya geçince üstte kısa bir yazı çıkar.
- **Olay kaydı:** Gelen para yeşil, giden para kırmızı (`EventDescriber.ToneOf`).

## Faz 7b: ses ve animasyon

- **Sesler:** 18 kısa efekt (zar, adım, satın alma, para, inşaat, ipotek, açık artırma, takas, sıra, başarım,
  kazanma, iflas, tıklama). Hepsi `Tools/Audio/make_sfx.py` ile koddan üretildi, yani tamamen bizim; dış paket yok.
  Yeniden üretmek için: `python Tools/Audio/make_sfx.py` (numpy gerekir). Müzik daha sonra.
- **Hangi olay hangi ses:** `Audio/SoundCues.For` tek yerde karar verir ve test edilir. Bir eylem birden çok olay
  üretse de bir kez ses çıkar (satın alma ödemede çalar, kira ödeyen tarafta).
- **Çalıcı:** `AudioPlayer` oyun boyunca yaşar, 10 sesli bir havuzdan çalar, tekrarlanan seslerin perdesini biraz
  oynatır. Ses düzeyi ayarlardaki "Ses" kaydırıcısına bağlı. Her arayüz düğmesi tıklar, her pencere açılırken ses verir.
- **Animasyonlar:** piyon zıplarken uzar, inerken basılır; para değişince piyonun üstünde "+200 / -60" yükselir ve
  sol paneldeki para sayarak değişir (ödeme ekranda görülmeden önce para değişmez); evler tahtaya düşüp seker; satın
  alınan karenin çerçevesi kabarır; iflas eden piyon devrilip tahtaya gömülür; kazanınca tahtaya konfeti yağar;
  pencereler yumuşakça açılır. Parçacık sistemi kullanılmadı, her şey kodla yapılmış basit parçalar.

## Faz 7c: oyun tasarımı

- **Özel kareler:** Çay Molası (vergiler ve kart cezaları kumbarada birikir, gelen hepsini alır), Sokak Haberi
  (24 kartlık deste `board.json` içinde), Otogar (bilet parasıyla istenen kareye gidilir, başlangıç bonusu yok),
  Seyir Terası (sonraki sırada iki atış, oyuncu birini seçer).
- **Masa kuralları:** ana menüdeki "Masa Kuralları" penceresinden başlangıç parası, açık artırma açık/kapalı ve
  tur sınırı (kısa oyun: sınır dolunca en değerli oyuncu kazanır). Çevrim içi odada ev sahibinin seçimi geçerli,
  kayıtlı oyun kendi kurallarıyla sürer. Protokol sürümü 3, kayıt biçimi 2.
- **İpuçları ve Nasıl Oynanır:** ilk oyunda durum ortaya çıktıkça tek seferlik ipuçları (alttaki kutu, "İpuçlarını
  Kapat" ile kapanır). "Nasıl Oynanır" sayfası menüde ve oyun içinde, sayıları tahtadan ve masa kurallarından alır.
- **Denge:** `Lazpoly/Balance Report` menüsü (ya da
  `-executeMethod Lazpoly.Editor.BalanceReportMenu.Run -balanceGames 1000 -balancePlayers 2,4,6`) yalnızca bilgisayar
  oyunculu oyunlar oynatıp `Reports/Balance` altına rapor yazar: oyun süresi, sıra ve zorluk başına kazanma, renk
  grubu başına kira geliri, karelere gelinme. Fiyat/kiralar bu raporlarla ayarlandı; Zor yapay zekâ artık ev için
  tek mülklerini ipotek eder ve gerçekten en güçlüsü. Kimsenin satmadığı masada teklifler turlar geçtikçe büyür,
  böylece oyun sonsuza dek sürmez.
- **Çevrim içi süre:** sıra için 90, açık artırma ve takas cevabı için 30 saniye. Süre dolunca host en temkinli
  hamleyi yapar (zar atar, almaz, pas geçer, reddeder, otogarda kalır); üst üste iki kez dolarsa ya da borç varsa
  bilgisayar o koltukta oynar, oyuncu tek bir hamle yapınca kontrol ona döner. Ekranın üstünde kalan süre görünür.

## Faz 7d: görsel kimlik ve yayın hazırlığı

- **Tema:** piyonlar kasabanın şekilleri (çay bardağı, fener, simit, şamandıra, çan, kayık, çaydanlık, saat kulesi;
  `PawnShapes`), ev ve otel yerine ahşap ev ve konak (`BuildingParts`), tahtanın ortası deniz ve üstünde kasabalı
  bir ada, fener, kayık. Hepsi koddan üretilir; Blender modelleri sonraya kaldı.
- **Ortam:** ahşap masa ve zemin (`RoomDressing`), sıcak güneş + soğuk dolgu ışığı, yumuşak gölgeler, kamerada
  HDR, SMAA, bloom, renk düzeltme (ACES) ve vinyet. Ana menünün arkasında gerçek tahta yavaşça döner (`MenuBackdrop`).
- **Yazı tipi ve palet:** Nunito Regular/Bold (OFL, `UI/Resources/Lazpoly/Fonts`), akşam kıyısı paleti (`UiTheme`).
- **Mağaza görselleri:** `python Tools/Art/make_capsules.py` logo ve Steam kapaklarını `Steam/Art` altına çizer
  (bkz. `Steam/README.md`).
- **Ayarlar:** Ses (genel, efektler, müzik ayrı), Görüntü (kalite Düşük/Orta/Yüksek: Düşük post-processing'i ve
  gölgeleri kapatır, çözünürlük seçici, tam ekran, VSync), Oyun, Sorun bildirme. Eski ayarlar korunur; yeni
  alanlar varsayılanla gelir.
- **Müzik:** `python Tools/Audio/make_music.py` iki sakin parça üretir (`kiyi`, `aksam`, 64 sn, 22 kHz);
  `MusicPlayer` sırayla ve çapraz geçişle çalar.
- **Hata günlüğü:** `ErrorLogService` hataları ve istisnaları `persistentDataPath/lazpoly-hata.log` dosyasına yazar
  (512 KB dolunca `.1` olarak saklanır); ayarlardaki "Günlük klasörünü aç" düğmesi klasörü açar.
- **Kayıt uyumluluğu:** 1. sürüm kayıt dosyaları (masa kuralları öncesi) hâlâ açılır: varsayılan masa, boş
  kumbara, tur numarası tahmini.
- **İngilizce:** koddaki Türkçe metin anahtardır; `L("...")` düz, `F($"...")` biçimli metinleri
  `UI/Resources/Lazpoly/Lang/en.json` dosyasından çevirir (eksik girdi Türkçe kalır). Dil ayarı Otomatik (sistem
  dili Türkçe değilse İngilizce), Türkçe, English; değişince bütün görünümler yeniden çizilir, tahtadaki 3B
  yazılar sahne yeniden yüklenince. Sokak ve meydan adları özel ad olarak Türkçe kalır; kartlar, özel kareler ve
  renk grupları çevrilir. `LangTests.EveryTextInTheCodeHasAnEnglishEntry` kaynak kodu tarar: çevirisi olmayan
  metin testi kırar.

## Faz 8: şehir kartları, bayraklar, yeni ev kuralı ve arayüz cilası

- **Ev ve otel kuralı değişti:** renk grubu şartı yok. Bina, sahibinin **o sırada geldiği** kendi şehrine
  kurulur (`GameRules.BuildableNow`): oyuncu başlangıçtan ilk kez geçene kadar her şehrine tek ev, geçtikten
  sonra dörde kadar (`PlayerState.Laps`, başlangıçtan geçişte artar); otel, dört evli şehre **bir sonraki
  gelişte** kurulur. `GameState.BuiltThisVisit` o gelişte kurulan bina sayısını tutar (sıra başında
  `NotArrived`, piyon hareket edince 0); anlık görüntü sürümü 4, kayıt formatı 4, protokol 5 (eski kayıtlar
  açılır: bina sayacı "gelmedi", tur sayısı biten tur başına bir geçiş sayılır). Evler eşit kurulmaz, tek tek satılır; binalı şehir
  ipotek edilemez ve takas edilemez (yalnızca o şehir, rengin diğer kareleri değil). Renk grubunun tamamı yine
  boş kirayı katlar. Yapay zekâ üzerinde durduğu şehre kurar, parası yetmezse başka bir mülkünü ipotek eder.
- **Şehir kartı:** sahipsiz bir şehre gelince ortada kart açılır: renk, fiyat, kira tablosu, ipotek değeri,
  rengin durumu ve paran/alırsan kalan para; altında **Satın Al (fiyat)** ve **Devam Et** (açık artırma
  açıksa devam edince şehir açık artırmaya çıkar).
- **Bina kartı:** kendi şehrine gelip en az bir bina kurabiliyorsan kart açılır: kaç ev (−/+), toplam fiyat,
  paran, kalan para ve yeni kira; dört evli şehre dönüşte "Otel Kur". **Şimdi Değil** o geliş için kapatır,
  sol sütundaki **Bina Kartı** düğmesi yeniden açar; "Mülklerin" listesindeki Ev +/Otel + düğmeleri de duruyor.
  Kart her ev için ayrı `BuildCommand` gönderir; host her birini sırayla doğrular.
- **Bayrak ve renkli binalar:** satın alınan şehirde binaların yerine sahibin renginde flamalı bir bayrak
  dikilir (`BuildingParts.Flag`); satın alınınca yerden yükselir (`BoardView.RaiseFlag`), ipotekte kararır,
  ilk bina gelince kalkar (son bina satılınca döner). Evler ve konak sahibinin renginde, çatılar koyu tonu.
- **Arayüz cilası:** çalışma anında çizilen ikonlar (`UiIcons`: zar, insanlar, ağ, kitap, sürgüler, kapı,
  liste, ev, bayrak, yer imi), ikonlu düğmeler ve tıklanabilir mod kartları (`UiBuilder.IconButton`,
  `Tile`, `Title`), düğme ve kartlarda gölge, üstüne gelince hafif büyüme (`UiHover`), panellerde üst
  kenarda vurgu çizgisi (`UiRoot.Panel`), oyuncu renkleri yuvarlak, ana menüde bayraklı logo ve gölgeli başlık.
- **Metinler:** ipucu, "Nasıl Oynanır", kare kartı ve bina ipuçları yeni kurala göre; İngilizce girdiler eklendi.
- **Denge:** yeni kuralla bina kurma fırsatı seyrekleştiği için oyunlar uzamıştı (4 oyuncuda ortalama 19 → 26
  tur). Ev maliyetleri yaklaşık %35 düştü, binalı kiralar yaklaşık %40 arttı, başlangıç ikramiyesi 200 → 150;
  Kolay ve Orta yapay zekâ bina fırsatını hata payı oranında kaçırır. 1000'er oyunluk `Reports/Balance`
  raporları yenilendi: 2 oyuncuda ortalama 16, 4'te 19.5, 6'da 24 tur; Kolay/Orta/Zor kazanma payı 4 oyuncuda
  %14 / %29 / %32.

## Faz 9: ipotek yerine mülk satışı

- **İpotek kalktı:** `MortgageCommand`, `RedeemCommand`, `MortgageChangedEvent`, `IsMortgaged` ve
  `mortgageInterestPercent` tamamen silindi; mülk ya sahibindedir ya bankada.
- **Mülk satışı:** oyuncu kendi sırasında (zar öncesi, sıra sonu, satın alma/borç/otogar kararlarında) bir
  mülkünü bankaya satar (`SellPropertyCommand`): banka fiyatın yarısını (`SquareDefinition.SaleValue`) ve
  üstündeki binaların geri satış değerini tek ödemede verir (`GameRules.PropertySaleProceeds`); binalar iner,
  şehir yeniden sahipsiz kalır ve üstüne gelen alabilir. Olay sırası: bina seviyesi 0 → sahip bankaya → para
  (`MoneyReason.PropertySale`). İflasta artık ipotek temizliği yok, mülkler alacaklıya olduğu gibi geçer.
- **Arayüz:** "Mülklerin" listesinde **Sat +tutar** düğmesi; kare kartında "İpotek değeri" yerine
  "Satış değeri"; borç kartı, ipuçları ve "Nasıl Oynanır" satış kuralına göre; takas listesinde ve tahtada
  ipotek karartması yok. Satış sesi bina satışıyla aynı.
- **Yapay zekâ:** borçta önce binaları, sonra en ucuz mülkünü satar; ev için mülk satmaz (yarı fiyat
  kayıptır), Zor seviyenin "ev için ipotek" davranışı kaldırıldı.
- **Sürümler:** anlık görüntü 5 (ipotek baytı yok), kayıt formatı 5, protokol 6. Eski kayıtlar açılır:
  ipotekli şehir sahipli olarak gelir.
- **Denge:** 1000'er oyunluk raporlar yenilendi; 2 oyuncuda ortalama 16, 4'te 18.5, 6'da 21.5 tur, takılan oyun yok.

## Faz 10a-1: hissiyat (cila)

Cila planı: https://claude.ai/artifact/53ynSWjq5NCCCj3gSZceTf. Bu PR kural değiştirmez; her hamlenin sonucunu ekranda
bakmadan da anlaşılır kılar.

- **Sıra geçişi:** `TurnStartedEvent` oynatılırken yeni oyuncunun piyonu yerinde bir kez zıplar
  (`PawnView.Bob`); kamera oyuncunun bıraktığı yerde kalır. Sıra bu ekranda olmayan birine ya da bilgisayara geçince kısık bir nota (`turn_pass`), bu
  ekrandaki kişiye geçince eskisi gibi "sıra sende" akoru. Bekleme metni adı söyler: "Ayşe düşünüyor (bilgisayar)...".
- **Zar sonucu:** zarlar durunca üstte büyük rakamla "3 + 4 = 7" (`GameHud.BuildRollPopup`), çiftte "Çift! Sıra bitince
  yeniden atarsın", üçüncü çiftte "hamle yanar, sıra geçer" (`GameSceneController.ShownRollForfeited`, sıradaki olayın
  `TurnEnd` olmasından anlaşılır). Piyon, sonuç okunsun diye `RollReadSeconds` (0,55 sn) bekleyip yürür.
- **Piyon hareketi:** bir yürüyüş en çok 2 saniye sürer, uzun hamlelerde adımlar hızlanır (`PawnView.MaxWalkSeconds`).
  Başlangıçtan geçince başlangıç karesi kabarır ve ikramiyenin kendi sesi çalar (`pass_start`).
- **Para akışı:** kira ödenince ödeyenin piyonundan alanın piyonuna jetonlar uçar (`Effects/CoinFlight`, 120 başına bir
  jeton, en çok dört); alan tarafın parası jetonlar inince değişir. Vergi ve kart cezaları kumbaraya uçar, kumbara yazısı
  sallanır; kumbara boşalınca kabarır (`BoardView.NudgePot`).
- **Binalar ve bayrak:** ilk bina gelince bayrak katlanarak iner (`BoardView.Lower`), satılan ya da iflasla giden binalar
  küçülerek kaybolur (`Shrink`), mülk bankaya satılınca bayrak önce iner sonra kalkar (`ClearOwner`). Otel kurulunca ayrı
  ses (`hotel`), mülk satışının kendi sesi (`property_sale`); Faz 9'dan kalan `mortgage.wav` silindi.
- **Oyun sonu:** bitiş penceresinde bütün oyuncuların sıralaması (para, mülk sayısı, değer; iflas edenler sonda) ve yerel
  oyunda aynı koltuklarla **Yeniden Oyna** (`GameSceneController.RestartLocalGame`).
- **Hız ayarı:** zaten vardı ("Hızlı animasyonlar"); yeni animasyonların hepsi aynı çarpanı kullanır.

## Faz 10a-2: duraklatma menüsü ve klavye kısayolları

Cila planının ikinci adımı. Kural değişmez; oyun klavyeden de oynanır ve bir menüden çıkılır.

- **Duraklatma menüsü (Esc):** sağ alttaki eski dört düğme yerine tek bir **Menü (Esc)** şeridi var; log paneli
  kazanılan yeri alır. Menü: **Devam Et (Esc)**, Ayarlar, Nasıl Oynanır (H), Görünümü Sıfırla (Home), kısayol listesi
  ve çıkış. Yerel oyunda menü açıkken bilgisayar hamle yapmaz ve sıradaki olay gösterilmez (`GameSceneController.Paused`,
  `SetPaused`); çıkış **Kaydet ve Çık**'tır ve oyun eskisi gibi tek kayıt yuvasına yazılır. Çevrim içi oyun
  duraklatılamaz; menü bunu söyler, çıkış **Oyundan Ayrıl**'dır (misafir için yerine bilgisayar oynar, ev sahibi için
  oda kapanır). Oyun bittiyse çıkış **Ana Menü** olur.
- **Kısayollar (`Game/Hotkeys.cs`):** Boşluk/Enter zar atar ya da sırayı bitirir, B bina kartını yeniden açar, T takas
  teklif eder, H nasıl oynanır, Esc önce üstteki pencereyi kapatır (ayarlar, yardım, menü, takas, bina kartı, bitiş
  penceresi) yoksa menüyü açar. Şehir kartı Esc ile kapanmaz; satın alma ve borç kararları tuşla verilmez. Bir tuş
  yalnızca o an ekranda olan düğmenin yaptığını yapar: `Hotkeys.Resolve` ekrandaki duruma (`HotkeyContext`) bakar,
  komut yine `IsAllowed` süzgecinden geçer ve ev sahibi doğrular. Bir metin alanı odaktayken (takas tutarı) tuşlar
  komut değildir.
- **Tuş ipuçları:** düğmeler kısayolunu söyler: "Zar At (Boşluk)", "Sırayı Bitir (Boşluk)", "Bina Kartı (B)",
  "Takas Teklif Et (T)", "Nasıl Oynanır (H)", "Menü (Esc)".
- **Testler:** `HotkeysTests` (EditMode) tuş-durum eşlemesini, `InterfaceTests.KeysOpenThePauseMenuAndPlayTheTurn`
  (PlayMode) menünün açılıp oyunu durdurmasını, yardımın açılıp kapanmasını ve Boşluk'un zar atmasını `GameHud.PressKey`
  üzerinden dener.

## Faz 10a-3: arayüz tutarlılığı ve okunurluk

Cila planının üçüncü adımı. Kural değişmez; her ekran aynı dili konuşur ve küçük ya da çok büyük ekranlarda da sığar.

- **Kartlar tek düzende:** şehir, bina, borç, açık artırma ve takas kartları aynı başlığı (`UiBuilder.CardHeader`: renk
  kutusu, başlık, alt satır, ince çizgi) ve aynı düğme sırasını kullanır (`UiBuilder.Actions`: vazgeç solda, asıl eylem
  sağda, 52 piksel). Bütün pencere başlıklarının altında aynı çizgi var (`Title`). Karar pencereleri aynı genişlikte
  (`UiLayout.WindowWidth` 520), takas geniş (760).
- **Arayüz boyutu ayarı:** Ayarlar > Görüntü > Arayüz boyutu: Küçük / Normal / Büyük (`GameSettings.UiScale`,
  çarpanlar 0,88 / 1 / 1,18). Canvas ölçekleyicisi referans çözünürlüğü bu çarpana böler (`UiRoot.ApplyScale`), açık
  ekranlar anında yeniden ölçeklenir; paneller de büyüdüğü için metin kesilmez.
- **Çözünürlük:** arayüz 1920x1080 referans pikseliyle çizilir ve ekranla yarı genişlik yarı yükseklik ölçeklenir
  (`UiLayout.CanvasScale`). `UiLayoutTests` 1280x720, 1920x1080, 1920x1200, 2560x1080 ve 3840x2160'ta üç arayüz
  boyutunda tahtaya en az %45 genişlik kaldığını ve en geniş/en uzun pencerenin kenar payıyla sığdığını hesaplar
  (en kötü durum: 1920x1200 Büyük, %46,6).
- **Ekran geçişleri:** menü, lobi ve oyun arasında kısa karartma (`Effects/SceneFader.Load`: 0,18 sn kararır, sahne
  yüklenir, 0,28 sn açılır; bu sürede tıklama almaz). Bütün `SceneManager.LoadScene` çağrıları buradan geçer.
- **Ana menü:** sağ üstte sürüm (`Application.version`); kayıtlı oyun düğmesinin altında "Tur 12 · 4 oyuncu ·
  01.11.2026 18:30" (`MainMenuController.SaveSummary`, tarih dosyanın yazılma zamanı). Okunamayan ya da başka tahta
  verisiyle yazılmış kayıt için uyarı ve **Kaydı Sil** düğmesi (`SaveGameStore.Exists`, `SavedAt`).
- **Lobi:** her üyenin yanında hazır/bekliyor işareti (ev sahibi her zaman hazır), misafirde **Hazırım** düğmesi, ev
  sahibinde "Herkes hazır" ya da "2/3 hazır" durumu; **Çevrim İçi Oyunu Başlat** yalnızca en az iki kişi ve herkes
  hazırken açılır. Ev sahibinin masa kuralları odaya yazılır ve herkes aynı özeti görür (`ILobbyService.Table`,
  `SetTable`; Steam'de lobi verisi `lazpoly_table`, hazır bilgisi üye verisi `lazpoly_ready`). Oda kodu (Steam lobi
  kimliği) **Kopyala** ile panoya; ana ekranda "Oda koduyla katıl" alanı.
- **Uyarılar tek tip:** `UiBuilder.Notice` (uyarı simgeli kart, ne olduğu ve ne yapılacağı, isteğe bağlı düğme). Steam
  kapalı, oda kurulamadı, oda dolu, koda katılınamadı, kayıt bozuk, komut reddedildi, takas gönderilemedi ve çevrim içi
  oyunun durma nedenleri (`EndReasonText`: artık "ne yapmalı" cümlesiyle) hep bu kartla gösterilir.
- **Sol panel:** iflas eden oyuncular listenin sonuna iner. İpucu kutusu zaten alt ortada, olay kaydını kapatmaz.
- **Ertelenen:** olay kaydının sıra sıra gruplanması ve adların renklenmesi, mülk kutucuğunda şehir adı ipucu, para
  değişiminde kart parlaması (10a-4'e kaldı).

## Faz 10a-4: sol panel ve olay kaydı

10a'nın son küçük adımı; plandaki "Sol panel" ve "Olay kaydı" maddeleri.

- **Olay kaydı:** satırlar artık `LogEntry` (metin, ton, kimin satırı, sıra başlığı mı). Her sıranın satırları o
  oyuncunun renginde bir "Sıra: Ayşe" başlığının altında toplanır; her satırın önünde satırın ait olduğu oyuncunun
  renginde küçük bir nokta (`EventDescriber.ActorOf`: hareket eden, ödeyen, yeni sahip, kazanan). Kayıt 10 yerine
  40 satır tutar; en yeni üstte, panel kaydırılabilir. `Log` artık `IReadOnlyList<LogEntry>`.
- **Para parlaması:** bir oyuncunun parası sayarken kartı kazançta yeşile, kayıpta kırmızıya hafifçe parlar ve yarım
  saniyede söner (`GameHud.FlashCard`).
- **Mülk ipucu:** sol paneldeki mülk kutucuklarının üstüne gelince o şehrin kartı açılır (`SquareHoverTarget`;
  `ShownSquare` sırası: sabitlenmiş kare, tahtada gösterilen kare, paneldeki kutucuk).
- İflas eden oyuncunun soluk görünmesi ve listenin sonuna inmesi 10a-3'te yapılmıştı.

## Faz 10b-1: tahta görseli

10b'nin (görsel ve hız) ilk adımı; plandaki "Görsel cila" bölümünün kare, piyon ve bina maddeleri. Blender
modeli yok; koddan üretilen parçalar iyileştirildi.

- **Kare yüzeyleri:** renk grubu şeridi daha geniş ve kabarık (`BoardView.BandOffset/BandDepth`); her karenin adı
  ve fiyatı koyu bir plaka üzerinde açık yazıyla (uzaktan okunurluk). Özel karelerde küçük işaret: çay bardağı
  (Çay Molası), minibüs (Otogar), dürbün (Seyir Terası), katlanmış gazete (Sokak Haberi) — `SquareIcons`.
- **Piyonlar:** aynı karede duran piyonlar yuvalara oturur (`PawnSlots`: 4 oyuncuya kadar 2x2, sonrası 3x3) ve
  `PawnCrowd` aynı yuvayı iki piyona vermez; gelen en düşük boş yuvayı alır, giden yuvasını bırakır. Sıradaki
  oyuncunun piyonunun altında rengiyle nefes alan bir halka (`TurnMarker`); yürüyen piyon her sıçrayışta ayrıldığı
  yerde yarım saniyede küçülen bir iz bırakır (`PawnTrail`, 6 disk havuzdan).
- **Binalar:** evler kareye göre küçültülüp aralandı (`HouseStep/HouseSize`), konak biraz büyüdü; otelin bacasından
  ince duman çıkar (`ChimneySmoke`, 4 puf havuzdan, yalnızca görünen konaklarda çalışır; adadaki konakta da);
  sahip bayrağının kumaşı hafifçe dalgalanır (`FlagWave`, yalnızca dikili bayraklarda).
- Testler: `PawnSlotsTests` (yuva aralığı, kalabalık kare, bayrak salınımı), `ModelTests` (küre ve işaretler).

Düzeltme (kare adları PR'ı): yan sıralardaki kare adları artık plakayla birlikte kenar boyunca döner (harflerin üstü
tahtanın ortasına bakar, `BoardView.LabelRotation`), uzun adlar plakaya sığacak kadar küçülür (`FitCharacterSize`),
plaka piyon yarısından uzaklaştı ve kenarı şeridin altına girer (`PlaqueOffset` 0,09, `PlaqueDepth` 0,32), iki satırlı adlar küçülmeden sığar; yalnızca uzun adlar daralır.

## Faz 10b-2: deniz, gün ışığı, kamera, zarlar

Plandaki "Görsel cila" bölümünün kalan sahne maddeleri.

- **Deniz ve ada:** deniz artık bir ızgara yüzey (`SeaSurface`, 26x26 kare); köşe noktaları iki çapraz sinüs dalgasıyla
  salınır, shader yok. Kayık yerinde ağır bir elipste süzülüp hafifçe yalpalar, halkalar nefes alır; akşam çökünce
  deniz fenerinin ışığı yanar ve döner (`TownLife`).
- **Gün ışığı:** gün öğlen başlar, tur ilerledikçe akşama kayar (`DayCycle`: tur sınırı varsa sınırda, yoksa 24. turda
  alacakaranlık; oyun bitince her zaman akşam). Güneşin rengi, açısı ve şiddeti, dolgu ışığı ve ortam ışığı birlikte
  değişir (`RoomDressing.ApplyDayLight`); geçiş saniyeler içinde yumuşak. Ayarlardan kapatılabilir
  (`GameSettings.DayCycle`, "Gün ışığı akışı").
- **Kamera:** iki hazır açı, masa başı (55°) ve kuş bakışı (76°), C tuşu ya da duraklatma menüsündeki Kamera Açısı düğmesiyle
  (`CameraView`, `BoardCameraRig.PitchOf`). Bir kare sabitlenince görünüm o kareye doğru yolun %30'u kadar kayar ve
  sabit kalkınca geri döner (`Peek`); kayma tahtanın kenarını aşamaz (`ClampToBoard`). Sıra değişiminde kamera yine
  kıpırdamaz.
- **Zarlar:** her zarın altında yükseldikçe küçülen bir gölge; duran zar kameraya dik gelecek şekilde döner, en fazla
  14° oynama (`DiceView.RestingYaw`). Zarlar dalganın tepesinin biraz üstünde durur.
- Testler: `SceneryTests` (gün akışı, akşam ışığı, dalga sınırı ve deniz yüzü, kayık, kamera kayma sınırı, zar
  yönü), `HotkeysTests` (C), `GameSettingsTests` (gün ışığı ayarı).

## Faz 10b-3: kalite ve performans

10b'nin son adımı; plandaki "Post-processing", "Ekran görüntüleri" ve "Performans ve kalite kapıları" maddeleri.

- **Post-processing:** bloom eşiği 1.25, şiddeti 0.18 (yazılar artık parlamıyor); vinyet 0.18. Orta kalitede bloom
  kapalı, renk ayarı ve vinyet açık; Düşük'te post-processing tamamen kapalı. Gölgeler her kalitede yumuşak; fark gölge
  haritasının boyutunda (4096 / 2048 / 1024). Tek gölge kademesi ve 40 birim gölge mesafesi (`RoomDressing.ApplyQuality`).
- **Performans ölçümü:** `Lazpoly.exe -perf` ana menüden doğrudan 8 bilgisayarlı bir masa açar, 3 sn ısınmadan sonra
  15 sn ölçer, raporu exe'nin yanına `lazpoly-perf.txt` olarak yazar ve kapanır (`PerfRun`, `PerfProbe`, `PerfReport`).
  Rapor kapıları plandaki gibi: ortalama ≥ 60 FPS (Düşük'te ≥ 30), en yavaş %1 bilgi olarak, bellek < 600 MB,
  menü → masa < 3 sn (`SceneFader.LoadRequestedAt` ile ölçülür, log satırına da yazılır). Oyun içinde F3 kare hızı ve
  belleği gösterir. Ayrıntı ve sonuçlar: `Reports/Perf/README.md`.
- **Ekran görüntüleri:** F11 beş mağaza açısı arasında gezer (`ScreenshotPresets`: masa başı, kuş bakışı, yakın köşe,
  ada ve deniz, karşı kıyı), F12 `persistentDataPath/Screenshots` altına PNG kaydeder. Hangi açının neyi göstereceği
  `Tools/Art/screenshots.md` içinde.
- Plandaki diğer performans maddeleri: koddan üretilen ağlar zaten `MeshFactory` önbelleğinde tek kez üretiliyor; arayüz
  `UiView` anahtarıyla yalnızca durum değişince yeniden çiziliyor. Build boyutu 38 MB (< 60 MB kapısı).
- Testler: `PerfTests` (FPS hesabı, kapılar, rapor metni, -perf bayrağı, ön ayarlar), `RenderingSetupTests` (kalite
  eşlemesi), `HotkeysTests` (F3/F11/F12).

## Çadır: sahipli şehrin işareti

Bayrağın yerini oyuncunun çadırı aldı. Şehir satın alınınca kare üstünde, piyonların arkasında, sahibin renginde bir
kamp çadırı kurulur (kumaş sahibin rengi, dikişler aynı rengin koyu tonu; direkler, şeritler ve taban kendi renginde).
İlk ev yapılınca çadır bayrak gibi yere iner, son bina satılınca geri gelir; şehir satılınca kalkar.

- Kaynak model: `Tools/Art/Models/CampTent` (OBJ + MTL, Blender dosyası, model bilgisi). Oyun bu klasörü doğrudan kullanmaz.
- `python3 Tools/Art/make_tent.py` ipleri, kazıkları ve ip etiketlerini kırpar, çadırı ortalayıp boyunu 1 birime getirir ve
  `Assets/_Lazpoly/Data/Resources/Lazpoly/Models/camp_tent.txt` dosyasını yazar (1.116 üçgen, 5 parça). Model
  değişirse yalnızca bu komut yeniden çalıştırılır.
- `ObjModel` küçük bir OBJ okuyucudur (v/vn/f, nesne ve malzeme grupları, sağ elden sol ele çevirme). `TentParts` modeli
  bir kez yükler, parça başına `ModelParts` malzemesi kurar ve `Paint` ile kumaşı boyar. `OwnerMarker` kare üstündeki
  işarettir: model eksikse ya da okunamazsa uyarı verip eski bayrağa döner, oyun durmaz.
- Boyut ve yer: çadır karenin 0,30'u uzunluğunda, iç kenara yakın (`OwnerMarker.TentLength` / `TentOffset`); piyon
  yuvalarıyla çakışmaz.
- Testler: `ObjModelTests` (okuyucu, kırpılmış çadır dosyası, boyama ve yerleşim).

## Faz 10c-1: yeniden bağlanma

Çevrim içi oyunda bağlantı sorunları artık oyunu bitirmez; herkes ne olduğunu görür.

- **Misafirin bağlantısı koparsa:** tahta ekranda kalır, "Ev sahibiyle bağlantı koptu; yeniden deneniyor" uyarısı
  geri sayımla görünür. İstemci her 3 saniyede bir ev sahibine yeniden selam gönderir (`ClientSession.RetrySeconds`);
  iki cevapsız denemeden sonra odadan çıkıp yeniden girer (Steam odası da düşürmüş olabilir, ev sahibi yalnızca oda
  üyelerini kabul eder). 45 saniye içinde cevap gelmezse (`GiveUpSeconds`) "Ev sahibi ayrıldı" penceresi açılır. Bu
  sırada ev sahibinde koltuğu bilgisayar oynar; selam gelince durum anlık görüntüyle yenilenir ve kontrol oyuncuya döner.
- **Oyun kapanmadan koparsa:** oda kimliği hatırlanır (`LastRoomStore`); ana menüde "Çevrim İçi Oyuna Yeniden Bağlan"
  düğmesi çıkar, aynı Steam hesabı aynı koltuğa döner. Oyun bitince, oyuncu bilerek ayrılınca ya da oda artık yoksa unutulur.
- **Hiç gelmeyen oyuncu:** oyun başladıktan 30 saniye sonra (`HostSession.NoShowSeconds`) koltuğuna bilgisayar oturur;
  oyuncu sonradan gelirse koltuğu yine alır.
- **Ev sahibi ayrılırsa:** bilerek ayrılış (menüden çıkış, pencereyi kapatma) `HostLeaving` mesajıyla herkese anında
  bildirilir; oda da "üye ayrıldı" derse misafirler beklemeden durur. Misafir pencerede "Bilgisayarlara Karşı Devam Et"
  seçebilir: son görüntü yerel kayda dönüştürülür, diğer koltuklara bilgisayar oturur (önceki yerel kaydın üzerine yazılır).
- **Durum yayını:** ev sahibi 2 saniyede bir ve her bağlantı değişiminde `Presence` mesajı gönderir (kim bağlı, kimi
  bilgisayar oynuyor, gecikme). Sol panelde her oyuncunun yanında yeşil/kırmızı bağlantı noktası, gecikme ("42 ms") ve
  gerekirse "Bağlantı yok, bilgisayar oynuyor" / "Bilgisayar oynuyor (hamle yapmadı)" etiketi görünür. Gecikme Steam'in
  oturum ölçümünden okunur (`ITransport.PingMs`). Protokol sürümü 7'ye çıktı; eski sürümle oynayan "Steam'den güncelle" uyarısı alır.
- Testler: `NetworkSessionTests` (kopan istemcinin geri dönüşü, gelmeyen oyuncu, bilerek ayrılan ev sahibi, bekleme
  süresinin dolması, durum yayını), `MessageCodecTests` (yeni mesajlar), `LastRoomStoreTests`. `LoopbackNetwork.Sever`
  ve `Restore` bir makinenin kendi hattının kopup gelmesini taklit eder.

## Faz 10c-2: masa kuralları ve sağlamlık

Masa kurulumuna iki yeni kural eklendi; hata günlüğü ve sürüm uyumu güçlendi.

- **Hamle süresi:** masa kuralları penceresinde 60 / 90 / 120 saniye ya da "Süresiz" seçilir (`TableRules.TurnSeconds`,
  varsayılan 90). Yalnızca çevrim içi oyunlarda geçerlidir: süre dolunca ev sahibi oyuncu adına en temkinli hamleyi yapar;
  açık artırma ve takas cevapları için en çok 30 sn tanınır (`TurnClock.LimitFor`). Süresiz seçildiğinde saat hiç görünmez
  ve ev sahibi kimsenin yerine hamle yapmaz. Son 10 saniyede ekranın dört kenarı kırmızı nabız gibi atar
  (`GameHud.EdgeWarningSeconds`). Kural oda verisiyle (`lazpoly_table`) ve kayıtla taşınır.
- **İflas edenin bankaya dönen mülkleri:** "Gelen alır" (varsayılan; mülkler bankada kalır, üstüne gelen liste
  fiyatından alır) ya da "Hemen açık artırmaya çıkar" (`BankReturns.Auction`; açık artırma açıkken seçilebilir). İkinci
  kuralda iflas anında dönen mülkler sıraya alınır (`BankAuctionsQueuedEvent`, `GameState.PendingAuctions`), kalan
  oyuncular sırayla teklif verir, kuyruk bitince sıra bir sonraki oyuncuya geçer. Kuyruk anlık görüntüye ve özete girer.
- **Hata günlüğü:** her oturumun `SessionTrail`'i son 20 komutu ve olay özetini tutar; ilk hata ya da istisna düştüğünde
  günlüğe otomatik yazılır. Duraklatma menüsü ve ayarlar yardım sekmesindeki **"Sorun Bildir"** düğmesi oyun modunu, masa
  kurallarını, her oyuncunun bağlantı durumunu, durum özetini ve izi `errors.log`'a "REPORT" kaydı olarak yazar ve klasörü
  açar. Günlük başlığında protokol ve kayıt biçimi sürümü de bulunur.
- **Sürüm uyuşmazlığı:** her üye protokol sürümünü oda üye verisine yazar (`lazpoly_proto`); ev sahibi oda verisine de yazar.
  Odada sürümü farklı üye "sürümü farklı" etiketiyle görünür, ev sahibi oyunu başlatamaz; misafir sürümü farklı odaya
  girince "Steam'den güncelle" uyarısı alır, hazır olamaz ve oyuna sürüklenmez. Protokol sürümü 8, kayıt biçimi 6
  (eski kayıtlar varsayılan zamanlamayla açılır).
- Testler: `EconomyTests` (banka açık artırma kuyruğu), `SnapshotAndChecksumTests` (kuyruğun özete girmesi),
  `MessageCodecTests` (masa zamanlaması, yeni olay), `LocalGameAndSaveTests` (kural kaydı, eski biçim),
  `NetworkSessionTests` (hamle süresi seçimi, saatsiz masa), `SessionTrailTests`, `ErrorLogTests` (rapor kaydı),
  `OnlineSetupTests` (sürümü farklı oda ve üye).

## Faz 10c-3: erişilebilirlik ve İngilizce

Renk görmeyen ya da fare kullanmayan biri de oynayabilsin; İngilizce oyuncu Türkçe sayı biçimi görmesin.

- **Renk körlüğü dostu renkler:** ayarlardaki "Erişilebilirlik" bölümünden açılır (`GameSettings.ColorBlind`). Açıkken renk
  grupları Paul Tol'un parlak paletini, oyuncular Okabe-Ito paletini alır (`ColorVision`) ve her grup harf taşır: tahtadaki
  şeritte, sol paneldeki mülk kutucuklarında ve kartların başlığındaki renk karesinde (A, B, C... tahta dosyasındaki sıra).
  Tahta ve piyonlar kurulurken boyandığı için seçim bir sonraki masada geçerli olur; ayar penceresi bunu söyler.
- **Klavyeyle tam oyun:** `UiFocus` (olay sistemine bağlı) Tab ve Shift+Tab ile üstteki pencerenin (pencere yoksa ekranın)
  düğmelerini okuma sırasıyla gezer (`FocusOrder`: satır satır, soldan sağa), Enter odaktaki düğmeye basar, odaktaki öğe
  turuncu halka giyer. Klavye kipi ilk Tab ile başlar, ilk fare tıklamasıyla biter; fareyle oynayan hiç halka görmez.
  Klavye kipinde bir pencere açılınca odak kendiliğinden birincil düğmeye (`UiPrimaryMark`) gider. Odakta düğme varken
  Space/Enter zar atmaz, düğmeye basar (`HotkeyContext.ButtonFocused`). Ana menüde Esc üstteki pencereyi kapatır.
- **Sayı biçimi:** `Lang.N` ve `F()` içindeki tam sayılar dile göre binlik ayraç alır: Türkçede 1.500, İngilizcede 1,500
  (`Lang.Numbers`, kültür verisine bağlı kalmamak için elle kurulur). Para, kira, fiyat ve servet her yerde aynı biçimde.
- **İngilizce:** tahtadaki kare yazıları dil değişince sahne yüklenmeden yeniden yazılır (`BoardView.Relabel`); özel
  karelerin adı da artık çevrilir. "town/city" ayrımı tekleştirildi (kasaba = town, şehir = city). Düğme yazıları gerekirse
  12 punto'ya kadar küçülür, taşmaz (`UiBuilder.ShrinkToFit`); `LangTests` kısa Türkçe metinlerin İngilizcesinin düğmeye
  sığacak uzunlukta kalmasını denetler.
- Testler: `AccessibilityTests` (paletler, grup harfleri, Tab sırası, odaktaki düğme ile Enter, pencere kapsamı, harfli
  kutucuk), `LangTests` (sayı biçimi, İngilizce uzunluk), `GameSettingsTests` (ayar kaydı).

## Faz 11a: izometrik kamera ve tahta plakası

Yeni görünümün ilk adımı (bkz. plan belgesindeki "Faz 11: yeni görünüm"): tahta artık masanın üstünde değil, açık
gökyüzünde duran bir plaka; kamera sabit ve izometrik.

- **Kamera:** ortografik, 35° eğik, Başlangıç köşesinden bakar (`BoardCameraRig.Pitch`, `BaseYaw`); tahta ekranda baklava
  biçiminde durur, Başlangıç altta. Yan paneller arasına sığacak büyüklük köşegenden hesaplanır
  (`BoardCameraFraming.OrthographicSize`); uzaklığın görüntüye etkisi yoktur. Tekerlek yakınlaştırır (`orthographicSize`
  ölçeklenir), Home sıfırlar, sabitlenen kare görünümü hafifçe çeker; sağ tuşla döndürme ve masa başı / kuş bakışı
  seçimi (C tuşu, duraklatma menüsündeki düğme) kalktı. Ekran görüntüsü ön ayarları (F11) artık köşe ve yakınlık seçer:
  Genel görünüm, Yakın plan, Karşı köşe, Sol köşe, Sağ köşe.
- **Plaka ve çim:** ahşap masa, deniz, ada, kayık ve fener kalktı (`SeaSurface`, `TownLife` silindi). Kareler açık renk bir
  plakanın kenarında durur (`BoardView.PlateThickness`, kenarı aşağıdan görünür); ortada çakıl halka, çim, çevresinde
  patika ve açık yeşil lekeler. Çimde büyük zar skoru ("6  4", ilk atıştan önce "–  –", `BoardView.ShowRoll`) ve altında
  kumbara satırı; başlık uzak tarafta yatar.
- **Yazılar kameraya göre:** plaka yazıları ve grup harfleri kenar boyunca yatar, harflerin tepesi kameradan soldan sağa
  okunacak yöne bakar (`BoardView.LabelRotation(outward, yaw)`); skor ve başlık ekrana dik yatar (`FlatFacing`). Ön ayar
  kamerayı başka köşeye çevirince `FaceCamera` hepsini yeniden döndürür.
- **Gökyüzü:** arka plan açık mavi, akşama doğru pembeye döner (`RoomDressing.SkyDay/SkyDusk`); ortam ışığı daha açık,
  gölgeler daha yumuşak, vinyet hafif. Menü arka planı da aynı açıdan bakar ve yavaşça nefes alır (`MenuBackdrop.BreathingSize`).
- Testler: `RenderingSetupTests` (plakanın alt köşeleri dahil köşeler ekranda ve ortalanmış), `SquareInfoTests` (kamera yeri,
  yakınlık sınırı), `SceneryTests` (dört köşeden okunurluk, skor metni, menü nefesi), `HotkeysTests` (C artık boş).

## Faz 11b: köşe panelleri ve yuvarlak düğmeler

Yan sütunlar kalktı; arayüz baklava biçimli tahtanın boş bıraktığı yerlere yerleşti.

- **Köşe kartları:** her oyuncunun kartı bir köşede (1-4. koltuk köşeler, 5-8. koltuk üstlerine dizilir; alt köşeler
  kenardan yukarı doğru). Kartta baş harfli yuvarlak avatar, ad, büyük para, bağlantı noktası ve gecikme, "Bilgisayar"
  etiketi, sırası olan kartta "düşünüyor..." / "oynuyor..." ve mülk kutucukları. Sıradaki kartın üstünde turuncu çizgi.
- **Sağ kenar düğmeleri (dock):** yuvarlak büyük düğmeler (`UiBuilder.RoundButton`, altında kısa yazı): Zar At / Sırayı
  Bitir (birincil), Bina (B), Mülkler, Takas (T), Olaylar, Yardım (H), Menü (Esc). İzin olmayan düğme kaybolmaz, grileşir.
  Kamera tahtayı yalnızca bu sütun kadar (`UiLayout.DockWidth`) kenardan uzak tutar; köşe kartları baklavanın yanındaki
  boş üçgenlerde durduğu için yer ayırtmaz.
- **Alt orta yığın:** ipucu, karar paneli ve olay şeridi üst üste. Karar paneli yalnızca bir şey beklenirken görünür
  (borç, yolculuk, Seyir Terası, açık artırma, takas yanıtı, bağlantı uyarıları, reddedilen hamle); zar ve sıra
  düğmeleri dock'ta, satın alma şehir kartında olduğu için boş anlarda gizlenir. Olay şeridi turu ve son olayı gösterir,
  tıklanınca tam olay kaydı pencerede açılır.
- **Pencereler:** Olaylar (kaydırmalı liste), Mülkler (`PropertyPanel`: ev kur, ev sat, mülk sat; borç kartından da
  açılır). Esc önce bunları kapatır.
- **Zar notu:** üstteki "4 + 1 = 5" kutusu kalktı (skor çimde); yalnızca çift ve üçüncü çift notu görünür.
- Testler: `UiBuilderTests` (yuvarlak düğme), `AccessibilityTests` (avatar baş harfi, Türkçe İ), `UiLayoutTests`
  (dock payı), PlayMode `InterfaceTests` düğmeleri yeni yazılarıyla basar.

## Faz 11c: dokulu kareler, karedeki adlar ve köşe yapıları

Tahta basılı bir oyun tahtası gibi: kareler dikdörtgen, köşeler büyük kare, adlar doğrudan karenin üstünde.

- **Düzen (`BoardLayout`):** kenar kareleri genişliğinin 1,4 katı derinlikte (`BoardView.DepthRatio`), köşeler bu
  derinlikte kare. Bir kenar yarım köşe + beş kare + yarım köşe olarak paylaşılır; `Width`, `Depth`, `SquareSize`,
  `IsCorner`, `Outward` ve `Cornerward` tahtanın ölçüsünü tek yerden verir. Dörde bölünmeyen kare sayısı eski gibi
  eşit karelerle dizilir. Tahtanın dış kenarı (`OuterHalf`) 11b'deki kadar kaldı, çim biraz küçüldü.
- **Basılı kareler:** her karenin gövdesi kendi renginin pastel tonu (`BoardView.Pastel`, kâğıt rengine doğru yıkanmış)
  ve üstünde paylaşılan bir kâğıt dokusu (`TileSkin`: gren + kenardan biraz içeride ince çerçeve çizgisi; 128 px,
  kodla üretilir); kareler mat (parlaklık 0,05) ki güneş ortalarını beyaza boğmasın. `MeshFactory.RoundedBox` artık üstten bakılmış doku koordinatları taşır; `ModelParts.Skin` dokuyu
  parçanın kendi malzemesine koyar. Grup rengi dış kenardaki bantta tam renk kalır.
- **Karede ad ve fiyat:** koyu plaka kalktı. Ad mürekkep renginde, kare genişliğine sığacak kadar büyük (`NameSize`,
  `NameFill`, `NameDepth`); altında daha küçük fiyat / vergi / bilet satırı (`Subtitle`). Kare üstündeki şerit sırası
  iç kenardan dışa: bina sırası ve çadır (`BuildingOffset`), piyonlar (`PawnOffset`), ad (`NameOffset`), fiyat
  (`PriceOffset`), bant (`BandOffset`). Kamera 35 derece eğik baktığı için bir parçanın boyu ekranda arkasındaki zemine
  düşer; kameradan uzağa bakan sıralarda bu zemin adın olduğu dış kısımdır. Piyon alanı iç kısımda ve addan uzak
  tutulduğu, piyon yuvaları da kareyi içten dışa doldurduğu için (`PawnView.Target`, yuva ızgarası kareye göre döner)
  piyonlar adı kapatmaz; yalnız aynı karede yedi ve daha çok piyon toplanınca son sıra adın alt kenarına yaklaşır.
- **Köşe sahneleri (`CornerParts`):** oyunun kendi parçalarından kurulu küçük sahneler. Kameraya bakan köşelerde
  (Başlangıç, Çay Molası) sahne köşenin iç yarısında, piyonlar yanında, ad dış kenarda; kameradan uzağa bakan köşelerde
  (Sokak Haberi, Otogar) sahne ve piyonlar dış yarıda, ad iç kenarda (`BoardView.FacesAway`): böylece yapıların boyu
  adın değil gökyüzünün üstüne düşer. Başlangıç'ta iskele ve fener (yanında kayık), Çay Molası'nda şemsiyeli çay bahçesi
  (masa, tabureler, semaver, çay bardağı), Sokak Haberi'nde gazete büfesi (tezgâhı kameraya dönük), Otogar'da dolmuş
  durağı (tente, bank, cam arkalık; minibüs durağın önünde). Köşelerdeki eski küçük tabelalar kalktı; Seyir Terası'nın
  dürbünü yerinde. Sahneler kodla üretildiği için kaynak notu gerektirmez; istenirse çadır gibi dış model de konabilir.
- Testler: `BoardLayoutTests` (derin kareler, köşeler, dörde bölünmeyen sayı), `SceneryTests` (şerit sırası ve köşe
  sahnesinin yerleşimi), `ModelTests` (doku koordinatları, kâğıt dokusu, sahnelerin karesine sığması).

## Faz 11d: çimenlik süslemeleri, kumbara ve konuşma balonları

Tahtanın ortasındaki park artık boş değil ve bilgisayar oyuncular kararlarını söylüyor.

- **Çimenlik süslemeleri** (`LawnParts`, `LawnDressing`): yolun dışındaki köşelerde üç çeşit ağaç (yuvarlak taç, çam, çift taç) ve alçak çalılar; yolun kameraya bakan iki kenarı boyunca üç direk arasına gerilmiş flama ipleri (kırmızı, beyaz, deniz mavisi, turuncu). Kamera 35° eğik baktığından bir parçanın yüksekliği ekranda yukarıdaki zemine düşer; her ağaç tacının çimenlikte kaldığı, karelerin adlarına ulaşmadığı yere dikildi, ekranın tepesindeki köşede (başlığın yanında) yalnız çalılar var. `LawnDressing.GroundUnderTop` bunu hesaplar, test de aynı kuralla her noktayı dener.
- **Kumbara** (`PotPile`): çay kumbarası yazısının altında cam bir kavanoz durur; kumbara büyüdükçe dibine 50'şerlik bozuk paralar yığılır (altışarlık beş yığın, sonra durur), ödeme çıkınca paralar gider.
- **Konuşma balonları** (`SpeechBubble`): bilgisayar oyuncunun piyonunun üstünde, kameraya dönük kâğıt bir balon, kuyruğu oyuncunun renginde; 2,6 saniye sonra küçülerek gider, yeni bir söz eskisinin yerini alır. Sözler: satın alınca "Aldım!", kareyi bırakınca "Almıyorum, artırmaya.", teklifte "Artırıyorum!", çekilince "Ben yokum.", artırmayı kazanınca "Benim oldu!", inşaatta "Bir ev daha!" / "Otel vakti!", takas teklifinde "Takas edelim mi?", yanıtında "Anlaştık!" / "Olmaz.", çift zarda "Çift!", iflasta "Battım...". Masadaki insanlar konuşmaz; ne yaptıklarını biliyorlar. Olay akışından tetiklenir: `GameSceneController.Say` yalnız `IsAiPlaying` olan oyuncular için balon açar; hangi sözün söyleneceği gösterilen evreye (`PhaseChangedEvent`), son hareket edene ve açık takas teklifine bakılarak seçilir.
- **Karar paneli açılınca kamera yer açar**: alt panel (satın alma, artırma, takas yanıtı) Başlangıç köşesini örtüyordu. `GameHud.ReservedBottomPixels` panel açıkken alt yığının yüksekliğini bildirir; `BoardCameraFraming.OrthographicSize` artık alttan ayrılan pikselleri de hesaba katar ve `AimAbovePanel` bakış noktasını kameranın kendi yukarı yönünde panelin yarısı kadar aşağı alır, böylece tahta panelin üstünde kalan alanın ortasına oturur. Yükseklik 40 piksellik adımlara yuvarlanır ki paneldeki bir satır fazlası kamerayı kıpırdatmasın; kamera rig'i yeni çerçeveye birkaç karede kayar.
- **Baskı gölgesi** (`PrintMaterial`, `Resources/Lazpoly/Shaders/Print.shader`): Unity'nin yazı gölgelendiricisi her şeyin üstünden çizer (derinlik testi yok); kare adları balonun kâğıdının üstünden okunuyordu. Tahtaya basılı yazılar (ad, fiyat, grup harfi, skor, başlık) artık derinlik testi yapan kendi gölgelendiricisiyle çizilir: üstünde duran piyon ya da bina gerçek bir tahtadaki gibi yazıyı örter. Dinamik yazı tipi atlası yenilendiğinde malzeme yeni dokuyu alır (`Font.textureRebuilt`). Yükselen "+200" ve balon satırı eskisi gibi her şeyin üstünden çizilir.
- **Baş payı**: kamera çerçevesi artık uzak köşedeki yapıya (büfe, otobüs durağı) `BoardCameraFraming.Headroom` kadar yer bırakır ve bakış noktası plaka kenarı ile baş payının ortasına alınır; panel açıkken büfenin çatısı ekranın üst kenarında kesilmez.
- **Aynı karede tek balon**: bir karedeki piyonlar art arda konuşunca (açık artırma) balonlar üst üste biniyordu; yeni balon aynı karedeki eskisini kapatır, satırların hepsi olay kaydında durur.
- **Çay bahçesi ve köşe piyonları**: şemsiye masanın ekranda sağına (piyonlardan uzak, çimenliğe bakan yana) alındı; yüksekliği ekranda yukarı düştüğü için gölgesi masanın değil çimenliğin üstünde görünür, tabureler buna göre döndü; köşe sahneleri biraz küçüldü (`SceneSize` 0,40) ve köşeye doğru kaydı (`SceneOffset` 0,29), köşedeki piyonlar da sahneden uzağa (`CornerPawnShift` 0,25); dokuz kişilik piyon ızgarasının sahneye en yakın yuvası bile şemsiyenin gölgesinin ekranda düştüğü yerin dışında kalır.
- **Büyük ZAR AT tuşu** (`GameHud.BuildRollPrompt`): sıra bu ekrandaki kişinin zarına gelince tahtanın ortasının biraz altında büyük bir tuş belirir (açılırken küçükten büyüyerek), basılınca ya da zar atılınca kaybolur; bir pencere açıkken görünmez. Dock'taki zar düğmesi ve Boşluk tuşu da atmaya devam eder. Boyut ve yer `UiLayout.RollPrompt*` sabitlerinde, 1280×720'de köşe kartlarına ve alt yığına değmediği testle kontrol edilir.
- **Testler**: ağaç/çalı/direk/kavanoz boyutları ve flamaların ipten sarkması (`ModelTests`), paraların yığılması, süslemelerin ekranda yukarı düşen tepelerinin çimenlikte kalması, kumbaranın para alıp vermesi, balonun satırını kâğıda sarması, baskı malzemesinin derlenip yazı tipinin atlasını kullanması (`SceneryTests`), alt panel varken tahtanın panelin üstündeki alana ortalanması ve sığması, baş payıyla birlikte (`RenderingSetupTests`).

## Faz 11e: mülk kartı

Şehre gelince açılan satın alma kartı ve kendi şehrinde açılan bina kartı artık aynı kart: referanstaki düzende, seviye kutucuklu.

- **Kart düzeni** (`PropertyCard`): üstte grubun renginde bir başlık bandı (şehrin adı, solda grup harfi, sağda × kapatma); altında yan yana altı kutucuk: Arsa, Ev 1-4, Otel. Her kutucukta o seviyenin resmi, sağ altında onay kutusu, adı ve o seviyedeki kira. Seçilen seviyeye kadar olan kutucuklar işaretli ve hafif sarı; seçilebilenler tıklanınca seçilir. Altında seçilen seviyenin kirası ("Kira: 150"), paran ve kalan para; en altta solda "Devam Et" / "Şimdi Değil", sağda toplam tutarı yazan büyük tuş ("Satın Al (110)" / "Kur (100)"). Bina almayan bir karede (liman gibi) yalnız Arsa kutucuğu var.
- **Kurallar aynı, kart kuralı gösterir**: kuralların bu gelişte izin vermediği ya da paranın yetmediği seviyeler gri, resminin üstünde çapraz bir damga: "İlk turdan sonra" (Başlangıç'tan geçmeden tek ev), "Bu gelişte en çok 2", "4 evle yeniden gelince" (otel bir sonraki gelişte), "Yeniden gelince" (dördüncü ev bu gelişte kurulduysa), "Para yetmez". Önce kural, sonra para. Satın alırken arsayla birlikte ilk gelişin izin verdiği evler de seçilebilir: kart önce `BuyPropertyCommand`, sonra ev başına bir `BuildCommand` gönderir; ev sahibi her birini sırayla doğrular. `GameRules.BuildableAfterPurchase` satın alma sonrası kurulabilecek ev sayısını söyler (ilk turda 1, sonra 4); `PropertyChoice` seviye, izin, bütçe, tutar, kira ve damga metnini hesaplar, Unity'siz test edilir.
- **Seviye resimleri** (`BuildingPreview`): tahtanın çok altında (y = −300) bir sahne: kâğıt dokulu bir kare, oyunun kendi ev ve konak modelleri; kendi dik kamerası tahtanın açısından (35°, −45°) her seviyeyi bir kez 256×256'lık dokuya çizer (`RenderPipeline.SubmitRenderRequest`), kart her açıldığında değil, kare rengi değişince yeniden. Kamera kapalı durur, ana kameranın kırpma düzlemlerinin dışındadır. Resim çizilemezse (editör, istek desteklenmeyen boru hattı) kutucukta simge görünür.
- **Pencere genişliği**: altı kutucuk için `UiLayout.PropertyCardWidth` 880; 1280×720'de büyük arayüzde bile sığdığı testle kontrol edilir. Satın alma kartı kapanınca bina kartı aynı geliş için kendiliğinden açılmaz (dock'taki Bina düğmesi açar).
- **Kare kartı yeniden** (`SquareCard`): karenin bilgi kartı artık fareyle üstüne gelince değil, kareye tıklayınca açılır ve ekranın ortasında durur; ×, Esc, "Kapat" ya da karartılmış tahtaya tıklama kapatır. Üstte grubun renginde başlık bandı; solda karenin şu anki hâlinin resmi (kâğıt kare ve üstündeki evler/otel, `BuildingPreview`'dan), yanında sahibi (renk noktası ve ad), fiyat, ev maliyeti, satış değeri ve bina durumu; altında kira tablosu (Arsa, Rengin tamamı, 1-4 ev, Otel) ve şu an geçerli satır sarı şeritte; sonra aynı rengin kareleri yan yana, her birinde sahibinin rengi ve adı (birine tıklayınca onun kartı açılır); en altta notlar. Özel kareler (Başlangıç, vergi, Sokak Haberi, kumbara, otogar, seyir, dinlenme) simge, kendi bilgileri ve açıklamasıyla. Köşe kartındaki mülk renklerine tıklamak da kartı açar (`SquareClickTarget`); fareyle üstüne gelmek artık kart açmaz, yalnız o rengin karelerini biraz aydınlatır; öteki kareler kararmaz. Otogar'da gidilecek kare seçilirken tıklama kartı değil hedefi seçer. `SquareInfo` satırları artık kira satırlarını `Rent` ile ayırır ve tablo başlıkları kısaldı (Arsa, Rengin tamamı, {n} ev, Otel).
- **Testler**: `PropertyChoiceTests` (ilk turda arsa + bir ev, para damgası, ilk turdan sonra dört ev, grup tamamlanınca arsa kirası, kendi şehrinde üst seviyeler, otelin yeni gelişi, binasız kare, kutucuk adları), `SceneryTests` (sahnenin yeri, kameranın açısı, her seviyede sahnedeki parçalar, editörde resim çizilmemesi), `UiLayoutTests` (kutucukların pencereye, pencerenin ve kare kartının ekranlara sığması), `SquareInfoTests` (kira satırları ve yeni başlıklar).

## Faz 11f: karakter piyonlar

Piyonlar artık kasabanın eşyaları değil, bir kişi: proje sahibinin verdiği iş adamı modeli, ceketi oyuncunun renginde.

- **Model** (`Tools/Art/Models/BusinessMan`): 2 m boyunda, 13.578 üçgenli, 112 adlı parçalı, 30 düz renkli malzemeli, iskeletsiz bir karakter (kaynağı ve kullanım hakkı `KAYNAK.md`'de). `Tools/Art/make_character.py` onu Y-yukarı çevirir, ayaklarını ortalayıp tabanını y = 0'a koyar, boyunu bir birime ölçekler, parçaları malzemeye göre birleştirir (112 yerine 30 çizim parçası) ve her yüze kendi düz normalini verir (modelde normal yok; kutu ve kama yüzeylerinde yumuşak normal erimiş görünürdü). Çıktı `Assets/_Lazpoly/Data/Resources/Lazpoly/Models/business_man.txt`, çadır gibi `ObjModel` ile okunur.
- **Boyama** (`CharacterParts`): her malzeme kendi `MeshRenderer`'ı ve malzemesiyle kurulur; ceketin dört malzemesi oyuncunun rengini alır: gövde rengin kendisi, kol ve kıvrımlar (`jacket_light`) %18 beyaza, cepler (`jacket_dark`) %30 siyaha, yakalar (`lapel`) %14 siyaha doğru. Ten, saç, sakal, gömlek, kot, ayakkabı ve tablet `BusinessMan.mtl` paletindeki renklerini korur; gözler, kemer tokası ve ekran biraz parlak.
- **Piyon** (`PawnView`): kök nesnenin altında `Model` adlı çocuk, karakter `CharacterHeight` (1,8; eşyalar 0,55'ti; 0,7 ve 0,9 denendi, proje sahibi iki katını istedi) boyunda ve kameraya dönük (`PawnView.Facing(yaw)`); kamera başka köşeden bakınca (ekran görüntüsü açıları) `FaceCamera` ile döner. Zıplama, yürüme, otogar atlayışı, sırada hoplama, iflasta devrilme aynen; ezilme/uzama kökte uygulandığından modelin ölçeği bozulmaz. Model yoksa (`business_man.txt` silinmişse) eski eşya piyonlar kurulur; `IsCharacter` hangisi olduğunu söyler. Konuşma balonu başın üstüne çıkacak kadar yükseldi (`SpeechBubble.Lift` 2,05). Ana menünün arka planındaki piyonlar da karakter.
- **Steam**: yapay zekâ içerik beyanı tablosuna eklendi (`Steam/README.md` §6).
- **Testler** (`CharacterPartsTests`): varlık bir birim boyunda, ayakları ortada ve tabanda, yüzü +Z'ye (tablet önde), her malzeme tek grup ve palette, her yüzün normali var; karakter kurulunca yalnız ceket boyanır ve açık/koyu tonlar sırayla; piyon her kamera açısında kameraya bakar ve balon başın üstünde.

## Faz 11g: ana menü, lobi ve logo

Proje sahibinin verdiği menü / lobi tasarımının düzeni ve tarzı kodla kuruldu; logo oyuna girdi. Tema ve özellikler bizim: kartlar oyunun gerçek yollarına bağlı, sahte veri yok (tasarım paketindeki demo projesi neden alınmadı: `Tools/Art/Design/Menu/KAYNAK.md`).

- **Kağıt paleti** (`UiPalette`): tema artık bir palet nesnesinden okunur; `UiTheme.Use(palet)` ile değişir. Ana menü `Paper` (krem kağıt, koyu yeşil mürekkep, orman yeşili başlık bandı, yaprak yeşili ana düğme, koyu mercan çıkış, deniz mavisi seçim) kullanır, oyun sahnesi `Dusk` (eski renkler) ile açılır. `UiPalette.Ink` tasarımın kendi renklerini (başlık bandı, altın, altı kart şeridi) ve `Wash` / `Deep` tonlarını verir; her mürekkep kendi zemininde en az 4,5:1 kontrastlı (`MenuTests`).
- **Yeni parçalar** (`UiBuilder`): `Eyebrow` (büyük harfli kısa üst satır), `Chip` (hap zeminli etiket; `UiSprites.Pill`), büyük harf için `Lang.Upper` (Türkçede noktalı İ korunur: "ÇEVRİM İÇİ").
- **Ana menü** (`MainMenuController`, ölçüler `MenuLayout`): tahtanın üstünde 40 px gök boşluklu, köşeleri maskeli kağıt pencere. Üstte 128 px yeşil bant: solda 116 px logo ve KARADENİZ MASASI (logo yoksa altın "L" ve yazı), Steam chip'i (açıksa çevrim içi arkadaş sayısı, değilse "Steam kapalı"), sürüm, Ayarlar ve Çıkış yuvarlak düğmeleri. Altında başlık şeridi (karşılama ve bir satır) ve 3x2 kart: Özel Masa (oda kurar), Odaya Katıl, Tek Oyunculu, Aynı Bilgisayarda, Masa Kuralları (pencere), Nasıl Oynanır (pencere). Her kartın sol kenarında kendi renginde şerit, başlık, açıklama, chip, simge diski ve "›". Alt bantta slogan ve büyük yeşil düğme: kayıtlı oyun varsa "Kayıtlı Oyuna Devam →", kopmuş oda varsa "Odaya Geri Dön →" (+ Unut), yoksa "Masaya Otur →" (seçili rakip sayısı ve zorlukla hemen tek oyunculu). Bozuk kayıt uyarısı da alt bantta. Tek oyunculu, aynı bilgisayar ve odaya katılma kurulumları pencerenin ortasında 860 px'lik bir sayfada (`< Geri` ile dönülür); masa kuralları formu `TableRulesForm`'a çıkarıldı (menü penceresi ve lobi aynı formu kullanır).
- **Lobi** (`LobbyPanel`): odadayken pencereyi üç sütun doldurur. Masa ayarları (ev sahibi seçer, konuk "salt okunur" görür; chip'li form), koltuklar (2x4 kart: durum bandı Ev sahibi / Hazır / Bekliyor / Sürümü farklı, baş harfli avatar, ad, "Senin koltuğun", piyon rengi; boş koltuklarda "Davet Et"), altında oda kodu çubuğu ve Kopyala; arkadaşlar sütununda "Steam ile Davet Et", çevrim içi arkadaşlar ve tek tek Davet. Alt bant: durum satırı ("3/4 hazır", "Herkes hazır."), Odadan Çık ve büyük "Oyunu Başlat →" (ev sahibi) ya da "Hazırım →" / "Hazırsın ✓" (konuk). Oda dışında katılma sayfası: Oda Kur, oda koduyla katıl, odası açık arkadaşlar.
- **Logo** (`Tools/Art/make_logo.py`, `MenuArt`): verilen JPG'deki (ChatGPT) sigaralı portre proje sahibinin isteğiyle kaldırıldı: madalyonun içi logonun mavilerinde iki tonlu disk ve beyaz "L" ile boyanır; sonra beyaz zemin şeffaflaştırılır, resmin kendi yumuşak gölgesi (tahtanın altındaki gri) harflerin altından itibaren ikinci bir doldurmayla silinir ve 640x640 PNG olarak `Resources/Lazpoly/Art/logo.bytes`'a yazılır. Çalışırken `Texture2D.LoadImage` ile açılır, dosya yoksa menü ve tahta yazıyla devam eder. Ayrıntı `Tools/Art/Logo/KAYNAK.md`; Steam beyan tablosu `Steam/README.md` §6.
- **Tahtadaki logo** (`PicturePlate`, `Lazpoly/Picture` gölgelendiricisi): çimenliğin uzak yarısındaki "LAZPOLY" yazısının yerinde logo yatıyor (`BoardView.TitleSize` 0,30 çimen genişliği, `TitleOffset` 0,40). Gölgelendirici `Print` gibi derinlik testi yapar: üstüne gelen piyon ya da zar logoyu örter. Dörtgen `PicturePlate.Quad` ile kurulur, kameraya göre `FlatFacing` ile döner.
- **Açılış ekranı** (`BootSplash`): ilk sahne yüklenmeden önce kurulur (sahne listesine girmez): krem zemin, ortada 420 px logo, KARADENİZ MASASI, dolan çubuk ve "Masa kuruluyor...". En az 1,6 sn durur (`HoldSeconds`; hızlı makinede göz kırpması gibi geçmesin), ilk sahne hazır olunca 0,5 sn'de solar ve silinir. Fareyi almaz (tıklama altındaki menüye geçer; testler engellenmez), `-perf` koşusunda ve logo yokken kurulmaz.
- **Unity açılış ekranı kapalı, exe simgesi logo** (`ProjectSettings.asset`): Unity 6 ücretsiz lisansta da "Made with Unity" ekranı kapatılabiliyor (`m_ShowUnitySplashScreen: 0`). Varsayılan simge `Assets/_Lazpoly/Art/Textures/lazpoly-icon.png` (256x256, `make_logo.py` üretir); masaüstü oyuncusunda 256 ve 128 tam logo, 48/32/16 ise yalnız madalyon (`lazpoly-icon-small.png`, 64x64: altın halka, lacivert disk, beyaz L) çünkü tam logo o boyda leke olur. Lazpoly.exe, görev çubuğu ve pencere başlığında böyle görünür. Ayarlar silinirse editörde `Lazpoly/Apply Branding` (ya da `-executeMethod Lazpoly.Editor.Branding.Apply`) yeniden yazar.
- **Testler** (`MenuTests`): kağıt mürekkebi her kağıt zeminde ve bant / chip renkleri kendi zeminlerinde okunur; tema verilen paleti alır; ana sayfa 1080 satıra sığar (`MinScreenHeight`) ve kart yüksekliği en küçük ekranda tam en az değer; logo yapıda, 640x640, köşeleri şeffaf, ortası boyalı, madalyon mavi (portre yok), alt gölge silinmiş; açılış ekranı en az 1 sn durur ve çubuğu zamanla dolar; Unity açılış ekranı kapalı ve varsayılan simge 256x256 logo (ProjectSettings ile simge meta'sının guid'i eşleşir); Türkçe büyük harfte noktalı İ. PlayMode `InterfaceTests` yeni menüde "Tek Oyunculu" kartına ve "Oyunu Başlat" düğmesine fareyle ulaşır.

## Faz 12a: oyun ekranı kağıt tarzında

Masa da menünün kağıt görünümüne geçti (`GameSceneController.Start` → `UiTheme.Use(UiPalette.Paper)`; `UiTheme`'in varsayılan paleti artık `Paper`, `Dusk` ikinci palet olarak duruyor).

- Köşe oyuncu kartları, dock, alt yığın, pencereler ve olay satırı krem kağıt ve koyu yeşil mürekkeple; sırası gelen oyuncunun kartı bant yeşilinin açık bir tonunda (`Ink.Wash(Masthead, 0.74)`).
- "Sıra sende" şeridi oyuncunun renginde, yazısı o renkte okunan mürekkeple (`ColorVision.TextOn`); çift atınca çıkan not yeşil bantta krem yazı (üçüncü çiftte altın).
- İpucu kutusu kağıt kart, borç kartı uyarı tonunda (`NoticeFill`); eski koyu lacivert kutular kalmadı.
- Test (`MenuTests.TheTableUsesPaperToo`): oyun sahnesi kağıt paletini seçer, HUD'da eski koyu kutu rengi kalmadı, sıradaki oyuncunun kartı krem kartlardan ayrılır ve mürekkep üstünde okunur.

## Kurallar

Modüler sistemler, tek sorumluluk, durum değişikliklerini yalnızca host doğrular, her aşama test edilir,
gereksiz paket yok, onay olmadan sonraki faza geçilmez.
