"""Shared helpers for Tactile Wars structure and environment props.

Used by build_environment.py, build_structures.py and build_lineups.py.
Run builders headless: blender -b -P build_environment.py -- [AssetName ...]

Conventions (see Docs/ARCHITECTURE_CONTRACT.md):
- Blender Z up, model front faces Blender -Y. FBX export with axis_forward='-Z',
  axis_up='Y', bake_space_transform=True gives Unity +Z forward, Y up, scale 1.
- Origin at ground contact center. Flat colored materials named by contract slots.
"""
import bpy
import math
import os
import random
import shutil
import json
from mathutils import Vector, Matrix, Euler

ROOT = "/Users/manasvardhan/TactileWars"
OUT = os.path.join(ROOT, "Tools/blender/out")
PREV = os.path.join(ROOT, "Docs/Previews")
ASSET_DIRS = {
    "Structures": os.path.join(ROOT, "Assets/Art/Models/Structures"),
    "Environment": os.path.join(ROOT, "Assets/Art/Models/Environment"),
}

# Muted, warm, low saturation preview palette. Team colors are the only saturated hues.
PALETTE = {
    "TeamColor": "#3D7FD3",
    "TeamColorDark": "#2A5596",
    "Dark": "#33384A",
    "Metal": "#A9AEB2",
    "Accent": "#F1EADB",
    "Paint": "#5DB8F5",
    "Wood": "#A77A4E",
    "Trunk": "#6E5038",
    "Leaf": "#3F6A58",
    "LeafLight": "#6B8C62",
    "Rock": "#9B958A",
    "Sand": "#B5A174",
    "Glass": "#B4D8DE",
    "Canvas": "#E6DAB9",
    "Stone": "#CDBE9B",
}
GROUND = "#C2B48C"
FIGURE_COLORS = {"FigTeam": "#D9442E", "FigFace": "#F4F0E8", "FigDark": "#2B2F3A"}


def srgb_to_lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def hex_rgba(h):
    h = h.lstrip("#")
    return tuple(srgb_to_lin(int(h[i:i + 2], 16) / 255.0) for i in (0, 2, 4)) + (1.0,)


# ---------------------------------------------------------------- scene

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.objects):
        for d in list(coll):
            coll.remove(d)


def M(name):
    """Get or create a flat material by contract slot name."""
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    col = PALETTE.get(name) or FIGURE_COLORS.get(name) or GROUND
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    rgba = hex_rgba(col)
    bsdf.inputs["Base Color"].default_value = rgba
    bsdf.inputs["Roughness"].default_value = 0.8
    try:
        bsdf.inputs["Specular IOR Level"].default_value = 0.25
    except KeyError:
        pass
    if name == "Paint":
        try:
            bsdf.inputs["Emission Color"].default_value = rgba
            bsdf.inputs["Emission Strength"].default_value = 0.35
        except KeyError:
            pass
    mat.diffuse_color = rgba
    return mat


def _active():
    return bpy.context.view_layer.objects.active


def _apply_mods(o):
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(o.evaluated_get(dg))
    old = o.data
    o.modifiers.clear()
    o.data = me
    if old.users == 0:
        bpy.data.meshes.remove(old)


def _finish(o, mat, loc, rot, bevel, bseg, angle, scale=None):
    if scale:
        o.data.transform(Matrix.Diagonal(Vector(scale)).to_4x4())
    if bevel and bevel > 0:
        m = o.modifiers.new("Bevel", "BEVEL")
        m.width = bevel
        m.segments = bseg
        m.limit_method = "ANGLE"
        m.angle_limit = math.radians(angle)
        m.use_clamp_overlap = True
        _apply_mods(o)
    o.data.materials.clear()
    o.data.materials.append(M(mat))
    o.location = loc
    o.rotation_euler = Euler([math.radians(a) for a in rot])
    return o


def cyl(r, h, mat, loc=(0, 0, 0), rot=(0, 0, 0), seg=12, r2=None, bevel=0.03,
        bseg=2, angle=50, scale=None):
    """Cylinder or cone, BASE at loc (local z from 0 to h) before rotation."""
    r2 = r if r2 is None else r2
    bpy.ops.mesh.primitive_cone_add(vertices=seg, radius1=r, radius2=r2, depth=h)
    o = _active()
    o.data.transform(Matrix.Translation((0, 0, h / 2)))
    return _finish(o, mat, loc, rot, bevel, bseg, angle, scale)


