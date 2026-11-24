"""Layout props for the per-level battlefields (our own designs, flat shaded toon style like build_biomes.py).
Usage: blender -b -P Tools/blender/build_layout_props.py -- [HQ_Meadow Wall_Canyon ...]
Exports to Assets/Art/Models/Environment and renders Docs/Previews/<Name>.png.

Enemy HQ per biome (same 5 x 5 footprint as the old keep, gate toward -Y, team colored roof and banners):
  HQ_Meadow (timber stockade hall), HQ_Canyon (adobe stronghold), HQ_Ridge (stone bastion)
Gameplay walls per biome, 2.0 long along X and 0.74 deep like the old Wall, a 0.96 corner and a 2.2 tower:
  Wall_Meadow / WallCorner_Meadow / Tower_Meadow (dry stone), Wall_Canyon / ... (adobe), Wall_Ridge / ... (dark
  slate with snow caps)
Landmarks and terrain pieces: Farmhouse, Bridge (deck 9 wide, walkable), Ford (stepping stones, walkable),
  Crag_A, Crag_B (snowy slate crags for the rock spine), Mesa_C (tall pale butte), Mesa_D (low dark table),
  Rubble (breach debris, walkable), Reeds (bank plants, walkable)
"""
import os
import sys
import math
import random

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import tw_env_common as C  # noqa: E402
from tw_env_common import cyl, box, sphere, blob, poly, join, rod, recolor_faces  # noqa: E402

LIGHT = (-0.9, -0.25, 0.45)


def lit(o, dark, light, thresh=0.3):
    from mathutils import Vector
    L = Vector(LIGHT).normalized()
    recolor_faces(o, light, lambda p, i: o.data.materials[p.material_index].name == dark and p.normal.dot(L) > thresh)


def tops(o, base, top, nz=0.7):
    recolor_faces(o, top, lambda p, i: o.data.materials[p.material_index].name == base and p.normal.z > nz)


def gable(w, d, h, mat, loc, eave=0.15):
    """A gabled roof: ridge along X, w long, d deep, h tall, base at loc z."""
    x0, x1 = -w / 2 - eave, w / 2 + eave
    y0, y1 = -d / 2 - eave, d / 2 + eave
    v = [(x0, y0, 0), (x1, y0, 0), (x1, y1, 0), (x0, y1, 0), (x0, 0, h), (x1, 0, h)]
    f = [(0, 1, 5, 4), (2, 3, 4, 5), (0, 4, 3), (1, 2, 5), (0, 3, 2, 1)]
    return poly(v, f, mat, loc=loc, solidify=0.08)


def banner(x, y, z, h=1.4, w=0.5, face=-1):
    """A pole with a hanging team banner facing +-Y."""
    return [cyl(0.05, h + 0.4, "Wood", (x, y, z), seg=8, bevel=0),
            box(w, 0.04, h * 0.55, "TeamColor", (x, y + face * 0.06, z + h + 0.1 - h * 0.3), bevel=0.0),
            box(w, 0.045, 0.08, "TeamColorDark", (x, y + face * 0.06, z + h + 0.1 - h * 0.6), bevel=0.0)]


# ================================================================== HQs

