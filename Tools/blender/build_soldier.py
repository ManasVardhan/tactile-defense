"""Build the Tactile Wars chibi paint-gun soldier and its variants, with animation takes.

Run:  blender -b -P Tools/blender/build_soldier.py -- [soldier] [heavy] [hero] [--no-export]
Outputs (per variant): Assets/Art/Models/Characters/<Name>.fbx and Docs/Previews/<Name>_*.png

Blender authoring space: Z up, facing -Y, origin at feet. Exported with axis_forward='-Z',
axis_up='Y', so in Unity the character is Y up, facing +Z, scale 1.
"""
import bpy
import math
import os
import sys
import importlib

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import tw_common as C
importlib.reload(C)
from tw_common import (Part, ellipsoid, capsule, cylinder, rbox, torus, surface_point, place_on,
                       blend_weights, smooth, ease_out, spring, seg, lerp)
from mathutils import Vector, Matrix
import bmesh

TAKES = ["Idle", "Run", "Shoot", "Hit", "Die", "Cheer"]


# ============================================================== shared head pieces
def helmet_parts(center, radii, cut_pt, cut_n, lip_out, lip_down, seg_=24, rings=14,
                 rim_mat="Accent", bone="Head"):
    """Bowl helmet: ellipsoid cut by a tilted plane, with a rolled lip on the opening."""
    bm = ellipsoid(center, radii, seg_, rings)
    geom = list(bm.verts) + list(bm.edges) + list(bm.faces)
    bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-5, plane_co=Vector(cut_pt),
                           plane_no=Vector(cut_n).normalized(), clear_inner=True)
    boundary = [e for e in bm.edges if len(e.link_faces) == 1]
    cx, cy = center[0], center[1]
    n = Vector(cut_n).normalized()

    def push(verts, out, down):
        for v in verts:
            d = Vector((v.co.x - cx, v.co.y - cy, 0))
            if d.length > 1e-6:
                d.normalize()
            v.co += d * out - n * down

    ret = bmesh.ops.extrude_edge_only(bm, edges=boundary)
    v1 = [g for g in ret["geom"] if isinstance(g, bmesh.types.BMVert)]
    e1 = [g for g in ret["geom"] if isinstance(g, bmesh.types.BMEdge) and
          all(v in v1 for v in g.verts)]
    push(v1, lip_out, lip_down * 0.25)
    lip_faces = set(f for v in v1 for f in v.link_faces)
    ret = bmesh.ops.extrude_edge_only(bm, edges=e1)
    v2 = [g for g in ret["geom"] if isinstance(g, bmesh.types.BMVert)]
    e2 = [g for g in ret["geom"] if isinstance(g, bmesh.types.BMEdge) and
          all(v in v2 for v in g.verts)]
    push(v2, lip_out * 0.15, lip_down)
    lip_faces |= set(f for v in v2 for f in v.link_faces)
    ret = bmesh.ops.extrude_edge_only(bm, edges=e2)
    v3 = [g for g in ret["geom"] if isinstance(g, bmesh.types.BMVert)]
    push(v3, -lip_out * 1.6, -lip_down * 0.3)
    lip_faces |= set(f for v in v3 for f in v.link_faces)
    # close the opening with a recessed dark cap (reads as shadow inside the helmet)
    hole = [e for e in bm.edges if len(e.link_faces) == 1]
    cap = bmesh.ops.holes_fill(bm, edges=hole, sides=0)["faces"]
    cap = bmesh.ops.triangulate(bm, faces=cap)["faces"]
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in lip_faces:
        f.material_index = 1
    for f in cap:
        f.material_index = 2
    p = Part(bm, "TeamColor", bone)
    p.extra_mats = [rim_mat, "Dark"]
    return p


def eye_parts(face_c, face_r, xs, z, size, bone="Head", glint=True):
    parts = []
    for x in xs:
        p, nrm = surface_point(face_c, face_r, x, z)
        e = ellipsoid((0, 0, 0), size, 8, 6)
        place_on(e, p, nrm, sink=size[1] * 0.35)
        parts.append(Part(e, "Dark", bone))
        if glint:
            gp, gn = surface_point(face_c, face_r, x + size[0] * 0.3, z + size[2] * 0.42)
            g = ellipsoid((0, 0, 0), (size[0] * 0.33, size[1] * 0.5, size[0] * 0.33), 6, 4)
            place_on(g, gp, gn, sink=-size[1] * 0.45)
            parts.append(Part(g, "Accent", bone))
    return parts


