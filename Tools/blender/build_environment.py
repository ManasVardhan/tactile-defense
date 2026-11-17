"""Environment props for Tactile Wars.
Usage: blender -b -P Tools/blender/build_environment.py -- [Tree_A Rock_B ...]
Exports to Tools/blender/out then moves into Assets/Art/Models/Environment,
renders Docs/Previews/<Name>.png.
"""
import os
import sys
import math
import random

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import tw_env_common as C  # noqa: E402
from tw_env_common import cyl, box, sphere, blob, torus, poly, join  # noqa: E402


LIGHT_DIR = None


def two_tone(o, dark, light, thresh=0.6, radial=False):
    """Stylised baked faceting: faces turned toward the key light use the light slot.
    radial=True decides by the face azimuth around the Z axis (clean for cones)."""
    from mathutils import Vector
    L = Vector((-0.9, -0.25, 0.45)).normalized()
    la = math.atan2(L.y, L.x)

    def pred(p, i):
        if o.data.materials[p.material_index].name != dark:
            return False
        if radial:
            c = p.center
            d = (math.atan2(c.y, c.x) - la + math.pi) % (2 * math.pi) - math.pi
            return abs(d) < math.radians(80) and p.normal.z > -0.3
        return p.normal.dot(L) > thresh
    C.recolor_faces(o, light, pred)


def pine(name, height, tiers, seed, radius=0.85, trunk_h=0.55):
    rnd = random.Random(seed)
    parts = [cyl(0.15, trunk_h + 0.4, "Trunk", (0, 0, 0), seg=7, r2=0.11, bevel=0.03),
             cyl(0.22, 0.1, "Trunk", (0, 0, 0), seg=7, r2=0.15, bevel=0.03)]
    crown_h = height - trunk_h
    step = crown_h / (tiers + 0.9)
    z = trunk_h
    for i in range(tiers):
        t = i / max(1, tiers - 1)
        r = radius * (1.0 - 0.5 * t)
        top = i == tiers - 1
        h = crown_h - (z - trunk_h) if top else step * 1.75
        p = cyl(r, h, "Leaf", (0, 0, z), rot=(0, 0, rnd.uniform(0, 45)),
                seg=8, r2=0.0 if top else r * 0.12, bevel=0.06, bseg=2, angle=40)
        # droop the bottom rim a little for a soft skirt
        for v in p.data.vertices:
            if v.co.z < 0.05:
                v.co.z -= 0.08
                v.co.x *= 1.04
                v.co.y *= 1.04
        parts.append(p)
        z += step
    o = join(parts, name, smooth_angle=30)
    two_tone(o, "Leaf", "LeafLight", radial=True)
    return [o]


def tree_a():
    return pine("Tree_A", 3.0, 3, seed=1, radius=0.95, trunk_h=0.7)


def tree_b():
    return pine("Tree_B", 4.0, 4, seed=2, radius=1.05, trunk_h=0.8)


def tree_c():
    """Rounder leafy broadleaf tree, about 2.6 tall."""
    parts = [cyl(0.17, 1.3, "Trunk", (0, 0, 0), seg=7, r2=0.12, bevel=0.03),
             cyl(0.26, 0.12, "Trunk", (0, 0, 0), seg=7, r2=0.17, bevel=0.04),
             cyl(0.07, 0.5, "Trunk", (0.05, 0, 0.75), rot=(0, 45, 0), seg=6, r2=0.04, bevel=0)]
    clumps = [((0, 0.05, 1.85), 0.82), ((0.5, 0.1, 1.55), 0.55), ((-0.5, 0.0, 1.6), 0.58),
              ((0.1, -0.35, 1.5), 0.5), ((-0.05, 0.1, 2.3), 0.55)]
    for i, (p, r) in enumerate(clumps):
        parts.append(blob(r, "Leaf", p, rot=(0, 0, i * 40), scale=(1, 1, 0.9), subdiv=1,
                          jitter=0.06, seed=i, bevel=0.04, bseg=1))
    o = join(parts, "Tree_C", smooth_angle=25)
    two_tone(o, "Leaf", "LeafLight", thresh=0.3)
    return [o]


