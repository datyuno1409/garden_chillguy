"""Trace the facet edges of the sculpted rock into a clean edge network.

Steps: smooth face normals -> grow flat regions -> merge tiny regions -> take region borders ->
chain them between junctions -> smooth + simplify each chain -> snap to the sculpt surface.
Result: object 'Rock_EdgeFlow' (vertices + edges only) lying on the rock.
"""
import math
from collections import deque

import bmesh
import bpy
from mathutils import Vector

SOURCE = "Boulder_HiPoly"
OUTPUT = "Rock_EdgeFlow"
NORMAL_RING_DEPTH = 4        # faces over which normals are averaged (kills chisel noise)
REGION_TOLERANCE_DEG = 24    # max angle between a face and its region's mean normal
MIN_REGION_AREA_FRACTION = 0.015
MERGE_ANGLE_DEG = 22         # neighbouring regions closer than this are one facet
MIN_CREASE_DEG = 0.0         # a border must bend the surface at least this much on average (tuned below)
SIDE_DEPTH = 8               # rings of faces averaged on each side of a border to measure its crease
REFINE_ITERATIONS = 30
NEIGHBOUR_WEIGHT = 0.08      # how strongly a face sides with its neighbours (straighter borders)
CHAIN_SMOOTH_WINDOW = 13
SIMPLIFY_TOLERANCE = 0.045   # metres


def ring_average(faces, adjacency, depth):
    """Average the raw normal of every face with its neighbours up to `depth` steps away."""
    result = []
    for start in range(len(faces)):
        seen = {start}
        frontier = [start]
        total = faces[start].normal.copy()
        for _ in range(depth):
            nxt = []
            for f in frontier:
                for n in adjacency[f]:
                    if n not in seen:
                        seen.add(n)
                        nxt.append(n)
                        total += faces[n].normal
            frontier = nxt
        result.append(total)
    return result


def grow_regions(faces, adjacency, smooth_normals, flatness, tolerance_cos):
    labels = [-1] * len(faces)
    order = sorted(range(len(faces)), key=lambda i: -flatness[i])
    region_count = 0
    for seed in order:
        if labels[seed] != -1:
            continue
        mean = smooth_normals[seed].copy() * faces[seed].calc_area()
        labels[seed] = region_count
        queue = deque([seed])
        while queue:
            f = queue.popleft()
            for n in adjacency[f]:
                if labels[n] != -1:
                    continue
                if smooth_normals[n].dot(mean.normalized()) >= tolerance_cos:
                    labels[n] = region_count
                    mean += smooth_normals[n] * faces[n].calc_area()
                    queue.append(n)
        region_count += 1
    return labels, region_count


def merge_small_regions(faces, adjacency, labels, smooth_normals, min_area):
    area, normal, neighbours = {}, {}, {}
    for i, f in enumerate(faces):
        r = labels[i]
        area[r] = area.get(r, 0.0) + f.calc_area()
        normal[r] = normal.get(r, Vector()) + smooth_normals[i] * f.calc_area()
        for n in adjacency[i]:
            if labels[n] != r:
                neighbours.setdefault(r, {}).setdefault(labels[n], 0)
                neighbours[r][labels[n]] += 1
    remap = {r: r for r in area}

    def root(r):
        while remap[r] != r:
            r = remap[r]
        return r

    for r in sorted(area, key=lambda k: area[k]):
        if area[r] >= min_area or r not in neighbours:
            continue
        mine = normal[r].normalized()
        candidates = [(n, c) for n, c in neighbours[r].items() if root(n) != r]
        if not candidates:
            continue
        best = max(candidates, key=lambda nc: normal[root(nc[0])].normalized().dot(mine) + 0.002 * nc[1])
        target = root(best[0])
        remap[r] = target
        area[target] += area[r]
        normal[target] = normal[target] + normal[r]
    return [root(l) for l in labels]


def merge_similar_regions(faces, adjacency, labels, smooth_normals, max_angle_deg):
    """Repeatedly merge the two most similar neighbouring regions until every border is a real crease."""
    limit = math.cos(math.radians(max_angle_deg))
    labels = list(labels)
    while True:
        normal, pairs = {}, set()
        for i, f in enumerate(faces):
            r = labels[i]
            normal[r] = normal.get(r, Vector()) + smooth_normals[i] * f.calc_area()
            for n in adjacency[i]:
                if labels[n] != r:
                    pairs.add((min(r, labels[n]), max(r, labels[n])))
        best, best_dot = None, limit
        for a, b in pairs:
            d = normal[a].normalized().dot(normal[b].normalized())
            if d > best_dot:
                best, best_dot = (a, b), d
        if best is None:
            return labels
        a, b = best
        labels = [a if l == b else l for l in labels]