# ============================================================== SOLDIER
def soldier_geo():
    P = []
    # face (white, pushed forward so it peeks out of the helmet like a mask)
    FC, FR = (0, -0.08, 0.50), (0.205, 0.175, 0.175)
    P.append(Part(ellipsoid(FC, FR, 18, 9), "Skin", "Head"))
    P += eye_parts(FC, FR, (-0.074, 0.074), 0.495, (0.042, 0.02, 0.066))
    # helmet: bell shaped bowl, opening tilted so it wraps the cheeks and the back
    P.append(helmet_parts(center=(0, 0.035, 0.60), radii=(0.31, 0.31, 0.30),
                          cut_pt=(0, -0.30, 0.648), cut_n=(0, 0.42, 1),
                          lip_out=0.02, lip_down=0.034, seg_=28, rings=12))
    # body
    P.append(Part(ellipsoid((0, 0.005, 0.265), (0.13, 0.115, 0.14), 14, 8), "TeamColor",
                  blend_weights("Hips", "Spine", 0.22, 0.32)))
    P.append(Part(torus((0, 0.005, 0.215), 0.121, 0.02, 14, 3), "TeamColorDark", "Hips"))
    # paint tank backpack
    P.append(Part(cylinder((0, 0.135, 0.19), (0, 0.135, 0.35), 0.055, 10, bevel=0.018,
                           bevel_segs=1), "Paint", "Spine", hard=True))
    # legs and boots
    for side, x in (("L", 0.062), ("R", -0.062)):
        P.append(Part(capsule((x, 0.0, 0.16), (x, 0.0, 0.07), 0.04, 8, 4), "TeamColorDark",
                      "Leg." + side))
        P.append(Part(ellipsoid((x * 1.05, -0.018, 0.04), (0.056, 0.074, 0.042), 10, 5),
                      "Dark", "Leg." + side))
    # arms + hands
    D = soldier_dims()
    for side in ("L", "R"):
        sh, hd = D["shoulder_" + side], D["hand_" + side]
        P.append(Part(capsule(sh, hd, 0.034, 8, 4, r1=0.03), "TeamColor", "Arm." + side))
        P.append(Part(ellipsoid(hd, (0.044, 0.044, 0.042), 8, 6), "Skin",
                      "Hand.R" if side == "R" else "Arm.L"))
    # chunky paint gun
    gx, gz = D["gx"], D["gz"]
    P.append(Part(rbox((gx, -0.19, gz), (0.10, 0.25, 0.11), 0.026), "Metal", "Gun", True))
    P.append(Part(rbox((gx, -0.05, gz - 0.01), (0.075, 0.09, 0.095), 0.022, 1), "Dark", "Gun",
                  True))
    P.append(Part(rbox((gx, -0.13, gz - 0.075), (0.05, 0.05, 0.07), 0.014, 1,
                       rot=(-15, 0, 0)), "Dark", "Gun", True))
    P.append(Part(cylinder((gx, -0.30, gz), (gx, -0.42, gz), 0.034, 10), "Metal", "Gun", True))
    P.append(Part(cylinder((gx, -0.41, gz), (gx, -0.47, gz), 0.048, 12, bevel=0.012,
                           bevel_segs=1), "Dark", "Gun", True))
    # paint chamber: glowing team-colored drum across the receiver, plus a top sight
    P.append(Part(cylinder((gx - 0.07, -0.235, gz + 0.012), (gx + 0.07, -0.235, gz + 0.012),
                           0.058, 12, bevel=0.014, bevel_segs=1), "Paint", "Gun", True))
    P.append(Part(rbox((gx, -0.13, gz + 0.07), (0.03, 0.07, 0.035), 0.01, 1), "Dark", "Gun",
                  True))
    return P


def soldier_dims():
    gx, gz = -0.035, 0.255
    return dict(
        name="Soldier", height=1.0, gx=gx, gz=gz,
        hips=0.17, spine=0.27, neck=0.40, top=0.9, leg_x=0.062, leg_bot=0.04,
        shoulder_L=(0.125, 0.0, 0.33), shoulder_R=(-0.125, 0.0, 0.33),
        hand_L=(0.035, -0.26, 0.21), hand_R=(-0.075, -0.13, 0.225),
        gun_head=(gx, -0.13, gz), gun_tail=(gx, -0.32, gz), muzzle=(gx, -0.47, gz),
    )


