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

- **Kare kartı:** Fareyle bir karenin üstüne gelince kartı açılır; tıklayınca sabitlenir, yeniden tıklayınca kapanır.
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

## Kurallar

Modüler sistemler, tek sorumluluk, durum değişikliklerini yalnızca host doğrular, her aşama test edilir,
gereksiz paket yok, onay olmadan sonraki faza geçilmez.
