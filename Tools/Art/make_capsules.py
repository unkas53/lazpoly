"""Cash Father: Steam mağaza görsellerini ve logo dosyalarını üretir.

Logo proje sahibinin resmidir (Tools/Art/Logo/cash-father-logo.png, kaynağı Logo/KAYNAK.md); burada yalnızca
kırpılır, küçültülür ve logonun gece şehri, altın ve siyah renklerinde kod ile çizilen bir zeminin üstüne konur:
koyu bir gece göğü, logonun arkasında yumuşak altın bir parıltı ve altta ışıklı pencereli bir şehir silueti.
Steam kuralı gereği library_hero ve page_background üstünde logo ya da yazı yoktur (Steam logoyu kendisi koyar).

Kullanım:  python Tools/Art/make_capsules.py [çıktı klasörü]
Varsayılan çıktı: Steam/Art
"""
import os
import random
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_logo  # noqa: E402

FONT = os.path.join(ROOT, "Assets", "_Lazpoly", "UI", "Resources", "Lazpoly", "Fonts", "Nunito-Bold.ttf")
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "Steam", "Art")

NIGHT_TOP = (8, 9, 14)
NIGHT_LOW = (30, 26, 34)
GOLD = (240, 186, 72)
TOWER = (18, 18, 24)
WINDOW = (255, 206, 110)
CREAM = (250, 236, 200)
TAGLINE = "Dünya şehirlerinde mülk oyunu"


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def night(w, h):
    img = Image.new("RGBA", (w, h))
    d = ImageDraw.Draw(img)
    for y in range(h):
        d.line([(0, y), (w, y)], fill=lerp(NIGHT_TOP, NIGHT_LOW, y / max(h - 1, 1)))
    return img


def glow(img, cx, cy, rx, ry, strength=110):
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(layer).ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=GOLD + (strength,))
    img.alpha_composite(layer.filter(ImageFilter.GaussianBlur(max(rx, ry) * 0.45)))


def skyline(img, base, height, seed):
    """A row of dark towers along the bottom with a few lit windows; the same seed draws the same city."""
    w, h = img.size
    rng = random.Random(seed)
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    x = -rng.uniform(0, height * 0.2)
    while x < w:
        tw = height * rng.uniform(0.12, 0.26)
        th = height * rng.uniform(0.35, 1.0)
        top = base - th
        d.rectangle([x, top, x + tw, h], fill=TOWER + (255,))
        if rng.random() < 0.3:
            d.rectangle([x + tw * 0.4, top - th * 0.12, x + tw * 0.6, top], fill=TOWER + (255,))
        step = max(3.0, height * 0.035)
        wy = top + step
        while wy < base - step:
            wx = x + step * 0.8
            while wx < x + tw - step:
                if rng.random() < 0.28:
                    d.rectangle([wx, wy, wx + step * 0.45, wy + step * 0.55], fill=WINDOW + (rng.randint(120, 230),))
                wx += step
            wy += step * 1.1
        x += tw + height * rng.uniform(0.0, 0.05)
    img.alpha_composite(layer)


def place_logo(img, logo, cx, cy, height):
    scale = height / logo.height
    if logo.width * scale > img.width * 0.94:
        scale = img.width * 0.94 / logo.width
    shrunk = logo.resize((max(1, round(logo.width * scale)), max(1, round(logo.height * scale))), Image.LANCZOS)
    img.alpha_composite(shrunk, (round(cx - shrunk.width / 2), round(cy - shrunk.height / 2)))
    return shrunk.size


def tagline(img, cx, y, size):
    d = ImageDraw.Draw(img)
    f = ImageFont.truetype(FONT, int(size))
    left, top, right, bottom = d.textbbox((0, 0), TAGLINE, font=f)
    d.text((cx - (right - left) / 2 - left, y - top), TAGLINE, font=f, fill=CREAM)


def capsule(logo, w, h, logo_at=None, logo_height=0.0, with_tagline=False, seed=1):
    """Drawn at double size and shrunk, so edges stay smooth. logo_at None: no logo (hero, page background)."""
    s = 2
    W, H = w * s, h * s
    img = night(W, H)
    cx, cy = (W * logo_at[0], H * logo_at[1]) if logo_at else (W * 0.5, H * 0.45)
    glow(img, cx, cy, W * 0.42, H * 0.36)
    skyline(img, H * 1.0, H * (0.32 if logo_at else 0.42), seed)
    if logo_at:
        _, lh = place_logo(img, logo, cx, cy, H * logo_height)
        if with_tagline:
            tagline(img, cx, cy + lh / 2 + H * 0.02, max(16, H * 0.045))
    return img.resize((w, h), Image.LANCZOS).convert("RGB")


def main():
    os.makedirs(OUT, exist_ok=True)
    logo = make_logo.load_logo(ROOT)
    capsule(logo, 460, 215, (0.5, 0.5), 0.9).save(os.path.join(OUT, "header_capsule.png"))
    capsule(logo, 231, 87, (0.5, 0.5), 0.94).save(os.path.join(OUT, "small_capsule.png"))
    capsule(logo, 616, 353, (0.5, 0.44), 0.74, with_tagline=True).save(os.path.join(OUT, "main_capsule.png"))
    capsule(logo, 600, 900, (0.5, 0.36), 0.42, with_tagline=True, seed=2).save(os.path.join(OUT, "library_capsule.png"))
    capsule(logo, 3840, 1240, seed=3).save(os.path.join(OUT, "library_hero.png"))
    capsule(logo, 1438, 810, seed=4).save(os.path.join(OUT, "page_background.png"))
    # Library logo: the logo alone on a see-through ground (Steam lays it over the hero).
    lg = Image.new("RGBA", (1280, 720), (0, 0, 0, 0))
    place_logo(lg, logo, 640, 360, 700)
    lg.save(os.path.join(OUT, "library_logo.png"))
    # The app icons: the logo's character, as in the game's own icon.
    make_logo.character(ROOT, make_logo.ICON_BOX, 1024).save(os.path.join(OUT, "logo_1024.png"))
    make_logo.character(ROOT, make_logo.ICON_BOX, 256).save(os.path.join(OUT, "logo_256.png"))
    print("yazıldı:", OUT)


if __name__ == "__main__":
    main()
