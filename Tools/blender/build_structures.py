"""Structures and defenses for Tactile Wars.
Usage: blender -b -P Tools/blender/build_structures.py -- [Turret Tank ...]
Exports to Tools/blender/out then moves into Assets/Art/Models/Structures,
renders Docs/Previews/<Name>.png.
Blender front is -Y (becomes Unity +Z). Child pivots sit on their rotation axes.
"""
import os
import sys
import math
import random

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import tw_env_common as C  # noqa: E402
from tw_env_common import (cyl, box, sphere, blob, torus, poly, join, empty,  # noqa: E402
                           parent_keep, rod, skull, recolor_faces)


def pillow(cx, cy, cz, rz, sx=0.5, sy=0.32, sz=0.26, mat="Sand"):
    """A sandbag: squashed low-poly sphere."""
    return sphere(1.0, mat, (cx, cy, cz), rot=(0, 0, rz), scale=(sx / 2, sy / 2, sz / 2),
                  seg=10, rings=6)


def sandbag_arc(radius, a0, a1, n, z, cy=0.0, sx=0.54, sy=0.34, sz=0.26, jitter=0.0, seed=0):
    """Row of n sandbags on an arc centred at (0, cy), angles in degrees (0 = +X)."""
    rnd = random.Random(seed)
    out = []
    for i in range(n):
        a = math.radians(a0 + (a1 - a0) * (i + 0.5) / n)
        x, y = radius * math.cos(a), cy + radius * math.sin(a)
        out.append(pillow(x, y, z, math.degrees(a) + 90 + rnd.uniform(-jitter, jitter),
                          sx, sy, sz))
    return out


# ------------------------------------------------------------------ Turret

def turret():
    base_parts = [
        cyl(0.66, 0.2, "Dark", (0, 0, 0), seg=8, r2=0.6, bevel=0.05),
        cyl(0.5, 0.58, "Metal", (0, 0, 0.18), seg=16, r2=0.44, bevel=0.06),
        cyl(0.52, 0.1, "TeamColorDark", (0, 0, 0.4), seg=16, bevel=0.03),
        cyl(0.4, 0.08, "Dark", (0, 0, 0.75), seg=16, bevel=0.02),
    ]
    for i in range(4):
        a = math.radians(45 + i * 90)
        base_parts.append(box(0.3, 0.3, 0.26, "Dark", (0.55 * math.cos(a), 0.55 * math.sin(a),
                                                       0.13), rot=(0, 0, 45 + i * 90),
                              bevel=0.06))
    for i in range(8):
        a = math.radians(22.5 + i * 45)
        base_parts.append(sphere(0.035, "Metal", (0.53 * math.cos(a), 0.53 * math.sin(a), 0.45),
                                 seg=6, rings=4))
    base = join(base_parts, "Base")

    hz = 0.82  # yaw pivot height
    head_parts = [
        box(0.84, 0.9, 0.66, "TeamColor", (0, 0.06, hz + 0.37), bevel=0.2, bseg=3),
        box(0.88, 0.94, 0.12, "TeamColorDark", (0, 0.06, hz + 0.1), bevel=0.05),
        cyl(0.19, 0.06, "TeamColorDark", (0, 0.04, hz + 0.68), seg=12, bevel=0.02),
        # barrel
        cyl(0.2, 0.22, "TeamColorDark", (0, -0.36, hz + 0.36), rot=(90, 0, 0), seg=12,
            bevel=0.03),
        cyl(0.14, 0.6, "Metal", (0, -0.45, hz + 0.36), rot=(90, 0, 0), seg=12, bevel=0.02),
        cyl(0.2, 0.18, "Dark", (0, -0.95, hz + 0.36), rot=(90, 0, 0), seg=12, r2=0.22,
            bevel=0.04),
        cyl(0.13, 0.02, "Paint", (0, -1.125, hz + 0.36), rot=(90, 0, 0), seg=12, bevel=0),
        # paint canister on the back
        cyl(0.2, 0.06, "Metal", (0, 0.62, hz + 0.18), seg=12, bevel=0.02),
        cyl(0.17, 0.46, "Paint", (0, 0.62, hz + 0.22), seg=12, bevel=0.03),
        cyl(0.2, 0.07, "Metal", (0, 0.62, hz + 0.66), seg=12, bevel=0.02),
        cyl(0.08, 0.06, "Dark", (0, 0.62, hz + 0.72), seg=8, bevel=0.01),
        rod((0, 0.45, hz + 0.6), (0, 0.6, hz + 0.6), 0.04, "Metal"),
    ]
    # side emblems: skulls on the cheeks, facing outward
    for sx in (-1, 1):
        em = skull(0, 0, 0, 0.32)
        for e in em:
            # stand the flat decal up on the side face
            e.location = (e.location.x, e.location.y, e.location.z)
        sk = join(em, "tmp")
        sk.rotation_euler = (math.radians(90), 0, math.radians(90 * sx))
        sk.location = (sx * 0.425, 0.04, hz + 0.4)
        head_parts.append(sk)
    head = join(head_parts, "Head", origin=(0, 0, hz))
    parent_keep(head, base)
    muzzle = empty("Muzzle", (0, -1.14, hz + 0.36), parent=head)
    return [base, head, muzzle]


