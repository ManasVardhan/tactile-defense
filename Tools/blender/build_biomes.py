"""Biome props for Tactile Wars' three battlefields (our own designs, flat shaded toon style).
Usage: blender -b -P Tools/blender/build_biomes.py -- [RoundTree_A Mesa_A ...]
Exports to Tools/blender/out then moves into Assets/Art/Models/Environment,
renders Docs/Previews/<Name>.png.

Meadow:      RoundTree_A, RoundTree_B, Hedge, StoneFence, RailFence, HayBale, Wildflowers, Pond,
             MeadowShrub, MeadowTuft
Red Canyon:  Mesa_A, Mesa_B, Boulder_A, Boulder_B, Boulder_C, Cactus_A, Cactus_B, DryShrub, DryTuft,
             RopeFence, Bones, WagonWheel
Frost Ridge: Fir_A, Fir_B, Slate_A, Slate_B, Slate_C, IceShard, Cairn, FrozenPool, FrostShrub, FrostTuft
"""
import os
import sys
import math
import random

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import tw_env_common as C  # noqa: E402
from tw_env_common import cyl, box, sphere, blob, torus, poly, join, rod, recolor_faces  # noqa: E402

LIGHT = (-0.9, -0.25, 0.45)


def lit(o, dark, light, thresh=0.3):
    """Faces turned toward the key light take the light slot (baked two tone, like the other props)."""
    from mathutils import Vector
    L = Vector(LIGHT).normalized()
    recolor_faces(o, light, lambda p, i: o.data.materials[p.material_index].name == dark and p.normal.dot(L) > thresh)


def tops(o, base, top, nz=0.7):
    """Upward faces of the base slot take the top slot (snow caps, sunlit rock tops)."""
    recolor_faces(o, top, lambda p, i: o.data.materials[p.material_index].name == base and p.normal.z > nz)


def tuft(name, light, dark, seed, n=9, h=(0.45, 0.72)):
    rnd = random.Random(seed)
    parts = []
    for i in range(n):
        a = i / n * 360 + rnd.uniform(-15, 15)
        tilt = rnd.uniform(12, 34)
        parts.append(cyl(0.09, rnd.uniform(*h), light if i % 3 else dark,
                         (0.06 * math.cos(math.radians(a)), 0.06 * math.sin(math.radians(a)), 0),
                         rot=(tilt, 0, a), seg=4, r2=0.0, bevel=0, scale=(1, 0.45, 1)))
    return [join(parts, name, smooth_angle=60)]


def lumpy_rock(name, size, n, seed, base, top, flat=0.8):
    rnd = random.Random(seed)
    parts = []
    b = size / 2
    for i in range(n):
        r = b * (1.0 if i == 0 else rnd.uniform(0.5, 0.68))
        off = (0, 0) if i == 0 else (rnd.uniform(0.62, 0.85) * b * math.cos(i * 2.3),
                                     rnd.uniform(0.62, 0.85) * b * math.sin(i * 2.3))
        sz = (rnd.uniform(1.0, 1.25), rnd.uniform(0.9, 1.05), rnd.uniform(0.7, 0.9) * flat)
        parts.append(blob(r, base, (off[0], off[1], r * sz[2] * 0.62), rot=(0, 0, rnd.uniform(0, 90)),
                          scale=sz, subdiv=1, jitter=0.2, seed=seed * 7 + i, bevel=r * 0.08, bseg=1))
    o = join(parts, name, smooth_angle=5)
    tops(o, base, top, 0.72)
    return o


# ================================================================== Meadow

