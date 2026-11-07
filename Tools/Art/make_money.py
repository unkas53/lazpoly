#!/usr/bin/env python3
"""Turns the owner's money set (a 1 L coin and a 100 L banknote, GLB files made in code with three.js) into the
game's money pack.

Each GLB holds one mesh per material: flat-coloured parts (gold, navy, ivory, ...) and a thin plane that carries the
Lazpoly logo, whose transparent pixels are cut away. This script:
  * reads every part's positions, normals, UVs and indices, with its material's colour (glTF's linear colour turned
    back to sRGB, as Unity's Color expects), metalness and smoothness (1 - roughness),
  * turns glTF's right-handed Y-up into Unity's left-handed Y-up: z is mirrored (the usual x mirror, turned half round
    so the lettering's tops point to +Z and it reads upright to a camera looking along +Z) and the winding flipped;
    v becomes 1 - v,
  * keeps the models' own units and pivots (the coin is 1.304 across, the note 2.8 x 1.36, both lying flat with their
    lowest face at y = 0); the game sizes them,
  * writes Assets/_Lazpoly/Data/Resources/Lazpoly/Models/money.bytes (the parts) and money_logo.bytes (the logo the
    planes carry, as a PNG with alpha, LOGO pixels wide).

Format of money.bytes (little-endian): "LZM1", int32 part count, then per part: uint8 name length, ASCII name
("Coin/<material>" or "Note/<material>"), float32 r, g, b, metallic, smoothness, uint8 1 when the part carries the logo,
int32 vertex count, float32 x/y/z per vertex, int16 normal x/y/z per vertex (1/32767), uint16 u/v per vertex (1/65535),
uint8 index size (2 or 4), int32 index count, the indices.

Needs: pip install numpy pillow. Run from the repository root:
    python3 Tools/Art/make_money.py
"""
import io
import json
import os
import struct
import sys

import numpy as np
from PIL import Image

SOURCE = os.path.join("Tools", "Art", "Models", "Money")
TARGET = os.path.join("Assets", "_Lazpoly", "Data", "Resources", "Lazpoly", "Models")
MODELS = (("Coin", "Lazpoly_Coin_1L.glb"), ("Note", "Lazpoly_Banknote_100L.glb"))
LOGO = 1024

COMPONENTS = {5126: np.float32, 5125: np.uint32, 5123: np.uint16, 5121: np.uint8}
WIDTH = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}


def srgb(linear):
    linear = max(0.0, min(1.0, linear))
    return 12.92 * linear if linear <= 0.0031308 else 1.055 * linear ** (1 / 2.4) - 0.055


def read_glb(path):
    data = open(path, "rb").read()
    if data[:4] != b"glTF":
        sys.exit(f"{path} is not a GLB file")
    json_length, = struct.unpack("<I", data[12:16])
    doc = json.loads(data[20:20 + json_length])
    offset = 20 + json_length
    bin_length, = struct.unpack("<I", data[offset:offset + 4])
    blob = data[offset + 8:offset + 8 + bin_length]

    def accessor(index):
        a = doc["accessors"][index]
        view = doc["bufferViews"][a["bufferView"]]
        start = view.get("byteOffset", 0) + a.get("byteOffset", 0)
        width = WIDTH[a["type"]]
        values = np.frombuffer(blob, dtype=COMPONENTS[a["componentType"]], count=a["count"] * width, offset=start)
        return values.reshape(a["count"], width) if width > 1 else values

    for node in doc["nodes"]:
        if any(key in node for key in ("matrix", "translation", "rotation", "scale")):
            sys.exit(f"{path}: node {node.get('name')} is moved; the parts are expected in place")

    parts, logo = [], None
    for mesh in doc["meshes"]:
        for primitive in mesh["primitives"]:
            material = doc["materials"][primitive["material"]]
            pbr = material["pbrMetallicRoughness"]
            attributes = primitive["attributes"]
            textured = "baseColorTexture" in pbr
            if textured and logo is None:
                image = doc["images"][doc["textures"][pbr["baseColorTexture"]["index"]]["source"]]
                view = doc["bufferViews"][image["bufferView"]]
                start = view.get("byteOffset", 0)
                logo = Image.open(io.BytesIO(blob[start:start + view["byteLength"]])).convert("RGBA")
            parts.append({
                "material": material["name"],
                "colour": [srgb(c) for c in pbr.get("baseColorFactor", [1, 1, 1, 1])[:3]],
                "metallic": pbr.get("metallicFactor", 1.0),
                "smoothness": 1.0 - pbr.get("roughnessFactor", 1.0),
                "textured": textured,
                "positions": accessor(attributes["POSITION"]).astype(np.float64),
                "normals": accessor(attributes["NORMAL"]).astype(np.float64),
                "uvs": accessor(attributes["TEXCOORD_0"]).astype(np.float64) if "TEXCOORD_0" in attributes else None,
                "indices": accessor(primitive["indices"]).astype(np.uint32).reshape(-1, 3),
            })
    return parts, logo