def bush_a():
    parts = []
    clumps = [((0, 0, 0.32), 0.42, "Leaf"), ((0.38, 0.08, 0.24), 0.32, "LeafLight"),
              ((-0.36, 0.05, 0.25), 0.33, "Leaf"), ((0.05, -0.28, 0.2), 0.28, "LeafLight")]
    for i, (p, r, m) in enumerate(clumps):
        parts.append(blob(r, "Leaf", p, scale=(1, 1, 0.85), subdiv=1, jitter=0.07, seed=10 + i,
                          bevel=0.04, bseg=1))
    o = join(parts, "Bush_A", smooth_angle=25)
    two_tone(o, "Leaf", "LeafLight", thresh=0.3)
    return [o]


def bush_b():
    """Spiky shrub: three mini pine cones on a soft mound (echoes the pines)."""
    parts = [blob(0.42, "Leaf", (0, 0, 0.05), scale=(1.25, 1.0, 0.45), subdiv=1, jitter=0.05,
                  seed=3, bevel=0.03, bseg=1)]
    for (x, y, r, h, rz) in ((-0.22, 0.05, 0.3, 0.95, 10), (0.25, 0.12, 0.26, 0.75, 30),
                             (0.02, -0.22, 0.22, 0.6, 50)):
        parts.append(cyl(r, h, "Leaf", (x, y, 0.08), rot=(0, 0, rz), seg=7, r2=0.0,
                         bevel=0.04, bseg=2, angle=40))
    o = join(parts, "Bush_B", smooth_angle=30)
    two_tone(o, "Leaf", "LeafLight", thresh=0.35)
    return [o]


def rock(name, size, n, seed):
    """Phase 8: chunkier, faceted, warmer rocks with a lighter top face (baked two-tone like the trees),
    so they read as boulders at game size instead of grey pebbles."""
    rnd = random.Random(seed)
    parts = []
    base = size / 2
    for i in range(n):
        r = base * (1.0 if i == 0 else rnd.uniform(0.5, 0.68))
        off = (0, 0) if i == 0 else (rnd.uniform(0.62, 0.85) * base * math.cos(i * 2.3),
                                     rnd.uniform(0.62, 0.85) * base * math.sin(i * 2.3))
        sz = (rnd.uniform(1.0, 1.25), rnd.uniform(0.9, 1.05), rnd.uniform(0.72, 0.9))
        p = blob(r, "Rock", (off[0], off[1], r * sz[2] * 0.62), rot=(0, 0, rnd.uniform(0, 90)),
                 scale=sz, subdiv=1, jitter=0.22, seed=seed * 7 + i, bevel=r * 0.08, bseg=1)
        parts.append(p)
    o = join(parts, name, smooth_angle=5)
    # Lighter top faces (sun lit tops, reference rocks and walls read with a pale top).
    C.recolor_faces(o, "Stone", lambda poly, i: o.data.materials[poly.material_index].name == "Rock" and poly.normal.z > 0.72)
    return [o]


def rock_a():
    return rock("Rock_A", 0.55, 1, 3)


def rock_b():
    return rock("Rock_B", 1.0, 2, 4)


def rock_c():
    return rock("Rock_C", 1.5, 3, 6)


def grass_tuft():
    """Phase 8: bigger, denser tuft so it reads at game size."""
    rnd = random.Random(8)
    parts = []
    for i in range(10):
        a = i / 10 * 360 + rnd.uniform(-15, 15)
        tilt = rnd.uniform(12, 34)
        m = "LeafLight" if i % 3 else "Leaf"
        parts.append(cyl(0.09, rnd.uniform(0.5, 0.78), m, (0.06 * math.cos(math.radians(a)),
                                                          0.06 * math.sin(math.radians(a)), 0),
                         rot=(tilt, 0, a), seg=4, r2=0.0, bevel=0, scale=(1, 0.45, 1)))
    return [join(parts, "GrassTuft", smooth_angle=60)]


