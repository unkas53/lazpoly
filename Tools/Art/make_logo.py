#!/usr/bin/env python3
"""Turns the Cash Father logo (Tools/Art/Logo/cash-father-logo.png, see KAYNAK.md there) into the game's brand assets.

The picture comes with a see-through ground, so it is only trimmed to what it shows (a small margin kept), its
nearly opaque pixels made fully opaque, and written as PNG bytes that ship as TextAssets:
    Assets/_Lazpoly/UI/Resources/Lazpoly/Art/logo.bytes            (MenuArt: the lawn's logo, the fallback logo)
    Assets/_Lazpoly/UI/Resources/Lazpoly/Art/Menu/Lazpoly_Logo.bytes (KitArt: the main menu, the loading screens)
The player's icon (CashFather.exe, the task bar, the window title) is the logo's character, as the owner asked
(2026-11-10), in a round gold-rimmed medallion: his head in the top hat with a little of the collar and tie,
    Assets/_Lazpoly/Art/Textures/lazpoly-icon.png (256x256)
and, for the sizes of 48 and under, a tighter crop of the hat and the face, so they still read:
    Assets/_Lazpoly/Art/Textures/lazpoly-icon-small.png (64x64)
The player card kit printed the old name and an "L" on its pictures; brand_kit.py repaints them (also run here).
The asset names keep their old "Lazpoly" spelling: they are internal, and keeping them keeps Unity's guids.
Run from the repository root: python3 Tools/Art/make_logo.py
"""
import os
import sys

from PIL import Image, ImageChops, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import brand_kit  # noqa: E402

SOURCE = os.path.join("Tools", "Art", "Logo", "cash-father-logo.png")
TARGETS = [
    os.path.join("Assets", "_Lazpoly", "UI", "Resources", "Lazpoly", "Art", "logo.bytes"),
    os.path.join("Assets", "_Lazpoly", "UI", "Resources", "Lazpoly", "Art", "Menu", "Lazpoly_Logo.bytes"),
]
ICON = os.path.join("Assets", "_Lazpoly", "Art", "Textures", "lazpoly-icon.png")
ICON_SIZE = 256
SMALL_ICON = os.path.join("Assets", "_Lazpoly", "Art", "Textures", "lazpoly-icon-small.png")
SMALL_ICON_SIZE = 64
MARGIN = 10          # Pixels of see-through ground kept round the trimmed picture, so its soft edge is not cut.
SOLID = 245          # Alpha from here up counts as opaque (the source's opaque parts carry 251-253).

# Squares of the source picture (1244x1265) round the character's head: with the collar, and tighter.
ICON_BOX = (440, 0, 800, 360)
SMALL_ICON_BOX = (465, 2, 775, 312)
RIM = 0.06                 # The medallion's gold rim, in icon widths.
GOLD = (232, 176, 58)
GOLD_DEEP = (150, 98, 24)
NIGHT = (28, 32, 52)       # The disc behind him, the logo's night-blue towers.


def load_logo(root):
    """The logo trimmed to its picture, with opaque pixels fully opaque."""
    image = Image.open(os.path.join(root, SOURCE)).convert("RGBA")
    alpha = image.getchannel("A").point(lambda a: 255 if a >= SOLID else a)
    image.putalpha(alpha)
    left, top, right, bottom = alpha.point(lambda a: 255 if a > 8 else 0).getbbox()
    box = (max(0, left - MARGIN), max(0, top - MARGIN), min(image.width, right + MARGIN), min(image.height, bottom + MARGIN))
    return image.crop(box)


def fit_square(picture, size):
    """The picture scaled to fit a see-through square, centred."""
    scale = size / max(picture.width, picture.height)
    shrunk = picture.resize((max(1, round(picture.width * scale)), max(1, round(picture.height * scale))), Image.LANCZOS)
    square = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    square.alpha_composite(shrunk, ((size - shrunk.width) // 2, (size - shrunk.height) // 2))
    return square


def character(root, box, size):
    """The logo's character in a round gold-rimmed medallion: a square of the source round his head, over a night-blue
    disc, cut to a circle, drawn four times bigger and scaled down. The corners stay see-through."""
    big = size * 4
    source = Image.open(os.path.join(root, SOURCE)).convert("RGBA")
    head = source.crop(box).resize((big, big), Image.LANCZOS)
    rim = big * RIM
    disc = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    draw = ImageDraw.Draw(disc)
    draw.ellipse((0, 0, big - 1, big - 1), fill=GOLD_DEEP + (255,))
    draw.ellipse((rim * 0.35, rim * 0.35, big - 1 - rim * 0.35, big - 1 - rim * 0.35), fill=GOLD + (255,))
    draw.ellipse((rim, rim, big - 1 - rim, big - 1 - rim), fill=NIGHT + (255,))
    inside = Image.new("L", (big, big), 0)
    ImageDraw.Draw(inside).ellipse((rim, rim, big - 1 - rim, big - 1 - rim), fill=255)
    face = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    face.paste(head, (0, 0), ImageChops.multiply(head.getchannel("A"), inside))
    disc.alpha_composite(face)
    return disc.resize((size, size), Image.LANCZOS)


def main():
    root = os.getcwd()
    if not os.path.exists(os.path.join(root, SOURCE)):
        sys.exit(f"missing {SOURCE}; run from the repository root")
    logo = load_logo(root)
    for target in TARGETS:
        path = os.path.join(root, target)
        logo.save(path, format="PNG", optimize=True)
        print(f"wrote {target}: {logo.width}x{logo.height} PNG, {os.path.getsize(path) // 1024} KB")

    character(root, ICON_BOX, ICON_SIZE).save(os.path.join(root, ICON), format="PNG", optimize=True)
    print(f"wrote {ICON}: {ICON_SIZE}x{ICON_SIZE}")
    character(root, SMALL_ICON_BOX, SMALL_ICON_SIZE).save(os.path.join(root, SMALL_ICON), format="PNG", optimize=True)
    print(f"wrote {SMALL_ICON}: {SMALL_ICON_SIZE}x{SMALL_ICON_SIZE}")

    brand_kit.repaint_folder(os.path.join(root, brand_kit.TARGET), root)


if __name__ == "__main__":
    main()
