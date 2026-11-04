# Kaynak ve kullanım hakkı

Bu bina modelleri (arsa, Ev 1-4, otel; `.glb`, `Lazpoly_Buildings.blend`, `Preview.png`, `Validation.json`)
2026-11-04'te proje sahibi **mami** tarafından projeye yüklendi. Lazpoly içinde ve Steam yayınında kullanım
hakkı proje sahibine aittir.

Steam notu (cila planı 10d): modeller bir üretim aracıyla yapıldıysa (çadır, karakterler ve logo gibi), aracın
adı buraya ve Steamworks içerik anketindeki yapay zekâ beyanına yazılır. Araç adı proje sahibinden
bekleniyor.

Oyundaki kopya: `Tools/Art/make_buildings.py` her modeli üçgenlerinin yaklaşık %30'una indirip
`Assets/_Lazpoly/Data/Resources/Lazpoly/Models/buildings.bytes` paketine yazar (araç yalnızca
`pip install fast-simplification` ister; oyun paketi değildir). `README_TR.md` model paketinin kendi
açıklamasıdır; oradaki Unity kurulum adımları (LazpolyBuildingsSetup, prefablar) bu projede kullanılmaz.
