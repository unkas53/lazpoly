# LAZPOLY — Low-Poly Business Man (orijinal 3D karakter)

Bu paket fotoğraftan 3D'ye otomatik dönüşüm değil; referanstaki genel stile benzeyen, sıfırdan geometrik parçalarla üretilmiş özgün bir modeldir.

## Dosyalar
- LAZPOLY_BusinessMan_Optimized.glb : Oyun için tek parça, renkli ve optimize edilmiş 3D model. **Önerilen**.
- LAZPOLY_BusinessMan.glb : Gövde, saç, eller, tablet gibi 112 ayrı nesne halinde düzenlenebilir sürüm.
- LAZPOLY_BusinessMan.obj + LAZPOLY_BusinessMan.mtl : Blender veya diğer 3D düzenleme yazılımları için model ve malzeme renkleri.
- preview_*.png : Modelin farklı açılardan görüntüsü.
- LAZPOLY_BusinessMan_Preview.png : Modelin 3 açılı görünümü.
- model_info.json : Teknik özet.
- generate_model.py : Python (trimesh + numpy) ile modeli yeniden üretmek için kaynak kod.

## Teknik özellikler
- Yaklaşık boy: 2,03 metre.
- Üçgen: 13.578.
- Dış doku/texture dosyası gerektirmez; renkler glTF/GLB içinde gömülüdür (vertex colors).
- Hareket/rig/skeleton/animasyon BULUNMAZ; sabit bir pozdaki modeldir.
- GLB: +Y yukarı, +Z ön (Godot/gltf uyumlu).
- OBJ: +Z yukarı, -Y ön (Blender/Z-up modelleme düzeni).

## Godot 4 için
1. `_Optimized.glb` dosyasını Godot projesi içine kopyalayın.
2. Godot otomatik olarak 3D sahne olarak içe alır.
3. Dosyayı sahneye sürükleyip konumlandırın; karakter yaklaşık 2m boyundadır.
4. Oyuncu karakteri gibi yürütmek isterseniz ayrıca rig/skeleton ve yürüyüş animasyonları üretmeniz gerekir.

## Blender için
GLB: File > Import > glTF 2.0 (.glb/.gltf).
OBJ: File > Import > Wavefront (.obj), yanında .mtl dosyasını tutun.

## Kullanım
Bu model bu istek için dışarıdan üçüncü taraf 3D asset indirilmeden, prosedürel geometriyle sıfırdan oluşturuldu. Oyun projenizde, ücretli oyun dahil, kullanabilir ve değiştirebilirsiniz. Üçüncü taraf içerik lisansı eklenmiş değildir. Çıktının başka çalışmalarla benzerlik göstermeyeceğine dair münhasırlık garantisi verilmez.