# ============================================================== HEAVY
def heavy_dims():
    gx, gz = -0.285, 0.445
    return dict(
        name="SoldierHeavy", height=1.0, gx=gx, gz=gz,
        hips=0.17, spine=0.27, neck=0.40, top=0.9, leg_x=0.08, leg_bot=0.04,
        shoulder_L=(0.165, 0.0, 0.33), shoulder_R=(-0.165, 0.0, 0.33),
        hand_L=(0.215, -0.07, 0.19), hand_R=(-0.255, -0.10, 0.365),
        gun_head=(gx, -0.10, gz), gun_tail=(gx, -0.30, gz), muzzle=(gx, -0.40, gz),
    )


def heavy_geo():
    P = []
    D = heavy_dims()
    FC, FR = (0, -0.08, 0.50), (0.2, 0.172, 0.172)
    P.append(Part(ellipsoid(FC, FR, 16, 8), "Skin", "Head"))
    P += eye_parts(FC, FR, (-0.072, 0.072), 0.495, (0.04, 0.02, 0.058))
    P.append(helmet_parts(center=(0, 0.035, 0.60), radii=(0.31, 0.31, 0.30),
                          cut_pt=(0, -0.30, 0.648), cut_n=(0, 0.42, 1),
                          lip_out=0.026, lip_down=0.042, seg_=24, rings=11))
    # helmet top knob (heavy rank mark)
    P.append(Part(ellipsoid((0, 0.03, 0.905), (0.065, 0.065, 0.055), 8, 4), "Accent", "Head"))
    # bulky body
    P.append(Part(ellipsoid((0, 0.01, 0.255), (0.175, 0.15, 0.15), 14, 8), "TeamColor",
                  blend_weights("Hips", "Spine", 0.22, 0.32)))
    P.append(Part(torus((0, 0.01, 0.20), 0.163, 0.026, 14, 3), "TeamColorDark", "Hips"))
    # chest plate
    P.append(Part(rbox((0, -0.13, 0.285), (0.15, 0.05, 0.11), 0.02, 1), "Metal", "Spine", True))
    # shoulder pads
    for x in (0.165, -0.165):
        P.append(Part(ellipsoid((x, 0.0, 0.35), (0.075, 0.08, 0.05), 10, 5), "TeamColorDark",
                      "Spine"))
    for side, x in (("L", 0.08), ("R", -0.08)):
        P.append(Part(capsule((x, 0.0, 0.15), (x, 0.0, 0.07), 0.05, 8, 4), "TeamColorDark",
                      "Leg." + side))
        P.append(Part(ellipsoid((x * 1.05, -0.02, 0.045), (0.066, 0.085, 0.047), 8, 5),
                      "Dark", "Leg." + side))
    for side in ("L", "R"):
        sh, hd = D["shoulder_" + side], D["hand_" + side]
        P.append(Part(capsule(sh, hd, 0.042, 8, 4, r1=0.036), "TeamColor", "Arm." + side))
        P.append(Part(ellipsoid(hd, (0.05, 0.05, 0.047), 8, 6), "Skin",
                      "Hand.R" if side == "R" else "Arm.L"))
    # bazooka resting on the right shoulder
    gx, gz = D["gx"], D["gz"]
    P.append(Part(cylinder((gx, 0.25, gz), (gx, -0.32, gz), 0.06, 12, bevel=0.01,
                           bevel_segs=1), "Metal", "Gun", True))
    P.append(Part(cylinder((gx, -0.30, gz), (gx, -0.40, gz), 0.074, 12, r1=0.086, bevel=0.012,
                           bevel_segs=1), "Dark", "Gun", True))
    P.append(Part(cylinder((gx, -0.392, gz), (gx, -0.398, gz), 0.07, 12), "Paint", "Gun"))
    P.append(Part(cylinder((gx, 0.20, gz), (gx, 0.27, gz), 0.072, 12, bevel=0.012,
                           bevel_segs=1), "Dark", "Gun", True))
    P.append(Part(cylinder((gx, -0.02, gz), (gx, -0.10, gz), 0.07, 12, bevel=0.01,
                           bevel_segs=1), "Paint", "Gun", True))
    P.append(Part(rbox((gx, -0.10, gz - 0.07), (0.04, 0.045, 0.06), 0.012, 1,
                       rot=(-15, 0, 0)), "Dark", "Gun", True))
    P.append(Part(rbox((gx - 0.06, -0.14, gz + 0.03), (0.035, 0.07, 0.05), 0.01, 1), "Dark",
                  "Gun", True))
    return P


