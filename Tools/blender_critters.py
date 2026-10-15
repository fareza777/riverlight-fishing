"""Riverlight ambient critters for Blender (stylized low-poly, palette materials, separate animatable parts).

exec(open(r"D:/CozyFishing/Tools/blender_critters.py").read()); build_all(export=True)

Each part is exported as its own FBX with its origin at the joint it rotates about
(Assets/Art/Models/Critters/<Critter>_<Part>.fbx). ArtSource/critters.json lists, per critter, the parts and their
joint offsets relative to the body origin (Blender coords; Unity = (x, z, y)). Forward is +Y in Blender (Unity +Z).
"""
import json
import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

ROOT = r"D:/CozyFishing"
OUT = os.path.join(ROOT, "Assets/Art/Models/Critters")
RZ = Matrix.Rotation(math.pi, 4, "Z")
M = bpy.data.materials


def hexlin(h):
    h = h.lstrip('#'); c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return [(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in c] + [1]


def mat(name, hx, rough=0.8):
    m = M.get(name) or M.new(name); m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = hexlin(hx); b.inputs["Roughness"].default_value = rough
    b.inputs["Emission Strength"].default_value = 0.0
    m.diffuse_color = hexlin(hx)
    return m


class Part:
    """Accumulates shapes into one mesh; every shape is placed relative to the part's pivot (origin)."""

    def __init__(self, name):
        self.name = name; self.bm = bmesh.new(); self.mats = []

    def mi(self, m):
        if m not in self.mats: self.mats.append(m)
        return self.mats.index(m)

    def ellipsoid(self, m, centre, radii, seg=10, ring=7, rot=(0, 0, 0)):
        res = bmesh.ops.create_uvsphere(self.bm, u_segments=seg, v_segments=ring, radius=1.0)
        R = Matrix.Rotation(rot[0], 4, "X") @ Matrix.Rotation(rot[1], 4, "Y") @ Matrix.Rotation(rot[2], 4, "Z")
        T = Matrix.Translation(Vector(centre)) @ R @ Matrix.Diagonal((*radii, 1))
        bmesh.ops.transform(self.bm, matrix=T, verts=res["verts"])
        idx = self.mi(m)
        for f in {f for v in res["verts"] for f in v.link_faces}: f.material_index = idx
        return res["verts"]

    def cone(self, m, a, b, r1, r2, seg=7):
        a, b = Vector(a), Vector(b); d = b - a
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, segments=seg, radius1=r1, radius2=r2, depth=d.length)
        q = d.to_track_quat("Z", "Y").to_matrix().to_4x4()
        bmesh.ops.transform(self.bm, matrix=Matrix.Translation((a + b) / 2) @ q, verts=res["verts"])
        idx = self.mi(m)
        for f in {f for v in res["verts"] for f in v.link_faces}: f.material_index = idx

    def wing(self, m, root, span, chord, side, thick=0.012, droop=0.0):
        """A flat tapered wing (two-sided slab) from the shoulder outward along +/-X."""
        s = 1 if side > 0 else -1
        pts = [(0, chord * 0.5, 0), (0, -chord * 0.5, 0), (s * span, -chord * 0.15, -droop), (s * span * 0.7, chord * 0.35, -droop * 0.6)]
        top = [self.bm.verts.new(Vector(root) + Vector(p) + Vector((0, 0, thick))) for p in pts]
        bot = [self.bm.verts.new(Vector(root) + Vector(p)) for p in pts]
        idx = self.mi(m)
        for f in (self.bm.faces.new(top), self.bm.faces.new(bot[::-1])): f.material_index = idx
        for i in range(4):
            j = (i + 1) % 4
            self.bm.faces.new([top[i], top[j], bot[j], bot[i]]).material_index = idx

    def finish(self, smooth=True):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        me = bpy.data.meshes.new(self.name); self.bm.to_mesh(me); self.bm.free()
        for m in self.mats: me.materials.append(m)
        for p in me.polygons: p.use_smooth = smooth
        me.set_sharp_from_angle(angle=math.radians(55))
        return me


