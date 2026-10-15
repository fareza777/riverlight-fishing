"""Riverlight fish generator for Blender: builds all fish in ArtSource/fish_catalog.json and exports FBX per fish.

Run inside Blender:  exec(open(r"D:/CozyFishing/Tools/blender_fish.py").read()); build_all(export=True)
"""
import json
import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector, noise

ROOT = r"D:/CozyFishing"
M = bpy.data.materials


def hex2lin(h):
    h = h.lstrip('#'); c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return [(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in c] + [1]


def mat(name, hx, emit=None):
    m = M.get(name) or M.new(name); m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = hex2lin(hx); b.inputs["Roughness"].default_value = 0.5; m.diffuse_color = hex2lin(hx)
    b.inputs["Emission Strength"].default_value = 3.0 if emit else 0.0
    if emit: b.inputs["Emission Color"].default_value = hex2lin(emit)
    return name


def radius(u, R, s):
    if s.get("eel"):
        return R * (min(1, u * 5) ** 0.6) * (1 - max(0, u - 0.88) / 0.12 * 0.5)
    if s.get("round"):
        return R * max(0.0, math.sin(math.pi * min(1, u * 0.9 + 0.08))) ** 0.55 * (0.15 + 0.85 * min(1, u * 3))
    if u >= 0.6:
        return R * max(0.0, 1 - ((u - 0.6) / 0.43) ** 2) ** 0.5
    return R * (0.22 + 0.78 * math.sin(math.pi / 2 * u / 0.6) ** 1.3)


def tri(bm, pts, mi, double=True):
    a = [bm.verts.new(p) for p in pts]
    bm.faces.new(a).material_index = mi
    if double:
        b = [bm.verts.new(p) for p in pts[::-1]]
        bm.faces.new(b).material_index = mi


def build_fish(fid, s, col):
    mats = [mat(f"Fish_{fid}_Body", s["body"]), mat(f"Fish_{fid}_Back", s["back"]), mat(f"Fish_{fid}_Belly", s["belly"]),
            mat(f"Fish_{fid}_Fin", s["fin"]), mat(f"Fish_{fid}_Accent", s["accent"], s["accent"] if s["pattern"] == "glow" else None),
            "Pal_Ink", "Pal_Cream"]
    L, R = s["L"], s["R"]
    eel, flat, snout = s.get("eel", False), s.get("flat", False), s.get("snout", 0.0)
    bm = bmesh.new(); SEG = 16; N = 22; rings = []
    for k in range(N + 1):
        u = k / N; r = radius(u, R, s); y = -L / 2 + L * u
        ring = []
        for i in range(SEG):
            a = 2 * math.pi * i / SEG
            x = math.cos(a) * r * s["w"]; z = math.sin(a) * r * s["h"]
            if eel: x += math.sin(u * math.pi * 2.2) * 0.03 * (1 - u)
            if flat and z < 0: z *= 0.5
            ring.append(bm.verts.new((x, y, z)))
        rings.append(ring)
    for k in range(N):
        for i in range(SEG):
            j = (i + 1) % SEG; bm.faces.new([rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]])
    bm.faces.new(rings[0])
    tipy = L / 2 + R * 0.06 + snout * L
    prev = rings[-1]
    if snout > 0:
        for kk in range(1, 4):
            f = kk / 4; yy = L / 2 + snout * L * f; rr = R * 0.32 * (1 - f * 0.6)
            ring = [bm.verts.new((math.cos(2 * math.pi * i / SEG) * rr * s["w"] * 1.2, yy, math.sin(2 * math.pi * i / SEG) * rr * 0.55 - R * 0.05)) for i in range(SEG)]
            for i in range(SEG):
                j = (i + 1) % SEG; bm.faces.new([prev[i], prev[j], ring[j], ring[i]])
            prev = ring
    nose = bm.verts.new((0, tipy, -R * 0.05))
    for i in range(SEG): bm.faces.new([prev[i], prev[(i + 1) % SEG], nose])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.normal_update()

    off = Vector(((sum(map(ord, fid)) % 97) * 0.37, 1.3, 2.1))
    for f in bm.faces:
        c = f.calc_center_median(); n = f.normal; u = (c.y + L / 2) / L
        mi = 1 if n.z > 0.6 else (2 if n.z < -0.4 else 0)
        p = s["pattern"]
        if mi == 0 and u < 0.92:
            if p == "bars" and 0.2 < u < 0.8 and int(u * 16) % 3 == 0: mi = 4
            if p == "spots" and noise.noise(c * 55 + off) > 0.3: mi = 4
            if p == "scales" and 0.1 < u < 0.85 and (int(u * N) + int(math.atan2(c.z, c.x) / (2 * math.pi) * SEG)) % 2 == 0: mi = 4
            if p == "stripe" and abs(c.z) < R * 0.18 and 0.1 < u < 0.9: mi = 4
        if p == "glow" and abs(n.z) < 0.7 and int(u * 24) % 3 == 0 and u > 0.1: mi = 4
        if p == "scutes" and n.z > 0.55 and int(u * 14) % 2 == 0: mi = 4
        f.material_index = mi

    TL = L * (0.17 if not eel else 0.07); t0 = Vector((0, -L / 2 + 0.005, 0))
    for sgn in (1, -1):
        tip = Vector((0, -L / 2 - TL, sgn * TL * 0.9)); notch = Vector((0, -L / 2 - TL * 0.5, 0))
        tri(bm, [t0 + Vector((0.004, 0, 0)), tip + Vector((0.004, 0, 0)), notch + Vector((0.004, 0, 0))], 3)
    top = R * s["h"]
    if not eel:
        y0, y1 = (-L * 0.3, L * 0.25) if s.get("sail") else (-L * 0.14, L * 0.16)
        hgt = R * (1.4 if s.get("sail") else 0.65)
        tri(bm, [(0, y0, top * 0.72), (0, y1, top * 0.94), (0, y0 - L * 0.04, top + hgt)], 3)
        tri(bm, [(0, -L * 0.28, -top * 0.55), (0, -L * 0.12, -top * 0.8), (0, -L * 0.32, -top - R * 0.3)], 3)
    else:
        prev2 = None
        for k in range(3, N - 3):
            u = k / N; y = -L / 2 + L * u; x = math.sin(u * math.pi * 2.2) * 0.03 * (1 - u); r = radius(u, R, s)
            v0 = bm.verts.new((x, y, r * 0.85)); v1 = bm.verts.new((x, y, r * 0.85 + R * 0.6))
            if prev2: bm.faces.new([prev2[0], v0, v1, prev2[1]]).material_index = 4
            prev2 = (v0, v1)
    wide = 2.2 if flat else 1.0
    for sgn in (1, -1):
        bx = sgn * R * s["w"] * 0.95; y = L * 0.2
        tri(bm, [(bx, y, -R * 0.35), (bx, y - L * 0.07, -R * 0.4), (bx + sgn * R * 0.35 * wide, y - L * 0.13, -R * 0.75)], 3)
    if s.get("whiskers"):
        for sgn in (1, -1):
            for k2 in range(2):
                base = Vector((sgn * R * 0.18, tipy - R * 0.15 - k2 * R * 0.12, -R * 0.25))
                end = base + Vector((sgn * R * (0.6 + 0.3 * k2), -R * 0.3, -R * 0.5))
                w = R * 0.04
                tri(bm, [base + Vector((0, 0, w)), base - Vector((0, 0, w)), end], 3)
    er = R * (0.2 if not eel else 0.32)
    ey = L / 2 - R * (0.5 if not eel else 0.9)
    ez = R * 0.22 if not flat else R * 0.45
    for sgn in (1, -1):
        rx = radius((ey + L / 2) / L, R, s) * s["w"]
        res = bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=er)
        for v in res["verts"]: v.co += Vector((sgn * rx * 0.85, ey, ez))
        for f in {f for v in res["verts"] for f in v.link_faces}: f.material_index = 5
        res = bmesh.ops.create_uvsphere(bm, u_segments=6, v_segments=4, radius=er * 0.32)
        for v in res["verts"]: v.co += Vector((sgn * (rx * 0.85 + er * 0.8), ey + er * 0.3, ez + er * 0.35))
        for f in {f for v in res["verts"] for f in v.link_faces}: f.material_index = 6

    name = f"Fish_{fid}"
    old = bpy.data.objects.get(name)
    if old: bpy.data.objects.remove(old)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me); col.objects.link(ob)
    for mname in mats: me.materials.append(M[mname])
    for p in me.polygons: p.use_smooth = True
    me.set_sharp_from_angle(angle=math.radians(50))
    return ob


