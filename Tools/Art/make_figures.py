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
    figure_<id>_skin.jpg (or .png), the colour map as a texture Unity imports (write_skin): the model's own image
    byte for byte when it is at most SKIN pixels, else scaled down to SKIN with a sharp, full-colour JPEG. Unity
    makes its mipmaps and compresses it to BC7 when the game is built (at most SKIN_IN_GAME pixels, see its .meta),
    so the game never decodes or compresses a colour map while playing. The old way (a 2048 JPEG with halved colour
    detail, compressed to DXT by the game itself) left grainy, speckled surfaces on the small figures.

Each vertex is also painted (paint): its colour map averaged over the triangles around it, on the surface. The game
shows that colour wherever the colour map would be read from its small mipmaps (the PaintedFigure shader), because
there the atlas's pieces, packed edge to edge, bleed into each other as grain and specks.

Format of the mesh file (little-endian): "LZF2", uint8 level count, then per level: uint8 name length, ASCII name
("High" or "Low"), int32 vertex count, float32 x/y/z per vertex, int16 normal x/y/z per vertex (1/32767), uint16 u/v
per vertex (1/65535), uint8 red/green/blue per vertex, uint8 index size (2 or 4), int32 index count, the indices.
("LZF1" files, from before the painting, have no colours.)

Needs: pip install numpy pillow meshoptimizer. Run from the repository root:
    python3 Tools/Art/make_figures.py <id>=<model.glb> [<id>=<model.glb> ...]
for example: python3 Tools/Art/make_figures.py suit=Tools/Art/Models/Figures/StylizedBusinessman.glb
With --skins first, only the colour maps are written (for any figure or corner model; the meshes stay as they are).
With --paint first, the meshes already in the game are painted from their models' colour maps (nothing else changes).
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
SKIN = 4096
SKIN_IN_GAME = 2048
SKIN_QUALITY = 95
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


def paint(uvs, indices, colour, positions):
    """
    Each vertex's colour: the colour map averaged over every triangle that meets at the vertex (15 samples spread over
    each triangle, bilinear), weighted by the triangles' areas on the model. It is a filter on the surface itself, so
    unlike the map's mipmaps it never mixes in the pieces that merely lie next to the vertex's piece on the atlas.
    """
    image = np.asarray(colour, dtype=np.uint8)
    height, width = image.shape[0], image.shape[1]
    steps = [(a, b, 4 - a - b) for a in range(5) for b in range(5 - a)]
    weights = np.array(steps, dtype=np.float64)
    weights = (weights + 0.25) / (weights + 0.25).sum(axis=1, keepdims=True)  # keep the samples inside the triangle
    corners = uvs[indices]  # triangles x 3 x 2, in the game's coordinates (v up from the image's bottom row)
    total = np.zeros((len(indices), 3))
    for weight in weights:
        point = (corners * weight[None, :, None]).sum(axis=1)
        x = np.clip(point[:, 0] * width - 0.5, 0, width - 1.001)
        y = np.clip((1.0 - point[:, 1]) * height - 0.5, 0, height - 1.001)
        x0, y0 = np.floor(x).astype(np.int64), np.floor(y).astype(np.int64)
        fx, fy = (x - x0)[:, None], (y - y0)[:, None]
        top = image[y0, x0] * (1 - fx) + image[y0, x0 + 1] * fx
        bottom = image[y0 + 1, x0] * (1 - fx) + image[y0 + 1, x0 + 1] * fx
        total += top * (1 - fy) + bottom * fy
    means = total / len(weights)
    triangle = positions[indices]
    areas = np.linalg.norm(np.cross(triangle[:, 1] - triangle[:, 0], triangle[:, 2] - triangle[:, 0]), axis=1) + 1e-12
    sums = np.zeros((len(uvs), 3))
    mass = np.zeros(len(uvs))
    for corner in range(3):
        np.add.at(sums, indices[:, corner], means * areas[:, None])
        np.add.at(mass, indices[:, corner], areas)
    painted = sums / np.maximum(mass, 1e-12)[:, None]
    return np.clip(np.round(painted), 0, 255).astype(np.uint8)


def read_pack(path):
    """The levels of a figure file: (name, positions, normals, uvs, indices), as make_figures.py wrote them."""
    data = open(path, "rb").read()
    magic, count = data[:4], data[4]
    if magic not in (b"LZF1", b"LZF2"):
        sys.exit(f"{path}: not a figure file")
    offset, levels = 5, []
    for _ in range(count):
        length = data[offset]
        name = data[offset + 1:offset + 1 + length].decode("ascii")
        offset += 1 + length
        vertices, = struct.unpack_from("<i", data, offset)
        offset += 4
        positions = np.frombuffer(data, "<f4", vertices * 3, offset).reshape(-1, 3).astype(np.float64)
        offset += vertices * 12
        normals = np.frombuffer(data, "<i2", vertices * 3, offset).reshape(-1, 3) / 32767.0
        offset += vertices * 6
        uvs = np.frombuffer(data, "<u2", vertices * 2, offset).reshape(-1, 2) / 65535.0
        offset += vertices * 4
        if magic == b"LZF2":
            offset += vertices * 3
        size = data[offset]
        count_indices, = struct.unpack_from("<i", data, offset + 1)
        offset += 5
        indices = np.frombuffer(data, "<u4" if size == 4 else "<u2", count_indices, offset).astype(np.int64).reshape(-1, 3)
        offset += count_indices * size
        levels.append((name, positions, normals, uvs, indices))
    return levels


