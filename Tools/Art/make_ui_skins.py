#!/usr/bin/env python3
"""Draws the interface's skins: the buttons, the window, the card and the disc every screen wears.

The owner asked for the menus, the loading screens and the buttons to be redesigned to suit the game (2026-11-21):
Cash Father's own colours, taken from its logo (deep navy, gold, ivory), in place of the old kit's cream and petrol
with a folded corner. Everything here is drawn by this script from the numbers below (rounded shapes, gradients,
rims): no picture is taken from anywhere and none comes out of a generative model.

A button stands on a darker lip, like a key that can be pushed; pressed, it sinks onto it. The pictures keep the old
kit's names, sizes and 9-slice borders (KitArt.Borders), so the game's code places them as before:
    Primary_*    navy with a gold rim: the main action, a chosen choice, the news bands
    Secondary_*  ivory with a pale gold rim: everything else
    Hero_*       gold: the one button a screen is about (the main menu's HEMEN OYNA)
    Panel        a window: ivory paper in a gold frame, a hairline inside it
    Chip         a card inside a window, a field, a profile
    Circle       a navy disc with a gold rim
They are written as PNG bytes under Assets/_Lazpoly/UI/Resources/Lazpoly/Art/Menu/<name>.bytes (decoded by KitArt).
The player cards of the table (Card_Normal, Card_Active, Card_Bankrupt) keep their drawing; their petrol side is
turned to the same navy (repaint_cards), and painting them again changes nothing.

Run from the repository root: python3 Tools/Art/make_ui_skins.py [preview.png]
"""
import os
import sys

import numpy as np
from PIL import Image

TARGET = os.path.join("Assets", "_Lazpoly", "UI", "Resources", "Lazpoly", "Art", "Menu")
SCALE = 3  # Drawn this many times larger and scaled down, so edges are smooth.

META = """fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def rgb(code):
    return np.array([(code >> 16) & 255, (code >> 8) & 255, code & 255], dtype=np.float64) / 255.0


# The game's colours (UiPalette holds the same ones for text and flat shapes).
NAVY_DEEP = rgb(0x0A1330)
NAVY_LOW = rgb(0x152A5E)
NAVY_HIGH = rgb(0x27448C)
GOLD_PALE = rgb(0xFFE7A0)
GOLD_HIGH = rgb(0xFFD764)
GOLD_LOW = rgb(0xF0A51C)
GOLD_RIM = rgb(0xD9A534)
GOLD_DEEP = rgb(0x9A6209)
IVORY = rgb(0xFFFCF3)
IVORY_LOW = rgb(0xF7EFDB)
IVORY_RIM = rgb(0xCDB981)
IVORY_LIP = rgb(0xB9A56E)
WHITE = np.array([1.0, 1.0, 1.0])


class Canvas:
    """A picture being drawn: premultiplied colour and coverage, in the final picture's pixels (drawn SCALE times finer)."""

    def __init__(self, width, height):
        self.width, self.height = width, height
        self.w, self.h = width * SCALE, height * SCALE
        ys, xs = np.mgrid[0:self.h, 0:self.w]
        self.x = (xs + 0.5) / SCALE
        self.y = (ys + 0.5) / SCALE
        self.color = np.zeros((self.h, self.w, 3))
        self.alpha = np.zeros((self.h, self.w))

    def box(self, left, top, right, bottom, radius):
        """Signed distance to a rounded rectangle: negative inside."""
        cx, cy = (left + right) / 2.0, (top + bottom) / 2.0
        hx, hy = (right - left) / 2.0 - radius, (bottom - top) / 2.0 - radius
        dx, dy = np.abs(self.x - cx) - hx, np.abs(self.y - cy) - hy
        return np.hypot(np.maximum(dx, 0), np.maximum(dy, 0)) + np.minimum(np.maximum(dx, dy), 0) - radius

    def disc(self, cx, cy, radius):
        return np.hypot(self.x - cx, self.y - cy) - radius

    @staticmethod
    def cover(distance):
        """How much of each pixel a shape covers, from its signed distance (one fine pixel of smoothing)."""
        return np.clip(0.5 - distance * SCALE, 0, 1)

    def paint(self, coverage, color, opacity=1.0):
        """Lays a colour (one colour, or one for each pixel) over the picture where coverage says."""
        a = coverage * opacity
        color = np.broadcast_to(color, self.color.shape) if np.ndim(color) == 1 else color
        self.color = color * a[..., None] + self.color * (1 - a[..., None])
        self.alpha = a + self.alpha * (1 - a)

    def shadow(self, distance, spread, color, opacity):
        """A soft shadow: full at the shape's edge, gone spread pixels out."""
        self.paint(np.clip(1 - distance / spread, 0, 1) ** 2, color, opacity)

    def down(self, top, bottom, high, low):
        """A colour for each pixel: high at top, low at bottom."""
        t = np.clip((self.y - top) / max(bottom - top, 1e-6), 0, 1)[..., None]
        return high * (1 - t) + low * t

    def image(self):
        alpha = np.clip(self.alpha, 0, 1)
        straight = np.where(alpha[..., None] > 1e-6, self.color / np.maximum(alpha[..., None], 1e-6), 0)
        data = np.dstack([np.clip(straight, 0, 1), alpha])
        big = Image.fromarray((data * 255 + 0.5).astype(np.uint8), "RGBA")
        return big.resize((self.width, self.height), Image.LANCZOS)


