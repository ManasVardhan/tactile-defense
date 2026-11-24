"""Shared helpers for Tactile Wars character scripts (Blender 5.3, headless).

Conventions (Blender side): Z up, character faces -Y, origin at feet, meters.
FBX export: axis_forward='-Z', axis_up='Y', FBX_SCALE_ALL, bake_space_transform=True.
Result in Unity: Y up, character faces +Z, scale 1.
"""
import bpy
import bmesh
import math
import os
import numpy as np
from mathutils import Vector, Matrix, Euler, Quaternion

ROOT = "/Users/manasvardhan/TactileWars"
OUT_TMP = os.path.join(ROOT, "Tools/blender/out")
CHAR_DIR = os.path.join(ROOT, "Assets/Art/Models/Characters")
PREVIEW_DIR = os.path.join(ROOT, "Docs/Previews")
FPS = 30


def hexcol(h, a=1.0):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    # sRGB to linear
    lin = [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]
    return (lin[0], lin[1], lin[2], a)


def darker(h, f):
    h = h.lstrip("#")
    c = [int(int(h[i:i + 2], 16) * f) for i in (0, 2, 4)]
    return "#%02X%02X%02X" % tuple(min(255, x) for x in c)


# Authoring colors (Unity remaps by slot name). TeamColor authored as mid blue per contract.
MAT_COLORS = {
    "TeamColor": "#3D74D6",
    "Helmet": "#3D74D6",
    "TeamColorDark": "#284C99",
    "Skin": "#F7F1E3",
    "Dark": "#262A3B",
    "Metal": "#9AA2AE",
    "Accent": "#FFF8E8",
    "Paint": "#5A9BFF",
    "Wood": "#9A6B43",
}
MAT_ROUGH = {"Metal": 0.38, "Dark": 0.5, "Paint": 0.3, "Skin": 0.55}


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.fps = FPS
    sc.unit_settings.system = 'METRIC'
    sc.unit_settings.scale_length = 1.0


def get_mat(name):
    m = bpy.data.materials.get(name)
    if m:
        return m
    m = bpy.data.materials.new(name)
    if m.node_tree is None:
        m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    col = hexcol(MAT_COLORS.get(name, "#FF00FF"))
    bsdf.inputs["Base Color"].default_value = col
    bsdf.inputs["Roughness"].default_value = MAT_ROUGH.get(name, 0.6)
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.35
    m.diffuse_color = col
    return m


def set_mat_color(name, hexc):
    m = get_mat(name)
    col = hexcol(hexc)
    m.node_tree.nodes.get("Principled BSDF").inputs["Base Color"].default_value = col
    m.diffuse_color = col


# ---------------------------------------------------------------- geometry
class Part:
    """A bmesh piece with one material and a bone weight spec."""

    def __init__(self, bm, mat, bone, hard=False):
        self.bm = bm
        self.mat = mat          # default material name for faces with index 0
        self.bone = bone        # str, or callable(co)->dict(bone:weight)
        self.hard = hard        # beveled hard surface: weighted normals
        self.extra_mats = []    # additional material names (face index 1..)


def _bm():
    return bmesh.new()


def xform(bm, mat):
    bmesh.ops.transform(bm, matrix=mat, verts=bm.verts)


def align_matrix(p0, p1):
    """Matrix mapping local +Z to direction p0->p1, placed at p0."""
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    q = Vector((0, 0, 1)).rotation_difference(d.normalized())
    return Matrix.Translation(p0) @ q.to_matrix().to_4x4()


def ellipsoid(center, radii, seg=16, rings=8, rot=(0, 0, 0)):
    bm = _bm()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=1.0)
    xform(bm, Matrix.Diagonal((radii[0], radii[1], radii[2], 1.0)))
    xform(bm, Euler([math.radians(a) for a in rot], 'XYZ').to_matrix().to_4x4())
    xform(bm, Matrix.Translation(Vector(center)))
    return bm


def capsule(p0, p1, r, seg=10, rings=6, r1=None):
    """Capsule from p0 to p1 with radius r (r1 = radius at p1 end, optional)."""
    r1 = r if r1 is None else r1
    bm = _bm()
    if rings % 2:
        rings += 1
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=1.0)
    L = (Vector(p1) - Vector(p0)).length
    for v in bm.verts:
        top = v.co.z > 1e-4
        rr = r1 if top else r
        v.co = Vector((v.co.x * rr, v.co.y * rr, v.co.z * rr + (L if top else 0.0)))
        if abs(v.co.z) < 1e-4 and not top:
            pass
    xform(bm, align_matrix(p0, p1))
    return bm


