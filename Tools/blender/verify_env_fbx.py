"""Re-import exported structure/environment FBX files into a fresh scene and report
names, parents, material slots, root rotations, bounds and Muzzle positions.
Usage: blender -b -P Tools/blender/verify_env_fbx.py -- Structures/Turret Environment/Tree_A ...
(no args = every FBX in both folders)
"""
import glob
import math
import os
import sys

import bpy
from mathutils import Vector

ROOT = "/Users/manasvardhan/TactileWars/Assets/Art/Models"
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
files = ([os.path.join(ROOT, a + ".fbx") for a in args] if args else
         sorted(glob.glob(ROOT + "/Structures/*.fbx") + glob.glob(ROOT + "/Environment/*.fbx")))

for f in files:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=f, bake_space_transform=True)
    bpy.context.view_layer.update()
    objs = list(bpy.context.scene.objects)
    pts = [o.matrix_world @ Vector(c) for o in objs if o.type == "MESH" for c in o.bound_box]
    lo = Vector([min(p[i] for p in pts) for i in range(3)])
    hi = Vector([max(p[i] for p in pts) for i in range(3)])
    tris = 0
    for o in objs:
        if o.type == "MESH":
            o.data.calc_loop_triangles()
            tris += len(o.data.loop_triangles)
    print("FILE", os.path.basename(f), "tris", tris)
    print("  size xyz (Blender Z up) %.2f %.2f %.2f  min z %.3f  center xy %.2f %.2f" % (
        hi.x - lo.x, hi.y - lo.y, hi.z - lo.z, lo.z, (lo.x + hi.x) / 2, (lo.y + hi.y) / 2))
    for o in objs:
        rot = [round(math.degrees(a), 1) for a in o.rotation_euler]
        sc = [round(s, 3) for s in o.scale]
        mats = [m.name for m in o.data.materials] if o.type == "MESH" else []
        line = "  %s %s parent=%s rot=%s scale=%s mats=%s" % (
            o.name, o.type, o.parent.name if o.parent else None, rot, sc, mats)
        if o.type == "EMPTY":
            w = o.matrix_world.translation
            line += " world=(%.2f, %.2f, %.2f)" % (w.x, w.y, w.z)
        print(line)
