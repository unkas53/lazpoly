"""Lazpoly logo ve Steam mağaza görsellerini üretir.

Dışarıdan resim kullanmaz: akşamüstü deniz, fener, martılar ve masadaki tahta çizilerek
her boyut aynı sahneden kesilir. Yazı tipi oyundaki Nunito (OFL).

Kullanım:  python Tools/Art/make_capsules.py [çıktı klasörü]
Varsayılan çıktı: Steam/Art
"""
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
FONT = os.path.join(ROOT, "Assets", "_Lazpoly", "UI", "Resources", "Lazpoly", "Fonts", "Nunito-Bold.ttf")
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "Steam", "Art")

# Oyun içi paletle aynı: gece lacivert, kum, gün batımı turuncusu, turkuaz.
NAVY = (13, 23, 33)
DUSK = (52, 44, 78)
AMBER = (250, 178, 61)
SUNSET = (242, 118, 72)
SAND = (245, 237, 219)
SEA = (31, 102, 138)
SEA_DEEP = (18, 56, 84)
TEAL = (41, 148, 153)
WOOD = (92, 58, 37)
TILE = (236, 226, 200)
RED = (196, 60, 48)
WHITE = (250, 248, 240)

SIZES = {
    "header_capsule": (460, 215),
    "small_capsule": (231, 87),
    "main_capsule": (616, 353),
    "library_capsule": (600, 900),
    "library_hero": (3840, 1240),
    "page_background": (1438, 810),
}


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def font(size):
    return ImageFont.truetype(FONT, int(size))


def sky(img, horizon):
    w, h = img.size
    d = ImageDraw.Draw(img)
    for y in range(horizon):
        t = y / max(horizon - 1, 1)
        colour = lerp(NAVY, DUSK, t * 1.6) if t < 0.6 else lerp(DUSK, SUNSET, (t - 0.6) / 0.4)
        d.line([(0, y), (w, y)], fill=colour)
    for y in range(horizon, h):
        t = (y - horizon) / max(h - horizon - 1, 1)
        d.line([(0, y), (w, y)], fill=lerp(SEA, SEA_DEEP, t))


