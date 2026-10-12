"""Fill the edge network 'Rock_EdgeFlow' with faces, then turn every face into quads.

1. Every closed region of the edge network becomes one n-gon (faces already made by hand are kept).
   Regions are traced on the rock surface: at each point the edges are sorted around the
   rock's surface normal, and each face is walked by always turning the same way.
2. Rock edges get evenly spaced points; each region gets one inset ring of quads running
   along its rock edges; the middle is filled with quads/tris.
3. One 'simple' subdivision makes everything quads without moving the rock edges.
4. Quads are evened out on the sculpt surface while points on the rock edges stay put.

Output: object 'Rock_Retopo'. The edge network is left untouched.
"""
import math

import bmesh
import bpy
from mathutils import Vector

SOURCE = "Boulder_HiPoly"
NETWORK = "Rock_EdgeFlow"
OUTPUT = "Rock_Retopo"
SNAP_OFFSET = 0.002
EDGE_TARGET_LENGTH = 0.16    # metres between points along a rock edge (before the final subdivision)
INSET_FRACTION = 0.12        # inset ring width as a fraction of sqrt(region area)
INSET_MAX = 0.07
UNTANGLE_PASSES = 30
RELAX_ITERATIONS = 12
RELAX_FACTOR = 0.5


def surface_normal(src, co):
    _, _, normal, _ = src.closest_point_on_mesh(co)
    return normal.normalized()


def sorted_neighbours(bm, src):
    """For every vertex: its neighbours, ordered counter-clockwise around the surface normal."""
    order = {}
    for v in bm.verts:
        n = surface_normal(src, v.co)
        ref = None
        items = []
        for e in v.link_edges:
            w = e.other_vert(v)
            d = w.co - v.co
            d = (d - n * d.dot(n))
            if d.length < 1e-9:
                continue
            d.normalize()
            if ref is None:
                ref = d
            angle = math.atan2(ref.cross(d).dot(n), ref.dot(d))
            items.append((angle, w))
        order[v] = [w for _, w in sorted(items, key=lambda t: t[0])]
    return order


def trace_faces(bm, order):
    """Walk every directed edge once; each walk closes one region."""
    used, loops = set(), []
    for v in bm.verts:
        for w in order[v]:
            if (v, w) in used:
                continue
            loop, a, b = [], v, w
            while (a, b) not in used:
                used.add((a, b))
                loop.append(a)
                ring = order[b]
                i = ring.index(a)
                a, b = b, ring[(i - 1) % len(ring)]   # next edge clockwise from the one we came in on
            loops.append(loop)
    return loops


def loop_area(loop):
    total = Vector()
    for i in range(len(loop)):
        total += loop[i].co.cross(loop[(i + 1) % len(loop)].co)
    return total.length / 2


def build_cage(src, network):
    bm = bmesh.new()
    bm.from_mesh(network.data)
    handmade = {frozenset(f.verts) for f in bm.faces}
    order = sorted_neighbours(bm, src)
    loops = trace_faces(bm, order)

    # the walk also produces one 'outside' loop per connected piece; on a closed rock there is none,
    # but drop any loop that repeats a vertex (dangling edge) and report it.
    created, skipped = 0, []
    for loop in loops:
        if len(set(loop)) != len(loop) or len(loop) < 3:
            skipped.append(len(loop))
            continue
        if frozenset(loop) in handmade:
            continue
        if bm.faces.get(loop) is None:
            bm.faces.new(loop)
            created += 1
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    euler = len(bm.verts) - len(bm.edges) + len(bm.faces)
    stats = {"loops": len(loops), "faces_created": created, "faces_handmade": len(handmade),
             "faces_total": len(bm.faces), "skipped_loops": skipped, "euler_V-E+F": euler,
             "edges_without_face": sum(1 for e in bm.edges if not e.link_faces)}
    return bm, stats


def densify_edges(bm, target_length):
    """Give every rock edge evenly spaced points, about `target_length` apart."""
    by_cuts = {}
    for e in bm.edges:
        cuts = max(0, round(e.calc_length() / target_length) - 1)
        if cuts:
            by_cuts.setdefault(cuts, []).append(e)
    for cuts, edges in by_cuts.items():
        bmesh.ops.subdivide_edges(bm, edges=edges, cuts=cuts)