# ------------------------------------------------------------------ Tank

def tank():
    hull_parts = []
    for sx in (-1, 1):
        hull_parts.append(box(0.42, 2.05, 0.56, "Dark", (sx * 0.64, 0, 0.3), bevel=0.24, bseg=2))
        for y in (-0.6, -0.2, 0.2, 0.6):
            hull_parts.append(cyl(0.17, 0.06, "Metal", (sx * 0.84, y, 0.3), rot=(0, 90 * sx, 0),
                                  seg=8, bevel=0.02, bseg=1))
        hull_parts.append(box(0.46, 1.9, 0.07, "TeamColorDark", (sx * 0.64, -0.02, 0.6),
                              bevel=0.03, bseg=1))
    hull_parts += [
        box(0.86, 1.7, 0.62, "TeamColor", (0, 0.04, 0.68), bevel=0.24, bseg=2),
        box(0.62, 0.26, 0.2, "TeamColorDark", (0, -0.8, 0.52), rot=(-25, 0, 0), bevel=0.06,
            bseg=1),
        sphere(0.08, "Accent", (-0.27, -0.83, 0.75), scale=(1, 0.6, 1), seg=8, rings=5),
        sphere(0.08, "Accent", (0.27, -0.83, 0.75), scale=(1, 0.6, 1), seg=8, rings=5),
        # paint tank on the back deck
        cyl(0.19, 0.74, "Paint", (-0.37, 0.78, 1.04), rot=(0, 90, 0), seg=12, bevel=0.04,
            bseg=1),
        cyl(0.21, 0.06, "Metal", (-0.4, 0.78, 1.04), rot=(0, 90, 0), seg=12, bevel=0.02, bseg=1),
        cyl(0.21, 0.06, "Metal", (0.34, 0.78, 1.04), rot=(0, 90, 0), seg=12, bevel=0.02, bseg=1),
        box(0.5, 0.2, 0.14, "Metal", (0, 0.78, 0.9), bevel=0.04, bseg=1),
        # exhaust stubs
        cyl(0.06, 0.32, "Dark", (0.3, 0.98, 0.72), seg=8, bevel=0.02, bseg=1),
        cyl(0.08, 0.06, "Dark", (0.3, 0.98, 1.03), seg=8, r2=0.09, bevel=0.01, bseg=1),
    ]
    hull = join(hull_parts, "Hull")

    tz = 0.96
    tur_parts = [
        cyl(0.52, 0.14, "TeamColorDark", (0, 0.05, tz), seg=16, bevel=0.04),
        sphere(0.48, "TeamColor", (0, 0.07, tz + 0.1), scale=(1, 1.05, 0.72), seg=14, rings=8),
        cyl(0.17, 0.07, "Metal", (0.1, 0.18, tz + 0.42), seg=12, bevel=0.02),
        cyl(0.17, 0.2, "TeamColorDark", (0, -0.36, tz + 0.2), rot=(90, 0, 0), seg=12,
            bevel=0.03),
        cyl(0.1, 0.62, "Metal", (0, -0.42, tz + 0.2), rot=(90, 0, 0), seg=10, bevel=0.02),
        cyl(0.15, 0.16, "Dark", (0, -0.98, tz + 0.2), rot=(90, 0, 0), seg=12, r2=0.16,
            bevel=0.035),
        cyl(0.09, 0.02, "Paint", (0, -1.14, tz + 0.2), rot=(90, 0, 0), seg=10, bevel=0),
    ]
    for sx in (-1, 1):
        em = join(skull(0, 0, 0, 0.26), "tmp")
        em.rotation_euler = (math.radians(90), 0, math.radians(90 * sx))
        em.location = (sx * 0.47, 0.08, tz + 0.2)
        tur_parts.append(em)
    tur = join(tur_parts, "Turret", origin=(0, 0, tz))
    parent_keep(tur, hull)
    muzzle = empty("Muzzle", (0, -1.16, tz + 0.2), parent=tur)
    return [hull, tur, muzzle]


