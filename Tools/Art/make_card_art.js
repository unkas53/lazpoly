#!/usr/bin/env node
/*
 * Draws the pictures on the cards that open in the middle of the game screen (the owner's wish, 2026-11-09): one for each
 * kind of Şans card and one for each of the table's own cards (a trade, a debt, the third double, a trade agreement, the
 * champions' cup), plus the card's face and its soft shadow.
 *
 * Each picture is a flat illustration with no words in it, so it serves every language: a rounded band in the card's
 * colour (red for attacks, petrol for protection, green for money, violet for moves...), soft circles and a row of
 * houses in a deeper shade, a white medallion with the card's line icon in the middle and two smaller icons beside it.
 * The icons are Tabler Icons (MIT, Tools/Art/Cards/tabler, see KAYNAK.md); nothing here is made by an AI.
 *
 * The pictures are laid out as SVG and photographed with the Chromium that comes with Playwright, then written as PNG
 * bytes that ship as TextAssets (read by Lazpoly.Presentation.Game.CardArt):
 *     Assets/_Lazpoly/UI/Resources/Lazpoly/Art/Cards/<name>.bytes
 * A .meta is written only for a new file, so running the script again keeps Unity's ids.
 * Run from the repository root: node Tools/Art/make_card_art.js
 */
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const { chromium } = require('playwright');

const ICONS = path.join('Tools', 'Art', 'Cards', 'tabler');
const TARGET = path.join('Assets', '_Lazpoly', 'UI', 'Resources', 'Lazpoly', 'Art', 'Cards');

// The band is 8:3 (shown 512 x 192 on a 1080p screen); drawn at twice that so it stays sharp at 4K and a bigger interface.
const WIDTH = 1040;
const HEIGHT = 390;
const RADIUS = 44;

// The card colours: [light, deep]. Kept in step with CardArt.ToneColor in the game.
const TONES = {
  attack: ['#d6503f', '#9c2f2a'],
  defence: ['#2d7d93', '#174959'],
  money: ['#3a9d63', '#1f6b43'],
  cost: ['#e0913a', '#a85f1c'],
  move: ['#6a58c2', '#41338a'],
  cup: ['#e2ad37', '#a8790f'],
  jail: ['#58657a', '#2f3847'],
  trade: ['#2aa093', '#17685f'],
  grey: ['#8a8f99', '#555b66'],
};

// name: [tone, main icon, left icon, right icon]. The names are the ones CardArt asks for.
const PICTURES = {
  // Şans cards, by kind.
  collect: ['money', 'coins', 'cash', 'star'],
  pay: ['cost', 'receipt', 'cash-off', 'coins'],
  moveTo: ['move', 'map-pin', 'route', 'flag'],
  moveBy: ['move', 'walk', 'route', 'arrow-big-right-lines'],
  repairs: ['cost', 'tool', 'hammer', 'home'],
  goToJail: ['jail', 'lock', 'fence', 'alert-triangle'],
  lookout: ['move', 'binoculars', 'eye', 'dice-5'],
  lawyer: ['defence', 'gavel', 'scale', 'key'],
  rentShield: ['defence', 'shield-dollar', 'home', 'cash-off'],
  chauffeur: ['move', 'car', 'map-pin', 'route'],
  cupTransfer: ['cup', 'trophy', 'ball-football', 'star'],
  landDeal: ['money', 'rosette-discount', 'home', 'tag'],
  renewal: ['money', 'crane', 'building-skyscraper', 'hammer'],
  fund: ['money', 'pig-money', 'chart-line', 'coins'],
  shield: ['defence', 'shield-check', 'shield', 'star'],
  strike: ['attack', 'speakerphone', 'hand-stop', 'cash-off'],
  audit: ['attack', 'report-search', 'receipt', 'zoom-money'],
  demolish: ['attack', 'bulldozer', 'home-x', 'alert-triangle'],
  illegalFloor: ['attack', 'stairs-down', 'building', 'hammer'],
  forceSale: ['attack', 'home-dollar', 'tag', 'coins'],
  // The table's own cards.
  tradeAccepted: ['trade', 'heart-handshake', 'arrows-exchange', 'check'],
  tradeRejected: ['grey', 'arrows-exchange', 'hand-stop', 'x'],
  debt: ['cost', 'cash-off', 'alert-triangle', 'receipt'],
  thirdDouble: ['jail', 'dice-5', 'lock', 'alert-triangle'],
  groupDeal: ['money', 'writing-sign', 'building-community', 'heart-handshake'],
  cup: ['cup', 'trophy', 'confetti', 'star'],
  shieldHeld: ['defence', 'shield-check', 'hand-stop', 'star'],
  takeover: ['trade', 'key', 'building', 'arrows-exchange'],
};

