#!/usr/bin/env python3
"""Turns the camp tent model (Tools/Art/Models/CampTent/CampTent.obj) into the game's owner-tent asset.

The source is a real-size tent (4.1 x 3.7 m with its guy lines and pegs). On a board square only the tent
itself fits, so this script:
  * drops the guy lines (group Guyline_Sand) and the pegs and line tags that sit outside the canvas
    (triangles with any vertex beyond |x| > PEG_LIMIT),
  * centres the tent on x/z, puts its base at y = 0 and scales it so its length along z is exactly 1,
  * writes vertices, normals and faces only (the model has no textures) as a TextAsset:
    Assets/_Lazpoly/Data/Resources/Lazpoly/Models/camp_tent.txt

The game reads it with ObjModel/TentParts; the group (usemtl) names tell the game which part is canvas.
Run from the repository root: python3 Tools/Art/make_tent.py
"""
import os
import sys

SOURCE = os.path.join("Tools", "Art", "Models", "CampTent", "CampTent.obj")
TARGET = os.path.join("Assets", "_Lazpoly", "Data", "Resources", "Lazpoly", "Models", "camp_tent.txt")
DROP_GROUPS = {"Guyline_Sand"}
PEG_LIMIT = 1.5  # metres from the centre line; the canvas and poles end at 1.45, pegs and tags start at 1.6


def read_obj(path):
    vertices, normals, groups, current = [], [], [], None
    with open(path, encoding="utf-8") as source:
        for line in source:
            parts = line.split()
            if not parts or parts[0] == "#":
                continue
            if parts[0] == "v":
                vertices.append(tuple(float(x) for x in parts[1:4]))
            elif parts[0] == "vn":
                normals.append(tuple(float(x) for x in parts[1:4]))
            elif parts[0] in ("o", "g"):
                current = {"name": parts[1] if len(parts) > 1 else f"group{len(groups)}", "material": None, "faces": []}
                groups.append(current)
            elif parts[0] == "usemtl" and current is not None:
                current["material"] = parts[1]
            elif parts[0] == "f":
                corners = []
                for corner in parts[1:]:
                    fields = corner.split("/")
                    v = int(fields[0])
                    vn = int(fields[2]) if len(fields) > 2 and fields[2] else 0
                    corners.append((v if v > 0 else len(vertices) + 1 + v, vn if vn > 0 else len(normals) + 1 + vn if vn else 0))
                current["faces"].append(corners)
    return vertices, normals, groups


def main():
    root = os.getcwd()
    source = os.path.join(root, SOURCE)
    if not os.path.exists(source):
        sys.exit(f"missing {source}; run from the repository root")
    vertices, normals, groups = read_obj(source)

    kept = []
    for group in groups:
        if group["name"] in DROP_GROUPS or group["material"] in DROP_GROUPS:
            continue
        faces = [f for f in group["faces"] if all(abs(vertices[v - 1][0]) <= PEG_LIMIT for v, _ in f)]
        if faces:
            kept.append({"name": group["name"], "material": group["material"] or group["name"], "faces": faces})

    used = sorted({v for g in kept for f in g["faces"] for v, _ in f})
    xs = [vertices[v - 1][0] for v in used]
    ys = [vertices[v - 1][1] for v in used]
    zs = [vertices[v - 1][2] for v in used]
    length = max(zs) - min(zs)
    scale = 1.0 / length
    cx, cz, y0 = (max(xs) + min(xs)) / 2, (max(zs) + min(zs)) / 2, min(ys)

    index_of = {v: i + 1 for i, v in enumerate(used)}
    used_normals = sorted({vn for g in kept for f in g["faces"] for _, vn in f if vn})
    normal_index = {vn: i + 1 for i, vn in enumerate(used_normals)}

    lines = [
        "# Lazpoly owner tent, made by Tools/Art/make_tent.py from Tools/Art/Models/CampTent/CampTent.obj",
        f"# length along z = 1, base at y = 0, centred; {sum(len(g['faces']) for g in kept)} triangles",
    ]
    for v in used:
        x, y, z = vertices[v - 1]
        lines.append(f"v {(x - cx) * scale:.4f} {(y - y0) * scale:.4f} {(z - cz) * scale:.4f}")
    for vn in used_normals:
        x, y, z = normals[vn - 1]
        lines.append(f"vn {x:.4f} {y:.4f} {z:.4f}")
    for group in kept:
        lines.append(f"o {group['name']}")
        lines.append(f"usemtl {group['material']}")
        for face in group["faces"]:
            lines.append("f " + " ".join(f"{index_of[v]}//{normal_index[vn]}" if vn else str(index_of[v]) for v, vn in face))

    target = os.path.join(root, TARGET)
    os.makedirs(os.path.dirname(target), exist_ok=True)
    with open(target, "w", encoding="utf-8", newline="\n") as out:
        out.write("\n".join(lines) + "\n")

    size = (max(xs) - min(xs)) * scale, (max(ys) - min(ys)) * scale, 1.0
    print(f"wrote {TARGET}: {len(kept)} groups, {sum(len(g['faces']) for g in kept)} triangles, {len(used)} vertices, "
          f"size x {size[0]:.3f} y {size[1]:.3f} z {size[2]:.3f} (unit = tent length)")
    for group in kept:
        print(f"  {group['material']:22s} {len(group['faces']):5d} triangles")


if __name__ == "__main__":
    main()
