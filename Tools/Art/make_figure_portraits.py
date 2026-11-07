#!/usr/bin/env python3
"""Renders the character portraits the menu, the lobby and the player cards show, from the game's own figures.

For every textured figure (Tools/Art/make_figures.py made figure_<id>.bytes and figure_<id>_skin.jpg) this
renders two pictures with Blender's Cycles on a transparent background, lit the same way for all of them:
  * Figure_<id>: the whole figure, three quarters towards the viewer, for the character cards (FIGURE_SIZE),
  * Face_<id>: head and shoulders, for the round avatar on a player card and a lobby seat (FACE_SIZE),
and writes them as PNG bytes next to the menu kit's pictures (Assets/_Lazpoly/UI/Resources/Lazpoly/Art/Menu),
where KitArt reads them.

Needs: pip install bpy numpy pillow (bpy is Blender as a Python module; it is only needed to make the pictures,
the game does not use it). Run from the repository root:
    python3 Tools/Art/make_figure_portraits.py suit cane teal hip navy purple boss mob
"""
import io
import math
import os
import struct
import sys
import tempfile
import uuid

import numpy as np
from PIL import Image
import bpy

MODELS = os.path.join("Assets", "_Lazpoly", "Data", "Resources", "Lazpoly", "Models")
TARGET = os.path.join("Assets", "_Lazpoly", "UI", "Resources", "Lazpoly", "Art", "Menu")
FIGURE_SIZE = (360, 600)
FACE_SIZE = (256, 256)
SAMPLES = 64
TURN = 18.0  # degrees the figure turns towards the viewer's right, so it does not stand flat


def read_high(path):
    """The High level of an LZF1 file: positions (Unity, left-handed), uvs and triangles."""
    data = open(path, "rb").read()
    if data[:4] != b"LZF1":
        raise SystemExit(f"{path} is not a figure")
    pos = 5
    for _ in range(data[4]):
        name_len = data[pos]
        name = data[pos + 1:pos + 1 + name_len].decode("ascii")
        pos += 1 + name_len
        (count,) = struct.unpack_from("<i", data, pos)
        pos += 4
        positions = np.frombuffer(data, np.float32, count * 3, pos).reshape(-1, 3)
        pos += count * 12
        pos += count * 6  # normals: Blender works them out again
        uvs = np.frombuffer(data, np.uint16, count * 2, pos).reshape(-1, 2) / 65535.0
        pos += count * 4
        size = data[pos]
        (indices,) = struct.unpack_from("<i", data, pos + 1)
        pos += 5
        triangles = np.frombuffer(data, np.uint32 if size == 4 else np.uint16, indices, pos).reshape(-1, 3)
        pos += indices * size
        if name == "High":
            return positions, uvs, triangles
    raise SystemExit(f"{path} has no High level")


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = SAMPLES
    scene.cycles.use_denoising = True
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.9, 0.88, 0.84, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.55
    scene.world = world
    return scene


def build(figure_id):
    positions, uvs, triangles = read_high(os.path.join(MODELS, f"figure_{figure_id}.bytes"))
    # Unity (x right, y up, z forward, left-handed) to Blender (x right, z up, -y forward): mirror x back.
    verts = np.column_stack([-positions[:, 0], -positions[:, 2], positions[:, 1]])
    mesh = bpy.data.meshes.new(figure_id)
    mesh.from_pydata(verts.tolist(), [], triangles[:, [0, 2, 1]].tolist())
    layer = mesh.uv_layers.new(name="uv")
    loop_uvs = uvs[triangles[:, [0, 2, 1]].reshape(-1)]
    layer.data.foreach_set("uv", loop_uvs.astype(np.float32).reshape(-1))
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    mesh.update()

    skin_path = next(os.path.join(MODELS, f"figure_{figure_id}_skin.{kind}") for kind in ("jpg", "png", "bytes")
                     if os.path.exists(os.path.join(MODELS, f"figure_{figure_id}_skin.{kind}")))
    skin = Image.open(skin_path).convert("RGB")
    temp = os.path.join(tempfile.gettempdir(), f"lazpoly_skin_{figure_id}.png")
    skin.save(temp)
    material = bpy.data.materials.new(figure_id)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    shader = nodes["Principled BSDF"]
    shader.inputs["Roughness"].default_value = 0.8
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = bpy.data.images.load(temp)
    texture.extension = "EXTEND"
    material.node_tree.links.new(texture.outputs["Color"], shader.inputs["Base Color"])
    mesh.materials.append(material)

    body = bpy.data.objects.new(figure_id, mesh)
    bpy.context.scene.collection.objects.link(body)
    body.rotation_euler = (0.0, 0.0, math.radians(TURN))
    return body


def light(scene):
    def lamp(name, kind, energy, location, size=1.0):
        data = bpy.data.lights.new(name, kind)
        data.energy = energy
        if kind == "AREA":
            data.size = size
        thing = bpy.data.objects.new(name, data)
        thing.location = location
        direction = -np.array(location, dtype=float) + np.array([0.0, 0.0, 0.55])
        thing.rotation_euler = direction_to_euler(direction)
        scene.collection.objects.link(thing)

    lamp("Key", "AREA", 90.0, (-1.4, -2.2, 2.0), 1.5)
    lamp("Fill", "AREA", 35.0, (1.8, -1.8, 1.0), 2.0)
    lamp("Rim", "AREA", 60.0, (0.6, 2.0, 1.8), 1.0)


def direction_to_euler(direction):
    from mathutils import Vector
    return Vector(direction).to_track_quat("-Z", "Y").to_euler()


def camera(scene):
    data = bpy.data.cameras.new("Camera")
    data.type = "ORTHO"
    data.sensor_fit = "VERTICAL"
    thing = bpy.data.objects.new("Camera", data)
    thing.rotation_euler = (math.radians(90.0), 0.0, 0.0)
    scene.collection.objects.link(thing)
    scene.camera = thing
    scene.render.resolution_percentage = 100
    return thing


def frame(scene, shot, centre, height, size):
    """Points the camera at <centre> (height on the figure) showing <height> of it, in a picture of <size>."""
    shot.location = (0.0, -4.0, centre)
    shot.data.ortho_scale = height
    scene.render.resolution_x, scene.render.resolution_y = size


def render(scene, path):
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


META = """fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def write(picture_name, temp):
    image = Image.open(temp).convert("RGBA")
    out = io.BytesIO()
    image.save(out, "PNG", optimize=True)
    target = os.path.join(TARGET, picture_name + ".bytes")
    with open(target, "wb") as handle:
        handle.write(out.getvalue())
    if not os.path.exists(target + ".meta"):  # Kept when it is there, so the guid does not change.
        with open(target + ".meta", "w", encoding="utf-8", newline="\n") as handle:
            handle.write(META.format(guid=uuid.uuid4().hex))
    print(f"{picture_name}: {image.size[0]}x{image.size[1]}, {len(out.getvalue()) // 1024} KB")


def main(ids):
    for figure_id in ids:
        scene = reset()
        build(figure_id)
        light(scene)
        shot = camera(scene)
        temp = os.path.join(tempfile.gettempdir(), f"lazpoly_{figure_id}.png")

        # The whole figure: one unit tall, a little room above the head and under the feet.
        frame(scene, shot, 0.5, 1.08, FIGURE_SIZE)
        render(scene, temp)
        write(f"Figure_{figure_id}", temp)

        # Head and shoulders: the top of the figure.
        frame(scene, shot, 0.855, 0.3, FACE_SIZE)
        render(scene, temp)
        write(f"Face_{figure_id}", temp)


if __name__ == "__main__":
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    main(sys.argv[1:])