def write_fbx(me, name, col, offset):
    ob = bpy.data.objects.get(name)
    if ob: bpy.data.objects.remove(ob)
    ob = bpy.data.objects.new(name, me); col.objects.link(ob); ob.location = Vector(offset)
    m2 = me.copy(); m2.transform(RZ)
    tmp = bpy.data.objects.new(name + "_EXP", m2); bpy.context.scene.collection.objects.link(tmp)
    bpy.ops.object.select_all(action="DESELECT"); tmp.select_set(True); bpy.context.view_layer.objects.active = tmp
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + ".fbx"), use_selection=True, object_types={"MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True,
                             axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False)
    bpy.data.objects.remove(tmp); bpy.data.meshes.remove(m2)


# ------------------------------------------------------------------ critters (parts are built around their joints)
def duck():
    body, head = mat("Pal_DuckBody", "#8C8478"), mat("Pal_DuckHead", "#2E6B47")
    chest, bill, white, eye = mat("Pal_DuckChest", "#7A4A34"), mat("Pal_DuckBill", "#E8B84A"), mat("Pal_Cream", "#F2E8D5"), mat("Pal_Ink", "#1E1A18")
    b = Part("Duck_Body")
    b.ellipsoid(body, (0, -0.02, 0.07), (0.11, 0.2, 0.085))
    b.ellipsoid(chest, (0, 0.1, 0.08), (0.095, 0.08, 0.075))
    b.cone(body, (0, -0.18, 0.09), (0, -0.27, 0.15), 0.05, 0.01, 6)                # tail
    b.ellipsoid(white, (0, -0.2, 0.1), (0.035, 0.02, 0.03))
    for s in (1, -1): b.ellipsoid(body, (s * 0.085, -0.04, 0.11), (0.03, 0.13, 0.035))   # folded wings
    h = Part("Duck_Head")
    h.cone(head, (0, 0, 0), (0, 0.03, 0.11), 0.045, 0.04, 8)                       # neck
    h.ellipsoid(head, (0, 0.04, 0.14), (0.055, 0.07, 0.055))
    h.ellipsoid(white, (0, 0.0, 0.07), (0.047, 0.04, 0.012))                        # collar
    h.ellipsoid(bill, (0, 0.13, 0.13), (0.03, 0.05, 0.015))
    for s in (1, -1): h.ellipsoid(eye, (s * 0.045, 0.075, 0.155), (0.009, 0.009, 0.009), 6, 4)
    return {"Body": (b.finish(), (0, 0, 0)), "Head": (h.finish(), (0, 0.12, 0.12))}


def bird(prefix, c_body, c_belly, c_wing, c_beak, size=1.0):
    body, belly, wingm = mat(f"Pal_{prefix}Body", c_body), mat(f"Pal_{prefix}Belly", c_belly), mat(f"Pal_{prefix}Wing", c_wing)
    beak, eye = mat(f"Pal_{prefix}Beak", c_beak), mat("Pal_Ink", "#1E1A18")
    s = size
    b = Part(f"{prefix}_Body")
    b.ellipsoid(body, (0, 0, 0), (0.035 * s, 0.07 * s, 0.035 * s))
    b.ellipsoid(belly, (0, 0.01 * s, -0.008 * s), (0.03 * s, 0.05 * s, 0.028 * s))
    b.ellipsoid(body, (0, 0.06 * s, 0.025 * s), (0.03 * s, 0.032 * s, 0.03 * s))     # head
    b.cone(beak, (0, 0.085 * s, 0.024 * s), (0, 0.115 * s, 0.02 * s), 0.009 * s, 0.001, 5)
    for k in (1, -1): b.ellipsoid(eye, (k * 0.022 * s, 0.075 * s, 0.035 * s), (0.005 * s,) * 3, 6, 4)
    b.cone(wingm, (0, -0.05 * s, 0.005 * s), (0, -0.11 * s, 0.018 * s), 0.02 * s, 0.004 * s, 5)   # tail
    parts = {"Body": (b.finish(), (0, 0, 0))}
    for k, side in ((1, "WingR"), (-1, "WingL")):
        w = Part(f"{prefix}_{side}")
        w.wing(wingm, (0, 0, 0), 0.11 * s, 0.06 * s, k, 0.006 * s)
        parts[side] = (w.finish(False), (k * 0.026 * s, 0.005 * s, 0.018 * s))
    return parts


