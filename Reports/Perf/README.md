# Lazpoly performans kapıları

Cila planındaki kapılar ve nasıl ölçüldükleri. Her build'den sonra `-perf` koşusu yapılır; sonuç PR açıklamasına
yazılır ve bu klasöre `perf-<faz>.md` olarak eklenir.

| Kapı | Hedef | Nasıl ölçülür |
|---|---|---|
| Kare hızı | 1080p Orta kalitede ortalama ≥ 60 FPS; Düşük'te entegre grafik kartında ≥ 30 FPS | `Lazpoly.exe -perf`: 8 bilgisayarlı masa, 3 sn ısınma, 15 sn ölçüm |
| En yavaş %1 | bilgi (takılma hissi) | aynı koşu, en yavaş karelerin ortalaması |
| Bellek | oyun sahnesi < 600 MB ayrılmış | aynı koşunun sonunda `Profiler.GetTotalAllocatedMemoryLong` |
| Yükleme | menü → masa < 3 sn | `SceneFader.LoadRequestedAt` ile masa kurulduğu an arasındaki süre |
| Derleme | 0 hata, 0 uyarı | RC build adımı |
| Testler | EditMode ve PlayMode tam geçer | RC test adımı |
| Denge | 1000 oyunda takılan 0, 4 oyuncuda 15-22 tur | `Reports/Balance` |
| Build boyutu | < 60 MB | zip boyutu |

## Koşuyu çalıştırmak

```
Lazpoly.exe -perf -screen-width 1920 -screen-height 1080 -screen-fullscreen 0
```

Oyun ana menüyü atlayıp 8 bilgisayarlı masayı açar, ölçer, `lazpoly-perf.txt` dosyasını exe'nin yanına yazar ve
kendini kapatır (yaklaşık 20 sn). Kapanmadan önce masanın bir ekran görüntüsünü de `%USERPROFILE%\AppData\LocalLow\DefaultCompany\lazpoly\Screenshots` klasörüne kaydeder. Kalite ayarı oyuncunun kayıtlı ayarıdır; Orta kalite kapısı için ayarlardan Orta
seçili olmalı. Oyun içinde **F3** kare hızı ve belleği anlık gösterir.

Not: batch mod (`-batchmode -nographics`) gerçek çizim yapmadığından kare hızı için anlamsızdır; koşu pencereli modda
yapılır.