def hq_meadow():
    """Timber stockade hall: a ring of sharpened logs around a long hall with a team colored gable roof and a
    lookout tower."""
    rnd = random.Random(301)
    parts = [box(5.0, 5.0, 0.16, "Rock", (0, 0, 0.08), bevel=0.05, bseg=1)]
    # palisade: logs along the square edge, gap at the front gate
    n = 9
    for side in range(4):
        for i in range(n):
            t = -2.25 + 4.5 * (i + 0.5) / n
            if side == 0 and abs(t) < 0.8:
                continue
            x, y = [(t, -2.3), (2.3, t), (t, 2.3), (-2.3, t)][side]
            h = rnd.uniform(1.15, 1.4)
            parts.append(cyl(0.2, h, "Trunk", (x, y, 0.1), seg=8, bevel=0.0))
            parts.append(cyl(0.2, 0.28, "Wood", (x, y, 0.1 + h), seg=8, r2=0.02, bevel=0.0))
    # gate posts
    for sx in (-1, 1):
        parts.append(box(0.36, 0.36, 1.9, "Trunk", (sx * 0.95, -2.3, 0.95 + 0.1), bevel=0.04, bseg=1))
    parts.append(box(2.3, 0.3, 0.26, "Wood", (0, -2.3, 1.95), bevel=0.04, bseg=1))
    # hall
    parts += [box(3.0, 2.0, 1.25, "Wood", (0, 0.35, 0.72), bevel=0.04, bseg=1),
              box(3.06, 2.06, 0.18, "Trunk", (0, 0.35, 0.2), bevel=0.03, bseg=1),
              box(0.7, 0.1, 0.85, "Dark", (0, -0.68, 0.55), bevel=0.02, bseg=1)]
    for x in (-1.2, 1.2):
        parts.append(box(0.36, 0.08, 0.3, "Dark", (x, -0.67, 0.95), bevel=0.01, bseg=1))
    roof = gable(3.0, 2.0, 1.15, "TeamColor", (0, 0.35, 1.34))
    recolor_faces(roof, "TeamColorDark", lambda p, i: p.normal.y > 0.3)
    parts.append(roof)
    # lookout tower back left
    tx, ty = -1.55, 1.5
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(cyl(0.09, 2.6, "Trunk", (tx + sx * 0.4, ty + sy * 0.4, 0.1), seg=8, bevel=0))
    parts += [box(1.1, 1.1, 0.12, "Wood", (tx, ty, 2.55), bevel=0.02, bseg=1),
              box(1.1, 0.08, 0.4, "Wood", (tx, ty - 0.52, 2.8), bevel=0.01, bseg=1),
              box(1.1, 0.08, 0.4, "Wood", (tx, ty + 0.52, 2.8), bevel=0.01, bseg=1)]
    parts.append(cyl(0.82, 0.6, "TeamColor", (tx, ty, 3.0), seg=4, r2=0.0, rot=(0, 0, 45), bevel=0.0))
    parts += banner(1.7, -1.7, 0.1, h=1.6)
    parts += [box(0.6, 0.6, 0.6, "Hay", (1.6, 1.4, 0.4), rot=(0, 0, 12), bevel=0.06, bseg=1)]
    o = join(parts, "HQ_Meadow")
    lit(o, "Wood", "Hay", 0.75)
    return [o]


def hq_canyon():
    """Adobe stronghold: stepped clay blocks with rounded parapets, protruding roof beams, a team colored awning
    and banners."""
    parts = [box(5.0, 5.0, 0.16, "ClayDark", (0, 0, 0.08), bevel=0.05, bseg=1)]
    # outer adobe wall, front opening
    for (x, y, sx, sy) in ((-1.6, -2.2, 1.8, 0.5), (1.6, -2.2, 1.8, 0.5), (0, 2.2, 5.0, 0.5),
                           (-2.25, 0, 0.5, 4.4), (2.25, 0, 0.5, 4.4)):
        parts.append(box(sx, sy, 1.0, "ClayLight", (x, y, 0.6), bevel=0.14, bseg=2))
    # stepped keep
    parts += [box(3.2, 2.6, 1.5, "Clay", (0, 0.4, 0.85), bevel=0.12, bseg=2),
              box(2.2, 1.8, 1.1, "ClayLight", (0.3, 0.7, 2.1), bevel=0.12, bseg=2),
              box(1.1, 1.1, 0.9, "Clay", (-0.7, 0.9, 2.6), bevel=0.1, bseg=2)]
    # roof beams poking out
    for i in range(5):
        x = -1.3 + i * 0.65
        parts.append(cyl(0.07, 0.5, "DryWood", (x, -0.95, 1.4), rot=(90, 0, 0), seg=8, bevel=0))
    for i in range(3):
        x = -0.4 + i * 0.6
        parts.append(cyl(0.06, 0.4, "DryWood", (x, -0.25, 2.45), rot=(90, 0, 0), seg=8, bevel=0))
    # dark doorway and team awning
    parts += [box(0.8, 0.12, 1.0, "Dark", (0, -0.92, 0.6), bevel=0.05, bseg=1),
              box(1.6, 0.9, 0.06, "TeamColor", (0, -1.35, 1.25), rot=(-14, 0, 0), bevel=0.0)]
    for sx in (-1, 1):
        parts.append(cyl(0.05, 1.2, "DryWood", (sx * 0.75, -1.75, 0.1), seg=8, bevel=0))
    # windows
    for x in (-1.0, 1.0):
        parts.append(box(0.3, 0.1, 0.36, "Dark", (x, -0.9, 1.15), bevel=0.04, bseg=1))
    parts.append(box(0.3, 0.1, 0.3, "Dark", (0.3, -0.22, 2.2), bevel=0.04, bseg=1))
    parts += banner(-1.9, -2.2, 1.1, h=1.2)
    parts += banner(1.9, -2.2, 1.1, h=1.2)
    parts.append(box(0.9, 0.9, 0.08, "TeamColorDark", (-0.7, 0.9, 3.08), bevel=0.0))
    parts += [cyl(0.3, 0.55, "Clay", (1.6, 1.5, 1.1), seg=10, r2=0.22, bevel=0.03, bseg=1)]
    o = join(parts, "HQ_Canyon", smooth_angle=30)
    lit(o, "Clay", "ClayLight", 0.6)
    return [o]


