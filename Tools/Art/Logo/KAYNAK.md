# Kaynak ve kullanım hakkı

`lazpoly-logo.jpg` proje sahibi **mami** tarafından 2026-11-03'te yüklendi; kendi beyanına göre **ChatGPT** ile üretildi.
Üç boyutlu çokgen "LAZPOLY" harfleri, zar, binalar, ev, ağaçlar ve masa tahtası içerir. Kaynakta sağ üstteki altın
madalyonun içinde fotoğraf gerçekliğinde, sigaralı bir adam portresi vardı; gerçek bir kişiye benzeme (kişilik
hakları, Steam içerik kuralları) ve sigaranın yaş derecelendirmesine etkisi nedeniyle proje sahibi kaldırılmasını
istedi (2026-11-03 16:08). Kaynak dosya olduğu gibi durur; oyuna giden kopyada madalyonun içi `make_logo.py`
tarafından yeniden boyanır (logonun kendi mavilerinde iki tonlu lacivert disk ve beyaz "L", Nunito Bold).

Oyundaki kopya: `Tools/Art/make_logo.py` önce madalyonu boyar, sonra beyaz zemini kenarlardan taşırmalı
doldurmayla (eşik 236) şeffaflığa çevirir, kareye kırpar, 640x640'a küçültür ve
`Assets/_Lazpoly/UI/Resources/Lazpoly/Art/logo.bytes` (PNG) olarak yazar. Oyun onu `MenuArt` ile açar: ana menünün
yeşil bandında (sol üst) ve oyun tahtasının çimenliğinde (`BoardView`, `PicturePlate` + `Lazpoly/Picture`
gölgelendiricisi) gösterir; dosya yoksa ikisi de adı yazıyla gösterir. Steam içerik beyanı: `Steam/README.md` §6.
