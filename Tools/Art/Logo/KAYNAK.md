# Kaynak ve kullanım hakkı

`cash-father-logo.png` (1774x887, saydam zeminli PNG) proje sahibi **mami** tarafından 2026-11-10'da oyunun yeni adıyla
birlikte verildi ("oyunumuzun adını CASH FATHER olarak değiştiriyoruz bu da yeni logomuz"). Taçlı altın "CASH",
gümüş "FATHER" harfleri, altta "$" madalyonu ve yıldızlar; yanlarda zarlar, para desteleri ve altın sikkeler; arkada gece
şehri; üstte altın-siyah bir tahtta oturan, güneş gözlüklü, puro içen, takım elbiseli bir adam. Üretim aracı henüz
yazılmadı; görünüşü yapay zekâ ile üretilmiş gibi, Steam içerik beyanına eklenmeli (`Steam/README.md` §6).

Eski logo (`lazpoly-logo.jpg`, ChatGPT ile üretilmişti) depodan kaldırıldı; git geçmişinde durur.

Oyundaki kopyalar `Tools/Art/make_logo.py` ile yazılır: resim gösterdiği kısma kırpılır (10 piksel pay kalır), neredeyse
opak pikselleri tam opak yapılır ve PNG olarak iki yere yazılır: `Assets/_Lazpoly/UI/Resources/Lazpoly/Art/logo.bytes`
(`MenuArt`: tahtanın çimenliği) ve `Menu/Lazpoly_Logo.bytes` (`KitArt`: ana menü, yükleme ekranları). Aynı betik exe
simgesini (logo kareye sığdırılmış, 256x256) ve küçük boyutlar için çizilmiş altın "$" sikkesini yazar, oyuncu kartı
kitindeki eski adı ve "L" mühürlerini de `Tools/Art/brand_kit.py` ile yeniler. Steam görselleri: `Tools/Art/make_capsules.py`.
