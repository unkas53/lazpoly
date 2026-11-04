# Kaynak ve kullanım hakkı

Bu bina modelleri (arsa, Ev 1-4, otel; `.glb`, `Lazpoly_Buildings.blend`, `Preview.png`, `Validation.json`)
2026-11-04'te proje sahibi **mami** tarafından projeye yüklendi. Lazpoly içinde ve Steam yayınında kullanım
hakkı proje sahibine aittir.

Üretim aracı (proje sahibinin beyanı, 2026-11-04): **ChatGPT** (OpenAI). Steamworks içerik anketinde yapay
zekâyla üretilmiş içerik olarak beyan edilir (`Steam/README.md` §6); OpenAI çıktıların kullanım hakkını kullanıcıya
bırakır, başvurudan önce güncel koşullara bakılır.

Oyundaki kopya: `Tools/Art/make_buildings.py` `Lazpoly_Buildings.blend` dosyasını okur, parçaları adlarına göre
renk gruplarına (duvar, çerçeve, cam, kapı, çatı, taban, metal) ayırır, arsanın tabelasını 1,7 kat büyütür ve her modeli olduğu gibi ve üçgenlerinin yaklaşık %30'una indirilmiş hafif
bir kopyası olarak `Assets/_Lazpoly/Data/Resources/Lazpoly/Models/buildings.bytes` paketine yazar (oyun Yüksek
grafik kalitesinde tam modelleri, Orta ve Düşükte hafif kopyaları gösterir) (araç yalnızca
`pip install bpy fast-simplification` ister; oyun paketi değildir). `README_TR.md` model paketinin kendi
açıklamasıdır; oradaki Unity kurulum adımları (LazpolyBuildingsSetup, prefablar) bu projede kullanılmaz.