def heron():
    grey, light, dark = mat("Pal_HeronGrey", "#9AA3AB"), mat("Pal_HeronLight", "#E3E6E4"), mat("Pal_HeronDark", "#3E4650")
    beak, eye = mat("Pal_HeronBeak", "#D9A441"), mat("Pal_Ink", "#1E1A18")
    b = Part("Heron_Body")
    b.ellipsoid(grey, (0, 0, 0.64), (0.13, 0.26, 0.14), rot=(0.35, 0, 0))
    b.ellipsoid(dark, (0, -0.09, 0.7), (0.115, 0.21, 0.07), rot=(0.35, 0, 0))
    b.cone(grey, (0, -0.17, 0.58), (0, -0.3, 0.52), 0.06, 0.01, 6)
    for k in (1, -1):
        b.cone(beak, (k * 0.04, 0, 0.55), (k * 0.045, 0.01, 0.0), 0.011, 0.008, 5)    # legs
        b.cone(beak, (k * 0.045, 0.01, 0.01), (k * 0.045, 0.1, 0.0), 0.007, 0.004, 4)  # toes
    n = Part("Heron_Neck")
    n.cone(light, (0, 0, 0), (0, 0.05, 0.22), 0.04, 0.03, 8)
    n.cone(light, (0, 0.05, 0.22), (0, 0.0, 0.36), 0.03, 0.03, 8)
    n.ellipsoid(light, (0, 0.02, 0.4), (0.04, 0.06, 0.04))
    n.ellipsoid(dark, (0, -0.02, 0.43), (0.02, 0.07, 0.012))                          # crest
    n.cone(beak, (0, 0.07, 0.4), (0, 0.2, 0.39), 0.014, 0.002, 5)
    for k in (1, -1): n.ellipsoid(eye, (k * 0.033, 0.05, 0.415), (0.007,) * 3, 6, 4)
    parts = {"Body": (b.finish(), (0, 0, 0)), "Neck": (n.finish(), (0, 0.15, 0.68))}
    for k, side in ((1, "WingR"), (-1, "WingL")):
        w = Part(f"Heron_{side}")
        w.wing(grey, (0, 0, 0), 0.5, 0.26, k, 0.015, 0.05)
        parts[side] = (w.finish(False), (k * 0.08, 0.0, 0.7))
    return parts


def rabbit():
    fur, belly, inner, eye = mat("Pal_RabbitFur", "#B49373"), mat("Pal_Cream", "#F2E8D5"), mat("Pal_RabbitInner", "#E7A6A0"), mat("Pal_Ink", "#1E1A18")
    b = Part("Rabbit_Body")
    b.ellipsoid(fur, (0, -0.02, 0.09), (0.075, 0.11, 0.08))
    b.ellipsoid(belly, (0, 0.03, 0.07), (0.055, 0.07, 0.055))
    b.ellipsoid(fur, (0, 0.09, 0.15), (0.055, 0.06, 0.055))                          # head
    b.ellipsoid(belly, (0, -0.13, 0.11), (0.03, 0.03, 0.03))                          # tail
    for k in (1, -1):
        b.ellipsoid(fur, (k * 0.055, -0.05, 0.05), (0.03, 0.07, 0.045))               # haunches
        b.ellipsoid(eye, (k * 0.04, 0.125, 0.165), (0.009,) * 3, 6, 4)
    b.ellipsoid(inner, (0, 0.148, 0.145), (0.01, 0.008, 0.008), 6, 4)                 # nose
    e = Part("Rabbit_Ears")
    for k in (1, -1):
        e.ellipsoid(fur, (k * 0.022, 0, 0.06), (0.018, 0.01, 0.065), rot=(0, k * 0.2, 0))
        e.ellipsoid(inner, (k * 0.022, 0.006, 0.06), (0.011, 0.006, 0.05), rot=(0, k * 0.2, 0))
    return {"Body": (b.finish(), (0, 0, 0)), "Ears": (e.finish(), (0, 0.085, 0.19))}


