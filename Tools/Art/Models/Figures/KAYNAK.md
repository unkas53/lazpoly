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

Araç her modeli bir birim boya ölçekler, Yüksek ayar için 120.000, Orta/Düşük için 24.000 üçgene indirir ve renk dokusunu 2048 piksele küçültür (`Assets/_Lazpoly/Data/Resources/Lazpoly/Models/figure_<kimlik>.bytes` ve `figure_<kimlik>_skin.bytes`).

Menüde, lobide ve oyuncu kartlarında görünen karakter resimleri (`Figure_<kimlik>` bütün figür, `Face_<kimlik>` baş ve omuzlar) bu figürlerden Blender ile çizilir (`pip install bpy`, yalnız resim üretmek için):

    python3 Tools/Art/make_figure_portraits.py hip purple teal navy mob suit boss cane

## Otogar dioraması

Otogar köşesindeki sahne de proje sahibinin Meshy AI ile ürettiği bir modeldir: `Meshy_AI_BusAtBusStopDiorama_1106131802_image-to-3d-texture.glb` (45 MB, 316.852 üçgen, 8192 doku), durakta bekleyen mavi beyaz bir otobüs. Bu klasöre `BusAtBusStopDiorama.glb` adıyla koyup çalıştır:

    python3 Tools/Art/make_corner_models.py station=Tools/Art/Models/Figures/BusAtBusStopDiorama.glb

Araç modeli boyuna göre değil genişliğine göre ölçekler (bir birim genişlik, taban y = 0, önü +Z), aynı iki ayrıntı düzeyini ve 2048'lik dokuyu yazar (`figure_station.bytes`, `figure_station_skin.bytes`). Dosyalar yoksa oyun çizilmiş durağı gösterir.

Steam yapay zekâ beyanına girer (Steam/README.md).
