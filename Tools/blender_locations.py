"""Riverlight location generator for Blender.

exec(open(r"D:/CozyFishing/Tools/blender_locations.py").read()); build_location("lake", export=True)

Each location exports:
  Assets/Art/Models/Level/Terrain_<id>.fbx      terrain (vertex colour R = path mask)
  ArtSource/locations/<id>.json                 props, dock, spawn, spots, schools (Blender coords; Unity x=x, z=y, y=z, yaw=-rot)
Shared detail meshes: Assets/Art/Models/Reeds.fbx, Assets/Art/Models/LilyPads.fbx
"""
import json
import math
import os
import random

import bmesh
import bpy
from mathutils import Matrix, Vector, noise
from mathutils.geometry import intersect_point_line

ROOT = r"D:/CozyFishing"
RZ = Matrix.Rotation(math.pi, 4, "Z")


def sstep(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def smin(a, b, k):
    h = max(k - abs(a - b), 0.0) / k
    return min(a, b) - h * h * k * 0.25


def n2(x, y, s=0.08, z=0.3):
    return noise.noise(Vector((x * s, y * s, z)))


# ------------------------------------------------------------------ height functions (water level = 0)
def h_lake(x, y):
    # Wide lake in a wooded bowl: island, lily bay to the west, meadow shore with the dock to the south.
    c = Vector((0.0, 8.0))
    d = (Vector((x, y)) - c).length - (26.0 + 4.0 * n2(x, y, 0.07, 4.2))
    bay = (Vector((x, y)) - Vector((-24, 20))).length - 8
    d = smin(d, bay, 6.0)
    isl = (Vector((x, y)) - Vector((10, 16))).length - 4.5
    if d < 0:
        depth = 0.3 + 2.6 * sstep(0, 14, -d)
        z = -depth + n2(x, y, 0.3, 1.7) * 0.12
        if isl < 3:
            z = max(z, 1.2 * sstep(3, -2.5, isl) - 0.3 + n2(x, y, 0.4, 9) * 0.25)
        return z
    south = sstep(-6, -22, y)                      # meadow side (dock, hut) stays low
    bowl = (2.0 + 5.5 * sstep(4, 18, d) * (0.6 + 0.4 * n2(x, y, 0.05, 7))) * (1 - 0.75 * south)
    hill = 0.9 + 0.35 * n2(x, y, 0.08) + bowl * sstep(2, 12, d)
    return hill * sstep(0, 3.0, d) - 0.3 * (1 - sstep(0, 1.2, d)) + n2(x, y, 0.25, 1.7) * 0.06 * sstep(0, 3, d)


def gorge_center(y):
    return 4.0 * math.sin(y / 9.0) - 2.0


def h_gorge(x, y):
    # North wall closes the gorge; the river pours over a lip in it (the waterfall) into the deep pool.
    if y > 33.0:
        wall = 7.5 + 1.5 * n2(x, y, 0.06, 5)
        lip = abs(x - gorge_center(26)) < 2.6
        k = sstep(33.0, 34.2, y)
        top = 5.6 if lip else wall
        return -2.0 * (1 - k) + top * k
    xc = gorge_center(y)
    d = abs(x - xc) - (5.0 + 1.2 * math.sin(y / 6.0))
    pool = (Vector((x, y)) - Vector((gorge_center(26), 26))).length - 9.0
    d = smin(d, pool, 4.0)
    if d < 0:
        rapids = sstep(-14, -8, y) * sstep(6, 0, y)          # shallow rocky run
        depth = (0.35 + 1.7 * sstep(0, 4, -d)) * (1 - 0.7 * rapids)
        depth += 1.6 * sstep(9, 2, pool)
        return -depth + n2(x, y, 0.35, 2.2) * (0.12 + 0.25 * rapids)
    # walkable west bench by the dock, then cliffs
    west = x < xc
    bench = 1.3 + 0.2 * n2(x, y, 0.2)
    cliff = 6.5 + 2.5 * n2(x, y, 0.05, 5) + 0.35 * noise.noise(Vector((x * 0.22, y * 0.22, 3)))
    if west and -40 < y < -12:
        z = bench * sstep(0, 2.0, d) + (cliff - bench) * sstep(7, 10, d)
    else:
        z = cliff * sstep(0.5, 3.5, d)
    north = sstep(30, 34, y) * (cliff + 1.0)
    if north > z and not (abs(x - gorge_center(26)) < 2.6 and y > 33):
        z = north
    elif abs(x - gorge_center(26)) < 2.6 and y > 33:
        z = max(z, north - 1.2)
    return z - 0.3 * (1 - sstep(0, 1.2, d))


def coast_x(y):
    return -8.0 + 6.0 * math.sin(y / 15.0) + 2.0 * math.sin(y / 5.3)


def h_estuary(x, y):
    d = coast_x(y) - x                                        # >0 on land (west)
    if d > 0:
        dunes = 0.6 * sstep(0, 6, d) + 0.9 * sstep(8, 20, d) * (0.5 + 0.5 * n2(x, y, 0.08, 3))
        return 0.15 + dunes + n2(x, y, 0.25, 6) * 0.08 * sstep(0, 4, d)
    sea = -d
    depth = 0.4 + 2.8 * sstep(2, 30, sea)
    z = -depth + n2(x, y, 0.3, 1.1) * 0.12
    # marsh islands (low, reedy) between the coast and the bay
    m = noise.noise(Vector((x * 0.16, y * 0.16, 8.0)))
    if 1.5 < sea < 14 and m > 0.32:
        z = max(z, -0.15 + (m - 0.32) * 1.6)
    # rocky lighthouse point
    rp = (Vector((x, y)) - Vector((16, 22))).length
    if rp < 6:
        z = max(z, 1.6 * sstep(6, 2.5, rp) + 0.4 * noise.noise(Vector((x * 0.8, y * 0.8, 1))))
    return z


HEIGHT = {"lake": h_lake, "gorge": h_gorge, "estuary": h_estuary}


# ------------------------------------------------------------------ layouts (Blender XY)
LAYOUT = {
    "lake": dict(
        dock=(-3.0, -20.0, 0), spawn=(-9.0, -28.0), path=[(-14.0, -31.0), (-9.0, -27.5), (-5.0, -24.0), (-3.0, -22.0)],
        hut=(-15.0, -31.0, 160), stall=(3.5, -27.0, 200), mapboard=(-10.5, -23.5, 180),
        spots=[("Lily Cove", "Teluk Teratai", "Reeds", -23, 20), ("Willow Shade", "Teduh Dedalu", "Lake|Dock", -20, -2),
               ("Old Pier", "Dermaga Tua", "Dock|Lake", -2, -13), ("Mirror Center", "Tengah Cermin", "Lake|Deep", 0, 6),
               ("Island Deep", "Dalam Pulau", "Deep|Lake", 14, 10)],
        schools=[("bluegill", -2, -10, 7, 4, .5), ("rainbow_trout", 2, 8, 5, 6, 1.0), ("golden_shiner", -22, 19, 8, 3.5, .4),
                 ("largemouth_bass", -19, -1, 3, 4, .7), ("lantern_koi", 0, 6, 1, 7, 1.2)],
        willows=[(-25, -6), (-31, 10), (-30, 30), (-8, 36), (12, 36), (29, 20), (28, -4), (19, -17), (-21, -19)],
        lily=[(-23, 20), (-20, 25), (-27, 16), (17, 2), (8, -12), (-10, 30), (21, 14)],
        reeds_where=lambda z, x, y: -0.6 < z < -0.15,
        pines=55, rocks=12, extent=(-46, 46, -48, 52)),
    "gorge": dict(
        dock=(-8.6, -24.0, -90), spawn=(-11.5, -30.0), path=[(-12.0, -34.0), (-11.0, -30.0), (-10.0, -26.0)],
        hut=(-10.5, -38.5, 180), stall=(-11.8, -20.0, 70), mapboard=(-14.0, -27.0, 120),
        spots=[("Lower Run", "Arus Bawah", "Rapids|Pool", -3, -33), ("Rapids", "Jeram", "Rapids|Shallows", -4, -4),
               ("Stone Shallows", "Dangkal Batu", "Shallows", 1, -12), ("Gorge Pool", "Kolam Ngarai", "Pool", -4, 20),
               ("Deep Hole", "Lubang Dalam", "Deep|Pool", -3, 28)],
        schools=[("grayling", -2, -6, 6, 3.5, .4), ("brown_trout", -3, -32, 5, 4, .6), ("arctic_char", -4, 22, 5, 5, 1.0),
                 ("chub", 0, 8, 6, 4, .6), ("ironscale_sturgeon", -3, 28, 1, 4, 1.6)],
        willows=[], lily=[], reeds_where=lambda z, x, y: False,
        waterfall=(gorge_center(26), 34.5),
        pines=70, rocks=26, rocks_in_water=(-14, 5), extent=(-38, 38, -48, 44)),
    "estuary": dict(
        dock=(coast_x(-10) - 0.6, -10.0, -90), spawn=(coast_x(-14) - 5.0, -14.0), path=[(coast_x(-16) - 8, -16.0), (coast_x(-12) - 3, -12.0)],
        hut=(coast_x(-25) - 11.0, -25.0, 30), stall=(coast_x(-6) - 4.5, -6.0, 300), mapboard=(coast_x(-10) - 6.5, -9.0, 270),
        lighthouse=(16.5, 23.0),
        spots=[("Harbor", "Pelabuhan", "Dock|Sea", -1, -9), ("Reed Maze", "Labirin Ilalang", "Reeds", 0, 6),
               ("Sand Flats", "Dataran Pasir", "Sea|Reeds", -4, -26), ("Open Bay", "Teluk Lepas", "Sea|Deep", 18, -4),
               ("Lighthouse Rocks", "Batu Mercusuar", "Deep|Dock", 12, 18)],
        schools=[("mullet", 1, -18, 7, 5, .5), ("mackerel", 18, -8, 8, 6, .8), ("flounder", -1, -8, 4, 3, .4),
                 ("sea_bass", 14, 10, 4, 5, 1.0), ("king_grouper", 12, 18, 1, 4, 1.6)],
        willows=[], lily=[], reeds_where=lambda z, x, y: -0.35 < z < 0.25 and x > coast_x(y) - 1,
        pines=18, rocks=12, extent=(-38, 38, -48, 44)),
}


def terrain_mesh(loc, cfg):
    H = HEIGHT[loc]
    x0, x1, y0, y1 = cfg["extent"]
    nx, ny = int(x1 - x0), int(y1 - y0)
    random.seed(11)
    bm = bmesh.new(); grid = []
    for j in range(ny + 1):
        row = []
        for i in range(nx + 1):
            x = x0 + i + (random.uniform(-0.28, 0.28) if 0 < i < nx else 0)
            y = y0 + j + (random.uniform(-0.28, 0.28) if 0 < j < ny else 0)
            row.append(bm.verts.new((x, y, H(x, y))))
        grid.append(row)
    for j in range(ny):
        for i in range(nx):
            a, b, c, d = grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]
            if (i + j) % 2: bm.faces.new([a, b, c]); bm.faces.new([a, c, d])
            else: bm.faces.new([a, b, d]); bm.faces.new([b, c, d])
    me = bpy.data.meshes.new(f"Terrain_{loc}"); bm.to_mesh(me); bm.free()
    # path mask in vertex colour R
    pts = [Vector(p) for p in cfg["path"]]
    attr = me.color_attributes.new("Col", "BYTE_COLOR", "POINT")
    for k, v in enumerate(me.vertices):
        p = Vector((v.co.x, v.co.y)); best = 9e9
        for a, b in zip(pts, pts[1:]):
            q, f = intersect_point_line(p, a, b); f = max(0, min(1, f))
            best = min(best, (p - (a + (b - a) * f)).length)
        t = sstep(1.4, 0.5, best)
        attr.data[k].color = (t, 0, 0, 1)
    for p in me.polygons: p.use_smooth = True
    return me