def cylinder(p0, p1, r, seg=12, bevel=0.0, bevel_segs=2, r1=None):
    bm = _bm()
    L = (Vector(p1) - Vector(p0)).length
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg,
                          radius1=r, radius2=(r if r1 is None else r1), depth=L)
    xform(bm, Matrix.Translation((0, 0, L / 2)))
    if bevel > 0:
        rim = [e for e in bm.edges if len(e.link_faces) == 2 and
               abs(e.link_faces[0].normal.dot(e.link_faces[1].normal)) < 0.5]
        bmesh.ops.bevel(bm, geom=rim, offset=bevel, offset_type='OFFSET', segments=bevel_segs,
                        profile=0.5, affect='EDGES', clamp_overlap=True)
    xform(bm, align_matrix(p0, p1))
    return bm


def rbox(center, size, bevel=0.01, segs=2, rot=(0, 0, 0), taper=None):
    """Rounded box. taper=(sx, sy) scales the +Z face for wedge shapes."""
    bm = _bm()
    bmesh.ops.create_cube(bm, size=1.0)
    if taper:
        for v in bm.verts:
            if v.co.z > 0:
                v.co.x *= taper[0]
                v.co.y *= taper[1]
    xform(bm, Matrix.Diagonal((size[0], size[1], size[2], 1.0)))
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, offset_type='OFFSET',
                        segments=segs, profile=0.5, affect='EDGES', clamp_overlap=True)
    xform(bm, Euler([math.radians(a) for a in rot], 'XYZ').to_matrix().to_4x4())
    xform(bm, Matrix.Translation(Vector(center)))
    return bm


def torus(center, R, r, seg=20, rseg=6, rot=(0, 0, 0), scale_minor_z=1.0):
    bm = _bm()
    verts = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        ring = []
        for j in range(rseg):
            b = 2 * math.pi * j / rseg
            x = (R + r * math.cos(b)) * math.cos(a)
            y = (R + r * math.cos(b)) * math.sin(a)
            z = r * math.sin(b) * scale_minor_z
            ring.append(bm.verts.new((x, y, z)))
        verts.append(ring)
    for i in range(seg):
        for j in range(rseg):
            a = verts[i][j]
            b = verts[(i + 1) % seg][j]
            c = verts[(i + 1) % seg][(j + 1) % rseg]
            d = verts[i][(j + 1) % rseg]
            bm.faces.new((a, b, c, d))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    xform(bm, Euler([math.radians(a) for a in rot], 'XYZ').to_matrix().to_4x4())
    xform(bm, Matrix.Translation(Vector(center)))
    return bm


def surface_point(center, radii, x, z, rot=(0, 0, 0)):
    """Front (-Y) surface point of an axis aligned ellipsoid at given world x, z."""
    cx, cy, cz = center
    a, b, c = radii
    u = (x - cx) / a
    w = (z - cz) / c
    s = max(0.0, 1 - u * u - w * w)
    y = cy - b * math.sqrt(s)
    p = Vector((x, y, z))
    n = Vector(((x - cx) / (a * a), (y - cy) / (b * b), (z - cz) / (c * c))).normalized()
    return p, n


def place_on(bm, p, n, sink=0.0):
    """Orient a bm built around origin (thin along local Y) onto surface point p, normal n."""
    q = Vector((0, -1, 0)).rotation_difference(n)
    xform(bm, q.to_matrix().to_4x4())
    xform(bm, Matrix.Translation(Vector(p) - n * sink))
    return bm


# ---------------------------------------------------------------- objects
def part_to_object(part, name):
    me = bpy.data.meshes.new(name)
    part.bm.normal_update()
    part.bm.to_mesh(me)
    part.bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    ob.data.materials.append(get_mat(part.mat))
    for m in part.extra_mats:
        ob.data.materials.append(get_mat(m))
    for p in me.polygons:
        p.use_smooth = True
    # weights
    groups = {}
    for v in me.vertices:
        w = part.bone(v.co) if callable(part.bone) else {part.bone: 1.0}
        for b, val in w.items():
            if val <= 0:
                continue
            if b not in groups:
                groups[b] = ob.vertex_groups.new(name=b)
            groups[b].add([v.index], val, 'REPLACE')
    if part.hard:
        mod = ob.modifiers.new("WN", 'WEIGHTED_NORMAL')
        mod.mode = 'FACE_AREA'
        mod.weight = 80
        mod.keep_sharp = True
        bpy.context.view_layer.objects.active = ob
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return ob