# ------------------------------------------------------------------ Cannon

def cannon():
    base_parts = []
    for sx in (-1, 1):
        base_parts += [
            cyl(0.4, 0.12, "Wood", (sx * 0.44, 0.0, 0.4), rot=(0, 90 * sx, 0), seg=12,
                bevel=0.03),
            torus(0.38, 0.05, "Dark", (sx * 0.5, 0.0, 0.4), rot=(0, 90, 0), seg=14, mseg=5),
            cyl(0.1, 0.1, "Metal", (sx * 0.52, 0.0, 0.4), rot=(0, 90 * sx, 0), seg=8,
                bevel=0.02),
            box(0.1, 0.62, 0.6, "TeamColorDark", (sx * 0.27, 0.05, 0.55), bevel=0.04),
        ]
        for k in range(4):
            a = math.radians(45 * k)
            base_parts.append(box(0.06, 0.62, 0.06, "Trunk", (sx * 0.44, 0, 0.4),
                                  rot=(math.degrees(a), 0, 0), bevel=0.01))
    base_parts += [
        rod((-0.6, 0, 0.4), (0.6, 0, 0.4), 0.06, "Metal", seg=8),
        box(0.44, 0.7, 0.2, "Wood", (0, 0.1, 0.35), bevel=0.05),
        box(0.3, 0.9, 0.14, "Wood", (0, 0.65, 0.2), rot=(14, 0, 0), bevel=0.05),
        box(0.36, 0.14, 0.1, "Dark", (0, 1.06, 0.07), bevel=0.03),
    ]
    base = join(base_parts, "Base")

    pz = 0.78
    bar_parts = [
        sphere(0.24, "Metal", (0, 0.32, pz), seg=12, rings=7),
        sphere(0.09, "Metal", (0, 0.6, pz), seg=8, rings=5),
        cyl(0.26, 1.15, "Metal", (0, 0.3, pz), rot=(90, 0, 0), seg=14, r2=0.2, bevel=0.03),
        cyl(0.27, 0.1, "TeamColor", (0, 0.18, pz), rot=(90, 0, 0), seg=14, bevel=0.02),
        cyl(0.24, 0.1, "TeamColor", (0, -0.35, pz), rot=(90, 0, 0), seg=14, bevel=0.02),
        cyl(0.24, 0.18, "Dark", (0, -0.78, pz), rot=(90, 0, 0), seg=14, r2=0.27, bevel=0.04),
        cyl(0.17, 0.02, "Paint", (0, -0.965, pz), rot=(90, 0, 0), seg=12, bevel=0),
        rod((-0.3, 0, pz), (0.3, 0, pz), 0.08, "Dark", seg=8),
    ]
    barrel = join(bar_parts, "Barrel", origin=(0, 0, pz))
    barrel.rotation_euler.x = math.radians(-8)  # rest pose slightly raised, pivot on trunnion
    parent_keep(barrel, base)
    muzzle = empty("Muzzle", (0, 0, 0))
    muzzle.parent = barrel
    muzzle.location = (0, -0.98, 0)  # local to Barrel, axes aligned with the bore
    return [base, barrel, muzzle]


# ------------------------------------------------------------------ Bunker

def bunker():
    parts = [
        cyl(1.25, 0.18, "Rock", (0, 0, 0), seg=8, r2=1.2, bevel=0.06),
        cyl(1.12, 0.85, "Stone", (0, 0, 0.12), rot=(0, 0, 22.5), seg=8, r2=1.02, bevel=0.1),
        cyl(1.05, 0.12, "TeamColor", (0, 0, 0.92), rot=(0, 0, 22.5), seg=8, r2=1.03, bevel=0.03),
        cyl(1.08, 0.28, "Stone", (0, 0, 1.0), rot=(0, 0, 22.5), seg=8, r2=0.72, bevel=0.08),
        cyl(0.42, 0.14, "Metal", (0.18, 0.18, 1.24), seg=12, bevel=0.03),
        cyl(0.3, 0.05, "Dark", (0.18, 0.18, 1.37), seg=12, bevel=0.01),
        # firing slit: dark recess with a heavy visor above
        box(1.0, 0.3, 0.2, "Dark", (0, -0.95, 0.66), bevel=0.04),
        box(1.25, 0.36, 0.12, "Metal", (0, -1.0, 0.82), bevel=0.04),
        box(1.2, 0.3, 0.08, "Stone", (0, -0.98, 0.52), bevel=0.03),
    ]
    # sandbags around the front half
    parts += sandbag_arc(1.4, 205, 335, 6, 0.13, seed=1)
    parts += sandbag_arc(1.36, 215, 325, 5, 0.36, seed=2)
    return [join(parts, "Bunker")]


