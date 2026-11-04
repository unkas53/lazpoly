#!/usr/bin/env python3
"""Turns the building models (Tools/Art/Models/Buildings/*.glb) into the game's building pack.

The six models (the empty plot, houses of one to four storeys and the hotel) share one white material and
stand on a 0.90 x 0.72 footprint with their pivot at the middle of the ground; +Z is the front (the door).
The full models carry 2,000-51,000 triangles each, far more than a building a few dozen pixels wide on the
board needs, so this script:
  * reads each .glb (glTF is right-handed: x is mirrored and the winding flipped for Unity),
  * welds the vertices and reduces the mesh to about 30% of its triangles with quadric edge collapse
    (the fast-simplification package: pip install fast-simplification; only this tool needs it),
  * gives it normals again, smooth across faces that bend less than CREASE degrees and split where they bend more,
  * writes all six into one binary TextAsset the game reads with BuildingPack:
    Assets/_Lazpoly/Data/Resources/Lazpoly/Models/buildings.bytes

Format (little-endian): "LZB1", int32 mesh count, then per mesh: uint8 name length, ASCII name, int32 vertex
count, float32 x/y/z per vertex, int16 normal x/y/z per vertex (x 1/32767), int32 index count, uint16 indices.
Run from the repository root: python3 Tools/Art/make_buildings.py
"""
import json
import os
import struct
import sys

import numpy as np

SOURCE = os.path.join("Tools", "Art", "Models", "Buildings")
TARGET = os.path.join("Assets", "_Lazpoly", "Data", "Resources", "Lazpoly", "Models", "buildings.bytes")
MODELS = ["Plot_00", "House_01", "House_02", "House_03", "House_04", "Hotel_05"]
KEEP = 0.3      # share of the triangles kept
AGGRESSION = 3  # fast-simplification's agg: low keeps the shape (windows, the H sign) at the cost of reaching KEEP
CREASE = 40.0   # degrees


def load_glb(path):
    data = open(path, "rb").read()
    json_length, _ = struct.unpack("<II", data[12:20])
    gltf = json.loads(data[20:20 + json_length])
    offset = 20 + json_length
    bin_length, _ = struct.unpack("<II", data[offset:offset + 8])
    blob = data[offset + 8:offset + 8 + bin_length]

    def accessor(index):
        a = gltf["accessors"][index]
        view = gltf["bufferViews"][a["bufferView"]]
        width = {"SCALAR": 1, "VEC2": 2, "VEC3": 3}[a["type"]]
        kind = {5126: np.float32, 5125: np.uint32, 5123: np.uint16}[a["componentType"]]
        start = view.get("byteOffset", 0) + a.get("byteOffset", 0)
        values = np.frombuffer(blob, kind, a["count"] * width, start)
        return values.reshape(-1, width) if width > 1 else values

    primitive = gltf["meshes"][0]["primitives"][0]
    positions = accessor(primitive["attributes"]["POSITION"]).astype(np.float64) * [-1.0, 1.0, 1.0]
    triangles = accessor(primitive["indices"]).astype(np.int64).reshape(-1, 3)[:, [0, 2, 1]]
    return positions, triangles


def weld(positions, triangles, step=1e-5):
    keys = np.round(positions / step).astype(np.int64)
    _, first, inverse = np.unique(keys, axis=0, return_index=True, return_inverse=True)
    triangles = inverse.reshape(-1)[triangles]
    whole = (triangles[:, 0] != triangles[:, 1]) & (triangles[:, 1] != triangles[:, 2]) & (triangles[:, 0] != triangles[:, 2])
    return positions[first], triangles[whole]


def crease_normals(positions, triangles, crease):
    """Splits each vertex into one copy per group of faces that bend less than `crease` against each other."""
    edge1 = positions[triangles[:, 1]] - positions[triangles[:, 0]]
    edge2 = positions[triangles[:, 2]] - positions[triangles[:, 0]]
    weighted = np.cross(edge1, edge2)
    unit = weighted / (np.linalg.norm(weighted, axis=1, keepdims=True) + 1e-20)
    limit = np.cos(np.radians(crease))
    faces_of = [[] for _ in range(len(positions))]
    for face, corners in enumerate(triangles):
        for vertex in corners:
            faces_of[vertex].append(face)

    out_positions, out_normals, out_triangles = [], [], triangles.copy()
    for vertex, faces in enumerate(faces_of):
        groups = []
        for face in faces:
            for group in groups:
                if unit[face] @ group["normal"] >= limit:
                    group["faces"].append(face)
                    group["sum"] = group["sum"] + weighted[face]
                    group["normal"] = group["sum"] / (np.linalg.norm(group["sum"]) + 1e-20)
                    break
            else:
                groups.append({"faces": [face], "sum": weighted[face].copy(), "normal": unit[face].copy()})
        for group in groups:
            index = len(out_positions)
            out_positions.append(positions[vertex])
            out_normals.append(group["normal"])
            for face in group["faces"]:
                row = out_triangles[face]
                row[triangles[face] == vertex] = index
    return np.array(out_positions), np.array(out_normals), out_triangles


def main():
    try:
        import fast_simplification
    except ImportError:
        sys.exit("needs the fast-simplification package: pip install fast-simplification")

    root = os.getcwd()
    chunks = [b"LZB1", struct.pack("<i", len(MODELS))]
    for name in MODELS:
        source = os.path.join(root, SOURCE, name + ".glb")
        if not os.path.exists(source):
            sys.exit(f"missing {source}; run from the repository root")
        positions, triangles = load_glb(source)
        original = len(triangles)
        positions, triangles = weld(positions, triangles)
        reduced, faces = fast_simplification.simplify(positions, triangles.astype(np.int32),
                                                      target_count=int(original * KEEP), agg=AGGRESSION)
        positions, normals, triangles = crease_normals(np.asarray(reduced, np.float64), np.asarray(faces, np.int64), CREASE)
        if len(positions) > 65535:
            sys.exit(f"{name}: {len(positions)} vertices do not fit 16-bit indices")

        chunks.append(struct.pack("<B", len(name)) + name.encode("ascii"))
        chunks.append(struct.pack("<i", len(positions)) + positions.astype("<f4").tobytes())
        chunks.append(np.clip(np.round(normals * 32767), -32767, 32767).astype("<i2").tobytes())
        chunks.append(struct.pack("<i", triangles.size) + triangles.astype("<u2").tobytes())
        low, high = positions.min(axis=0), positions.max(axis=0)
        print(f"{name:9s} {original:6d} -> {len(triangles):6d} triangles, {len(positions):6d} vertices, "
              f"x {low[0]:+.3f}..{high[0]:+.3f} y {low[1]:.3f}..{high[1]:.3f} z {low[2]:+.3f}..{high[2]:+.3f}")

    target = os.path.join(root, TARGET)
    os.makedirs(os.path.dirname(target), exist_ok=True)
    with open(target, "wb") as out:
        out.write(b"".join(chunks))
    print(f"wrote {TARGET}: {os.path.getsize(target)} bytes")


if __name__ == "__main__":
    main()