def round_tree(name, h, crown, seed, oval=1.0):
    """Broadleaf tree: short stout trunk, one big round crown with two side lobes."""
    rnd = random.Random(seed)
    trunk_h = h * 0.38
    parts = [cyl(0.2, trunk_h + 0.3, "Trunk", (0, 0, 0), seg=7, r2=0.14, bevel=0.03),
             cyl(0.3, 0.14, "Trunk", (0, 0, 0), seg=7, r2=0.2, bevel=0.04)]
    cz = trunk_h + crown * 0.75 * oval
    parts.append(blob(crown, "Grass", (0, 0, cz), scale=(1, 1, 0.92 * oval), subdiv=2, jitter=0.04, seed=seed))
    for k in range(2):
        a = rnd.uniform(0, 6.28) + k * 3.0
        r = crown * rnd.uniform(0.5, 0.62)
        parts.append(blob(r, "Grass", (math.cos(a) * crown * 0.7, math.sin(a) * crown * 0.7, cz - crown * 0.25),
                          scale=(1, 1, 0.9), subdiv=2, jitter=0.05, seed=seed + 5 + k))
    o = join(parts, name, smooth_angle=25)
    lit(o, "Grass", "GrassLight", 0.25)
    return [o]


def round_tree_a():
    return round_tree("RoundTree_A", 3.2, 1.25, 21)


def round_tree_b():
    """Taller, oval crown (a different silhouette in the same family)."""
    return round_tree("RoundTree_B", 3.9, 1.05, 34, oval=1.45)


def hedge():
    """Clipped hedgerow segment about 2.2 long along X, 0.9 tall."""
    parts = [box(2.1, 0.75, 0.75, "Grass", (0, 0, 0.42), bevel=0.22, bseg=2)]
    for i, x in enumerate((-0.7, 0.0, 0.7)):
        parts.append(blob(0.46, "Grass", (x, 0.02 * i, 0.56), scale=(1.1, 0.85, 0.7), subdiv=2, jitter=0.04, seed=40 + i))
    o = join(parts, "Hedge", smooth_angle=30)
    lit(o, "Grass", "GrassLight", 0.3)
    return [o]


def stone_fence():
    """Low dry stone wall 2.0 long along X: irregular stones in two courses with a cap."""
    rnd = random.Random(51)
    parts = []
    for course, (z, hgt) in enumerate(((0.16, 0.3), (0.44, 0.24))):
        x = -1.0
        while x < 0.98:
            w = rnd.uniform(0.32, 0.5)
            w = min(w, 1.0 - x)
            parts.append(box(w - 0.04, 0.5 - course * 0.06, hgt, "Rock", (x + w / 2, rnd.uniform(-0.03, 0.03), z),
                             rot=(0, 0, rnd.uniform(-5, 5)), bevel=0.06, bseg=1))
            x += w
    for i in range(5):
        parts.append(blob(0.17, "Stone", (-0.8 + i * 0.4, 0, 0.62), scale=(1.3, 1.1, 0.6), subdiv=1, jitter=0.15, seed=60 + i))
    o = join(parts, "StoneFence", smooth_angle=10)
    tops(o, "Rock", "Stone", 0.75)
    return [o]


def rail_fence():
    """Post and rail fence 2.0 long along X, about 0.95 tall."""
    parts = []
    for x in (-0.95, 0.95):
        parts.append(box(0.16, 0.16, 0.95, "Trunk", (x, 0, 0.475), bevel=0.03))
        parts.append(box(0.2, 0.2, 0.05, "Wood", (x, 0, 0.97), bevel=0.02))
    parts.append(box(2.05, 0.09, 0.14, "Wood", (0, -0.1, 0.72), rot=(0, 1.5, 0), bevel=0.03))
    parts.append(box(2.0, 0.09, 0.13, "Wood", (0, -0.1, 0.38), rot=(0, -1.5, 0), bevel=0.03))
    return [join(parts, "RailFence")]


def hay_bale():
    """Round hay bale lying on its side, with a rolled face and a twine band."""
    r, L = 0.55, 0.95
    parts = [cyl(r, L, "Hay", (-L / 2, 0, r), rot=(0, 90, 0), seg=14, bevel=0.08, bseg=2)]
    for x in (-L / 2 - 0.005, L / 2 + 0.005):
        parts.append(torus(r * 0.55, 0.035, "HayDark", (x, 0, r), rot=(0, 90, 0), seg=14, mseg=3))
        parts.append(cyl(r * 0.22, 0.02, "HayDark", (x, 0, r), rot=(0, 90 if x > 0 else -90, 0), seg=10, bevel=0))
    parts.append(torus(r + 0.01, 0.025, "HayDark", (0.18, 0, r), rot=(0, 90, 0), seg=14, mseg=3))
    return [join(parts, "HayBale", smooth_angle=40)]


