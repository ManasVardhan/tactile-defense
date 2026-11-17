"""Build the enemy squad flag: a pole with a waving team-colored banner and an original
cream skull-badge emblem. Output: Assets/Art/Models/Characters/Flag.fbx, Docs/Previews/Flag.png

Objects: `Pole` (Metal, origin at the pole base) and `Cloth` (TeamColor + Accent emblem +
Dark details, origin at the top pole attach point; UV.x runs 0 at the pole to 1 at the free
edge so a Unity vertex shader can scale a wave by it). Height 1.6, origin at the pole base.
Run: blender -b -P Tools/blender/build_flag.py
"""
import bpy
import bmesh
import math
import os
import sys
import importlib

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import tw_common as C
importlib.reload(C)
from tw_common import Part, cylinder, ellipsoid, rbox
from mathutils import Vector, Matrix

POLE_H = 1.6
CLOTH_W, CLOTH_H = 0.62, 0.44
CLOTH_TOP = 1.52
POLE_R = 0.024
X0 = POLE_R * 0.6


def wave(x, z):
    """Static wave baked into the cloth (Y displacement), zero at the pole."""
    u = x / CLOTH_W
    return 0.045 * u * math.sin(u * 2.4 * math.pi + 0.4) + 0.006 * u * math.sin(z * 9.0)


def cloth_part():
    nx, nz = 10, 5
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    grid = []
    for i in range(nx + 1):
        col = []
        for j in range(nz + 1):
            x = X0 + CLOTH_W * i / nx
            z = CLOTH_TOP - CLOTH_H * j / nz
            # slight droop toward the free edge
            z -= 0.04 * (i / nx) ** 2
            col.append(bm.verts.new((x, wave(x, z), z)))
        grid.append(col)
    for i in range(nx):
        for j in range(nz):
            f = bm.faces.new((grid[i][j], grid[i][j + 1], grid[i + 1][j + 1], grid[i + 1][j]))
            for lp in f.loops:
                lp[uv].uv = ((lp.vert.co.x - X0) / CLOTH_W, (lp.vert.co.z - (CLOTH_TOP - CLOTH_H))
                             / CLOTH_H)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # thickness
    ret = bmesh.ops.solidify(bm, geom=list(bm.faces), thickness=0.016)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return Part(bm, "TeamColor", "Cloth")


def emblem_parts():
    """Original skull badge: round cream head with a jaw block, dark eye sockets and a small
    nose notch, on both faces of the cloth. Pieces are conformed to the cloth wave."""
    parts = []
    cx = X0 + CLOTH_W * 0.48
    cz = CLOTH_TOP - CLOTH_H * 0.5 - 0.02
    for side in (-1, 1):
        def piece(bm, dx, dz, lift, mat):
            # bm is built facing -Y with depth along -Y; conform to the wave surface
            for v in bm.verts:
                x = cx + dx + (v.co.x if side < 0 else -v.co.x)
                z = cz + dz + v.co.z
                depth = -v.co.y + lift
                v.co = Vector((x, wave(x, z) + side * (0.02 + depth), z))
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
            parts.append(Part(bm, mat, "Cloth"))

        piece(cylinder((0, 0.0, 0), (0, -0.012, 0), 0.105, 16), 0, 0.018, 0.0, "Accent")
        piece(rbox((0, -0.006, 0), (0.11, 0.012, 0.06), 0.012, 1), 0, -0.085, 0.0, "Accent")
        for ex in (-0.042, 0.042):
            piece(cylinder((0, 0, 0), (0, -0.008, 0), 0.03, 10), ex, 0.02, 0.013, "Dark")
        n = cylinder((0, 0, 0), (0, -0.008, 0), 0.016, 3)
        C.xform(n, Matrix.Rotation(math.radians(-90), 4, 'Y'))
        piece(n, 0, -0.035, 0.013, "Dark")
        for tx in (-0.03, 0.0, 0.03):
            piece(rbox((0, 0, 0), (0.008, 0.008, 0.035), 0.0, 0), tx, -0.09, 0.014, "Dark")
    return parts


def pole_parts():
    P = []
    P.append(Part(cylinder((0, 0, 0.02), (0, 0, POLE_H - 0.05), POLE_R, 10), "Metal", "Pole",
                  True))
    P.append(Part(ellipsoid((0, 0, POLE_H - 0.035), (0.045, 0.045, 0.04), 10, 6), "Accent",
                  "Pole"))
    P.append(Part(cylinder((0, 0, 0.0), (0, 0, 0.05), 0.07, 12, r1=0.05, bevel=0.012,
                           bevel_segs=1), "Dark", "Pole", True))
    # ring clamps that hold the cloth
    for z in (CLOTH_TOP - 0.01, CLOTH_TOP - CLOTH_H + 0.03):
        P.append(Part(cylinder((0, 0, z - 0.015), (0, 0, z + 0.015), POLE_R * 1.6, 10),
                      "Dark", "Pole", True))
    return P


def build():
    C.reset_scene()
    pole = C.join_parts(pole_parts(), "Pole")
    cloth = C.join_parts([cloth_part()] + emblem_parts(), "Cloth")
    for o in (pole, cloth):
        o.vertex_groups.clear()
    # cloth origin at the top attach point on the pole
    pivot = Vector((0, 0, CLOTH_TOP))
    cloth.data.transform(Matrix.Translation(-pivot))
    cloth.location = pivot
    tris = C.tri_count(pole) + C.tri_count(cloth)
    print("BUILD Flag tris=%d (pole %d, cloth %d)" % (tris, C.tri_count(pole),
                                                      C.tri_count(cloth)))
    # previews: three team colors, 3/4 view, plus one game camera view
    cam = C.setup_render()
    tmp = os.path.join(C.OUT_TMP, "pv")
    os.makedirs(tmp, exist_ok=True)
    files = []
    for i, hx in enumerate(("#229A8A", "#DA4029", "#DB4793")):
        C.set_mat_color("TeamColor", hx)
        C.set_mat_color("TeamColorDark", C.darker(hx, 0.68))
        if i < 2:
            C.aim_camera(cam, (0.25, 0, 0.85), 12, 25 if i == 0 else 200, 1.9)
        else:
            C.aim_camera(cam, (0.25, 0, 0.85), 35, 0, 1.9)
        files.append(C.render(os.path.join(tmp, "flag%d.png" % i), (400, 480)))
    C.contact_sheet(files, os.path.join(C.PREVIEW_DIR, "Flag.png"), 3)
    for n in ("TeamColor", "TeamColorDark"):
        C.set_mat_color(n, C.MAT_COLORS[n])
    tmpf = C.export_static_fbx([pole, cloth], "Flag.fbx")
    dst = C.move_into_assets(tmpf)
    print("EXPORTED", dst)


if __name__ == "__main__":
    build()