def join_parts(parts, name):
    obs = [part_to_object(p, "%s_%d" % (name, i)) for i, p in enumerate(parts)]
    if os.environ.get("TW_DEBUG"):
        for o in obs:
            print("PART", o.name, o.data.materials[0].name, tri_count(o))
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = name
    ob.data.name = name
    # merge duplicate material slots (join keeps unique materials already)
    return ob


def tri_count(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def blend_weights(b_lo, b_hi, z0, z1):
    """Vertex weight blend from bone b_lo (below z0) to b_hi (above z1)."""
    def f(co):
        t = min(1.0, max(0.0, (co.z - z0) / max(1e-6, z1 - z0)))
        t = t * t * (3 - 2 * t)
        return {b_lo: 1 - t, b_hi: t}
    return f


# ---------------------------------------------------------------- rig
RIG_ROT_X = math.radians(90)  # cancels the FBX -90 X axis conversion on the armature node


def build_rig(name, bones):
    """bones: list of (name, head, tail, parent or None) in world (Z up) coordinates.

    The armature object is rotated +90 deg about X so that, after the exporter applies its
    axis conversion (which bake_space_transform does not bake into armatures), the FBX
    armature node and Root bone end up with identity rotation (Unity friendly)."""
    arm = bpy.data.armatures.new(name)
    ob = bpy.data.objects.new(name, arm)
    bpy.context.scene.collection.objects.link(ob)
    ob.rotation_euler = (RIG_ROT_X, 0, 0)
    bpy.context.view_layer.update()
    Ai = ob.matrix_world.inverted()
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode='EDIT')
    for bn, h, t, par in bones:
        eb = arm.edit_bones.new(bn)
        eb.head = Ai @ Vector(h)
        eb.tail = Ai @ Vector(t)
        eb.roll = 0.0
        if par:
            eb.parent = arm.edit_bones[par]
            eb.use_connect = False
    bpy.ops.object.mode_set(mode='OBJECT')
    for pb in ob.pose.bones:
        pb.rotation_mode = 'QUATERNION'
    return ob


def skin(mesh_ob, rig_ob):
    mesh_ob.parent = rig_ob
    mesh_ob.matrix_parent_inverse = rig_ob.matrix_world.inverted()
    mod = mesh_ob.modifiers.new("Armature", 'ARMATURE')
    mod.object = rig_ob


# ---------------------------------------------------------------- animation
def _rest3(rig, bn):
    return rig.data.bones[bn].matrix_local.to_3x3()


def set_yaw(rig, deg):
    rig.rotation_euler = (RIG_ROT_X, 0, math.radians(deg))


def apply_pose(rig, pose):
    """pose: {bone: {'r':(deg x,y,z euler, Z-up character space), 'l':(offset), 's':(per-axis
    scale), 'aim':(direction), 'sl':(bone local scale)}}. Character space = the rest frame:
    Z up, facing -Y (the rig object's own +90 X rotation is compensated here)."""
    A = Euler((RIG_ROT_X, 0, 0)).to_matrix()
    Ai = A.inverted()
    for pb in rig.pose.bones:
        pb.location = (0, 0, 0)
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.scale = (1, 1, 1)
    for bn, d in pose.items():
        pb = rig.pose.bones[bn]
        R = A @ _rest3(rig, bn)      # bone rest orientation in character space
        Ri = R.inverted()
        if 'r' in d:
            W = Euler([math.radians(a) for a in d['r']], 'XYZ').to_matrix()
            pb.rotation_quaternion = (Ri @ W @ R).to_quaternion()
        if 'aim' in d:  # rotate so the bone points along a world direction (rest frame)
            b = rig.data.bones[bn]
            rest_dir = (A @ (b.tail_local - b.head_local)).normalized()
            W = rest_dir.rotation_difference(Vector(d['aim']).normalized()).to_matrix()
            pb.rotation_quaternion = (Ri @ W @ R).to_quaternion()
        if 'l' in d:
            pb.location = Ri @ Vector(d['l'])
        if 's' in d:
            s = d['s']
            loc_s = []
            for i in range(3):
                axis = R.col[i]
                k = max(range(3), key=lambda j: abs(axis[j]))
                loc_s.append(s[k])
            pb.scale = loc_s
        if 'sl' in d:  # local scale override
            pb.scale = d['sl']


