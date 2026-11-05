# Dokulu karakterler (Meshy AI)

Proje sahibinin 2026-11-05'te verdiği iki karakter, Meshy AI ile resimden üretilmiş 3B modellerdir (image-to-3D, dokulu; iskelet ve animasyon yok):

| Kimlik | Karakter | Kaynak dosya | Üçgen | Renk dokusu |
|---|---|---|---|---|
| `suit` | Takım elbiseli iş adamı | `Meshy_AI_StylizedBusinessman_1105220443_image-to-3d-texture.glb` (23 MB) | 303.566 | 4096 |
| `cane` | Bastonlu beyefendi | `Meshy_AI_GentlemanWithCane_1105221220_image-to-3d-texture.glb` (52 MB) | 602.912 | 8192 |

Kaynak GLB dosyaları büyük oldukları için depoda değil; proje dosyalarında (`characters/` ve kök klasör) duruyorlar. Oyundaki dosyaları yeniden üretmek için bu klasöre koyup depo kökünden çalıştır:

    python3 Tools/Art/make_figures.py suit=Tools/Art/Models/Figures/StylizedBusinessman.glb cane=Tools/Art/Models/Figures/GentlemanWithCane.glb

Araç her modeli bir birim boya ölçekler, Yüksek ayar için 120.000, Orta/Düşük için 24.000 üçgene indirir ve renk dokusunu 2048 piksele küçültür (`Assets/_Lazpoly/Data/Resources/Lazpoly/Models/figure_<kimlik>.bytes` ve `figure_<kimlik>_skin.bytes`).

Steam yapay zekâ beyanına girer (Steam/README.md).