def sun(img, cx, cy, r):
    glow = Image.new("RGBA", img.size, (0, 0, 0, 0))
    g = ImageDraw.Draw(glow)
    g.ellipse([cx - r * 3, cy - r * 3, cx + r * 3, cy + r * 3], fill=AMBER + (90,))
    glow = glow.filter(ImageFilter.GaussianBlur(r * 1.2))
    img.alpha_composite(glow)
    d = ImageDraw.Draw(img)
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=AMBER)
    # Sea reflection: broken streaks under the sun, on their own layer so the alpha blends.
    streaks = Image.new("RGBA", img.size, (0, 0, 0, 0))
    sd = ImageDraw.Draw(streaks)
    for i in range(8):
        y = cy + r * (1.6 + i * 0.75)
        half = r * (0.35 + i * 0.22)
        sd.line([(cx - half, y), (cx + half, y)], fill=AMBER + (150 - i * 14,), width=max(2, r // 10))
    img.alpha_composite(streaks)


def hills(img, horizon):
    w, h = img.size
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for k, (height, shade) in enumerate([(0.22, (34, 44, 66)), (0.13, (22, 36, 52))]):
        pts = [(0, horizon)]
        for x in range(0, w + 40, 40):
            y = horizon - height * horizon * (0.55 + 0.45 * math.sin(x / (w * 0.13) + k * 2.1) * math.cos(x / (w * 0.31) + k))
            pts.append((x, y))
        pts.append((w, horizon))
        d.polygon(pts, fill=shade)
    img.alpha_composite(layer)


def lighthouse(img, x, base_y, height):
    d = ImageDraw.Draw(img)
    wb, wt = height * 0.16, height * 0.11
    d.polygon([(x - wb, base_y), (x + wb, base_y), (x + wt, base_y - height), (x - wt, base_y - height)], fill=WHITE)
    for i in range(3):
        y0 = base_y - height * (0.15 + i * 0.3)
        y1 = y0 - height * 0.12
        d.polygon([(x - wb, y0), (x + wb, y0), (x + wt, y1), (x - wt, y1)], fill=RED)
    top = base_y - height
    lantern = height * 0.16
    d.rectangle([x - wt * 1.2, top - lantern, x + wt * 1.2, top], fill=NAVY)
    d.rectangle([x - wt * 0.9, top - lantern * 0.85, x + wt * 0.9, top - lantern * 0.15], fill=AMBER)
    d.polygon([(x - wt * 1.5, top - lantern), (x + wt * 1.5, top - lantern), (x, top - lantern * 1.8)], fill=RED)
    beam = Image.new("RGBA", img.size, (0, 0, 0, 0))
    b = ImageDraw.Draw(beam)
    cy = top - lantern * 0.5
    b.polygon([(x, cy), (x - height * 2.4, cy - height * 0.45), (x - height * 2.4, cy + height * 0.25)], fill=AMBER + (70,))
    beam = beam.filter(ImageFilter.GaussianBlur(height * 0.03))
    img.alpha_composite(beam)
    # Rock under the tower.
    d.ellipse([x - wb * 2.4, base_y - height * 0.08, x + wb * 2.4, base_y + height * 0.1], fill=(40, 32, 30))


def gulls(img, spots, size):
    d = ImageDraw.Draw(img)
    for (x, y) in spots:
        s = size
        d.line([(x - s, y), (x - s * 0.4, y - s * 0.55), (x, y)], fill=SAND, width=max(2, int(s * 0.18)), joint="curve")
        d.line([(x, y), (x + s * 0.4, y - s * 0.55), (x + s, y)], fill=SAND, width=max(2, int(s * 0.18)), joint="curve")


def board(img, cx, top, width, depth):
    """The game board in perspective: a cream ring of squares around a sea centre, on dark wood."""
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)

    def quad(u0, v0, u1, v1):
        # u across (0..1), v back-to-front (0 = far edge). Far edge narrower.
        def pt(u, v):
            half = width * (0.62 + 0.38 * v) / 2
            return (cx - half + half * 2 * u, top + depth * v)
        return [pt(u0, v0), pt(u1, v0), pt(u1, v1), pt(u0, v1)]

    shadow = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(shadow).polygon(quad(-0.04, -0.03, 1.04, 1.08), fill=(0, 0, 0, 140))
    layer.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(width * 0.02)))
    d.polygon(quad(-0.03, -0.02, 1.03, 1.05), fill=WOOD)
    d.polygon(quad(0, 0, 1, 1), fill=TILE)
    d.polygon(quad(0.11, 0.11, 0.89, 0.89), fill=SEA)
    # Square separators.
    n = 11
    for i in range(1, n):
        u = i / n
        p = quad(u, 0, u, 1)
        d.line([p[0], p[3]], fill=lerp(TILE, WOOD, 0.45), width=2)
    for i in range(1, n):
        v = i / n
        p = quad(0, v, 1, v)
        d.line([p[0], p[1]], fill=lerp(TILE, WOOD, 0.45), width=2)
    # Colour bands on the squares.
    bands = [TEAL, SUNSET, AMBER, (120, 90, 170), (210, 70, 90), (90, 160, 90), (60, 120, 200), (230, 180, 60)]
    for i in range(1, n - 1):
        colour = bands[i % len(bands)]
        d.polygon(quad(i / n, 0.11 - 0.025, (i + 1) / n, 0.11), fill=colour)
        d.polygon(quad(i / n, 0.89, (i + 1) / n, 0.89 + 0.025), fill=bands[(i + 3) % len(bands)])
        d.polygon(quad(0.11 - 0.025, i / n, 0.11, (i + 1) / n), fill=bands[(i + 5) % len(bands)])
        d.polygon(quad(0.89, i / n, 0.89 + 0.025, (i + 1) / n), fill=bands[(i + 1) % len(bands)])
    # Island in the sea.
    island = quad(0.42, 0.44, 0.62, 0.66)
    d.ellipse([island[0][0], island[0][1], island[2][0], island[2][1]], fill=(206, 184, 130))
    d.ellipse([island[0][0] + 6, island[0][1] + 5, island[2][0] - 6, island[2][1] - 6], fill=(96, 142, 78))
    # Houses and a mansion.
    for (u, v, colour, size) in [(0.5, 0.52, RED, 0.035), (0.56, 0.58, RED, 0.03), (0.47, 0.6, WOOD, 0.045)]:
        q = quad(u, v, u + size, v + size)
        d.polygon(q, fill=colour)
        d.polygon([q[0], q[1], ((q[0][0] + q[1][0]) / 2, q[0][1] - width * size * 0.6)], fill=(60, 40, 35))
    # Pawns: a tea glass and a boat as simple silhouettes.
    for (u, v, colour) in [(0.14, 0.3, TEAL), (0.86, 0.5, AMBER), (0.3, 0.95, SUNSET), (0.6, 0.05, (190, 90, 190))]:
        q = quad(u, v, u + 0.05, v + 0.05)
        x, y = (q[0][0] + q[2][0]) / 2, (q[0][1] + q[2][1]) / 2
        r = width * 0.02
        d.ellipse([x - r, y - r * 0.5, x + r, y + r * 0.5], fill=(0, 0, 0, 90))
        d.rounded_rectangle([x - r * 0.7, y - r * 2.2, x + r * 0.7, y], radius=r * 0.4, fill=colour)
        d.ellipse([x - r * 0.9, y - r * 3.2, x + r * 0.9, y - r * 1.6], fill=colour)
    img.alpha_composite(layer)