def mesh_min_z(mesh_ob):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = mesh_ob.evaluated_get(dg)
    me = ev.to_mesh()
    co = np.empty(len(me.vertices) * 3, dtype=np.float32)
    me.vertices.foreach_get("co", co)
    ev.to_mesh_clear()
    co = co.reshape(-1, 3)
    M = np.array(mesh_ob.matrix_world)
    z = co @ M[2, :3] + M[2, 3]
    return float(z.min())


def bake_action(rig, mesh_ob, name, nframes, fn, ground_fix=True):
    """fn(t, u) -> (pose dict, snap_flag). t seconds, u in [0,1]. Keys every frame 0..nframes."""
    sc = bpy.context.scene
    poses = []
    for f in range(nframes + 1):
        t = f / FPS
        res = fn(t, f / nframes)
        pose, snap = res if isinstance(res, tuple) else (res, False)
        if ground_fix:
            apply_pose(rig, pose)
            bpy.context.view_layer.update()
            mz = mesh_min_z(mesh_ob)
            dz = 0.0
            if isinstance(snap, tuple):      # ("lift", h): rest on ground, then raise by h
                dz = -mz + snap[1]
            elif snap or mz < -0.002:
                dz = -mz
            if dz != 0.0:
                r = dict(pose.get('Root', {}))
                l = list(r.get('l', (0, 0, 0)))
                l[2] += dz
                r['l'] = tuple(l)
                pose = dict(pose)
                pose['Root'] = r
        poses.append(pose)
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    rig.animation_data_create()
    rig.animation_data.action = act
    for f, pose in enumerate(poses):
        apply_pose(rig, pose)
        for pb in rig.pose.bones:
            pb.keyframe_insert("location", frame=f, group=pb.name)
            pb.keyframe_insert("rotation_quaternion", frame=f, group=pb.name)
            pb.keyframe_insert("scale", frame=f, group=pb.name)
    for layer in act.layers:
        for strip in layer.strips:
            for cb in strip.channelbags:
                for fc in cb.fcurves:
                    for kp in fc.keyframe_points:
                        kp.interpolation = 'LINEAR'
    rig.animation_data.action = None
    tr = rig.animation_data.nla_tracks.new()
    tr.name = name
    st = tr.strips.new(name, 0, act)
    st.name = name
    tr.mute = True
    return act


def set_take(rig, name, frame):
    """Show a given take at a frame for preview rendering (uses the action directly)."""
    for tr in rig.animation_data.nla_tracks:
        tr.mute = True
    rig.animation_data.action = bpy.data.actions[name]
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()


def clear_take(rig):
    rig.animation_data.action = None
    for pb in rig.pose.bones:
        pb.location = (0, 0, 0)
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.scale = (1, 1, 1)
    bpy.context.view_layer.update()


# easing
def smooth(t):
    t = min(1, max(0, t))
    return t * t * (3 - 2 * t)


def ease_out(t):
    t = min(1, max(0, t))
    return 1 - (1 - t) ** 3


def ease_in(t):
    t = min(1, max(0, t))
    return t ** 3


def spring(t, freq=3.0, damp=6.0):
    """Damped oscillation starting at 1 when t=0, tending to 0."""
    if t <= 0:
        return 1.0
    return math.exp(-damp * t) * math.cos(2 * math.pi * freq * t)


def lerp(a, b, t):
    return a + (b - a) * t


def seg(t, t0, t1):
    return min(1.0, max(0.0, (t - t0) / (t1 - t0)))


