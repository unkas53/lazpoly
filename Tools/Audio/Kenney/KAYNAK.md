# Kenney ses kayıtları

Oyundaki ses efektleri (`Assets/_Lazpoly/Audio/Resources/Lazpoly/Sfx/*.wav`) bu klasördeki kayıtlardan
`Tools/Audio/make_sfx_kenney.py` ile üretilir. Kayıtlar Kenney'nin (kenney.nl) ücretsiz paketlerinden alındı:
Casino Audio, Impact Sounds, Interface Sounds, RPG Audio, Music Jingles, UI Audio (2026-11-03'te kenney.nl'den
indirildi) ve Sci-fi Sounds (2026-11-21, aşağıda); yalnız oyunun kullandığı dosyalar tutuluyor.

Lisans: CC0 1.0 (kamu malı). Ticari oyunda kullanılabilir, ad vermek zorunlu değil; her paketin `License.txt`
dosyası klasöründe. Kayıtlar insan yapımıdır, yapay zekâ ile üretilmedi: Steam içerik beyanına girmez.
Teşekkür olarak oyunun künyesinde "Ses: Kenney (kenney.nl)" yazılabilir.

Yeni bir ses eklemek için: kaydı ilgili paket klasörüne koy, betikteki `RECIPES` tablosuna ekle, betiği çalıştır.

Otobüsün dört sesi (`bus_start`, `bus_rev`, `bus_horn`, `bus_brake`) bu kayıtlardan değildir: Kenney paketlerinde
otobüs sesi yok, o yüzden `Tools/Audio/make_sfx_bus.py` onları hesaplayarak üretir (2026-11-11). Gerçek kayıtla
değiştirilecekse CC0 lisanslı kayıt bu klasöre konur ve tarif `make_sfx_kenney.py` içindeki `RECIPES` tablosuna yazılır.

Hapishanenin kapısı (`jail_open`, `jail_shut`, 2026-11-12) yine bu kayıtlardan kurulur: RPG Audio'dan `metalLatch`,
`metalClick`, `creak3`; Impact Sounds'tan `impactMetal_heavy_001`, `impactMetal_medium_003`. Dosyalar yeniden
indirilmedi, 2026-11-03'te indirilen paketlerin tamamını tutan `kenney-kaynak` dalından alındı.

Binaların gidiş sesleri (`building_sold`, `building_fall`, 2026-11-18) de bu kayıtlardan kurulur ve animasyonun
anlarına göre kesilir (betikteki `SOLD_POP`, `FALL_DROP`, `FALL_DOWN`). Satış: tahta tıkırtısı, kumaş hışırtısı
(`cloth2`, `cloth4`, `dropLeather`), çekmece mandalı (`metalLatch`), yazar kasa zili (`impactBell_heavy_000`, hızlı
çalınarak inceltilmiş, üstünde `impactMetal_light_003`) ve bozuk paralar (`handleCoins`, `handleCoins2`). Yıkım:
çatlama (`chop`, `impactPlank_medium`), ufalanan duvar (`footstep_snow`, yavaş çalınarak kalınlaştırılmış;
`chips-handle-5`, `doorClose_1/3`), cam (`impactGlass_light`), gümleme (`impactMining`, `impactPlate_heavy_004`,
`impactPunch_heavy_000`, `doorClose_4`) ve yere düşen moloz (`bookPlace1-3`, `impactGeneric_light`, `drop_001/002/004`).
Yeni indirme yapılmadı: dosyalar `kenney-kaynak` dalından alındı. Kayıtların hızı ve düzeyi dışında bir işlem yok.

Füze kartının üç sesi (`missile_launch`, `missile_blast`, `missile_block`, 2026-11-21) Kenney'nin Sci-fi Sounds
paketindeki (1.0) roket itici ve patlama kayıtlarından kurulur; hesaplanan katman kalmadı. (İlk iki denemede motor
ve patlamanın gövdesi betikte hesaplanıyordu; sahip "daha gerçekçi olsun" dedi ve paketin indirilmesini onayladı.)
Paket 2026-11-21'de sahibin onayıyla, sahibin bilgisayarında kenney.nl'den indirildi: `kenney_sci-fi-sounds.zip`,
5.875.104 bayt, SHA-256 `119340F351A5098AD814F78719438C0DA355A9CE8A4C8A3AF6A8D48AA3D49E04`; lisans CC0 1.0
(`sci-fi-sounds/License.txt`). 73 kaydın tamamı `kenney-kaynak` dalında (740eb67), burada yalnız kullanılanlar var.
Kalkış: ateşlemenin derin vuruşu (`lowFrequency_explosion_001`, `explosionCrunch_000`), rögar kapağının demiri
(`impactMetal_medium_003`) ve uçuş boyunca üç itici kaydı üst üste (`thrusterFire_004` alevin tıslaması,
`thrusterFire_001` kükreme, `thrusterFire_003` alttaki uğultu); düzeyleri füze uzaklaşırken düşer, şehre inerken
yeniden yükselir (`stretch()`: uzun kaydın bir parçası, ses düzeyi eğrisiyle). Patlama: iki patlama kaydı üst üste
(`explosionCrunch_001`, `explosionCrunch_004`), altında derin gümleme (`lowFrequency_explosion_000`), içinde
dağılan üçüncüsü (`explosionCrunch_002`), üstünde yıkımdaki kırılan duvar, cam ve yere düşen moloz kayıtları.
Kalkana çarpma: boğuk darbe, çan ve cam çınlaması (`impactSoft_heavy_000`, `impactBell_heavy_000`,
`impactGlass_light`) ile kalkanın uğultusu (`forceField_000`). Kayıtların kesilmesi, hızı ve düzeyi dışında işlem yok.

Arayüzün yeni sesleri (`ui_hover`, `ui_hover_2`, `ui_hover_3`, `ui_close`, `ui_start`, `ui_leave`, 2026-11-21) de
bu kayıtlardandır; sahip menüler için "buton sesleri de olsun" dedi. Fare bir butona gelince: UI Audio'dan
`rollover4`, `rollover5`, `rollover2` (üç ayrı kayıt, art arda aynısı çalmaz). Pencere kapanınca: Casino Audio'dan
`card-shove-3` (masada itilen kart; açılıştaki sesin kardeşi, ondan kısık). Oyuna geçerken: Interface Sounds'tan
`maximize_006` ile üstünde `confirmation_002`. Menüye dönerken: `minimize_006`. Yeni indirme yapılmadı: dosyalar
`kenney-kaynak` dalından alındı. Kayıtların kesilmesi ve düzeyi dışında bir işlem yok; hesaplanan ses yok.
