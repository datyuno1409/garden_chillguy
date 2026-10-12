"""Neat, hidden, straight UV seams for the stylised boulder (v3).

Seams are edge-loop walks: at every vertex the walk continues along the edge that keeps the current
heading, so a seam follows the mesh's own edge loops and never staircases. Layout:
- one ring around the base (ground contact, not visible),
- a few vertical relief slits on the back that start at the ring and stop where the surface becomes visible.
The dome stays one uncut island. Islands are straightened, scaled by visibility and packed axis-aligned.
Several slit layouts are tried; the best-scoring one is kept.
"""
import heapq
import math

import bmesh
import bpy
from mathutils import Vector

OBJECT = "Boulder_Retopo_Diag"
UV_NAME = "UVMap"
VIEW_DIR = Vector((0.0, -1.0, 0.9)).normalized()   # from the rock towards the player (front = -Y, above)
BOTTOM_NORMAL_Z = -0.5
HIDDEN_DOT = 0.30                                  # seam edges need every adjacent face below this against VIEW_DIR (grazing = hard to see)
VISIBLE_DOT = 0.35                                 # an edge counts as clearly facing the player above this
UNWRAP_METHOD = 'ANGLE_BASED'
STRETCH_ITERATIONS = 40
RING_HEIGHT_K = 8.0                                # how strongly the base ring is kept level
RING_WAYPOINTS = 8
MIN_STRAIGHT = math.cos(math.radians(50))          # a walk never turns sharper than this per step
MOMENTUM = 0.65                                    # weight of the previous heading (higher = straighter)
HEIGHT_PIN_K = 1.5                                 # keeps the base ring level
SLIT_TOP_FRACTION = 0.97                           # slits never climb above this fraction of the rock height
MIN_SLIT_LENGTH = 0.35
GROUND_BAND = 0.15                                 # edges this close to the ring height touch the ground
TURN_LIMIT_DEG = 35.0                              # a kink sharper than this counts as a zig-zag
BOTTOM_DENSITY = 0.45
BACK_DENSITY = 0.8
MARGIN = 0.008
GRID = 256

LAYOUTS = {
    "2 slits": [75, 105],
    "3 slits": [60, 90, 120],
    "3 wide": [40, 90, 140],
    "4 slits": [50, 80, 110, 140],
    "4 wide": [30, 70, 110, 150],
    "5 slits": [40, 65, 90, 115, 140],
}


def edge_hidden(e):
    return all(f.normal.dot(VIEW_DIR) < HIDDEN_DOT for f in e.link_faces)


def edge_visible(e):
    return any(f.normal.dot(VIEW_DIR) > VISIBLE_DOT for f in e.link_faces)


def tangent_of(v, direction):
    n = v.normal
    t = direction - n * direction.dot(n)
    return t.normalized() if t.length > 1e-9 else direction.normalized()


def walk(start, heading, allowed, stop=None, z_pin=None, close_on=None, max_steps=500):
    """Follow the straightest allowed edges from `start`. Returns (vertices, edges, closed)."""
    path, edges, used = [start], [], set()
    on_path = {start}
    h = heading.normalized()
    v = start
    for _ in range(max_steps):
        best = None
        for e in v.link_edges:
            if e in used:
                continue
            w = e.other_vert(v)
            if w in on_path and w is not close_on:
                continue
            if not allowed(e):
                continue
            d = w.co - v.co
            if d.length < 1e-9:
                continue
            d = d.normalized()
            dot = d.dot(h)
            if dot < MIN_STRAIGHT:
                continue
            score = dot - (HEIGHT_PIN_K * abs(w.co.z - z_pin) if z_pin is not None else 0.0)
            if best is None or score > best[0]:
                best = (score, e, w, d)
        if best is None:
            return path, edges, False
        _, e, w, d = best
        used.add(e)
        edges.append(e)
        path.append(w)
        on_path.add(w)
        h = (h * MOMENTUM + d * (1.0 - MOMENTUM)).normalized()
        v = w
        if close_on is not None and w is close_on and len(path) > 8:
            return path, edges, True
        if stop is not None and stop(w):
            return path, edges, False
    return path, edges, False