def flower(name, heads, seed):
    rnd = random.Random(seed)
    parts = [blob(0.07, "Leaf", (0, 0, 0.02), scale=(1.4, 1.4, 0.45), subdiv=1, seed=seed)]
    for i, (x, y, h) in enumerate(heads):
        lean = (math.degrees(math.atan2(y, x)) if (x or y) else 0)
        parts.append(cyl(0.018, h, "LeafLight", (x * 0.3, y * 0.3, 0), seg=5, bevel=0))
        cz = h
        petal_n = 6
        for k in range(petal_n):
            a = k / petal_n * 2 * math.pi + rnd.uniform(0, 0.3)
            parts.append(sphere(0.095, "Accent", (x * 0.3 + 0.1 * math.cos(a),
                                                  y * 0.3 + 0.1 * math.sin(a), cz),
                                scale=(1.0, 0.7, 0.35), rot=(0, 0, math.degrees(a)),
                                seg=6, rings=4))
        parts.append(sphere(0.065, "Wood", (x * 0.3, y * 0.3, cz + 0.02), scale=(1, 1, 0.6),
                            seg=8, rings=4))
        parts.append(cyl(0.05, 0.03, "LeafLight", (x * 0.3 + 0.04, y * 0.3, h * 0.45),
                         rot=(0, 60, 0), seg=4, r2=0.0, bevel=0, scale=(1, 0.4, 2.5)))
    return [join(parts, name, smooth_angle=45)]


def flower_a():
    return flower("Flower_A", [(0, 0, 0.4)], 1)


def flower_b():
    return flower("Flower_B", [(0, 0, 0.38), (0.4, 0.22, 0.28), (-0.36, 0.15, 0.32)], 2)


def fence():
    """Crossed plank barricade, 2 units long along X, about 1.0 tall."""
    rnd = random.Random(11)
    parts = []
    for x in (-0.5, 0.5):
        for sgn in (1, -1):
            ang = sgn * 38 + rnd.uniform(-3, 3)
            parts.append(box(0.17, 0.11, 1.45, "Wood", (x, -0.1 - 0.08 * (sgn > 0), 0.52),
                             rot=(0, ang, 0), bevel=0.035))
        # rope wrap at the crossing
        parts.append(cyl(0.08, 0.12, "Trunk", (x, -0.18, 0.52), rot=(90, 0, 0), seg=8,
                         bevel=0.02))
    # long rails, slightly uneven
    parts.append(box(2.1, 0.12, 0.18, "Trunk", (0, 0.03, 0.78), rot=(0, 2.5, 0), bevel=0.035))
    parts.append(box(2.0, 0.12, 0.16, "Trunk", (0, 0.03, 0.25), rot=(0, -2, 0), bevel=0.035))
    # nails
    for x in (-0.85, -0.15, 0.15, 0.85):
        for z in (0.78, 0.25):
            parts.append(cyl(0.025, 0.03, "Metal", (x, -0.04, z), rot=(90, 0, 0), seg=6,
                             bevel=0))
    return [join(parts, "Fence")]