# ------------------------------------------------------------------ TeslaAntenna

def tesla():
    parts = [
        cyl(0.62, 0.22, "Dark", (0, 0, 0), seg=8, r2=0.56, bevel=0.05),
        cyl(0.48, 0.16, "Metal", (0, 0, 0.2), seg=8, r2=0.42, bevel=0.04),
        cyl(0.07, 2.0, "Metal", (0, 0, 0.3), seg=8, bevel=0.0),
    ]
    for i in range(3):
        a = math.radians(90 + i * 120)
        p0 = (0.42 * math.cos(a), 0.42 * math.sin(a), 0.3)
        p1 = (0.1 * math.cos(a), 0.1 * math.sin(a), 1.9)
        parts.append(rod(p0, p1, 0.055, "Metal", seg=6))
        parts.append(box(0.16, 0.16, 0.12, "Dark", p0, rot=(0, 0, math.degrees(a)), bevel=0.03))
    for z, R, m in ((0.75, 0.38, "TeamColor"), (1.1, 0.31, "Metal"), (1.42, 0.25, "TeamColor"),
                    (1.7, 0.19, "Metal")):
        parts.append(torus(R, 0.055, m, (0, 0, z), seg=16, mseg=6))
    parts += [
        cyl(0.2, 0.12, "TeamColorDark", (0, 0, 2.0), seg=12, r2=0.26, bevel=0.03),
        sphere(0.27, "Paint", (0, 0, 2.36), seg=14, rings=8),
        torus(0.27, 0.035, "Metal", (0, 0, 2.36), rot=(90, 0, 0), seg=16, mseg=4),
        torus(0.27, 0.035, "Metal", (0, 0, 2.36), rot=(90, 0, 90), seg=16, mseg=4),
    ]
    return [join(parts, "TeslaAntenna")]


# ------------------------------------------------------------------ Mine

def mine():
    parts = [
        cyl(0.35, 0.09, "Dark", (0, 0, 0), seg=16, r2=0.31, bevel=0.03),
        cyl(0.29, 0.03, "Paint", (0, 0, 0.08), seg=16, bevel=0.01),
        cyl(0.25, 0.04, "Dark", (0, 0, 0.08), seg=16, bevel=0.01),
    ]
    parts += skull(0, 0.01, 0.115, 0.3)
    for i in range(6):
        a = math.radians(i * 60)
        parts.append(sphere(0.022, "Metal", (0.325 * math.cos(a), 0.325 * math.sin(a), 0.06),
                            seg=6, rings=4))
    return [join(parts, "Mine")]


# ------------------------------------------------------------------ Sandbags

def sandbags():
    R = 2.0
    cy = R  # arc centre behind, wall bulges toward -Y (front)
    parts = []
    parts += sandbag_arc(R, 242, 298, 4, 0.13, cy=cy, jitter=4, seed=3)
    parts += sandbag_arc(R - 0.02, 248, 292, 3, 0.36, cy=cy, jitter=5, seed=4)
    parts += sandbag_arc(R - 0.04, 256, 284, 2, 0.58, cy=cy, jitter=6, seed=5)
    return [join(parts, "Sandbags")]


# ------------------------------------------------------------------ Wall

def wall_parts(length=2.0, merlon_xs=(-0.5, 0.5)):
    """Wall of offset stone blocks along X, tiles every `length` units."""
    p = [box(length, 0.74, 0.2, "Rock", (0, 0, 0.1), bevel=0.05, bseg=1)]
    g = 0.025
    rows = [(0.2, 0.62, [length / 3] * 3),
            (0.62, 1.06, [length / 6, length / 3, length / 3, length / 6])]
    for z0, z1, widths in rows:
        x = -length / 2
        for w in widths:
            p.append(box(w - g, 0.62, z1 - z0 - g, "Stone", (x + w / 2, 0, (z0 + z1) / 2),
                         bevel=0.05, bseg=1))
            x += w
    p.append(box(length, 0.72, 0.12, "Stone", (0, 0, 1.12), bevel=0.04, bseg=1))
    for x in merlon_xs:
        p.append(box(0.56, 0.64, 0.38, "Stone", (x, 0, 1.36), bevel=0.07, bseg=2))
    return p