def box(sx, sy, sz, mat, loc=(0, 0, 0), rot=(0, 0, 0), bevel=0.04, bseg=2, angle=50):
    """Box CENTERED at loc."""
    bpy.ops.mesh.primitive_cube_add(size=1.0)
    o = _active()
    o.data.transform(Matrix.Diagonal((sx, sy, sz, 1)))
    bevel = min(bevel, 0.49 * min(sx, sy, sz))
    return _finish(o, mat, loc, rot, bevel, bseg, angle)


def sphere(r, mat, loc=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1), seg=12, rings=7):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, radius=r)
    o = _active()
    return _finish(o, mat, loc, rot, 0, 0, 0, scale)


def blob(r, mat, loc=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1), subdiv=1, jitter=0.0,
         seed=0, bevel=0.0, bseg=1):
    """Faceted icosphere with optional jitter: rocks, foliage clumps."""
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdiv, radius=r)
    o = _active()
    if jitter:
        rnd = random.Random(seed)
        for v in o.data.vertices:
            v.co *= 1.0 + rnd.uniform(-jitter, jitter)
    return _finish(o, mat, loc, rot, bevel, bseg, 20, scale)


def torus(R, r, mat, loc=(0, 0, 0), rot=(0, 0, 0), seg=16, mseg=6, scale=None):
    bpy.ops.mesh.primitive_torus_add(major_segments=seg, minor_segments=mseg,
                                     major_radius=R, minor_radius=r)
    o = _active()
    return _finish(o, mat, loc, rot, 0, 0, 0, scale)


def poly(verts, faces, mat, loc=(0, 0, 0), rot=(0, 0, 0), bevel=0.0, bseg=2, angle=50,
         solidify=0.0, name="poly"):
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.update()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    bpy.context.view_layer.objects.active = o
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    if solidify:
        m = o.modifiers.new("Sol", "SOLIDIFY")
        m.thickness = solidify
        m.offset = 0
        _apply_mods(o)
    return _finish(o, mat, loc, rot, bevel, bseg, angle)


def rod(p0, p1, r, mat, seg=6, r2=None, bevel=0.0):
    """Cylinder from point p0 to p1."""
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    o = cyl(r, d.length, mat, tuple(p0), seg=seg, r2=r2, bevel=bevel)
    o.rotation_euler = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_euler()
    return o


def skull(cx, cy, z, s, light="Accent", dark="Dark", t=0.02):
    """Flat cartoon skull decal lying on a horizontal surface at height z, jaw toward -Y."""
    return [
        cyl(0.5 * s, t, light, (cx, cy + 0.08 * s, z), seg=14, bevel=0),
        box(0.56 * s, 0.4 * s, t, light, (cx, cy - 0.38 * s, z + t / 2), bevel=0.0),
        cyl(0.15 * s, t, dark, (cx - 0.2 * s, cy + 0.02 * s, z + 0.004), seg=8, bevel=0),
        cyl(0.15 * s, t, dark, (cx + 0.2 * s, cy + 0.02 * s, z + 0.004), seg=8, bevel=0),
        box(0.05 * s, 0.22 * s, t, dark, (cx - 0.1 * s, cy - 0.45 * s, z + t / 2 + 0.004),
            bevel=0),
        box(0.05 * s, 0.22 * s, t, dark, (cx + 0.1 * s, cy - 0.45 * s, z + t / 2 + 0.004),
            bevel=0),
    ]


def recolor_faces(o, mat_name, pred):
    """Assign mat_name to faces whose polygon satisfies pred(poly, index)."""
    me = o.data
    if M(mat_name).name not in [m.name for m in me.materials]:
        me.materials.append(M(mat_name))
    idx = [m.name for m in me.materials].index(mat_name)
    for i, p in enumerate(me.polygons):
        if pred(p, i):
            p.material_index = idx


def join(parts, name, origin=(0, 0, 0), smooth_angle=35):
    """Join parts into one mesh object named `name` with its origin at `origin`."""
    parts = [p for p in parts if p]
    bpy.context.view_layer.update()
    for p in parts:
        p.select_set(True)
    with bpy.context.temp_override(active_object=parts[0], selected_objects=parts,
                                   selected_editable_objects=parts):
        bpy.ops.object.join()
    o = parts[0]
    o.data.transform(o.matrix_world)
    o.matrix_world = Matrix.Identity(4)
    o.data.transform(Matrix.Translation(-Vector(origin)))
    o.location = origin
    o.name = name
    o.data.name = name
    # merge duplicate materials (join keeps one slot per material already)
    me = o.data
    me.shade_smooth()
    me.set_sharp_from_angle(angle=math.radians(smooth_angle))
    for p in bpy.context.selected_objects:
        p.select_set(False)
    return o


