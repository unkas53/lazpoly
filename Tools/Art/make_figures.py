#!/usr/bin/env python3
"""Turns the owner's textured figures (Meshy AI image-to-3D GLB files) into the game's pawn assets.

Each source is one textured mesh without a skeleton (hundreds of thousands of triangles, a 4K-8K colour map).
The game shows a figure about 1.8 board units tall, so this script:
  * reads the GLB's mesh (positions, normals, UVs, indices) and its base colour map,
  * turns it from glTF's right-handed Y-up (front +Z) into Unity's left-handed Y-up (front +Z): x is mirrored and
    the winding flipped; v becomes 1 - v,
  * puts the soles at y = 0, centres x and z on the feet and scales it to exactly one unit tall,
  * simplifies it twice with meshoptimizer (which keeps the UV seams): HIGH triangles for the High graphics
    quality and LOW for Medium and Low,
  * writes Assets/_Lazpoly/Data/Resources/Lazpoly/Models/figure_<id>.bytes (the meshes) and
    figure_<id>_skin.bytes (the colour map as a JPEG, SKIN pixels square).

Format of the mesh file (little-endian): "LZF1", uint8 level count, then per level: uint8 name length, ASCII name
("High" or "Low"), int32 vertex count, float32 x/y/z per vertex, int16 normal x/y/z per vertex (1/32767), uint16 u/v
per vertex (1/65535), uint8 index size (2 or 4), int32 index count, the indices.

Needs: pip install numpy pillow meshoptimizer. Run from the repository root:
    python3 Tools/Art/make_figures.py <id>=<model.glb> [<id>=<model.glb> ...]
for example: python3 Tools/Art/make_figures.py suit=Tools/Art/Models/Figures/StylizedBusinessman.glb
"""
import io
import json
import os
import struct
import sys

import numpy as np
from PIL import Image
import meshoptimizer

TARGET = os.path.join("Assets", "_Lazpoly", "Data", "Resources", "Lazpoly", "Models")
HIGH = 120000
LOW = 24000
SKIN = 2048
SKIN_QUALITY = 90
FEET = 0.03  # share of the height counted as the feet when centring

COMPONENTS = {5126: np.float32, 5125: np.uint32, 5123: np.uint16, 5121: np.uint8}
WIDTH = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}


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

    if len(doc["meshes"]) != 1 or len(doc["meshes"][0]["primitives"]) != 1:
        sys.exit(f"{path}: expected one mesh with one part")
    primitive = doc["meshes"][0]["primitives"][0]
    attributes = primitive["attributes"]
    positions = accessor(attributes["POSITION"]).astype(np.float64)
    normals = accessor(attributes["NORMAL"]).astype(np.float64)
    uvs = accessor(attributes["TEXCOORD_0"]).astype(np.float64)
    indices = accessor(primitive["indices"]).astype(np.uint32).reshape(-1, 3)
    material = doc["materials"][primitive["material"]]
    texture = doc["textures"][material["pbrMetallicRoughness"]["baseColorTexture"]["index"]]
    image = doc["images"][texture["source"]]
    view = doc["bufferViews"][image["bufferView"]]
    start = view.get("byteOffset", 0)
    colour = Image.open(io.BytesIO(blob[start:start + view["byteLength"]])).convert("RGB")
    return positions, normals, uvs, indices, colour


def to_game(positions, normals, uvs, indices):
    positions = positions * np.array([-1.0, 1.0, 1.0])
    normals = normals * np.array([-1.0, 1.0, 1.0])
    indices = indices[:, [0, 2, 1]]  # Mirroring x turns the faces inside out; the winding turns them back.
    uvs = np.column_stack([uvs[:, 0], 1.0 - uvs[:, 1]])
    low, high = positions[:, 1].min(), positions[:, 1].max()
    height = high - low
    feet = positions[positions[:, 1] < low + FEET * height]
    centre = np.array([(feet[:, 0].min() + feet[:, 0].max()) / 2, low, (feet[:, 2].min() + feet[:, 2].max()) / 2])
    positions = (positions - centre) / height
    return positions, normals, uvs, indices


def simplify(positions, indices, triangles):
    flat = np.ascontiguousarray(indices.reshape(-1))
    vertices = np.ascontiguousarray(positions.astype(np.float32))
    if len(flat) // 3 <= triangles:
        return indices
    destination = np.zeros(len(flat), dtype=np.uint32)
    count = meshoptimizer.simplify(destination, flat, vertices, target_index_count=triangles * 3, target_error=0.02)
    return destination[:count].reshape(-1, 3)


def compact(positions, normals, uvs, indices):
    used = np.unique(indices)
    remap = np.full(len(positions), -1, dtype=np.int64)
    remap[used] = np.arange(len(used))
    return positions[used], normals[used], uvs[used], remap[indices]


def pack_level(name, positions, normals, uvs, indices):
    out = bytearray()
    encoded = name.encode("ascii")
    out += struct.pack("<B", len(encoded)) + encoded
    out += struct.pack("<i", len(positions))
    out += positions.astype("<f4").tobytes()
    lengths = np.linalg.norm(normals, axis=1, keepdims=True)
    unit = normals / np.where(lengths < 1e-9, 1.0, lengths)
    out += np.round(unit * 32767).astype("<i2").tobytes()
    if uvs.min() < -1e-4 or uvs.max() > 1 + 1e-4:
        sys.exit(f"{name}: UVs outside 0-1 ({uvs.min():.3f}..{uvs.max():.3f})")
    out += np.round(np.clip(uvs, 0, 1) * 65535).astype("<u2").tobytes()
    flat = indices.reshape(-1)
    wide = len(positions) > 65535
    out += struct.pack("<B", 4 if wide else 2) + struct.pack("<i", len(flat))
    out += flat.astype("<u4" if wide else "<u2").tobytes()
    return out


def main():
    jobs = sys.argv[1:]
    if not jobs or any("=" not in job for job in jobs):
        sys.exit(__doc__)
    for job in jobs:
        figure, source = job.split("=", 1)
        positions, normals, uvs, indices, colour = read_glb(source)
        positions, normals, uvs, indices = to_game(positions, normals, uvs, indices)
        pack = bytearray(b"LZF1") + struct.pack("<B", 2)
        for level, triangles in (("High", HIGH), ("Low", LOW)):
            kept = simplify(positions, indices, triangles)
            p, n, u, i = compact(positions, normals, uvs, kept)
            pack += pack_level(level, p, n, u, i)
            print(f"{figure} {level}: {len(i)} triangles, {len(p)} vertices (from {len(indices)})")
        with open(os.path.join(TARGET, f"figure_{figure}.bytes"), "wb") as out:
            out.write(pack)
        skin = colour.resize((SKIN, SKIN), Image.LANCZOS)
        buffer = io.BytesIO()
        skin.save(buffer, "JPEG", quality=SKIN_QUALITY, optimize=True)
        with open(os.path.join(TARGET, f"figure_{figure}_skin.bytes"), "wb") as out:
            out.write(buffer.getvalue())
        print(f"{figure}: {len(pack) / 1e6:.1f} MB of meshes, {len(buffer.getvalue()) / 1e6:.1f} MB colour map")


if __name__ == "__main__":
    main()
