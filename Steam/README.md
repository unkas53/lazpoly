# Steam mağaza kurulumu (Faz 6d)

Bu klasör Unity projesinin dışında durur; içindekiler Steamworks iş ortağı sitesine ve SteamPipe'a yüklenir.
Gerçek App ID gelmeden yapılabilecek her şey hazır; aşağıdaki **App ID gerekli** adımları ondan sonra yapılır.

## 1. App ID (App ID gerekli)

1. Steam Direct ücretini öde, Steamworks'te Lazpoly uygulamasını aç; App ID ve Windows depot numarasını not al.
2. `Assets/_Lazpoly/Code/Platform/Steam/SteamSettings.cs` içinde `AppId`'yi yeni numara yap.
3. Proje kökündeki `steam_appid.txt` dosyasına aynı numarayı yaz (yalnızca geliştirme içindir, mağaza derlemesine girmez).
4. `app_build.vdf` ve `depot_build_windows.vdf` içindeki `APP_ID` ve `DEPOT_ID` yerlerine numaraları yaz.

## 2. Başarımlar

Steamworks > Stats & Achievements > Achievements. API adları koddakiyle birebir aynı olmalı
(`Assets/_Lazpoly/Code/Application/Achievements/AchievementCatalog.cs`):

| API adı | Ad | Açıklama |
|---|---|---|
| LAZ_FIRST_WIN | İlk Zafer | Bir oyunu kazan. |
| LAZ_ONLINE_WIN | Masanın Hâkimi | Çevrim içi bir oyunu kazan. |
| LAZ_BEAT_COMPUTERS | Makineye Karşı | Bilgisayar oyuncularının da olduğu bir oyunu kazan. |
| LAZ_FULL_GROUP | Tam Takım | Bir renk grubunun bütün mülklerine sahip ol. |
| LAZ_FIRST_HOTEL | Otelci | İlk otelini kur. |
| LAZ_TRADE_DONE | Pazarlıkçı | Bir takası tamamla. |
| LAZ_BANKRUPT_RIVAL | Alacaklı | Bir rakibi sana olan borcu yüzünden iflas ettir. |
| LAZ_TYCOON | Kodaman | Aynı anda 6M paraya sahip ol. |

Her biri için kilitli ve açık simge (64x64 JPG) gerekir; simgeler henüz yok. Kaydedip **Publish** et.
`SteamPartnerFilesTests` tablodaki adların kodla aynı kaldığını denetler.

## 3. Rich Presence

Steamworks > Community > Rich Presence: `rich_presence.vdf` (Türkçe) ve `rich_presence_english.vdf` dosyalarını yükle.
Oyun `status` anahtarına durum satırını, `steam_display` anahtarına `#Status` yazar; arkadaş listesi bunu
"Bilgisayarlara karşı, 12. tur, 4 oyuncu" gibi gösterir. Test App ID'sinde (480) `steam_display` yazılmaz, çünkü
o uygulamanın jetonları Lazpoly'nin değil.

## 4. Mağaza derlemesi

Unity menüsü `Lazpoly > Build Store Version` (veya
`-batchmode -quit -executeMethod Lazpoly.Editor.StoreBuild.Run`) `Builds/Store/windows/` içine geliştirme
seçenekleri kapalı bir Win64 derlemesi alır. App ID hâlâ 480 ise durur; bu durumda deneme derlemesi için
`-allowDevelopmentAppId` ekle (bu derleme yüklenmemeli).

## 5. Yükleme (App ID gerekli)

```
steamcmd +login <hesap> +run_app_build "C:\...\lazpoly\Steam\app_build.vdf" +quit
```

Ardından Steamworks > SteamPipe > Builds'te derlemeyi bir dala (önce `beta`) al. Kapalı beta için anahtarları
Steamworks > Request Steam Product Keys'ten iste ya da Playtest uygulamasını aç.

## 5. Mağaza görselleri (Faz 7d)

`Steam/Art/` içindeki görseller `python Tools/Art/make_capsules.py` ile üretilir (Pillow gerekir; dışarıdan resim kullanılmaz).
Steamworks > Store Presence > Graphical Assets bölümüne yüklenecek dosyalar:

| Dosya | Boyut | Yer |
|---|---|---|
| header_capsule.png | 460×215 | Header Capsule |
| small_capsule.png | 231×87 | Small Capsule |
| main_capsule.png | 616×353 | Main Capsule |
| library_capsule.png | 600×900 | Library Capsule |
| library_hero.png | 3840×1240 | Library Hero |
| library_logo.png | 1280×720, saydam | Library Logo |
| page_background.png | 1438×810 | Page Background |
| logo_1024.png / logo_256.png | kare | Uygulama simgesi (Unity Player Settings > Icon) ve Steam istemci simgesi |

Vitrin ekran görüntüleri (en az 5 adet, 1920×1080) oyunun kendisinden alınır; henüz yok.

## 6. İçerik beyanı (yapay zekâ)