def hq_ridge():
    """Stone bastion: an angular dark slate fort with corner towers, a tall square keep with snowy merlons and team
    banners on the front."""
    parts = [box(5.0, 5.0, 0.18, "Slate", (0, 0, 0.09), bevel=0.05, bseg=1)]
    for (x, y, sx, sy) in ((-1.55, -2.15, 1.9, 0.6), (1.55, -2.15, 1.9, 0.6), (0, 2.15, 4.9, 0.6),
                           (-2.15, 0, 0.6, 4.9), (2.15, 0, 0.6, 4.9)):
        parts.append(box(sx, sy, 1.2, "Slate", (x, y, 0.75), bevel=0.06, bseg=1))
        parts.append(box(sx + 0.04, sy + 0.04, 0.14, "Snow", (x, y, 1.4), bevel=0.04, bseg=1))
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(box(1.0, 1.0, 1.8, "SlateLight", (sx * 2.1, sy * 2.1, 0.95), bevel=0.06, bseg=1))
            parts.append(box(1.06, 1.06, 0.18, "Snow", (sx * 2.1, sy * 2.1, 1.92), bevel=0.05, bseg=1))
    # keep
    parts += [box(2.2, 2.2, 3.0, "Slate", (0, 0.4, 1.6), bevel=0.06, bseg=1),
              box(2.4, 2.4, 0.24, "SlateLight", (0, 0.4, 3.2), bevel=0.05, bseg=1)]
    for i in range(4):
        t = -0.85 + i * 0.57
        for (x, y) in ((t, -0.72), (t, 1.52), (-1.12, 0.4 + t), (1.12, 0.4 + t)):
            parts.append(box(0.3, 0.3, 0.32, "SlateLight", (x, y, 3.47), bevel=0.04, bseg=1))
    parts.append(box(2.0, 2.0, 0.1, "Snow", (0, 0.4, 3.36), bevel=0.02, bseg=1))
    parts += [box(0.9, 0.14, 1.2, "Dark", (0, -0.72, 0.75), bevel=0.04, bseg=1),
              box(1.0, 0.1, 0.12, "Metal", (0, -0.8, 1.42), bevel=0.02, bseg=1)]
    for x in (-0.55, 0.55):
        parts.append(box(0.62, 0.05, 1.3, "TeamColor", (x, -0.75, 2.2), bevel=0.0))
        parts.append(box(0.62, 0.055, 0.1, "TeamColorDark", (x, -0.76, 1.6), bevel=0.0))
    parts.append(box(0.2, 0.1, 0.5, "Dark", (0, -0.72, 2.6), bevel=0.02, bseg=1))
    parts += banner(0, 0.4, 3.3, h=1.2)
    o = join(parts, "HQ_Ridge")
    lit(o, "Slate", "SlateLight", 0.6)
    return [o]


