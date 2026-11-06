#!/usr/bin/env python3
"""Turns the owner's textured corner dioramas (Meshy AI image-to-3D GLB files) into the game's corner scenes.

The same pack format and steps as make_figures.py (read the GLB, glTF to Unity axes, two levels of detail with
meshoptimizer, a JPEG colour map), but a diorama is fitted by its footprint instead of its height: its base sits at
y = 0, its footprint is centred on x and z, and it is scaled so it is exactly one unit wide along x (its long side,
the front at +Z). BoardView then sizes it to the corner's scene room.

Writes Assets/_Lazpoly/Data/Resources/Lazpoly/Models/figure_<id>.bytes and figure_<id>_skin.bytes, which
FigureModels loads like a character (the corner scenes are not in its character list).

Needs: pip install numpy pillow meshoptimizer. Run from the repository root:
    python3 Tools/Art/make_corner_models.py station=Tools/Art/Models/Figures/CartoonBusStopDiorama.glb
"""
import io
import os
import struct
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_figures as figures  # noqa: E402


def to_game(positions, normals, uvs, indices):
    positions = positions * np.array([-1.0, 1.0, 1.0])
    normals = normals * np.array([-1.0, 1.0, 1.0])
    indices = indices[:, [0, 2, 1]]  # Mirroring x turns the faces inside out; the winding turns them back.
    uvs = np.column_stack([uvs[:, 0], 1.0 - uvs[:, 1]])
    low = positions.min(axis=0)
    high = positions.max(axis=0)
    width = high[0] - low[0]
    centre = np.array([(low[0] + high[0]) / 2, low[1], (low[2] + high[2]) / 2])
    return (positions - centre) / width, normals, uvs, indices


def main():
    jobs = sys.argv[1:]
    if not jobs or any("=" not in job for job in jobs):
        sys.exit(__doc__)
    for job in jobs:
        model, source = job.split("=", 1)
        positions, normals, uvs, indices, colour = figures.read_glb(source)
        positions, normals, uvs, indices = to_game(positions, normals, uvs, indices)
        size = positions.max(axis=0) - positions.min(axis=0)
        print(f"{model}: {size[0]:.2f} wide, {size[1]:.2f} tall, {size[2]:.2f} deep")
        pack = bytearray(b"LZF1") + struct.pack("<B", 2)
        for level, triangles in (("High", figures.HIGH), ("Low", figures.LOW)):
            kept = figures.simplify(positions, indices, triangles)
            p, n, u, i = figures.compact(positions, normals, uvs, kept)
            pack += figures.pack_level(level, p, n, u, i)
            print(f"{model} {level}: {len(i)} triangles, {len(p)} vertices (from {len(indices)})")
        with open(os.path.join(figures.TARGET, f"figure_{model}.bytes"), "wb") as out:
            out.write(pack)
        skin = colour.resize((figures.SKIN, figures.SKIN), Image.LANCZOS)
        buffer = io.BytesIO()
        skin.save(buffer, "JPEG", quality=figures.SKIN_QUALITY, optimize=True)
        with open(os.path.join(figures.TARGET, f"figure_{model}_skin.bytes"), "wb") as out:
            out.write(buffer.getvalue())
        print(f"{model}: {len(pack) / 1e6:.1f} MB of meshes, {len(buffer.getvalue()) / 1e6:.1f} MB colour map")


if __name__ == "__main__":
    main()