def azimuth_vertex(verts, centre, azimuth_deg):
    a = math.radians(azimuth_deg)
    direction = Vector((math.cos(a), math.sin(a)))
    return max(verts, key=lambda v: Vector((v.co.x - centre.x, v.co.y - centre.y)).normalized().dot(direction))


def level_path(bm, a, b, z_pin):
    """Cheapest edge path from a to b that stays at height z_pin (Dijkstra)."""
    cost, prev = {a: 0.0}, {}
    heap = [(0.0, a.index)]
    while heap:
        c, idx = heapq.heappop(heap)
        v = bm.verts[idx]
        if v is b:
            break
        if c > cost.get(v, 1e18):
            continue
        for e in v.link_edges:
            w = e.other_vert(v)
            step = e.calc_length() * (1.0 + RING_HEIGHT_K * abs((v.co.z + w.co.z) / 2 - z_pin))
            if c + step < cost.get(w, 1e18):
                cost[w] = c + step
                prev[w] = (v, e)
                heapq.heappush(heap, (c + step, w.index))
    verts, edges, v = [b], [], b
    while v in prev:
        v, e = prev[v]
        verts.append(v)
        edges.append(e)
    verts.reverse()
    edges.reverse()
    return verts, edges


def build_ring(bm, bottom, centre):
    """Level loop around the base, routed through evenly spaced waypoints on the rim of the underside."""
    rim = [v for v in bm.verts
           if any(f in bottom for f in v.link_faces) and any(f not in bottom for f in v.link_faces)]
    z_pin = sorted(v.co.z for v in rim)[len(rim) // 2]
    marks = []
    for i in range(RING_WAYPOINTS):
        w = azimuth_vertex(rim, centre, -90 + i * 360.0 / RING_WAYPOINTS)
        if w not in marks:
            marks.append(w)
    path, edges = [], []
    for a, b in zip(marks, marks[1:] + marks[:1]):
        p, e = level_path(bm, a, b, z_pin)
        path += p[:-1]
        edges += e
    return path, edges, z_pin, len(edges) > 0


def build_slits(bm, ring_path, centre, z_cut, azimuths, seam_verts):
    slits = []
    for az in azimuths:
        start = azimuth_vertex(ring_path, centre, az)
        heading = tangent_of(start, Vector((0.0, 0.0, 1.0)))
        path, edges, _ = walk(start, heading, edge_hidden,
                              stop=lambda w: w.co.z >= z_cut or (w in seam_verts and w is not start))
        length = sum(e.calc_length() for e in edges)
        if length >= MIN_SLIT_LENGTH:
            slits.append((path, edges))
            seam_verts.update(path)
    return slits


def kinks(path):
    count = 0
    for i in range(1, len(path) - 1):
        a = (path[i].co - path[i - 1].co)
        b = (path[i + 1].co - path[i].co)
        if a.length > 1e-9 and b.length > 1e-9 and math.degrees(a.angle(b)) > TURN_LIMIT_DEG:
            count += 1
    return count


def islands_from_seams(bm):
    seen, islands = set(), []
    for f in bm.faces:
        if f in seen:
            continue
        stack, group = [f], []
        seen.add(f)
        while stack:
            cur = stack.pop()
            group.append(cur)
            for e in cur.edges:
                if e.seam:
                    continue
                for n in e.link_faces:
                    if n not in seen:
                        seen.add(n)
                        stack.append(n)
        islands.append(group)
    return islands


def tri_area(a, b, c):
    return abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) * 0.5


