# Lazpoly — Beyaz Bina Seviyeleri

Altı bağımsız, tek renk 3D model: arsa, dört farklı ev seviyesi ve otel. Yuvarlatılmış kenarlar, gömme pencereler, balkonlar, kat silmeleri ve otel giriş sundurması gerçek geometridir. Kaplama resmi gerekmez.

## Unity'ye ekleme

1. Bu paketteki `Assets/LazpolyBuildings` klasörünü Unity projenizin `Assets` klasörüne kopyalayın.
2. Script derlemesi tamamlanınca **Tools > Lazpoly Buildings > Create 6 Prefabs** menüsünü çalıştırın.
3. Oluşan `Generated/Prefabs` klasöründeki modelleri sahneye sürükleyin.

Kurulum Unity'nin kendi Mesh/Prefab API'lerini kullanır; GLB eklentisi gerektirmez. Built-in, URP ve HDRP için uygun Lit shader'ı seçer. Her çalıştırmada yeni bir Generated klasörü açar, önceki materyal ve prefab düzenlemelerini korur.

| Aşama | Model | Tasarım |
|---|---|---|
| 0 | Plot_00 | Sınırları belli boş arsa ve boş satış tabelası |
| 1 | House_01 | Tek katlı, bacalı küçük ev |
| 2 | House_02 | İki katlı, balkonlu ev |
| 3 | House_03 | Üç katlı, çok balkonlu konut |
| 4 | House_04 | Dört katlı, çatı teraslı apartman |
| 5 | Hotel_05 | İki yan kanatlı, kolonlu girişli ve H tabelalı otel |

## Renk ve yerleştirme

- Bütün modeller **Lazpoly_White.mat** materyalini paylaşır. Base Map/Base Color veya Albedo rengini değiştirin.
- Farklı oyuncu renkleri için materyali çoğaltıp ilgili modelin MeshRenderer bileşenine atayın. Paylaşılan orijinal materyali değiştirirseniz bütün modellerin rengi değişir.
- Taban 0,90 × 0,72 Unity birimidir. Pivot zeminin merkezindedir, Y yukarıdır. Prefabları aynı position/rotation/scale değerlerinde değiştirerek seviye yükseltebilirsiniz.
- Her prefab bir mesh, bir materyal ve basit BoxCollider içerir. Kutusal collider seçim/tıklama içindir; mimari yüzeyleri birebir takip etmez.
- İhtiyaca göre tamamını aynı ölçekle küçültün. Board paketindeki evlerin yerine yerleştirme ve satın alma/seviye mantığı oyun kodunuza aittir.

## Dosyalar

- `Assets/LazpolyBuildings`: Unity mesh verisi ve prefab oluşturma aracı.
- `Models`: Her bina için bağımsız OBJ ve GLB; OBJ'lerle aynı yerde ortak MTL bulunur. OBJ dosyaları Unity'ye doğrudan da alınabilir.
- `Source/Lazpoly_Buildings.blend`: Düzenlenebilir Blender kaynak sahnesi.
- `Source/Lazpoly_Buildings.glb`: Altı modeli yan yana gösteren kaynak sahne.
- `Lazpoly_Buildings_White.png`: Gerçek modellerden alınmış önizleme.
- `Validation.json`: Her modelin üçgen sayısı, sınırları ve dışa aktarım kontrolleri.

Modellerin renkleri geometriye işlenmemiştir; dışa aktarılan normaller korunur. Kaynakta parçalar düzenlenebilir; Unity prefabında performans için her bina tek mesh halinde birleştirilir. LOD ve ışık haritası için özel ikinci UV seti dahil değildir.

Kontrol: Blender renderı ve dışa aktarılan mesh verisi incelendi. Unity Editor bu ortamda çalıştırılmadığından Unity içi import/derleme testi yapılmadı.