# ============================================================== HERO
def hero_dims():
    gx, gz = -0.10, 0.42
    return dict(
        name="Hero", height=1.5, gx=gx, gz=gz,
        hips=0.25, spine=0.40, neck=0.62, top=1.3, leg_x=0.11, leg_bot=0.05,
        shoulder_L=(0.26, 0.0, 0.58), shoulder_R=(-0.26, 0.0, 0.58),
        hand_L=(0.06, -0.33, 0.40), hand_R=(-0.12, -0.17, 0.38),
        gun_head=(gx, -0.17, gz), gun_tail=(gx, -0.45, gz), muzzle=(gx, -0.74, gz),
    )


def hero_geo():
    P = []
    D = hero_dims()
    # boxy helmet with a dark visor
    HC = (0, 0.0, 0.92)
    P.append(Part(rbox(HC, (0.52, 0.48, 0.42), 0.11, 3), "TeamColor", "Head", True))
    P.append(Part(rbox((0, 0.0, 1.14), (0.36, 0.30, 0.06), 0.03, 1), "TeamColorDark", "Head",
                  True))
    P.append(Part(rbox((0, -0.215, 0.89), (0.40, 0.08, 0.15), 0.055, 3), "Dark", "Head", True))
    # visor eyes (white glowing dots)
    for x in (-0.085, 0.085):
        P.append(Part(ellipsoid((x, -0.255, 0.895), (0.032, 0.015, 0.042), 8, 5), "Accent",
                      "Head"))
    # side ear plates
    for x in (-0.27, 0.27):
        P.append(Part(cylinder((x * 0.93, 0, 0.90), (x * 1.06, 0, 0.90), 0.075, 12,
                               bevel=0.015, bevel_segs=1), "Metal", "Head", True))
    # antenna with paint bulb
    P.append(Part(cylinder((0.17, 0.12, 1.08), (0.20, 0.15, 1.40), 0.012, 6), "Metal", "Head"))
    P.append(Part(ellipsoid((0.20, 0.15, 1.42), (0.035, 0.035, 0.035), 8, 6), "Paint", "Head"))
    # chest
    P.append(Part(rbox((0, 0.0, 0.50), (0.42, 0.32, 0.30), 0.09, 2, taper=(1.15, 1.0)),
                  "TeamColor", blend_weights("Hips", "Spine", 0.40, 0.48), True))
    P.append(Part(rbox((0, -0.16, 0.52), (0.24, 0.05, 0.16), 0.03, 1), "Metal", "Spine", True))
    P.append(Part(rbox((0, 0.0, 0.34), (0.34, 0.27, 0.07), 0.03, 1), "Dark", "Hips", True))
    # shoulder pads
    for x in (-0.27, 0.27):
        P.append(Part(ellipsoid((x, 0.0, 0.62), (0.12, 0.12, 0.08), 12, 6), "TeamColorDark",
                      "Spine"))
    # legs + boots
    for side, x in (("L", 0.11), ("R", -0.11)):
        P.append(Part(capsule((x, 0, 0.30), (x, 0, 0.11), 0.07, 8, 6), "TeamColorDark",
                      "Leg." + side))
        P.append(Part(rbox((x, -0.035, 0.065), (0.15, 0.22, 0.13), 0.05, 2), "Dark",
                      "Leg." + side, True))
    for side in ("L", "R"):
        sh, hd = D["shoulder_" + side], D["hand_" + side]
        P.append(Part(capsule(sh, hd, 0.06, 8, 4, r1=0.055), "TeamColor", "Arm." + side))
        P.append(Part(ellipsoid(hd, (0.07, 0.07, 0.066), 8, 6), "Dark",
                      "Hand.R" if side == "R" else "Arm.L"))
    # big paint blaster: body, drum, triple barrel
    gx, gz = D["gx"], D["gz"]
    P.append(Part(rbox((gx, -0.31, gz), (0.18, 0.34, 0.17), 0.045, 2), "Metal", "Gun", True))
    P.append(Part(cylinder((gx - 0.13, -0.29, gz + 0.015), (gx + 0.13, -0.29, gz + 0.015), 0.12,
                           14, bevel=0.025, bevel_segs=1), "Paint", "Gun", True))
    for a in (90, 210, 330):
        ox = 0.05 * math.cos(math.radians(a))
        oz = 0.05 * math.sin(math.radians(a))
        P.append(Part(cylinder((gx + ox, -0.46, gz + oz), (gx + ox, -0.74, gz + oz), 0.033, 8),
                      "Dark", "Gun", True))
    P.append(Part(cylinder((gx, -0.47, gz), (gx, -0.53, gz), 0.10, 12, bevel=0.015,
                           bevel_segs=1), "Dark", "Gun", True))
    P.append(Part(cylinder((gx, -0.68, gz), (gx, -0.71, gz), 0.095, 12, bevel=0.01,
                           bevel_segs=1), "Metal", "Gun", True))
    P.append(Part(rbox((gx, -0.16, gz - 0.11), (0.07, 0.08, 0.12), 0.02, 1, rot=(-15, 0, 0)),
                  "Dark", "Gun", True))
    return P