# ================================================================== walls

def wall_meadow():
    """Dry stone wall, 2.0 long: irregular stacked stones with a rough cope."""
    rnd = random.Random(401)
    parts = [box(2.0, 0.78, 0.14, "Rock", (0, 0, 0.07), bevel=0.04, bseg=1)]
    z = 0.14
    for course in range(3):
        h = 0.32
        x = -1.0
        while x < 0.99:
            w = min(rnd.uniform(0.45, 0.75), 1.0 - x)
            parts.append(box(w - 0.04, 0.7 - course * 0.05, h - 0.04, "Rock" if rnd.random() < 0.55 else "Stone",
                             (x + w / 2, rnd.uniform(-0.03, 0.03), z + h / 2), rot=(0, 0, rnd.uniform(-4, 4)),
                             bevel=0.0))
            x += w
        z += h * 0.96
    for i in range(4):
        parts.append(blob(0.26, "Stone", (-0.75 + i * 0.5, 0, z + 0.08), scale=(1.1, 1.3, 0.55), subdiv=1, jitter=0.15,
                          seed=410 + i))
    o = join(parts, "Wall_Meadow", smooth_angle=10)
    return [o]


def wall_canyon():
    """Adobe wall, 2.0 long: smooth clay with a rounded top, two beam ends and a scar of bare brick."""
    parts = [box(2.0, 0.74, 1.15, "ClayLight", (0, 0, 0.58), bevel=0.16, bseg=3),
             box(2.0, 0.8, 0.14, "ClayDark", (0, 0, 0.07), bevel=0.04, bseg=1),
             box(0.5, 0.06, 0.24, "Clay", (-0.45, -0.36, 0.55), bevel=0.02, bseg=1)]
    for x in (-0.6, 0.6):
        parts.append(cyl(0.06, 0.95, "DryWood", (x, -0.48, 0.95), rot=(-90, 0, 0), seg=8, bevel=0))
    o = join(parts, "Wall_Canyon", smooth_angle=40)
    lit(o, "ClayLight", "Sand", 0.8)
    return [o]


def wall_ridge():
    """Dark slate wall, 2.0 long: big squared blocks with snow lying on the cope."""
    parts = [box(2.0, 0.8, 0.16, "Slate", (0, 0, 0.08), bevel=0.04, bseg=1)]
    for z0, z1, ws in ((0.16, 0.66, (0.7, 0.6, 0.7)), (0.66, 1.16, (0.45, 0.7, 0.85))):
        x = -1.0
        for w in ws:
            parts.append(box(w - 0.03, 0.7, z1 - z0 - 0.03, "Slate" if w > 0.6 else "SlateLight",
                             (x + w / 2, 0, (z0 + z1) / 2), bevel=0.04, bseg=1))
            x += w
    parts.append(box(2.0, 0.76, 0.12, "SlateLight", (0, 0, 1.22), bevel=0.03, bseg=1))
    parts.append(blob(0.5, "Snow", (0, 0, 1.28), scale=(2.05, 0.82, 0.18), subdiv=2, jitter=0.03, seed=421))
    o = join(parts, "Wall_Ridge", smooth_angle=15)
    return [o]


def corner(name, base, top, cap, snow=False, h=1.5):
    parts = [box(0.96, 0.96, h, base, (0, 0, h / 2), bevel=0.06, bseg=1),
             box(1.04, 1.04, 0.16, top, (0, 0, h + 0.06), bevel=0.04, bseg=1)]
    if snow:
        parts.append(blob(0.5, cap, (0, 0, h + 0.14), scale=(1.0, 1.0, 0.22), subdiv=2, jitter=0.03, seed=431))
    else:
        parts.append(box(0.7, 0.7, 0.2, cap, (0, 0, h + 0.22), bevel=0.06, bseg=1))
    return [join(parts, name)]


def wallcorner_meadow():
    return corner("WallCorner_Meadow", "Rock", "Stone", "Stone")