def uv_metrics(mesh, island_of_face):
    uv = mesh.uv_layers.active.data
    rows, cells, out_of_bounds = [], {}, 0
    for p in mesh.polygons:
        pts = [uv[i].uv.copy() for i in p.loop_indices]
        a = sum(tri_area(pts[0], pts[k], pts[k + 1]) for k in range(1, len(pts) - 1))
        rows.append((island_of_face[p.index], p.area, a))
        if any(q.x < -1e-6 or q.x > 1 + 1e-6 or q.y < -1e-6 or q.y > 1 + 1e-6 for q in pts):
            out_of_bounds += 1
        for k in range(1, len(pts) - 1):
            t = (pts[0], pts[k], pts[k + 1])
            x0 = max(0, int(min(q.x for q in t) * GRID)); x1 = min(GRID - 1, int(max(q.x for q in t) * GRID))
            y0 = max(0, int(min(q.y for q in t) * GRID)); y1 = min(GRID - 1, int(max(q.y for q in t) * GRID))
            for gx in range(x0, x1 + 1):
                for gy in range(y0, y1 + 1):
                    cx, cy = (gx + 0.5) / GRID, (gy + 0.5) / GRID
                    d1 = (cx - t[1].x) * (t[0].y - t[1].y) - (t[0].x - t[1].x) * (cy - t[1].y)
                    d2 = (cx - t[2].x) * (t[1].y - t[2].y) - (t[1].x - t[2].x) * (cy - t[2].y)
                    d3 = (cx - t[0].x) * (t[2].y - t[0].y) - (t[2].x - t[0].x) * (cy - t[0].y)
                    if not ((d1 < 0 or d2 < 0 or d3 < 0) and (d1 > 0 or d2 > 0 or d3 > 0)):
                        cells.setdefault((gx, gy), set()).add(island_of_face[p.index])
    per_island = {}
    for isl, s, u in rows:                       # stretch is judged inside each island
        per_island.setdefault(isl, []).append((s, u))
    ratios = []
    for items in per_island.values():
        ts = sum(s for s, _ in items); tu = sum(u for _, u in items)
        ratios += [(u / tu) / (s / ts) for s, u in items if s > 0 and u > 0]
    ratios.sort()
    n = len(ratios)
    return {"coverage": sum(u for _, _, u in rows), "p5": ratios[int(0.05 * n)], "median": ratios[n // 2],
            "p95": ratios[int(0.95 * n)], "max": ratios[-1],
            "overlap_cells": sum(1 for s in cells.values() if len(s) > 1), "out_of_bounds_faces": out_of_bounds,
            "zero_area": sum(1 for _, _, u in rows if u <= 1e-9)}


def unwrap_and_pack(ob, island_of_face, bottom_island):
    mesh = ob.data
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.select_all(action='SELECT')
    bpy.ops.uv.unwrap(method=UNWRAP_METHOD, fill_holes=True, margin=0.0)
    bpy.ops.uv.select_all(action='SELECT')
    if STRETCH_ITERATIONS:
        bpy.ops.uv.minimize_stretch(iterations=STRETCH_ITERATIONS)
    bpy.ops.uv.average_islands_scale()
    bpy.ops.uv.align_rotation(method='AUTO')
    bpy.ops.object.mode_set(mode='OBJECT')

    uv = mesh.uv_layers.active.data
    groups = {}
    for p in mesh.polygons:
        groups.setdefault(island_of_face[p.index], []).append(p)
    for isl, polys in groups.items():
        loops = [i for p in polys for i in p.loop_indices]
        centre = sum((uv[i].uv for i in loops), Vector((0, 0))) / len(loops)
        normal = sum((p.normal * p.area for p in polys), Vector())
        if isl == bottom_island:
            k = BOTTOM_DENSITY
        elif normal.normalized().dot(VIEW_DIR) < -0.2:
            k = BACK_DENSITY
        else:
            k = 1.0
        for i in loops:
            uv[i].uv = centre + (uv[i].uv - centre) * k

    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.select_all(action='SELECT')
    bpy.ops.uv.pack_islands(rotate=True, rotate_method='ANY', margin=MARGIN, margin_method='ADD',
                            shape_method='CONCAVE', scale=True)
    bpy.ops.object.mode_set(mode='OBJECT')


def try_layout(ob, name, azimuths):
    mesh = ob.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.normal_update()
    bm.verts.ensure_lookup_table()
    bm.faces.ensure_lookup_table()
    for e in bm.edges:
        e.seam = False
    centre = sum((v.co for v in bm.verts), Vector()) / len(bm.verts)
    z_min = min(v.co.z for v in bm.verts)
    z_max = max(v.co.z for v in bm.verts)
    bottom = {f for f in bm.faces if f.normal.z < BOTTOM_NORMAL_Z}

    ring_path, ring_edges, ring_z, ring_closed = build_ring(bm, bottom, centre)
    seam_verts = set(ring_path)
    slits = build_slits(bm, ring_path, centre, z_min + SLIT_TOP_FRACTION * (z_max - z_min), azimuths, seam_verts)
    for e in ring_edges:
        e.seam = True
    for _, edges in slits:
        for e in edges:
            e.seam = True

    slit_len = sum(e.calc_length() for _, edges in slits for e in edges)
    slit_visible = sum(e.calc_length() for _, edges in slits for e in edges if edge_visible(e))
    ring_len = sum(e.calc_length() for e in ring_edges)
    ground_len = sum(e.calc_length() for e in ring_edges if all(v.co.z < ring_z + GROUND_BAND for v in e.verts))
    ring_visible = sum(e.calc_length() for e in ring_edges
                       if edge_visible(e) and not all(v.co.z < ring_z + GROUND_BAND for v in e.verts))
    kink_count = (kinks(ring_path) if ring_closed else 0) + sum(kinks(p) for p, _ in slits)
    seam_vertices = (len(ring_path) if ring_closed else 0) + sum(len(p) for p, _ in slits)

    islands = islands_from_seams(bm)
    island_of_face, bottom_island, lowest = {}, None, 9.0
    for i, group in enumerate(islands):
        nz = sum(f.normal.z * f.calc_area() for f in group) / sum(f.calc_area() for f in group)
        if nz < lowest:
            lowest, bottom_island = nz, i
        for f in group:
            island_of_face[f.index] = i
    bm.to_mesh(mesh)
    bm.free()

    unwrap_and_pack(ob, island_of_face, bottom_island)
    m = uv_metrics(mesh, island_of_face)
    visible_pct = 100.0 * (slit_visible + ring_visible) / max(slit_len + ring_len - ground_len, 1e-9)
    zig_pct = 100.0 * kink_count / max(seam_vertices, 1)
    m.update({"layout": name, "islands": len(islands), "ring_closed": ring_closed, "slits": len(slits),
              "seam_m": slit_len + ring_len, "visible_seam_pct": visible_pct, "kink_pct": zig_pct})
    m["score"] = (max(0.0, m["p95"] - 1.3) * 4 + max(0.0, 0.8 - m["p5"]) * 4 + max(0.0, m["max"] - 1.7) * 2
                  + m["overlap_cells"] * 0.01 + (0.8 - min(m["coverage"], 0.8)) * 2 + m["out_of_bounds_faces"]
                  + visible_pct * 0.04 + zig_pct * 0.05 + (0 if ring_closed else 1.0))
    return m


def run():
    ob = bpy.data.objects[OBJECT]
    if bpy.context.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    if not ob.data.uv_layers:
        ob.data.uv_layers.new(name=UV_NAME)
    results = [try_layout(ob, name, az) for name, az in LAYOUTS.items()]
    best = min(results, key=lambda r: r["score"])
    try_layout(ob, best["layout"], LAYOUTS[best["layout"]])         # leave the winner applied
    return results, best


if __name__ == "__main__" or True:
    all_results, winner = run()
    for r in all_results:
        print({k: (round(v, 3) if isinstance(v, float) else v) for k, v in r.items()})
    print("WINNER:", winner["layout"])
