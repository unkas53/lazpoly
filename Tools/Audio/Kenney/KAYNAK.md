# Kenney ses kayıtları

Oyundaki ses efektleri (`Assets/_Lazpoly/Audio/Resources/Lazpoly/Sfx/*.wav`) bu klasördeki kayıtlardan
`Tools/Audio/make_sfx_kenney.py` ile üretilir. Kayıtlar Kenney'nin (kenney.nl) ücretsiz paketlerinden alındı:
Casino Audio, Impact Sounds, Interface Sounds, RPG Audio, Music Jingles, UI Audio. 2026-11-03'te kenney.nl'den
indirildi; yalnız oyunun kullandığı dosyalar tutuluyor.

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

Füze kartının üç sesi (`missile_launch`, `missile_blast`, `missile_block`, 2026-11-21) yarı kayıt, yarı hesaptır:
Kenney paketlerinde roket ve patlama sesi yok. Kalkıştaki darbe, patlamanın gümlemesi (ağır darbeler yavaş
çalınarak), kırılan duvar, cam ve yere düşen moloz ile kalkana çarpmanın çınlaması (`impactBell_heavy_000`,
`impactGlass_light`) bu klasördeki kayıtlardandır. Motorun uğultusu, inerken çıkan ıslık, patlamanın derin gövdesi ve
kalkandaki kısa tıslama ise `make_sfx_kenney.py` içinde hesaplanır (`made()`: biçim verilmiş gürültü ve tonlar).
Yeni indirme yapılmadı. Gerçek bir roket ya da patlama kaydıyla değiştirilecekse CC0 kayıt bu klasöre konur ve
tarifteki `made(...)` katmanının yerine yazılır.