Steamworks mağaza başvurusundaki içerik anketi, yapay zekâyla üretilmiş içeriğin beyan edilmesini ister. Bu oyunda
beyan edilecek içerik:

| İçerik | Nerede | Not |
|---|---|---|
| Bina modelleri (arsa, Ev 1-4, otel; sahipli şehir işareti ve binalar) | `Tools/Art/Models/Buildings`, oyunda `buildings.bytes` | Proje sahibi ChatGPT ile üretti (2026-11-04); ayrıntı `KAYNAK.md` |
| Logo (ana menü bandı, tahtanın çimenliği, açılış ekranı, exe simgesi) | `Tools/Art/Logo`, oyunda `logo.bytes` | Proje sahibi ChatGPT ile üretti; kaynaktaki portre oyun kopyasında boyanarak kaldırıldı (`KAYNAK.md`) |
| Ana menü / lobi konsept ekranları | `Tools/Art/Design/Menu` | Yalnız başvuru (ChatGPT); oyunda yalnızca düzen ve tarz kodla kuruldu, görsel kullanılmadı |
| Ana menü kiti: şehir arka planı, geniş logo | oyunda `Resources/Lazpoly/Art/Menu` (`Tools/Art/make_menu_art.py`) | Proje sahibinin kiti (2026-11-05): arka plan Higgsfield (nano_banana_2_1) ile üretildi; düğme, panel ve simgeler kitin kendi çizimi (SVG). Kitin karakter portreleri Faz 21'de oyundan çıktı |
| Oyuncu kartı kiti: kart zeminleri (normal, sırada, iflas), çerçeve, şerit, para, ev ve süre çizgisi | oyunda `Resources/Lazpoly/Art/Menu` (`Tools/Art/make_menu_art.py`) | Proje sahibinin kiti (2026-11-05): kart parçaları kitin kendi çizimi (SVG) |
| Karakterler (piyonlar): sekiz dokulu 3B figür (takım elbiseli, bastonlu, turkuaz takımlı, eli belinde, lacivert takımlı, mor ceketli, purolu patron, kürklü kadın) ve onlardan çizilen karakter resimleri | oyunda `Resources/Lazpoly/Models/figure_*` (`Tools/Art/make_figures.py`), resimler `Resources/Lazpoly/Art/Menu/Figure_*`, `Face_*` (`Tools/Art/make_figure_portraits.py`) | Proje sahibi Meshy AI ile resimden üretti (2026-11-05/06); oyun için sadeleştirildi ve dokusu küçültüldü; resimler figürlerden Blender ile çizildi |
| Otogar köşesi: çizgi film tarzı otobüs durağı dioraması (sarı "City Bus", sundurma, bank, durak tabelası) | oyunda `Resources/Lazpoly/Models/figure_station*` (`Tools/Art/make_corner_models.py`) | Proje sahibi Meshy AI ile resimden üretti (2026-11-06, ilk otobüs dioramasının yerine); oyun için sadeleştirildi ve dokusu küçültüldü |
| Şampiyonlar Kupası: mat altın kupa ("LazPoly CHAMPIONS" yazılı), kupanın şehrinde | oyunda `Resources/Lazpoly/Models/figure_trophy*` (`Tools/Art/make_figures.py`) | Proje sahibi Meshy AI ile resimden üretti (2026-11-07); oyun için sadeleştirildi ve dokusu küçültüldü |
| Uçan para: bantlı para destesi ve altın külçe | `Tools/Art/Models/Figures/CleanBandMoneyStack.glb`, `CleanGoldBar.glb` (git dışı), oyunda `Resources/Lazpoly/Models/figure_moneystack*`, `figure_goldbar*` (`Tools/Art/make_corner_models.py`) | Proje sahibi Meshy AI ile görüntüden 3B üretti (2026-11-07); önceki dolar destesi ve yapay zekâ kodlu para seti kaldırıldı |
| Şampiyonlar Kupası köşesi: çizgi film tarzı stadyum dioraması (tribün, ışık direkleri, ağaçlar) | oyunda `Resources/Lazpoly/Models/figure_stadium*` (`Tools/Art/make_corner_models.py`) | Proje sahibi Meshy AI ile resimden üretti (2026-11-07, köşedeki kupanın yerine); oyun için sadeleştirildi ve dokusu küçültüldü |

Ses efektleri yapay zekâ ürünü değildir: Kenney'nin CC0 kayıtlarından kurulur (`Tools/Audio/Kenney/KAYNAK.md`), beyan gerekmez.

Başvurudan önce üretim araçlarının (ChatGPT) kullanım koşullarının ticari kullanıma izin verdiği kontrol
edilir. Listeye yeni üretilmiş içerik eklendikçe bu tablo güncellenir. Kamp çadırı modeli (ChatGPT + Higgsfield) Faz 16a'da oyundan çıkarıldı; yerini arsa modeli aldı, beyan gerekmez.