def wallcorner_canyon():
    o = corner("WallCorner_Canyon", "ClayLight", "Clay", "ClayLight")
    return o


def wallcorner_ridge():
    return corner("WallCorner_Ridge", "Slate", "SlateLight", "Snow", snow=True)


def tower(name, base, band, roof, snowy=False, adobe=False):
    """A 2.2 square tower for compound corners and gates, team banner on the front."""
    parts = [box(2.2, 2.2, 2.6, base, (0, 0, 1.3), bevel=0.16 if adobe else 0.06, bseg=2 if adobe else 1),
             box(2.36, 2.36, 0.2, band, (0, 0, 2.66), bevel=0.05, bseg=1)]
    for i in range(3):
        t = -0.75 + i * 0.75
        for (x, y) in ((t, -1.0), (t, 1.0), (-1.0, t), (1.0, t)):
            parts.append(box(0.42, 0.42, 0.4, band, (x, y, 2.96), bevel=0.06 if adobe else 0.04, bseg=1))
    if snowy:
        parts.append(blob(1.0, roof, (0, 0, 2.78), scale=(1.05, 1.05, 0.12), subdiv=2, jitter=0.02, seed=441))
    else:
        parts.append(box(1.9, 1.9, 0.08, roof, (0, 0, 2.78), bevel=0.0))
    parts += [box(0.36, 0.1, 0.6, "Dark", (0, -1.11, 1.8), bevel=0.03, bseg=1),
              box(0.8, 0.05, 1.1, "TeamColor", (0, -1.13, 1.0), bevel=0.0),
              box(0.8, 0.055, 0.1, "TeamColorDark", (0, -1.14, 0.5), bevel=0.0)]
    return [join(parts, name)]


def tower_meadow():
    return tower("Tower_Meadow", "Rock", "Stone", "Wood")


def tower_canyon():
    return tower("Tower_Canyon", "ClayLight", "Clay", "ClayDark", adobe=True)


def tower_ridge():
    return tower("Tower_Ridge", "Slate", "SlateLight", "Snow", snowy=True)


# ================================================================== landmarks and terrain pieces

def farmhouse():
    """A farmhouse with whitewashed walls, timber frame, a red tiled roof, chimney and a lean-to; about 5 x 3.6."""
    parts = [box(4.2, 2.8, 1.6, "Canvas", (0, 0, 0.8), bevel=0.04, bseg=1),
             box(4.3, 2.9, 0.16, "Rock", (0, 0, 0.08), bevel=0.03, bseg=1)]
    for x in (-2.05, -0.7, 0.7, 2.05):
        parts.append(box(0.14, 2.86, 1.6, "Trunk", (x, 0, 0.8), bevel=0.02, bseg=1))
    parts.append(box(4.26, 2.86, 0.12, "Trunk", (0, 0, 1.55), bevel=0.02, bseg=1))
    roof = gable(4.2, 2.8, 1.5, "Clay", (0, 0, 1.6), eave=0.25)
    recolor_faces(roof, "ClayDark", lambda p, i: p.normal.y > 0.3)
    parts.append(roof)
    parts += [box(0.5, 0.5, 1.3, "Stone", (1.3, 0.6, 2.4), bevel=0.04, bseg=1),
              box(0.6, 0.6, 0.12, "Rock", (1.3, 0.6, 3.08), bevel=0.02, bseg=1),
              box(0.75, 0.1, 1.05, "Wood", (-0.05, -1.42, 0.6), bevel=0.02, bseg=1)]
    for x in (-1.4, 1.4):
        parts.append(box(0.55, 0.08, 0.5, "Glass", (x, -1.42, 1.0), bevel=0.01, bseg=1))
        parts.append(box(0.7, 0.1, 0.08, "Wood", (x, -1.45, 0.72), bevel=0.01, bseg=1))
    # lean-to woodshed on the side
    parts += [box(1.2, 2.0, 0.9, "Wood", (2.75, 0.2, 0.45), bevel=0.03, bseg=1)]
    lean = poly([(0, -1.1, 0), (1.5, -1.1, -0.5), (1.5, 1.1, -0.5), (0, 1.1, 0)], [(0, 1, 2, 3)], "ClayDark",
                loc=(2.1, 0.2, 1.35), solidify=0.08)
    parts.append(lean)
    for i in range(3):
        parts.append(cyl(0.14, 0.9, "Trunk", (3.5, -0.9 + i * 0.3, 0.14), rot=(0, 90, 0), seg=8, bevel=0))
    o = join(parts, "Farmhouse", smooth_angle=20)
    lit(o, "Canvas", "Accent", 0.6)
    return [o]