def crate():
    s = 0.9
    parts = [box(s, s, s, "Wood", (0, 0, s / 2), bevel=0.04)]
    t = 0.09
    # dark frame planks on each visible face edge
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(box(t, t, s + 0.02, "Trunk", (sx * (s / 2 - t / 2 + 0.012),
                                                     sy * (s / 2 - t / 2 + 0.012), s / 2),
                             bevel=0.02, bseg=1))
    for sz in (0, 1):
        z = 0.06 + sz * (s - 0.12)
        for sy in (-1, 1):
            parts.append(box(s + 0.03, t, t, "Trunk", (0, sy * (s / 2 - t / 2 + 0.015), z),
                             bevel=0.02, bseg=1))
        for sx in (-1, 1):
            parts.append(box(t, s + 0.03, t, "Trunk", (sx * (s / 2 - t / 2 + 0.015), 0, z),
                             bevel=0.02, bseg=1))
    # diagonal brace on front and side
    d = math.degrees(math.atan2(s - 0.12, s - 0.12))
    parts.append(box(t, 0.04, (s - 0.1) * 1.38, "Trunk", (0, -s / 2 - 0.01, s / 2),
                     rot=(0, d, 0), bevel=0.015))
    parts.append(box(0.04, t, (s - 0.1) * 1.38, "Trunk", (s / 2 + 0.01, 0, s / 2),
                     rot=(-d, 0, 0), bevel=0.015))
    return [join(parts, "Crate")]


def barrel():
    h = 1.0
    seg = 14
    import bpy
    from mathutils import Matrix
    parts = []
    b = cyl(0.36, h, "Wood", (0, 0, 0), seg=seg, bevel=0.04)
    # bulge staves in the middle
    for v in b.data.vertices:
        z = v.co.z / h
        k = 1.0 + 0.16 * math.sin(math.pi * max(0.0, min(1.0, z)))
        v.co.x *= k
        v.co.y *= k
    parts.append(b)
    for z in (0.1, 0.32, 0.68, 0.9):
        r = 0.36 * (1.0 + 0.16 * math.sin(math.pi * z)) + 0.012
        parts.append(cyl(r, 0.07, "Metal", (0, 0, z - 0.035), seg=seg, bevel=0.012, bseg=1))
    parts.append(cyl(0.3, 0.03, "Trunk", (0, 0, h - 0.01), seg=seg, bevel=0))
    parts.append(cyl(0.05, 0.05, "Trunk", (0.12, 0.08, h), seg=8, bevel=0.01))
    return [join(parts, "Barrel", smooth_angle=40)]


def log():
    parts = [cyl(0.24, 1.6, "Trunk", (-0.8, 0, 0.24), rot=(0, 90, 0), seg=9, bevel=0.04,
                 scale=(1, 1, 1))]
    for x in (-0.81, 0.81):
        parts.append(cyl(0.2, 0.02, "Wood", (x, 0, 0.24), rot=(0, 90 if x > 0 else -90, 0),
                         seg=9, bevel=0))
        parts.append(torus(0.11, 0.012, "Trunk", (x + (0.012 if x > 0 else -0.012), 0, 0.24),
                           rot=(0, 90, 0), seg=9, mseg=3))
    parts.append(cyl(0.08, 0.35, "Trunk", (0.2, -0.1, 0.36), rot=(-40, 25, 0), seg=6, r2=0.06,
                     bevel=0.02))
    return [join(parts, "Log", smooth_angle=40)]


def hex_tile():
    R = 1.5 / math.cos(math.radians(30))  # 3.0 across flats
    t = 0.06
    o = cyl(R, t, "Stone", (0, 0, 0), seg=6, bevel=0.025, bseg=2, angle=30)
    o.rotation_euler.z = math.radians(30)  # flats facing +-Y
    return [join([o], "HexTile")]


BUILDERS = {
    "Tree_A": tree_a, "Tree_B": tree_b, "Tree_C": tree_c,
    "Bush_A": bush_a, "Bush_B": bush_b,
    "Rock_A": rock_a, "Rock_B": rock_b, "Rock_C": rock_c,
    "GrassTuft": grass_tuft, "Flower_A": flower_a, "Flower_B": flower_b,
    "Fence": fence, "Crate": crate, "Barrel": barrel, "Log": log, "HexTile": hex_tile,
}

if __name__ == "__main__":
    C.run(BUILDERS, "Environment", C.cli_names(),
          stats_file=os.path.join(C.OUT, "stats_environment.json"))
