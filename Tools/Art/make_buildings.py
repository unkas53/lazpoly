#!/usr/bin/env python3
"""Turns the building models (Tools/Art/Models/Buildings/Lazpoly_Buildings.blend) into the game's building pack.

The Blender file holds six models, each an empty (Plot_00, House_01..House_04, Hotel_05) with its parts as named
child meshes: bodies, window and door openings (jambs, lintels, sills, mullions...), door panels, roofs, balconies,
the foundation and kerbs, the plot's sale sign. They share one white material. The game paints them:
the walls in the owner's colour and the rest in fixed colours, so this script sorts every part into a slot
(SLOT_RULES) and writes one mesh per slot. The window panes are no parts of their own: they are the faces of a
body at the back of a window opening, picked out as GLASS. The plot's sale sign is made SIGN_SCALE times larger
so it reads from the game's camera.

Every model goes into the pack twice: whole, as made (the game shows it at the High graphics quality), and a light
copy (<model>_Low) for Medium and Low, each slot reduced to about KEEP of its triangles with quadric edge collapse and
given normals again (smooth below CREASE degrees). Blender is Z-up and right-handed; the game is Y-up with the
fronts (doors) towards +Z: x is mirrored, y and z swapped, the winding flipped.

Format (little-endian): "LZB2", int32 mesh count, then per mesh: uint8 name length, ASCII name ("<model>/<slot>"),
int32 vertex count, float32 x/y/z per vertex, int16 normal x/y/z per vertex (x 1/32767), uint8 index size (2 or 4),
int32 index count, the indices (uint16 or uint32).

Needs Blender's Python module and the simplifier: pip install bpy fast-simplification (bpy 5.x runs on Python 3.13).
Run from the repository root: python3 Tools/Art/make_buildings.py
"""
import os
import re
import struct
import sys

import numpy as np

SOURCE = os.path.join("Tools", "Art", "Models", "Buildings", "Lazpoly_Buildings.blend")
TARGET = os.path.join("Assets", "_Lazpoly", "Data", "Resources", "Lazpoly", "Models", "buildings.bytes")
MODELS = ["Plot_00", "House_01", "House_02", "House_03", "House_04", "Hotel_05"]
LOW_SUFFIX = "_Low"
KEEP = 0.3      # share of the triangles the light copies keep
AGGRESSION = 3  # fast-simplification's agg: low keeps the shape at the cost of reaching KEEP
CREASE = 40.0   # degrees
SMALL = 150     # slots with fewer triangles are not thinned
SIGN_SCALE = 1.7

# The first rule whose pattern the part's name matches gives its slot; a part no rule takes stops the script.
SLOT_RULES = [
    (r"DoorPanel|BoundaryStake|SaleSign_Post", "Door"),
    (r"Handle|Hotel_H_", "Metal"),
    (r"Penthouse_Window", "Glass"),
    (r"_Opening_\d+_(Jamb|Lintel|Sill|Mullion|Transom|Horizontal|Vertical)|Balcony_(Rail|Post|Baluster)|Stringcourse|"
     r"Cornice|TowerBelt|Parapet|EntryColumn|SaleSign_Board", "Trim"),
    (r"Roof|Ridge|Chimney|Canopy|PorteCochere|Hotel_Crown", "Roof"),
    (r"Foundation|Curb|Step|Balcony_Slab", "Base"),
    (r"Body|CentralTower|Hotel_Wing|Penthouse|Paving|SaleSign_Inset", "Wall"),
]
SLOTS = ["Wall", "Trim", "Glass", "Door", "Roof", "Base", "Metal"]


def slot_of(name):
    for pattern, slot in SLOT_RULES:
        if re.search(pattern, name):
            return slot
    sys.exit(f"no slot for part {name}; add a rule to SLOT_RULES")


def to_game(points):
    """Blender (x, y, z) about the model's origin -> game (-x, z, -y)."""
    return np.stack([-points[:, 0], points[:, 2], -points[:, 1]], axis=1)


