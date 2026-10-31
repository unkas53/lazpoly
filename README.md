# Lazpoly

Steam için 3D, 2-8 oyunculu çevrim içi masa oyunu. Mimari ve yol haritası:
https://claude.ai/artifact/3hqmWm5rQuN5tmmuMwSXNn

## Durum: Faz 0 (proje iskeleti) tamamlandı

Doğrulama (2026-10-31, Unity 6000.6.7f1, Windows): derleme hatasız, 14/14 EditMode testi geçti,
Windows derlemesinde Steam bağlandı (`[Steam] Initialized as ... (App ID 480)`).

| Yol | İçerik |
| --- | --- |
| `Assets/_Lazpoly/Code/Core` | `Lazpoly.Core`: saf C#, Unity referansı yok. Şimdilik tohumlu RNG (`Pcg32Random`). |
| `Assets/_Lazpoly/Code/Application` | `Lazpoly.App`: boş, Faz 2'de dolacak. |
| `Assets/_Lazpoly/Code/Networking` | `Lazpoly.Net`: boş, Faz 3'te dolacak. |
| `Assets/_Lazpoly/Code/Infrastructure` | `Lazpoly.Infra`: servis kaydı ve yaşam döngüsü. |
| `Assets/_Lazpoly/Code/Platform` | `Lazpoly.Platform`: Steamworks.NET'e dokunan tek modül (`SteamService`). |
| `Assets/_Lazpoly/Code/Boot` | `Lazpoly.Boot`: açılış; servisleri ilk sahneden önce kurar. |
| `Assets/_Lazpoly/Code/Presentation` | `Lazpoly.Presentation`: boş, Faz 2'de dolacak. |
| `Assets/_Lazpoly/Code/Editor` | `Lazpoly > Create Scenes and Build Settings` menüsü. |
| `Assets/_Lazpoly/Scenes` | `MainMenu`, `Lobby`, `Game` (Build Settings'te) ve `Sandbox_Board` (yalnızca geliştirme). |
| `Assets/_Lazpoly/Tests/EditMode` | Core ve Infra birim testleri. |
| `steam_appid.txt` | Geliştirme için Spacewar (480). |

Bağımlılık yönü: Presentation → App → Core. Core hiçbir modülü bilmez. Boot en üstteki
bileşim köküdür (composition root) ve Infra ile Platform'u birbirine bağlar.

Mimari belgesinden bir sapma: ayrı bir `Boot` sahnesi yok. Açılış
`[RuntimeInitializeOnLoadMethod]` ile çalışır, böylece hangi sahnede Play'e basılırsa
basılsın servisler aynı şekilde kurulur.

Paketler bilerek az tutuldu: Steamworks.NET (`2025.164.1`), URP, Test Framework, uGUI ve IDE paketleri.

## Kurulum

1. Unity Hub'a **Unity 6000.6.7f1** kur.
2. Depoyu klonla ve Unity Hub'da **Add > Add project from disk** ile klasörü aç.
   Steamworks.NET bir git paketi olduğu için bilgisayarda Git kurulu olmalı.
3. Steam açık ve giriş yapılmış olsun.

## Testler ve kontrol

- **Window > General > Test Runner > EditMode > Run All**: tüm testler yeşil olmalı.
- Komut satırından:
  `Unity.exe -batchmode -projectPath <klasör> -runTests -testPlatform EditMode -testResults results.xml -logFile editor.log`
- Steam: bir Windows derlemesi al, `steam_appid.txt` dosyasını exe'nin yanına koy, çalıştır;
  `Player.log` içinde `[Steam] Initialized as` satırı görünmeli. Editörde Play'e basınca da Console'da aynı satır çıkar.

## Faz 2: yerel prototip

- **Oynamak:** `MainMenu` sahnesini aç, Play'e bas, 2-8 oyuncu seç, "Oyunu Başlat". `Game` sahnesi doğrudan
  açılırsa iki varsayılan oyuncuyla başlar.
- **Tahta ve ekonomi verisi:** `Assets/_Lazpoly/Data/Resources/Lazpoly/board.json`. Kare isimleri, fiyatlar,
  kiralar, vergiler, gruplar ve başlangıç parası buradan okunur; kod değiştirmeden düzenlenebilir.
  Şu anki değerler taslaktır.
- **Sahne kurulumu:** `Lazpoly > Add Scene Controllers` menüsü MainMenu ve Game sahnelerine kök bileşenleri ekler.
- **Geçici arayüz:** Menü ve oyun arayüzü IMGUI ile çizilen prototiptir (`PrototypeHud`, `MainMenuController`);
  Faz 6'da asıl arayüzle değiştirilecek. Tahta ve piyonlar da Blender modelleri gelene kadar basit şekillerdir.

## Kurallar

Modüler sistemler, tek sorumluluk, durum değişikliklerini yalnızca host doğrular, her aşama test edilir,
gereksiz paket yok, onay olmadan sonraki faza geçilmez.