def deer():
    coat, belly, dark, antler, eye = mat("Pal_DeerCoat", "#A6693E"), mat("Pal_Cream", "#F2E8D5"), mat("Pal_DeerDark", "#5B3A24"), mat("Pal_Antler", "#D8C7A3"), mat("Pal_Ink", "#1E1A18")
    b = Part("Deer_Body")
    b.ellipsoid(coat, (0, 0, 0.78), (0.2, 0.44, 0.22))
    b.ellipsoid(belly, (0, 0, 0.66), (0.15, 0.34, 0.11))
    b.ellipsoid(belly, (0, -0.45, 0.86), (0.07, 0.05, 0.08))                          # tail
    for k in (1, -1):
        for y in (0.28, -0.28):
            b.ellipsoid(belly, (k * 0.1, y, 0.93), (0.03, 0.03, 0.02), 6, 4)          # spots
    n = Part("Deer_Neck")
    n.cone(coat, (0, 0, 0), (0, 0.12, 0.34), 0.1, 0.07, 8)
    n.ellipsoid(coat, (0, 0.18, 0.4), (0.075, 0.13, 0.08), rot=(-0.3, 0, 0))
    n.ellipsoid(dark, (0, 0.3, 0.37), (0.035, 0.04, 0.035))                           # muzzle
    for k in (1, -1):
        n.ellipsoid(coat, (k * 0.08, 0.1, 0.5), (0.05, 0.015, 0.035), rot=(0, k * 0.4, 0))   # ears
        n.ellipsoid(eye, (k * 0.055, 0.22, 0.44), (0.013,) * 3, 6, 4)
        n.cone(antler, (k * 0.03, 0.12, 0.47), (k * 0.1, 0.08, 0.68), 0.012, 0.007, 5)
        n.cone(antler, (k * 0.08, 0.09, 0.62), (k * 0.03, 0.16, 0.72), 0.008, 0.004, 5)
    parts = {"Body": (b.finish(), (0, 0, 0)), "Neck": (n.finish(), (0, 0.38, 0.9))}
    for name, x, y in (("LegFL", 0.11, 0.3), ("LegFR", -0.11, 0.3), ("LegBL", 0.11, -0.3), ("LegBR", -0.11, -0.3)):
        l = Part(f"Deer_{name}")
        l.cone(coat, (0, 0, 0.05), (0, 0, -0.34), 0.065, 0.04, 7)
        l.cone(coat, (0, 0, -0.34), (0, 0.02, -0.64), 0.035, 0.026, 7)
        l.ellipsoid(dark, (0, 0.03, -0.66), (0.03, 0.04, 0.03), 6, 4)
        parts[name] = (l.finish(), (x, y, 0.68))
    return parts


def butterfly(prefix, c_wing, c_edge):
    wing, edge, body = mat(f"Pal_{prefix}", c_wing), mat(f"Pal_{prefix}Edge", c_edge), mat("Pal_Ink", "#1E1A18")
    b = Part(f"{prefix}_Body")
    b.ellipsoid(body, (0, 0, 0), (0.006, 0.025, 0.006), 6, 4)
    parts = {"Body": (b.finish(), (0, 0, 0))}
    for k, side in ((1, "WingR"), (-1, "WingL")):
        w = Part(f"{prefix}_{side}")
        w.ellipsoid(wing, (k * 0.025, 0.008, 0), (0.025, 0.02, 0.002), 8, 4)
        w.ellipsoid(edge, (k * 0.02, -0.018, 0), (0.017, 0.013, 0.0025), 8, 4)
        parts[side] = (w.finish(False), (k * 0.004, 0, 0))
    return parts


