#!/usr/bin/env python3
"""Puts the Cash Father name on the player card kit's pictures, which printed the old one.

- Card_Normal, Card_Active, Card_Bankrupt: the small spaced "LAZPOLY" under the skyline of the card's dark side is
  painted over with that side's own colour, row by row, and "CASH FATHER" is lettered in its place in the same
  colour, size and spacing (Nunito Black, drawn four times bigger and scaled down).
- Lazpoly_Mark (the seal on the card, tinted by the game) and Currency_L (the coin before each balance): the old
  "L" becomes a "$", like the medallion under the logo's lettering. Both are small and plain, so they are drawn
  anew in the kit's shapes and colours: a white diamond outline, and a gold hexagon coin with a cream face.
Painting the same picture again gives the same picture, so this can run on the kit's files or on the assets.
make_menu_art.py calls it after importing a kit; make_logo.py runs it on the assets.
Run from the repository root: python3 Tools/Art/brand_kit.py
"""
import math
import os

from PIL import Image, ImageDraw, ImageFont

TARGET = os.path.join("Assets", "_Lazpoly", "UI", "Resources", "Lazpoly", "Art", "Menu")
BOLD = os.path.join("Assets", "_Lazpoly", "UI", "Resources", "Lazpoly", "Fonts", "Nunito-Bold.ttf")
BLACK = os.path.join("Assets", "_Lazpoly", "UI", "Resources", "Lazpoly", "Fonts", "Nunito-Black.ttf")
NAME = "CASH FATHER"
CARDS = ["Card_Normal", "Card_Active", "Card_Bankrupt"]

# The print on the 640x224 card: its letters fill rows 197-205 and sit centred on the dark side's x 80.5.
PRINT_BOX = (27, 192, 137, 211)   # Painted over (left, top, right, bottom; right and bottom exclusive).
PANEL_SAMPLE_X = 26               # Each row is painted with the colour this column has on that row.
CAP_TOP = 197
CAP_HEIGHT = 9
CENTRE_X = 80.5
TRACKING = 1.3                    # Pixels between letters, close to the old print.
SCALE = 4

# The old print's colour on each card (its brightest pixel), so painting again gives the same picture.
INK = {"Card_Normal": (228, 210, 162), "Card_Active": (228, 210, 162), "Card_Bankrupt": (204, 213, 214)}

COIN_RIM = (215, 172, 85)
COIN_FACE = (245, 225, 166)
COIN_INK = (143, 100, 37)


def font(root, path, size):
    return ImageFont.truetype(os.path.join(root, path), size)


def lettering(root, text, cap_height, tracking):
    """White-on-clear spaced capitals SCALE times bigger than wanted, cut to their cap height; also their width."""
    probe = font(root, BLACK, 100)
    left, top, right, bottom = probe.getbbox("H")
    size = round(100 * cap_height * SCALE / (bottom - top))
    f = font(root, BLACK, size)
    widths = [f.getlength(ch) for ch in text]
    total = sum(widths) + tracking * SCALE * (len(text) - 1)
    h_top = f.getbbox("H")[1]
    layer = Image.new("L", (math.ceil(total) + 8, cap_height * SCALE + 8), 0)
    draw = ImageDraw.Draw(layer)
    x = 4.0
    for ch, w in zip(text, widths):
        draw.text((x, 4 - h_top), ch, font=f, fill=255)
        x += w + tracking * SCALE
    return layer, total


def repaint_card(name, picture, root):
    picture = picture.convert("RGBA")
    pixels = picture.load()
    left, top, right, bottom = PRINT_BOX
    ink = INK[name]
    for y in range(top, bottom):
        colour = pixels[PANEL_SAMPLE_X, y]
        for x in range(left, right):
            pixels[x, y] = colour

    layer, width = lettering(root, NAME, CAP_HEIGHT, TRACKING)
    small = layer.resize((round(layer.width / SCALE), round(layer.height / SCALE)), Image.LANCZOS)
    x0 = round(CENTRE_X - width / SCALE / 2 - 1)
    y0 = CAP_TOP - 1
    solid = Image.new("RGBA", small.size, ink[:3] + (255,))
    clear = Image.new("RGBA", small.size, (0, 0, 0, 0))
    text = Image.composite(solid, clear, small)
    picture.alpha_composite(text, (x0, y0))
    return picture


def dollar(draw, root, centre, height, fill):
    f = font(root, BLACK, 100)
    left, top, right, bottom = f.getbbox("$")
    f = font(root, BLACK, round(100 * height / (bottom - top)))
    left, top, right, bottom = draw.textbbox((0, 0), "$", font=f)
    draw.text((centre[0] - (right - left) / 2 - left, centre[1] - (bottom - top) / 2 - top), "$", font=f, fill=fill)


def seal(size, root):
    """The card's seal: a white diamond outline with a white "$" inside (the game tints it)."""
    s = size * SCALE
    image = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    c = s / 2
    outer = c - 0.6 * SCALE
    width = 2.6 * SCALE
    diamond = [(c, c - outer), (c + outer, c), (c, c + outer), (c - outer, c)]
    draw.polygon(diamond, fill=(255, 255, 255, 255))
    inner = outer - width * math.sqrt(2)
    draw.polygon([(c, c - inner), (c + inner, c), (c, c + inner), (c - inner, c)], fill=(0, 0, 0, 0))
    dollar(draw, root, (c, c), s * 0.44, (255, 255, 255, 255))
    return image.resize((size, size), Image.LANCZOS)


def hexagon(c, radius):
    return [(c + radius * math.sin(math.radians(a)), c - radius * math.cos(math.radians(a))) for a in range(0, 360, 60)]


def money_coin(size, root):
    """The coin before each balance: a gold hexagon with a cream face and a brown "$"."""
    s = size * SCALE
    image = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    c = s / 2
    draw.polygon(hexagon(c, s * 0.47), fill=COIN_RIM + (255,))
    draw.polygon(hexagon(c, s * 0.36), fill=COIN_FACE + (255,))
    dollar(draw, root, (c, c), s * 0.46, COIN_INK + (255,))
    return image.resize((size, size), Image.LANCZOS)


def repaint(name, picture, root):
    """The kit picture with the new name on it; pictures that carry no name come back as they are."""
    if name in CARDS:
        return repaint_card(name, picture, root)
    if name == "Lazpoly_Mark":
        return seal(picture.width, root)
    if name == "Currency_L":
        return money_coin(picture.width, root)
    return picture


def repaint_folder(folder, root):
    for name in CARDS + ["Lazpoly_Mark", "Currency_L"]:
        path = os.path.join(folder, name + ".bytes")
        picture = Image.open(path)
        picture.load()
        repaint(name, picture, root).save(path, format="PNG", optimize=True)
        print(f"repainted {name}")


if __name__ == "__main__":
    repaint_folder(os.path.join(os.getcwd(), TARGET), os.getcwd())
