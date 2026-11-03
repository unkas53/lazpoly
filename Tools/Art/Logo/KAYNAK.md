# Kaynak ve kullanım hakkı

`lazpoly-logo.jpg` proje sahibi **mami** tarafından 2026-11-03'te yüklendi; kendi beyanına göre bir yapay zekâ
aracıyla üretildi (araç henüz belirtilmedi, Steam başvurusundan önce sorulur). Üç boyutlu çokgen "LAZPOLY"
harfleri, zar, binalar, ev, ağaçlar, masa tahtası ve altın bir çember içinde sigaralı bir adam portresi içerir.

**Dikkat edilecekler (Steam / hak):**

- Portre fotoğraf gerçekçiliğinde ve gerçek bir oyuncuya / film karakterine benziyor olabilir. Gerçek bir kişinin
  benzerliği izinsiz kullanılamaz (kişilik hakları, Steam'in içerik kuralları). Mağazaya çıkmadan önce logo
  portresiz ya da oyundaki iş adamı karakteriyle yeniden üretilmeli, ya da portrenin kime ait olmadığı kesinleşmeli.
- Sigara, yaş derecelendirme anketlerinde (PEGI / ESRB) "tütün kullanımı" olarak beyan edilir ve mağaza görselinde
  yaş sınırını etkileyebilir.

Oyundaki kopya: `Tools/Art/make_logo.py` beyaz zemini kenarlardan taşırmalı doldurmayla (eşik 236) şeffaflığa
çevirir, kareye kırpar, 640x640'a küçültür ve `Assets/_Lazpoly/UI/Resources/Lazpoly/Art/logo.bytes` (PNG) olarak
yazar. Oyun onu `MenuArt` ile açar ve ana menünün başlık şeridinde gösterir; dosya yoksa menü adını yazıyla gösterir.
Steam içerik beyanı: `Steam/README.md` §6.
