# Dokulu karakterler (Meshy AI)

Proje sahibinin 2026-11-05 ve 2026-11-06'da verdiği sekiz karakter, Meshy AI ile resimden üretilmiş 3B modellerdir (image-to-3D, dokulu; iskelet ve animasyon yok):

| Kimlik | Karakter | Kaynak dosya | Üçgen | Renk dokusu |
|---|---|---|---|---|
| `suit` | Takım elbiseli iş adamı | `Meshy_AI_StylizedBusinessman_1105220443_image-to-3d-texture.glb` (23 MB) | 303.566 | 4096 |
| `cane` | Bastonlu beyefendi | `Meshy_AI_GentlemanWithCane_1105221220_image-to-3d-texture.glb` (52 MB) | 602.912 | 8192 |
| `teal` | Turkuaz takımlı iş kadını | `Meshy_AI_TealSuitBusinesswoman_1105233412_image-to-3d-texture.glb` (40 MB) | 365.326 | 8192 |
| `hip` | Eli belinde iş kadını (bordo ceket) | `Meshy_AI_BusinesswomanHandOnHi_1105231612_image-to-3d-texture (1).glb` (51 MB) | 426.956 | 8192 |
| `navy` | Lacivert takımlı iş adamı | `Meshy_AI_NavySuitBusinessman_1105235258_image-to-3d-texture (2).glb` (53 MB) | 448.078 | 8192 |
| `purple` | Mor ceketli iş adamı | `Meshy_AI_PurpleBlazerBusinessm_1105234203_image-to-3d-texture (1).glb` (54 MB) | 371.134 | 8192 |
| `boss` | Purolu, fötr şapkalı patron | `Meshy_AI__1106111336_texture.glb` (62 MB) | 751.899 | 8192 |
| `mob` | Kürk paltolu kadın | `Meshy_AI_MobWomanMainPose_1106111318_image-to-3d-texture.glb` (95 MB) | 1.969.714 | 8192 |

Kaynak GLB dosyaları büyük oldukları için depoda değil; proje dosyalarında (`characters/` ve kök klasör) duruyorlar. Oyundaki dosyaları yeniden üretmek için bu klasöre koyup depo kökünden çalıştır:

    python3 Tools/Art/make_figures.py suit=Tools/Art/Models/Figures/StylizedBusinessman.glb cane=Tools/Art/Models/Figures/GentlemanWithCane.glb teal=Tools/Art/Models/Figures/TealSuitBusinesswoman.glb hip=Tools/Art/Models/Figures/BusinesswomanHandOnHip.glb navy=Tools/Art/Models/Figures/NavySuitBusinessman.glb purple=Tools/Art/Models/Figures/PurpleBlazerBusinessman.glb boss=Tools/Art/Models/Figures/MobBoss.glb mob=Tools/Art/Models/Figures/MobWomanMainPose.glb

Araç her modeli bir birim boya ölçekler, Yüksek ayar için 120.000, Orta/Düşük için 24.000 üçgene indirir ve renk dokusunu Unity'nin içe aldığı bir doku olarak yazar: 4096 ve altıysa olduğu gibi, 8192'likler 4096'ya küçültülür (`Assets/_Lazpoly/Data/Resources/Lazpoly/Models/figure_<kimlik>.bytes` ve `figure_<kimlik>_skin.jpg`; oyunda mipmap'li BC7, 2048 piksel). Yalnız dokuları yeniden yazmak için: `python3 Tools/Art/make_figures.py --skins <kimlik>=<model.glb> ...`.

Menüde, lobide ve oyuncu kartlarında görünen karakter resimleri (`Figure_<kimlik>` bütün figür, `Face_<kimlik>` baş ve omuzlar) bu figürlerden Blender ile çizilir (`pip install bpy`, yalnız resim üretmek için):

    python3 Tools/Art/make_figure_portraits.py hip purple teal navy mob suit boss cane

## Otogar dioraması

Otogar köşesindeki sahne de proje sahibinin Meshy AI ile ürettiği bir modeldir: `Meshy_AI_CartoonBusStopDiorama_1106180721_image-to-3d-texture.glb` (52 MB, 615.500 üçgen), çizgi film tarzı sarı bir şehir otobüsü ve durak (ilk `BusAtBusStopDiorama` modelinin yerine, 2026-11-06). Bu klasöre `CartoonBusStopDiorama.glb` adıyla koyup çalıştır:

    python3 Tools/Art/make_station.py

Araç modeli boyuna göre değil genişliğine göre ölçekler (bir birim genişlik, taban y = 0, önü +Z), aynı iki ayrıntı düzeyini ve içe alınan dokuyu yazar. Otobüs oyuncuyu ortadaki yoldan taşıdığı için (sahibin isteği, 2026-11-10) model ikiye ayrılır: otobüssüz durak (`figure_station.bytes`, `figure_station_skin.jpg`) ve tek başına otobüs (`figure_bus.bytes`; durağın dokusunu kullanır, tekerlekleri y = 0'da, burnu +Z'de, durakla aynı ölçekte). Otobüs modele yalnızca lastiklerinden bağlı olduğu için yol hizasının hemen üstünden kesilir; lastiklerin yolda bıraktığı delikler koyu bir yamayla kapanır. Aracın sonunda yazdığı park yeri ve ölçüler `BusModel.cs` içindedir. Dosyalar yoksa oyun çizilmiş durağı gösterir; otobüs dosyası yoksa yolculuk eskisi gibi yürüyerek gösterilir.

Steam yapay zekâ beyanına girer (Steam/README.md).

## Şampiyonlar Kupası

Kupanın şehrindeki kupa da proje sahibinin Meshy AI ile ürettiği bir modeldir: `Meshy_AI_MatteGoldLazPolyTroph_1107100206_texture.glb` (24 MB, 339.543 üçgen; önünde siyah "LazPoly CHAMPIONS" yazılı mat altın kupa, 2026-11-07; aynı günün ilk gümüş kupasının yerine geldi). Bu klasöre `ChampionsTrophy.glb` adıyla koyup çalıştır:

    python3 Tools/Art/make_figures.py trophy=Tools/Art/Models/Figures/ChampionsTrophy.glb

Araç onu bir figür gibi bir birim boya ölçekler (yazısı +Z'de), aynı iki ayrıntı düzeyini (120.000 / 24.000 üçgen) ve 2048'lik dokuyu yazar (`figure_trophy.bytes`, `figure_trophy_skin.bytes`). Dosyalar yoksa oyun çizilmiş altın kupayı gösterir.

Steam yapay zekâ beyanına girer (Steam/README.md).

## Şampiyonlar Kupası stadyumu

Şampiyonlar Kupası köşesinde, kupanın yerine (sahibin isteği) proje sahibinin Meshy AI ile ürettiği çizgi film stadyumu durur: `Meshy_AI_CartoonStadiumDiorama_1107093213_image-to-3d-texture.glb` (140 MB, 3.904.102 üçgen, 2026-11-07). Bu klasöre `StadiumDiorama.glb` adıyla koyup çalıştır:

    python3 Tools/Art/make_corner_models.py stadium=Tools/Art/Models/Figures/StadiumDiorama.glb

Araç onu otogar dioraması gibi bir birim genişliğe ölçekler (taban y = 0, tribün +Z'de), iki ayrıntı düzeyini (120.000 / 30.000 üçgen) ve 2048'lik dokuyu yazar (`figure_stadium.bytes`, `figure_stadium_skin.bytes`). Dosyalar yoksa oyun çizilmiş kupayı gösterir.

Steam yapay zekâ beyanına girer (Steam/README.md).
