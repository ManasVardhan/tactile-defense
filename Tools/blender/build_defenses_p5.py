"""Phase 5 defenses for Tactile Wars: Sniper tower and Geyser vent.
Usage: blender -b -P Tools/blender/build_defenses_p5.py -- [Sniper Geyser]
Exports to Assets/Art/Models/Structures and renders Docs/Previews/<Name>.png.
Blender front is -Y (becomes Unity +Z). Child pivots sit on their rotation axes.

Hierarchies (contract style, code driven parts):
- Sniper.fbx: Base > Head (yaws toward the target) > Muzzle (empty at the rifle tip).
- Geyser.fbx: Geyser (rock ring and crack) > Vent (paint pool disc that bulges before an eruption).
Uses tw_env_common.patch_fbx_exporter() (through export_fbx) for the nested Muzzle.
"""
import os
import sys
import math
import random

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import tw_env_common as C  # noqa: E402
from tw_env_common import (cyl, box, sphere, blob, torus, poly, join, empty,  # noqa: E402
                           parent_keep, rod, skull)


def pillow(cx, cy, cz, rz, sx=0.42, sy=0.27, sz=0.2, mat="Sand"):
    return sphere(1.0, mat, (cx, cy, cz), rot=(0, 0, rz), scale=(sx / 2, sy / 2, sz / 2), seg=10, rings=6)


# ------------------------------------------------------------------ Sniper

def sniper():
    pz = 1.45  # platform top
    base_parts = [
        cyl(0.78, 0.16, "Stone", (0, 0, 0), seg=8, r2=0.72, bevel=0.05),
        cyl(0.62, 0.08, "Dark", (0, 0, 0.14), seg=8, bevel=0.02),
    ]
    # four splayed wooden legs with cross braces
    feet = []
    for i in range(4):
        a = math.radians(45 + i * 90)
        f = (0.6 * math.cos(a), 0.6 * math.sin(a), 0.16)
        t = (0.4 * math.cos(a), 0.4 * math.sin(a), pz - 0.1)
        feet.append((f, t))
        base_parts.append(rod(f, t, 0.075, "Wood", seg=6))
        base_parts.append(box(0.2, 0.2, 0.1, "Dark", (f[0], f[1], 0.2), rot=(0, 0, 45 + i * 90), bevel=0.03))
    for i in range(4):
        (f0, t0), (f1, t1) = feet[i], feet[(i + 1) % 4]
        m0 = tuple(f0[k] + (t0[k] - f0[k]) * 0.3 for k in range(3))
        m1 = tuple(f1[k] + (t1[k] - f1[k]) * 0.65 for k in range(3))
        base_parts.append(rod(m0, m1, 0.035, "Wood", seg=5))
    # platform deck with a sandbag rim and a team colored apron
    base_parts += [
        cyl(0.66, 0.12, "Wood", (0, 0, pz - 0.12), seg=8, bevel=0.03),
        cyl(0.7, 0.07, "TeamColorDark", (0, 0, pz - 0.16), seg=8, bevel=0.02),
    ]
    rnd = random.Random(7)
    for i in range(9):
        a = math.radians(20 + i * 36 + rnd.uniform(-5, 5))
        if 230 < math.degrees(a) % 360 < 310:
            continue  # leave the front open for the rifle
        base_parts.append(pillow(0.56 * math.cos(a), 0.56 * math.sin(a), pz + 0.08, math.degrees(a) + 90))
    # small pennant pole at the back
    base_parts.append(cyl(0.025, 0.95, "Metal", (0.0, 0.55, pz), seg=6, bevel=0))
    base_parts.append(poly([(0.0, 0.55, pz + 0.95), (0.0, 0.55, pz + 0.68), (0.36, 0.6, pz + 0.83)],
                           [(0, 1, 2)], "TeamColor", solidify=0.02, name="pennant"))
    base = join(base_parts, "Base")

    hz = pz + 0.02  # yaw pivot
    head_parts = [
        # swivel ring and a domed gunner cupola (helmet read, team colored)
        cyl(0.3, 0.1, "Dark", (0, 0, hz), seg=12, bevel=0.02),
        sphere(0.36, "TeamColor", (0, 0.05, hz + 0.3), scale=(1, 1, 0.88), seg=16, rings=9),
        cyl(0.38, 0.06, "TeamColorDark", (0, 0.05, hz + 0.18), seg=16, bevel=0.02),
        # visor slit with two eyes, like the soldiers' face
        box(0.42, 0.08, 0.13, "Accent", (0, -0.27, hz + 0.29), bevel=0.04),
        sphere(0.035, "Dark", (-0.08, -0.31, hz + 0.29), seg=6, rings=4),
        sphere(0.035, "Dark", (0.08, -0.31, hz + 0.29), seg=6, rings=4),
        # long rifle: receiver, barrel, muzzle brake, scope with glass lenses
        box(0.16, 0.42, 0.14, "Dark", (0.2, -0.32, hz + 0.24), bevel=0.04),
        cyl(0.06, 1.05, "Metal", (0.2, -0.5, hz + 0.26), rot=(90, 0, 0), seg=8, bevel=0.01),
        cyl(0.09, 0.16, "Dark", (0.2, -1.5, hz + 0.26), rot=(90, 0, 0), seg=8, bevel=0.02),
        cyl(0.06, 0.02, "Paint", (0.2, -1.64, hz + 0.26), rot=(90, 0, 0), seg=8, bevel=0),
        cyl(0.065, 0.42, "Dark", (0.2, -0.36, hz + 0.39), rot=(90, 0, 0), seg=10, bevel=0.015),
        cyl(0.055, 0.015, "Glass", (0.2, -0.575, hz + 0.39), rot=(90, 0, 0), seg=10, bevel=0),
        cyl(0.05, 0.015, "Glass", (0.2, -0.145, hz + 0.39), rot=(90, 0, 0), seg=10, bevel=0),
        rod((0.2, -0.33, hz + 0.31), (0.2, -0.33, hz + 0.35), 0.02, "Dark"),
        # bipod
        rod((0.2, -0.95, hz + 0.22), (0.12, -1.0, hz + 0.02), 0.018, "Metal", seg=4),
        rod((0.2, -0.95, hz + 0.22), (0.28, -1.0, hz + 0.02), 0.018, "Metal", seg=4),
        # paint ammo drum on the side
        cyl(0.11, 0.16, "Paint", (0.33, -0.22, hz + 0.18), rot=(0, 90, 0), seg=10, bevel=0.02),
    ]
    head = join(head_parts, "Head", origin=(0, 0, hz))
    parent_keep(head, base)
    muzzle = empty("Muzzle", (0.2, -1.66, hz + 0.26), parent=head)
    return [base, head, muzzle]