def wildflowers():
    """A loose patch of small pink and gold flowers on short stems (decorative)."""
    rnd = random.Random(71)
    parts = [blob(0.25, "Grass", (0, 0, 0.0), scale=(2.2, 1.6, 0.25), subdiv=1, jitter=0.1, seed=71)]
    for i in range(11):
        a, d = rnd.uniform(0, 6.28), rnd.uniform(0.05, 0.5)
        x, y = math.cos(a) * d * 1.3, math.sin(a) * d
        h = rnd.uniform(0.16, 0.34)
        parts.append(cyl(0.016, h, "GrassLight", (x, y, 0), seg=4, bevel=0))
        parts.append(sphere(0.075, "Bloom" if i % 3 else "BloomGold", (x, y, h), scale=(1, 1, 0.6), seg=6, rings=4))
    return [join(parts, "Wildflowers", smooth_angle=45)]


def pond():
    """Small pond: an irregular water surface with a stony rim and a few reeds; about 3.6 by 2.6."""
    rnd = random.Random(81)
    n = 18
    pts = []
    for i in range(n):
        a = i / n * 2 * math.pi
        k = 1.0 + 0.12 * math.sin(a * 3 + 0.7) + 0.06 * math.sin(a * 5)
        pts.append((math.cos(a) * 1.7 * k, math.sin(a) * 1.2 * k))
    water = poly([(x, y, 0.04) for x, y in pts] + [(0, 0, 0.04)], [(i, (i + 1) % n, n) for i in range(n)], "Water", name="water")
    bank = poly([(x * 1.12, y * 1.15, 0.02) for x, y in pts] + [(0, 0, 0.02)], [(i, (i + 1) % n, n) for i in range(n)], "Sand", name="bank")
    parts = [water, bank]
    for i in range(0, n, 2):
        x, y = pts[i]
        parts.append(blob(rnd.uniform(0.16, 0.26), "Rock", (x * 1.12, y * 1.15, 0.05), scale=(1.2, 1, 0.6), subdiv=1, jitter=0.15, seed=82 + i))
    for i in range(7):
        x, y = pts[(i * 3 + 1) % n]
        for k in range(3):
            parts.append(cyl(0.025, rnd.uniform(0.45, 0.75), "GrassLight" if k else "Grass",
                             (x * 1.02 + rnd.uniform(-0.08, 0.08), y * 1.02 + rnd.uniform(-0.08, 0.08), 0),
                             rot=(rnd.uniform(-10, 10), rnd.uniform(-10, 10), 0), seg=4, r2=0.006, bevel=0))
    return [join(parts, "Pond", smooth_angle=30)]


def meadow_shrub():
    """Low round shrub in the meadow greens (decorative)."""
    parts = []
    for i, (p, r) in enumerate((((0, 0, 0.3), 0.4), ((0.34, 0.1, 0.22), 0.3), ((-0.32, 0.06, 0.22), 0.31))):
        parts.append(blob(r, "Grass", p, scale=(1, 1, 0.82), subdiv=2, jitter=0.05, seed=90 + i))
    o = join(parts, "MeadowShrub", smooth_angle=25)
    lit(o, "Grass", "GrassLight", 0.25)
    return [o]


def meadow_tuft():
    return tuft("MeadowTuft", "GrassLight", "Grass", 95)


# ================================================================== Red Canyon

