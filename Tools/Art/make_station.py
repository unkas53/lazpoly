#!/usr/bin/env python3
"""Turns the owner's bus station diorama (Meshy AI, one fused model: a bus at a stop) into two models, so the bus
can drive off with a player (the owner's wish, 2026-11-10): the stop without its bus, and the bus on its own.

The diorama is one connected surface; the bus only meets it where its tyres stand on the road. So every triangle
that reaches down to the road (CUT above its level) is set aside, what is left falls into separate pieces, and the
piece standing on the road side of the kerb is the bus. The thin slivers of tyre left on the road are pressed flat
onto it, and a dark patch just under the road's surface closes the four holes the tyres leave.

- figure_station.bytes: the stop, fitted exactly as make_corner_models.py fits a diorama (by the whole diorama's
  footprint, bus included: base at y = 0, centred on x and z, one unit wide along x, front at +Z), so it stands
  where it always stood. Its colour map (figure_station_skin.jpg) is written as before.
- figure_bus.bytes: the bus in its own frame, in the same units: wheels on y = 0, centred on its footprint, its
  nose towards +Z. It reads the station's colour map (FigureModels.SkinOf), since it is cut from the same model.

The bus's parking place in the station's frame is printed at the end; BusModel.ParkedAt and ParkedTurn hold it.

Needs: pip install numpy pillow meshoptimizer scipy. Run from the repository root:
    python3 Tools/Art/make_station.py
"""
import os
import struct
import sys

import numpy as np
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import connected_components

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_figures as figures  # noqa: E402

SOURCE = os.path.join("Tools", "Art", "Models", "Figures", "CartoonBusStopDiorama.glb")
ROAD = -0.33          # The road's surface in the model's own units (measured: -0.333 to -0.329).
CUT = ROAD + 0.004    # Triangles reaching below this touch the road.
SLIVER = ROAD + 0.002  # Left-over triangles under the bus that rise over this are tyre, not road.
PATCH = ROAD - 0.0008  # The patch under the tyres' holes.
STATION_HIGH, STATION_LOW = 90000, 18000
BUS_HIGH, BUS_LOW = 70000, 14000


def split(positions, indices):
    """Which triangles are the bus: the piece on the road side of the kerb once the road's triangles are set aside."""
    keys = np.round(positions * 1e5).astype(np.int64)
    _, welded = np.unique(keys, axis=0, return_inverse=True)
    welded = welded.ravel()
    triangles = welded[indices]
    heights = positions[indices][:, :, 1]
    above = heights.min(axis=1) > CUT
    kept = triangles[above]
    count = welded.max() + 1
    rows = np.concatenate([kept[:, 0], kept[:, 1]])
    columns = np.concatenate([kept[:, 1], kept[:, 2]])
    graph = coo_matrix((np.ones(len(rows), dtype=np.int8), (rows, columns)), shape=(count, count))
    _, labels = connected_components(graph, directed=False)
    piece = np.full(len(indices), -1, dtype=np.int64)
    piece[above] = labels[kept[:, 0]]
    centres = positions[indices].mean(axis=1)
    sizes = np.bincount(piece[above])
    bus = None
    for label in np.argsort(-sizes)[:4]:
        if centres[piece == label][:, 2].mean() > 0.1:  # In front of the kerb (the stop stands behind it).
            bus = piece == label
            break
    if bus is None:
        sys.exit("no bus found on the road side of the diorama")

    # The slivers of tyre between the cut and the road stay with the stop, pressed flat (see main).
    body = positions[indices[bus].reshape(-1)]
    low, high = body.min(axis=0), body.max(axis=0)
    under = (centres[:, 0] > low[0] - 0.02) & (centres[:, 0] < high[0] + 0.02) & (centres[:, 2] > low[2] - 0.02) & (centres[:, 2] < high[2] + 0.02)
    sliver = ~bus & under & (heights.max(axis=1) > SLIVER) & (heights.min(axis=1) <= CUT)
    return bus, sliver, low, high


def with_patch(positions, normals, uvs, indices, low, high, uv):
    """The model with a flat dark patch under the bus's footprint, facing up, to close the tyres' holes."""
    corners = np.array([[low[0], PATCH, high[2]], [high[0], PATCH, high[2]], [high[0], PATCH, low[2]], [low[0], PATCH, low[2]]])
    first = len(positions)
    positions = np.vstack([positions, corners])
    normals = np.vstack([normals, np.tile([0.0, 1.0, 0.0], (4, 1))])
    uvs = np.vstack([uvs, np.tile(uv, (4, 1))])
    indices = np.vstack([indices, [[first, first + 1, first + 2], [first, first + 2, first + 3]]])
    return positions, normals, uvs, indices