VARIANTS = {
    "soldier": (soldier_geo, soldier_dims),
    "heavy": (heavy_geo, heavy_dims),
    "hero": (hero_geo, hero_dims),
}


# ============================================================== rig
def bone_spec(D, s):
    S = lambda p: tuple(c * s for c in p)
    hr, hl = D["hand_R"], D["hand_L"]
    lx, lb = D["leg_x"], D["leg_bot"]
    hand_tail = (hr[0], hr[1] - 0.05, hr[2])
    mz = D["muzzle"]
    return [
        ("Root", S((0, 0, 0)), S((0, 0, 0.1)), None),
        ("Hips", S((0, 0, D["hips"])), S((0, 0, D["spine"])), "Root"),
        ("Spine", S((0, 0, D["spine"])), S((0, 0, D["neck"])), "Hips"),
        ("Head", S((0, 0, D["neck"])), S((0, 0, D["top"])), "Spine"),
        ("Arm.L", S(D["shoulder_L"]), S(hl), "Spine"),
        ("Arm.R", S(D["shoulder_R"]), S(hr), "Spine"),
        ("Hand.R", S(hr), S(hand_tail), "Arm.R"),
        ("Gun", S(D["gun_head"]), S(D["gun_tail"]), "Hand.R"),
        ("Muzzle", S(mz), S((mz[0], mz[1] - 0.06, mz[2])), "Gun"),
        ("Leg.L", S((lx, 0, D["hips"] - 0.01)), S((lx, 0, lb)), "Hips"),
        ("Leg.R", S((-lx, 0, D["hips"] - 0.01)), S((-lx, 0, lb)), "Hips"),
    ]


