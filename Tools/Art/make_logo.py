#!/usr/bin/env python3
"""Turns the Lazpoly logo picture (Tools/Art/Logo/lazpoly-logo.jpg, on a white ground) into the menu's logo asset.

The picture is a JPEG on plain white. Its top right held a photo-like portrait of a man with a cigarette in a
gold medallion; the owner asked for the portrait to go (likeness and age-rating risk), so the medallion's inside
is repainted first: a two-tone navy disc in the picture's own blues with a white "L" (Nunito Bold) on it. The menu shows the logo over the green masthead and the paper window,
so the white ground has to go: every near-white pixel that connects to the picture's border is made transparent
(a flood fill, so the white faces of the dice and the letters inside the picture stay), the edge gets one pixel
of softening, and the result is written as a PNG whose bytes ship as a TextAsset:
    Assets/_Lazpoly/UI/Resources/Lazpoly/Art/logo.bytes
The game reads it with MenuArt (Texture2D.LoadImage). Run from the repository root: python3 Tools/Art/make_logo.py
"""
import os
import sys
from collections import deque

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

SOURCE = os.path.join("Tools", "Art", "Logo", "lazpoly-logo.jpg")
TARGET = os.path.join("Assets", "_Lazpoly", "UI", "Resources", "Lazpoly", "Art", "logo.bytes")
SIZE = 640          # The menu shows it 160 pixels tall at 1080p; 640 keeps it sharp on a 4K screen and in a bigger interface size.
WHITE = 236         # A pixel whose darkest channel is at least this counts as the white ground.

# The gold medallion round the portrait, measured on the 1254x1254 source (least-squares fit of the ring's gold pixels):
# centre, the ring's inner radius and the radius of the disc painted over the portrait (a little under the ring).
MEDALLION_CENTRE = (952, 520)
MEDALLION_INNER = 100
MEDALLION_FILL = 104
NAVY_LIGHT = (28, 96, 160)    # The picture's lighter facet blue (top left of the disc).
NAVY_DEEP = (10, 58, 106)     # Its deep blue (bottom right).
MARK_FONT = os.path.join("Assets", "_Lazpoly", "UI", "Resources", "Lazpoly", "Fonts", "Nunito-Bold.ttf")


def remove_portrait(image, root):
    """Paints the medallion's inside over: a navy disc split light/deep along the diagonal, with a white L."""
    cx, cy = MEDALLION_CENTRE
    r = MEDALLION_FILL
    box = (cx - r, cy - r, cx + r, cy + r)
    draw = ImageDraw.Draw(image)
    draw.ellipse(box, fill=NAVY_DEEP)
    disc = Image.new("L", image.size, 0)
    ImageDraw.Draw(disc).ellipse(box, fill=255)
    upper_left = Image.new("L", image.size, 0)
    ImageDraw.Draw(upper_left).polygon([(cx - r, cy - r), (cx + r, cy - r), (cx - r, cy + r)], fill=255)
    image.paste(Image.new("RGB", image.size, NAVY_LIGHT), (0, 0), ImageChops.multiply(disc, upper_left))
    font = ImageFont.truetype(os.path.join(root, MARK_FONT), int(MEDALLION_INNER * 1.35))
    left, top, right, bottom = draw.textbbox((0, 0), "L", font=font)
    draw.text((cx - (right - left) / 2 - left, cy - (bottom - top) / 2 - top + MEDALLION_INNER * 0.02), "L", font=font, fill=(252, 252, 250))


def main():
    root = os.getcwd()
    source = os.path.join(root, SOURCE)
    if not os.path.exists(source):
        sys.exit(f"missing {source}; run from the repository root")
    image = Image.open(source).convert("RGB")
    remove_portrait(image, root)
    width, height = image.size
    pixels = image.load()

    ground = bytearray(width * height)
    queue = deque()
    for x in range(width):
        queue.append((x, 0))
        queue.append((x, height - 1))
    for y in range(height):
        queue.append((0, y))
        queue.append((width - 1, y))
    while queue:
        x, y = queue.popleft()
        if x < 0 or y < 0 or x >= width or y >= height or ground[y * width + x]:
            continue
        if min(pixels[x, y]) < WHITE:
            continue
        ground[y * width + x] = 1
        queue.append((x + 1, y))
        queue.append((x - 1, y))
        queue.append((x, y + 1))
        queue.append((x, y - 1))

    alpha = Image.frombytes("L", (width, height), bytes(255 - 255 * g for g in ground))
    alpha = alpha.filter(ImageFilter.GaussianBlur(0.6))
    result = image.convert("RGBA")
    result.putalpha(alpha)

    # Trim to the picture, keep it square, then scale.
    box = alpha.point(lambda a: 255 if a > 8 else 0).getbbox()
    left, top, right, bottom = box
    side = max(right - left, bottom - top)
    cx, cy = (left + right) // 2, (top + bottom) // 2
    crop = result.crop((cx - side // 2 - 8, cy - side // 2 - 8, cx + side // 2 + 8, cy + side // 2 + 8))
    crop = crop.resize((SIZE, SIZE), Image.LANCZOS)

    target = os.path.join(root, TARGET)
    os.makedirs(os.path.dirname(target), exist_ok=True)
    crop.save(target, format="PNG", optimize=True)
    cleared = sum(ground) * 100 // (width * height)
    print(f"wrote {TARGET}: {SIZE}x{SIZE} PNG, {os.path.getsize(target)} bytes, {cleared}% of the source was ground")


if __name__ == "__main__":
    main()
