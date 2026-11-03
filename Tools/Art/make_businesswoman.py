#!/usr/bin/env python3
"""Derives the business woman from the business man (Tools/Art/Models/BusinessMan/BusinessMan.obj).

The owner's references (a business man and a business woman in one cartoon style) came as pictures; only the man
exists as a model. The woman is made from him so both stand in the same style, scale and palette:
  * beard, moustache, sideburns, quiff, fringe and hair ridges go; the hair grows long: a shell behind the head
    down past the shoulders, open at the face, plus small gold earrings,
  * the figure is slimmer below the head (waist, legs, shoes), the neck and brows finer,
  * the jeans become dark tights and a knee-length skirt is added (painted in the player's colour, a shade darker
    than the jacket, like a suit), the sneakers become plain dark shoes without laces or socks,
  * the T-shirt becomes a plum blouse (as in the reference picture), the smile gets rose lips.
The tablet, the hands, the face and the jacket stay as they are, so the two read as colleagues.

Writes Tools/Art/Models/BusinessWoman/BusinessWoman.obj in the man's own frame (Z-up metres, facing -Y); then
Tools/Art/make_character.py turns it into the game's asset. Run from the repository root:
    python3 Tools/Art/make_businesswoman.py && python3 Tools/Art/make_character.py woman
"""
import math
import os
import sys

import numpy as np

SOURCE = os.path.join("Tools", "Art", "Models", "BusinessMan", "BusinessMan.obj")
TARGET = os.path.join("Tools", "Art", "Models", "BusinessWoman", "BusinessWoman.obj")

DROPPED = {
    "Chin_beard", "L_Jaw_beard", "R_Jaw_beard", "L_Cheek_beard", "R_Cheek_beard", "L_Sideburn", "R_Sideburn",
    "L_Moustache", "R_Moustache", "Top_quiff", "Fringe_tip", "Hair_ridge_0", "Hair_ridge_1", "Hair_ridge_2",
    "L_Lace0", "L_Lace1", "L_Lace2", "R_Lace0", "R_Lace1", "R_Lace2", "L_Sock", "R_Sock",
    "L_Sneaker_tongue", "R_Sneaker_tongue", "L_Denim_outer_seam", "R_Denim_outer_seam", "L_Knee_fold", "R_Knee_fold",
    "Pocket_square",
}

# New material names; the game's palette (CharacterParts) knows them.
RENAMED = {
    "hair": "hair_w", "hair_light": "hair_w_light", "hair_dark": "hair_w_dark",
    "jeans": "tights", "jeans_light": "tights", "jeans_dark": "tights",
    "shoe": "pump", "shoe_s": "pump", "sole": "pump_sole",
    "shirt": "blouse", "shirt_shadow": "blouse_shadow", "mouth": "lips",
}

# Parts that keep their own material although the material is renamed elsewhere.
KEEP_MATERIAL = {"Hips_denim": "skirt"}

LEG_PARTS = {"L_Denim", "R_Denim"}
SHOE_PARTS = {"L_Shoe_sole", "L_Sneaker", "L_Sneaker_toe", "R_Shoe_sole", "R_Sneaker", "R_Sneaker_toe"}


def read_obj(path):
    vertices, groups, current = [], [], None
    with open(path, encoding="utf-8") as source:
        for line in source:
            parts = line.split()
            if not parts or parts[0] == "#":
                continue
            if parts[0] == "v":
                vertices.append([float(v) for v in parts[1:4]])
            elif parts[0] == "o":
                current = {"name": parts[1], "material": None, "faces": []}
                groups.append(current)
            elif parts[0] == "usemtl":
                current["material"] = parts[1]
            elif parts[0] == "f":
                current["faces"].append([int(c.split("/")[0]) - 1 for c in parts[1:]])
    return np.array(vertices), groups


def as_part(name, material, vertices, faces):
    """A part with its own vertex array and faces indexing into it."""
    return {"name": name, "material": material, "v": np.asarray(vertices, dtype=float), "f": [list(f) for f in faces]}


def split(vertices, groups):
    parts = []
    for group in groups:
        used = sorted({i for f in group["faces"] for i in f})
        local = {i: n for n, i in enumerate(used)}
        parts.append(as_part(group["name"], group["material"], vertices[used], [[local[i] for i in f] for f in group["faces"]]))
    return parts