def empty(name, loc, parent=None):
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = "PLAIN_AXES"
    e.empty_display_size = 0.15
    bpy.context.scene.collection.objects.link(e)
    e.location = loc
    if parent:
        parent_keep(e, parent)
    return e


def parent_keep(child, parent):
    """Parent while keeping world transform, with identity parent inverse."""
    bpy.context.view_layer.update()
    wm = child.matrix_world.copy()
    child.parent = parent
    child.matrix_parent_inverse = Matrix.Identity(4)
    child.matrix_world = wm


def tri_count(objs):
    n = 0
    for o in objs:
        if o.type == "MESH":
            o.data.calc_loop_triangles()
            n += len(o.data.loop_triangles)
    return n


def hierarchy(objs):
    out = []
    for o in objs:
        out.append((o.name, o.type, o.parent.name if o.parent else None,
                    [m.name for m in o.data.materials] if o.type == "MESH" else []))
    return out


# ---------------------------------------------------------------- export

_FBX_PATCHED = False


def patch_fbx_exporter():
    """Work around a Blender FBX exporter bug: with bake_space_transform=True, objects
    two or more levels deep (e.g. Muzzle under Head under Base) get a wrong local matrix
    because the parent's FBX-space matrix is rebuilt from its local matrix only.
    With baking, every child's FBX local matrix should simply be G @ local @ G^-1."""
    global _FBX_PATCHED
    if _FBX_PATCHED:
        return
    import inspect
    import textwrap
    from io_scene_fbx import fbx_utils
    src = textwrap.dedent(inspect.getsource(fbx_utils.ObjectWrapper.fbx_object_matrix))
    old = """                matrix = (parent.matrix_rest_local if rest else parent.matrix_local) @ matrix
                # ...and move it back into parent's *FBX* local space.
                par_mat = parent.fbx_object_matrix(scene_data, rest=rest, local_space=True)
                matrix = par_mat.inverted_safe() @ matrix"""
    old = textwrap.dedent("    " + old.strip("\n")).replace("\n    ", "\n")
    lines_old = [l.strip() for l in old.splitlines()]
    out, i, src_lines, done = [], 0, src.splitlines(), False
    while i < len(src_lines):
        chunk = [l.strip() for l in src_lines[i:i + len(lines_old)]]
        if not done and chunk == lines_old:
            indent = src_lines[i][:len(src_lines[i]) - len(src_lines[i].lstrip())]
            out.append(indent + "matrix = scene_data.settings.global_matrix @ matrix")
            i += len(lines_old)
            done = True
        else:
            out.append(src_lines[i])
            i += 1
    if not done:
        print("WARNING: FBX exporter patch target not found; nested children may be wrong")
        return
    ns = {}
    exec("\n".join(out), fbx_utils.__dict__, ns)
    fbx_utils.ObjectWrapper.fbx_object_matrix = ns["fbx_object_matrix"]
    _FBX_PATCHED = True


def export_fbx(objs, name, category):
    patch_fbx_exporter()
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name + ".fbx")
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH", "EMPTY"},
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type="FACE",
        add_leaf_bones=False, bake_anim=False, use_custom_props=False,
        path_mode="AUTO", embed_textures=False)
    bpy.ops.object.select_all(action="DESELECT")
    dst_dir = ASSET_DIRS[category]
    os.makedirs(dst_dir, exist_ok=True)
    shutil.move(path, os.path.join(dst_dir, name + ".fbx"))
    return os.path.join(dst_dir, name + ".fbx")


# ---------------------------------------------------------------- preview

def stand_in_figure(loc=(0, 0, 0)):
    """1.0 unit tall, 0.55 wide chibi stand-in (not exported)."""
    x, y, z = loc
    parts = [
        cyl(0.07, 0.22, "FigDark", (x - 0.1, y, z), seg=8, bevel=0.02),
        cyl(0.07, 0.22, "FigDark", (x + 0.1, y, z), seg=8, bevel=0.02),
        cyl(0.2, 0.32, "FigTeam", (x, y, z + 0.18), seg=12, r2=0.17, bevel=0.06),
        sphere(0.22, "FigFace", (x, y - 0.01, z + 0.62), seg=14, rings=8),
        sphere(0.275, "FigTeam", (x, y, z + 0.68), scale=(1, 1, 1.15), seg=16, rings=9),
        cyl(0.29, 0.05, "FigTeam", (x, y, z + 0.6), seg=16, bevel=0.02),
        sphere(0.03, "FigDark", (x - 0.07, y - 0.21, z + 0.55)),
        sphere(0.03, "FigDark", (x + 0.07, y - 0.21, z + 0.55)),
    ]
    # cut the helmet lower front so the face shows: push face forward instead
    parts[3].location.y -= 0.06
    parts[6].location.y -= 0.06
    parts[7].location.y -= 0.06
    parts[4].data.transform(Matrix.Translation((0, 0, 0.0)))
    return join(parts, "StandIn", origin=loc)