def part_triangles(obj, origin):
    """World-space triangles of a mesh object about `origin`: positions (n,3,3) and corner normals (n,3,3), Blender axes."""
    mesh = obj.data
    mesh.calc_loop_triangles()
    matrix = np.array(obj.matrix_world)
    normal_matrix = np.array(obj.matrix_world.to_3x3().inverted().transposed())
    co = np.array([v.co for v in mesh.vertices])
    co = co @ matrix[:3, :3].T + matrix[:3, 3] - origin
    corner = np.array([n.vector for n in mesh.corner_normals])
    corner = corner @ normal_matrix.T
    corner /= np.linalg.norm(corner, axis=1, keepdims=True) + 1e-20
    tris = mesh.loop_triangles
    loops = np.array([t.loops[:] for t in tris])
    verts = np.array([t.vertices[:] for t in tris])
    polys = np.array([t.polygon_index for t in tris])
    return co[verts], corner[loops], polys


def bounds(points):
    return points.reshape(-1, 3).min(axis=0), points.reshape(-1, 3).max(axis=0)


def glass_faces(body_name, body_positions, body_normals, openings):
    """Triangles of a body that form the back of one of its window openings (not doors)."""
    picked = np.zeros(len(body_positions), bool)
    centres = body_positions.mean(axis=1)
    face_normals = body_normals.mean(axis=1)
    for (owner, _), parts in openings.items():
        if owner != body_name or any("DoorPanel" in name for name, _ in parts):
            continue
        low = np.min([bounds(p)[0] for _, p in parts], axis=0)
        high = np.max([bounds(p)[1] for _, p in parts], axis=0)
        size = high - low
        across = 0 if size[0] < size[1] else 1           # the wall's normal runs along the opening's thin side
        along = 1 - across
        margin = np.array([0.0, 0.0, 0.0]); margin[across] = 0.06
        inside = np.all((centres >= low - margin) & (centres <= high + margin), axis=1)
        inside &= (centres[:, along] > low[along] + 0.004) & (centres[:, along] < high[along] - 0.004)
        inside &= (centres[:, 2] > low[2] + 0.004) & (centres[:, 2] < high[2] - 0.004)
        facing = np.abs(face_normals[:, across]) > 0.9
        picked |= inside & facing
    return picked


def gather(bpy, model):
    """The model's triangles per slot, in game axes: {slot: (positions (n,3,3), normals (n,3,3))}."""
    root = bpy.data.objects[model]
    origin = np.array(root.matrix_world.to_translation())
    parts = [o for o in root.children_recursive if o.type == "MESH"]
    loaded = {o.name: part_triangles(o, origin) for o in parts}

    sign = [n for n in loaded if n.startswith("SaleSign_")]
    if sign:
        low = np.min([bounds(loaded[n][0])[0] for n in sign], axis=0)
        high = np.max([bounds(loaded[n][0])[1] for n in sign], axis=0)
        pivot = np.array([(low[0] + high[0]) / 2, (low[1] + high[1]) / 2, low[2]])
        for n in sign:
            positions, normals, polys = loaded[n]
            loaded[n] = (pivot + (positions - pivot) * SIGN_SCALE, normals, polys)

    openings = {}
    for name in loaded:
        match = re.match(r"(.+)_Opening_(\d+)_", name)
        if match:
            openings.setdefault((match.group(1), match.group(2)), []).append((name, loaded[name][0]))

    slots = {}
    for name, (positions, normals, _) in loaded.items():
        slot = slot_of(name)
        if slot == "Wall" and any(owner == name for owner, _ in openings):
            glass = glass_faces(name, positions, normals, openings)
            add(slots, "Glass", positions[glass], normals[glass])
            positions, normals = positions[~glass], normals[~glass]
        add(slots, slot, positions, normals)

    out = {}
    for slot, (positions, normals) in slots.items():
        p = np.concatenate(positions); n = np.concatenate(normals)
        flat_p = to_game(p.reshape(-1, 3)).reshape(-1, 3, 3)[:, [0, 2, 1]]
        flat_n = to_game(n.reshape(-1, 3)).reshape(-1, 3, 3)[:, [0, 2, 1]]
        out[slot] = (flat_p, flat_n)
    return out


