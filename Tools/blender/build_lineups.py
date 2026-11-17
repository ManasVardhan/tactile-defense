"""Group previews: Docs/Previews/Structures_lineup.png and Environment_lineup.png.
Builds every asset in one scene on sand ground next to 1.0 unit stand-in figures.
Usage: blender -b -P Tools/blender/build_lineups.py -- [Structures|Environment]
"""
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import tw_env_common as C  # noqa: E402
import build_structures as S  # noqa: E402
import build_environment as E  # noqa: E402

ROWS = {
    "Structures": [["HQ", "Turret", "Tank", "Cannon", "Bunker", "TeslaAntenna"],
                   ["CapturePoint", "Tent", "Wall", "WallCorner", "Sandbags", "SpawnHatch",
                    "Mine"]],
    "Environment": [["Tree_B", "Tree_A", "Tree_C", "Bush_A", "Bush_B", "Rock_C", "Rock_B",
                     "Rock_A"],
                    ["Fence", "Crate", "Barrel", "Log", "GrassTuft", "Flower_A", "Flower_B",
                     "HexTile"]],
}


def bbox(objs):
    bpy.context.view_layer.update()
    pts = [o.matrix_world @ Vector(c) for o in objs if o.type == "MESH" for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def lineup(kind):
    C.reset_scene()
    builders = dict(S.BUILDERS)
    builders.update(E.BUILDERS)
    every = []
    y = 0.0
    for row in ROWS[kind]:
        x = 0.0
        row_depth = 0.0
        fig = C.stand_in_figure((x + 0.3, y, 0))
        every.append(fig)
        x += 1.0
        for name in row:
            objs = builders[name]()
            roots = [o for o in objs if o.parent is None]
            lo, hi = bbox(objs)
            for r in roots:
                r.location.x += x - lo.x
                r.location.y += y - (lo.y + hi.y) / 2
            x += (hi.x - lo.x) + 0.7
            row_depth = max(row_depth, hi.y - lo.y)
            every += objs
            if name in ("Bunker", "Rock_A", "Tent"):
                every.append(C.stand_in_figure((x - 0.35, y - 0.6, 0)))
                x += 0.3
        y -= row_depth + 1.4
    cam = C.setup_render(1600, 820)
    C.frame_camera(cam, every, elev=35, yaw=-12, margin=1.06, aspect=1600 / 820)
    C.render(os.path.join(C.PREV, kind + "_lineup.png"))


if __name__ == "__main__":
    kinds = C.cli_names() or ["Structures", "Environment"]
    for k in kinds:
        lineup(k)