def dark_road_uv(positions, normals, uvs, low, high, colour):
    """A point of the colour map that shows the road in the bus's shadow: the darkest tenth under it, its middle one."""
    under = (normals[:, 1] > 0.9) & (np.abs(positions[:, 1] - ROAD) < 0.004)
    under &= (positions[:, 0] > low[0] + 0.05) & (positions[:, 0] < high[0] - 0.05) & (positions[:, 2] > low[2] + 0.05) & (positions[:, 2] < high[2] - 0.05)
    image = np.asarray(colour, dtype=np.float64)
    candidates = uvs[under]
    x = np.clip((candidates[:, 0] * image.shape[1]).astype(np.int64), 0, image.shape[1] - 1)
    y = np.clip((candidates[:, 1] * image.shape[0]).astype(np.int64), 0, image.shape[0] - 1)
    brightness = image[y, x].sum(axis=1)
    order = np.argsort(brightness)
    return candidates[order[len(order) // 20]]


def write_pack(model, positions, normals, uvs, indices, colour, high, low, extra=None):
    pack = bytearray(b"LZF2") + struct.pack("<B", 2)
    for level, triangles in (("High", high), ("Low", low)):
        kept = figures.simplify(positions, indices, triangles)
        if extra is not None:
            kept = np.vstack([kept, extra])
        p, n, u, i = figures.compact(positions, normals, uvs, kept)
        pack += figures.pack_level(level, p, n, u, i, figures.paint(u, i, colour, p))
        print(f"{model} {level}: {len(i)} triangles, {len(p)} vertices")
    with open(os.path.join(figures.TARGET, f"figure_{model}.bytes"), "wb") as out:
        out.write(pack)
    return len(pack)


def main():
    if not os.path.exists(SOURCE):
        sys.exit(f"missing {SOURCE}; run from the repository root")
    positions, normals, uvs, indices, colour = figures.read_glb(SOURCE)
    indices = indices.astype(np.int64)
    bus, sliver, low, high = split(positions, indices)
    print(f"bus: {bus.sum()} of {len(indices)} triangles, {sliver.sum()} tyre slivers pressed flat")

    # The whole diorama's footprint fits both models, as make_corner_models.py fitted it.
    whole_low, whole_high = positions.min(axis=0), positions.max(axis=0)
    width = whole_high[0] - whole_low[0]
    flipped = np.column_stack([uvs[:, 0], 1.0 - uvs[:, 1]])

    # The stop: mirrored on x into the game's axes, the patch added after the simplifying so it is never merged away.
    patch_uv = dark_road_uv(positions, normals, uvs, low, high, colour)
    pressed = positions.copy()
    tyre = np.unique(indices[sliver])
    pressed[tyre, 1] = np.minimum(pressed[tyre, 1], ROAD)
    stop_positions, stop_normals, stop_uvs, stop_indices = with_patch(pressed, normals, uvs, indices[~bus], low, high, patch_uv)
    patch = stop_indices[-2:][:, [0, 2, 1]]
    stop_indices = stop_indices[:-2][:, [0, 2, 1]]
    centre = np.array([-(whole_low[0] + whole_high[0]) / 2, whole_low[1], (whole_low[2] + whole_high[2]) / 2])
    stop_positions = (stop_positions * np.array([-1.0, 1.0, 1.0]) - centre) / width
    stop_normals = stop_normals * np.array([-1.0, 1.0, 1.0])
    stop_uvs = np.column_stack([stop_uvs[:, 0], 1.0 - stop_uvs[:, 1]])
    size = write_pack("station", stop_positions, stop_normals, stop_uvs, stop_indices, colour, STATION_HIGH, STATION_LOW, extra=patch)
    path, length = figures.write_skin("station", SOURCE, colour, largest=figures.SKIN)
    print(f"station: {size / 1e6:.1f} MB of meshes, {length / 1e6:.1f} MB colour map ({os.path.basename(path)})")

    # The bus: the model's x is its length (nose at +x) and z its width, so swapping them points its nose to +Z;
    # the swap mirrors it like the stop's mirror does, so the winding turns the same way.
    middle = np.array([(low[2] + high[2]) / 2, ROAD, (low[0] + high[0]) / 2])
    bus_positions = (positions[:, [2, 1, 0]] - middle) / width
    bus_normals = normals[:, [2, 1, 0]]
    bus_indices = indices[bus][:, [0, 2, 1]]
    size = write_pack("bus", bus_positions, bus_normals, flipped, bus_indices, colour, BUS_HIGH, BUS_LOW)
    body = bus_positions[np.unique(bus_indices)]
    extent = body.max(axis=0) - body.min(axis=0)
    print(f"bus: {size / 1e6:.1f} MB of meshes; {extent[0]:.4f} wide, {extent[1]:.4f} tall, {extent[2]:.4f} long (station widths)")

    # Where the bus stands in the stop's frame: its nose towards the stop's -X (a turn of -90 degrees about y).
    parked = (np.array([-(low[0] + high[0]) / 2, ROAD, (low[2] + high[2]) / 2]) - centre) / width
    print(f"BusModel.ParkedAt = ({parked[0]:.4f}, {parked[1]:.4f}, {parked[2]:.4f}); ParkedTurn = -90; "
          f"Width = {extent[0]:.4f}; Height = {body[:, 1].max():.4f} (the roof over the ground); Length = {extent[2]:.4f}")


if __name__ == "__main__":
    main()
