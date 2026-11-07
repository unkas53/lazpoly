# Para seti

Proje sahibinin para seti (2026-11-07): "1 L" altın jeton (`Lazpoly_Coin_1L.glb`, 10.586 üçgen) ve "100 L" banknot
(`Lazpoly_Banknote_100L.glb`, 30.100 üçgen). Modeller bir yapay zekâ aracıyla kodla üretildi (`Source/build_currency.mjs`,
three.js 0.187, MIT, `Source/THREE_LICENSE.txt`); yazılar `Source/font.json` yazı tipinden geometri olarak çıkarıldı
(MgOpen, `Source/FONT_LICENSE.txt`: daha büyük bir yazılımın parçası olarak satılabilir). Logo yapay zekâ görüntü aracıyla
yeniden çizildi (`Source/GENERATING_NOTES.md`) ve GLB'lere gömülüdür.

Oyuna almak için depo kökünden:

    python3 Tools/Art/make_money.py

Araç parçaları renkleriyle (`money.bytes`) ve logoyu saydam PNG olarak (`money_logo.bytes`) `Resources/Lazpoly/Models`
altına yazar. Setin ölçüleri korunur (jeton 1,304 birim çapında, banknot 2,8 x 1,36); oyun onları masaya göre küçültür.
Yazıları değiştirmek için `Source` altında `npm install` ve `node build_currency.mjs <çıkış klasörü>` ile GLB'leri yeniden
üretip buraya koyun.

Steam yapay zekâ beyanına girer (Steam/README.md).