# ------------------------------------------------------------------ Geyser

def geyser():
    rnd = random.Random(11)
    parts = [
        # cracked earth disc that blends with the ground, a low rock rim
        cyl(0.82, 0.03, "Sand", (0, 0, 0), seg=10, r2=0.78, bevel=0.01),
    ]
    n = 9
    for i in range(n):
        a = math.radians(i * 360 / n + rnd.uniform(-10, 10))
        r = 0.6 + rnd.uniform(-0.05, 0.06)
        s = rnd.uniform(0.15, 0.22)
        parts.append(blob(s, "Rock", (r * math.cos(a), r * math.sin(a), s * 0.35), rot=(0, 0, rnd.uniform(0, 360)),
                          scale=(1.2, 0.9, 0.55), subdiv=1, jitter=0.25, seed=i + 3))
    # dark throat and three crack lines radiating out
    parts.append(cyl(0.36, 0.04, "Dark", (0, 0, 0.02), seg=12, r2=0.3, bevel=0.01))
    for i in range(3):
        a = math.radians(30 + i * 120 + rnd.uniform(-15, 15))
        p0 = (0.32 * math.cos(a), 0.32 * math.sin(a), 0.035)
        p1 = (0.78 * math.cos(a + 0.2), 0.78 * math.sin(a + 0.2), 0.035)
        parts.append(rod(p0, p1, 0.025, "Dark", seg=4))
    body = join(parts, "Geyser")
    vent_parts = [
        cyl(0.26, 0.05, "Paint", (0, 0, 0.04), seg=14, r2=0.22, bevel=0.02),
        sphere(0.07, "Paint", (0.08, 0.05, 0.08), scale=(1, 1, 0.6), seg=8, rings=5),
        sphere(0.05, "Paint", (-0.09, -0.04, 0.08), scale=(1, 1, 0.6), seg=8, rings=5),
    ]
    vent = join(vent_parts, "Vent", origin=(0, 0, 0.04))
    parent_keep(vent, body)
    return [body, vent]


BUILDERS = {"Sniper": sniper, "Geyser": geyser}

if __name__ == "__main__":
    C.run(BUILDERS, "Structures", C.cli_names(),
          stats_file=os.path.join(C.OUT, "stats_defenses_p5.json"))
