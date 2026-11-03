# Kaynak ve kullanım hakkı

Bu karakter modeli (BusinessMan.obj / .mtl, üreten betik generate_model.py) proje sahibi **mami** tarafından bir
yapay zekâ aracına ürettirildi (2026-11-03) ve projeye kendisi yükledi. Aracın kendi açıklaması `README_TR.md`:
üçüncü taraf bir asset indirilmeden, prosedürel geometriyle (trimesh + numpy) sıfırdan üretilmiş özgün bir
modeldir; ücretli oyun dahil kullanım ve değiştirme hakkı proje sahibine aittir, üçüncü taraf lisansı yoktur.
Yüklenen paketteki GLB dosyaları (`LAZPOLY_BusinessMan.glb`, `_Optimized.glb`) oyunda kullanılmadığı için depoya
alınmadı; gerekirse `generate_model.py` ile yeniden üretilir.

Model: 2,03 m boyunda, 13.578 üçgen, 112 adlı parça, 30 düz renkli malzeme (BusinessMan.mtl). İskeleti ve
animasyonu yoktur; oyunda tek parça hareket eder (piyonun zıplaması).

Steam için (cila planı 10d): Steamworks içerik anketinde bu model de çadır gibi "önceden üretilmiş yapay zekâ
içeriği" olarak beyan edilir; `Steam/README.md` §6'daki tabloya eklendi. Üretim aracı proje sahibinin beyanına
göre yazılır (çadırda ChatGPT ve Higgsfield; bu model için araç henüz belirtilmedi, başvurudan önce sorulur).

Oyundaki kopya: `Tools/Art/make_character.py` ile Y-yukarı çevrilip malzemeye göre birleştirilir ve
`Assets/_Lazpoly/Data/Resources/Lazpoly/Models/business_man.txt` olarak yazılır (bir birim boy, ayaklar y = 0,
yüzü +Z). Oyunda ceketin dört malzemesi (jacket, jacket_light, jacket_dark, lapel) oyuncunun rengine boyanır
(`CharacterParts`), kalan renkler `BusinessMan.mtl`'deki paletin kopyasıdır.
