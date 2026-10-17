"""Riverlight boundary markers: a split-rail fence (land) and a rope of floats (water).

exec(open(r"D:/CozyFishing/Tools/blender_bounds.py").read()); build_bounds(True)

Parts are written to Assets/Art/Models/Bounds/<Name>.fbx. Origins: post and buoy at their base / waterline,
rail and rope at their start, running 1 m along +Y in Blender (Unity +Z) so the builder can stretch them between posts.
"""
import os

exec(open(r"D:/CozyFishing/Tools/blender_critters.py").read())   # Part, mat, write_fbx helpers
OUT = os.path.join(ROOT, "Assets/Art/Models/Bounds")


def fence_post():
    wood, dark = mat("Pal_FenceWood", "#8A6744"), mat("Pal_FenceDark", "#5E4430")
    p = Part("Fence_Post")
    p.cone(wood, (0, 0, -0.25), (0, 0, 1.0), 0.1, 0.088, 6)          # hewn post, sunk into the ground
    p.cone(dark, (0, 0, 1.0), (0, 0, 1.08), 0.088, 0.04, 6)            # weathered, rounded cap
    p.cone(dark, (0, 0, 0.0), (0, 0, 0.07), 0.112, 0.104, 6)           # dirt-stained foot
    return p.finish(smooth=False)


def fence_rail():
    wood = mat("Pal_FenceRail", "#A07B52")
    r = Part("Fence_Rail")
    r.cone(wood, (0, 0, 0), (0, 1.0, 0), 0.055, 0.05, 5)               # split log, slightly tapered
    return r.finish(smooth=False)


def buoy():
    red, white, ink = mat("Pal_BuoyRed", "#D2553F"), mat("Pal_Cream", "#F2E8D5"), mat("Pal_Ink", "#1E1A18")
    b = Part("Buoy")
    b.ellipsoid(red, (0, 0, 0.02), (0.2, 0.2, 0.16), 12, 8)            # float body, half under water
    b.cone(white, (0, 0, 0.1), (0, 0, 0.2), 0.17, 0.12, 12)            # white band
    b.cone(red, (0, 0, 0.2), (0, 0, 0.3), 0.12, 0.05, 12)              # top
    b.cone(ink, (0, 0, 0.3), (0, 0, 0.38), 0.018, 0.018, 6)            # eye stem for the rope
    return b.finish()


def rope():
    tan = mat("Pal_Rope", "#D8C49A")
    r = Part("Buoy_Rope")
    r.cone(tan, (0, 0, 0), (0, 1.0, 0), 0.016, 0.016, 5)
    return r.finish(smooth=False)


def build_bounds(export=False):
    col = bpy.data.collections.get("Bounds") or bpy.data.collections.new("Bounds")
    if col.name not in bpy.context.scene.collection.children: bpy.context.scene.collection.children.link(col)
    col.hide_viewport = False
    os.makedirs(OUT, exist_ok=True)
    parts = {"Fence_Post": fence_post(), "Fence_Rail": fence_rail(), "Buoy": buoy(), "Buoy_Rope": rope()}
    for i, (name, me) in enumerate(parts.items()):
        if export:
            write_fbx(me, name, col, (i * 1.5, -20, 0))
        else:
            ob = bpy.data.objects.get(name) or bpy.data.objects.new(name, me)
            if ob.name not in col.objects: col.objects.link(ob)
            ob.location = (i * 1.5, -20, 0)
    print("bounds parts:", list(parts))