def wall():
    return [join(wall_parts(), "Wall")]


def wall_corner():
    """Square corner pillar, 0.9 footprint, origin at its center. Place at wall junctions."""
    parts = [box(0.96, 0.96, 0.22, "Rock", (0, 0, 0.11), bevel=0.05, bseg=1)]
    for z0, z1 in ((0.22, 0.66), (0.66, 1.12)):
        parts.append(box(0.84, 0.84, z1 - z0 - 0.025, "Stone", (0, 0, (z0 + z1) / 2),
                         bevel=0.05, bseg=1))
    parts.append(box(0.98, 0.98, 0.14, "Stone", (0, 0, 1.19), bevel=0.045, bseg=1))
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(box(0.32, 0.32, 0.38, "Stone", (sx * 0.31, sy * 0.31, 1.44),
                             bevel=0.06, bseg=2))
    return [join(parts, "WallCorner")]


# ------------------------------------------------------------------ HQ

def hq():
    """Command base: fortified plinth, round keep with team roof, paint tank, flag mount."""
    pz = 0.62  # plinth top
    parts = [
        box(5.0, 5.0, 0.2, "Rock", (0, 0, 0.1), bevel=0.07, bseg=1),
        box(4.6, 4.6, 0.46, "Stone", (0, 0, 0.36), bevel=0.1, bseg=2),
        box(4.72, 4.72, 0.1, "Stone", (0, 0, pz - 0.04), bevel=0.04, bseg=1),
        box(1.3, 0.3, 0.3, "Stone", (0, -2.45 + 0.03, 0.15), bevel=0.05, bseg=1),
    ]
    # merlons along the plinth edge, gap at the front gate
    for i in range(5):
        t = -1.84 + i * 0.92
        for (x, y, rz) in ((t, 2.2, 0), (2.2, t, 90), (-2.2, t, 90), (t, -2.2, 0)):
            if y < -2 and abs(x) < 1.0:
                continue
            parts.append(box(0.5, 0.28, 0.32, "Stone", (x, y, pz + 0.14), rot=(0, 0, rz),
                             bevel=0.06, bseg=1))
    # keep
    ky = 0.3
    kz = pz
    parts += [
        cyl(1.72, 0.2, "Rock", (0, ky, kz - 0.02), seg=12, bevel=0.05, bseg=1),
        cyl(1.58, 1.7, "Stone", (0, ky, kz + 0.12), seg=12, r2=1.47, bevel=0.07, bseg=1),
        cyl(1.56, 0.2, "TeamColorDark", (0, ky, kz + 1.78), seg=12, bevel=0.04, bseg=1),
        cyl(1.6, 0.06, "Stone", (0, ky, kz + 0.95), seg=12, bevel=0.0),
    ]
    rz0 = kz + 1.9
    roof = cyl(1.86, 1.4, "TeamColor", (0, ky, rz0), seg=12, r2=0.14, bevel=0.06, bseg=1)
    recolor_faces(roof, "TeamColorDark",
                  lambda p, i: -0.5 < p.normal.z < 0.95 and
                  int((math.atan2(p.center.y, p.center.x) + math.pi) / (2 * math.pi) * 12
                      + 0.5) % 2 == 0)
    parts.append(roof)
    # flag mount
    top = rz0 + 1.4
    parts += [
        cyl(0.2, 0.14, "Metal", (0, ky, top - 0.08), seg=8, bevel=0.03, bseg=1),
        cyl(0.05, 0.55, "Metal", (0, ky, top), seg=6, bevel=0),
        sphere(0.09, "Accent", (0, ky, top + 0.58), seg=8, rings=5),
        poly([(0.05, 0, 0), (0.6, 0, -0.14), (0.05, 0, -0.28)], [(0, 1, 2)], "TeamColor",
             loc=(0, ky, top + 0.5), solidify=0.04),
    ]
    # gate: dark arch with wooden door on the keep front
    fy = ky - 1.5
    parts += [
        box(0.92, 0.3, 0.9, "Dark", (0, fy, kz + 0.45), bevel=0.04, bseg=1),
        cyl(0.46, 0.3, "Dark", (0, fy + 0.15, kz + 0.9), rot=(90, 0, 0), seg=10, bevel=0.0),
        box(0.74, 0.3, 0.84, "Wood", (0, fy - 0.03, kz + 0.42), bevel=0.04, bseg=1),
        box(1.12, 0.3, 0.14, "Metal", (0, fy - 0.08, kz + 1.42), bevel=0.04, bseg=1),
        cyl(0.27, 0.06, "Accent", (0, fy - 0.2, kz + 1.42), rot=(90, 0, 0), seg=12, bevel=0.01),
    ]
    # window slits
    for a in (-60, 60, 180, 120, -120):
        r = math.radians(a - 90)
        parts.append(box(0.16, 0.14, 0.45, "Dark", (1.5 * math.cos(r), ky + 1.5 * math.sin(r),
                                                     kz + 1.3), rot=(0, 0, a), bevel=0.03, bseg=1))
    # back corner bastions
    for sx in (-1, 1):
        bx, by = sx * 1.75, 1.75
        parts += [
            cyl(0.58, 1.1, "Stone", (bx, by, pz - 0.05), seg=10, r2=0.54, bevel=0.05, bseg=1),
            cyl(0.6, 0.12, "TeamColorDark", (bx, by, pz + 1.0), seg=10, bevel=0.03, bseg=1),
            cyl(0.68, 0.6, "TeamColor", (bx, by, pz + 1.1), seg=10, r2=0.08, bevel=0.04,
                bseg=1),
        ]
    # big paint tank front right
    px, py = 1.5, -1.35
    parts += [
        cyl(0.66, 0.14, "Metal", (px, py, pz - 0.02), seg=12, bevel=0.03, bseg=1),
        cyl(0.56, 1.3, "Paint", (px, py, pz + 0.1), seg=12, bevel=0.05, bseg=2),
        cyl(0.59, 0.1, "Metal", (px, py, pz + 0.6), seg=12, bevel=0.02, bseg=1),
        cyl(0.6, 0.14, "Metal", (px, py, pz + 1.36), seg=12, bevel=0.03, bseg=1),
        sphere(0.54, "Metal", (px, py, pz + 1.46), scale=(1, 1, 0.42), seg=12, rings=6),
        cyl(0.1, 0.18, "Dark", (px, py, pz + 1.64), seg=8, bevel=0.02, bseg=1),
        rod((px - 0.35, py + 0.35, pz + 1.2), (0.9, -0.3, pz + 1.5), 0.08, "Metal", seg=8),
    ]
    # sandbags and crates front left
    bags = sandbag_arc(0.85, 215, 325, 4, pz + 0.13)
    bags += sandbag_arc(0.82, 230, 310, 3, pz + 0.36)
    for bg in bags:
        bg.location.x -= 1.45
        bg.location.y -= 0.65
    parts += bags
    parts += [box(0.62, 0.62, 0.62, "Wood", (-1.55, 0.3, pz + 0.31), rot=(0, 0, 15), bevel=0.05,
                  bseg=1),
              box(0.42, 0.42, 0.42, "Wood", (-1.6, 0.35, pz + 0.83), rot=(0, 0, -10),
                  bevel=0.04, bseg=1)]
    return [join(parts, "HQ")]