def setup_render(res_x=760, res_y=760, ground=True):
    sc = bpy.context.scene
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            sc.render.engine = eng
            break
        except TypeError:
            continue
    sc.render.resolution_x = res_x
    sc.render.resolution_y = res_y
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = False
    try:
        sc.eevee.taa_render_samples = 24
    except AttributeError:
        pass
    sc.view_settings.view_transform = "Standard"
    sc.view_settings.look = "None"
    sc.view_settings.exposure = 0.0
    world = bpy.data.worlds.new("W")
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = hex_rgba("#D8D2C4")
    bg.inputs["Strength"].default_value = 0.85
    sc.world = world
    sun_d = bpy.data.lights.new("Sun", "SUN")
    sun_d.energy = 2.2
    sun_d.angle = math.radians(18)
    sun_d.color = (1.0, 0.97, 0.92)
    sun = bpy.data.objects.new("Sun", sun_d)
    sc.collection.objects.link(sun)
    sun.rotation_euler = Euler((math.radians(40), math.radians(0), math.radians(-35)))
    if ground:
        bpy.ops.mesh.primitive_plane_add(size=200)
        g = _active()
        g.name = "Ground"
        gm = bpy.data.materials.new("GroundMat")
        gm.use_nodes = True
        b = gm.node_tree.nodes.get("Principled BSDF")
        b.inputs["Base Color"].default_value = hex_rgba(GROUND)
        b.inputs["Roughness"].default_value = 1.0
        try:
            b.inputs["Specular IOR Level"].default_value = 0.0
        except KeyError:
            pass
        g.data.materials.append(gm)
    cam_d = bpy.data.cameras.new("Cam")
    cam_d.type = "ORTHO"
    cam = bpy.data.objects.new("Cam", cam_d)
    sc.collection.objects.link(cam)
    sc.camera = cam
    return cam


def frame_camera(cam, objs, elev=35.0, yaw=-28.0, margin=1.18, aspect=1.0):
    bpy.context.view_layer.update()
    rot = Euler((math.radians(90 - elev), 0, math.radians(yaw)))
    cam.rotation_euler = rot
    rm = rot.to_matrix()
    right, up, fwd = rm.col[0], rm.col[1], -rm.col[2]
    pts = []
    for o in objs:
        if o.type != "MESH":
            continue
        for c in o.bound_box:
            pts.append(o.matrix_world @ Vector(c))
    xs = [p.dot(right) for p in pts]
    ys = [p.dot(up) for p in pts]
    cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
    w, h = max(xs) - min(xs), max(ys) - min(ys)
    cam.data.ortho_scale = max(w, h * aspect) * margin
    center = right * cx + up * cy
    cam.location = center - fwd * 60
    cam.data.clip_end = 200


def render(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


# ---------------------------------------------------------------- driver

def run(builders, category, names=None, stats_file=None):
    """Build, export and preview each asset. builders: dict name -> fn() -> [objs]."""
    names = names or list(builders.keys())
    stats = {}
    if stats_file and os.path.exists(stats_file):
        with open(stats_file) as f:
            stats = json.load(f)
    for name in names:
        reset_scene()
        objs = builders[name]()
        bpy.context.view_layer.update()
        tris = tri_count(objs)
        dst = export_fbx(objs, name, category)
        stats[name] = {"tris": tris, "objects": hierarchy(objs)}
        print("ASSET", name, tris, hierarchy(objs))
        cam = setup_render()
        frame_camera(cam, objs)
        render(os.path.join(PREV, name + ".png"))
    if stats_file:
        with open(stats_file, "w") as f:
            json.dump(stats, f, indent=1)
    return stats


def cli_names():
    import sys
    if "--" in sys.argv:
        return sys.argv[sys.argv.index("--") + 1:] or None
    return None
