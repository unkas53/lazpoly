# Kart resimleri: kaynak

Ekranın ortasında açılan kartların resimleri (`Assets/_Lazpoly/UI/Resources/Lazpoly/Art/Cards/*.bytes`)
`Tools/Art/make_card_art.js` ile çizilir. Her resim kod ile kurulan bir SVG'dir: renkli bir bant, yumuşak daireler,
bir sıra ev, beyaz bir madalyon ve içinde çizgi ikonlar. Hiçbiri yapay zekâ ile yapılmadı; Steam'in yapay zekâ
beyanına girmez.

## İkonlar

- Tabler Icons 3.19.0 (https://tabler.io/icons), MIT lisansı; lisans metni `tabler/LICENSE`.
- npm paketinden alındı (https://registry.npmjs.org/@tabler/icons/-/icons-3.19.0.tgz), 2026-11-09.
- Yalnızca kullanılan ikonlar `tabler/` içinde (outline, 24x24 SVG).

## Yeniden çizmek

Depo kökünden: `node Tools/Art/make_card_art.js`

Playwright ve onunla gelen Chromium gerekir. Var olan bir dosyanın `.meta`'sı yeniden yazılmaz, Unity'nin kimlikleri
korunur. Kart renkleri `CardArt.ToneColor` ile aynı tutulmalıdır.