def bridge():
    """A plank bridge, deck 9 wide (across, X) and 9.6 long (Y), on stone abutments, low rails (walkable)."""
    parts = []
    for i in range(16):
        y = -4.5 + i * 0.6
        parts.append(box(9.0, 0.55, 0.12, "Wood" if i % 3 else "DryWood", (random.Random(451 + i).uniform(-0.06, 0.06), y, 0.22),
                         bevel=0.02, bseg=1))
    for sx in (-1, 1):
        parts.append(box(0.3, 9.8, 0.3, "Trunk", (sx * 4.4, 0, 0.12), bevel=0.03, bseg=1))
        for k in range(5):
            y = -4.2 + k * 2.1
            parts.append(box(0.18, 0.18, 0.8, "Trunk", (sx * 4.55, y, 0.6), bevel=0.02, bseg=1))
        parts.append(box(0.14, 9.0, 0.12, "Wood", (sx * 4.55, 0, 0.92), bevel=0.02, bseg=1))
    for sy in (-1, 1):
        for sx in (-1, 1):
            parts.append(blob(0.55, "Rock", (sx * 4.6, sy * 4.9, 0.1), scale=(1.4, 1.0, 0.6), subdiv=1, jitter=0.15,
                              seed=460 + sx + 3 * sy))
    return [join(parts, "Bridge", smooth_angle=20)]


def ford():
    """Flat stepping stones and gravel laid across a crossing about 9 x 9 (walkable)."""
    rnd = random.Random(471)
    parts = []
    for i in range(22):
        x, y = rnd.uniform(-4.2, 4.2), rnd.uniform(-4.2, 4.2)
        r = rnd.uniform(0.45, 0.8)
        parts.append(cyl(r, 0.12, "Stone" if rnd.random() < 0.6 else "Rock", (x, y, 0.0), seg=9, r2=r * 0.85,
                         rot=(0, 0, rnd.uniform(0, 60)), bevel=0.0))
    return [join(parts, "Ford", smooth_angle=20)]


def crag(name, w, d, h, seed):
    """A tall angular slate crag with snow on its ledges (rock spine piece)."""
    rnd = random.Random(seed)
    parts = []
    for i in range(5):
        sx = w * rnd.uniform(0.45, 0.7)
        sy = d * rnd.uniform(0.45, 0.75)
        hh = h * rnd.uniform(0.45, 1.0)
        x = rnd.uniform(-w * 0.28, w * 0.28)
        y = rnd.uniform(-d * 0.25, d * 0.25)
        o = cyl(max(sx, sy) * 0.5, hh, "Slate" if i % 2 else "SlateLight", (x, y, 0), seg=5,
                r2=max(sx, sy) * rnd.uniform(0.12, 0.3), rot=(rnd.uniform(-6, 6), rnd.uniform(-6, 6), rnd.uniform(0, 70)),
                bevel=0.05, bseg=1, angle=30, scale=(sx / max(sx, sy), sy / max(sx, sy), 1))
        parts.append(o)
    o = join(parts, name, smooth_angle=5)
    lit(o, "Slate", "SlateLight", 0.5)
    tops(o, "Slate", "Snow", 0.55)
    tops(o, "SlateLight", "Snow", 0.55)
    return [o]


def crag_a():
    return crag("Crag_A", 3.6, 2.8, 4.4, 481)


def crag_b():
    return crag("Crag_B", 2.8, 2.4, 3.2, 487)