# ============================================================== animation
def make_anims(rig, mesh, H, heavy=False):
    """All takes. H = character height (offsets scale with it)."""
    TAU = 2 * math.pi

    # ---- Idle: 2 s loop, breathing at 1 Hz, helmet wobble at 0.5 Hz
    def idle(t, u):
        b = math.sin(TAU * t)
        w = math.sin(TAU * t / 2)
        return {
            "Root": {"s": (1 + 0.012 * b, 1 + 0.012 * b, 1 - 0.018 * b)},
            "Spine": {"r": (1.5 * b, 0, 0)},
            "Head": {"r": (2.0 * math.sin(TAU * t - 0.7), 0, 3.0 * w),
                     "s": (1 + 0.01 * math.sin(TAU * t - 0.9),) * 2 +
                          (1 - 0.012 * math.sin(TAU * t - 0.9),)},
            "Arm.R": {"r": (-2.5 * b, 0, 0)},
            "Arm.L": {"r": (-2.5 * b, 0, 0)},
            "Gun": {"r": (-1.5 * math.sin(TAU * t - 0.5), 0, 0)},
        }

    # ---- Run: 0.5 s loop, two steps; bouncy waddle with helmet lag
    def run(t, u):
        p = TAU * u
        c2 = math.cos(2 * p)          # +1 passing (high), -1 contact (low)
        swing = 40 if not heavy else 34
        pose = {
            "Root": {"s": (1 - 0.035 * c2, 1 - 0.035 * c2, 1 + 0.06 * c2)},
            "Hips": {"r": (0, 8 * math.sin(p), 7 * math.sin(p))},
            "Spine": {"r": (10, -3 * math.sin(p), -4 * math.sin(p))},
            "Head": {"r": (-6 * math.cos(2 * p - 0.9) - 4, -6 * math.sin(p - 0.9),
                           -3 * math.sin(p - 0.6))},
            "Leg.L": {"r": (swing * math.sin(p), 0, 0)},
            "Leg.R": {"r": (-swing * math.sin(p), 0, 0)},
            "Arm.R": {"r": (5 * math.cos(2 * p - 0.5), 0, 0)},
            "Arm.L": {"r": (5 * math.cos(2 * p - 0.5) - 6 * math.sin(p), 0, 0)},
            "Gun": {"r": (-3 * math.cos(2 * p - 1.0), 0, 0)},
        }
        lift = 0.045 * H * max(0.0, c2) ** 1.5   # small air time while passing
        return pose, lift

    # ---- Shoot: 0.267 s, anticipation, sharp recoil squash, damped settle
    def shoot(t, u):
        a = math.sin(math.pi * seg(t, 0.0, 0.034)) if t < 0.034 else 0.0
        if t < 0.034:
            k = 0.0
        elif t < 0.067:
            k = ease_out(seg(t, 0.034, 0.067))
        else:
            k = spring(t - 0.067, freq=2.6, damp=11.0)
        k *= 1 - smooth(seg(t, 0.20, 0.267))
        kl = 0.0 if t < 0.05 else spring(t - 0.08, 2.2, 10.0) * smooth(seg(t, 0.05, 0.09)) * \
            (1 - smooth(seg(t, 0.2, 0.267)))
        return {
            "Root": {"s": (1 + 0.09 * k, 1 + 0.09 * k, 1 - 0.13 * k + 0.03 * a)},
            "Spine": {"r": (4 * a - 9 * k, 0, 0)},
            "Head": {"r": (-10 * kl, 0, 0), "s": (1 + 0.05 * kl, 1 + 0.05 * kl, 1 - 0.07 * kl)},
            "Arm.R": {"r": (-12 * k, 0, 0)},
            "Arm.L": {"r": (-12 * k, 0, 0)},
            "Gun": {"r": (-16 * k, 0, 0), "l": (0, 0.05 * H * k, 0),
                    "s": (1 + 0.12 * k, 1 - 0.1 * k, 1 + 0.12 * k)},
        }

    # ---- Hit: 0.2 s flinch
    def hit(t, u):
        if t < 0.04:
            k = ease_out(seg(t, 0, 0.04))
        else:
            k = spring(t - 0.04, 2.8, 9.0) * (1 - smooth(seg(t, 0.12, 0.2)))
        return {
            "Root": {"l": (0, 0.07 * H * k, 0), "s": (1 + 0.1 * k, 1 + 0.1 * k, 1 - 0.12 * k)},
            "Spine": {"r": (-20 * k, 0, 7 * k)},
            "Head": {"r": (-18 * k, 8 * k, -6 * k), "s": (1 + 0.07 * k,) * 3},
            "Arm.R": {"r": (-25 * k, 0, 0)},
            "Arm.L": {"r": (-30 * k, 15 * k, 0)},
            "Leg.L": {"r": (12 * k, 0, 0)},
            "Leg.R": {"r": (-8 * k, 0, 0)},
        }

    # ---- Die: 0.6 s, anticipation, pop up with spin, fall flat on back, squash
    def die(t, u):
        pose = {}
        sl = [1.0, 1.0, 1.0]         # Root local scale: x, y (height axis), z (depth)
        lift = 0.0
        if t < 0.07:
            a = math.sin(math.pi * seg(t, 0, 0.07) * 0.5)
            sl = [1 + 0.1 * a, 1 - 0.16 * a, 1 + 0.1 * a]
        air = seg(t, 0.07, 0.40)
        if 0.07 <= t < 0.40:
            lift = 0.42 * H * 4 * air * (1 - air)
            st = math.sin(math.pi * air)
            sl = [1 - 0.06 * st, 1 + 0.12 * st, 1 - 0.06 * st]
        spin = 360 * ease_out(seg(t, 0.07, 0.42))
        tilt = -88 * smooth(seg(t, 0.12, 0.40))
        snap = t >= 0.40
        if t >= 0.40:
            k = spring(t - 0.40, 2.4, 8.0)
            sl = [1 + 0.16 * k, 1 + 0.08 * k, 1 - 0.35 * k]
            # settle into a slightly flattened pose
            settle = smooth(seg(t, 0.40, 0.6))
            sl[2] -= 0.12 * settle
            sl[0] += 0.06 * settle
        flop = smooth(seg(t, 0.12, 0.45))
        pose["Root"] = {"r": (tilt, 0, spin), "sl": tuple(sl), "l": (0, 0, lift)}
        land = spring(t - 0.40, 2.5, 7.0) if t >= 0.40 else 0.0
        pose["Head"] = {"r": (-14 * flop - 10 * land, 0, 0)}
        pose["Spine"] = {"r": (-8 * flop, 0, 0)}
        pose["Arm.L"] = {"r": (30 * flop, -40 * flop, 0)}
        pose["Arm.R"] = {"r": (25 * flop, 35 * flop, 0)}
        pose["Gun"] = {"r": (70 * flop, 0, 0)}
        pose["Leg.L"] = {"r": (35 * flop, 0, 18 * flop)}
        pose["Leg.R"] = {"r": (28 * flop, 0, -18 * flop)}
        if snap:
            return pose, 0.0
        return pose, None

    # ---- Cheer: 0.8 s loop hop with the gun raised
    def cheer(t, u):
        crouch = math.sin(math.pi * seg(u, 0.0, 0.22)) if u < 0.22 else 0.0
        v = seg(u, 0.22, 0.62)
        air = 4 * v * (1 - v) if 0.22 <= u < 0.62 else 0.0
        land = math.sin(math.pi * seg(u, 0.62, 0.9)) if 0.62 <= u < 0.9 else 0.0
        stretch = math.sin(math.pi * v) if 0.22 <= u < 0.62 else 0.0
        sz = 1 - 0.14 * crouch - 0.12 * land + 0.08 * stretch
        sxy = 1 + 0.08 * crouch + 0.07 * land - 0.04 * stretch
        w = math.sin(TAU * u)
        pose = {
            "Root": {"s": (sxy, sxy, sz)},
            "Hips": {"r": (0, 6 * w, 0)},
            "Spine": {"r": (-6 * stretch + 5 * crouch, 0, 0)},
            "Head": {"r": (-10 + 8 * crouch - 6 * stretch, 0, 4 * w)},
            "Arm.R": ({"r": (-10 * stretch + 4 * crouch, 0, 0)} if heavy else
                      {"r": (-72 - 8 * stretch + 6 * crouch, -20, -78)}),
            "Arm.L": {"aim": (0.62, -0.25 + 0.1 * w, 0.75 - 0.2 * crouch)},
            "Gun": {"r": ((-12 if heavy else -10) * stretch, 0, 0)},
            "Leg.L": {"r": (-14 * stretch, 0, 0)},
            "Leg.R": {"r": (10 * stretch, 0, 0)},
        }
        return pose, 0.2 * H * air

    specs = [("Idle", 60, idle), ("Run", 15, run), ("Shoot", 8, shoot), ("Hit", 6, hit),
             ("Die", 18, die), ("Cheer", 24, cheer)]
    for name, n, fn in specs:
        C.bake_action(rig, mesh, name, n, wrap_lift(fn))
    return {name: n for name, n, _ in specs}