# ------------------------------------------------------------------ CapturePoint

def flag_cloth(w=1.15, h=0.78, nx=7, nz=4, amp=0.07, name="Flag"):
    """Waving flag along +X from the pole, top edge at local z=0."""
    def wave(x):
        return amp * math.sin(x / w * math.pi * 1.6) * (x / w)

    verts, faces = [], []
    for j in range(nz + 1):
        for i in range(nx + 1):
            x = 0.04 + w * i / nx
            z = -h * j / nz - 0.06 * (x / w) * (j / nz)
            verts.append((x, wave(x), z))
    for j in range(nz):
        for i in range(nx):
            a = j * (nx + 1) + i
            faces.append((a, a + 1, a + nx + 2, a + nx + 1))
    cloth = poly(verts, faces, "TeamColor", solidify=0.035, name=name)
    # white star emblem on both faces
    cx, cz = w * 0.55, -h * 0.5
    sv = []
    for k in range(10):
        r = 0.2 if k % 2 == 0 else 0.085
        a = math.pi / 2 + k * math.pi / 5
        sv.append((cx + r * math.cos(a), cz + r * math.sin(a)))
    stars = []
    for side in (-1, 1):
        vv = [(x, wave(x) + side * 0.02, z) for (x, z) in sv] + [(cx, wave(cx) + side * 0.02, cz)]
        ff = [(k, (k + 1) % 10, 10) for k in range(10)]
        stars.append(poly(vv, ff, "Accent", solidify=0.006, name="star"))
    return [cloth] + stars