/** The icon's inner SVG (its paths), with the empty frame path Tabler puts first left out. */
function iconBody(name) {
  const file = path.join(ICONS, `${name}.svg`);
  const svg = fs.readFileSync(file, 'utf8');
  const inner = svg.replace(/^[\s\S]*?<svg[^>]*>/, '').replace(/<\/svg>\s*$/, '');
  return inner.replace(/<path stroke="none" d="M0 0h24v24H0z" fill="none"\s*\/>/g, '');
}

/** A line icon placed at (x, y), size s, in colour c, its strokes w wide (in the icon's 24-unit grid). */
function icon(name, x, y, s, c, w, extra = '') {
  return `<g transform="translate(${x - s / 2} ${y - s / 2}) scale(${s / 24})" fill="none" stroke="${c}" stroke-width="${w}"
    stroke-linecap="round" stroke-linejoin="round" ${extra}>${iconBody(name)}</g>`;
}

/** A row of houses and towers along the bottom, the same for every card, so the cards read as one set. */
function skyline(colour) {
  const blocks = [
    [0, 90], [60, 140], [120, 70], [170, 180], [230, 110], [300, 150], [360, 80], [410, 200], [480, 120],
    [540, 160], [610, 90], [660, 190], [730, 130], [790, 75], [840, 170], [910, 105], [970, 145],
  ];
  return blocks.map(([x, h], i) => {
    const w = 62 + (i % 3) * 6;
    const roof = i % 4 === 1 ? `<polygon points="${x},${HEIGHT - h} ${x + w / 2},${HEIGHT - h - 34} ${x + w},${HEIGHT - h}" fill="${colour}"/>` : '';
    return `<rect x="${x}" y="${HEIGHT - h}" width="${w}" height="${h}" fill="${colour}"/>${roof}`;
  }).join('');
}