def mesa(name, w, h, tiers, seed):
    """Flat topped butte of stacked, slightly offset sandstone bands."""
    rnd = random.Random(seed)
    parts = []
    z = 0.0
    r = w / 2
    for t in range(tiers):
        th = h / tiers * rnd.uniform(0.85, 1.15)
        r2 = r * rnd.uniform(0.86, 0.95)
        o = cyl(r, th, "Clay" if t % 2 == 0 else "ClayDark", (rnd.uniform(-0.08, 0.08), rnd.uniform(-0.08, 0.08), z),
                rot=(0, 0, rnd.uniform(0, 40)), seg=7, r2=r2, bevel=0.06, bseg=1, angle=30)
        for v in o.data.vertices:
            if 0.01 < v.co.z < th - 0.01:
                continue
            v.co.x *= 1.0 + rnd.uniform(-0.08, 0.08)
            v.co.y *= 1.0 + rnd.uniform(-0.08, 0.08)
        parts.append(o)
        z += th * 0.97
        r = r2 * rnd.uniform(0.95, 1.02)
    parts.append(cyl(r * 1.02, 0.12, "ClayLight", (0, 0, z - 0.04), seg=7, r2=r * 0.95, bevel=0.04, bseg=1))
    for i in range(3):
        a = rnd.uniform(0, 6.28)
        parts.append(blob(rnd.uniform(0.25, 0.4), "Clay", (math.cos(a) * w * 0.55, math.sin(a) * w * 0.55, 0.15),
                          scale=(1.2, 1, 0.7), subdiv=1, jitter=0.2, seed=seed + i))
    o = join(parts, name, smooth_angle=10)
    tops(o, "Clay", "ClayLight", 0.7)
    lit(o, "Clay", "ClayLight", 0.55)
    return [o]


def mesa_a():
    return mesa("Mesa_A", 3.0, 3.4, 3, 101)


def mesa_b():
    return mesa("Mesa_B", 2.3, 2.2, 2, 107)


def boulder_a():
    return [lumpy_rock("Boulder_A", 0.6, 1, 111, "Clay", "ClayLight")]


def boulder_b():
    return [lumpy_rock("Boulder_B", 1.05, 2, 113, "Clay", "ClayLight")]


def boulder_c():
    return [lumpy_rock("Boulder_C", 1.55, 3, 117, "Clay", "ClayLight", flat=0.7)]


def cactus_a():
    """Tall column cactus with two raised arms, about 2.2 tall."""
    parts = [cyl(0.22, 2.0, "Cactus", (0, 0, 0), seg=8, r2=0.2, bevel=0.0),
             sphere(0.2, "Cactus", (0, 0, 2.0), scale=(1, 1, 0.8), seg=8, rings=5)]
    for sx, z0, z1 in ((1, 0.8, 1.6), (-1, 1.1, 1.75)):
        parts.append(rod((0, 0, z0), (sx * 0.5, 0, z0 + 0.05), 0.12, "Cactus", seg=7))
        parts.append(cyl(0.13, z1 - z0, "Cactus", (sx * 0.5, 0, z0), seg=7, r2=0.12, bevel=0.0))
        parts.append(sphere(0.12, "Cactus", (sx * 0.5, 0, z1), scale=(1, 1, 0.8), seg=7, rings=4))
    o = join(parts, "Cactus_A", smooth_angle=50)
    lit(o, "Cactus", "CactusLight", 0.25)
    return [o]


def cactus_b():
    """Cluster of three round barrel cacti, one with a pink bloom (decorative)."""
    parts = []
    for i, (x, y, r) in enumerate(((0, 0, 0.3), (0.38, 0.14, 0.2), (-0.3, 0.2, 0.17))):
        o = cyl(r, r * 1.6, "Cactus", (x, y, 0), seg=10, r2=r * 0.7, bevel=r * 0.35, bseg=2, angle=30)
        parts.append(o)
    parts.append(sphere(0.09, "Bloom", (0, 0, 0.5), scale=(1, 1, 0.6), seg=6, rings=4))
    o = join(parts, "Cactus_B", smooth_angle=40)
    lit(o, "Cactus", "CactusLight", 0.25)
    return [o]


def dry_shrub():
    """Tangle of bleached twigs on a dry tuft (decorative)."""
    rnd = random.Random(131)
    parts = [blob(0.22, "DryWood", (0, 0, 0.05), scale=(1.4, 1.2, 0.4), subdiv=1, jitter=0.2, seed=131)]
    for i in range(9):
        a = i / 9 * 6.28 + rnd.uniform(-0.3, 0.3)
        L = rnd.uniform(0.45, 0.75)
        p1 = (math.cos(a) * L * 0.7, math.sin(a) * L * 0.7, L * 0.8)
        parts.append(rod((0, 0, 0.05), p1, 0.025, "DryWood", seg=4, r2=0.01))
        parts.append(rod(p1, (p1[0] * 1.3, p1[1] * 1.3 + 0.1, p1[2] + 0.15), 0.015, "Trunk", seg=4, r2=0.006))
    return [join(parts, "DryShrub", smooth_angle=50)]