def wrap_lift(fn):
    """Convert (pose, lift) results: lift is None -> plain ground fix, float -> snap + lift."""
    def g(t, u):
        r = fn(t, u)
        if not isinstance(r, tuple):
            return r, False
        pose, lift = r
        if lift is None:
            return pose, False
        return pose, ("lift", lift)
    return g


# ============================================================== previews
def render_previews(name, rig, mesh, H, nframes):
    pv = C.PREVIEW_DIR
    os.makedirs(pv, exist_ok=True)
    cam = C.setup_render()
    tgt = (0, 0, H * 0.5)
    tmp = os.path.join(C.OUT_TMP, "pv")
    os.makedirs(tmp, exist_ok=True)

    # front (3/4 front and 3/4 back)
    C.clear_take(rig)
    files = []
    for i, (el, az) in enumerate(((8, 28), (12, 155))):
        C.aim_camera(cam, tgt, el, az, H * 1.25)
        files.append(C.render(os.path.join(tmp, "f%d.png" % i), (560, 640)))
    C.contact_sheet(files, os.path.join(pv, name + "_front.png"), 2)

    # game camera: orthographic 35 deg elevation, camera behind (+Y side in Blender = Unity -Z)
    files = []
    for i, yaw in enumerate((180, 135, 90, 0)):
        C.set_yaw(rig, yaw)
        C.aim_camera(cam, tgt, 35, 180, H * 1.4)
        files.append(C.render(os.path.join(tmp, "g%d.png" % i), (400, 400)))
    C.set_yaw(rig, 0)
    C.contact_sheet(files, os.path.join(pv, name + "_gamecam.png"), 2)

    # poses
    picks = [("Idle", 0), ("Run", 4), ("Shoot", 2), ("Hit", 2), ("Die", nframes["Die"]),
             ("Cheer", 13)]
    files = []
    for i, (take, f) in enumerate(picks):
        C.set_take(rig, take, f)
        C.aim_camera(cam, (0, 0.15 * H if take == "Die" else 0, H * 0.5), 22, 38, H * 1.75)
        files.append(C.render(os.path.join(tmp, "p%d.png" % i), (400, 400)))
    C.clear_take(rig)
    C.contact_sheet(files, os.path.join(pv, name + "_poses.png"), 3)

    # team colors
    files = []
    for i, hx in enumerate(("#DA4029", "#229A8A", "#DB4793")):
        team_colors(hx)
        C.aim_camera(cam, tgt, 18, 30, H * 1.3)
        files.append(C.render(os.path.join(tmp, "t%d.png" % i), (400, 460)))
    C.contact_sheet(files, os.path.join(pv, name + "_teamcolors.png"), 3)

    # tiny: game size readability (character about 25 to 30 px tall in a 64 px tile)
    C.set_ground_color("#7F7867")
    files = []
    k = 0
    for hx in ("#DA4029", "#229A8A", "#DB4793"):
        team_colors(hx)
        for yaw in (0, 140):
            C.set_yaw(rig, yaw + 180)
            C.aim_camera(cam, tgt, 35, 180, H * 2.3)
            files.append(C.render(os.path.join(tmp, "y%d.png" % k), (64, 64)))
            k += 1
    C.set_yaw(rig, 0)
    C.contact_sheet(files, os.path.join(pv, name + "_tiny.png"), 6)
    team_colors(None)
    C.set_ground_color("#BDB08C")