def export_fish(ob, path):
    """Export with the project convention: 180 deg about Z so the fish's head (+Y) faces Unity +Z after axis conversion."""
    me = ob.data.copy(); me.transform(Matrix.Rotation(math.pi, 4, "Z"))
    tmp = bpy.data.objects.new(ob.name, me) if False else bpy.data.objects.new(ob.name + "_EXP", me)
    bpy.context.scene.collection.objects.link(tmp)
    tmp.name = ob.name + "_EXP"
    bpy.ops.object.select_all(action="DESELECT"); tmp.select_set(True); bpy.context.view_layer.objects.active = tmp
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True, axis_forward="-Z", axis_up="Y",
                             mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False)
    bpy.data.objects.remove(tmp); bpy.data.meshes.remove(me)


def build_all(export=False):
    cat = json.load(open(os.path.join(ROOT, "ArtSource/fish_catalog.json"), encoding="utf-8"))["fish"]
    col = bpy.data.collections.get("Fish48") or bpy.data.collections.new("Fish48")
    if col.name not in bpy.context.scene.collection.children: bpy.context.scene.collection.children.link(col)
    col.hide_viewport = False
    out_dir = os.path.join(ROOT, "Assets/Art/Models/Fish")
    os.makedirs(out_dir, exist_ok=True)
    for i, f in enumerate(cat):
        ob = build_fish(f["id"], f["model"], col)
        if export: export_fish(ob, os.path.join(out_dir, f"{f['id']}.fbx"))
        ob.location = ((i % 12) * 1.3 - 7, 20 + (i // 12) * 1.6, 1.0)
        ob.rotation_euler = (0, 0, -1.5708)
    return len(cat)