def banner_cloth(w=0.86, h=1.25, nx=4, nz=6, amp=0.05, name="Flag"):
    """Vertical banner hanging from a crossbar along +X, top edge at local z=0, a diamond emblem on both faces."""
    def wave(x, z):
        return amp * math.sin(-z / h * math.pi * 1.3) * (0.4 + x / w)

    verts, faces = [], []
    for j in range(nz + 1):
        for i in range(nx + 1):
            x = 0.1 + w * i / nx
            z = -h * j / nz
            if j == nz:  # swallowtail: the bottom edge is notched in the middle
                z += 0.22 * (1.0 - abs(i / nx - 0.5) * 2.0)
            verts.append((x, wave(x, z), z))
    for j in range(nz):
        for i in range(nx):
            a = j * (nx + 1) + i
            faces.append((a, a + 1, a + nx + 2, a + nx + 1))
    cloth = poly(verts, faces, "TeamColor", solidify=0.035, name=name)
    cx, cz = 0.1 + w * 0.5, -h * 0.42
    marks = []
    for side in (-1, 1):
        d = [(cx, cz + 0.24), (cx + 0.19, cz), (cx, cz - 0.24), (cx - 0.19, cz)]
        vv = [(x, wave(x, z) + side * 0.024, z) for (x, z) in d]
        marks.append(poly(vv, [(0, 1, 2, 3)], "Accent", solidify=0.006, name="mark"))
    return [cloth] + marks


def capture_point():
    """Round stone dais in two steps with a team colored inlay ring, a center plinth and a banner pole."""
    pad_parts = [
        cyl(2.15, 0.12, "Rock", (0, 0, 0), seg=24, bevel=0.035, bseg=1),
        cyl(1.78, 0.12, "Stone", (0, 0, 0.12), seg=24, bevel=0.035, bseg=1),
        cyl(1.52, 0.015, "TeamColor", (0, 0, 0.24), seg=24, bevel=0),
        cyl(1.32, 0.02, "Stone", (0, 0, 0.24), seg=24, bevel=0),
        cyl(0.45, 0.16, "Rock", (0, 0, 0.24), seg=10, bevel=0.04),
        cyl(0.28, 0.06, "Metal", (0, 0, 0.4), seg=10, bevel=0.02),
    ]
    for i in range(6):
        a = math.radians(i * 60 + 30)
        pad_parts.append(box(0.3, 0.3, 0.1, "Stone", (1.97 * math.cos(a), 1.97 * math.sin(a), 0.12),
                             rot=(0, 0, i * 60 + 30), bevel=0.03, bseg=1))
    pad = join(pad_parts, "Pad")
    pz = 0.44
    top = pz + 3.0
    pole = join([cyl(0.07, 3.1, "Trunk", (0, 0, pz), seg=8, bevel=0.0),
                 cyl(0.11, 0.14, "Dark", (0, 0, pz), seg=8, bevel=0.02),
                 rod((0, 0, top - 0.05), (1.05, 0, top - 0.05), 0.045, "Trunk", seg=6),
                 sphere(0.1, "Accent", (0, 0, pz + 3.15), seg=10, rings=6),
                 sphere(0.06, "Accent", (1.07, 0, top - 0.05), seg=8, rings=5)],
                "Pole", origin=(0, 0, pz))
    parent_keep(pole, pad)
    flag = join(banner_cloth(), "Flag", smooth_angle=60)
    flag.location = (0, 0, top - 0.1)
    parent_keep(flag, pole)
    return [pad, pole, flag]


# ------------------------------------------------------------------ SpawnHatch

def spawn_hatch():
    parts = [
        cyl(1.0, 0.08, "Metal", (0, 0, 0), seg=24, r2=0.96, bevel=0.03),
        cyl(0.9, 0.1, "TeamColor", (0, 0, 0.0), seg=24, bevel=0.02),
        cyl(0.76, 0.14, "Dark", (0, 0, 0.0), seg=24, r2=0.72, bevel=0.035),
        box(0.36, 0.2, 0.08, "Metal", (0, 0.86, 0.1), bevel=0.03),
        cyl(0.05, 0.3, "Metal", (-0.15, 0.86, 0.12), rot=(0, 90, 0), seg=8, bevel=0),
    ]
    for i in range(10):
        a = math.radians(i * 36 + 18)
        parts.append(sphere(0.035, "Dark", (0.95 * math.cos(a), 0.95 * math.sin(a), 0.08),
                            seg=6, rings=4))
    parts += skull(0, 0.02, 0.135, 0.62)
    return [join(parts, "SpawnHatch")]


# ------------------------------------------------------------------ Tent