def add(slots, slot, positions, normals):
    if len(positions) == 0:
        return
    entry = slots.setdefault(slot, ([], []))
    entry[0].append(positions); entry[1].append(normals)


def index(positions, normals):
    """Shared vertices for triangles given corner by corner: one vertex per distinct position and normal."""
    corners_p = positions.reshape(-1, 3); corners_n = normals.reshape(-1, 3)
    key = np.concatenate([np.round(corners_p / 1e-5), np.round(corners_n * 1000)], axis=1).astype(np.int64)
    _, first, inverse = np.unique(key, axis=0, return_index=True, return_inverse=True)
    return corners_p[first], corners_n[first], inverse.reshape(-1, 3)


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
            new = len(out_positions)
            out_positions.append(positions[vertex])
            out_normals.append(group["normal"])
            for face in group["faces"]:
                row = out_triangles[face]
                row[triangles[face] == vertex] = new
    return np.array(out_positions), np.array(out_normals), out_triangles


def thin(simplifier, positions, normals, triangles):
    if len(triangles) < SMALL:
        return positions, normals, triangles
    welded, faces = weld(positions, triangles)
    reduced, faces = simplifier.simplify(welded, faces.astype(np.int32), target_count=max(12, int(len(triangles) * KEEP)), agg=AGGRESSION)
    return crease_normals(np.asarray(reduced, np.float64), np.asarray(faces, np.int64), CREASE)


def mesh_bytes(name, positions, normals, triangles):
    wide = len(positions) > 65535
    out = struct.pack("<B", len(name)) + name.encode("ascii")
    out += struct.pack("<i", len(positions)) + positions.astype("<f4").tobytes()
    out += np.clip(np.round(normals * 32767), -32767, 32767).astype("<i2").tobytes()
    out += struct.pack("<Bi", 4 if wide else 2, triangles.size) + triangles.astype("<u4" if wide else "<u2").tobytes()
    return out


def main():
    try:
        import bpy
        import fast_simplification
    except ImportError:
        sys.exit("needs Blender's Python module and the simplifier: pip install bpy fast-simplification")

    root = os.getcwd()
    source = os.path.join(root, SOURCE)
    if not os.path.exists(source):
        sys.exit(f"missing {source}; run from the repository root")
    bpy.ops.wm.open_mainfile(filepath=source)

    chunks = []
    for model in MODELS:
        slots = gather(bpy, model)
        whole_total = light_total = 0
        low, high = np.full(3, np.inf), np.full(3, -np.inf)
        for slot in SLOTS:
            if slot not in slots:
                continue
            positions, normals, triangles = index(*slots[slot])
            light = thin(fast_simplification, positions, normals, triangles)
            chunks.append(mesh_bytes(f"{model}/{slot}", positions, normals, triangles))
            chunks.append(mesh_bytes(f"{model}{LOW_SUFFIX}/{slot}", *light))
            whole_total += len(triangles); light_total += len(light[2])
            low = np.minimum(low, positions.min(axis=0)); high = np.maximum(high, positions.max(axis=0))
        print(f"{model:9s} {whole_total:6d} triangles, light copy {light_total:6d}; slots {', '.join(s for s in SLOTS if s in slots)}; "
              f"x {low[0]:+.3f}..{high[0]:+.3f} y {low[1]:.3f}..{high[1]:.3f} z {low[2]:+.3f}..{high[2]:+.3f}")

    target = os.path.join(root, TARGET)
    os.makedirs(os.path.dirname(target), exist_ok=True)
    with open(target, "wb") as out:
        out.write(b"LZB2" + struct.pack("<i", len(chunks)) + b"".join(chunks))
    print(f"wrote {TARGET}: {os.path.getsize(target)} bytes, {len(chunks)} meshes")


if __name__ == "__main__":
    main()