def dry_tuft():
    return tuft("DryTuft", "DryWood", "Sand", 137, n=8, h=(0.35, 0.6))


def rope_fence():
    """Rope and plank fence 2.0 long along X: two leaning posts, a sagging rope and loose planks."""
    parts = []
    for x, lean in ((-0.95, 4), (0.95, -3)):
        parts.append(cyl(0.08, 1.05, "DryWood", (x, 0, 0), rot=(0, lean, 0), seg=6, r2=0.065, bevel=0.0))
    pts = [(-0.95 + 1.9 * i / 6, 0, 0.9 - 0.18 * math.sin(math.pi * i / 6)) for i in range(7)]
    for i in range(6):
        parts.append(rod(pts[i], pts[i + 1], 0.025, "Canvas", seg=4))
    for i, x in enumerate((-0.55, -0.1, 0.4)):
        parts.append(box(0.32, 0.05, 0.62, "Wood", (x, 0.04, 0.42), rot=(0, (-1) ** i * 6, 0), bevel=0.02))
    return [join(parts, "RopeFence")]


def bones():
    """An old rib cage and a horned skull half sunk in the sand."""
    parts = []
    for i in range(5):
        x = -0.5 + i * 0.22
        parts.append(torus(0.32, 0.035, "Bone", (x, 0, 0.0), rot=(0, 90, 0), seg=10, mseg=3, scale=(1, 1, 0.85)))
    parts.append(rod((-0.6, 0, 0.27), (0.45, 0, 0.27), 0.04, "Bone", seg=5))
    parts.append(blob(0.17, "Bone", (0.72, 0.05, 0.14), scale=(1.3, 1, 0.9), subdiv=1, jitter=0.05, seed=141))
    for sy in (-1, 1):
        parts.append(rod((0.72, sy * 0.12, 0.24), (0.66, sy * 0.38, 0.42), 0.035, "Bone", seg=5, r2=0.01))
    return [join(parts, "Bones", smooth_angle=50)]


def wagon_wheel():
    """A broken wagon wheel leaning on a rock."""
    parts = [blob(0.3, "Clay", (0.2, 0.3, 0.12), scale=(1.3, 1, 0.7), subdiv=1, jitter=0.2, seed=151)]
    R = 0.62
    wheel = [torus(R, 0.06, "DryWood", (0, 0, 0), seg=16, mseg=4),
             cyl(0.11, 0.14, "Trunk", (0, 0, -0.07), seg=8, bevel=0.02)]
    for k in range(7):
        a = k / 8 * 2 * math.pi
        wheel.append(rod((0, 0, 0), (math.cos(a) * R, math.sin(a) * R, 0), 0.03, "DryWood", seg=4))
    w = join(wheel, "wheel")
    w.rotation_euler = (math.radians(70), 0, math.radians(10))
    w.location = (0, -0.05, 0.6)
    parts.append(w)
    return [join(parts, "WagonWheel", smooth_angle=40)]


# ================================================================== Frost Ridge