def inset_and_fill(bm, inset_fraction, inset_max):
    """One ring of quads along the inside of every region, then quads/tris in the middle."""
    for face in list(bm.faces):
        if not face.is_valid or len(face.verts) <= 4:
            continue
        thickness = min(inset_max, inset_fraction * math.sqrt(face.calc_area()))
        bmesh.ops.inset_individual(bm, faces=[face], thickness=thickness, depth=0.0,
                                   use_even_offset=True, use_relative_offset=False)
        # inset keeps the original face as the (smaller) centre of the region
        centre = face if face.is_valid else None
        if centre is not None and len(centre.verts) > 4:
            tris = bmesh.ops.triangulate(bm, faces=[centre])["faces"]
            bmesh.ops.join_triangles(bm, faces=tris, angle_face_threshold=math.radians(60),
                                     angle_shape_threshold=math.radians(60))


def snap_to_surface(src, co, normal, reach=0.12):
    """Project along the mesh normal (like Shrinkwrap 'Project'); nearest point only as fallback.

    Projecting keeps points from both sides of a dent from collapsing onto the same crease.
    """
    if normal.length > 1e-6:
        n = normal.normalized()
        hit, loc, _, _ = src.ray_cast(co + n * reach, -n, distance=2 * reach)
        if hit:
            return loc
    _, loc, _, _ = src.closest_point_on_mesh(co)
    return loc


def relax_on_surface(bm, src, pinned, iterations, factor):
    """Even out the quads by Laplacian smoothing, re-snapping to the rock each step."""
    for _ in range(iterations):
        bm.normal_update()
        moved = {}
        for v in bm.verts:
            if v in pinned or not v.link_edges:
                continue
            avg = sum((e.other_vert(v).co for e in v.link_edges), Vector()) / len(v.link_edges)
            moved[v] = (v.co.lerp(avg, factor), v.normal.copy())
        for v, (co, normal) in moved.items():
            v.co = snap_to_surface(src, co, normal)


def flipped_faces(bm, src):
    bm.normal_update()
    result = []
    for f in bm.faces:
        _, _, n, _ = src.closest_point_on_mesh(f.calc_center_median())
        if f.normal.dot(n) < 0.0:
            result.append(f)
    return result


def untangle(bm, src, pinned, passes):
    """Smooth only around folded faces until none are left (or passes run out)."""
    for _ in range(passes):
        bad = flipped_faces(bm, src)
        if not bad:
            return 0
        zone = set()
        for f in bad:
            for v in f.verts:
                zone.add(v)
                zone.update(e.other_vert(v) for e in v.link_edges)
        bm.normal_update()
        for v in zone:
            if v in pinned:
                continue
            avg = sum((e.other_vert(v).co for e in v.link_edges), Vector()) / len(v.link_edges)
            v.co = snap_to_surface(src, avg, v.normal)
    return len(flipped_faces(bm, src))


def segment_distance(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-12)))
    return (a + ab * t - p).length


def run():
    src = bpy.data.objects[SOURCE]
    network = bpy.data.objects[NETWORK]
    if bpy.context.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')

    bm, stats = build_cage(src, network)
    rock_edges = [(e.verts[0].co.copy(), e.verts[1].co.copy()) for e in bm.edges]
    densify_edges(bm, EDGE_TARGET_LENGTH)
    inset_and_fill(bm, INSET_FRACTION, INSET_MAX)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

    old = bpy.data.objects.get(OUTPUT)
    if old:
        bpy.data.objects.remove(old, do_unlink=True)
    mesh = bpy.data.meshes.new(OUTPUT)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(OUTPUT, mesh)
    bpy.context.scene.collection.objects.link(obj)

    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    sub = obj.modifiers.new("Quads", 'SUBSURF')
    sub.subdivision_type = 'SIMPLE'
    sub.levels = 1
    bpy.ops.object.modifier_apply(modifier=sub.name)

    bm = bmesh.new()
    bm.from_mesh(obj.data)
    pinned = {v for v in bm.verts if any(segment_distance(v.co, a, b) < 1e-4 for a, b in rock_edges)}
    relax_on_surface(bm, src, pinned, RELAX_ITERATIONS, RELAX_FACTOR)
    stats["flipped_before_untangle"] = len(flipped_faces(bm, src))
    stats["flipped_after_untangle"] = untangle(bm, src, pinned, UNTANGLE_PASSES)
    for v in bm.verts:                         # pinned points also sit on the surface
        _, loc, nrm, _ = src.closest_point_on_mesh(v.co)
        v.co = loc + nrm * SNAP_OFFSET
    stats["pinned_on_rock_edges"] = len(pinned)
    bm.to_mesh(obj.data)
    bm.free()

    for p in obj.data.polygons:
        p.use_smooth = True
    obj.show_wire = True
    obj.show_in_front = True
    obj.color = (0.2, 0.8, 1.0, 1.0)

    sides = {}
    for p in obj.data.polygons:
        sides[len(p.vertices)] = sides.get(len(p.vertices), 0) + 1
    stats["final_faces_by_sides"] = dict(sorted(sides.items()))
    return stats


print(run())