# ---------------------------------------------------------------- export / verify
def export_fbx(rig, mesh_obs, fname):
    os.makedirs(OUT_TMP, exist_ok=True)
    tmp = os.path.join(OUT_TMP, fname)
    clear_take(rig)
    for tr in rig.animation_data.nla_tracks:
        tr.mute = False
    bpy.context.scene.frame_set(0)
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    for m in mesh_obs:
        m.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(
        filepath=tmp, use_selection=True, object_types={'ARMATURE', 'MESH', 'EMPTY'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z', axis_up='Y', bake_space_transform=True,
        use_mesh_modifiers=False, mesh_smooth_type='FACE', use_tspace=False,
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
        use_armature_deform_only=False, armature_nodetype='NULL',
        bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=True,
        bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True,
        bake_anim_step=1.0, bake_anim_simplify_factor=0.0, embed_textures=False,
        path_mode='AUTO')
    for tr in rig.animation_data.nla_tracks:
        tr.mute = True
    return tmp


def export_static_fbx(obs, fname):
    os.makedirs(OUT_TMP, exist_ok=True)
    tmp = os.path.join(OUT_TMP, fname)
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]
    bpy.ops.export_scene.fbx(
        filepath=tmp, use_selection=True, object_types={'MESH', 'EMPTY'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z', axis_up='Y', bake_space_transform=True,
        mesh_smooth_type='FACE', bake_anim=False, add_leaf_bones=False)
    return tmp


def move_into_assets(tmp):
    os.makedirs(CHAR_DIR, exist_ok=True)
    dst = os.path.join(CHAR_DIR, os.path.basename(tmp))
    os.replace(tmp, dst)  # single atomic rename (same volume)
    return dst


def verify_fbx(path, expect_takes=None):
    """Re-import into an empty scene twice: with contract axes (round trip) and with
    identity axes (raw FBX space, Y up / +Z forward expected). Returns a report string."""
    lines = []
    for label, fwd, up in (("contract", '-Z', 'Y'), ("raw", 'Y', 'Z')):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=path, use_manual_orientation=True,
                                 axis_forward=fwd, axis_up=up,
                                 automatic_bone_orientation=False)
        meshes = [o for o in bpy.data.objects if o.type == 'MESH']
        arms = [o for o in bpy.data.objects if o.type == 'ARMATURE']
        bpy.context.view_layer.update()
        allco = []
        for m in meshes:
            for v in m.data.vertices:
                allco.append(m.matrix_world @ v.co)
        mn = Vector([min(c[i] for c in allco) for i in range(3)])
        mx = Vector([max(c[i] for c in allco) for i in range(3)])
        lines.append("[%s] bbox min %s max %s" % (label, tuple(round(x, 3) for x in mn),
                                                 tuple(round(x, 3) for x in mx)))
        for o in bpy.data.objects:
            lines.append("[%s]   obj %s type %s rot %s scale %s" % (
                label, o.name, o.type, tuple(round(math.degrees(a), 1) for a in o.rotation_euler),
                tuple(round(s, 3) for s in o.scale)))
        if label == "contract":
            import re
            raw = open(path, 'rb').read()
            stacks = sorted(set(x.decode() for x in
                                re.findall(rb'([A-Za-z_|.]+)\x00\x01AnimStack', raw)))
            lines.append("[file]   FBX take names %s" % stacks)
            for m in meshes:
                lines.append("[contract]   mesh %s tris %d mats %s groups %s" % (
                    m.name, tri_count(m), [s.material.name for s in m.material_slots],
                    [g.name for g in m.vertex_groups]))
            for a in arms:
                lines.append("[contract]   bones %s" % [b.name for b in a.data.bones])
            for a in bpy.data.actions:
                fr = a.frame_range
                msg = "[contract]   take %s frames %.0f-%.0f (%.3f s)" % (
                    a.name, fr[0], fr[1], (fr[1] - fr[0]) / FPS)
                if arms and meshes:
                    rig = arms[0]
                    rig.animation_data_create()
                    rig.animation_data.action = a
                    try:
                        if a.slots and rig.animation_data.action_slot is None:
                            rig.animation_data.action_slot = a.slots[0]
                    except Exception:
                        pass
                    zs, tops = [], []
                    for f in range(int(fr[0]), int(fr[1]) + 1):
                        bpy.context.scene.frame_set(f)
                        zs.append(mesh_min_z(meshes[0]))
                    msg += " playback min z %.3f..%.3f" % (min(zs), max(zs))
                lines.append(msg)
        if label == "raw":
            # locate gun muzzle direction: muzzle bone head relative to origin
            for a in arms:
                if "Muzzle" in a.data.bones:
                    h = a.matrix_world @ a.data.bones["Muzzle"].head_local
                    lines.append("[raw]   Muzzle bone head at %s (expect +Z forward, +Y up)" %
                                 (tuple(round(x, 3) for x in h),))
    return "\n".join(lines)