def strip_debug(name, rig, H, nframes, take):
    cam = C.setup_render()
    tmp = os.path.join(C.OUT_TMP, "pv")
    os.makedirs(tmp, exist_ok=True)
    files = []
    az = float(os.environ.get("TW_AZ", "70"))
    for f in range(nframes[take] + 1):
        C.set_take(rig, take, f)
        C.aim_camera(cam, (0, 0, H * 0.55), 10, az, H * 1.6)
        files.append(C.render(os.path.join(tmp, "s%02d.png" % f), (200, 200)))
    C.contact_sheet(files, os.path.join(C.OUT_TMP, "strip_%s_%s.png" % (name, take)), 8)


def team_colors(hx):
    if hx is None:
        for n in ("TeamColor", "TeamColorDark", "Paint"):
            C.set_mat_color(n, C.MAT_COLORS[n])
        return
    C.set_mat_color("TeamColor", hx)
    C.set_mat_color("TeamColorDark", C.darker(hx, 0.68))
    C.set_mat_color("Paint", C.darker(hx, 1.25))


# ============================================================== main
def build(variant, do_export=True):
    geo_fn, dims_fn = VARIANTS[variant]
    D = dims_fn()
    name = D["name"]
    C.reset_scene()
    parts = geo_fn()
    mesh = C.join_parts(parts, name + "_Mesh")
    # scale to exact target height, origin at feet
    zs = [v.co.z for v in mesh.data.vertices]
    zmin, zmax = min(zs), max(zs)
    s = D["height"] / (zmax - zmin)
    mesh.data.transform(Matrix.Translation((0, 0, -zmin * s)) @ Matrix.Scale(s, 4))
    rig = C.build_rig(name + "_Rig", [(n, Vector(h) + Vector((0, 0, -zmin * s)),
                                       Vector(t) + Vector((0, 0, -zmin * s)), p)
                                      for n, h, t, p in bone_spec(D, s)])
    C.skin(mesh, rig)
    H = D["height"]
    nframes = make_anims(rig, mesh, H, heavy=(variant == "heavy"))
    tris = C.tri_count(mesh)
    print("BUILD %s tris=%d scale=%.4f mats=%s" % (name, tris, s,
                                                    [m.name for m in mesh.data.materials]))
    if os.environ.get("TW_STRIP"):
        strip_debug(name, rig, H, nframes, os.environ["TW_STRIP"])
        return
    render_previews(name, rig, mesh, H, nframes)
    if do_export:
        tmp = C.export_fbx(rig, [mesh], name + ".fbx")
        print(C.verify_fbx(tmp))
        dst = C.move_into_assets(tmp)
        print("EXPORTED", dst)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    do_export = "--no-export" not in argv
    vs = [a for a in argv if a in VARIANTS] or list(VARIANTS)
    for v in vs:
        build(v, do_export)
