#!/usr/bin/env python3
"""Turns the business man model (Tools/Art/Models/BusinessMan/BusinessMan.obj) into the game's pawn asset.

The source is a real-size figure (2.03 m) in Z-up metres facing -Y, split into 112 named parts that share
28 flat-coloured materials. On the board one figure is a pawn, so this script:
  * turns it Y-up facing +Z (x, y, z -> x, z, -y),
  * centres it on x and on its feet along z (the tablet reaches forward), puts the soles at y = 0 and
    scales it so it is exactly one unit tall,
  * merges the parts by material, one group per material (the jacket's four materials are the ones the
    game paints in the player's colour), so a pawn is 28 renderers, not 112,
  * gives every face its own flat normal (the model has none; smooth normals over its boxes and wedges
    would look melted), written once per distinct normal,
  * writes the result as a TextAsset: Assets/_Lazpoly/Data/Resources/Lazpoly/Models/business_man.txt

The game reads it with ObjModel/CharacterParts. Run from the repository root: python3 Tools/Art/make_character.py
"""
import os
import sys

SOURCE = os.path.join("Tools", "Art", "Models", "BusinessMan", "BusinessMan.obj")
TARGET = os.path.join("Assets", "_Lazpoly", "Data", "Resources", "Lazpoly", "Models", "business_man.txt")
FOOT_PARTS = {"L_Shoe_sole", "R_Shoe_sole"}


def read_obj(path):
    vertices, groups, current = [], [], None
    with open(path, encoding="utf-8") as source:
        for line in source:
            parts = line.split()
            if not parts or parts[0] == "#":
                continue
            if parts[0] == "v":
                x, y, z = (float(v) for v in parts[1:4])
                vertices.append((x, z, -y))  # Z-up facing -Y becomes Y-up facing +Z.
            elif parts[0] in ("o", "g"):
                current = {"name": parts[1] if len(parts) > 1 else f"group{len(groups)}", "material": None, "faces": []}
                groups.append(current)
            elif parts[0] == "usemtl" and current is not None:
                current["material"] = parts[1]
            elif parts[0] == "f":
                corners = [int(corner.split("/")[0]) for corner in parts[1:]]
                current["faces"].append([v if v > 0 else len(vertices) + 1 + v for v in corners])
    return vertices, groups


def face_normal(a, b, c):
    ux, uy, uz = b[0] - a[0], b[1] - a[1], b[2] - a[2]
    vx, vy, vz = c[0] - a[0], c[1] - a[1], c[2] - a[2]
    nx, ny, nz = uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx
    length = (nx * nx + ny * ny + nz * nz) ** 0.5
    if length < 1e-12:
        return (0.0, 1.0, 0.0)
    return (round(nx / length, 4), round(ny / length, 4), round(nz / length, 4))


def main():
    root = os.getcwd()
    source = os.path.join(root, SOURCE)
    if not os.path.exists(source):
        sys.exit(f"missing {source}; run from the repository root")
    vertices, groups = read_obj(source)

    xs = [v[0] for v in vertices]
    ys = [v[1] for v in vertices]
    feet = [vertices[v - 1] for g in groups if g["name"] in FOOT_PARTS for f in g["faces"] for v in f]
    if not feet:
        sys.exit("the model has no shoe soles to stand on")
    zs = [v[2] for v in feet]
    height = max(ys) - min(ys)
    scale = 1.0 / height
    cx, cz, y0 = (max(xs) + min(xs)) / 2, (max(zs) + min(zs)) / 2, min(ys)

    def placed(v):
        x, y, z = vertices[v - 1]
        return ((x - cx) * scale, (y - y0) * scale, (z - cz) * scale)

    # One group per material, in the order the materials first appear.
    merged, by_material = [], {}
    for group in groups:
        material = group["material"] or group["name"]
        if material not in by_material:
            by_material[material] = {"material": material, "faces": []}
            merged.append(by_material[material])
        by_material[material]["faces"].extend(group["faces"])

    used = sorted({v for g in merged for f in g["faces"] for v in f})
    index_of = {v: i + 1 for i, v in enumerate(used)}
    normals, normal_index = [], {}
    for group in merged:
        for face in group["faces"]:
            normal = face_normal(*(placed(v) for v in face[:3]))
            if normal not in normal_index:
                normal_index[normal] = len(normals) + 1
                normals.append(normal)

    lines = [
        "# Lazpoly pawn character, made by Tools/Art/make_character.py from Tools/Art/Models/BusinessMan/BusinessMan.obj",
        f"# one unit tall, soles at y = 0, feet centred, faces +z; {sum(len(g['faces']) for g in merged)} triangles",
    ]
    for v in used:
        x, y, z = placed(v)
        lines.append(f"v {x:.4f} {y:.4f} {z:.4f}")
    for x, y, z in normals:
        lines.append(f"vn {x:.4f} {y:.4f} {z:.4f}")
    for group in merged:
        lines.append(f"o {group['material']}")
        lines.append(f"usemtl {group['material']}")
        for face in group["faces"]:
            vn = normal_index[face_normal(*(placed(v) for v in face[:3]))]
            lines.append("f " + " ".join(f"{index_of[v]}//{vn}" for v in face))

    target = os.path.join(root, TARGET)
    os.makedirs(os.path.dirname(target), exist_ok=True)
    with open(target, "w", encoding="utf-8", newline="\n") as out:
        out.write("\n".join(lines) + "\n")

    all_z = [v[2] for v in vertices]
    print(f"wrote {TARGET}: {len(merged)} materials, {sum(len(g['faces']) for g in merged)} triangles, {len(used)} vertices, "
          f"{len(normals)} normals, size x {(max(xs) - min(xs)) * scale:.3f} y 1.000 z {(max(all_z) - min(all_z)) * scale:.3f} "
          f"(unit = height; source {height:.3f} m)")
    for group in merged:
        print(f"  {group['material']:14s} {len(group['faces']):5d} triangles")


if __name__ == "__main__":
    main()