def refine_borders(faces, adjacency, labels, smooth_normals, iterations, neighbour_weight):
    """Move each face to the neighbouring region whose facet normal fits it best.

    The neighbour term straightens jagged borders; the normal term pulls them onto the real crease.
    """
    labels = list(labels)
    for _ in range(iterations):
        mean = {}
        for i, f in enumerate(faces):
            mean[labels[i]] = mean.get(labels[i], Vector()) + smooth_normals[i] * f.calc_area()
        mean = {r: v.normalized() for r, v in mean.items()}
        changed = 0
        new_labels = list(labels)
        for i in range(len(faces)):
            votes = {}
            for n in adjacency[i]:
                votes[labels[n]] = votes.get(labels[n], 0) + 1
            if len(votes) == 1 and labels[i] in votes:
                continue
            candidates = set(votes) | {labels[i]}
            best = max(candidates, key=lambda r: smooth_normals[i].dot(mean[r]) + neighbour_weight * votes.get(r, 0))
            if best != labels[i]:
                new_labels[i] = best
                changed += 1
        labels = new_labels
        if changed == 0:
            break
    return labels


def side_normal(faces, adjacency, labels, start, depth):
    """Average normal of the faces of `start`'s own region within `depth` steps of it."""
    label = labels[start]
    seen, frontier = {start}, [start]
    total = faces[start].normal * faces[start].calc_area()
    for _ in range(depth):
        nxt = []
        for f in frontier:
            for n in adjacency[f]:
                if n not in seen and labels[n] == label:
                    seen.add(n)
                    nxt.append(n)
                    total += faces[n].normal * faces[n].calc_area()
        frontier = nxt
    return total.normalized()


def border_creases(bm, faces, adjacency, labels, depth):
    """Length-weighted mean crease angle (degrees) for every pair of touching regions."""
    cache, crease = {}, {}
    for e in bm.edges:
        if len(e.link_faces) != 2:
            continue
        a, b = e.link_faces[0].index, e.link_faces[1].index
        if labels[a] == labels[b]:
            continue
        for f in (a, b):
            if f not in cache:
                cache[f] = side_normal(faces, adjacency, labels, f, depth)
        key = (min(labels[a], labels[b]), max(labels[a], labels[b]))
        angle = math.degrees(cache[a].angle(cache[b], 0.0))
        total, length = crease.get(key, (0.0, 0.0))
        crease[key] = (total + angle * e.calc_length(), length + e.calc_length())
    return {k: t / l for k, (t, l) in crease.items() if l > 0}


def drop_weak_borders(bm, faces, adjacency, labels, min_crease_deg, depth):
    """Merge region pairs whose shared border is not a real crease, weakest first."""
    labels = list(labels)
    while True:
        crease = border_creases(bm, faces, adjacency, labels, depth)
        if not crease:
            return labels, crease
        key, angle = min(crease.items(), key=lambda kv: kv[1])
        if angle >= min_crease_deg:
            return labels, crease
        keep, gone = key
        labels = [keep if l == gone else l for l in labels]


def border_edges(bm, labels):
    edges = []
    for e in bm.edges:
        if len(e.link_faces) == 2 and labels[e.link_faces[0].index] != labels[e.link_faces[1].index]:
            edges.append((e.verts[0].index, e.verts[1].index))
    return edges


def build_chains(edge_pairs):
    adj = {}
    for a, b in edge_pairs:
        adj.setdefault(a, []).append(b)
        adj.setdefault(b, []).append(a)
    nodes = {v for v, ns in adj.items() if len(ns) != 2}
    used, chains = set(), []

    def walk(start, first):
        path, prev, cur = [start], start, first
        used.add(frozenset((start, first)))
        while True:
            path.append(cur)
            if cur in nodes:
                return path
            nxt = [n for n in adj[cur] if n != prev]
            if not nxt or frozenset((cur, nxt[0])) in used:
                return path
            used.add(frozenset((cur, nxt[0])))
            prev, cur = cur, nxt[0]

    for node in nodes:
        for first in adj[node]:
            if frozenset((node, first)) not in used:
                chains.append(walk(node, first))
    for v in adj:                                   # isolated closed loops
        for first in adj[v]:
            if frozenset((v, first)) not in used:
                chains.append(walk(v, first))
    return chains, nodes