def to_game(part):
    # Mirroring z (glTF's x mirror turned half round) also turns the set so its lettering reads from -Z, its tops at +Z.
    part["positions"] = part["positions"] * np.array([1.0, 1.0, -1.0])
    part["normals"] = part["normals"] * np.array([1.0, 1.0, -1.0])
    part["indices"] = part["indices"][:, [0, 2, 1]]  # The mirror turns the faces inside out; the winding turns them back.
    uvs = part["uvs"] if part["uvs"] is not None else np.zeros((len(part["positions"]), 2))
    part["uvs"] = np.column_stack([uvs[:, 0], 1.0 - uvs[:, 1]])


def pack_part(name, part):
    out = bytearray()
    encoded = name.encode("ascii")
    out += struct.pack("<B", len(encoded)) + encoded
    out += struct.pack("<5f", *part["colour"], part["metallic"], part["smoothness"])
    out += struct.pack("<B", 1 if part["textured"] else 0)
    positions = part["positions"]
    out += struct.pack("<i", len(positions)) + positions.astype("<f4").tobytes()
    normals = part["normals"]
    lengths = np.linalg.norm(normals, axis=1, keepdims=True)
    out += np.round(normals / np.where(lengths < 1e-9, 1.0, lengths) * 32767).astype("<i2").tobytes()
    out += np.round(np.clip(part["uvs"], 0, 1) * 65535).astype("<u2").tobytes()
    flat = part["indices"].reshape(-1)
    wide = len(positions) > 65535
    out += struct.pack("<B", 4 if wide else 2) + struct.pack("<i", len(flat))
    out += flat.astype("<u4" if wide else "<u2").tobytes()
    return out


def main():
    chunks, logo = [], None
    for model, file_name in MODELS:
        parts, model_logo = read_glb(os.path.join(SOURCE, file_name))
        logo = logo or model_logo
        low = np.min([p["positions"].min(axis=0) for p in parts], axis=0)
        high = np.max([p["positions"].max(axis=0) for p in parts], axis=0)
        print(f"{model}: {high[0] - low[0]:.3f} x {high[1] - low[1]:.3f} x {high[2] - low[2]:.3f}, "
              f"{sum(len(p['indices']) for p in parts)} triangles in {len(parts)} parts")
        for part in parts:
            to_game(part)
            chunks.append(pack_part(f"{model}/{part['material']}", part))
    if logo is None:
        sys.exit("no logo in the money set")
    with open(os.path.join(TARGET, "money.bytes"), "wb") as out:
        out.write(b"LZM1" + struct.pack("<i", len(chunks)) + b"".join(chunks))
    logo = logo.resize((LOGO, round(LOGO * logo.height / logo.width)), Image.LANCZOS)
    buffer = io.BytesIO()
    logo.save(buffer, "PNG", optimize=True)
    with open(os.path.join(TARGET, "money_logo.bytes"), "wb") as out:
        out.write(buffer.getvalue())
    print(f"money: {sum(len(c) for c in chunks) / 1e6:.1f} MB of meshes, {len(buffer.getvalue()) / 1e6:.2f} MB logo")


if __name__ == "__main__":
    main()
