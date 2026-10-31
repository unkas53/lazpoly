# Lazpoly

Steam için 3D, 2-8 oyunculu çevrim içi masa oyunu. Mimari ve yol haritası:
https://claude.ai/artifact/3hqmWm5rQuN5tmmuMwSXNn

## Durum: Faz 0 (proje iskeleti)

Bu klasör yeni bir Unity projesinin içine kopyalanacak dosyaları içerir:

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
| `Assets/_Lazpoly/Tests/EditMode` | Core ve Infra birim testleri. |
| `steam_appid.txt` | Geliştirme için Spacewar (480). |

Bağımlılık yönü: Presentation → App → Core. Core hiçbir modülü bilmez. Boot en üstteki
bileşim köküdür (composition root) ve Infra ile Platform'u birbirine bağlar.

Mimari belgesinden bir sapma: ayrı bir `Boot` sahnesi yok. Açılış
`[RuntimeInitializeOnLoadMethod]` ile çalışır, böylece hangi sahnede Play'e basılırsa
basılsın servisler aynı şekilde kurulur.

## Kurulum

1. Unity Hub'da **Unity 6 LTS** ile **Universal 3D (URP)** şablonundan `Lazpoly` adlı yeni bir proje oluştur.
2. Bu klasördeki `Assets/_Lazpoly`, `steam_appid.txt`, `.gitignore` ve `README.md` dosyalarını proje köküne kopyala.
3. Unity'de **Window > Package Manager > + > Install package from git URL** ile şunu ekle:
   `https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2025.164.1`
4. Package Manager'da **Test Framework** paketinin kurulu olduğunu kontrol et (URP şablonunda varsayılan olarak gelir).
5. Menüden **Lazpoly > Create Scenes and Build Settings** çalıştır. `MainMenu`, `Lobby`, `Game` ve
   `Sandbox_Board` sahneleri oluşur; ilk üçü Build Settings'e eklenir.

## Faz 0 kapısı (kontrol listesi)

- [ ] Proje hatasız derleniyor (Console'da kırmızı hata yok).
- [ ] **Window > General > Test Runner > EditMode > Run All**: tüm testler yeşil.
- [ ] Steam açıkken Play'e basınca Console'da `[Steam] Initialized as '<adın>' (App ID 480).` görünüyor.
- [ ] Bir Windows build alıp `steam_appid.txt` dosyasını exe'nin yanına koyunca Shift+Tab ile Steam overlay açılıyor.
  (Overlay Unity editöründe güvenilir çalışmaz; bu yüzden build ile denenir.)

## Kurallar

Modüler sistemler, tek sorumluluk, durum değişikliklerini yalnızca host doğrular, her aşama test edilir,
gereksiz paket yok, onay olmadan sonraki faza geçilmez.