def moving_average(points, window):
    half = window // 2
    out = [points[0]]
    for i in range(1, len(points) - 1):
        lo, hi = max(0, i - half), min(len(points), i + half + 1)
        out.append(sum(points[lo:hi], Vector()) / (hi - lo))
    out.append(points[-1])
    return out


def simplify(points, tolerance):
    """Ramer-Douglas-Peucker; returns indices of the points to keep."""
    keep = {0, len(points) - 1}
    stack = [(0, len(points) - 1)]
    while stack:
        a, b = stack.pop()
        line = points[b] - points[a]
        length = line.length
        worst, worst_d = None, tolerance
        for i in range(a + 1, b):
            d = (points[i] - points[a]).length if length < 1e-9 else \
                (points[i] - points[a]).cross(line).length / length
            if d > worst_d:
                worst, worst_d = i, d
        if worst is not None:
            keep.add(worst)
            stack += [(a, worst), (worst, b)]
    return sorted(keep)


def run():
    src = bpy.data.objects[SOURCE]
    if bpy.context.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    bm = bmesh.new()
    bm.from_mesh(src.data)
    bm.normal_update()
    bm.faces.ensure_lookup_table()
    bm.verts.ensure_lookup_table()
    faces = list(bm.faces)
    adjacency = [[f.index for e in face.edges for f in e.link_faces if f is not face] for face in faces]

    ring = ring_average(faces, adjacency, NORMAL_RING_DEPTH)
    flatness = [v.length for v in ring]
    smooth = [v.normalized() for v in ring]

    labels, count = grow_regions(faces, adjacency, smooth, flatness, math.cos(math.radians(REGION_TOLERANCE_DEG)))
    total_area = sum(f.calc_area() for f in faces)
    labels = merge_small_regions(faces, adjacency, labels, smooth, total_area * MIN_REGION_AREA_FRACTION)
    labels = merge_similar_regions(faces, adjacency, labels, smooth, MERGE_ANGLE_DEG)
    labels = refine_borders(faces, adjacency, labels, smooth, REFINE_ITERATIONS, NEIGHBOUR_WEIGHT)
    before = border_creases(bm, faces, adjacency, labels, SIDE_DEPTH)
    labels, after = drop_weak_borders(bm, faces, adjacency, labels, MIN_CREASE_DEG, SIDE_DEPTH)
    labels = refine_borders(faces, adjacency, labels, smooth, REFINE_ITERATIONS, NEIGHBOUR_WEIGHT)
    merged = len(set(labels))
    print("crease per border before:", sorted(round(v, 1) for v in before.values()))
    print("crease per border kept:  ", sorted(round(v, 1) for v in after.values()))

    chains, nodes = build_chains(border_edges(bm, labels))

    out = bmesh.new()
    node_map = {}

    def node_vert(index):
        if index not in node_map:
            node_map[index] = out.verts.new(bm.verts[index].co.copy())
        return node_map[index]

    segments = 0
    for chain in chains:
        pts = [bm.verts[i].co.copy() for i in chain]
        if len(pts) < 2:
            continue
        smoothed = moving_average(pts, CHAIN_SMOOTH_WINDOW)
        keep = simplify(smoothed, SIMPLIFY_TOLERANCE)
        previous = None
        for k in keep:
            if k in (0, len(chain) - 1):
                current = node_vert(chain[k])
            else:
                _, location, _, _ = src.closest_point_on_mesh(smoothed[k])
                current = out.verts.new(location)
            if previous is not None and previous is not current and not out.edges.get((previous, current)):
                out.edges.new((previous, current))
                segments += 1
            previous = current

    old = bpy.data.objects.get(OUTPUT)
    if old:
        bpy.data.objects.remove(old, do_unlink=True)
    mesh = bpy.data.meshes.new(OUTPUT)
    out.to_mesh(mesh)
    obj = bpy.data.objects.new(OUTPUT, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.show_in_front = True
    obj.color = (1.0, 0.55, 0.1, 1.0)
    bm.free()
    out.free()
    return {"faces": len(faces), "regions_grown": count, "regions_after_merge": merged,
            "chains": len(chains), "junctions": len(nodes), "edge_segments": segments,
            "verts": len(mesh.vertices)}


print(run())
