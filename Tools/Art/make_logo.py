#!/usr/bin/env python3
"""Turns the Lazpoly logo picture (Tools/Art/Logo/lazpoly-logo.jpg, on a white ground) into the menu's logo asset.

The picture is a JPEG on plain white. The menu shows the logo over the green masthead and the paper window,
so the white ground has to go: every near-white pixel that connects to the picture's border is made transparent
(a flood fill, so the white faces of the dice and the letters inside the picture stay), the edge gets one pixel
of softening, and the result is written as a PNG whose bytes ship as a TextAsset:
    Assets/_Lazpoly/UI/Resources/Lazpoly/Art/logo.bytes
The game reads it with MenuArt (Texture2D.LoadImage). Run from the repository root: python3 Tools/Art/make_logo.py
"""
import os
import sys
from collections import deque

from PIL import Image, ImageFilter

SOURCE = os.path.join("Tools", "Art", "Logo", "lazpoly-logo.jpg")
TARGET = os.path.join("Assets", "_Lazpoly", "UI", "Resources", "Lazpoly", "Art", "logo.bytes")
SIZE = 640          # The menu shows it 160 pixels tall at 1080p; 640 keeps it sharp on a 4K screen and in a bigger interface size.
WHITE = 236         # A pixel whose darkest channel is at least this counts as the white ground.


def main():
    root = os.getcwd()
    source = os.path.join(root, SOURCE)
    if not os.path.exists(source):
        sys.exit(f"missing {source}; run from the repository root")
    image = Image.open(source).convert("RGB")
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