def cat():
    fur, stripe, belly, eye = mat("Pal_CatFur", "#D99A5B"), mat("Pal_CatStripe", "#B06E3A"), mat("Pal_Cream", "#F2E8D5"), mat("Pal_Ink", "#1E1A18")
    b = Part("Cat_Body")   # curled up asleep
    b.ellipsoid(fur, (0, 0, 0.08), (0.16, 0.13, 0.085))
    for x in (-0.06, 0.0, 0.06): b.ellipsoid(stripe, (x, -0.02, 0.152), (0.02, 0.09, 0.012), 8, 4)
    b.ellipsoid(fur, (0.1, 0.09, 0.09), (0.065, 0.06, 0.055))                         # head tucked in
    b.ellipsoid(belly, (0.13, 0.13, 0.075), (0.03, 0.025, 0.02))
    for k in (1, -1): b.cone(fur, (0.1 + k * 0.03, 0.08, 0.13), (0.1 + k * 0.04, 0.07, 0.18), 0.022, 0.003, 4)
    for k in (1, -1): b.ellipsoid(eye, (0.1 + k * 0.025, 0.145, 0.1), (0.012, 0.004, 0.003), 6, 4)   # closed eyes
    t = Part("Cat_Tail")
    t.cone(fur, (0, 0, 0), (-0.05, 0.12, 0.0), 0.032, 0.028, 7)
    t.cone(stripe, (-0.05, 0.12, 0.0), (0.05, 0.2, 0.0), 0.028, 0.016, 7)
    return {"Body": (b.finish(), (0, 0, 0)), "Tail": (t.finish(), (-0.12, -0.08, 0.04))}


def stool():
    wood, dark = mat("Pal_WoodMid", "#9A6B43"), mat("Pal_WoodDark", "#5E3F28")
    s = Part("Stool_Body")
    s.ellipsoid(wood, (0, 0, 0.42), (0.18, 0.18, 0.03), 10, 4)
    for a in range(3):
        ang = a * 2.094
        s.cone(dark, (math.cos(ang) * 0.12, math.sin(ang) * 0.12, 0.42), (math.cos(ang) * 0.16, math.sin(ang) * 0.16, 0.0), 0.02, 0.018, 5)
    s.cone(dark, (0.25, 0.1, 0.0), (0.25, 0.1, 0.22), 0.1, 0.09, 10)                  # bucket
    return {"Body": (s.finish(), (0, 0, 0))}


CRITTERS = {
    "Duck": duck,
    "Songbird": lambda: bird("Songbird", "#8A6A4E", "#E9925A", "#5E4636", "#E8B84A"),
    "Gull": lambda: bird("Gull", "#F1F1EE", "#FFFFFF", "#9AA2AA", "#E9B53C", 2.6),
    "Heron": heron,
    "Rabbit": rabbit,
    "Deer": deer,
    "ButterflyOrange": lambda: butterfly("ButterflyOrange", "#F2994A", "#4A2E1F"),
    "ButterflyBlue": lambda: butterfly("ButterflyBlue", "#6FB7E8", "#1F3550"),
    "ButterflyYellow": lambda: butterfly("ButterflyYellow", "#F6D55C", "#5A4A1F"),
    "Cat": cat,
    "Stool": stool,
}


def build_all(export_files=True):
    os.makedirs(OUT, exist_ok=True)
    col = bpy.data.collections.get("Critters") or bpy.data.collections.new("Critters")
    if col.name not in bpy.context.scene.collection.children: bpy.context.scene.collection.children.link(col)
    meta = {}
    x = 0.0
    for name, fn in CRITTERS.items():
        parts = fn()
        meta[name] = {}
        for part, (me, offset) in parts.items():
            pname = f"{name}_{part}"
            ob_off = Vector((x, 40.0, 0.0)) + Vector(offset)
            if export_files: write_fbx(me, pname, col, ob_off)
            meta[name][part] = [round(v, 4) for v in offset]
        x += 1.6
    json.dump(meta, open(os.path.join(ROOT, "ArtSource/critters.json"), "w"), indent=1)
    return meta