# ---------------------------------------------------------------- rendering
def setup_render(res=(800, 800), ground_hex="#BDB08C", samples=24):
    sc = bpy.context.scene
    for eng in ('BLENDER_EEVEE', 'BLENDER_EEVEE_NEXT'):
        try:
            sc.render.engine = eng
            break
        except TypeError:
            continue
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = False
    try:
        sc.eevee.taa_render_samples = samples
    except Exception:
        pass
    sc.view_settings.view_transform = 'Standard'
    sc.view_settings.look = 'None'
    world = bpy.data.worlds.new("W")
    sc.world = world
    if world.node_tree is None:
        world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs[0].default_value = (0.62, 0.64, 0.68, 1)
    bg.inputs[1].default_value = 0.9
    # sun, soft
    sun_d = bpy.data.lights.new("Sun", 'SUN')
    sun_d.energy = 2.6
    sun_d.angle = math.radians(35)
    sun = bpy.data.objects.new("Sun", sun_d)
    sun.rotation_euler = (math.radians(40), 0, math.radians(-30))
    sc.collection.objects.link(sun)
    fill_d = bpy.data.lights.new("Fill", 'SUN')
    fill_d.energy = 0.6
    fill_d.angle = math.radians(60)
    fill = bpy.data.objects.new("Fill", fill_d)
    fill.rotation_euler = (math.radians(70), 0, math.radians(150))
    sc.collection.objects.link(fill)
    try:
        fill_d.use_shadow = False
    except Exception:
        pass
    # ground
    bpy.ops.mesh.primitive_plane_add(size=40, location=(0, 0, 0))
    g = bpy.context.active_object
    g.name = "PreviewGround"
    gm = bpy.data.materials.new("PreviewGroundMat")
    if gm.node_tree is None:
        gm.use_nodes = True
    b = gm.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = hexcol(ground_hex)
    b.inputs["Roughness"].default_value = 0.95
    g.data.materials.append(gm)
    cam_d = bpy.data.cameras.new("Cam")
    cam_d.type = 'ORTHO'
    cam = bpy.data.objects.new("Cam", cam_d)
    sc.collection.objects.link(cam)
    sc.camera = cam
    return cam


def set_ground_color(hexc):
    gm = bpy.data.materials["PreviewGroundMat"]
    gm.node_tree.nodes.get("Principled BSDF").inputs["Base Color"].default_value = hexcol(hexc)


def aim_camera(cam, target, elev_deg, azim_deg, ortho, dist=8.0):
    """azim 0 = camera in front of the character (on -Y side, looking +Y)."""
    e = math.radians(elev_deg)
    a = math.radians(azim_deg)
    d = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
    cam.location = Vector(target) + d * dist
    cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    cam.data.ortho_scale = ortho
    cam.data.clip_end = 50


def render(path, res=None):
    sc = bpy.context.scene
    if res:
        sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.filepath = path
    sc.render.image_settings.file_format = 'PNG'
    bpy.ops.render.render(write_still=True)
    return path


def contact_sheet(paths, out, cols, pad=0):
    imgs = [bpy.data.images.load(p, check_existing=False) for p in paths]
    w, h = imgs[0].size
    rows = math.ceil(len(imgs) / cols)
    W, H = cols * w, rows * h
    canvas = np.ones((H, W, 4), dtype=np.float32)
    for i, im in enumerate(imgs):
        px = np.empty(w * h * 4, dtype=np.float32)
        im.pixels.foreach_get(px)
        px = px.reshape(h, w, 4)
        r, c = divmod(i, cols)
        y0 = H - (r + 1) * h  # images are bottom-up
        canvas[y0:y0 + h, c * w:(c + 1) * w] = px
    ob = bpy.data.images.new("sheet", W, H, alpha=True)
    ob.pixels.foreach_set(canvas.ravel())
    ob.filepath_raw = out
    ob.file_format = 'PNG'
    ob.save()
    for im in imgs:
        bpy.data.images.remove(im)
    bpy.data.images.remove(ob)
    for p in paths:
        try:
            os.remove(p)
        except OSError:
            pass
    return out
