#!/usr/bin/env python3
"""Turns the owner's main menu kit (Layout.json's pictures, made with Higgsfield) into the menu's art assets.

The kit's pictures are found by their kit names in the folder given (for example the unzipped
Assets/LazpolyMainMenu/Art and Sprites folders copied into one place) and written as TextAssets under
    Assets/_Lazpoly/UI/Resources/Lazpoly/Art/Menu/<name>.bytes
which the game decodes with MenuKit (Texture2D.LoadImage), like the logo. The city background and the four
character portraits have no see-through parts, so they are stored as JPEG (a tenth of the PNG's size);
the buttons, panels, icons and the wide logo stay PNG for their transparency. The 9-slice borders of the
buttons and panels are not in the pictures: MenuKit.Borders holds them (from the kit's SpriteManifest.json).
Run from the repository root: python3 Tools/Art/make_menu_art.py <kit folder>
"""
import os
import sys

from PIL import Image

TARGET = os.path.join("Assets", "_Lazpoly", "UI", "Resources", "Lazpoly", "Art", "Menu")
PHOTOS = ["Higgsfield_City_Background", "Portrait_Woman01", "Portrait_Woman02", "Portrait_Man01", "Portrait_Man02"]
CUTOUTS = [
    "Lazpoly_Logo", "Panel", "Chip", "Circle",
    "Primary_Normal", "Primary_Hover", "Primary_Pressed", "Primary_Disabled",
    "Secondary_Normal", "Secondary_Hover", "Secondary_Pressed", "Secondary_Disabled",
    "Icon_Arrow", "Icon_Book", "Icon_Case", "Icon_Close", "Icon_Dice", "Icon_Gear", "Icon_Globe",
    "Icon_Join", "Icon_Mute", "Icon_People", "Icon_Plus", "Icon_Sound",
]
JPEG_QUALITY = 90
META = """fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def write_meta(path):
    """A TextAsset's .meta, kept when it is already there so the guid does not change."""
    meta = path + ".meta"
    if os.path.exists(meta):
        return
    import uuid
    with open(meta, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(META.format(guid=uuid.uuid4().hex))


def main(source):
    os.makedirs(TARGET, exist_ok=True)
    for name in PHOTOS + CUTOUTS:
        picture = Image.open(os.path.join(source, name + ".png"))
        target = os.path.join(TARGET, name + ".bytes")
        if name in PHOTOS:
            picture.convert("RGB").save(target, "JPEG", quality=JPEG_QUALITY, optimize=True, progressive=False)
        else:
            picture.convert("RGBA").save(target, "PNG", optimize=True)
        write_meta(target)
        print(f"{name}: {picture.size[0]}x{picture.size[1]} -> {os.path.getsize(target) // 1024} KB")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit("usage: make_menu_art.py <kit folder>")
    main(sys.argv[1])
