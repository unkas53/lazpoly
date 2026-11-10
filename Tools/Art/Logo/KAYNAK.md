# Kaynak ve kullanım hakkı

`cash-father-logo.png` (1244x1265, saydam zeminli PNG) proje sahibi **mami** tarafından 2026-11-10 20:16'da verildi
("logoyu bununla değiştir iconda logodaki karakter olsun"). Üstte çizgi film tarzında, silindir şapkalı, kızgın yüzlü,
iri yapılı bir adam (siyah palto, kırmızı kravat, altın topuzlu baston, "$" yüzük); arkasında gökdelenler ve para
desteleri; altta taçlı altın "CASH", gümüş "FATHER", iki yanda zarlar, en altta "$" madalyonu. Üretim aracı henüz
yazılmadı; görünüşü yapay zekâ ile üretilmiş gibi, Steam içerik beyanına eklenmeli (`Steam/README.md` §6).

Aynı gün verilen ilk Cash Father logosu (puro içen fotogerçekçi adam) ve eski Lazpoly logosu (`lazpoly-logo.jpg`,
ChatGPT) depodan kaldırıldı; git geçmişinde durur.

Oyundaki kopyalar `Tools/Art/make_logo.py` ile yazılır: resim gösterdiği kısma kırpılır (10 piksel pay kalır), neredeyse
opak pikselleri tam opak yapılır ve PNG olarak iki yere yazılır: `Assets/_Lazpoly/UI/Resources/Lazpoly/Art/logo.bytes`
(`MenuArt`: tahtanın çimenliği) ve `Menu/Lazpoly_Logo.bytes` (`KitArt`: ana menü, yükleme ekranları). Uygulama simgesi
sahibin isteğiyle logodaki karakterdir: şapkalı başı yakasıyla (256x256) ve 48 piksel ve altı için daha sıkı kırpılmış
yüzü (64x64). Aynı betik oyuncu kartı kitindeki eski adı ve "L" mühürlerini de `Tools/Art/brand_kit.py` ile yeniler.
Steam görselleri ve simgeleri: `Tools/Art/make_capsules.py`.
