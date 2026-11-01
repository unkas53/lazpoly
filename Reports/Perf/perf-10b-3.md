# Faz 10b-3 performans ölçümü

Koşu: `Lazpoly.exe -perf -screen-width 1920 -screen-height 1080 -screen-fullscreen 0`, build `lazpoly-faz10b-3.zip` (38,1 MB).
Makine: NVIDIA GeForce RTX 4070 Laptop GPU · 13th Gen Intel Core i7-13620H · 31,7 GB RAM. Kayıtlı kalite: High.

2026-11-01 21:15 · sürüm 1.0 · 1920x1080 · 8 bilgisayar oyuncu · 1766 kare ölçüldü

| Kapı | Hedef | Ölçüm | Sonuç |
|---|---|---|---|
| Kare hızı (ortalama) | ≥ 60 FPS | 117.6 FPS | geçti |
| Kare hızı (en yavaş %1) | bilgi | 51.0 FPS | – |
| Bellek (ayrılmış) | < 600 MB | 238 MB (rezerve 282 MB) | geçti |
| Yükleme (menü → masa) | < 3 sn | 0.45 sn | geçti |

İkinci koşu (bfc575f, 21:20, 1786 kare): ortalama 119.0 FPS, en yavaş %1 72.0 FPS, 242 MB (rezerve 282 MB), yükleme 0.46 sn.
İki koşu arasında en yavaş %1 51 → 72 FPS: kod bu yolu değiştirmedi, fark ölçüm oynaklığı; tek koşuya güvenilmemeli.

Diğer kapılar: derleme 0 hata / 0 uyarı; EditMode 294/294; PlayMode 8/8; zip 38,1 MB < 60 MB.

Notlar
- Ölçüm güçlü bir dizüstünde, High kalitede alındı; Orta kalite 1080p kapısı ve Düşük kalite entegre grafik kapısı bu
  makinede ancak üst sınır olarak okunabilir. En yavaş %1'de 51 FPS: tek tek kareler 60'ın altına iniyor (masa kurulumu
  ve ilk tur animasyonları).
- İlk koşu oyuncunun kayıt dosyasını (lazpoly-kayit.dat) üzerine yazdı; -perf masası artık kayıt slotuna dokunmuyor.
