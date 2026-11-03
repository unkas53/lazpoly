# Ana menü ve lobi tasarımı (yalnız başvuru)

`lazpoly-main-menu.svg` ve `lazpoly-lobby.svg` proje sahibi **mami**'nin 2026-11-03'te verdiği konsept ekranlardır
(proje sahibinin beyanına göre ChatGPT ile üretilmiş). Oyunda doğrudan kullanılmazlar: oyun
arayüzünü kodla kurar (`UiBuilder`), tasarımın **düzeni ve tarzı** koda aktarıldı (Faz 11g, README).

Verilen paketteki Unity demo projesi (`LazpolyDemoBootstrap.cs` ve yanındaki sahne / ayar dosyaları) depoya
alınmadı, çünkü:

- `Packages/manifest.json`'u URP ve test paketleri olmadan yazıyordu; `EditorBuildSettings` sahne listesini
  değiştiriyordu (oyunun MainMenu ve Game sahneleri düşerdi).
- Eski `Text` bileşeni ve `FindObjectOfType` kullanıyordu (uyarı verir; projede 0 uyarı kuralı var).
- Sahte veriyle doluydu (AHMET, ₺6 100, taşlar, "248 çevrim içi", turnuva / mağaza / sıralama): bunlar oyunda
  olmayan özelliklerdi. Oyundaki menü gerçek özelliklere bağlı: kartlar özel masa, odaya katılma, tek oyunculu,
  aynı bilgisayar, masa kuralları ve rehber; lobi Steam odasının gerçek üyelerini, hazır durumlarını, oda kodunu ve
  arkadaş listesini gösterir.

Tasarımdan alınanlar: gökyüzü üstünde kağıt pencere, yeşil başlık bandı (altın marka işareti, Steam durumu,
sürüm, ayarlar ve çıkış), başlık şeridi, 3x2 renk şeritli kartlar (chip, simge diski, "›"), alt bantta slogan ve
büyük yeşil "Masaya Otur →"; lobide üç sütun (masa ayarları chip'leri, renk bantlı koltuk kartları ve boş koltuk,
oda kodu çubuğu ve Kopyala, arkadaş sütunu ve davet), alt bantta "Masa seni bekliyor" ve "Hazırım →".