function picture(name, [tone, main, left, right]) {
  const [light, deep] = TONES[tone];
  const cx = WIDTH / 2;
  const cy = HEIGHT / 2 - 6;
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${WIDTH}" height="${HEIGHT}" viewBox="0 0 ${WIDTH} ${HEIGHT}">
  <defs>
    <linearGradient id="ground" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="${light}"/><stop offset="1" stop-color="${deep}"/>
    </linearGradient>
    <radialGradient id="glow" cx="0.5" cy="0.45" r="0.5">
      <stop offset="0" stop-color="#ffffff" stop-opacity="0.35"/><stop offset="1" stop-color="#ffffff" stop-opacity="0"/>
    </radialGradient>
    <filter id="lift" x="-30%" y="-30%" width="160%" height="160%">
      <feDropShadow dx="0" dy="10" stdDeviation="14" flood-color="#000000" flood-opacity="0.28"/>
    </filter>
    <clipPath id="band"><rect width="${WIDTH}" height="${HEIGHT}" rx="${RADIUS}" ry="${RADIUS}"/></clipPath>
  </defs>
  <g clip-path="url(#band)">
    <rect width="${WIDTH}" height="${HEIGHT}" fill="url(#ground)"/>
    <circle cx="${WIDTH * 0.1}" cy="${HEIGHT * 0.12}" r="150" fill="#ffffff" opacity="0.08"/>
    <circle cx="${WIDTH * 0.9}" cy="${HEIGHT * 0.2}" r="110" fill="#ffffff" opacity="0.07"/>
    <circle cx="${WIDTH * 0.78}" cy="${HEIGHT * 1.02}" r="190" fill="#000000" opacity="0.08"/>
    <g opacity="0.22">${skyline(deep)}</g>
    <ellipse cx="${cx}" cy="${cy}" rx="330" ry="210" fill="url(#glow)"/>
    ${dots(light)}
    ${icon(left, cx - 285, cy + 18, 116, '#ffffff', 1.6, 'opacity="0.55"')}
    ${icon(right, cx + 285, cy + 18, 116, '#ffffff', 1.6, 'opacity="0.55"')}
    <g filter="url(#lift)">
      <circle cx="${cx}" cy="${cy}" r="132" fill="#fffdf6"/>
    </g>
    <circle cx="${cx}" cy="${cy}" r="132" fill="none" stroke="#e9ce8b" stroke-width="8"/>
    <circle cx="${cx}" cy="${cy}" r="112" fill="none" stroke="${light}" stroke-width="2" opacity="0.35"/>
    ${icon(main, cx, cy, 150, deep, 1.7)}
  </g>
</svg>`;
}

/** A few sparkles scattered over the band, in the same places on every card. */
function dots() {
  const spots = [[140, 250, 5], [215, 95, 4], [330, 300, 3], [700, 80, 4], [820, 270, 5], [905, 110, 3], [610, 320, 3], [450, 70, 3]];
  return spots.map(([x, y, r]) => `<circle cx="${x}" cy="${y}" r="${r}" fill="#ffffff" opacity="0.55"/>`).join('');
}

/** The card's face: cream, rounded, a thin warm edge, cut as a 9-slice (its corners are 64 px). */
function face() {
  return `<svg xmlns="http://www.w3.org/2000/svg" width="192" height="192" viewBox="0 0 192 192">
  <rect x="2" y="2" width="188" height="188" rx="46" ry="46" fill="#fffcf3" stroke="#e3d6b8" stroke-width="3"/>
  <rect x="9" y="9" width="174" height="174" rx="40" ry="40" fill="none" stroke="#ffffff" stroke-width="2" opacity="0.8"/>
</svg>`;
}

/** The soft shadow under the card, a 9-slice too: a blurred dark rounded square on a clear ground. */
function shadow() {
  return `<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
  <defs><filter id="blur" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="22"/></filter></defs>
  <rect x="64" y="70" width="128" height="128" rx="46" ry="46" fill="#1d1a14" opacity="0.55" filter="url(#blur)"/>
</svg>`;
}

function writeAsset(name, png) {
  fs.mkdirSync(TARGET, { recursive: true });
  const file = path.join(TARGET, `${name}.bytes`);
  fs.writeFileSync(file, png);
  const meta = `${file}.meta`;
  if (!fs.existsSync(meta)) {
    const guid = crypto.randomBytes(16).toString('hex');
    fs.writeFileSync(meta, `fileFormatVersion: 2\nguid: ${guid}\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`);
  }
}

async function shoot(page, svg, width, height) {
  await page.setViewportSize({ width, height });
  await page.setContent(`<!doctype html><html><body style="margin:0;background:transparent">${svg}</body></html>`);
  return page.screenshot({ omitBackground: true, clip: { x: 0, y: 0, width, height } });
}

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ deviceScaleFactor: 1 });
  for (const [name, spec] of Object.entries(PICTURES)) {
    writeAsset(`Card_${name}`, await shoot(page, picture(name, spec), WIDTH, HEIGHT));
  }

  writeAsset('Card_Face', await shoot(page, face(), 192, 192));
  writeAsset('Card_Shadow', await shoot(page, shadow(), 256, 256));
  await browser.close();
  console.log(`${Object.keys(PICTURES).length + 2} pictures in ${TARGET}`);
})();