def tent():
    W, D, E, H = 1.0, 0.95, 0.38, 1.55  # half width, half depth, eave height, ridge
    o = 0.1  # roof overhang
    verts = [(-W, -D, 0), (W, -D, 0), (W, D, 0), (-W, D, 0),
             (-W, -D, E), (W, -D, E), (W, D, E), (-W, D, E),
             (0, -D, H), (0, D, H)]
    faces = [(0, 1, 5, 8, 4), (2, 3, 7, 9, 6), (1, 2, 6, 5), (3, 0, 4, 7),
             (0, 3, 2, 1)]
    body = poly(verts, faces, "Canvas", bevel=0.04, bseg=2, angle=30)
    # roof slabs with overhang and a gentle sag
    roofs = []
    for sx in (-1, 1):
        rv, rf = [], []
        n = 4
        for i in range(n + 1):
            y = -D - o + (2 * (D + o)) * i / n
            sag = 0.05 * math.sin(math.pi * i / n)
            rv.append((sx * (W + o), y, E - 0.08))
            rv.append((0, y, H + 0.03 - sag))
        for i in range(n):
            a = 2 * i
            rf.append((a, a + 2, a + 3, a + 1) if sx > 0 else (a, a + 1, a + 3, a + 2))
        roofs.append(poly(rv, rf, "Canvas", solidify=0.06, bevel=0.025, bseg=1, angle=30,
                          name="roof"))
    parts = [body] + roofs
    # seams: strips across each roof slope and along the eaves
    import mathutils
    for sx in (-1, 1):
        slope = mathutils.Vector((-sx * (W + o), 0, H - E + 0.1))
        L = slope.length
        ang = math.degrees(math.atan2(H - E + 0.1, W + o))
        for y in (-0.45, 0.45):
            parts.append(box(L, 0.07, 0.03, "Sand", (sx * (W + o) / 2, y,
                                                    (E - 0.08 + H) / 2 + 0.05),
                             rot=(0, sx * ang, 0), bevel=0.0))
        parts.append(box(0.06, 2 * (D + o), 0.06, "Sand", (sx * (W + o - 0.03), 0, E - 0.06),
                         rot=(0, sx * ang, 0), bevel=0.0))
    # ridge pole and front poles
    parts += [
        rod((0, -D - 0.28, H + 0.08), (0, D + 0.28, H + 0.08), 0.06, "Wood", seg=8),
        sphere(0.09, "Wood", (0, -D - 0.3, H + 0.08), seg=8, rings=5),
        sphere(0.09, "Wood", (0, D + 0.3, H + 0.08), seg=8, rings=5),
        cyl(0.05, H + 0.1, "Wood", (0, -D - 0.06, 0), seg=6, bevel=0),
    ]
    # doorway with tied-back flaps
    parts.append(poly([(-0.36, -D - 0.025, 0), (0.36, -D - 0.025, 0), (0, -D - 0.025, 1.12)],
                      [(0, 1, 2)], "Dark", solidify=0.01, name="door"))
    for sx in (-1, 1):
        parts.append(poly([(sx * 0.36, -D - 0.05, 0), (sx * 0.62, -D - 0.06, 0),
                           (sx * 0.06, -D - 0.05, 1.08)], [(0, 1, 2)], "Canvas", solidify=0.03,
                          name="flap"))
        parts.append(box(0.08, 0.05, 0.06, "Wood", (sx * 0.45, -D - 0.08, 0.35), bevel=0))
    # guy ropes and stakes
    for sx in (-1, 1):
        for sy in (-1, 1):
            st = (sx * (W + 0.45), sy * (D - 0.25), 0)
            parts.append(rod((sx * (W + o - 0.02), sy * (D - 0.25), E - 0.05),
                             (st[0], st[1], 0.06), 0.015, "Accent", seg=4))
            parts.append(cyl(0.04, 0.14, "Wood", st, seg=5, bevel=0))
    return [join(parts, "Tent", smooth_angle=35)]


BUILDERS = {
    "Turret": turret, "Tank": tank, "Cannon": cannon, "Bunker": bunker,
    "TeslaAntenna": tesla, "Mine": mine, "Sandbags": sandbags, "Wall": wall,
    "WallCorner": wall_corner, "HQ": hq, "CapturePoint": capture_point,
    "SpawnHatch": spawn_hatch, "Tent": tent,
}

if __name__ == "__main__":
    C.run(BUILDERS, "Structures", C.cli_names(),
          stats_file=os.path.join(C.OUT, "stats_structures.json"))
