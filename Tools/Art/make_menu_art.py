#!/usr/bin/env python3
"""Turns the owner's interface kits (the main menu kit made with Higgsfield, and the player card kit V2) into art assets.

The kit's pictures are found by their kit names in the folder given (for example the unzipped
Assets/LazpolyMainMenu/Art and Sprites folders copied into one place) and written as TextAssets under
    Assets/_Lazpoly/UI/Resources/Lazpoly/Art/Menu/<name>.bytes
which the game decodes with KitArt (Texture2D.LoadImage), like the logo. The city background has no
see-through parts, so it is stored as JPEG (a tenth of the PNG's size);
the buttons, panels and icons stay PNG for their transparency. The kit's wide logo is no longer taken from the kit:
make_logo.py writes the Cash Father logo in its place (Lazpoly_Logo.bytes). The 9-slice borders of the
buttons and panels are not in the pictures: KitArt.Borders holds them (from the kit's SpriteManifest.json).
The kits' character portraits are no longer used: since Faz 21 the characters are the 3D figures, and
Tools/Art/make_figure_portraits.py draws their pictures from the figures themselves.
The player card kit printed the game's old name (LAZPOLY) and an "L" seal and coin; brand_kit.py puts
Cash Father and a "$" on them as they are imported.
Run from the repository root: python3 Tools/Art/make_menu_art.py <kit folder>
"""
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import brand_kit  # noqa: E402

TARGET = os.path.join("Assets", "_Lazpoly", "UI", "Resources", "Lazpoly", "Art", "Menu")
PHOTOS = ["Higgsfield_City_Background"]
CUTOUTS = [
    "Panel", "Chip", "Circle",
    "Primary_Normal", "Primary_Hover", "Primary_Pressed", "Primary_Disabled",
    "Secondary_Normal", "Secondary_Hover", "Secondary_Pressed", "Secondary_Disabled",
    "Icon_Arrow", "Icon_Book", "Icon_Case", "Icon_Close", "Icon_Dice", "Icon_Gear", "Icon_Globe",
    "Icon_Join", "Icon_Mute", "Icon_People", "Icon_Plus", "Icon_Sound", "Icon_Home", "Icon_Check", "Icon_Hotel",
    # The player card kit (V2): the card in its three states and its parts.
    "Card_Normal", "Card_Active", "Card_Bankrupt", "Avatar_Disc", "Avatar_Frame", "Player_Stripe", "Currency_L",
    "Lazpoly_Mark", "Property_Pip", "Turn_Pill", "Timer_Line", "Number_Tab",
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
        picture = brand_kit.repaint(name, Image.open(os.path.join(source, name + ".png")), os.getcwd())
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