def mix(a, b, t):
    return a * (1 - t) + b * t


def grey(color, amount):
    """A colour with most of its colour taken out, for a button that cannot be pressed."""
    luma = float(color @ np.array([0.299, 0.587, 0.114]))
    return mix(color, np.array([luma, luma, luma]), amount)


def button(width, height, look, state):
    """One button picture. The body is `height - 14` tall at rest; pressed, it sinks onto its lip."""
    c = Canvas(width, height)
    radius = 20.0
    lip = look["lip_height"]
    sink = {"Pressed": lip - 2.0}.get(state, 0.0)
    left, right = 2.0, width - 2.0
    top = 2.0 + sink
    bottom = height - 8.0 - lip + sink
    high, low, rim, lip_color, shine = look["high"], look["low"], look["rim"], look["lip"], look["shine"]
    if state == "Hover":
        high, low, rim = look["hover_high"], look["hover_low"], look["hover_rim"]
    elif state == "Pressed":
        high, low = mix(high, look["lip"], 0.22), mix(low, look["lip"], 0.22)
    elif state == "Disabled":
        fade = look["fade"]
        high, low, rim, lip_color = [mix(grey(v, 0.85), fade, 0.45) for v in (high, low, rim, lip_color)]
        shine *= 0.4

    body = c.box(left, top, right, bottom, radius)
    under = c.box(left, 2.0 + lip, right, height - 8.0, radius)
    c.shadow(under - 0.5, 7.0, NAVY_DEEP, 0.0 if state == "Disabled" else (0.16 if state == "Pressed" else 0.26))
    c.paint(Canvas.cover(under), lip_color)
    c.paint(Canvas.cover(body), rim)
    inner = body + look["rim_width"]
    c.paint(Canvas.cover(inner), c.down(top, bottom, high, low))
    # The light along the top edge, and a faint sheen over the upper half.
    edge = Canvas.cover(inner) * Canvas.cover(-(inner + 1.6)) * np.clip(1 - (c.y - top) / ((bottom - top) * 0.55), 0, 1)
    c.paint(edge, WHITE, shine)
    sheen = Canvas.cover(inner + 1.6) * np.clip(1 - (c.y - top) / ((bottom - top) * 0.5), 0, 1)
    c.paint(sheen, WHITE, shine * 0.16)
    return c.image()


NAVY = dict(high=NAVY_HIGH, low=NAVY_LOW, rim=GOLD_RIM, lip=NAVY_DEEP, shine=0.30, hover_high=rgb(0x3559B4), hover_low=rgb(0x1E3A80), hover_rim=GOLD_HIGH,
            rim_width=2.4, lip_height=6.0, fade=rgb(0x8E97AB))
LIGHT = dict(high=WHITE, low=rgb(0xFFF8E4), rim=IVORY_RIM, lip=IVORY_LIP, shine=0.0, hover_high=WHITE, hover_low=rgb(0xFFF3CF), hover_rim=GOLD_RIM,
             rim_width=2.0, lip_height=5.0, fade=rgb(0xE4E0D4))
GOLD = dict(high=GOLD_HIGH, low=GOLD_LOW, rim=rgb(0xB87D10), lip=GOLD_DEEP, shine=0.75, hover_high=rgb(0xFFE38A), hover_low=rgb(0xF7B733), hover_rim=rgb(0xC78A14),
            rim_width=2.4, lip_height=7.0, fade=rgb(0xC9C2AE))


def panel():
    """A window: ivory paper in a gold frame with a hairline inside it, and a soft shadow under it."""
    size = 256
    c = Canvas(size, size)
    left, top, right, bottom, radius = 3.0, 2.0, size - 3.0, size - 10.0, 26.0
    body = c.box(left, top, right, bottom, radius)
    c.shadow(c.box(left + 2, top + 8, right - 2, bottom + 5, radius) - 1.0, 6.0, NAVY_DEEP, 0.34)
    c.paint(Canvas.cover(body), c.down(top, bottom, GOLD_HIGH, GOLD_RIM))
    c.paint(Canvas.cover(body + 1.0) * Canvas.cover(-(body + 2.2)), GOLD_PALE, 0.9)  # The frame's lit inner edge.
    c.paint(Canvas.cover(body + 4.0), c.down(top, bottom, IVORY, mix(IVORY, IVORY_LOW, 0.55)))
    line = body + 11.0
    c.paint(Canvas.cover(line) * Canvas.cover(-(line + 1.4)), IVORY_RIM, 0.55)
    return c.image()


