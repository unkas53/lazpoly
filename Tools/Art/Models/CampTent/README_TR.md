# Unity için kamp çadırı

Zeytin yeşili A tipi çadır; açık giriş, iç taban, kumaş kalınlığı, alüminyum direkler, gergi ipleri, kazıklar ve turuncu dokuma detayları.

## Kurulum

1. Bu paketteki `Assets/CampTent` klasörünü Unity projenizin `Assets` klasörüne kopyalayın.
2. Derleme tamamlanınca **Tools > Camp Tent > Create Prefab** menüsünü çalıştırın.
3. Oluşan `Assets/CampTent/Generated/CampTent.prefab` dosyasını sahneye sürükleyin. Araç tekrar çalıştırılırsa yeni bir Generated klasörü açılır; önceki prefab korunur.

Editör aracı Built-in veya URP için altı malzemeyi oluşturur, UV2 üretimini açar ve kumaş/tabana statik MeshCollider ekler. Giriş açık kalır. Statik dekor olarak kullanın; oluşturulan non-convex collider'lara dinamik Rigidbody eklemeyin. HDRP kullanıyorsanız OBJ'yi alıp HDRP/Lit malzemelerini elle atayın.

## Dosyalar ve ölçek

- `Assets/CampTent/Models/CampTent.obj`: Unity için ana model; Y yukarı, metre ölçeği, zemin merkezinde pivot.
- `Assets/CampTent/Models/CampTent.mtl`: renk ve malzeme tanımları; OBJ ile aynı klasörde kalmalı.
- `Assets/CampTent/Models/model-info.json`: kesin üçgen sayısı, sınırlar ve malzeme değerleri.
- `Source/CampTent.glb`: alternatif taşınabilir model; Unity'de ek bir glTF içe aktarıcısı gerektirir. Unity kurulumu için OBJ'yi kullanın.
- `Source/CampTent.blend`: ayrı parçalar halinde düzenlenebilir Blender kaynağı; önizleme kamerasını ve ışıklarını içerir. Doğrudan Unity Assets içine koymanız gerekmez.
- `CampTent_preview.png`: gerçek modelden render alınmış önizleme.

Model **1.436 üçgen**, 2.616 dışa aktarılmış vertex ve 6 malzeme içerir. OBJ, malzemeye göre 6 parçada birleştirilmiştir. Kumaş gövde yaklaşık **2,6 m genişlik × 3,2 m uzunluk × 1,85 m yükseklik**. İpler ve kazıklarla toplam alan yaklaşık **4,11 × 3,73 m** olur. Ana UV'ler mevcuttur; renkler düz PBR malzemelerdir, harici doku dosyası yoktur. Animasyon ve LOD içermez.

## Kontrol kapsamı

Model Blender'da görsel olarak incelendi. OBJ dönüşümünde koordinatlar, yüz indeksleri, UV'ler ve sıfır olmayan üçgen alanları denetlendi. Bu bilgisayarda Unity Editor bulunmadığından prefab oluşturma aracını Unity içinde çalıştırarak doğrulamak mümkün olmadı.

Editör API başvuruları: [ModelImporter](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityeditor/modelimporter), [PrefabUtility kaynak kodu](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/Prefabs/PrefabUtility.cs).