def title(img, cx, cy, size, tagline=True, align="center"):
    d = ImageDraw.Draw(img)
    f = font(size)
    text = "LAZPOLY"
    bbox = d.textbbox((0, 0), text, font=f)
    tw, th = bbox[2] - bbox[0], bbox[3] - bbox[1]
    x = cx - tw / 2 if align == "center" else cx
    y = cy - th / 2
    shadow = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(shadow).text((x - bbox[0] + size * 0.04, y - bbox[1] + size * 0.06), text, font=f, fill=(0, 0, 0, 170))
    img.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(size * 0.04)))
    d.text((x - bbox[0], y - bbox[1]), text, font=f, fill=SAND)
    d.rounded_rectangle([x, y + th + size * 0.12, x + tw, y + th + size * 0.12 + size * 0.07], radius=size * 0.03, fill=AMBER)
    if tagline:
        f2 = font(size * 0.3)
        tag = "Dünya şehirlerinde mülk oyunu"
        b2 = d.textbbox((0, 0), tag, font=f2)
        tx = cx - (b2[2] - b2[0]) / 2 if align == "center" else cx
        d.text((tx - b2[0], y + th + size * 0.32), tag, font=f2, fill=SAND)


def scene(w, h, title_size, title_at, board_at, tagline=True, align="center"):
    """Draws at double size and shrinks, so edges stay smooth."""
    s = 2
    W, H = w * s, h * s
    img = Image.new("RGBA", (W, H))
    horizon = int(H * 0.46)
    sky(img, horizon)
    sun(img, int(W * 0.68), horizon - int(H * 0.03), int(H * 0.07))
    hills(img, horizon)
    lighthouse(img, int(W * 0.86), horizon + int(H * 0.04), int(min(H * 0.34, W * 0.3)))
    gulls(img, [(W * 0.56, H * 0.09), (W * 0.62, H * 0.05), (W * 0.68, H * 0.11)], H * 0.02)
    bx, by, bw, bd = board_at
    board(img, int(W * bx), int(H * by), int(W * bw), int(H * bd))
    # Darken the bottom a touch so the title and board separate from the sea.
    veil = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    vd = ImageDraw.Draw(veil)
    for y in range(int(H * 0.55), H):
        a = int(110 * (y - H * 0.55) / (H * 0.45))
        vd.line([(0, y), (W, y)], fill=(5, 8, 14, a))
    img.alpha_composite(veil)
    tx, ty = title_at
    title(img, int(W * tx), int(H * ty), title_size * s, tagline, align)
    return img.resize((w, h), Image.LANCZOS).convert("RGB")


def logo(size):
    """Square app icon: the lighthouse on its rock against the dusk, inside a rounded tile."""
    s = 2
    S = size * s
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    tile = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    sky(tile, int(S * 0.6))
    sun(tile, int(S * 0.3), int(S * 0.56), int(S * 0.09))
    lighthouse(tile, int(S * 0.62), int(S * 0.72), int(S * 0.5))
    gulls(tile, [(S * 0.25, S * 0.25), (S * 0.36, S * 0.2)], S * 0.04)
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, S - 1, S - 1], radius=S * 0.2, fill=255)
    img.paste(tile, (0, 0), mask)
    return img.resize((size, size), Image.LANCZOS)


def main():
    os.makedirs(OUT, exist_ok=True)
    scene(460, 215, 62, (0.36, 0.26), (0.36, 0.5, 0.6, 0.45), tagline=False).save(os.path.join(OUT, "header_capsule.png"))
    scene(231, 87, 30, (0.36, 0.3), (0.36, 0.55, 0.62, 0.4), tagline=False).save(os.path.join(OUT, "small_capsule.png"))
    scene(616, 353, 84, (0.36, 0.24), (0.36, 0.48, 0.6, 0.48)).save(os.path.join(OUT, "main_capsule.png"))
    scene(600, 900, 80, (0.44, 0.17), (0.5, 0.5, 0.95, 0.42)).save(os.path.join(OUT, "library_capsule.png"))
    scene(3840, 1240, 360, (0.26, 0.3), (0.52, 0.6, 0.36, 0.4), align="center").save(os.path.join(OUT, "library_hero.png"))
    scene(1438, 810, 160, (0.3, 0.2), (0.4, 0.5, 0.55, 0.45)).save(os.path.join(OUT, "page_background.png"))
    # Library logo: transparent, title only (Steam lays it over the hero).
    lg = Image.new("RGBA", (1280 * 2, 720 * 2), (0, 0, 0, 0))
    title(lg, 1280, 640, 400, tagline=True)
    lg.resize((1280, 720), Image.LANCZOS).save(os.path.join(OUT, "library_logo.png"))
    logo(1024).save(os.path.join(OUT, "logo_1024.png"))
    logo(256).save(os.path.join(OUT, "logo_256.png"))
    print("yazıldı:", OUT)


if __name__ == "__main__":
    main()