def fir(name, h, r, tiers, seed):
    """Tall narrow fir: a column of slim tiers, each capped with snow on its upper face."""
    rnd = random.Random(seed)
    trunk_h = 0.5
    parts = [cyl(0.12, trunk_h + 0.3, "Trunk", (0, 0, 0), seg=6, r2=0.09, bevel=0.02)]
    crown = h - trunk_h
    step = crown / (tiers + 0.6)
    z = trunk_h
    for i in range(tiers):
        t = i / max(1, tiers - 1)
        rr = r * (1.0 - 0.62 * t) * rnd.uniform(0.95, 1.05)
        top = i == tiers - 1
        hh = crown - (z - trunk_h) if top else step * 1.55
        rot = rnd.uniform(0, 60)
        r2 = 0.0 if top else rr * 0.35
        parts.append(cyl(rr, hh, "Fir", (0, 0, z), rot=(0, 0, rot), seg=6, r2=r2, bevel=0.04, bseg=1, angle=40))
        # Snow resting on the tier: a slightly wider cone over its upper part.
        k = 0.4
        rs = rr + (r2 - rr) * k
        parts.append(cyl(rs * 1.1, hh * (1 - k) + 0.02, "Snow", (0, 0, z + hh * k), rot=(0, 0, rot), seg=6,
                         r2=0.0 if top else r2 * 1.08, bevel=0.0))
        z += step
    o = join(parts, name, smooth_angle=30)
    lit(o, "Fir", "FirLight", 0.35)
    tops(o, "Fir", "Snow", 0.5)
    tops(o, "FirLight", "Snow", 0.5)
    return [o]


def fir_a():
    return fir("Fir_A", 4.4, 0.78, 5, 201)


def fir_b():
    return fir("Fir_B", 3.3, 0.72, 4, 203)


def slate(name, size, n, seed):
    """Angular slate slabs, tilted, with snow on their upper faces."""
    rnd = random.Random(seed)
    parts = []
    for i in range(n):
        s = size * (1.0 if i == 0 else rnd.uniform(0.5, 0.75))
        x = 0 if i == 0 else rnd.uniform(-0.45, 0.45) * size
        y = 0 if i == 0 else rnd.uniform(-0.4, 0.4) * size
        parts.append(box(s, s * rnd.uniform(0.5, 0.75), s * rnd.uniform(0.45, 0.7), "Slate", (x, y, s * 0.25),
                         rot=(rnd.uniform(-14, 14), rnd.uniform(-18, 18), rnd.uniform(0, 90)), bevel=s * 0.06, bseg=1))
    o = join(parts, name, smooth_angle=5)
    lit(o, "Slate", "SlateLight", 0.4)
    tops(o, "Slate", "Snow", 0.8)
    tops(o, "SlateLight", "Snow", 0.8)
    return [o]


def slate_a():
    return slate("Slate_A", 0.6, 1, 211)


def slate_b():
    return slate("Slate_B", 1.0, 2, 213)


def slate_c():
    return slate("Slate_C", 1.4, 3, 217)


def ice_shard():
    """Cluster of tapered ice crystals growing out of a snow mound, about 1.6 tall."""
    rnd = random.Random(221)
    parts = [blob(0.45, "Snow", (0, 0, 0.0), scale=(1.4, 1.2, 0.45), subdiv=1, jitter=0.1, seed=221)]
    for i, (h, r) in enumerate(((1.6, 0.22), (1.1, 0.16), (0.9, 0.15), (0.7, 0.12), (1.25, 0.17))):
        a = i * 1.3
        d = 0 if i == 0 else 0.28
        parts.append(cyl(r, h, "Ice", (math.cos(a) * d, math.sin(a) * d, 0.05),
                         rot=(rnd.uniform(-18, 18) if i else 4, rnd.uniform(-18, 18) if i else -6, rnd.uniform(0, 60)),
                         seg=5, r2=0.0, bevel=0.0))
    o = join(parts, "IceShard", smooth_angle=10)
    lit(o, "Ice", "Glass", 0.45)
    return [o]


def cairn():
    """A stacked stone marker with a little snow on top, about 1.2 tall."""
    rnd = random.Random(231)
    parts = []
    z = 0.0
    for i, r in enumerate((0.42, 0.36, 0.3, 0.24, 0.17)):
        hgt = r * 0.75
        parts.append(blob(r, "Slate" if i % 2 == 0 else "SlateLight", (rnd.uniform(-0.04, 0.04), rnd.uniform(-0.04, 0.04), z + hgt * 0.5),
                          scale=(1.1, 1.0, 0.62), subdiv=1, jitter=0.12, seed=231 + i))
        z += hgt * 0.92
    parts.append(blob(0.13, "Snow", (0, 0, z + 0.02), scale=(1.2, 1.1, 0.5), subdiv=1, jitter=0.1, seed=239))
    return [join(parts, "Cairn", smooth_angle=10)]