def export_mesh(me, path, name):
    m2 = me.copy(); m2.transform(RZ)
    o = bpy.data.objects.new(name, m2); bpy.context.scene.collection.objects.link(o)
    bpy.ops.object.select_all(action="DESELECT"); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True, axis_forward="-Z", axis_up="Y",
                             mesh_smooth_type="OFF", colors_type="LINEAR", add_leaf_bones=False, bake_anim=False)
    bpy.data.objects.remove(o); bpy.data.meshes.remove(m2)


def detail_meshes():
    """Reeds clump and lily-pad cluster, exported once."""
    M = bpy.data.materials
    def hexmat(name, hx):
        m = M.get(name) or M.new(name); m.use_nodes = True
        b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        h = hx.lstrip('#'); c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
        lin = [(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in c] + [1]
        b.inputs["Base Color"].default_value = lin; b.inputs["Roughness"].default_value = 0.85; m.diffuse_color = lin
        return m
    reed, reed2, cat, pad, petal = (hexmat("Pal_Reed", "#7E9A4E"), hexmat("Pal_ReedDry", "#B4A35E"), hexmat("Pal_Cattail", "#6A4A2E"),
                                    hexmat("Pal_LilyPad", "#4E8A4A"), hexmat("Pal_LilyFlower", "#F2C6D6"))
    random.seed(3)
    bm = bmesh.new()
    for k in range(26):
        a = random.uniform(0, 6.28); r = random.uniform(0, 0.55)
        base = Vector((math.cos(a) * r, math.sin(a) * r, -0.3))
        h = random.uniform(1.0, 1.9); lean = Vector((random.uniform(-.25, .25), random.uniform(-.25, .25), 0))
        w = 0.025
        v = [bm.verts.new(base + Vector((-w, 0, 0))), bm.verts.new(base + Vector((w, 0, 0))), bm.verts.new(base + Vector((0, 0, h)) + lean)]
        f = bm.faces.new(v); f.material_index = 0 if k % 3 else 1
        v2 = [bm.verts.new(base + Vector((0, -w, 0))), bm.verts.new(base + Vector((0, w, 0))), bm.verts.new(base + Vector((0, 0, h)) + lean)]
        f = bm.faces.new(v2); f.material_index = 0 if k % 3 else 1
        if k % 4 == 0:
            res = bmesh.ops.create_cone(bm, cap_ends=True, segments=6, radius1=0.04, radius2=0.04, depth=0.22,
                                        matrix=Matrix.Translation(base + lean * 0.85 + Vector((0, 0, h * 0.85))))
            for ff in {ff for vv in res["verts"] for ff in vv.link_faces}: ff.material_index = 2
    me = bpy.data.meshes.new("Reeds"); bm.to_mesh(me); bm.free()
    for m in (reed, reed2, cat): me.materials.append(m)
    export_mesh(me, os.path.join(ROOT, "Assets/Art/Models/Reeds.fbx"), "Reeds")
    bm = bmesh.new()
    for k in range(9):
        a = random.uniform(0, 6.28); r = random.uniform(0, 1.2)
        c = Vector((math.cos(a) * r, math.sin(a) * r, 0.02))
        pr = random.uniform(0.18, 0.32); notch = random.uniform(0, 6.28)
        ring = [bm.verts.new(c + Vector((math.cos(notch + 0.35 + t / 14 * 5.6) * pr, math.sin(notch + 0.35 + t / 14 * 5.6) * pr, 0))) for t in range(15)]
        center = bm.verts.new(c)
        for t in range(14): bm.faces.new([center, ring[t], ring[t + 1]]).material_index = 0
        if k % 3 == 0:
            res = bmesh.ops.create_cone(bm, cap_ends=True, segments=7, radius1=0.09, radius2=0.03, depth=0.08,
                                        matrix=Matrix.Translation(c + Vector((0.05, 0.05, 0.05))))
            for ff in {ff for vv in res["verts"] for ff in vv.link_faces}: ff.material_index = 1
    me = bpy.data.meshes.new("LilyPads"); bm.to_mesh(me); bm.free()
    me.materials.append(pad); me.materials.append(petal)
    export_mesh(me, os.path.join(ROOT, "Assets/Art/Models/LilyPads.fbx"), "LilyPads")


def build_location(loc, export=True):
    cfg = LAYOUT[loc]
    H = HEIGHT[loc]
    me = terrain_mesh(loc, cfg)
    old = bpy.data.objects.get(f"Terrain_{loc}")
    if old: bpy.data.objects.remove(old)
    ob = bpy.data.objects.new(f"Terrain_{loc}", me)
    col = bpy.data.collections.get("Locations") or bpy.data.collections.new("Locations")
    if col.name not in bpy.context.scene.collection.children: bpy.context.scene.collection.children.link(col)
    col.objects.link(ob)
    ob.location.x = {"lake": 120, "gorge": 220, "estuary": 320}[loc]

    random.seed(hash(loc) % 1000)
    items = []
    def add(asset, x, y, rot=0.0, scale=1.0, size=0.0, sink=0.0):
        items.append(dict(asset=asset, x=round(x, 3), y=round(y, 3), z=round(H(x, y) - sink, 3), rot=round(rot, 2), scale=round(scale, 3), size=size))
    dx, dy, drot = cfg["dock"]
    items.append(dict(asset="Dock", x=dx, y=dy, z=0.0, rot=drot, scale=1.0, size=0.0))
    for key, asset, size in (("hut", "Prop_Boathouse", 4.6), ("stall", "Prop_MarketStall", 3.0), ("mapboard", "Prop_MapBoard", 2.2)):
        x, y, r = cfg[key]; add(asset, x, y, r, 1.0, size)
    if "lighthouse" in cfg:
        x, y = cfg["lighthouse"]; add("Prop_Lighthouse", x, y, 0, 1.0, 9.0)
    for (x, y) in cfg["willows"]:
        add("Prop_Willow", x, y, random.uniform(0, 360), 1.0, random.uniform(6.0, 8.0), 0.1)
    for (x, y) in cfg["lily"]:
        items.append(dict(asset="LilyPads", x=x, y=y, z=0.0, rot=random.uniform(0, 360), scale=random.uniform(0.9, 1.4), size=0.0))
    if loc == "gorge":
        wx, wy = cfg["waterfall"]
        items.append(dict(asset="Waterfall", x=round(wx, 3), y=wy, z=round(H(wx, wy + 3.0), 3), rot=0, scale=1.0, size=5.0))
    # pines on dry ground away from water and buildings
    keep = [Vector((v[0], v[1])) for v in [cfg["dock"][:2], cfg["hut"][:2], cfg["stall"][:2], cfg["mapboard"][:2], cfg["spawn"]]]
    placed = []; tries = 0
    x0, x1, y0, y1 = cfg["extent"]
    while len(placed) < cfg["pines"] and tries < 6000:
        tries += 1
        x, y = random.uniform(x0 + 2, x1 - 2), random.uniform(y0 + 2, y1 - 2)
        z = H(x, y)
        if z < 0.9 or any((Vector((x, y)) - k).length < 6 for k in keep): continue
        if any((Vector((x, y)) - p).length < 3.2 for p in placed): continue
        placed.append(Vector((x, y)))
        a = random.choices(["Pine_Tall", "Pine_Medium", "Pine_Small"], weights=[3, 4, 3])[0]
        add(a, x, y, random.uniform(0, 360), random.uniform(0.85, 1.3), 0.0, 0.1)
    rocks = 0; tries = 0
    while rocks < cfg["rocks"] and tries < 4000:
        tries += 1
        x, y = random.uniform(x0 + 2, x1 - 2), random.uniform(y0 + 2, y1 - 2)
        z = H(x, y)
        inwater = "rocks_in_water" in cfg and cfg["rocks_in_water"][0] < y < cfg["rocks_in_water"][1] and -1.0 < z < 0
        if not (0.2 < z < 3.0 or inwater): continue
        if any((Vector((x, y)) - k).length < 5 for k in keep): continue
        rocks += 1
        add(random.choice(["Rock_Large", "Rock_Medium", "Rock_Small", "Rock_Tall"]), x, y, random.uniform(0, 360), random.uniform(0.7, 1.3), 0.0, 0.25)
    # reeds on shallow margins
    reeds = 0
    for j in range(int(y0), int(y1), 2):
        for i in range(int(x0), int(x1), 2):
            x, y = i + random.uniform(-0.8, 0.8), j + random.uniform(-0.8, 0.8)
            z = H(x, y)
            if cfg["reeds_where"](z, x, y) and noise.noise(Vector((x * 0.2, y * 0.2, 4))) > -0.1 and random.random() < 0.55:
                if any((Vector((x, y)) - k).length < 4 for k in keep[:1]): continue
                items.append(dict(asset="Reeds", x=round(x, 2), y=round(y, 2), z=round(max(z, -0.4), 2), rot=random.uniform(0, 360), scale=round(random.uniform(0.8, 1.3), 2), size=0.0))
                reeds += 1
    def wet(x, y, depth):
        # Spots and schools must sit in open water at least `depth` deep; spiral out to the nearest such point.
        if H(x, y) < -depth:
            return x, y
        for r in range(1, 30):
            best = None
            for k in range(24):
                a = k / 24 * 6.2832
                px, py = x + math.cos(a) * r, y + math.sin(a) * r
                if H(px, py) < -depth and all(H(px + dx, py + dy) < -0.25 for dx, dy in ((1.5, 0), (-1.5, 0), (0, 1.5), (0, -1.5))):
                    best = (px, py) if best is None or H(px, py) < H(*best) else best
            if best:
                return round(best[0], 2), round(best[1], 2)
        raise ValueError(f"no water near {x},{y} in {loc}")
    cfg = dict(cfg, spots=[(sp[0], sp[1], sp[2], *wet(sp[3], sp[4], 0.6)) for sp in cfg["spots"]],
               schools=[(sc[0], *wet(sc[1], sc[2], sc[5] + 0.3), sc[3], sc[4], sc[5]) for sc in cfg["schools"]])
    data = dict(location=loc, items=items, spawn=dict(x=cfg["spawn"][0], y=cfg["spawn"][1], z=H(*cfg["spawn"])),
                spots=[dict(name=s[0], nameID=s[1], habitat=s[2], x=s[3], y=s[4]) for s in cfg["spots"]],
                schools=[dict(fish=s[0], x=s[1], y=s[2], count=s[3], radius=s[4], depth=s[5]) for s in cfg["schools"]])
    if export:
        os.makedirs(os.path.join(ROOT, "ArtSource/locations"), exist_ok=True)
        json.dump(data, open(os.path.join(ROOT, f"ArtSource/locations/{loc}.json"), "w"), indent=1)
        export_mesh(me, os.path.join(ROOT, f"Assets/Art/Models/Level/Terrain_{loc}.fbx"), f"Terrain_{loc}")
    return dict(items=len(items), reeds=reeds, pines=len(placed), rocks=rocks)