def chip():
    """A card inside a window: near white, a thin pale gold edge and a small shadow."""
    width, height = 256, 88
    c = Canvas(width, height)
    left, top, right, bottom, radius = 2.0, 2.0, width - 2.0, height - 7.0, 17.0
    body = c.box(left, top, right, bottom, radius)
    c.shadow(c.box(left + 1, top + 4, right - 1, bottom + 3, radius) - 0.5, 4.0, NAVY_DEEP, 0.2)
    c.paint(Canvas.cover(body), mix(IVORY_RIM, IVORY, 0.35))
    c.paint(Canvas.cover(body + 1.5), c.down(top, bottom, WHITE, mix(IVORY, IVORY_LOW, 0.4)))
    return c.image()


def circle():
    """A navy disc with a gold rim and a thin gold ring inside it."""
    size = 96
    c = Canvas(size, size)
    centre, radius = size / 2.0, size / 2.0 - 3.0
    c.shadow(c.disc(centre, centre + 2.0, radius) - 0.5, 3.0, NAVY_DEEP, 0.3)
    disc = c.disc(centre, centre, radius)
    c.paint(Canvas.cover(disc), c.down(3, size - 3, GOLD_HIGH, GOLD_RIM))
    c.paint(Canvas.cover(disc + 3.0), c.down(3, size - 3, NAVY_HIGH, NAVY_LOW))
    ring = disc + 8.0
    c.paint(Canvas.cover(ring) * Canvas.cover(-(ring + 1.3)), GOLD_RIM, 0.75)
    return c.image()


# The table's player cards were drawn in the old kit's petrol (hue about 199 degrees); it becomes the navy's hue.
PETROL_HUES = (0.50, 0.60)
NAVY_HUE = 0.622
CARDS = ["Card_Normal", "Card_Active", "Card_Bankrupt"]


def to_navy(picture):
    """Turns the petrol in a kit picture to navy: every pixel of a petrol hue takes the navy's hue and keeps how
    strong and how light it is (the grey petrol of a bankrupt's card stays grey). Navy already, it is left alone."""
    data = np.array(picture.convert("RGBA"), dtype=np.float64) / 255.0
    r, g, b = data[..., 0], data[..., 1], data[..., 2]
    high, low = data[..., :3].max(axis=2), data[..., :3].min(axis=2)
    chroma = high - low
    safe = np.maximum(chroma, 1e-9)
    hue = np.where(high == b, 4 + (r - g) / safe, np.where(high == g, 2 + (b - r) / safe, ((g - b) / safe) % 6)) / 6.0
    petrol = (chroma > 0.03) & (hue >= PETROL_HUES[0]) & (hue <= PETROL_HUES[1])
    # The navy's hue lies between blue and magenta's side of blue: blue on top, then green over red by a little.
    part = NAVY_HUE * 6 - 4  # 0 at pure blue, towards magenta as it grows; negative towards cyan.
    new_r = np.where(part >= 0, low + chroma * part, low)
    new_g = np.where(part >= 0, low, low + chroma * -part)
    out = data.copy()
    out[..., 0] = np.where(petrol, new_r, r)
    out[..., 1] = np.where(petrol, new_g, g)
    out[..., 2] = np.where(petrol, high, b)
    return Image.fromarray((out * 255 + 0.5).astype(np.uint8), "RGBA")


def write(name, picture):
    path = os.path.join(TARGET, name + ".bytes")
    picture.save(path, "PNG", optimize=True)
    meta = path + ".meta"
    if not os.path.exists(meta):
        import uuid
        with open(meta, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(META.format(guid=uuid.uuid4().hex))
    return path


def skins():
    made = {}
    for state in ("Normal", "Hover", "Pressed", "Disabled"):
        made["Primary_" + state] = button(640, 120, NAVY, state)
        made["Secondary_" + state] = button(360, 100, LIGHT, state)
        made["Hero_" + state] = button(640, 120, GOLD, state)
    made["Panel"] = panel()
    made["Chip"] = chip()
    made["Circle"] = circle()
    return made


def main(preview=None):
    made = skins()
    for name in CARDS:
        path = os.path.join(TARGET, name + ".bytes")
        if os.path.exists(path):
            made[name] = to_navy(Image.open(path))
    for name, picture in made.items():
        path = write(name, picture)
        print(f"{name}: {picture.size[0]}x{picture.size[1]} -> {os.path.getsize(path)} bytes")
    if preview:
        sheet(made).save(preview)


def sheet(made):
    """Every skin on a sky-blue and on a navy ground, to look at."""
    names = list(made)
    width = max(p.width for p in made.values()) * 2 + 60
    height = sum(made[n].height + 16 for n in names) + 16
    out = Image.new("RGBA", (width, height), (150, 196, 232, 255))
    out.paste((16, 30, 66, 255), (width // 2, 0, width, height))
    y = 16
    for n in names:
        out.alpha_composite(made[n], (20, y))
        out.alpha_composite(made[n], (width // 2 + 20, y))
        y += made[n].height + 16
    return out


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else None)