def frozen_pool():
    """A frozen pool: pale ice with darker cracks, a snow bank and rim stones; about 3.6 by 2.6."""
    rnd = random.Random(241)
    n = 18
    pts = []
    for i in range(n):
        a = i / n * 2 * math.pi
        k = 1.0 + 0.1 * math.sin(a * 3 + 1.9) + 0.07 * math.sin(a * 4)
        pts.append((math.cos(a) * 1.7 * k, math.sin(a) * 1.2 * k))
    parts = [poly([(x, y, 0.05) for x, y in pts] + [(0, 0, 0.05)], [(i, (i + 1) % n, n) for i in range(n)], "Ice", name="ice"),
             poly([(x * 1.16, y * 1.2, 0.03) for x, y in pts] + [(0, 0, 0.03)], [(i, (i + 1) % n, n) for i in range(n)], "Snow", name="bank")]
    for i in range(5):
        a = rnd.uniform(0, 6.28)
        L = rnd.uniform(0.6, 1.1)
        parts.append(box(L, 0.04, 0.01, "Glass", (math.cos(a) * 0.3, math.sin(a) * 0.25, 0.055), rot=(0, 0, math.degrees(a) + 90), bevel=0))
    for i in range(1, n, 3):
        x, y = pts[i]
        parts.append(blob(rnd.uniform(0.18, 0.3), "Slate", (x * 1.15, y * 1.18, 0.06), scale=(1.2, 1, 0.6), subdiv=1, jitter=0.15, seed=242 + i))
    for i in range(0, n, 4):
        x, y = pts[i]
        parts.append(blob(0.3, "Snow", (x * 1.25, y * 1.28, 0.02), scale=(1.4, 1, 0.4), subdiv=1, jitter=0.1, seed=260 + i))
    return [join(parts, "FrozenPool", smooth_angle=20)]


def frost_shrub():
    """A low, snow dusted conifer shrub (decorative)."""
    parts = []
    for i, (x, y, r, h) in enumerate(((0, 0, 0.36, 0.75), (0.3, 0.12, 0.26, 0.55), (-0.26, 0.1, 0.24, 0.5))):
        parts.append(cyl(r, h, "Fir", (x, y, 0), seg=6, r2=0.0, bevel=0.03, bseg=1, angle=40, rot=(0, 0, i * 20)))
    o = join(parts, "FrostShrub", smooth_angle=30)
    lit(o, "Fir", "FirLight", 0.35)
    tops(o, "Fir", "Snow", 0.45)
    tops(o, "FirLight", "Snow", 0.45)
    return [o]


def frost_tuft():
    return tuft("FrostTuft", "SlateLight", "Snow", 251, n=8, h=(0.3, 0.55))


BUILDERS = {
    # Meadow
    "RoundTree_A": round_tree_a, "RoundTree_B": round_tree_b, "Hedge": hedge, "StoneFence": stone_fence,
    "RailFence": rail_fence, "HayBale": hay_bale, "Wildflowers": wildflowers, "Pond": pond,
    "MeadowShrub": meadow_shrub, "MeadowTuft": meadow_tuft,
    # Red Canyon
    "Mesa_A": mesa_a, "Mesa_B": mesa_b, "Boulder_A": boulder_a, "Boulder_B": boulder_b, "Boulder_C": boulder_c,
    "Cactus_A": cactus_a, "Cactus_B": cactus_b, "DryShrub": dry_shrub, "DryTuft": dry_tuft,
    "RopeFence": rope_fence, "Bones": bones, "WagonWheel": wagon_wheel,
    # Frost Ridge
    "Fir_A": fir_a, "Fir_B": fir_b, "Slate_A": slate_a, "Slate_B": slate_b, "Slate_C": slate_c,
    "IceShard": ice_shard, "Cairn": cairn, "FrozenPool": frozen_pool, "FrostShrub": frost_shrub,
    "FrostTuft": frost_tuft,
}

if __name__ == "__main__":
    C.run(BUILDERS, "Environment", C.cli_names(),
          stats_file=os.path.join(C.OUT, "stats_biomes.json"))
