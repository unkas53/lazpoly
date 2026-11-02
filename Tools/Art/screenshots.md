# Mağaza ekran görüntüleri

Steam mağazası için beş görüntü, her seferinde aynı açılardan. Oyun içinde **F11** açılar arasında gezer
(`ScreenshotPresets`), **F12** `%USERPROFILE%\AppData\LocalLow\<şirket>\Lazpoly\Screenshots` altına PNG kaydeder.
1920x1080 pencerede, Yüksek kalitede, Türkçe ve İngilizce birer set alınır.

| # | Ön ayar | Ne göstermeli |
|---|---|---|
| 1 | Genel görünüm | Oyunun kendi açısı: baklava biçimli tahta, Başlangıç köşesi altta, 4 oyuncu, birkaç sahipli şehir ve çadır; çimde zar skoru ve kumbara |
| 2 | Yakın plan | Aynı açıdan yakınlaşmış: bir köşedeki şehirlerde 3-4 ev ve bir otel (baca dumanı), piyonlar yuvalarında |
| 3 | Karşı köşe | Tahtanın karşı köşesinden; zarlar atılmış, zar sonucu kartı açık; gökyüzü akşama yakın |
| 4 | Sol köşe | Soldaki köşeden, hafif yakın; piyonlar ve plakalar okunur |
| 5 | Sağ köşe | Sağdaki köşeden, hafif yakın; sahipli şehirlerin çerçeveleri ve renk şeritleri |

Yazılar her açıdan soldan sağa okunacak şekilde döner (`BoardView.FaceCamera`). Öneri: 3 numaralı görüntü için
ayarlardan tur sınırını Kısa (25) yapıp 20. turdan sonra çekin; akşam göğü böyle en iyi görünür. Kapak görselleri `make_capsules.py` ile üretilir (Steam/Art).