def butte(name, w, h, tiers, seed, mats):
    rnd = random.Random(seed)
    parts = []
    z = 0.0
    r = w / 2
    for t in range(tiers):
        th = h / tiers * rnd.uniform(0.8, 1.2)
        r2 = r * rnd.uniform(0.82, 0.95)
        o = cyl(r, th, mats[t % len(mats)], (rnd.uniform(-0.1, 0.1), rnd.uniform(-0.1, 0.1), z),
                rot=(0, 0, rnd.uniform(0, 40)), seg=9, r2=r2, bevel=0.06, bseg=1, angle=30)
        for v in o.data.vertices:
            v.co.x *= 1.0 + rnd.uniform(-0.1, 0.1)
            v.co.y *= 1.0 + rnd.uniform(-0.1, 0.1)
        parts.append(o)
        z += th * 0.97
        r = r2
    parts.append(cyl(r * 1.02, 0.12, "Sand", (0, 0, z - 0.04), seg=9, r2=r * 0.95, bevel=0.04, bseg=1))
    o = join(parts, name, smooth_angle=10)
    return [o]


def mesa_c():
    """A tall, narrow, pale butte (breaks up the canyon border)."""
    return butte("Mesa_C", 2.2, 5.2, 4, 491, ("ClayLight", "Sand", "ClayLight", "Clay"))


def mesa_d():
    """A low, wide, dark table rock."""
    return butte("Mesa_D", 4.0, 1.6, 2, 497, ("ClayDark", "Clay"))


def rubble():
    """Broken blocks and dust of a wall breach, about 4 x 2.5 (walkable)."""
    rnd = random.Random(501)
    parts = []
    for i in range(16):
        s = rnd.uniform(0.25, 0.55)
        parts.append(box(s, s * rnd.uniform(0.7, 1.2), s * 0.6, "ClayLight" if i % 3 else "Clay",
                         (rnd.uniform(-1.9, 1.9), rnd.uniform(-1.1, 1.1), s * 0.25),
                         rot=(rnd.uniform(-20, 20), rnd.uniform(-20, 20), rnd.uniform(0, 90)), bevel=0.04, bseg=1))
    return [join(parts, "Rubble", smooth_angle=20)]


def reeds():
    """A clump of reeds and cattails for a meadow bank (walkable)."""
    rnd = random.Random(511)
    parts = []
    for i in range(14):
        a = rnd.uniform(0, 6.28)
        d = rnd.uniform(0, 0.4)
        h = rnd.uniform(0.6, 1.1)
        x, y = math.cos(a) * d, math.sin(a) * d
        parts.append(cyl(0.025, h, "Grass" if i % 2 else "GrassLight", (x, y, 0), rot=(rnd.uniform(-10, 10), rnd.uniform(-10, 10), 0),
                         seg=4, r2=0.005, bevel=0))
        if i % 4 == 0:
            parts.append(cyl(0.045, 0.18, "Trunk", (x, y, h * 0.75), seg=6, bevel=0))
    return [join(parts, "Reeds", smooth_angle=50)]


BUILDERS = {
    "HQ_Meadow": hq_meadow, "HQ_Canyon": hq_canyon, "HQ_Ridge": hq_ridge,
    "Wall_Meadow": wall_meadow, "Wall_Canyon": wall_canyon, "Wall_Ridge": wall_ridge,
    "WallCorner_Meadow": wallcorner_meadow, "WallCorner_Canyon": wallcorner_canyon, "WallCorner_Ridge": wallcorner_ridge,
    "Tower_Meadow": tower_meadow, "Tower_Canyon": tower_canyon, "Tower_Ridge": tower_ridge,
    "Farmhouse": farmhouse, "Bridge": bridge, "Ford": ford, "Crag_A": crag_a, "Crag_B": crag_b,
    "Mesa_C": mesa_c, "Mesa_D": mesa_d, "Rubble": rubble, "Reeds": reeds,
}

if __name__ == "__main__":
    C.run(BUILDERS, "Environment", C.cli_names(), stats_file=os.path.join(C.OUT, "stats_layout.json"))