def smooth_step(edge0, edge1, x):
    t = np.clip((x - edge0) / (edge1 - edge0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def slim_factor(z):
    """How much x shrinks at height z: legs most, the torso a little, the head not at all; the waist is pinched."""
    legs, torso, head = 0.86, 0.92, 1.0
    f = legs + (torso - legs) * smooth_step(0.78, 0.9, z)
    f = f + (head - f) * smooth_step(1.42, 1.52, z)
    waist = 0.06 * np.exp(-((z - 1.02) / 0.08) ** 2)
    return f - waist


def scale_about_centre(v, sx, sy, sz=1.0):
    c = (v.min(0) + v.max(0)) / 2
    return (v - c) * np.array([sx, sy, sz]) + c


def ring_loft(name, material, rings, start_angle, end_angle, steps, thickness):
    """A curved shell through elliptical arcs (cx, cy, z, rx, ry) from start to end angle, seen from both sides."""
    angles = np.linspace(start_angle, end_angle, steps + 1)
    outer, inner = [], []
    for cx, cy, z, rx, ry in rings:
        for a in angles:
            outer.append((cx + rx * math.cos(a), cy + ry * math.sin(a), z))
            inner.append((cx + (rx - thickness) * math.cos(a), cy + (ry - thickness) * math.sin(a), z))
    columns = len(angles)
    count = len(outer)
    faces = []
    for r in range(len(rings) - 1):
        for c in range(columns - 1):
            a, b = r * columns + c, r * columns + c + 1
            d, e = (r + 1) * columns + c, (r + 1) * columns + c + 1
            faces += [[a, d, b], [b, d, e]]  # outer side
            faces += [[count + a, count + b, count + d], [count + b, count + e, count + d]]  # inner side
    # Close the shell's open edges: both ends of every arc and the lowest ring.
    for r in range(len(rings) - 1):
        for c in (0, columns - 1):
            a, d = r * columns + c, (r + 1) * columns + c
            faces += [[a, count + a, d], [d, count + a, count + d]] if c == 0 else [[a, d, count + a], [d, count + d, count + a]]
    last = (len(rings) - 1) * columns
    for c in range(columns - 1):
        a, b = last + c, last + c + 1
        faces += [[a, b, count + a], [b, count + b, count + a]]
    return as_part(name, material, outer + inner, faces)


def frustum(name, material, rings, sides, cap_bottom=True):
    """A closed loft through full ellipses (cx, cy, z, rx, ry), top to bottom."""
    vertices, faces = [], []
    for cx, cy, z, rx, ry in rings:
        for s in range(sides):
            a = 2 * math.pi * s / sides
            vertices.append((cx + rx * math.cos(a), cy + ry * math.sin(a), z))
    for r in range(len(rings) - 1):
        for s in range(sides):
            a, b = r * sides + s, r * sides + (s + 1) % sides
            d, e = (r + 1) * sides + s, (r + 1) * sides + (s + 1) % sides
            faces += [[a, d, b], [b, d, e]]
    if cap_bottom:
        cx, cy, z, _, _ = rings[-1]
        centre = len(vertices)
        vertices.append((cx, cy, z))
        base = (len(rings) - 1) * sides
        for s in range(sides):
            faces.append([base + s, centre, base + (s + 1) % sides])
    return as_part(name, material, vertices, faces)


def icosphere(name, material, centre, radius):
    t = (1 + 5 ** 0.5) / 2
    v = [(-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0), (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t),
         (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1)]
    f = [[0, 11, 5], [0, 5, 1], [0, 1, 7], [0, 7, 10], [0, 10, 11], [1, 5, 9], [5, 11, 4], [11, 10, 2], [10, 7, 6],
         [7, 1, 8], [3, 9, 4], [3, 4, 2], [3, 2, 6], [3, 6, 8], [3, 8, 9], [4, 9, 5], [2, 4, 11], [6, 2, 10], [8, 6, 7],
         [9, 8, 1]]
    v = np.array(v, dtype=float)
    v = v / np.linalg.norm(v, axis=1)[:, None] * radius + np.array(centre)
    return as_part(name, material, v, f)


def outward_share(part):
    """Share of faces whose winding (counter-clockwise) points away from the part's centre."""
    v, centre, good = part["v"], part["v"].mean(0), 0
    for a, b, c in (f[:3] for f in part["f"]):
        n = np.cross(v[b] - v[a], v[c] - v[a])
        good += np.dot(n, (v[a] + v[b] + v[c]) / 3 - centre) > 0
    return good / max(len(part["f"]), 1)


def main():
    if not os.path.exists(SOURCE):
        sys.exit(f"missing {SOURCE}; run from the repository root")
    vertices, groups = read_obj(SOURCE)
    man = split(vertices, groups)
    if outward_share(next(p for p in man if p["name"] == "Head_flesh")) < 0.9:
        sys.exit("the source's winding is not counter-clockwise outward; the added parts would face inside")

    woman = []
    for part in man:
        if part["name"] in DROPPED:
            continue
        v = part["v"].copy()
        if part["name"] in LEG_PARTS:
            v = scale_about_centre(v, 0.78, 0.82)  # Slimmer legs in tights.
        elif part["name"] in SHOE_PARTS:
            v = scale_about_centre(v, 0.82, 0.9)
        elif part["name"] == "Neck":
            v = scale_about_centre(v, 0.84, 0.88)
        elif part["name"] in ("L_Brow", "R_Brow"):
            v = scale_about_centre(v, 0.92, 1.0, 0.6)
        v[:, 0] *= slim_factor(v[:, 2])
        material = KEEP_MATERIAL.get(part["name"], RENAMED.get(part["material"], part["material"]))
        woman.append(as_part(part["name"], material, v, part["f"]))

    # Long hair: a shell behind and beside the head, open at the face, falling past the shoulders.
    back = math.radians(-12), math.radians(192)
    woman.append(ring_loft("Long_hair", "hair_w", [
        (0.0, 0.05, 2.005, 0.07, 0.07),
        (0.0, 0.05, 1.975, 0.165, 0.16),
        (0.0, 0.045, 1.88, 0.212, 0.19),
        (0.0, 0.06, 1.74, 0.222, 0.17),
        (0.0, 0.085, 1.6, 0.21, 0.14),
        (0.0, 0.11, 1.47, 0.2, 0.1),
        (0.0, 0.12, 1.39, 0.17, 0.07),
    ], back[0], back[1], 14, 0.03))
    # A darker lock down the back, for depth.
    woman.append(ring_loft("Hair_lock", "hair_w_dark", [
        (0.0, 0.1, 1.72, 0.14, 0.15),
        (0.0, 0.125, 1.55, 0.13, 0.115),
        (0.0, 0.14, 1.41, 0.1, 0.075),
    ], math.radians(40), math.radians(140), 6, 0.02))
    # A side-swept fringe over the forehead, lighter where the light catches it.
    woman.append(ring_loft("Fringe", "hair_w_light", [
        (0.0, 0.03, 1.975, 0.17, 0.13),
        (0.0, 0.02, 1.9, 0.19, 0.135),
        (0.03, 0.0, 1.83, 0.18, 0.12),
    ], math.radians(200), math.radians(300), 8, 0.02))

    # The skirt: from the waist over the hips to just above the knee, a little flared.
    woman.append(frustum("Skirt", "skirt", [
        (0.0, 0.048, 0.935, 0.2 * slim_factor(0.935), 0.122),
        (0.0, 0.048, 0.85, 0.21 * slim_factor(0.85), 0.128),
        (0.0, 0.048, 0.7, 0.2, 0.128),
        (0.0, 0.048, 0.56, 0.205, 0.135),
    ], 18))

    # Small gold earrings under the ears.
    for side, x in (("L", -0.178), ("R", 0.178)):
        woman.append(icosphere(f"{side}_Earring", "accent", (x, 0.03, 1.63), 0.016))

    for part in woman:
        if part["name"] in ("Skirt",) or part["name"].endswith("_Earring"):
            if outward_share(part) < 0.9:
                sys.exit(f"{part['name']} faces inside")

    os.makedirs(os.path.dirname(TARGET), exist_ok=True)
    lines = ["# LAZPOLY Business Woman, derived from BusinessMan.obj by Tools/Art/make_businesswoman.py; units: metres; Z UP."]
    offset = 1
    for part in woman:
        lines.append(f"o {part['name']}")
        lines.append(f"usemtl {part['material']}")
        lines += [f"v {x:.7f} {y:.7f} {z:.7f}" for x, y, z in part["v"]]
        lines += ["f " + " ".join(str(i + offset) for i in face) for face in part["f"]]
        offset += len(part["v"])
    with open(TARGET, "w", encoding="utf-8", newline="\n") as out:
        out.write("\n".join(lines) + "\n")

    all_v = np.vstack([p["v"] for p in woman])
    triangles = sum(len(p["f"]) for p in woman)
    materials = sorted({p["material"] for p in woman})
    print(f"wrote {TARGET}: {len(woman)} parts, {len(materials)} materials, {triangles} triangles, "
          f"size {np.round(all_v.max(0) - all_v.min(0), 3)} m")
    print("  materials: " + ", ".join(materials))


if __name__ == "__main__":
    main()