def pack_level(name, positions, normals, uvs, indices, colours):
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
    out += np.ascontiguousarray(colours, dtype=np.uint8).tobytes()
    flat = indices.reshape(-1)
    wide = len(positions) > 65535
    out += struct.pack("<B", 4 if wide else 2) + struct.pack("<i", len(flat))
    out += flat.astype("<u4" if wide else "<u2").tobytes()
    return out


def original_colour(path):
    """The GLB's base colour image exactly as it is stored (JPEG or PNG bytes)."""
    data = open(path, "rb").read()
    json_length, = struct.unpack("<I", data[12:16])
    doc = json.loads(data[20:20 + json_length])
    offset = 20 + json_length
    bin_length, = struct.unpack("<I", data[offset:offset + 4])
    blob = data[offset + 8:offset + 8 + bin_length]
    material = doc["materials"][doc["meshes"][0]["primitives"][0]["material"]]
    texture = doc["textures"][material["pbrMetallicRoughness"]["baseColorTexture"]["index"]]
    view = doc["bufferViews"][doc["images"][texture["source"]]["bufferView"]]
    start = view.get("byteOffset", 0)
    return blob[start:start + view["byteLength"]]


SKIN_META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: {size}
  textureSettings:
    serializedVersion: 2
    filterMode: 2
    aniso: 8
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 100
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 0
  alphaIsTransparency: 0
  spriteTessellationDetail: -1
  textureType: 0
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 1
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: {size}
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 2
    compressionQuality: 100
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 1
  - serializedVersion: 4
    buildTarget: Standalone
    maxTextureSize: {size}
    resizeAlgorithm: 0
    textureFormat: 25
    textureCompression: 2
    compressionQuality: 100
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 1
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 1
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData: 
    physicsShape: []
    bones: []
    spriteID: 
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def write_skin(figure, source, colour, largest=SKIN, in_game=SKIN_IN_GAME):
    """
    Writes the figure's colour map as a texture for Unity to import: the model's own image byte for byte when it is at
    most `largest` pixels (None: whatever its size), else scaled down to `largest` as a JPEG at quality SKIN_QUALITY
    with full colour detail (no chroma subsampling, which smeared the colour across the atlas's tightly packed pieces).
    A new file gets a .meta that imports it with mipmaps, trilinear filtering and BC7 at most `in_game` pixels; an
    existing .meta is kept. Older colour maps of the figure (the runtime JPEG, the other image type) are removed.
    """
    import uuid
    original = original_colour(source)
    kind = "png" if original[:4] == b"\x89PNG" else "jpg" if original[:3] == b"\xff\xd8\xff" else None
    if kind is not None and (largest is None or max(colour.size) <= largest):
        data, extension = original, kind
    else:
        buffer = io.BytesIO()
        colour.resize((largest, largest), Image.LANCZOS).save(buffer, "JPEG", quality=SKIN_QUALITY, subsampling=0, optimize=True)
        data, extension = buffer.getvalue(), "jpg"
    stale = [f"figure_{figure}_skin.bytes", f"figure_{figure}_skin.{'png' if extension == 'jpg' else 'jpg'}"]
    for name in stale:
        for path in (os.path.join(TARGET, name), os.path.join(TARGET, name + ".meta")):
            if os.path.exists(path):
                os.remove(path)
    path = os.path.join(TARGET, f"figure_{figure}_skin.{extension}")
    with open(path, "wb") as out:
        out.write(data)
    if not os.path.exists(path + ".meta"):
        with open(path + ".meta", "w", newline="\n") as out:
            out.write(SKIN_META.format(guid=uuid.uuid4().hex, size=in_game))
    return path, len(data)


def main():
    jobs = sys.argv[1:]
    skins_only = bool(jobs) and jobs[0] == "--skins"
    paint_only = bool(jobs) and jobs[0] == "--paint"
    if skins_only or paint_only:
        jobs = jobs[1:]
    if not jobs or any("=" not in job for job in jobs):
        sys.exit(__doc__)
    for job in jobs:
        figure, source = job.split("=", 1)
        if paint_only:
            colour = read_glb(source)[4]
            path = os.path.join(TARGET, f"figure_{figure}.bytes")
            pack = bytearray(b"LZF2")
            levels = read_pack(path)
            pack += struct.pack("<B", len(levels))
            for name, p, n, u, i in levels:
                pack += pack_level(name, p, n, u, i, paint(u, i, colour, p))
            with open(path, "wb") as out:
                out.write(pack)
            print(f"{figure}: painted {', '.join(name for name, *_ in levels)} ({len(pack) / 1e6:.1f} MB)")
            continue
        if skins_only:
            colour = read_glb(source)[4]
            path, size = write_skin(figure, source, colour)
            print(f"{figure}: {colour.size[0]} px colour map -> {os.path.basename(path)}, {size / 1e6:.1f} MB")
            continue
        positions, normals, uvs, indices, colour = read_glb(source)
        positions, normals, uvs, indices = to_game(positions, normals, uvs, indices)
        pack = bytearray(b"LZF2") + struct.pack("<B", 2)
        for level, triangles in (("High", HIGH), ("Low", LOW)):
            kept = simplify(positions, indices, triangles)
            p, n, u, i = compact(positions, normals, uvs, kept)
            pack += pack_level(level, p, n, u, i, paint(u, i, colour, p))
            print(f"{figure} {level}: {len(i)} triangles, {len(p)} vertices (from {len(indices)})")
        with open(os.path.join(TARGET, f"figure_{figure}.bytes"), "wb") as out:
            out.write(pack)
        path, size = write_skin(figure, source, colour)
        print(f"{figure}: {len(pack) / 1e6:.1f} MB of meshes, {size / 1e6:.1f} MB colour map ({os.path.basename(path)})")


if __name__ == "__main__":
    main()
