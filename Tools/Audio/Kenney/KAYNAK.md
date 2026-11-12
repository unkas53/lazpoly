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
